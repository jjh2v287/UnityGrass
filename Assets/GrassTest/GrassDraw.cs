using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Mathematics;

[ExecuteInEditMode]
public class GrassDraw : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GrassPainterTest grassPainter = default;
    [SerializeField] private Mesh sourceMesh = default;
    [SerializeField] private Material material = default;
    [SerializeField] private ComputeShader computeShader = default;

    [Header("잔디 Blade")]
    public float grassHeight = 1;
    public float grassWidth = 0.06f;
    public float grassRandomHeight = 0.25f;
    [Range(0, 1)] public float bladeRadius = 0.6f;
    [Range(0, 1)] public float bladeForwardAmount = 0.38f;
    [Range(1, 5)] public float bladeCurveAmount = 2;

    [SerializeField]
    GameObject interactor;

    [Header("바람")]
    public float windSpeed = 10;
    public float windStrength = 0.05f;
    [Header("인터렉티브")]
    public float affectRadius = 0.3f;
    public float affectStrength = 5;
    [Header("LOD")]
    public float minFadeDistance = 40;
    public float maxFadeDistance = 60;
    [Header("Material")]
    public Color topTint = new Color(1, 1, 1);
    public Color bottomTint = new Color(0, 0, 1);
    public float ambientStrength = 0.1f;
    [Header("그림자")]
    public UnityEngine.Rendering.ShadowCastingMode castShadow;

    private Camera m_MainCamera;

    private readonly int m_AllowedBladesPerVertex = 6;
    private readonly int m_AllowedSegmentsPerBlade = 7;

    // 컴퓨트 셰이더에 보낼 구조
    // 이 레이아웃 종류는 데이터가 순차적으로 배치되도록 한다
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SourceVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Vector3 color;
    }

    // 컴퓨팅 버퍼가 설정되었는지 여부
    private bool m_Initialized;
    // 마우스로 찍은 원본 데이터
    private ComputeBuffer m_SourceVertBuffer;
    // 그리기용 데이터
    private ComputeBuffer m_DrawBuffer;
    // draw arguments 데이터
    private ComputeBuffer m_ArgsBuffer;
    // 데이터가 고유한 컴퓨팅 버퍼에 속하도록 셰이더를 인스턴스화
    private ComputeShader m_InstantiatedComputeShader;
    private Material m_InstantiatedMaterial;

    private int m_IdGrassKernel;
    // 컴퓨트 셰이더의 x 디스패치 크기
    private int m_DispatchSize;
    private Bounds m_LocalBounds;

    private Camera sceneCam;

    private const int SOURCE_VERT_STRIDE = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 2 + 3) * 3);
    private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;

    // 매 프레임마다 args 버퍼를 재설정하기 위한 데이터
    // 0: 그리기 인스턴스당 정점 수. 하나의 인스턴스만 사용하겠다
    // 1: 인스턴스 수. 하나
    // 2: 그래픽 버퍼를 사용하는 경우 정점 위치 시작
    // 3: 그래픽 버퍼를 사용하는 경우 인스턴스 위치 시작
    private int[] argsBufferReset = new int[] { 0, 1, 0, 0 };

#if UNITY_EDITOR
    SceneView view;


    void OnFocus()
    {
        SceneView.duringSceneGui -= this.OnScene;
        SceneView.duringSceneGui += this.OnScene;
    }

    void OnDestroy()
    {
        SceneView.duringSceneGui -= this.OnScene;
    }

    void OnScene(SceneView scene)
    {
        view = scene;

    }

#endif
    private void OnValidate()
    {
        m_MainCamera = Camera.main;
        grassPainter = GetComponent<GrassPainterTest>();
        sourceMesh = GetComponent<MeshFilter>().sharedMesh;
    }

    private void OnEnable()
    {
        if (m_Initialized)
        {
            OnDisable();
        }
#if UNITY_EDITOR
        SceneView.duringSceneGui += this.OnScene;
#endif
        m_MainCamera = Camera.main;

        if (grassPainter == null || sourceMesh == null || computeShader == null || material == null)
        {
            return;
        }
        sourceMesh = GetComponent<MeshFilter>().sharedMesh;

        if (sourceMesh.vertexCount == 0)
        {
            return;
        }

        m_Initialized = true;

        // 자체 버퍼를 가리킬 수 있도록 셰이더를 인스턴스화
        m_InstantiatedComputeShader = Instantiate(computeShader);
        m_InstantiatedMaterial = Instantiate(material);

        // 원본 메시에서 데이터 가져오기
        Vector3[] positions = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector2[] uvs = sourceMesh.uv;
        Color[] colors = sourceMesh.colors;

        // 원본 vert 버퍼로 업로드할 데이터를 생성한다
        SourceVertex[] vertices = new SourceVertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Color color = colors[i];
            vertices[i] = new SourceVertex()
            {
                position = positions[i],
                normal = normals[i],
                uv = uvs[i],
                color = new Vector3(color.r, color.g, color.b)
            };
        }

        int numSourceVertices = vertices.Length;

        // 각 세그먼트에는 두 개의 점이 있다
        int maxBladesPerVertex = Mathf.Max(1, m_AllowedBladesPerVertex);
        int maxSegmentsPerBlade = Mathf.Max(1, m_AllowedSegmentsPerBlade);
        int maxBladeTriangles = maxBladesPerVertex * ((maxSegmentsPerBlade - 1) * 2 + 1);

        // 버퍼 요소 하나의 크기. 쉐이더 내 버퍼 타입의 크기와 일치해야 한다.
        // 예를 들어 float3 맴버변수로 갖고 있는 구조체를 사용할 경우
        // stride의 값은 sizeof(float) * 3
        m_SourceVertBuffer = new ComputeBuffer(vertices.Length, SOURCE_VERT_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        m_SourceVertBuffer.SetData(vertices);

        m_DrawBuffer = new ComputeBuffer(numSourceVertices * maxBladeTriangles, DRAW_STRIDE, ComputeBufferType.Append);
        m_DrawBuffer.SetCounterValue(0);

        m_ArgsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

        // 커널ID 저장
        m_IdGrassKernel = m_InstantiatedComputeShader.FindKernel("Main");

        // ComputeShader
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_SourceVertices", m_SourceVertBuffer);
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_DrawTriangles", m_DrawBuffer);
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_IndirectArgsBuffer", m_ArgsBuffer);
        m_InstantiatedComputeShader.SetInt("_NumSourceVertices", numSourceVertices);
        m_InstantiatedComputeShader.SetInt("_MaxBladesPerVertex", maxBladesPerVertex);
        m_InstantiatedComputeShader.SetInt("_MaxSegmentsPerBlade", maxSegmentsPerBlade);

        // 일반 Lit쉐이더
        m_InstantiatedMaterial.SetBuffer("_DrawTriangles", m_DrawBuffer);
        m_InstantiatedMaterial.SetColor("_TopTint", topTint);
        m_InstantiatedMaterial.SetColor("_BottomTint", bottomTint);
        m_InstantiatedMaterial.SetFloat("_AmbientStrength", ambientStrength);


        // 사용할 스레드 수를 계산 커널에서 스레드 크기 가져오기
        // 그런 다음 삼각형의 수를 해당 크기로 나눈다
        m_InstantiatedComputeShader.GetKernelThreadGroupSizes(m_IdGrassKernel, out uint threadGroupSize, out _, out _);
        m_DispatchSize = Mathf.CeilToInt((float)numSourceVertices / threadGroupSize);

        // 원본 메시의 경계를 가져온 다음 최대 블레이드 너비와 높이만큼 확장
        m_LocalBounds = sourceMesh.bounds;
        m_LocalBounds.Expand(Mathf.Max(grassHeight + grassRandomHeight, grassWidth));

        SetGrassDataBase();
    }

    private void OnDisable()
    {
        if (m_Initialized)
        {
            if (Application.isPlaying)
            {
                Destroy(m_InstantiatedComputeShader);
                Destroy(m_InstantiatedMaterial);
            }
            else
            {
                DestroyImmediate(m_InstantiatedComputeShader);
                DestroyImmediate(m_InstantiatedMaterial);
            }

            m_SourceVertBuffer?.Release();
            m_DrawBuffer?.Release();
            m_ArgsBuffer?.Release();
        }

        m_Initialized = false;
    }

    private void LateUpdate()
    {
        if (Application.isPlaying == false)
        {
            OnDisable();
            OnEnable();
        }

        if (!m_Initialized)
        {
            return;
        }

        // 마지막 프레임에 데이터의 그리기 및 간접 인수 버퍼를 지운다
        m_DrawBuffer.SetCounterValue(0);
        m_ArgsBuffer.SetData(argsBufferReset);

        Bounds bounds = TransformBounds(m_LocalBounds);

        SetGrassDataUpdate();

        // 잔디 셰이더를 디스패치 GPU에서 실행
        m_InstantiatedComputeShader.Dispatch(m_IdGrassKernel, m_DispatchSize, 1, 1);

        // 실제 그리기 생성된 메시에 대한 그리기 호출해서 대기열에 넣기
        Graphics.DrawProceduralIndirect(
            m_InstantiatedMaterial, bounds, MeshTopology.Triangles, m_ArgsBuffer, 0, null, null, castShadow, true, gameObject.layer
            );
    }

    private void SetGrassDataBase()
    {
        m_InstantiatedComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        m_InstantiatedComputeShader.SetFloat("_Time", Time.time);

        m_InstantiatedComputeShader.SetFloat("_GrassHeight", grassHeight);
        m_InstantiatedComputeShader.SetFloat("_GrassWidth", grassWidth);
        m_InstantiatedComputeShader.SetFloat("_GrassRandomHeight", grassRandomHeight);

        m_InstantiatedComputeShader.SetFloat("_WindSpeed", windSpeed);
        m_InstantiatedComputeShader.SetFloat("_WindStrength", windStrength);

        m_InstantiatedComputeShader.SetFloat("_InteractorRadius", affectRadius);
        m_InstantiatedComputeShader.SetFloat("_InteractorStrength", affectStrength);

        m_InstantiatedComputeShader.SetFloat("_BladeRadius", bladeRadius);
        m_InstantiatedComputeShader.SetFloat("_BladeForward", bladeForwardAmount);
        m_InstantiatedComputeShader.SetFloat("_BladeCurve", Mathf.Max(0, bladeCurveAmount));

        m_InstantiatedComputeShader.SetFloat("_MinFadeDist", minFadeDistance);
        m_InstantiatedComputeShader.SetFloat("_MaxFadeDist", maxFadeDistance);

    }

    private void SetGrassDataUpdate()
    {
        //  m_InstantiatedComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        m_InstantiatedComputeShader.SetFloat("_Time", Time.time);
        if (interactor != null)
        {
            m_InstantiatedComputeShader.SetVector("_PositionMoving", interactor.transform.position);
        }
        else
        {
            m_InstantiatedComputeShader.SetVector("_PositionMoving", Vector3.zero);
        }

        if (m_MainCamera != null)
        {
            m_InstantiatedComputeShader.SetVector("_CameraPositionWS", m_MainCamera.transform.position);
            m_InstantiatedComputeShader.SetVector("_CameraForward", m_MainCamera.transform.forward);

        }
#if UNITY_EDITOR
        // 기본 카메라가 없는 경우(게임 플레이 중에 추가됨) 장면 카메라를 사용
        else if (view != null)
        {
            m_InstantiatedComputeShader.SetVector("_CameraPositionWS", view.camera.transform.position);
            m_InstantiatedComputeShader.SetVector("_CameraForward", view.camera.transform.forward);
        }
#endif

    }


    private Bounds TransformBounds(Bounds boundsOS)
    {
        var center = transform.TransformPoint(boundsOS.center);

        // 로컬 범위의 축을 변환
        var extents = boundsOS.extents;
        var axisX = transform.TransformVector(extents.x, 0, 0);
        var axisY = transform.TransformVector(0, extents.y, 0);
        var axisZ = transform.TransformVector(0, 0, extents.z);

        // 월드 범위를 얻기 위해 절대 값을 더함
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds { center = center, extents = extents };
    }
}
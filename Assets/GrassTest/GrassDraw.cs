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

    [Header("�ܵ� Blade")]
    public float grassHeight = 1;
    public float grassWidth = 0.06f;
    public float grassRandomHeight = 0.25f;
    [Range(0, 1)] public float bladeRadius = 0.6f;
    [Range(0, 1)] public float bladeForwardAmount = 0.38f;
    [Range(1, 5)] public float bladeCurveAmount = 2;

    [SerializeField]
    GameObject interactor;

    [Header("�ٶ�")]
    public float windSpeed = 10;
    public float windStrength = 0.05f;
    [Header("���ͷ�Ƽ��")]
    public float affectRadius = 0.3f;
    public float affectStrength = 5;
    [Header("LOD")]
    public float minFadeDistance = 40;
    public float maxFadeDistance = 60;
    [Header("Material")]
    public Color topTint = new Color(1, 1, 1);
    public Color bottomTint = new Color(0, 0, 1);
    public float ambientStrength = 0.1f;
    [Header("�׸���")]
    public UnityEngine.Rendering.ShadowCastingMode castShadow;

    private Camera m_MainCamera;

    private readonly int m_AllowedBladesPerVertex = 6;
    private readonly int m_AllowedSegmentsPerBlade = 7;

    // ��ǻƮ ���̴��� ���� ����
    // �� ���̾ƿ� ������ �����Ͱ� ���������� ��ġ�ǵ��� �Ѵ�
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SourceVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Vector3 color;
    }

    // ��ǻ�� ���۰� �����Ǿ����� ����
    private bool m_Initialized;
    // ���콺�� ���� ���� ������
    private ComputeBuffer m_SourceVertBuffer;
    // �׸���� ������
    private ComputeBuffer m_DrawBuffer;
    // draw arguments ������
    private ComputeBuffer m_ArgsBuffer;
    // �����Ͱ� ������ ��ǻ�� ���ۿ� ���ϵ��� ���̴��� �ν��Ͻ�ȭ
    private ComputeShader m_InstantiatedComputeShader;
    private Material m_InstantiatedMaterial;

    private int m_IdGrassKernel;
    // ��ǻƮ ���̴��� x ����ġ ũ��
    private int m_DispatchSize;
    private Bounds m_LocalBounds;

    private Camera sceneCam;

    private const int SOURCE_VERT_STRIDE = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 2 + 3) * 3);
    private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;

    // �� �����Ӹ��� args ���۸� �缳���ϱ� ���� ������
    // 0: �׸��� �ν��Ͻ��� ���� ��. �ϳ��� �ν��Ͻ��� ����ϰڴ�
    // 1: �ν��Ͻ� ��. �ϳ�
    // 2: �׷��� ���۸� ����ϴ� ��� ���� ��ġ ����
    // 3: �׷��� ���۸� ����ϴ� ��� �ν��Ͻ� ��ġ ����
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

        // ��ü ���۸� ����ų �� �ֵ��� ���̴��� �ν��Ͻ�ȭ
        m_InstantiatedComputeShader = Instantiate(computeShader);
        m_InstantiatedMaterial = Instantiate(material);

        // ���� �޽ÿ��� ������ ��������
        Vector3[] positions = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector2[] uvs = sourceMesh.uv;
        Color[] colors = sourceMesh.colors;

        // ���� vert ���۷� ���ε��� �����͸� �����Ѵ�
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

        // �� ���׸�Ʈ���� �� ���� ���� �ִ�
        int maxBladesPerVertex = Mathf.Max(1, m_AllowedBladesPerVertex);
        int maxSegmentsPerBlade = Mathf.Max(1, m_AllowedSegmentsPerBlade);
        int maxBladeTriangles = maxBladesPerVertex * ((maxSegmentsPerBlade - 1) * 2 + 1);

        // ���� ��� �ϳ��� ũ��. ���̴� �� ���� Ÿ���� ũ��� ��ġ�ؾ� �Ѵ�.
        // ���� ��� float3 �ɹ������� ���� �ִ� ����ü�� ����� ���
        // stride�� ���� sizeof(float) * 3
        m_SourceVertBuffer = new ComputeBuffer(vertices.Length, SOURCE_VERT_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        m_SourceVertBuffer.SetData(vertices);

        m_DrawBuffer = new ComputeBuffer(numSourceVertices * maxBladeTriangles, DRAW_STRIDE, ComputeBufferType.Append);
        m_DrawBuffer.SetCounterValue(0);

        m_ArgsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

        // Ŀ��ID ����
        m_IdGrassKernel = m_InstantiatedComputeShader.FindKernel("Main");

        // ComputeShader
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_SourceVertices", m_SourceVertBuffer);
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_DrawTriangles", m_DrawBuffer);
        m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_IndirectArgsBuffer", m_ArgsBuffer);
        m_InstantiatedComputeShader.SetInt("_NumSourceVertices", numSourceVertices);
        m_InstantiatedComputeShader.SetInt("_MaxBladesPerVertex", maxBladesPerVertex);
        m_InstantiatedComputeShader.SetInt("_MaxSegmentsPerBlade", maxSegmentsPerBlade);

        // �Ϲ� Lit���̴�
        m_InstantiatedMaterial.SetBuffer("_DrawTriangles", m_DrawBuffer);
        m_InstantiatedMaterial.SetColor("_TopTint", topTint);
        m_InstantiatedMaterial.SetColor("_BottomTint", bottomTint);
        m_InstantiatedMaterial.SetFloat("_AmbientStrength", ambientStrength);


        // ����� ������ ���� ��� Ŀ�ο��� ������ ũ�� ��������
        // �׷� ���� �ﰢ���� ���� �ش� ũ��� ������
        m_InstantiatedComputeShader.GetKernelThreadGroupSizes(m_IdGrassKernel, out uint threadGroupSize, out _, out _);
        m_DispatchSize = Mathf.CeilToInt((float)numSourceVertices / threadGroupSize);

        // ���� �޽��� ��踦 ������ ���� �ִ� ���̵� �ʺ�� ���̸�ŭ Ȯ��
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

        // ������ �����ӿ� �������� �׸��� �� ���� �μ� ���۸� �����
        m_DrawBuffer.SetCounterValue(0);
        m_ArgsBuffer.SetData(argsBufferReset);

        Bounds bounds = TransformBounds(m_LocalBounds);

        SetGrassDataUpdate();

        // �ܵ� ���̴��� ����ġ GPU���� ����
        m_InstantiatedComputeShader.Dispatch(m_IdGrassKernel, m_DispatchSize, 1, 1);

        // ���� �׸��� ������ �޽ÿ� ���� �׸��� ȣ���ؼ� ��⿭�� �ֱ�
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
        // �⺻ ī�޶� ���� ���(���� �÷��� �߿� �߰���) ��� ī�޶� ���
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

        // ���� ������ ���� ��ȯ
        var extents = boundsOS.extents;
        var axisX = transform.TransformVector(extents.x, 0, 0);
        var axisY = transform.TransformVector(0, extents.y, 0);
        var axisZ = transform.TransformVector(0, 0, extents.z);

        // ���� ������ ��� ���� ���� ���� ����
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds { center = center, extents = extents };
    }
}
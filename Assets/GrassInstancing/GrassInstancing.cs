using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.SocialPlatforms;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SimpleRandomThreadSafe;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class IndexVector4
{
    public Dictionary<int, Vector4> pos;
}

public class QtreeNode
{
    public Bounds bounds;
    public QtreeNode[] children;
    public Dictionary<int, Vector4> positions;
    public List<List<IndexVector4>> IndexPos;
    public Dictionary<int, Vector4> Hash;
    public KDTree.KDTree kdTree;
    public ComputeBuffer positionBuffer; // 위치&스케일 버퍼
    public Material instantiatedMaterial;
    public int CurrentDepth;
}

[ExecuteInEditMode]
public class GrassInstancing : MonoBehaviour
{
    [Range(1, 100_000)]
    public int instanceCount = 10_000;
    [Range(0.5f, 2.0f)]
    public float scaleMin = 0.5f;
    [Range(0.5f, 2.0f)]
    public float scaleMax = 1.2f;
    [Range(10.0f, 500.0f)]
    public float MaxDistance = 500.0f;
    [Range(0.5f, 10.0f)]
    public float WindSpeed = 1.5f;
    [Range(1, 500)]
    public int WindSize = 5;
    [Range(1, 500)]
    public int BoundSize = 10;
    public int maxDepth = 5;

    [Range(0.0f, 1.0f)]
    public float Range = 0.5f;

    [SerializeField] private Mesh sourceMesh = default;
    [SerializeField] private Material material = default;
    [SerializeField] private Texture texture = default;
    [SerializeField] private Texture windtexture = default;
    [SerializeField] private Texture HiZtexture = default;
    [SerializeField] private ComputeShader computeShader = default;
    [SerializeField] private bool shadow = true;
    [SerializeField] private ShadowCastingMode shadowcastingmode = ShadowCastingMode.On;
    [SerializeField] private Terrain terrain;
    [SerializeField] private bool iskdtree;
    [HideInInspector]
    public int subMeshIndex = 0;
    [HideInInspector]
    public Bounds renderBounds = new Bounds(Vector3.zero, Vector3.one * 50f);

    private HiZOcclusion m_HiZOcclusion;
    private Camera m_MainCamera;
    private ComputeBuffer argsBuffer;     // 메시 데이터 버퍼
    private ComputeBuffer positionBuffer; // 위치&스케일 버퍼
    private uint[] argsData = new uint[5];

    // 쿼드 트리 관련
    private QtreeNode rootNode = default;
    private List<QtreeNode> renderNode;
    private int renderMaxCount = 1024;
    private List<QtreeNode> DataNode;

    // 그림자가 나오게 하기위한 매직ㅜ
    MaterialPropertyBlock mpbs;

    // 쓰레드
    readonly ConcurrentQueue<int> Nodes = new();
    readonly ConcurrentQueue<int> RenderNodes = new();
    public Thread GrassBuildThread;

    private void OnDestroy()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= this.OnScene;
#endif
        if (argsBuffer != null)
            argsBuffer.Release();

        if (positionBuffer != null)
            positionBuffer.Release();
    }

#if UNITY_EDITOR
    void OnFocus()
    {
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.duringSceneGui -= this.OnScene;
        // Add (or re-add) the delegate.
        SceneView.duringSceneGui += this.OnScene;
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += this.OnScene;
    }
#endif

    void Start()
    {
        Init();
    }

    private void OnValidate()
    {
        CameraSetup();
    }

    private void Init()
    {
        renderBounds.size = new Vector3(BoundSize * 2, BoundSize * 2, BoundSize * 2);

        int minwidth = (int)renderBounds.size.x;
        for (int i = 0; i < maxDepth; i++)
            minwidth = minwidth / 2;
        minwidth = (int)renderBounds.size.x / minwidth != 0 ? minwidth : 1;
        renderMaxCount = minwidth * minwidth;

        CameraSetup();

        if (sourceMesh == null)
            sourceMesh = GetGrassQuadMeshCache();

        InitArgsBuffer();
        //InitPositionBuffer();
        InitTree();
        InitTreeNodeData().Forget();

        //GrassBuildThread = new Thread(new ThreadStart(GrassPositionBuild)) { IsBackground = true };
        //GrassBuildThread.Start();
    }

    private void InitTree()
    {
        if (renderNode == null)
        {
            renderNode = new List<QtreeNode>();
            renderNode.Capacity = renderMaxCount;
        }

        if (rootNode == null)
        {
            Bounds initialBounds = new Bounds(gameObject.transform.position, renderBounds.size);

            rootNode = new QtreeNode();
            rootNode.bounds = initialBounds;
            rootNode.CurrentDepth = 0;
            InitializeNode(rootNode, 0);
        }
    }

    private async UniTask InitTreeNodeData()
    {
        for(int icount=0; icount < DataNode.Count; icount++)
        {
            QtreeNode node = DataNode[icount];
            List<KDTree.KDTreePosition> points = new List<KDTree.KDTreePosition>();
            if (!iskdtree)
            {
                node.positions = new Dictionary<int, Vector4>();
                //node.positions.Capacity = instanceCount;
                node.Hash = new Dictionary<int, Vector4>();
                node.Hash.EnsureCapacity(instanceCount);
            }
            else
            {
                node.Hash = new Dictionary<int, Vector4>();
                node.Hash.EnsureCapacity(instanceCount);
                node.kdTree = new KDTree.KDTree();
            }
            // XYZ : 위치, W : 스케일
            Vector3 boundsMin = node.bounds.min;
            Vector3 boundsMax = node.bounds.max;
            float maxY = 0.0f;
            float minY = 0.0f;
            for (int i = 0; i < instanceCount; i++)
            {
                Vector4 pos = new Vector4();
                pos.x = UnityEngine.Random.Range(boundsMin.x, boundsMax.x);
                pos.z = UnityEngine.Random.Range(boundsMin.z, boundsMax.z);
                pos.y = 0.0f;

                if (terrain != null)
                    pos.y = GetWorldHeightOnTerrain(terrain, transform.position + new Vector3(pos.x, pos.y, pos.z));

                maxY = maxY < pos.y ? pos.y : maxY;
                minY = minY > pos.y ? pos.y : minY;

                pos.w = UnityEngine.Random.Range(scaleMin, scaleMax); // Scale

                // 해쉬로 중복 검사
                int code = pos.GetHashCode();
                while (node.Hash.ContainsKey(code))
                {
                    pos.x = UnityEngine.Random.Range(boundsMin.x, boundsMax.x);
                    pos.z = UnityEngine.Random.Range(boundsMin.z, boundsMax.z);
                    code = pos.GetHashCode();
                }

                if (!iskdtree)
                {
                    node.positions.Add(i, pos);
                    node.Hash.Add(code, pos);
                }
                else
                {
                    node.Hash.Add(code, pos);
                    points.Add(new KDTree.KDTreePosition(new Vector4(pos.x, pos.y, pos.z, pos.w), code));
                }
            }
            if (!iskdtree)
            {
                node.Hash.Clear();
            }
            else
            {
                node.kdTree.Build(points);
                points.Clear();
            }

            // 프러스텀 컬링에서 잘 걸리기 위해
            //float newMaxY = Mathf.Max(Mathf.Max(Mathf.Abs(minY), maxY) , 4.0f) * 2;
            float newMaxY = Mathf.Max(node.bounds.size.x, node.bounds.size.z);
            node.bounds.size = new Vector3(node.bounds.size.x, newMaxY, node.bounds.size.z);

            node.instantiatedMaterial = Instantiate(material);
            node.positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);
            if (!iskdtree)
                node.positionBuffer.SetData(node.positions.Values.ToList());
            else
                node.positionBuffer.SetData(node.Hash.Values.ToArray());

            node.instantiatedMaterial.SetBuffer("positionBuffer", node.positionBuffer);
            node.instantiatedMaterial.SetTexture("_MainTexture", texture);
            node.instantiatedMaterial.SetTexture("_WindTexture", windtexture);
            node.instantiatedMaterial.SetFloat("_WindSpeed", WindSpeed);
            node.instantiatedMaterial.SetFloat("_WorldSize", renderBounds.size.x);
            node.instantiatedMaterial.SetFloat("_WindSize", WindSize);
            await UniTask.Yield();
        }
    }

    void GrassPositionBuild()
    {
        Parallel.ForEach(DataNode, node => {
            //if (Nodes.TryDequeue(out _))
            //{
            //    //Debug.Log(node);
            //}

            //QtreeNode node = DataNode[icount];
            List<KDTree.KDTreePosition> points = new List<KDTree.KDTreePosition>();
            if (!iskdtree)
            {
                node.positions = new Dictionary<int, Vector4>();
                //node.positions.Capacity = instanceCount;
                node.Hash = new Dictionary<int, Vector4>();
                node.Hash.EnsureCapacity(instanceCount);
            }
            else
            {
                node.Hash = new Dictionary<int, Vector4>();
                node.Hash.EnsureCapacity(instanceCount);
                node.kdTree = new KDTree.KDTree();
            }
            // XYZ : 위치, W : 스케일
            Vector3 boundsMin = node.bounds.min;
            Vector3 boundsMax = node.bounds.max;
            float maxY = 0.0f;
            float minY = 0.0f;
            for (int i = 0; i < instanceCount; i++)
            {
                Vector4 pos = new Vector4();
                pos.x = (float)Rnd.Between(boundsMin.x, boundsMax.x);
                pos.z = (float)Rnd.Between(boundsMin.z, boundsMax.z);
                pos.y = 0.0f;

                if (terrain != null)
                    pos.y = GetWorldHeightOnTerrain(terrain, transform.position + new Vector3(pos.x, pos.y, pos.z));

                maxY = maxY < pos.y ? pos.y : maxY;
                minY = minY > pos.y ? pos.y : minY;

                pos.w = (float)Rnd.Between(scaleMin, scaleMax); // Scale

                // 해쉬로 중복 검사
                int code = pos.GetHashCode();
                while (node.Hash.ContainsKey(code))
                {
                    pos.x = (float)Rnd.Between(boundsMin.x, boundsMax.x);
                    pos.z = (float)Rnd.Between(boundsMin.z, boundsMax.z);
                    code = pos.GetHashCode();
                }

                if (!iskdtree)
                {
                    node.positions.Add(i,   pos);
                    node.Hash.Add(code, pos);
                }
                else
                {
                    node.Hash.Add(code, pos);
                    points.Add(new KDTree.KDTreePosition(new Vector4(pos.x, pos.y, pos.z, pos.w), code));
                }
            }
            if (!iskdtree)
            {
                node.Hash.Clear();
            }
            else
            {
                node.kdTree.Build(points);
                points.Clear();
            }

            // 프러스텀 컬링에서 잘 걸리기 위해
            //float newMaxY = Mathf.Max(Mathf.Max(Mathf.Abs(minY), maxY) , 4.0f) * 2;
            float newMaxY = Mathf.Max(node.bounds.size.x, node.bounds.size.z);
            node.bounds.size = new Vector3(node.bounds.size.x, newMaxY, node.bounds.size.z);

            node.instantiatedMaterial = Instantiate(material);
            node.positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);
            if (!iskdtree)
                node.positionBuffer.SetData(node.positions.Values.ToList());
            else
                node.positionBuffer.SetData(node.Hash.Values.ToArray());

            node.instantiatedMaterial.SetBuffer("positionBuffer", node.positionBuffer);
            node.instantiatedMaterial.SetTexture("_MainTexture", texture);
            node.instantiatedMaterial.SetTexture("_WindTexture", windtexture);
            node.instantiatedMaterial.SetFloat("_WindSpeed", WindSpeed);
            node.instantiatedMaterial.SetFloat("_WorldSize", renderBounds.size.x);
            node.instantiatedMaterial.SetFloat("_WindSize", WindSize);
        });
    }
    private void CameraSetup()
    {
        m_MainCamera = Camera.main;
        m_HiZOcclusion = m_MainCamera.GetComponent<HiZOcclusion>();
        //if (m_MainCamera == null)
        //{
        //    URPCameraStacker urpcamerastacker = GameObject.FindObjectOfType<URPCameraStacker>();
        //    if (urpcamerastacker != null)
        //        m_MainCamera = urpcamerastacker.GetComponent<Camera>();
        //    m_MainCamera.farClipPlane = 1500.0f;
        //    m_MainCamera.useOcclusionCulling = false;
        //}
    }

    private void InitArgsBuffer()
    {
        if (argsBuffer == null)
            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

        argsData[0] = (uint)sourceMesh.GetIndexCount(subMeshIndex);
        argsData[1] = (uint)instanceCount;
        argsData[2] = (uint)sourceMesh.GetIndexStart(subMeshIndex);
        argsData[3] = (uint)sourceMesh.GetBaseVertex(subMeshIndex);
        argsData[4] = 0;

        argsBuffer.SetData(argsData);

        if(mpbs == null)
            mpbs = new MaterialPropertyBlock();
    }

    /// <summary> 위치, 스케일 데이터 버퍼 생성 </summary>
    private void InitPositionBuffer()
    {
        if (positionBuffer != null)
            positionBuffer.Release();

#if UNITY_EDITOR
        debubPos.Clear();
#endif

        Vector4[] positions = new Vector4[instanceCount];
        Vector3 boundsMin = renderBounds.min;
        Vector3 boundsMax = renderBounds.max;

        // XYZ : 위치, W : 스케일
        for (int i = 0; i < instanceCount; i++)
        {
            ref Vector4 pos = ref positions[i];
            pos.x = UnityEngine.Random.Range(boundsMin.x, boundsMax.x);
            pos.y = 0.0f;// UnityEngine.Random.Range( boundsMin.y, boundsMax.y );
            pos.z = UnityEngine.Random.Range(boundsMin.z, boundsMax.z);
            pos.w = UnityEngine.Random.Range(scaleMin, scaleMax); // Scale

#if UNITY_EDITOR
            debubPos.Add(pos);
#endif
        }

        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);
        positionBuffer.SetData(positions);

        material.SetBuffer("positionBuffer", positionBuffer);
        material.SetTexture("_MainTexture", texture);
        material.SetTexture("_WindTexture", windtexture);
        material.SetFloat("_WindSpeed", WindSpeed);
        material.SetFloat("_WorldSize", renderBounds.size.x);
        material.SetFloat("_WindSize", WindSize);
    }

    Mesh GetGrassTriangleMeshCache()
    {
        if (!sourceMesh)
        {
            //if not exist, create a 3 vertices hardcode triangle grass mesh
            sourceMesh = new Mesh();

            //single grass (vertices)
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-0.25f, 0);
            verts[1] = new Vector3(+0.25f, 0);
            verts[2] = new Vector3(-0.0f, 1);

            // UV coordinates
            Vector2[] uvs = new Vector2[3];
            uvs[0] = new Vector2(0, 0);
            uvs[1] = new Vector2(1, 0);
            uvs[2] = new Vector2(0.5f, 1);

            // Normal vectors
            Vector3[] normals = new Vector3[3];
            Vector3 normal = Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).normalized;
            normals[0] = normal;
            normals[1] = normal;
            normals[2] = normal;

            //single grass (Triangle index)
            int[] trinagles = new int[3] { 2, 1, 0, }; //order to fit Cull Back in grass shader

            sourceMesh.SetVertices(verts);
            sourceMesh.SetUVs(0, uvs);
            sourceMesh.SetNormals(normals);
            sourceMesh.SetTriangles(trinagles, 0);
        }

        return sourceMesh;
    }

    Mesh GetGrassQuadMeshCache()
    {
        if (!sourceMesh)
        {
            // If not exist, create a 4 vertices hardcode rectangle grass mesh
            sourceMesh = new Mesh();

            // Single grass (vertices)
            Vector3[] verts = new Vector3[4];
            verts[0] = new Vector3(-0.5f, 0.0f, 0.0f);
            verts[1] = new Vector3(-0.5f, 1.0f, 0.0f);
            verts[2] = new Vector3(0.5f, 1.0f, 0.0f);
            verts[3] = new Vector3(0.5f, 0.0f, 0.0f);

            // UV coordinates
            Vector2[] uvs = new Vector2[4];
            uvs[0] = new Vector2(0, 0);
            uvs[1] = new Vector2(0, 1);
            uvs[2] = new Vector2(1, 1);
            uvs[3] = new Vector2(1, 0);

            // Normal vectors
            Vector3[] normals = new Vector3[4];
            normals[0] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[1] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[2] = new Vector3(0.0f, 0.0f, -1.0f);
            normals[3] = new Vector3(0.0f, 0.0f, -1.0f);

            // Single grass (Triangle index)
            int[] triangles = new int[6] { 0, 1, 3, 1, 2, 3 }; // Order to fit Cull Back in grass shader

            sourceMesh.SetVertices(verts);
            sourceMesh.SetUVs(0, uvs);
            sourceMesh.SetTriangles(triangles, 0);
        }

        return sourceMesh;
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        DrawInstancesGrassEditor();
        //if (Application.isPlaying == false)
        //    DrawInstancesGrassEditor();
        //else
        //    DrawInstancesGrass().Forget();
#elif UNITY_STANDALONE_WIN
        DrawInstancesGrass().Forget();
#endif
        
        /*if (BackgroundThread != null && !BackgroundThread.IsAlive && Application.isPlaying)
        {
            BackgroundThread = new Thread(new ThreadStart(RenderUnanite)) { IsBackground = true };
            BackgroundThread.Start();
        }

        for (int i = 0; i < 1000; i++)
        {
            if (!Nodes.Any(item => item == i))
            {
                Nodes.Enqueue(i);
            }
        }*/
    }

    private async UniTask DrawInstancesGrass()
    {
        if (rootNode != null && argsBuffer != null)
        {
            for (int i = 0; i < renderNode.Count; i++)
            {
                var draw = renderNode[i];
                if (draw.instantiatedMaterial == null)
                    continue;
                draw.instantiatedMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
                draw.instantiatedMaterial.SetFloat("_WindSpeed", WindSpeed);
                draw.instantiatedMaterial.SetFloat("_WorldSize", renderBounds.size.x);
                draw.instantiatedMaterial.SetFloat("_WindSize", WindSize);

                // 이것은 매직 라인. 그림자를 위해 이것을 주석 해제!!
                mpbs.SetFloat("_Bla", (float)i);

                Graphics.DrawMeshInstancedIndirect(
                    sourceMesh,             // 그려낼 메시
                    subMeshIndex,           // 서브메시 인덱스
                    draw.instantiatedMaterial,               // 그려낼 마테리얼
                    draw.bounds,           // 렌더링 영역
                    argsBuffer,             // 메시 데이터 버퍼
                    0,                      // argsOffset
                    mpbs,                   // MaterialPropertyBlock
                    shadowcastingmode,   // ShadowCastingMode
                    shadow                    // receiveShadows
                );

                await UniTask.Yield(PlayerLoopTiming.LastUpdate);
            }
        }
    }

    private void DrawInstancesGrassEditor()
    {
        if (rootNode != null && argsBuffer != null)
        {
            for (int i = 0; i < renderNode.Count; i++)
            {
                var draw = renderNode[i];
                if (draw.instantiatedMaterial == null)
                    continue;

                {
                    if (m_HiZOcclusion.hiZDepthTexture != null)
                    { 
                        int kernel = 0;
                        computeShader.SetBool("isOpenGL", false);
                        computeShader.SetInt("depthTextureSizeW", m_HiZOcclusion.hiZDepthTexture.width);
                        computeShader.SetInt("depthTextureSizeH", m_HiZOcclusion.hiZDepthTexture.height);
                        computeShader.SetTexture(kernel, "hizTexture", /*HiZtexture*/m_HiZOcclusion.hiZDepthTexture);
                        computeShader.SetInt("grassCount", draw.positions.Count);
                        computeShader.SetBuffer(kernel, "grassMatrixBuffer", draw.positionBuffer);
                        Matrix4x4 vp = GL.GetGPUProjectionMatrix(m_MainCamera.projectionMatrix, false) * m_MainCamera.worldToCameraMatrix;
                        Matrix4x4 v = m_MainCamera.worldToCameraMatrix;
                        Matrix4x4 p = m_MainCamera.projectionMatrix;
                        Matrix4x4 _MVP = p * v;
                        computeShader.SetMatrix("vpMatrix", _MVP);
                        computeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
                        computeShader.SetFloat("range", Range);


                        ComputeBuffer cullResultBuffer = new ComputeBuffer(draw.positions.Count, sizeof(float) * 4, ComputeBufferType.Append);
                        cullResultBuffer.SetCounterValue(0);
                        computeShader.SetBuffer(kernel, "cullResultBuffer", cullResultBuffer);
                        computeShader.Dispatch(kernel, 1 + draw.positions.Count / 640, 1, 1);
                        draw.instantiatedMaterial.SetBuffer("positionBuffer", cullResultBuffer);

                    }
                }

                draw.instantiatedMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
                draw.instantiatedMaterial.SetFloat("_WindSpeed", WindSpeed);
                draw.instantiatedMaterial.SetFloat("_WorldSize", renderBounds.size.x);
                draw.instantiatedMaterial.SetFloat("_WindSize", WindSize);

                // 이것은 매직 라인. 그림자를 위해 이것을 주석 해제!!
                mpbs.SetFloat("_Bla", (float)i);

                Graphics.DrawMeshInstancedIndirect(
                    sourceMesh,             // 그려낼 메시
                    subMeshIndex,           // 서브메시 인덱스
                    draw.instantiatedMaterial,               // 그려낼 마테리얼
                    draw.bounds,           // 렌더링 영역
                    argsBuffer,             // 메시 데이터 버퍼
                    0,                      // argsOffset
                    mpbs,                   // MaterialPropertyBlock
                    shadowcastingmode,   // ShadowCastingMode
                    shadow                    // receiveShadows
                );
            }
        }
    }

    #region 에디터용
    [Header("에디터용")]
    [HideInInspector]
    public int toolbarInt = 0;
    Vector3 mousePos;
    public LayerMask paintMask = 1;
    Vector3 hitPos;
    public float brushSize;
    [HideInInspector]
    public Vector3 hitPosGizmo;
    [HideInInspector]
    public Vector3 hitNormal;
#if UNITY_EDITOR
    SceneView CurrentScene;
#endif

    /*[InitializeOnLoadMethod]
    private static void InitOnLoad()
    {
        //if (Initialize())
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }
    }

    private static void EditorUpdate()
    {
        if (Application.isPlaying == false)
        {
            if ((Selection.Contains(gameObject)))
            {
                mousePos = Event.current.mousePosition;
                float ppp = EditorGUIUtility.pixelsPerPoint;
                mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
                mousePos.x *= ppp;

                // ray for gizmo(disc)
                Ray rayGizmo = scene.camera.ScreenPointToRay(mousePos);
                RaycastHit hitGizmo;

                if (Physics.Raycast(rayGizmo, out hitGizmo, 200f, paintMask.value))
                    hitPosGizmo = hitGizmo.point;

                // 추가
                if (toolbarInt == 1)
                {
                }

                // 제거
                if (toolbarInt == 2)
                {
                    if (Event.current.keyCode == KeyCode.B)
                    {
                        Ray ray = CurrentScene.camera.ScreenPointToRay(mousePos);
                        RaycastHit terrainHit;
                        if (Physics.Raycast(ray, out terrainHit, 200f, paintMask.value))
                        {
                            hitPos = terrainHit.point;
                            hitPosGizmo = hitPos;
                            hitNormal = terrainHit.normal;
                        }
                    }
                    RemoveGrass();
                }
            }
        }
    }*/

    void OnRenderObject()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }

#if UNITY_EDITOR
    void OnScene(SceneView scene)
    {
        CurrentScene = scene;
        if ((Selection.Contains(gameObject)))
        {
            mousePos = Event.current.mousePosition;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            mousePos.y = CurrentScene.camera.pixelHeight - mousePos.y * ppp;
            mousePos.x *= ppp;

            // ray for gizmo(disc)
            Ray rayGizmo = CurrentScene.camera.ScreenPointToRay(mousePos);
            RaycastHit hitGizmo;

            if (Physics.Raycast(rayGizmo, out hitGizmo, 200f, paintMask.value))
                hitPosGizmo = hitGizmo.point;

            // 추가
            if (toolbarInt == 1)
            {
            }

            // 제거
            if (toolbarInt == 2)
            {
                if (Event.current.keyCode == KeyCode.B)
                {
                    Ray ray = CurrentScene.camera.ScreenPointToRay(mousePos);
                    RaycastHit terrainHit;
                    if (Physics.Raycast(ray, out terrainHit, 200f, paintMask.value))
                    {
                        hitPos = terrainHit.point;
                        hitPosGizmo = hitPos;
                        hitNormal = terrainHit.normal;
                        RemoveGrass(rootNode, hitPos, brushSize);
                    }
                }
            }
        }
    }
#endif
    #endregion 에디터용

    public void RemoveGrass(QtreeNode node, Vector3 hitpos, float Radius)
    {
        if (node == null)
            return;

        if (node.CurrentDepth < maxDepth)
        {
            for (int i = 0; i < 4; i++)
            {
                // 쿼드 안에 있는지 검사후 자식 검사
                bool intersects = node.bounds.Intersects(new Bounds(hitpos, Vector3.one * Radius * 2f));
                if (intersects)
                    RemoveGrass(node.children[i], hitpos, Radius);
            }
        }
        else
        {
            // 최동 단계 노드 안에 있는지 검사
            bool intersects = node.bounds.Intersects(new Bounds(hitpos, Vector3.one * Radius * 2f));
            if (intersects)
            {
                bool bufferReset = false;
                Vector3 hit3d = hitpos + transform.position;
                Vector2 hit2d = new Vector2(hit3d.x, hit3d.z);

                if (!iskdtree)
                {
                    for (int icount = node.positions.Count - 1; icount >= 0; icount--)
                    {
                        Vector2 node2d = new Vector2(node.positions[icount].x, node.positions[icount].z);
                        float dist = Vector2.Distance(node2d, hit2d);
                        if (dist <= Radius)
                        {
                            node.positions.Remove(icount);
                            bufferReset = true;
                        }
                    }
                    if (bufferReset)
                    {
                        // 렌더링 버퍼 재설정
                        //node.positionBuffer.SetCounterValue(0);
                        //node.positionBuffer.SetData(node.positions);
                        if (node.positions.Count != 0)
                        {
                            if (node.positionBuffer != null)
                                node.positionBuffer.Release();
                            node.positionBuffer = new ComputeBuffer(node.positions.Count, sizeof(float) * 4);
                            node.positionBuffer.SetData(node.positions.Values.ToList());

                            material.SetBuffer("positionBuffer", positionBuffer);
                            node.instantiatedMaterial.SetBuffer("positionBuffer", node.positionBuffer);
                        }
                        else
                        {
                            node.positionBuffer.Release();
                        }
                    }
                }
                else
                {
                    List<KDTree.KDTreePosition> pointsInRange = node.kdTree.FindInRange(hit3d, Radius);
                    foreach (var item in node.kdTree.findNodes)
                    {
                        if (node.Hash.Remove(item.point.Hash))
                        {
                            //node.kdTree.RemoveNode(item);
                            node.kdTree.Delete(item);
                            bufferReset = true;
                        }
                    }
                    
                    if (bufferReset)
                    {
                        if (node.Hash.Count != 0)
                        {
                            if (node.positionBuffer != null)
                                node.positionBuffer.Release();
                            node.positionBuffer = new ComputeBuffer(node.Hash.Count, sizeof(float) * 4);
                            node.positionBuffer.SetData(node.Hash.Values.ToArray());

                            //material.SetBuffer("positionBuffer", positionBuffer);
                            node.instantiatedMaterial.SetBuffer("positionBuffer", node.positionBuffer);
                        }
                        else
                        {
                            node.positionBuffer.Release();
                            node.positionBuffer = null;
                            node.kdTree.Clear();
                        }
                    }
                }
            }
        }
    }

    #region 쿼드 트리
    private void Update()
    {
        // Perform frustum culling
        if (rootNode != null)
        {
            if (Input.GetKey(KeyCode.B))
            {
                Ray ray = m_MainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit terrainHit;
                if (Physics.Raycast(ray, out terrainHit, 200f, paintMask.value))
                {
                    hitPos = terrainHit.point;
                    hitPosGizmo = hitPos;
                    hitNormal = terrainHit.normal;
                    RemoveGrass(rootNode, hitPos, brushSize);

                    RemoveGrass(rootNode, hitPos + new Vector3(15,0,0), brushSize);

                    RemoveGrass(rootNode, hitPos + new Vector3(0, 0, 15), brushSize);

                    RemoveGrass(rootNode, hitPos + new Vector3(-15, 0, 0), brushSize);
                }
            }

            renderNode.Clear();
            FrustumCull(rootNode, m_MainCamera);
        }
    }

    private void InitializeNode(QtreeNode node, int depth)
    {
        // Create child nodes if depth is within limit
        if (depth < maxDepth)
        {
            node.children = new QtreeNode[4];
            for (int i = 0; i < 4; i++)
            {
                node.children[i] = new QtreeNode();
                node.children[i].bounds = CalculateChildBounds(node.bounds, i);
                node.children[i].CurrentDepth = depth + 1;
            }
            for (int i = 0; i < 4; i++)
            {
                InitializeNode(node.children[i], depth + 1);
            }
        }
        else
        {
            if(DataNode == null)
                DataNode = new List<QtreeNode>();
            DataNode.Add(node);
        }
    }

    private float GetWorldHeightOnTerrain(Terrain terrain, Vector3 worldPos)
    {
        float worldY = terrain.SampleHeight(worldPos) + terrain.GetPosition().y;
        return worldY;
    }

    private Bounds CalculateChildBounds(Bounds parentBounds, int quadrant)
    {
        Vector3 center = new Vector3(parentBounds.center.x, parentBounds.center.y, parentBounds.center.z);
        Vector3 size = new Vector3(parentBounds.size.x * 0.5f, parentBounds.size.y * 0.5f, parentBounds.size.z * 0.5f);

        Vector3 offsetvec = new Vector3(parentBounds.size.x * 0.25f, parentBounds.size.y, parentBounds.size.z * 0.25f);
        float xOffset = (quadrant % 2 == 0) ? offsetvec.x : -offsetvec.x;
        float zOffset = (quadrant < 2) ? offsetvec.z : -offsetvec.z;

        center.x += xOffset;
        center.z += zOffset;

        //size *= 2;
        //size.y = 100.0f;
        return new Bounds(center, size);
    }

    private void FrustumCull(QtreeNode node, Camera camera)
    {
        float dist = Vector3.Distance(camera.gameObject.transform.position, node.bounds.center + gameObject.transform.position);
        if (dist > MaxDistance && node.CurrentDepth == maxDepth)
            return;

        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), node.bounds))
        {
            return;
        }

        if (!iskdtree && node.positions != null && node.positions.Count == 0)
            return;

        if (iskdtree && node.Hash != null && node.Hash.Count == 0)
            return;

        if (node.children == null)
        {
            renderNode.Add(node);
            // Perform object culling at leaf nodes
            //foreach (GameObject obj in node.objects)
            //{
            //    if (obj != null)
            //    {
            //        obj.SetActive(false);
            //    }
            //}
        }
        else
        {
            // Traverse the children nodes
            for (int i = 0; i < 4; i++)
            {
                FrustumCull(node.children[i], camera);
            }
        }
    }
    #endregion 쿼드 트리

#if UNITY_EDITOR
    List<Vector3> debubPos = new List<Vector3>();
    private void OnDrawGizmos()
    {
        //if (rootNode != null)
        //{
        //    foreach (var draw in rootNode.children)
        //    //foreach (var draw in renderNode)
        //    {
        //        UnityEditor.Handles.color = Color.red;
        //        //for (int i = 0; i < debubPos.Count; i++)
        //        {
        //            Vector3 size = draw.bounds.size;
        //            size.y = 1;
        //            Handles.DrawWireCube(draw.bounds.center, size);
        //            //UnityEditor.Handles.DrawLines(new Vector3[] { centerPos, p1, centerPos, p2, p2, p4, p1, p3, p3, p4 });
        //        }
        //    }
        //}
    }
#endif
}

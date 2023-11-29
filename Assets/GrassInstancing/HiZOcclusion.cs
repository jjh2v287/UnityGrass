using GPUInstancer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HiZOcclusion : MonoBehaviour
{
    public static float COMPUTE_SHADER_THREAD_COUNT = 512;
    public static float COMPUTE_SHADER_THREAD_COUNT_2D = 16;

    public static readonly string COMPUTE_TEXTURE_UTILS_PATH = "Compute/GrassCSTextureUtils";
    public static readonly string COMPUTE_COPY_TEXTURE_KERNEL = "CSCopyTexture";
    public static readonly string COMPUTE_REDUCE_TEXTURE_KERNEL = "CSReduceTexture";
    public static int computeTextureUtilsCopyTextureId;
    public static int computeTextureUtilsReduceTextureId;
    [SerializeField] private ComputeShader computeTextureUtils = default;
    public static class CopyTextureKernelProperties
    {
        public static readonly int SOURCE_TEXTURE = Shader.PropertyToID("source");
        public static readonly int SOURCE_TEXTURE_ARRAY = Shader.PropertyToID("textureArray");
        public static readonly int DESTINATION_TEXTURE = Shader.PropertyToID("destination");
        public static readonly int OFFSET_X = Shader.PropertyToID("offsetX");
        public static readonly int SOURCE_SIZE_X = Shader.PropertyToID("sourceSizeX");
        public static readonly int SOURCE_SIZE_Y = Shader.PropertyToID("sourceSizeY");
        public static readonly int DESTINATION_SIZE_X = Shader.PropertyToID("destinationSizeX");
        public static readonly int DESTINATION_SIZE_Y = Shader.PropertyToID("destinationSizeY");
        public static readonly int REVERSE_Z = Shader.PropertyToID("reverseZ");
        public static readonly int TEXTURE_ARRAY_INDEX = Shader.PropertyToID("textureArrayIndex");
    }

    [Range(0, 16)]
    public int debuggerMipLevel;
    [SerializeField]
    private RawImage _hiZDebugDepthTextureGUIImage;

    [Header("For info only, don't change:")]
    //[HideInInspector]
    public RenderTexture hiZDepthTexture;
    //[HideInInspector]
    public Texture unityDepthTexture;
    //[HideInInspector]
    public Vector2 hiZTextureSize;

    private Camera _mainCamera;
    private int _hiZMipLevels = 0;
    private RenderTexture[] _hiZMipLevelTextures = null;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!GPUInstancerConstants.gpuiSettings.IsStandardRenderPipeline())
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCameraRenderingSRP;
        else
            Camera.onPostRender += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        if (!GPUInstancerConstants.gpuiSettings.IsStandardRenderPipeline())
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCameraRenderingSRP;
        else
            Camera.onPostRender -= OnEndCameraRendering;

        if (hiZDepthTexture != null)
        {
            hiZDepthTexture.Release();
            hiZDepthTexture = null;
        }

        if (_hiZMipLevelTextures != null)
        {
            for (int i = 0; i < _hiZMipLevelTextures.Length; i++)
            {
                if (_hiZMipLevelTextures[i] != null)
                    _hiZMipLevelTextures[i].Release();
            }
            _hiZMipLevelTextures = null;
        }
    }

    public void Initialize(Camera occlusionCamera = null)
    {
        _mainCamera = occlusionCamera != null ? occlusionCamera : gameObject.GetComponent<Camera>();

        if (_mainCamera == null)
        {
            Debug.LogError("GPUI Hi-Z Occlusion Culling Generator failed to initialize: camera not found.");
            return;
        }

        _mainCamera.depthTextureMode |= DepthTextureMode.Depth;

        CreateHiZDepthTexture();
        SetupComputeTextureUtils();
    }

    private bool CreateHiZDepthTexture()
    {
        hiZTextureSize = GetScreenSize();

        _hiZMipLevels = (int)Mathf.Floor(Mathf.Log(hiZTextureSize.x, 2f));

        if (hiZTextureSize.x <= 0 || hiZTextureSize.y <= 0 || _hiZMipLevels == 0)
        {
            if (hiZDepthTexture != null)
            {
                hiZDepthTexture.Release();
                hiZDepthTexture = null;
            }

            //Debug.LogError("Cannot create GPUI HiZ Depth Texture for occlusion culling: Screen size is too small.");
            return false;
        }

        if (hiZDepthTexture != null)
            hiZDepthTexture.Release();

        int width = (int)hiZTextureSize.x;
        int height = (int)hiZTextureSize.y;

        hiZDepthTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        hiZDepthTexture.name = "GPUIHiZDepthTexture";
        hiZDepthTexture.filterMode = FilterMode.Point;
        hiZDepthTexture.useMipMap = true;
        hiZDepthTexture.autoGenerateMips = false;
        hiZDepthTexture.enableRandomWrite = true;
        hiZDepthTexture.Create();
        hiZDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        hiZDepthTexture.GenerateMips();

        if (_hiZMipLevelTextures != null)
        {
            for (int i = 0; i < _hiZMipLevelTextures.Length; i++)
            {
                if (_hiZMipLevelTextures[i] != null)
                    _hiZMipLevelTextures[i].Release();
            }
        }

        _hiZMipLevelTextures = new RenderTexture[_hiZMipLevels];

        for (int i = 0; i < _hiZMipLevels; ++i)
        {
            width = width >> 1;

            height = height >> 1;

            if (width == 0)
                width = 1;

            if (height == 0)
                height = 1;

            _hiZMipLevelTextures[i] = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            _hiZMipLevelTextures[i].name = "GPUIHiZDepthTexture_Mip_" + i;
            _hiZMipLevelTextures[i].filterMode = FilterMode.Point;
            _hiZMipLevelTextures[i].useMipMap = false;
            _hiZMipLevelTextures[i].autoGenerateMips = false;
            _hiZMipLevelTextures[i].enableRandomWrite = true;
            _hiZMipLevelTextures[i].Create();
            _hiZMipLevelTextures[i].hideFlags = HideFlags.HideAndDontSave;
        }

        return true;
    }

    private Vector2 GetScreenSize()
    {
        Vector2 screenSize = Vector2.zero;
        screenSize.x = _mainCamera.pixelWidth;
        screenSize.y = _mainCamera.pixelHeight;
        return screenSize;
    }

    public void SetupComputeTextureUtils()
    {
        if (computeTextureUtils == null)
            computeTextureUtils = Resources.Load<ComputeShader>(COMPUTE_TEXTURE_UTILS_PATH);

        if (computeTextureUtils != null)
        { 
            computeTextureUtilsCopyTextureId = computeTextureUtils.FindKernel(COMPUTE_COPY_TEXTURE_KERNEL);
            computeTextureUtilsReduceTextureId = computeTextureUtils.FindKernel(COMPUTE_REDUCE_TEXTURE_KERNEL);
        }
    }

    private void OnEndCameraRenderingSRP(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        OnEndCameraRendering(camera);
    }

    private void OnEndCameraRendering(Camera camera)
    {
        if (_mainCamera == null || camera != _mainCamera)
            return;

        if (unityDepthTexture == null)
            unityDepthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        
        UpdateTextureWithComputeShader(0);

        if(_hiZDebugDepthTextureGUIImage != null)
            _hiZDebugDepthTextureGUIImage.texture = debuggerMipLevel == 0 ? hiZDepthTexture : _hiZMipLevelTextures[debuggerMipLevel >= _hiZMipLevels ? _hiZMipLevels - 1 : debuggerMipLevel];
    }

    private void UpdateTextureWithComputeShader(int offset, int textureArrayIndex = 0)
    {
        CopyTextureWithComputeShader(unityDepthTexture, hiZDepthTexture, offset);

        for (int i = 0; i < _hiZMipLevels - 1; ++i)
        {
            RenderTexture tempRT = _hiZMipLevelTextures[i];

            if (i == 0)
                ReduceTextureWithComputeShader(hiZDepthTexture, tempRT, offset);
            else
                ReduceTextureWithComputeShader(_hiZMipLevelTextures[i - 1], tempRT, offset);

            CopyTextureWithComputeShader(tempRT, hiZDepthTexture, offset, 0, i + 1, false);
        }
    }

    public void ReduceTextureWithComputeShader(Texture source, Texture destination, int offsetX, int sourceMip = 0, int destinationMip = 0)
    {
        int sourceW = source.width;
        int sourceH = source.height;
        int destinationW = destination.width;
        int destinationH = destination.height;
        for (int i = 0; i < sourceMip; i++)
        {
            sourceW >>= 1;
            sourceH >>= 1;
        }
        for (int i = 0; i < destinationMip; i++)
        {
            destinationW >>= 1;
            destinationH >>= 1;
        }

        computeTextureUtils.SetTexture(computeTextureUtilsReduceTextureId, CopyTextureKernelProperties.SOURCE_TEXTURE, source, sourceMip);
        computeTextureUtils.SetTexture(computeTextureUtilsReduceTextureId, CopyTextureKernelProperties.DESTINATION_TEXTURE, destination, destinationMip);

        computeTextureUtils.SetInt(CopyTextureKernelProperties.OFFSET_X, offsetX);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.SOURCE_SIZE_X, sourceW);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.SOURCE_SIZE_Y, sourceH);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.DESTINATION_SIZE_X, destinationW);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.DESTINATION_SIZE_Y, destinationH);

        computeTextureUtils.Dispatch(computeTextureUtilsReduceTextureId, Mathf.CeilToInt(destinationW / COMPUTE_SHADER_THREAD_COUNT_2D),
            Mathf.CeilToInt(destinationH / COMPUTE_SHADER_THREAD_COUNT_2D), 1);
    }

    public void CopyTextureWithComputeShader(Texture source, Texture destination, int offsetX, int sourceMip = 0, int destinationMip = 0, bool reverseZ = true)
    {
        computeTextureUtils.SetTexture(computeTextureUtilsCopyTextureId,
            CopyTextureKernelProperties.SOURCE_TEXTURE, source, sourceMip);
        computeTextureUtils.SetTexture(computeTextureUtilsCopyTextureId,
            CopyTextureKernelProperties.DESTINATION_TEXTURE, destination, destinationMip);


        computeTextureUtils.SetInt(CopyTextureKernelProperties.OFFSET_X, offsetX);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.SOURCE_SIZE_X, source.width);
        computeTextureUtils.SetInt(CopyTextureKernelProperties.SOURCE_SIZE_Y, source.height);
        computeTextureUtils.SetBool(CopyTextureKernelProperties.REVERSE_Z, reverseZ);

        computeTextureUtils.Dispatch(computeTextureUtilsCopyTextureId,
            Mathf.CeilToInt(source.width / COMPUTE_SHADER_THREAD_COUNT_2D),
            Mathf.CeilToInt(source.height / COMPUTE_SHADER_THREAD_COUNT_2D), 1);
    }
}

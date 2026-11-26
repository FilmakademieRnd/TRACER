using System;
using System.Collections.Generic;
using tracer;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public class GsplatRenderer : MonoBehaviour
{
    internal static class SortResource
    {
        public static GraphicsBuffer AltKeysBuffer { get; private set; }
        public static GraphicsBuffer AltPayloadBuffer { get; private set; }
        public static GraphicsBuffer PassHistBuffer { get; private set; }
        public static GraphicsBuffer GlobalHistBuffer { get; private set; }
        private static GraphicsBuffer s_dummyBuffer;

        public static GraphicsBuffer DummyBuffer
        {
            get
            {
                if (s_dummyBuffer == null || !s_dummyBuffer.IsValid())
                {
                    s_dummyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 16);
                }
                return s_dummyBuffer;
            }
        }

        private static GraphicsBuffer s_dummyRWBuffer;
        public static GraphicsBuffer DummyRWBuffer
        {
            get
            {
                if (s_dummyRWBuffer == null || !s_dummyRWBuffer.IsValid())
                {
                    s_dummyRWBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 16);
                }
                return s_dummyRWBuffer;
            }
        }

        public static int CurrentCapacity { get; private set; }

        /// <summary>
        /// Ensures the shared scratch buffers are large enough for the requested splat count.
        /// Reallocates only if a larger object is loaded.
        /// </summary>
        public static void EnsureCapacity(int requiredSplatCount)
        {
            if (requiredSplatCount <= CurrentCapacity && AltKeysBuffer != null && AltKeysBuffer.IsValid())
                return;

            Dispose();

            CurrentCapacity = requiredSplatCount;
            int threadBlocksCount = (requiredSplatCount + 3839) / 3840;

            GlobalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 256 * 4, sizeof(uint));
            PassHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, 256 * threadBlocksCount), sizeof(uint));
            AltKeysBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredSplatCount, sizeof(uint));
            AltPayloadBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredSplatCount, sizeof(uint));
        }

        /// <summary>
        /// Releases all shared sorting buffers (call on scene unload or application quit).
        /// </summary>
        public static void Dispose()
        {
            if (AltKeysBuffer != null && AltKeysBuffer.IsValid()) AltKeysBuffer.Dispose();
            if (AltPayloadBuffer != null && AltPayloadBuffer.IsValid()) AltPayloadBuffer.Dispose();
            if (PassHistBuffer != null && PassHistBuffer.IsValid()) PassHistBuffer.Dispose();
            if (GlobalHistBuffer != null && GlobalHistBuffer.IsValid()) GlobalHistBuffer.Dispose();
            if (s_dummyBuffer != null && s_dummyBuffer.IsValid()) s_dummyBuffer.Dispose();
            if (s_dummyRWBuffer != null && s_dummyRWBuffer.IsValid()) s_dummyRWBuffer.Dispose();

            AltKeysBuffer = null;
            AltPayloadBuffer = null;
            PassHistBuffer = null;
            GlobalHistBuffer = null;
            s_dummyBuffer = null;
            s_dummyRWBuffer = null;
            CurrentCapacity = 0;
        }
    }


    public class RenderResource : IDisposable
    {
        public uint SplatCount { get; private set; }
        public Bounds Bounds { get; private set; }

        // Persistent VRAM structured buffer references owned exclusively by this instance
        public GraphicsBuffer PositionBuffer { get; private set; }       // _PositionBuffer (uint4)
        public GraphicsBuffer ScaleBuffer { get; private set; }          // _ScaleBuffer (uint)
        public GraphicsBuffer OrderBuffer { get; private set; }          // _OrderBuffer (uint)
        public GraphicsBuffer DistanceBuffer { get; private set; }       // b_sort (uint)
        public GraphicsBuffer SHCentroidsBuffer { get; private set; }    // _SHCentroidsBuffer (float4)
        public GraphicsBuffer EvaluatedColorBuffer { get; private set; } // Pre-evaluated 10-10-10 HDR RGB color
        public GraphicsBuffer ProjectedSplatBuffer { get; private set; } // Pre-evaluated Splat projection


        public RenderResource(
            NativeArray<uint4> packedSplats,
            NativeArray<uint> shLabels,
            NativeArray<float4> shCentroids,
            int count,
            Bounds bounds)
        {
            SplatCount = (uint)count;
            Bounds = bounds;

            PositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint) * 4);
            OrderBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint));
            DistanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint));
            ProjectedSplatBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count * 2, sizeof(uint) * 4);

            PositionBuffer.SetData(packedSplats, 0, 0, count);

            // Allocate SH buffers ONLY if higher SH bands (1-3) are present
            if (shLabels.IsCreated && shLabels.Length > 0 && shCentroids.IsCreated && shCentroids.Length > 0)
            {
                ScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint));
                ScaleBuffer.SetData(shLabels, 0, 0, count);

                SHCentroidsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, shCentroids.Length, sizeof(float) * 4);
                SHCentroidsBuffer.SetData(shCentroids);

                EvaluatedColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint));
            }

            NativeArray<uint> initialOrder = new NativeArray<uint>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < count; i++) initialOrder[i] = (uint)i;
                OrderBuffer.SetData(initialOrder);
            }
            finally
            {
                if (initialOrder.IsCreated) initialOrder.Dispose();
            }
        }

        public void Dispose()
        {
            if (PositionBuffer != null && PositionBuffer.IsValid()) PositionBuffer.Dispose();
            if (ScaleBuffer != null && ScaleBuffer.IsValid()) ScaleBuffer.Dispose();
            if (SHCentroidsBuffer != null && SHCentroidsBuffer.IsValid()) SHCentroidsBuffer.Dispose();
            if (EvaluatedColorBuffer != null && EvaluatedColorBuffer.IsValid()) EvaluatedColorBuffer.Dispose();
            if (OrderBuffer != null && OrderBuffer.IsValid()) OrderBuffer.Dispose();
            if (DistanceBuffer != null && DistanceBuffer.IsValid()) DistanceBuffer.Dispose();
            if (ProjectedSplatBuffer != null && ProjectedSplatBuffer.IsValid()) ProjectedSplatBuffer.Dispose();

            PositionBuffer = null;
            ScaleBuffer = null;
            SHCentroidsBuffer = null;
            EvaluatedColorBuffer = null;
            OrderBuffer = null;
            DistanceBuffer = null;
            ProjectedSplatBuffer = null;
        }
    }


    private class GsplatSettings
    {
        // High-performance constant tweaks optimized for smooth WebGL WebGPU rendering
        public const uint SplatInstanceSize = 128; // Pack 128 splats into a single instanced mesh batch
        public const float CameraTranslationThresholdSqr = 0.04f; // 0.2f * 0.2f (pre-squared for zero-allocation magnitude checks)
        public const float CameraRotationThreshold = 10f; // Sort if camera rotates more than 10 degrees

        private static Mesh s_instancedMesh;

        /// <summary>
        /// Retrieves or lazily creates the synchronized, high-performance batch mesh for instanced blitting.
        /// </summary>
        public static Mesh GetBatchMesh()
        {
            if (s_instancedMesh != null) return s_instancedMesh;

            int vertexCount = 4 * (int)SplatInstanceSize;
            int indexCount = 6 * (int)SplatInstanceSize;

            Vector3[] meshPositions = new Vector3[vertexCount];
            int[] meshIndices = new int[indexCount];

            for (uint i = 0; i < SplatInstanceSize; ++i)
            {
                // Store the sub-instance ID as an ordinary float inside the Z coordinate.
                // This prevents mobile/WebAssembly subnormal register flush errors at the vertex boundary.
                float subInstanceId = (float)i;

                int vIdx = (int)i * 4;
                meshPositions[vIdx + 0] = new Vector3(-1f, -1f, subInstanceId);
                meshPositions[vIdx + 1] = new Vector3(1f, -1f, subInstanceId);
                meshPositions[vIdx + 2] = new Vector3(-1f, 1f, subInstanceId);
                meshPositions[vIdx + 3] = new Vector3(1f, 1f, subInstanceId);

                int iIdx = (int)i * 6;
                meshIndices[iIdx + 0] = 0 + vIdx;
                meshIndices[iIdx + 1] = 1 + vIdx;
                meshIndices[iIdx + 2] = 2 + vIdx;
                meshIndices[iIdx + 3] = 1 + vIdx;
                meshIndices[iIdx + 4] = 3 + vIdx;
                meshIndices[iIdx + 5] = 2 + vIdx;
            }

            s_instancedMesh = new Mesh
            {
                name = "GsplatWebGL_MeshInstance",
                vertices = meshPositions,
                triangles = meshIndices,
                hideFlags = HideFlags.HideAndDontSave
            };

            // Enforce boundary pre-calculation
            s_instancedMesh.RecalculateBounds();

            return s_instancedMesh;
        }
    }

    private Material SplatMaterial;
    private ComputeShader SortCompute;
    private ComputeShader ProjectCompute;
    private static Shader s_shadowDepthShader;
    private static UnityEngine.Rendering.CommandBuffer s_selfShadowCmd;

    // Explicit object ownership: This renderer alone owns and destroys its GPU data resource
    private RenderResource m_gpuResource;
    public RenderResource GpuResource => m_gpuResource;
    public int SHBands => m_shBands;

    private MaterialPropertyBlock m_propertyBlock;
    private int m_shBands;
    private Camera m_mainCamera;
    private Vector3 m_lastCameraPos;
    private Quaternion m_lastCameraRot;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SceneLightData
    {
        public Vector4 posAndRange;
        public Vector4 colorAndType;
        public Vector4 dirAndSpot;
    }

    private const int k_maxSceneLights = 16;
    private static GraphicsBuffer s_sceneLightsBuffer;
    private static readonly SceneLightData[] s_lightDataArray = new SceneLightData[k_maxSceneLights];
    private static int s_cachedLightCount = 0;
    private static List<Light> s_cachedLights;

    private static Light s_primaryShadowLight;
    private static int s_shadowLightIndex = -1;
    private static Camera s_shadowCam;
    private static RenderTexture s_shadowRT;
    private static int s_lastShadowFrame = -1;

    private float m_lastSortTime;

    // Shader Property uniform caching IDs
    private static readonly int k_splatCount = Shader.PropertyToID("_SplatCount");
    private static readonly int k_matrixMV = Shader.PropertyToID("_MatrixMV");
    private static readonly int k_posBuffer = Shader.PropertyToID("_PositionBuffer");
    private static readonly int k_numKeys = Shader.PropertyToID("e_numKeys");
    private static readonly int k_threadBlocks = Shader.PropertyToID("e_threadBlocks");
    private static readonly int k_radixShift = Shader.PropertyToID("e_radixShift");
    private static readonly int k_globalHist = Shader.PropertyToID("b_globalHist");
    private static readonly int k_passHist = Shader.PropertyToID("b_passHist");
    private static readonly int k_sort = Shader.PropertyToID("b_sort");
    private static readonly int k_sortPayload = Shader.PropertyToID("b_sortPayload");
    private static readonly int k_alt = Shader.PropertyToID("b_alt");
    private static readonly int k_altPayload = Shader.PropertyToID("b_altPayload");
    private static readonly int k_orderBuffer = Shader.PropertyToID("_OrderBuffer");
    private static readonly int k_projectBuffer = Shader.PropertyToID("_ProjectedSplatBuffer");
    private static readonly int k_scaleBuffer = Shader.PropertyToID("_ScaleBuffer");
    private static readonly int k_evalColorBuffer   = Shader.PropertyToID("_EvaluatedColorBuffer");
    private static readonly int k_shCentroidsBuffer = Shader.PropertyToID("_SHCentroidsBuffer");
    private static readonly int k_shDegree = Shader.PropertyToID("_SHDegree");
    private static readonly int k_brightness = Shader.PropertyToID("_Brightness");
    private static readonly int k_scaleFactor = Shader.PropertyToID("_ScaleFactor");
    private static readonly int k_matrixM = Shader.PropertyToID("_MATRIX_M");

    [Header("GSplat settings")]
    public float m_Brightness = 1.0f;
    public float m_ScaleFactor = 1.0f;

    [Header("Sorting Precision")]
    [Tooltip("Minimum camera movement in meters before re-sorting (default: 0.05m = 5cm)")]
    [SerializeField] private float m_positionThreshold = 0.05f;

    [Tooltip("Minimum camera rotation in degrees before re-sorting (default: 1.5 degrees)")]
    [SerializeField] private float m_rotationThreshold = 1.5f;

    [Tooltip("Minimum time in seconds between two GPU sorts to protect framerate (default: 0.03s = ~30Hz)")]
    [SerializeField] private float m_minSortInterval = 0.03f;

    [Header("Dynamic Lighting & Shadows (Toggleable)")]
    public bool EnableRelighting = true;
    [Range(0f, 1f)] public float RelightStrength = 0.75f;
    public bool CastShadows = true;
    public bool ReceiveShadows = true;
    public bool SelfShadows = true;
    [Range(0f, 1f)] public float ShadowStrength = 0.75f;
    [Range(0.005f, 0.6f)] public float ShadowBias = 0.3f; // Bias in Metern (unabhängig von ShadowRange!)
    public float ShadowRange = 15f;

    public void Initialize(RenderResource allocatedResource, int shBands)
    {
        m_gpuResource = allocatedResource ?? throw new ArgumentNullException(nameof(allocatedResource));
        // Safety clamp: Force SH0 if no higher SH buffers exist (prevents WebGPU Out-of-Bounds crash)
        bool hasSHData = m_gpuResource.EvaluatedColorBuffer != null && m_gpuResource.ScaleBuffer != null;
        m_shBands = hasSHData ? Mathf.Clamp(shBands, 0, 4) : 0;
        m_mainCamera = Camera.main;

        if (SplatMaterial == null) SplatMaterial = Resources.Load<Material>("Materials/Gsplat");
        if (SplatMaterial == null)
            Debug.LogError("[GsplatRenderer] Critical: 'Gsplat' Material could not be loaded from Resources. Vertex/Fragment processing halted.");

#if UNITY_WEBGL && !UNITY_EDITOR
        SortCompute = Resources.Load<ComputeShader>("Shader/GsplatSortWebGPU");
#else
        if (SortCompute == null) SortCompute = Resources.Load<ComputeShader>("Shader/GsplatSort");
#endif
        if (SortCompute == null)
            Debug.LogError("[GsplatRenderer] Critical: Sort ComputeShader could not be loaded from Resources.");
        
        if (ProjectCompute == null) ProjectCompute = Resources.Load<ComputeShader>("Shader/GsplatProject");


        if (m_mainCamera != null)
        {
            m_lastCameraPos = m_mainCamera.transform.position;
            m_lastCameraRot = m_mainCamera.transform.rotation;
        }

        m_propertyBlock = new MaterialPropertyBlock();

        int count = (int)m_gpuResource.SplatCount;
        SortResource.EnsureCapacity(count);

        // Dynamically enable the correct Spherical Harmonics variant on the material
        if (SplatMaterial != null)
        {
            for (int i = 0; i <= 4; i++) SplatMaterial.DisableKeyword($"SH_BANDS_{i}");
            SplatMaterial.EnableKeyword($"SH_BANDS_{m_shBands}");
        }

        SetupPropertyBlock();
        DispatchSort();
    }
    private static void OnParameterObjectAdded(object sender, ParameterObject obj)
    {
        if (obj == null || s_cachedLights == null) return;

        Light[] newLights = obj.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < newLights.Length; i++)
        {
            if (newLights[i] != null && !s_cachedLights.Contains(newLights[i]))
                s_cachedLights.Add(newLights[i]);
        }
    }

    private static void OnParameterObjectRemoved(object sender, ParameterObject obj)
    {
        if (obj == null || s_cachedLights == null) return;

        Light[] removedLights = obj.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < removedLights.Length; i++)
        {
            if (removedLights[i] != null)
                s_cachedLights.Remove(removedLights[i]);
        }
    }

    private void SetupPropertyBlock()
    {
        if (m_gpuResource == null) return;
        if (m_propertyBlock == null) m_propertyBlock = new MaterialPropertyBlock();

        m_propertyBlock.SetInt(k_splatCount, (int)m_gpuResource.SplatCount);
        m_propertyBlock.SetInt(k_shDegree, m_shBands);

        // Core geometry and sorting index buffers
        m_propertyBlock.SetBuffer(k_posBuffer, m_gpuResource.PositionBuffer);
        m_propertyBlock.SetBuffer(k_orderBuffer, m_gpuResource.OrderBuffer);
        m_propertyBlock.SetBuffer(k_projectBuffer, m_gpuResource.ProjectedSplatBuffer);

        // Bind pre-evaluated SH color buffer (with Metal dummy fallback when m_shBands == 0)
        GraphicsBuffer evalColorBuf = (m_shBands > 0 && m_gpuResource.EvaluatedColorBuffer != null)
            ? m_gpuResource.EvaluatedColorBuffer
            : SortResource.DummyBuffer;

        m_propertyBlock.SetBuffer(k_evalColorBuffer, evalColorBuf);
    }

    private void DispatchSort()
    {
        if (m_gpuResource == null || SortCompute == null) return;
        if (m_mainCamera == null) m_mainCamera = Camera.main;
        if (m_mainCamera == null) return;

        int count = (int)m_gpuResource.SplatCount;

        // Ensure shared scratch buffers exist and are large enough for this object
        SortResource.EnsureCapacity(count);

        GraphicsBuffer srcKeys    = m_gpuResource.DistanceBuffer;
        GraphicsBuffer dstKeys    = SortResource.AltKeysBuffer;
        GraphicsBuffer srcPayload = m_gpuResource.OrderBuffer;
        GraphicsBuffer dstPayload = SortResource.AltPayloadBuffer;
        GraphicsBuffer globalHist = SortResource.GlobalHistBuffer;
        GraphicsBuffer passHist   = SortResource.PassHistBuffer;

        if (dstKeys == null || dstPayload == null || globalHist == null || passHist == null) return;

        int kernelCalcDepth = SortCompute.FindKernel("CalcDepth");
        int kernelInitSort  = SortCompute.FindKernel("InitDeviceRadixSort");
        int kernelUpsweep   = SortCompute.FindKernel("Upsweep");
        int kernelScan      = SortCompute.FindKernel("Scan");
        int kernelDownsweep = SortCompute.FindKernel("Downsweep");

        int threadBlocksCount = (count + 3839) / 3840;
        Matrix4x4 modelViewMatrix = m_mainCamera.worldToCameraMatrix * transform.localToWorldMatrix;

        // --- STEP 1: CALCULATE VIEW-SPACE DEPTH KEYS, SEED PAYLOAD & PRE-EVALUATE SH ---
        SortCompute.SetInt(k_splatCount, count);
        SortCompute.SetInt(k_shDegree, m_shBands);
        SortCompute.SetMatrix(k_matrixMV, modelViewMatrix);

        SortCompute.SetBuffer(kernelCalcDepth, k_posBuffer, m_gpuResource.PositionBuffer);
        SortCompute.SetBuffer(kernelCalcDepth, k_sortPayload, srcPayload);
        SortCompute.SetBuffer(kernelCalcDepth, k_sort, srcKeys);

        // Metal requires ALL declared buffers in CalcDepth to be bound, even when m_shBands == 0
        bool hasSH = m_shBands > 0 && m_gpuResource.EvaluatedColorBuffer != null;
        GraphicsBuffer dummy = SortResource.DummyBuffer;
        GraphicsBuffer dummyRW = SortResource.DummyRWBuffer;

        SortCompute.SetBuffer(kernelCalcDepth, k_scaleBuffer, hasSH ? m_gpuResource.ScaleBuffer : dummy);
        SortCompute.SetBuffer(kernelCalcDepth, k_shCentroidsBuffer, hasSH ? m_gpuResource.SHCentroidsBuffer : dummy);
        SortCompute.SetBuffer(kernelCalcDepth, k_evalColorBuffer, hasSH ? m_gpuResource.EvaluatedColorBuffer : dummyRW);

        SortCompute.Dispatch(kernelCalcDepth, (count + 255) / 256, 1, 1);

        // --- STEP 2: RUN 4-PASS LSD RADIX SORT WITH PING-PONG ---
        SortCompute.SetInt(k_numKeys, count);
        SortCompute.SetInt(k_threadBlocks, threadBlocksCount);

        // Clear global histogram ONCE before the 4 passes (1024 uints / 256 threads = 4 groups)
        SortCompute.SetBuffer(kernelInitSort, k_globalHist, globalHist);

        SortCompute.Dispatch(kernelInitSort, 4, 1, 1);

        for (int shiftPass = 0; shiftPass < 32; shiftPass += 8)
        {
            SortCompute.SetInt(k_radixShift, shiftPass);

            // Upsweep
            SortCompute.SetBuffer(kernelUpsweep, k_sort, srcKeys);
            SortCompute.SetBuffer(kernelUpsweep, k_globalHist, globalHist);
            SortCompute.SetBuffer(kernelUpsweep, k_passHist, passHist);
            SortCompute.Dispatch(kernelUpsweep, threadBlocksCount, 1, 1);

            // Scan (256 radix digits = 256 thread groups)
            SortCompute.SetBuffer(kernelScan, k_passHist, passHist);
            SortCompute.Dispatch(kernelScan, 256, 1, 1);

            // Downsweep
            SortCompute.SetBuffer(kernelDownsweep, k_sort, srcKeys);
            SortCompute.SetBuffer(kernelDownsweep, k_sortPayload, srcPayload);
            SortCompute.SetBuffer(kernelDownsweep, k_alt, dstKeys);
            SortCompute.SetBuffer(kernelDownsweep, k_altPayload, dstPayload);
            SortCompute.SetBuffer(kernelDownsweep, k_globalHist, globalHist);
            SortCompute.SetBuffer(kernelDownsweep, k_passHist, passHist);
            SortCompute.Dispatch(kernelDownsweep, threadBlocksCount, 1, 1);

            // Swap Ping-Pong buffers (after 4 passes, sorted indices are back in OrderBuffer)
            (srcKeys, dstKeys) = (dstKeys, srcKeys);
            (srcPayload, dstPayload) = (dstPayload, srcPayload);
        }
    }

    private void LateUpdate()
    {
        if (m_gpuResource == null || SplatMaterial == null) return;
        if (m_mainCamera == null) m_mainCamera = Camera.main;
        if (m_mainCamera == null) return;

        // 1. Sort & evaluate SH1-3 after all camera/object movements in Update() have finished
        if (SortCompute != null && Time.unscaledTime - m_lastSortTime >= m_minSortInterval)
        {
            Vector3 currentPos = m_mainCamera.transform.position;
            Quaternion currentRot = m_mainCamera.transform.rotation;

            float posThresholdSqr = m_positionThreshold * m_positionThreshold;
            float travelDeltaSqr = (currentPos - m_lastCameraPos).sqrMagnitude;
            float angleDelta = Quaternion.Angle(m_lastCameraRot, currentRot);

            if (travelDeltaSqr > posThresholdSqr || angleDelta > m_rotationThreshold || transform.hasChanged)
            {
                m_lastCameraPos = currentPos;
                m_lastCameraRot = currentRot;
                transform.hasChanged = false;
                m_lastSortTime = Time.unscaledTime;
                DispatchSort();
            }
        }

        // 2. Update only per-frame dynamic uniforms
        m_propertyBlock.SetMatrix(k_matrixM, transform.localToWorldMatrix);
        m_propertyBlock.SetFloat(k_scaleFactor, m_ScaleFactor);
        m_propertyBlock.SetFloat(k_brightness, m_Brightness);

        // 3. Setup RenderParams (CastShadows can be toggled at any time)
        RenderParams rp = new RenderParams(SplatMaterial)
        {
            worldBounds = CalcWorldBounds(m_gpuResource.Bounds, transform),
            matProps = m_propertyBlock,
            layer = gameObject.layer,
            shadowCastingMode = CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off
        };

        // 4. Pre-project 1x per splat (including optional dynamic relighting & shadow receiving)
        if (ProjectCompute != null)
        {
            if (s_sceneLightsBuffer == null || !s_sceneLightsBuffer.IsValid())
                s_sceneLightsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, k_maxSceneLights, 48);

            // Sync active lights from event-maintained s_cachedLights list (zero allocations)
            s_cachedLightCount = 0;
            s_primaryShadowLight = null;
            s_shadowLightIndex = -1;

            if ((EnableRelighting || ReceiveShadows) && s_cachedLights != null)
            {
                for (int i = 0; i < s_cachedLights.Count && s_cachedLightCount < k_maxSceneLights; i++)
                {
                    Light l = s_cachedLights[i];
                    if (l == null)
                    {
                        s_cachedLights.RemoveAt(i--);
                        continue;
                    }

                    if (!l.enabled || !l.gameObject.activeInHierarchy || l.intensity <= 0.001f)
                        continue;

                    Color linCol = l.color.linear * l.intensity;
                    if (l.type == LightType.Directional)
                    {
                        if (s_primaryShadowLight == null || s_primaryShadowLight.type != LightType.Directional)
                        {
                            s_primaryShadowLight = l;
                            s_shadowLightIndex = s_cachedLightCount;
                        }

                        Vector3 dirToLight = -l.transform.forward;
                        s_lightDataArray[s_cachedLightCount++] = new SceneLightData
                        {
                            posAndRange = Vector4.zero,
                            colorAndType = new Vector4(linCol.r, linCol.g, linCol.b, 0f),
                            dirAndSpot = new Vector4(dirToLight.x, dirToLight.y, dirToLight.z, 0f)
                        };
                    }
                    else if (l.type == LightType.Point || l.type == LightType.Spot)
                    {
                        if (l.type == LightType.Spot && s_primaryShadowLight == null)
                        {
                            s_primaryShadowLight = l;
                            s_shadowLightIndex = s_cachedLightCount;
                        }

                        Vector3 pos = l.transform.position;
                        float invRangeSqr = 1f / Mathf.Max(0.0001f, l.range * l.range);
                        Vector3 dirToSpot = (l.type == LightType.Spot) ? -l.transform.forward : Vector3.zero;
                        float cosHalfSpot = (l.type == LightType.Spot) ? Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad) : 0f;

                        s_lightDataArray[s_cachedLightCount++] = new SceneLightData
                        {
                            posAndRange = new Vector4(pos.x, pos.y, pos.z, invRangeSqr),
                            colorAndType = new Vector4(linCol.r, linCol.g, linCol.b, 1f),
                            dirAndSpot = new Vector4(dirToSpot.x, dirToSpot.y, dirToSpot.z, cosHalfSpot)
                        };
                    }
                }

                if (s_cachedLightCount > 0)
                    s_sceneLightsBuffer.SetData(s_lightDataArray, 0, 0, s_cachedLightCount);
            }

            // Render directional / spot shadow map once per frame (including optional splat self-shadows)
            bool activeShadowRecv = ReceiveShadows && s_primaryShadowLight != null;
            Matrix4x4 shadowVP = Matrix4x4.identity;
            float ndcShadowBias = 0.001f;

            if (activeShadowRecv)
            {
                if (s_shadowDepthShader == null)
                {
                    s_shadowDepthShader = Shader.Find("Hidden/GsplatShadowDepth");
                    if (s_shadowDepthShader == null)
                        s_shadowDepthShader = Resources.Load<Shader>("Shader/GsplatShadowDepth");
                }

                if (s_shadowRT == null)
                {
                    s_shadowRT = new RenderTexture(1024, 1024, 24, RenderTextureFormat.RFloat);
                    s_shadowRT.filterMode = FilterMode.Point;
                    s_shadowRT.Create();
                }
                if (s_shadowCam == null)
                {
                    var go = new GameObject("_GsplatShadowCam") { hideFlags = HideFlags.HideAndDontSave };
                    s_shadowCam = go.AddComponent<Camera>();
                    s_shadowCam.enabled = false;
                    s_shadowCam.clearFlags = CameraClearFlags.SolidColor;
                    s_shadowCam.backgroundColor = SystemInfo.usesReversedZBuffer ? Color.black : Color.white;
                    s_shadowCam.targetTexture = s_shadowRT;
                }

                if (s_primaryShadowLight.type == LightType.Directional)
                {
                    const float fixedDepthSpan = 100f;
                    const float shadowMapRes = 1024f;

                    Vector3 focusCenter = m_mainCamera.transform.position + m_mainCamera.transform.forward * (ShadowRange * 0.5f);
                    Quaternion lightRot = s_primaryShadowLight.transform.rotation;
                    Vector3 lightDir = s_primaryShadowLight.transform.forward;

                    // Texel-snapping prevents shadow swimming during camera movement
                    float texelSize = (ShadowRange * 2f) / shadowMapRes;
                    Vector3 lightSpacePos = Quaternion.Inverse(lightRot) * focusCenter;
                    lightSpacePos.x = Mathf.Floor(lightSpacePos.x / texelSize) * texelSize;
                    lightSpacePos.y = Mathf.Floor(lightSpacePos.y / texelSize) * texelSize;
                    Vector3 snappedFocusCenter = lightRot * lightSpacePos;

                    s_shadowCam.orthographic = true;
                    s_shadowCam.orthographicSize = ShadowRange;
                    s_shadowCam.nearClipPlane = 0.1f;
                    s_shadowCam.farClipPlane = fixedDepthSpan;
                    s_shadowCam.transform.SetPositionAndRotation(snappedFocusCenter - lightDir * (fixedDepthSpan * 0.5f), lightRot);

                    ndcShadowBias = ShadowBias / fixedDepthSpan;
                }
                else // Spot Light
                {
                    s_shadowCam.orthographic = false;
                    s_shadowCam.fieldOfView = s_primaryShadowLight.spotAngle;
                    s_shadowCam.nearClipPlane = 0.1f;
                    s_shadowCam.farClipPlane = s_primaryShadowLight.range;
                    s_shadowCam.transform.SetPositionAndRotation(s_primaryShadowLight.transform.position, s_primaryShadowLight.transform.rotation);

                    ndcShadowBias = ShadowBias * 0.01f;
                }

                Matrix4x4 gpuShadowProj = GL.GetGPUProjectionMatrix(s_shadowCam.projectionMatrix, false);
                shadowVP = gpuShadowProj * s_shadowCam.worldToCameraMatrix;

                if (s_lastShadowFrame != Time.frameCount && s_shadowDepthShader != null)
                {
                    s_lastShadowFrame = Time.frameCount;
                    s_shadowCam.RenderWithShader(s_shadowDepthShader, "RenderType");

                    if (SelfShadows)
                    {
                        int selfShadowPass = SplatMaterial.FindPass("SplatShadowDepth");
                        if (selfShadowPass >= 0)
                        {
                            if (s_selfShadowCmd == null)
                                s_selfShadowCmd = new UnityEngine.Rendering.CommandBuffer { name = "GsplatSelfShadow" };

                            s_selfShadowCmd.Clear();
                            s_selfShadowCmd.SetRenderTarget(s_shadowRT);

                            Matrix4x4 shadowWriteProj = GL.GetGPUProjectionMatrix(s_shadowCam.projectionMatrix, true);
                            m_propertyBlock.SetMatrix("_ShadowViewMatrix", s_shadowCam.worldToCameraMatrix);
                            m_propertyBlock.SetMatrix("_ShadowProjMatrix", shadowWriteProj);
                            m_propertyBlock.SetVector("_ShadowMapSize", new Vector2(1024f, 1024f));

                            Mesh shadowBatchMesh = GsplatSettings.GetBatchMesh();
                            int shadowInstanceCount = ((int)m_gpuResource.SplatCount + (int)GsplatSettings.SplatInstanceSize - 1) / (int)GsplatSettings.SplatInstanceSize;

                            s_selfShadowCmd.DrawMeshInstancedProcedural(shadowBatchMesh, 0, SplatMaterial, selfShadowPass, shadowInstanceCount, m_propertyBlock);
                            Graphics.ExecuteCommandBuffer(s_selfShadowCmd);
                        }
                    }
                }
            }

            int kernelProj = 0;
            int count = (int)m_gpuResource.SplatCount;
            Matrix4x4 mv = m_mainCamera.worldToCameraMatrix * transform.localToWorldMatrix;
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(m_mainCamera.projectionMatrix, m_mainCamera.targetTexture != null);

            ProjectCompute.SetInt("_SplatCount", count);
            ProjectCompute.SetInt("_UseEvaluatedColor", (m_shBands > 0 && m_gpuResource.EvaluatedColorBuffer != null) ? 1 : 0);
            ProjectCompute.SetInt("_GammaToLinear", QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
            ProjectCompute.SetFloat("_ScaleFactor", m_ScaleFactor);
            ProjectCompute.SetFloat("_Brightness", m_Brightness);

            // Relighting & shadow receiving uniforms
            ProjectCompute.SetInt("_EnableRelighting", EnableRelighting ? 1 : 0);
            ProjectCompute.SetFloat("_RelightStrength", RelightStrength);
            ProjectCompute.SetInt("_LightCount", EnableRelighting ? s_cachedLightCount : 0);
            ProjectCompute.SetVector("_AmbientColor", RenderSettings.ambientLight.linear);
            ProjectCompute.SetVector("_WorldSpaceCameraPos", m_mainCamera.transform.position);
            ProjectCompute.SetMatrix("_MatrixM", transform.localToWorldMatrix);

            ProjectCompute.SetInt("_ReceiveShadows", activeShadowRecv ? 1 : 0);
            ProjectCompute.SetInt("_ShadowLightIndex", s_shadowLightIndex);
            ProjectCompute.SetInt("_ReversedZ", SystemInfo.usesReversedZBuffer ? 1 : 0);
            ProjectCompute.SetFloat("_ShadowBias", ndcShadowBias);
            ProjectCompute.SetFloat("_ShadowStrength", ShadowStrength);
            ProjectCompute.SetVector("_ShadowMapSize", new Vector2(1024f, 1024f));
            ProjectCompute.SetMatrix("_ShadowMatrixVP", shadowVP);

            ProjectCompute.SetVector("_ScreenParams", new Vector4(m_mainCamera.pixelWidth, m_mainCamera.pixelHeight, 1f + 1f / m_mainCamera.pixelWidth, 1f + 1f / m_mainCamera.pixelHeight));
            ProjectCompute.SetVector("_OrthoParams", new Vector4(m_mainCamera.orthographicSize * m_mainCamera.aspect, m_mainCamera.orthographicSize, 0f, m_mainCamera.orthographic ? 1f : 0f));
            ProjectCompute.SetMatrix("_MatrixMV", mv);
            ProjectCompute.SetMatrix("_MatrixP", proj);

            ProjectCompute.SetBuffer(kernelProj, "_PositionBuffer", m_gpuResource.PositionBuffer);
            ProjectCompute.SetBuffer(kernelProj, "_EvaluatedColorBuffer", (m_shBands > 0 && m_gpuResource.EvaluatedColorBuffer != null) ? m_gpuResource.EvaluatedColorBuffer : SortResource.DummyBuffer);
            ProjectCompute.SetBuffer(kernelProj, "_SceneLightsBuffer", s_sceneLightsBuffer);
            ProjectCompute.SetTexture(kernelProj, "_SplatShadowMap", activeShadowRecv ? (Texture)s_shadowRT : Texture2D.blackTexture);
            ProjectCompute.SetBuffer(kernelProj, "_ProjectedSplatBuffer", m_gpuResource.ProjectedSplatBuffer);
            int groups = (count + 255) / 256;
            ProjectCompute.Dispatch(kernelProj, groups, 1, 1);
        }

        // 5. Submit instanced draw call (128 splats per instance)
        Mesh batchMesh = GsplatSettings.GetBatchMesh();
        int instanceCount = ((int)m_gpuResource.SplatCount + (int)GsplatSettings.SplatInstanceSize - 1) / (int)GsplatSettings.SplatInstanceSize;

        Graphics.RenderMeshPrimitives(rp, batchMesh, 0, instanceCount);
    }

    private Bounds CalcWorldBounds(Bounds localBounds, Transform trans)
    {
        Vector3 center = trans.TransformPoint(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = trans.TransformVector(new Vector3(extents.x, 0, 0));
        Vector3 axisY = trans.TransformVector(new Vector3(0, extents.y, 0));
        Vector3 axisZ = trans.TransformVector(new Vector3(0, 0, extents.z));
        return new Bounds(center, new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
        ) * 2f);
    }

    private void OnEnable()
    {
        if (s_cachedLights == null)
            s_cachedLights = new List<Light>(FindObjectsByType<Light>(FindObjectsSortMode.None));
        Core core = FindFirstObjectByType<Core>();
        core.parameterObjectAdded += OnParameterObjectAdded;
        core.parameterObjectRemoved += OnParameterObjectRemoved;

    }


    private void OnDisable()
    {
        Core core = FindFirstObjectByType<Core>();
        if (core != null)
        {
            core.parameterObjectAdded -= OnParameterObjectAdded;
            core.parameterObjectRemoved -= OnParameterObjectRemoved;
        }
    }

    private void OnDestroy()
    {
        // Shared scratch buffers (AltKeys, AltPayload, PassHist, GlobalHist) are managed 
        // centrally by SortResource and must NOT be disposed per individual object.

        // CRITICAL OWNERSHIP: This renderer instance is the sole keeper of its scene GPU buffers.
        // Safely wipe the unmanaged VRAM memory blocks when this object gets destroyed.
        if (m_gpuResource != null)
        {
            m_gpuResource.Dispose();
            m_gpuResource = null;
        }

        if (s_sceneLightsBuffer != null && s_sceneLightsBuffer.IsValid())
        {
            s_sceneLightsBuffer.Dispose();
            s_sceneLightsBuffer = null;
        }

        if (s_shadowRT != null)
        {
            s_shadowRT.Release();
            Destroy(s_shadowRT);
            s_shadowRT = null;
        }
        if (s_shadowCam != null)
        {
            Destroy(s_shadowCam.gameObject);
            s_shadowCam = null;
        }

        if (s_selfShadowCmd != null) 
        { 
            s_selfShadowCmd.Release(); 
            s_selfShadowCmd = null; 
        }
    }
}
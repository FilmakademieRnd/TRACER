using System;
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

            PositionBuffer.SetData(packedSplats);

            // Allocate SH buffers ONLY if higher SH bands (1-3) are present
            if (shLabels.IsCreated && shLabels.Length > 0 && shCentroids.IsCreated && shCentroids.Length > 0)
            {
                ScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, count, sizeof(uint));
                ScaleBuffer.SetData(shLabels);

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

            PositionBuffer = null;
            ScaleBuffer = null;
            SHCentroidsBuffer = null;
            EvaluatedColorBuffer = null;
            OrderBuffer = null;
            DistanceBuffer = null;
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

    public float m_Brightness = 1.0f;
    public float m_ScaleFactor = 1.0f;

    internal Material SplatMaterial;
    internal ComputeShader SortCompute;

    // Explicit object ownership: This renderer alone owns and destroys its GPU data resource
    private RenderResource m_gpuResource;
    public RenderResource GpuResource => m_gpuResource;
    public int SHBands => m_shBands;

    private MaterialPropertyBlock m_propertyBlock;
    private int m_shBands;
    private Camera m_mainCamera;
    private Vector3 m_lastCameraPos;
    private Quaternion m_lastCameraRot;

    [Header("Sorting Precision")]
    [Tooltip("Minimum camera movement in meters before re-sorting (default: 0.05m = 5cm)")]
    [SerializeField] private float m_positionThreshold = 0.05f;

    [Tooltip("Minimum camera rotation in degrees before re-sorting (default: 1.5 degrees)")]
    [SerializeField] private float m_rotationThreshold = 1.5f;

    [Tooltip("Minimum time in seconds between two GPU sorts to protect framerate (default: 0.03s = ~30Hz)")]
    [SerializeField] private float m_minSortInterval = 0.03f;

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
    private static readonly int k_scaleBuffer = Shader.PropertyToID("_ScaleBuffer");
    private static readonly int k_evalColorBuffer   = Shader.PropertyToID("_EvaluatedColorBuffer");
    private static readonly int k_shCentroidsBuffer = Shader.PropertyToID("_SHCentroidsBuffer");
    private static readonly int k_shDegree = Shader.PropertyToID("_SHDegree");
    private static readonly int k_brightness = Shader.PropertyToID("_Brightness");
    private static readonly int k_scaleFactor = Shader.PropertyToID("_ScaleFactor");
    private static readonly int k_matrixM = Shader.PropertyToID("_MATRIX_M");

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

    private void SetupPropertyBlock()
    {
        if (m_gpuResource == null) return;
        if (m_propertyBlock == null) m_propertyBlock = new MaterialPropertyBlock();

        m_propertyBlock.SetInt(k_splatCount, (int)m_gpuResource.SplatCount);
        m_propertyBlock.SetInt(k_shDegree, m_shBands);

        // Core geometry and sorting index buffers
        m_propertyBlock.SetBuffer(k_posBuffer, m_gpuResource.PositionBuffer);
        m_propertyBlock.SetBuffer(k_orderBuffer, m_gpuResource.OrderBuffer);

        // Bind pre-evaluated SH color buffer (with Metal dummy fallback when m_shBands == 0)
        GraphicsBuffer evalColorBuf = (m_shBands > 0 && m_gpuResource.EvaluatedColorBuffer != null)
            ? m_gpuResource.EvaluatedColorBuffer
            : SortResource.DummyBuffer;

        m_propertyBlock.SetBuffer(k_evalColorBuffer, evalColorBuf);
    }


    private void Update()
    {
        if (m_gpuResource == null || SortCompute == null) return;
        if (m_mainCamera == null) m_mainCamera = Camera.main;
        if (m_mainCamera == null) return;

        // Protect framerate during rapid camera movements
        if (Time.unscaledTime - m_lastSortTime < m_minSortInterval)
            return;

        Vector3 currentPos = m_mainCamera.transform.position;
        Quaternion currentRot = m_mainCamera.transform.rotation;

        // 1. Check linear movement distance (squared for performance)
        float posThresholdSqr = m_positionThreshold * m_positionThreshold;
        float travelDeltaSqr = (currentPos - m_lastCameraPos).sqrMagnitude;

        // 2. Check true angular rotation without 360-degree Euler wrap-around bugs
        float angleDelta = Quaternion.Angle(m_lastCameraRot, currentRot);

        if (travelDeltaSqr > posThresholdSqr || angleDelta > m_rotationThreshold)
        {
            m_lastCameraPos = currentPos;
            m_lastCameraRot = currentRot;
            m_lastSortTime = Time.unscaledTime;
            DispatchSort();
        }
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

        // 1. Update only per-frame dynamic uniforms (Buffers & SplatCount are already bound in SetupPropertyBlock!)
        m_propertyBlock.SetMatrix(k_matrixM, transform.localToWorldMatrix);
        m_propertyBlock.SetFloat(k_scaleFactor, m_ScaleFactor);
        m_propertyBlock.SetFloat(k_brightness, m_Brightness);

        // 2. Setup RenderParams using pre-allocated batch mesh and world-space bounds
        RenderParams rp = new RenderParams(SplatMaterial)
        {
            worldBounds = CalcWorldBounds(m_gpuResource.Bounds, transform),
            matProps = m_propertyBlock,
            layer = gameObject.layer
        };

        // 3. Submit instanced draw call (128 splats per instance)
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
    }
}
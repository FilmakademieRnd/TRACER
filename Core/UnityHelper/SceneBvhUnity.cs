/*
-----------------------------------------------------------------------------------
TRACER FOUNDATION -
Toolset for Realtime Animation, Collaboration & Extended Reality

Copyright (c) 2024 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs
https://research.animationsinstitut.de/tracer 
https://github.com/FilmakademieRnd/TRACER

TRACER FOUNDATION is a development by Filmakademie Baden-Wuerttemberg,
Animationsinstitut R&D Labs in the scope of the EU funded project
MAX-R (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.
Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the
TRACER FOUNDATION development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program;
if not go to https://opensource.org/licenses/MIT
-----------------------------------------------------------------------------------
*/


//! @file "SceneBvhUnity.cs"
//! @brief Unity adapter for SceneBvh: mesh source, tag handling, mesh backend, facade, drop-in replacement
//! @author Thomas Krüger
//! @version 1
//! @date 18.11.2026
//! @changed fixes for the test cases (SCeneBvhTests)
//!
//! Contents
//!   SceneBvhUnityExt      conversions Vector3/Bounds/Matrix4x4 <-> core types
//!   UnityMeshSource       IMeshSource over Mesh.AcquireReadOnlyMeshData (no managed copy of the geometry)
//!   MeshSceneQuery        mesh backend: scans/registers MeshRenderers, tags, dynamic refresh, build slicing
//!   SplatSceneQuery       empty backend stub (Capabilities = None, everything Unsupported)
//!   SceneQueryService     facade: holds the active backend, switches it at runtime, raises an event
//!   SceneQueryRunner      optional MonoBehaviour that calls SceneQueryService.Tick() every frame
//!   HeightProbe           cached height-over-ground value that updates when a mover passes underneath
//!   StaticMeshBvh         drop-in replacement for StaticMeshQuadtree (same public API)
//!   SceneBvhBenchmark     synthetic benchmark (Editor and Development builds only)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using URay = UnityEngine.Ray;

namespace tracer.spatial
{
    #region CONVERSIONS

    public static class SceneBvhUnityExt
    {
        public static Float3 ToFloat3(this Vector3 v) => new Float3(v.x, v.y, v.z);
        public static Vector3 ToVector3(this Float3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Aabb ToAabb(this Bounds b) => new Aabb(b.min.ToFloat3(), b.max.ToFloat3());
        public static Bounds ToBounds(this Aabb a)
        {
            Bounds b = new Bounds();
            b.SetMinMax(a.Min.ToVector3(), a.Max.ToVector3());
            return b;
        }

        //! Unity matrices are row major in the m<row><col> fields, translation in m03/m13/m23
        public static Affine3x4 ToAffine(this Matrix4x4 m)
        {
            Affine3x4 a;
            a.M00 = m.m00; a.M01 = m.m01; a.M02 = m.m02; a.M03 = m.m03;
            a.M10 = m.m10; a.M11 = m.m11; a.M12 = m.m12; a.M13 = m.m13;
            a.M20 = m.m20; a.M21 = m.m21; a.M22 = m.m22; a.M23 = m.m23;
            return a;
        }
    }

    #endregion

    #region MESH SOURCE

    //!
    //! reads positions and triangle indices straight from the mesh's CPU data (Mesh.AcquireReadOnlyMeshData),
    //! no Vector3[] / int[] copy. Triangle submeshes are concatenated (baseVertex is applied), other
    //! topologies are skipped. Float32 positions are read in place from their vertex stream; for other
    //! position formats a temporary Vector3 copy is made (freed in Release()).
    //!
    //! @note The mesh must be readable and must stay alive and unmodified until the build is finished
    //!       (MeshSceneQuery checks this every Tick). Release() disposes the MeshDataArray.
    //!
    public struct UnityMeshSource : IMeshSource
    {
        private Mesh.MeshDataArray array;
        private bool acquired;
        private NativeArray<float> vdata;
        private NativeArray<Vector3> fallback;
        private bool useFallback;
        private int strideF, offsetF, vertCount;
        private NativeArray<ushort> i16;
        private NativeArray<uint> i32;
        private bool is32;
        private int[] subTriStart, subIndexStart, subBase;
        private int subCount, triTotal, lastSub;

        public int VertexCount => vertCount;
        public int TriangleCount => triTotal;

        public static bool TryCreate(Mesh mesh, out UnityMeshSource s)
        {
            s = default(UnityMeshSource);
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) return false;
            Mesh.MeshDataArray arr = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData md = arr[0];
            s.array = arr;
            s.acquired = true;
            s.vertCount = md.vertexCount;

            if (md.HasVertexAttribute(VertexAttribute.Position) &&
                md.GetVertexAttributeFormat(VertexAttribute.Position) == VertexAttributeFormat.Float32 &&
                md.GetVertexAttributeDimension(VertexAttribute.Position) >= 3)
            {
                int stream = md.GetVertexAttributeStream(VertexAttribute.Position);
                s.strideF = md.GetVertexBufferStride(stream) / 4;
                s.offsetF = md.GetVertexAttributeOffset(VertexAttribute.Position) / 4;
                s.vdata = md.GetVertexData<float>(stream);
            }
            else
            {
                s.fallback = new NativeArray<Vector3>(md.vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                md.GetVertices(s.fallback);
                s.useFallback = true;
            }

            s.is32 = md.indexFormat == IndexFormat.UInt32;
            if (s.is32) s.i32 = md.GetIndexData<uint>(); else s.i16 = md.GetIndexData<ushort>();

            int sc = md.subMeshCount;
            s.subCount = sc;
            s.subTriStart = new int[sc + 1];
            s.subIndexStart = new int[sc];
            s.subBase = new int[sc];
            int cum = 0;
            for (int i = 0; i < sc; i++)
            {
                SubMeshDescriptor d = md.GetSubMesh(i);
                s.subTriStart[i] = cum;
                s.subIndexStart[i] = d.indexStart;
                s.subBase[i] = d.baseVertex;
                if (d.topology == MeshTopology.Triangles) cum += d.indexCount / 3;
            }
            s.subTriStart[sc] = cum;
            s.triTotal = cum;
            return true;
        }

        public Float3 GetPosition(int v)
        {
            if (useFallback) { Vector3 p = fallback[v]; return new Float3(p.x, p.y, p.z); }
            int k = v * strideF + offsetF;
            return new Float3(vdata[k], vdata[k + 1], vdata[k + 2]);
        }

        public void GetTriangle(int t, out int a, out int b, out int c)
        {
            int s = lastSub;
            if (t < subTriStart[s] || t >= subTriStart[s + 1])
            {
                s = 0;
                while (s < subCount - 1 && t >= subTriStart[s + 1]) s++;
                lastSub = s;
            }
            int k = subIndexStart[s] + 3 * (t - subTriStart[s]);
            int bv = subBase[s];
            if (is32) { a = (int)i32[k] + bv; b = (int)i32[k + 1] + bv; c = (int)i32[k + 2] + bv; }
            else { a = i16[k] + bv; b = i16[k + 1] + bv; c = i16[k + 2] + bv; }
        }

        public void Release()
        {
            if (fallback.IsCreated) fallback.Dispose();
            if (acquired) { array.Dispose(); acquired = false; }
        }
    }

    #endregion

    #region MESH BACKEND

    //!
    //! mesh backend: registers MeshRenderers of the scene, owns the SceneBvh. Objects below a transform
    //! with one of the configured tags are DYNAMIC (matrix refreshed lazily at query time), everything
    //! else is static. Objects can be added and removed at runtime (Add / Remove).
    //!
    //! Mesh builds: BuildMode.Lazy (default, no Tick needed, first query that needs the mesh blocks),
    //! Sliced (call Tick() every frame, queries are approximate until ready) or Immediate.
    //!
    //! @note Only MeshFilter + MeshRenderer with readable meshes. SkinnedMeshRenderer is not supported.
    //! @note Not thread safe, main thread only.
    //!
    public sealed class MeshSceneQuery : ISceneQuery
    {
        private sealed class Entry
        {
            public MeshRenderer Renderer;
            public Transform Transform;
            public InstanceHandle Handle;
            public bool Dynamic;
        }

        private struct Pending { public Mesh Mesh; public MeshBvh Bvh; }

        //! the core scene (public for Version / TryGetChangedBoundsSince / advanced use)
        public readonly SceneBvh Scene = new SceneBvh();
        //! settings used for every mesh that is added afterwards
        public BuildOptions MeshOptions = BuildOptions.Default;
        public BuildMode MeshBuildMode = BuildMode.Lazy;
        //! milliseconds per Tick() spent on sliced mesh builds
        public double BuildBudgetMs = 2.0;
        //! BuildTree() also registers renderers of inactive objects (the old StaticMeshQuadtree did)
        public bool IncludeInactive = true;

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly List<string> dynamicTags = new List<string>();
        private readonly List<Pending> pending = new List<Pending>();
        private readonly List<int> removeBuf = new List<int>();
        private readonly List<MeshRenderer> rendererBuf = new List<MeshRenderer>();
        private InstanceHandle[] ignoreBuf = new InstanceHandle[16];
        private int tickCount, lastRefreshFrame = -1;

        public MeshSceneQuery() { Scene.DynamicTransformProvider = ProvideTransform; }

        public QueryCapabilities Capabilities => QueryCapabilities.All;
        public int InstanceCount => entries.Count;
        public int Version => Scene.Version;

        #region registration

        //!
        //! tags that mark moving objects (checked on the renderer's transform and all its parents).
        //! The tags must exist in the Tag Manager (CompareTag logs an error otherwise).
        //! Instances that are already registered are updated.
        //!
        public void SetDynamicTags(IEnumerable<string> tags)
        {
            dynamicTags.Clear();
            if (tags != null) foreach (string t in tags) if (!string.IsNullOrEmpty(t)) dynamicTags.Add(t);
            foreach (KeyValuePair<int, Entry> kv in entries)
            {
                Entry e = kv.Value;
                bool dyn = e.Transform != null && IsDynamic(e.Transform);
                if (dyn == e.Dynamic) continue;
                e.Dynamic = dyn;
                Scene.SetFlags(e.Handle, dyn ? InstanceFlags.Snappable | InstanceFlags.Dynamic : InstanceFlags.Snappable);
                Scene.Refresh(e.Handle);
            }
        }

        private bool IsDynamic(Transform t)
        {
            if (dynamicTags.Count == 0) return false;
            for (Transform p = t; p != null; p = p.parent)
                for (int i = 0; i < dynamicTags.Count; i++)
                    if (p.CompareTag(dynamicTags[i])) return true;
            return false;
        }

        //! registers all MeshRenderers of the loaded scenes (like StaticMeshQuadtree.BuildTree)
        public void BuildTree()
        {
            MeshRenderer[] all = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                IncludeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int added = 0;
            for (int i = 0; i < all.Length; i++) if (Add(all[i])) added++;
            Debug.Log($"MeshSceneQuery registered {added} of {all.Length} mesh renderers ({Scene.PendingBuilds} mesh builds pending).");
        }

        //! registers one renderer (false if unusable or already registered)
        public bool Add(MeshRenderer rend)
        {
            if (rend == null) return false;
            int id = rend.GetInstanceID();
            if (entries.ContainsKey(id)) return false;
            if (!rend.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) return false;
            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                Debug.LogWarning("Mesh of '" + rend.gameObject.name + "' `isReadable` not set! Queries will never hit it!");
                return false;
            }
            long key = mesh.GetInstanceID();
            if (!Scene.TryGetMesh(key, out MeshBvh bvh))
            {
                if (!UnityMeshSource.TryCreate(mesh, out UnityMeshSource src)) return false;
                bvh = Scene.AddMesh(key, src, mesh.bounds.ToAabb(), MeshOptions, MeshBuildMode);
                if (bvh.State != MeshState.Ready) pending.Add(new Pending { Mesh = mesh, Bvh = bvh });
            }
            Transform tr = rend.transform;
            bool dyn = IsDynamic(tr);
            InstanceFlags flags = dyn ? InstanceFlags.Snappable | InstanceFlags.Dynamic : InstanceFlags.Snappable;
            InstanceHandle h = Scene.Register(bvh, tr.localToWorldMatrix.ToAffine(), 1u << rend.gameObject.layer, flags, id);
            entries.Add(id, new Entry { Renderer = rend, Transform = tr, Handle = h, Dynamic = dyn });
            return true;
        }

        //! registers all MeshRenderers below 'root' (e.g. an object that arrived over the network)
        public int Add(GameObject root)
        {
            int n = 0;
            root.GetComponentsInChildren(true, rendererBuf);
            for (int i = 0; i < rendererBuf.Count; i++) if (Add(rendererBuf[i])) n++;
            rendererBuf.Clear();
            return n;
        }

        public bool Remove(MeshRenderer rend) => rend != null && RemoveById(rend.GetInstanceID());

        //! removes all registered MeshRenderers below 'root'. Destroyed objects are removed by Tick().
        public int Remove(GameObject root)
        {
            int n = 0;
            root.GetComponentsInChildren(true, rendererBuf);
            for (int i = 0; i < rendererBuf.Count; i++) if (RemoveById(rendererBuf[i].GetInstanceID())) n++;
            rendererBuf.Clear();
            return n;
        }

        private bool RemoveById(int id)
        {
            if (!entries.TryGetValue(id, out Entry e)) return false;
            Scene.Unregister(e.Handle);
            entries.Remove(id);
            return true;
        }

        //! the renderer that belongs to a hit (RayHit.UserData / ClosestPointHit.UserData)
        public bool TryGetRenderer(int userData, out MeshRenderer renderer)
        {
            renderer = null;
            if (!entries.TryGetValue(userData, out Entry e) || e.Renderer == null) return false;
            renderer = e.Renderer;
            return true;
        }

        //!
        //! all queries skip every registered renderer below 'root' (the object that is being dragged).
        //! Call again if renderers were added below root afterwards. null clears.
        //!
        public void SetIgnoredRoot(Transform root)
        {
            int n = 0;
            if (root != null)
            {
                root.GetComponentsInChildren(true, rendererBuf);
                for (int i = 0; i < rendererBuf.Count; i++)
                {
                    if (!entries.TryGetValue(rendererBuf[i].GetInstanceID(), out Entry e)) continue;
                    if (n == ignoreBuf.Length) Array.Resize(ref ignoreBuf, n * 2);
                    ignoreBuf[n++] = e.Handle;
                }
                rendererBuf.Clear();
            }
            Scene.SetIgnoreSet(ignoreBuf, n);
        }

        #endregion

        #region per frame

        //!
        //! call once per frame (SceneQueryService.Tick / SceneQueryRunner): steps sliced mesh builds within
        //! BuildBudgetMs and removes entries of destroyed objects.
        //!
        public void Tick()
        {
            if (pending.Count > 0)
            {
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    Pending p = pending[i];
                    if (p.Bvh.State == MeshState.Ready || p.Bvh.State == MeshState.Removed) { pending.RemoveAt(i); continue; }
                    if (p.Mesh == null)
                    {
                        Debug.LogWarning("Mesh destroyed while its BVH was building, build cancelled.");
                        p.Bvh.Cancel();
                        pending.RemoveAt(i);
                    }
                }
                if (MeshBuildMode == BuildMode.Sliced) Scene.StepBuilds(BuildBudgetMs);
            }
            if ((++tickCount & 15) == 0) PurgeDestroyed();
        }

        private void PurgeDestroyed()
        {
            removeBuf.Clear();
            foreach (KeyValuePair<int, Entry> kv in entries) if (kv.Value.Renderer == null) removeBuf.Add(kv.Key);
            for (int i = 0; i < removeBuf.Count; i++) RemoveById(removeBuf[i]);
            removeBuf.Clear();
        }

        private bool ProvideTransform(int userData, out Affine3x4 m)
        {
            if (entries.TryGetValue(userData, out Entry e) && e.Transform != null)
            {
                m = e.Transform.localToWorldMatrix.ToAffine();
                return true;
            }
            m = default(Affine3x4);
            return false;
        }

        //! refreshes all dynamic instances (at most once per frame), e.g. before reading change information
        public void RefreshDynamics()
        {
            if (lastRefreshFrame == Time.frameCount) return;
            lastRefreshFrame = Time.frameCount;
            Scene.BeginBatch();
            Scene.EndBatch();
        }

        public bool TryGetChangedBoundsSince(int sinceVersion, out Bounds bounds)
        {
            bool changed = Scene.TryGetChangedBoundsSince(sinceVersion, out Aabb a);
            bounds = changed ? a.ToBounds() : default(Bounds);
            return changed;
        }

        #endregion

        #region ISceneQuery

        public QueryStatus Raycast(in Ray ray, float maxDist, uint layerMask, InstanceHandle ignore, RayMode mode, out RayHit hit) =>
            Scene.Raycast(ray, maxDist, layerMask, ignore, mode, out hit);

        public QueryStatus HeightOverGround(in Float3 origin, QueryAccuracy accuracy, float defaultY, out float groundY,
                                            InstanceHandle ignore, uint layerMask, float vertexSnapRadius) =>
            Scene.HeightOverGround(origin, accuracy, defaultY, out groundY, ignore, layerMask, vertexSnapRadius);

        public QueryStatus ClosestPoint(in Float3 point, float maxDist, uint layerMask, InstanceHandle ignore, out ClosestPointHit hit) =>
            Scene.ClosestPoint(point, maxDist, layerMask, ignore, out hit);

        public QueryStatus OverlapSphere(in Float3 center, float radius, uint layerMask, InstanceHandle ignore,
                                         InstanceHandle[] results, out int count) =>
            Scene.OverlapSphere(center, radius, layerMask, ignore, results, out count);

        public QueryStatus OverlapAabb(in Aabb box, uint layerMask, InstanceHandle ignore, InstanceHandle[] results, out int count) =>
            Scene.OverlapAabb(box, layerMask, ignore, results, out count);

        #endregion
    }

    #endregion

    #region SPLAT STUB

    //! placeholder for a Gaussian splat backend: supports nothing, every query reports Unsupported
    public sealed class SplatSceneQuery : ISceneQuery
    {
        public QueryCapabilities Capabilities => QueryCapabilities.None;

        public QueryStatus Raycast(in Ray ray, float maxDist, uint layerMask, InstanceHandle ignore, RayMode mode, out RayHit hit)
        { hit = default(RayHit); return QueryStatus.Unsupported; }

        public QueryStatus HeightOverGround(in Float3 origin, QueryAccuracy accuracy, float defaultY, out float groundY,
                                            InstanceHandle ignore, uint layerMask, float vertexSnapRadius)
        { groundY = defaultY; return QueryStatus.Unsupported; }

        public QueryStatus ClosestPoint(in Float3 point, float maxDist, uint layerMask, InstanceHandle ignore, out ClosestPointHit hit)
        { hit = default(ClosestPointHit); return QueryStatus.Unsupported; }

        public QueryStatus OverlapSphere(in Float3 center, float radius, uint layerMask, InstanceHandle ignore,
                                         InstanceHandle[] results, out int count)
        { count = 0; return QueryStatus.Unsupported; }

        public QueryStatus OverlapAabb(in Aabb box, uint layerMask, InstanceHandle ignore, InstanceHandle[] results, out int count)
        { count = 0; return QueryStatus.Unsupported; }
    }

    #endregion

    #region FACADE

    public enum SceneQueryMode { Mesh, Splat }

    //!
    //! holds the active query backend. Callers talk to the facade only, so that the mesh/splat switch is
    //! invisible to them. Check Supports(..) / the QueryStatus.Unsupported result before relying on a query.
    //!
    public sealed class SceneQueryService
    {
        private ISceneQuery active;

        public MeshSceneQuery Mesh { get; }
        public SplatSceneQuery Splat { get; }
        public ISceneQuery Active => active;
        public SceneQueryMode Mode { get; private set; }
        public QueryCapabilities Capabilities => active.Capabilities;

        //! raised after the backend was replaced (new active backend as argument)
        public event Action<ISceneQuery> BackendChanged;

        public SceneQueryService()
        {
            Mesh = new MeshSceneQuery();
            Splat = new SplatSceneQuery();
            active = Mesh;
            Mode = SceneQueryMode.Mesh;
        }

        public bool Supports(QueryCapabilities c) => (active.Capabilities & c) == c;

        public void SetMode(SceneQueryMode mode)
        {
            Mode = mode;
            SetBackend(mode == SceneQueryMode.Mesh ? (ISceneQuery)Mesh : Splat);
        }

        //! replace the active backend (e.g. with a real splat implementation)
        public void SetBackend(ISceneQuery backend)
        {
            if (backend == null || ReferenceEquals(backend, active)) return;
            active = backend;
            BackendChanged?.Invoke(active);
        }

        //! per frame: mesh builds and cleanup (not needed with BuildMode.Lazy/Immediate and no destroyed objects)
        public void Tick() { Mesh.Tick(); }

        //! Unity friendly overloads (layerMask in Unity bit format, -1 = everything)
        public QueryStatus Raycast(URay ray, float maxDist, out RayHit hit, int layerMask = -1, RayMode mode = RayMode.DoubleSided) =>
            active.Raycast(new Ray(ray.origin.ToFloat3(), ray.direction.ToFloat3()), maxDist, (uint)layerMask,
                           default(InstanceHandle), mode, out hit);

        public QueryStatus HeightOverGround(Vector3 origin, QueryAccuracy accuracy, float defaultY, out float groundY,
                                            int layerMask = -1, float vertexSnapRadius = float.PositiveInfinity) =>
            active.HeightOverGround(origin.ToFloat3(), accuracy, defaultY, out groundY, default(InstanceHandle),
                                    (uint)layerMask, vertexSnapRadius);

        public QueryStatus ClosestPoint(Vector3 point, float maxDist, out ClosestPointHit hit, int layerMask = -1) =>
            active.ClosestPoint(point.ToFloat3(), maxDist, (uint)layerMask, default(InstanceHandle), out hit);

        public QueryStatus OverlapSphere(Vector3 center, float radius, InstanceHandle[] results, out int count, int layerMask = -1) =>
            active.OverlapSphere(center.ToFloat3(), radius, (uint)layerMask, default(InstanceHandle), results, out count);

        public QueryStatus OverlapBounds(Bounds box, InstanceHandle[] results, out int count, int layerMask = -1) =>
            active.OverlapAabb(box.ToAabb(), (uint)layerMask, default(InstanceHandle), results, out count);

        //! the dragged object (all its renderers) is skipped by all mesh queries; null clears
        public void SetIgnoredRoot(Transform root) { Mesh.SetIgnoredRoot(root); }
    }

    //! optional driver: add it to a GameObject and assign the service, it calls Tick() every frame
    public sealed class SceneQueryRunner : MonoBehaviour
    {
        public SceneQueryService Service;
        private void Update() { if (Service != null) Service.Tick(); }
    }

    #endregion

    #region HEIGHT PROBE

    //!
    //! height over ground of a position that stays valid while nothing changes. Update() re-queries only if the
    //! position moved, the backend was switched, or a registered object changed inside the vertical column
    //! below the position (e.g. a moving object passes underneath), so it is cheap to call every frame.
    //!
    public sealed class HeightProbe
    {
        private readonly SceneQueryService service;
        private ISceneQuery lastBackend;
        private Vector3 lastOrigin;
        private int seenVersion;
        private bool valid;

        public QueryAccuracy Accuracy = QueryAccuracy.ExactMesh;
        public float DefaultY;
        public float VertexSnapRadius = float.PositiveInfinity;
        public int LayerMask = -1;

        public float Height { get; private set; }
        public QueryStatus Status { get; private set; }

        public HeightProbe(SceneQueryService service) { this.service = service; }

        //! @return true if Height or Status changed
        public bool Update(Vector3 origin)
        {
            ISceneQuery backend = service.Active;
            bool dirty = !valid || !ReferenceEquals(backend, lastBackend) ||
                         origin.x != lastOrigin.x || origin.y != lastOrigin.y || origin.z != lastOrigin.z;
            MeshSceneQuery mq = backend as MeshSceneQuery;
            if (mq != null)
            {
                mq.RefreshDynamics();
                if (!dirty && mq.Scene.TryGetChangedBoundsSince(seenVersion, out Aabb box) &&
                    box.Min.Y <= origin.y && origin.x >= box.Min.X && origin.x <= box.Max.X &&
                    origin.z >= box.Min.Z && origin.z <= box.Max.Z)
                    dirty = true;
            }
            bool changed = false;
            if (dirty)
            {
                QueryStatus st = service.HeightOverGround(origin, Accuracy, DefaultY, out float y, LayerMask, VertexSnapRadius);
                changed = !valid || st != Status || y != Height;
                Status = st; Height = y;
                lastOrigin = origin; lastBackend = backend; valid = true;
            }
            if (mq != null) seenVersion = mq.Scene.Version;
            return changed;
        }
    }

    #endregion

    #region DROP-IN

    //!
    //! drop-in replacement for StaticMeshQuadtree: same public API (BuildTree, GetHeightOverGround).
    //! Unlike the quadtree it also handles moving objects (tags), runtime add/remove and exact distances.
    //! Use 'Query' for everything beyond the old API.
    //!
    //! @note References RayMeshUtility (global, main assembly). If SceneBvh*.cs move into an asmdef, move
    //!       this region into a file of your main assembly (an asmdef cannot reference Assembly-CSharp).
    //!
    public class StaticMeshBvh
    {
        public readonly MeshSceneQuery Query = new MeshSceneQuery();

        //! scans the scene (all MeshRenderers). Same name as the old method.
        public void BuildTree() { Query.BuildTree(); }

        //! call once per frame only if Query.MeshBuildMode is Sliced
        public void Tick() { Query.Tick(); }

        //!
        //! finds the highest point on meshes below 'origin' (same signature as StaticMeshQuadtree)
        //!
        //! @param accuracy the accuracy we want to use when searching for the highest point
        //! @param origin the position to use the check at
        //! @param defaultGroundY our ground (lowest) y position
        //!
        public float GetHeightOverGround(RayMeshUtility.Accuracy accuracy, Vector3 origin, float defaultGroundY = 0f)
        {
            QueryAccuracy acc;
            switch (accuracy)
            {
                case RayMeshUtility.Accuracy.BoundingBox: acc = QueryAccuracy.BoundingBox; break;
                case RayMeshUtility.Accuracy.NearestVertex: acc = QueryAccuracy.NearestVertex; break;
                default: acc = QueryAccuracy.ExactMesh; break;
            }
            Query.HeightOverGround(origin.ToFloat3(), acc, defaultGroundY, out float y, default(InstanceHandle),
                                   uint.MaxValue, float.PositiveInfinity);
            return y;
        }
    }

    #endregion

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region BENCHMARK

    //!
    //! synthetic benchmark (Editor menu "Tools/TRACER/SceneBvh Benchmark" or the runner below in a Development
    //! build, e.g. WebGL: add the scripting define SCENEBVH_BENCH_AUTORUN, results go to the console and OnGUI).
    //!
    //! Scenarios (Scale multiplies all triangle counts, use ~0.2 on phones):
    //!   A  1 mesh with 5M triangles (terrain)
    //!   B  3 meshes with 2M triangles (terrain, noisy sphere, terrain)
    //!   C  20 instances of one 1M triangle mesh (rotated + scaled)
    //!   D  static 1M terrain + 5 MOVING instances of a 200k mesh, per-frame refresh + height queries
    //!   E  1M triangle Unity Mesh through the adapter (MeshSceneQuery, UnityMeshSource, sliced build)
    //! Reported per scenario: build (CPU time, wall time, per-step distribution, peak managed memory), final
    //! BVH memory, query times (mean/p50/p95/p99/max per query, computed from batches because timer
    //! resolution can be coarse on WebGL), managed allocation and GC count during the query loops.
    //!
    //! @note Memory numbers are managed heap numbers (GC.GetTotalMemory) plus Profiler heap sizes. The Editor
    //!       (Mono, safety checks) is NOT representative for IL2CPP/WebGL, measure in a Development build.
    //!
    public sealed class BenchmarkConfig
    {
        public float Scale = 1f;
        public double BuildBudgetMs = 2.0;   //!< budget per StepBuilds/Tick call
        public int Queries = 20000;          //!< per query kind
        public int Batch = 20;               //!< queries per timed batch
        public int LeafSize = 8;
        public bool Quantize16 = true;
        public bool RunA = true, RunB = true, RunC = true, RunD = true, RunE = true;
    }

    public static class SceneBvhBenchmark
    {
        //! markdown report of the last finished run
        public static string LastReport { get; private set; }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string F2(double v) => v.ToString("F2", Inv);
        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("F1", Inv);
        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private sealed class Rep
        {
            public readonly StringBuilder Sb = new StringBuilder();
            public Action<string> Log;
            public void Line(string s = "") { Sb.AppendLine(s); if (Log != null) Log(s); }
        }

        private struct Dist { public int N; public double Mean, P50, P95, P99, Max; }

        private sealed class BuildResult
        {
            public double CpuMs, WallMs;
            public int Steps, Over2x, Over16Ms;
            public Dist StepMs;
            public long PeakManaged;
        }

        private sealed class MeshSpec { public bool Sphere; public int Tris; public float Size; public float Seed; }

        private static Dist Summarize(long[] ticks, int n, double toUnit)
        {
            Dist d = new Dist { N = n };
            if (n == 0) return d;
            long[] c = new long[n];
            Array.Copy(ticks, c, n);
            Array.Sort(c);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += c[i];
            d.Mean = sum / n * toUnit;
            d.P50 = c[(int)(0.50 * (n - 1))] * toUnit;
            d.P95 = c[(int)(0.95 * (n - 1))] * toUnit;
            d.P99 = c[(int)(0.99 * (n - 1))] * toUnit;
            d.Max = c[n - 1] * toUnit;
            return d;
        }

        private static string Row(Dist d) => F2(d.Mean) + " | " + F2(d.P50) + " | " + F2(d.P95) + " | " + F2(d.P99) + " | " + F2(d.Max);

        private static long Managed(bool collect)
        {
            if (collect) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
            return GC.GetTotalMemory(false);
        }

        private static BuildOptions Opts(BenchmarkConfig cfg)
        {
            BuildOptions o = BuildOptions.Default;
            o.LeafSize = cfg.LeafSize;
            o.Quantize16 = cfg.Quantize16;
            o.KeepSourceTriangleIds = false;
            o.ReleaseSource = true;
            return o;
        }

        #region synthetic geometry

        private static float TerrainHeight(float x, float z, float seed) =>
            6f * Mathf.Sin(x * 0.015f + seed) * Mathf.Cos(z * 0.02f) + 2f * Mathf.Sin(x * 0.11f + z * 0.09f) +
            0.4f * Mathf.Sin(x * 0.9f) * Mathf.Sin(z * 0.7f);

        //! indexed height field, shared vertices, 2*n*n triangles
        private static void MakeTerrain(int n, float size, float seed, out Float3[] pos, out int[] idx)
        {
            int vn = n + 1;
            pos = new Float3[vn * vn];
            float cell = size / n;
            for (int z = 0; z < vn; z++)
                for (int x = 0; x < vn; x++)
                {
                    float px = x * cell - size * 0.5f, pz = z * cell - size * 0.5f;
                    pos[z * vn + x] = new Float3(px, TerrainHeight(px, pz, seed), pz);
                }
            idx = new int[6 * n * n];
            int k = 0;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * vn + x, b = a + 1, c = a + vn, d = c + 1;
                    idx[k++] = a; idx[k++] = c; idx[k++] = d;
                    idx[k++] = a; idx[k++] = d; idx[k++] = b;
                }
        }

        //! closed, bumpy sphere with shared vertices, 2*rings*seg triangles
        private static void MakeSphere(int rings, int seg, float radius, out Float3[] pos, out int[] idx)
        {
            pos = new Float3[(rings + 1) * seg];
            for (int i = 0; i <= rings; i++)
            {
                float th = Mathf.PI * i / rings;
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    float r = radius * (1f + 0.08f * Mathf.Sin(5f * ph) * Mathf.Sin(4f * th));
                    pos[i * seg + j] = new Float3(r * Mathf.Sin(th) * Mathf.Cos(ph), r * Mathf.Cos(th), r * Mathf.Sin(th) * Mathf.Sin(ph));
                }
            }
            idx = new int[rings * seg * 6];
            int k = 0;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int v00 = i * seg + j, v01 = i * seg + (j + 1) % seg, v10 = (i + 1) * seg + j, v11 = (i + 1) * seg + (j + 1) % seg;
                    idx[k++] = v00; idx[k++] = v10; idx[k++] = v11;
                    idx[k++] = v00; idx[k++] = v11; idx[k++] = v01;
                }
        }

        private static void Generate(MeshSpec s, out Float3[] pos, out int[] idx)
        {
            if (s.Sphere)
            {
                int rings = Math.Max(4, (int)Math.Sqrt(s.Tris / 4.0));
                MakeSphere(rings, rings * 2, s.Size, out pos, out idx);
            }
            else MakeTerrain(Math.Max(2, (int)Math.Ceiling(Math.Sqrt(s.Tris / 2.0))), s.Size, s.Seed, out pos, out idx);
        }

        private static Affine3x4 MatY(float angle, float scale, float x, float y, float z)
        {
            float c = Mathf.Cos(angle) * scale, s = Mathf.Sin(angle) * scale;
            Affine3x4 m = default(Affine3x4);
            m.M00 = c; m.M02 = s; m.M11 = scale; m.M20 = -s; m.M22 = c;
            m.M03 = x; m.M13 = y; m.M23 = z;
            return m;
        }

        #endregion

        #region measurement pieces

        //! runs 'step' (one StepBuilds/Tick call per frame) until it reports no pending build, times every call
        private static IEnumerator BuildLoop(Func<int> step, double budgetMs, BuildResult r)
        {
            List<long> samples = new List<long>(4096);
            long wall0 = Stopwatch.GetTimestamp(), cpu = 0, peak = 0;
            while (true)
            {
                long t0 = Stopwatch.GetTimestamp();
                int pending = step();
                long dt = Stopwatch.GetTimestamp() - t0;
                samples.Add(dt);
                cpu += dt;
                long m = GC.GetTotalMemory(false);
                if (m > peak) peak = m;
                if (pending == 0) break;
                yield return null;
            }
            long[] arr = samples.ToArray();
            r.Steps = arr.Length;
            r.CpuMs = Ms(cpu);
            r.WallMs = Ms(Stopwatch.GetTimestamp() - wall0);
            r.PeakManaged = peak;
            r.StepMs = Summarize(arr, arr.Length, 1000.0 / Stopwatch.Frequency);
            long b2 = (long)(budgetMs * 2.0 * Stopwatch.Frequency / 1000.0), b16 = (long)(16.0 * Stopwatch.Frequency / 1000.0);
            for (int i = 0; i < arr.Length; i++) { if (arr[i] > b2) r.Over2x++; if (arr[i] > b16) r.Over16Ms++; }
        }

        private static void ReportBuild(Rep rep, BenchmarkConfig cfg, BuildResult br, long base1, double boundsMs)
        {
            rep.Line("**Build** (sliced, budget " + F2(cfg.BuildBudgetMs) + " ms per call)");
            rep.Line("- CPU time in build calls: " + F2(br.CpuMs) + " ms, wall time incl. frames: " + F2(br.WallMs) + " ms, calls/frames: " + br.Steps +
                     (boundsMs > 0 ? ", bounds hint (ComputeBounds): " + F2(boundsMs) + " ms" : ""));
            rep.Line("- per call ms (mean / p50 / p95 / p99 / max): " + Row(br.StepMs));
            rep.Line("- calls over 2x budget: " + br.Over2x + ", calls over 16 ms (frame hitch): " + br.Over16Ms);
            rep.Line("- peak managed memory during build, above the source data: " + Mb(br.PeakManaged - base1) + " MB");
        }

        private static void ReportMesh(Rep rep, string name, MeshBvh m)
        {
            double bpt = m.TriangleCount > 0 ? (double)m.MemoryBytes / m.TriangleCount : 0;
            rep.Line("- " + name + ": " + m.TriangleCount.ToString("N0", Inv) + " tris, " + m.NodeCount.ToString("N0", Inv) + " nodes, " +
                     Mb(m.MemoryBytes) + " MB (" + F2(bpt) + " B/tri), quantized: " + m.Quantized + ", build CPU " + F2(Ms(m.BuildTicks)) + " ms in " + m.BuildSteps + " steps");
        }

        private static readonly string[] QueryNames =
            { "Raycast down (double sided)", "Height BoundingBox", "Height ExactMesh", "Height NearestVertex", "ClosestPoint (unbounded)", "OverlapSphere (bounds)" };

        private static int RunOne(int kind, SceneBvh s, int i, Float3[] pts, Float3[] cpts, Float3[] dirs, InstanceHandle[] buf, float radius)
        {
            switch (kind)
            {
                case 0:
                    return s.Raycast(new Ray(pts[i], dirs[i]), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit h) != QueryStatus.Miss ? 1 : 0;
                case 1:
                    return s.HeightOverGround(pts[i], QueryAccuracy.BoundingBox, 0f, out float y1) != QueryStatus.Miss ? 1 : 0;
                case 2:
                    return s.HeightOverGround(pts[i], QueryAccuracy.ExactMesh, 0f, out float y2) != QueryStatus.Miss ? 1 : 0;
                case 3:
                    return s.HeightOverGround(pts[i], QueryAccuracy.NearestVertex, 0f, out float y3) != QueryStatus.Miss ? 1 : 0;
                case 4:
                    return s.ClosestPoint(cpts[i], float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit c) != QueryStatus.Miss ? 1 : 0;
                default:
                    s.OverlapSphere(cpts[i], radius, uint.MaxValue, default(InstanceHandle), buf, out int n);
                    return n > 0 ? 1 : 0;
            }
        }

        private static IEnumerator RunQueries(Rep rep, BenchmarkConfig cfg, SceneBvh scene, Aabb world)
        {
            int batch = Math.Max(1, cfg.Batch);
            int q = Math.Max(batch * 10, cfg.Queries);
            q -= q % batch;
            int batches = q / batch;
            System.Random rnd = new System.Random(12345);
            Float3[] pts = new Float3[q], cpts = new Float3[q], dirs = new Float3[q];
            float sx = world.Max.X - world.Min.X, sy = world.Max.Y - world.Min.Y, sz = world.Max.Z - world.Min.Z;
            for (int i = 0; i < q; i++)
            {
                float x = world.Min.X + (float)rnd.NextDouble() * sx, z = world.Min.Z + (float)rnd.NextDouble() * sz;
                pts[i] = new Float3(x, world.Max.Y + 1f, z);
                dirs[i] = new Float3(((float)rnd.NextDouble() - 0.5f) * 0.6f, -1f, ((float)rnd.NextDouble() - 0.5f) * 0.6f);
                cpts[i] = new Float3(x, world.Min.Y + (float)rnd.NextDouble() * (sy + 2f), z);
            }
            float radius = 0.05f * Math.Max(sx, sz);
            InstanceHandle[] buf = new InstanceHandle[64];
            rep.Line("**Queries** (" + q + " per kind, timed in batches of " + batch + "; times in microseconds per query)");
            rep.Line();
            rep.Line("| query | mean | p50 | p95 | p99 | max | hit % | managed alloc (bytes) | GCs |");
            rep.Line("|---|---|---|---|---|---|---|---|---|");
            for (int kind = 0; kind < 6; kind++)
            {
                for (int i = 0; i < 200; i++) RunOne(kind, scene, i % q, pts, cpts, dirs, buf, radius);   // warm-up
                long[] samples = new long[batches];
                int hits = 0, idx = 0;
                long mem0 = GC.GetTotalMemory(false);
                int gc0 = GC.CollectionCount(0);
                for (int b = 0; b < batches; b++)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    for (int k = 0; k < batch; k++) { hits += RunOne(kind, scene, idx, pts, cpts, dirs, buf, radius); idx++; }
                    samples[b] = Stopwatch.GetTimestamp() - t0;
                }
                long alloc = GC.GetTotalMemory(false) - mem0;
                int gcs = GC.CollectionCount(0) - gc0;
                Dist d = Summarize(samples, batches, 1e6 / Stopwatch.Frequency / batch);
                rep.Line("| " + QueryNames[kind] + " | " + Row(d) + " | " + F2(100.0 * hits / q) + " | " + alloc + " | " + gcs + " |");
                yield return null;
            }
            rep.Line();
        }

        #endregion

        #region scenarios

        private struct InstSpec { public int Mesh; public Affine3x4 M; }

        private static IEnumerator RunStatic(Rep rep, BenchmarkConfig cfg, string title, MeshSpec[] specs, InstSpec[] insts)
        {
            rep.Line("## " + title);
            long base0 = Managed(true), mono0 = Profiler.GetMonoHeapSizeLong();
            List<ArraySource> srcs = new List<ArraySource>();
            int totalTris = 0;
            for (int i = 0; i < specs.Length; i++)
            {
                Generate(specs[i], out Float3[] pos, out int[] idx);
                srcs.Add(new ArraySource(pos, idx));
                totalTris += idx.Length / 3;
                yield return null;
            }
            long base1 = Managed(true);
            BuildOptions opt = Opts(cfg);
            SceneBvh scene = new SceneBvh();
            MeshBvh[] meshes = new MeshBvh[specs.Length];
            long tb = Stopwatch.GetTimestamp(), boundsTicks = 0;
            for (int i = 0; i < specs.Length; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                Aabb hint = MeshBvh.ComputeBounds(srcs[i]);
                boundsTicks += Stopwatch.GetTimestamp() - t0;
                meshes[i] = scene.AddMesh(i + 1, srcs[i], hint, opt, BuildMode.Sliced);
            }
            Aabb world = Aabb.Empty;
            for (int i = 0; i < insts.Length; i++)
            {
                InstanceHandle h = scene.Register(meshes[insts[i].Mesh], insts[i].M, 1u, InstanceFlags.Snappable, i);
                if (scene.TryGetWorldBounds(h, out Aabb wb)) world.Encapsulate(wb);
            }
            rep.Line("- " + totalTris.ToString("N0", Inv) + " triangles in " + specs.Length + " mesh(es), " + insts.Length + " instance(s), source data " + Mb(base1 - base0) + " MB");
            BuildResult br = new BuildResult();
            IEnumerator sub = BuildLoop(() => scene.StepBuilds(cfg.BuildBudgetMs), cfg.BuildBudgetMs, br);
            while (sub.MoveNext()) yield return null;
            ReportBuild(rep, cfg, br, base1, Ms(boundsTicks));
            long bvhBytes = 0;
            for (int i = 0; i < meshes.Length; i++) { ReportMesh(rep, "mesh " + (i + 1), meshes[i]); bvhBytes += meshes[i].MemoryBytes; }
            srcs.Clear();
            long retained = Managed(true) - base0;
            rep.Line("- BVH total " + Mb(bvhBytes) + " MB, managed memory retained after source release + GC: " + Mb(retained) + " MB, Mono heap size grew by " + Mb(Profiler.GetMonoHeapSizeLong() - mono0) + " MB (never shrinks on WebGL)");
            rep.Line("- instances: " + scene.InstanceCount + " (about 190 bytes each, mesh BVHs are shared)");
            rep.Line();
            IEnumerator qi = RunQueries(rep, cfg, scene, world);
            while (qi.MoveNext()) yield return null;
        }

        private static IEnumerator RunMoving(Rep rep, BenchmarkConfig cfg)
        {
            rep.Line("## D: static terrain + 5 moving instances");
            MeshSpec gs = new MeshSpec { Tris = (int)(1000000 * cfg.Scale), Size = 1000f, Seed = 0.3f };
            MeshSpec ms = new MeshSpec { Sphere = true, Tris = (int)(200000 * cfg.Scale), Size = 3f };
            Generate(gs, out Float3[] gp, out int[] gi);
            Generate(ms, out Float3[] mp, out int[] mi);
            ArraySource gsrc = new ArraySource(gp, gi), msrc = new ArraySource(mp, mi);
            BuildOptions opt = Opts(cfg);
            SceneBvh scene = new SceneBvh();
            MeshBvh gm = scene.AddMesh(1, gsrc, MeshBvh.ComputeBounds(gsrc), opt, BuildMode.Sliced);
            MeshBvh mm = scene.AddMesh(2, msrc, MeshBvh.ComputeBounds(msrc), opt, BuildMode.Sliced);
            BuildResult br = new BuildResult();
            IEnumerator sub = BuildLoop(() => scene.StepBuilds(cfg.BuildBudgetMs), cfg.BuildBudgetMs, br);
            while (sub.MoveNext()) yield return null;

            const int movers = 5;
            const float orbit = 300f;
            Affine3x4[] mats = new Affine3x4[movers];
            InstanceHandle[] hs = new InstanceHandle[movers];
            scene.DynamicTransformProvider = (int ud, out Affine3x4 m) => { m = mats[ud]; return true; };
            scene.Register(gm, Affine3x4.Identity);
            for (int i = 0; i < movers; i++)
            {
                float x = Mathf.Cos(i * 1.26f) * orbit, z = Mathf.Sin(i * 1.26f) * orbit;
                mats[i] = MatY(0f, 1f, x, TerrainHeight(x, z, gs.Seed) + 4f, z);
                hs[i] = scene.Register(mm, mats[i], 1u, InstanceFlags.Snappable | InstanceFlags.Dynamic, i);
            }
            const int frames = 300;
            long[] refresh = new long[frames], queries = new long[frames];
            int hits = 0;
            long mem0 = GC.GetTotalMemory(false);
            int gc0 = GC.CollectionCount(0);
            for (int f = 0; f < frames; f++)
            {
                float t = f * 0.05f;
                for (int i = 0; i < movers; i++)
                {
                    float x = Mathf.Cos(t + i * 1.26f) * orbit, z = Mathf.Sin(t + i * 1.26f) * orbit;
                    mats[i] = MatY(t, 1f, x, TerrainHeight(x, z, gs.Seed) + 4f, z);
                }
                long t0 = Stopwatch.GetTimestamp();
                scene.BeginBatch();                       // refreshes the 5 dynamic instances once
                long t1 = Stopwatch.GetTimestamp();
                for (int i = 0; i < movers; i++)
                {
                    Float3 o = new Float3(mats[i].M03 + 0.3f, 1000f, mats[i].M23 + 0.2f);
                    if (scene.HeightOverGround(o, QueryAccuracy.ExactMesh, 0f, out float y) != QueryStatus.Miss) hits++;
                    if (scene.HeightOverGround(o, QueryAccuracy.ExactMesh, 0f, out y, hs[i], uint.MaxValue, float.PositiveInfinity) != QueryStatus.Miss) hits++;
                }
                long t2 = Stopwatch.GetTimestamp();
                scene.EndBatch();
                refresh[f] = t1 - t0;
                queries[f] = t2 - t1;
                yield return null;
            }
            long alloc = GC.GetTotalMemory(false) - mem0;
            int gcs = GC.CollectionCount(0) - gc0;
            rep.Line("- terrain " + gm.TriangleCount.ToString("N0", Inv) + " tris + " + movers + " moving instances of " + mm.TriangleCount.ToString("N0", Inv) + " tris, " + frames + " frames, 10 height queries per frame (5 hit the mover, 5 ignore it)");
            rep.Line("- initial build CPU " + F2(br.CpuMs) + " ms in " + br.Steps + " calls (max call " + F2(br.StepMs.Max) + " ms)");
            rep.Line();
            rep.Line("| per frame | mean | p50 | p95 | p99 | max |");
            rep.Line("|---|---|---|---|---|---|");
            rep.Line("| refresh of 5 dynamic instances (us) | " + Row(Summarize(refresh, frames, 1e6 / Stopwatch.Frequency)) + " |");
            rep.Line("| 10 height queries (us) | " + Row(Summarize(queries, frames, 1e6 / Stopwatch.Frequency)) + " |");
            rep.Line();
            rep.Line("- height queries answered: " + hits + " of " + (frames * movers * 2) + ", managed alloc during the frames: " + alloc + " bytes, GCs: " + gcs);
            rep.Line();
        }

        private static IEnumerator RunUnityMesh(Rep rep, BenchmarkConfig cfg)
        {
            rep.Line("## E: Unity Mesh through the adapter (MeshSceneQuery, sliced build)");
            MeshSpec gs = new MeshSpec { Tris = (int)(1000000 * cfg.Scale), Size = 1000f, Seed = 0.7f };
            Generate(gs, out Float3[] pos, out int[] idx);
            Vector3[] verts = new Vector3[pos.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = pos[i].ToVector3();
            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetIndices(idx, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            GameObject go = new GameObject("SceneBvhBenchTerrain");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer rend = go.AddComponent<MeshRenderer>();
            int tris = idx.Length / 3;
            pos = null; idx = null; verts = null;
            long base0 = Managed(true), nat0 = Profiler.GetTotalAllocatedMemoryLong();

            MeshSceneQuery mq = new MeshSceneQuery();
            mq.MeshBuildMode = BuildMode.Sliced;
            mq.BuildBudgetMs = cfg.BuildBudgetMs;
            mq.MeshOptions = Opts(cfg);
            mq.Add(rend);
            BuildResult br = new BuildResult();
            IEnumerator sub = BuildLoop(() => { mq.Tick(); return mq.Scene.PendingBuilds; }, cfg.BuildBudgetMs, br);
            while (sub.MoveNext()) yield return null;
            rep.Line("- " + tris.ToString("N0", Inv) + " triangles, Unity Mesh with " + mesh.vertexCount.ToString("N0", Inv) + " vertices");
            ReportBuild(rep, cfg, br, base0, 0);
            if (mq.Scene.TryGetMesh(mesh.GetInstanceID(), out MeshBvh bvh)) ReportMesh(rep, "mesh", bvh);
            rep.Line("- managed memory retained after build: " + Mb(Managed(true) - base0) + " MB, native allocated delta: " + Mb(Profiler.GetTotalAllocatedMemoryLong() - nat0) + " MB");
            rep.Line();
            IEnumerator qi = RunQueries(rep, cfg, mq.Scene, mesh.bounds.ToAabb());
            while (qi.MoveNext()) yield return null;

            if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion

        //!
        //! the whole benchmark as an iterator: drive it from a coroutine (runtime, one step per frame) or
        //! iterate it synchronously (Editor menu).
        //!
        public static IEnumerator Run(BenchmarkConfig cfg, Action<string> log, Action<string> status = null)
        {
            Rep rep = new Rep { Log = log };
            rep.Line("# SceneBvh benchmark");
            rep.Line("- platform: " + Application.platform + ", CPU: " + SystemInfo.processorType + ", system RAM: " + SystemInfo.systemMemorySize + " MB");
#if UNITY_EDITOR
            rep.Line("- NOTE: run inside the Editor (Mono, safety checks). NOT representative for IL2CPP/WebGL, measure in a Development build.");
#endif
            rep.Line("- timer: Stopwatch.Frequency " + Stopwatch.Frequency + ", high resolution: " + Stopwatch.IsHighResolution);
            rep.Line("- config: scale " + F2(cfg.Scale) + ", build budget " + F2(cfg.BuildBudgetMs) + " ms, leaf size " + cfg.LeafSize + ", quantize16 " + cfg.Quantize16 + ", " + cfg.Queries + " queries per kind");
            rep.Line();
            float sc = cfg.Scale;
            IEnumerator e;

            if (cfg.RunA)
            {
                if (status != null) status("A: 1 mesh, 5M triangles");
                e = RunStatic(rep, cfg, "A: 1 mesh with 5M triangles",
                    new[] { new MeshSpec { Tris = (int)(5000000 * sc), Size = 2000f, Seed = 0.1f } },
                    new[] { new InstSpec { Mesh = 0, M = Affine3x4.Identity } });
                while (e.MoveNext()) yield return null;
            }
            if (cfg.RunB)
            {
                if (status != null) status("B: 3 meshes, 2M triangles each");
                e = RunStatic(rep, cfg, "B: 3 meshes with 2M triangles",
                    new[] {
                        new MeshSpec { Tris = (int)(2000000 * sc), Size = 1200f, Seed = 0.2f },
                        new MeshSpec { Sphere = true, Tris = (int)(2000000 * sc), Size = 300f },
                        new MeshSpec { Tris = (int)(2000000 * sc), Size = 1200f, Seed = 0.9f } },
                    new[] {
                        new InstSpec { Mesh = 0, M = MatY(0f, 1f, -1300f, 0f, 0f) },
                        new InstSpec { Mesh = 1, M = MatY(0f, 1f, 0f, 200f, 0f) },
                        new InstSpec { Mesh = 2, M = MatY(0f, 1f, 1300f, 0f, 0f) } });
                while (e.MoveNext()) yield return null;
            }
            if (cfg.RunC)
            {
                if (status != null) status("C: 20 instances of one mesh");
                InstSpec[] insts = new InstSpec[20];
                System.Random r = new System.Random(7);
                for (int i = 0; i < insts.Length; i++)
                    insts[i] = new InstSpec { Mesh = 0, M = MatY((float)r.NextDouble() * 6.28f, 0.8f + (float)r.NextDouble() * 0.4f, (i % 5) * 700f, 0f, (i / 5) * 700f) };
                e = RunStatic(rep, cfg, "C: 20 instances of one 1M triangle mesh",
                    new[] { new MeshSpec { Tris = (int)(1000000 * sc), Size = 600f, Seed = 0.4f } }, insts);
                while (e.MoveNext()) yield return null;
            }
            if (cfg.RunD)
            {
                if (status != null) status("D: 5 moving instances");
                e = RunMoving(rep, cfg);
                while (e.MoveNext()) yield return null;
            }
            if (cfg.RunE)
            {
                if (status != null) status("E: Unity mesh adapter");
                e = RunUnityMesh(rep, cfg);
                while (e.MoveNext()) yield return null;
            }
            LastReport = rep.Sb.ToString();
            if (log != null) log("benchmark finished");
        }
    }

    //! runs the benchmark as a coroutine in a Development build and shows the log with OnGUI
    public sealed class SceneBvhBenchmarkRunner : MonoBehaviour
    {
        public BenchmarkConfig Config = new BenchmarkConfig();
        private readonly List<string> lines = new List<string>();
        private string statusText = "";
        private Vector2 scroll;

        private IEnumerator Start()
        {
            IEnumerator run = SceneBvhBenchmark.Run(Config, l => lines.Add(l), s => statusText = s);
            while (run.MoveNext()) yield return null;
            statusText = "finished";
            Debug.Log(SceneBvhBenchmark.LastReport);
        }

        private void OnGUI()
        {
            GUILayout.Label("SceneBvh benchmark: " + statusText);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height - 40));
            for (int i = 0; i < lines.Count; i++) GUILayout.Label(lines[i]);
            GUILayout.EndScrollView();
        }

        public static void Launch(float scale)
        {
            SceneBvhBenchmarkRunner r = new GameObject("SceneBvhBenchmark").AddComponent<SceneBvhBenchmarkRunner>();
            r.Config.Scale = scale;
        }

#if SCENEBVH_BENCH_AUTORUN
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoRun() { Launch(1f); }
#endif
    }

    #endregion
#endif
}
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
//! @date 16.11.2026
//! @changed initial adapter
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

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
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
}
# SceneBvh – spatial queries without colliders

Raycasts, height-over-ground, closest-point snapping and overlap tests against runtime-created meshes.
Replaces `StaticMeshQuadtree`. Built for WebGL first: low RAM, no threads, no GC allocations in queries.

Namespace: `tracer.spatial`

| File | Contains | Unity dependency |
|---|---|---|
| `SceneBvh.cs` | math structs, per-mesh BVH, time-sliced builder, instance list, all queries | none |
| `SceneBvhUnity.cs` | Unity adapter, tag handling, `SceneQueryService` facade, drop-in `StaticMeshBvh`, benchmark | yes |
| `SceneBvhTests.cs` | EditMode/PlayMode tests, benchmark menu items (Editor folder only) | editor |

---

## 1. Concept in 60 seconds

* **Two levels.** Every mesh asset gets one BVH (local space, built once, shared by all instances).
  The scene is a flat list of instances (matrix + world AABB + mesh reference); queries cull instances
  linearly, then traverse the mesh BVH of each candidate.
* **Compact.** About 25 B per triangle (measured: 25.5 B/tri at 5M triangles with 16-bit quantization).
* **No hitches.** The BVH is built in slices (`StepBuilds(ms)` / `Tick()`), or lazily on first use.
  While a mesh is still building, queries fall back to the instance bounds and report `HitApproximate`.
* **Moving objects** are identified by Unity **tags**. Their matrix is refreshed lazily when a query runs.
* **Splat-ready.** `SceneQueryService` holds the active backend (`Mesh` or `Splat`). A backend that cannot
  answer a query returns `QueryStatus.Unsupported`.

Requirements: meshes must have **Read/Write enabled** (`isReadable`). `MeshFilter` + `MeshRenderer` only
(no `SkinnedMeshRenderer`). Only `MeshTopology.Triangles` submeshes are used.

---

## 2. Quick start

### 2.1 Drop-in replacement for `StaticMeshQuadtree`

```csharp
using tracer.spatial;

StaticMeshBvh bvh = new StaticMeshBvh();
bvh.BuildTree();                                   // registers all MeshRenderers (like before)
float y = bvh.GetHeightOverGround(RayMeshUtility.Accuracy.ExactMesh, origin, 0f);   // same signature as before
```

Default build mode is `Lazy`: the first query that needs a mesh builds it (blocking). For hitch-free
loading use `Sliced` (section 4).

### 2.2 Facade with all queries

```csharp
var service = new SceneQueryService();
service.Mesh.SetDynamicTags(new[] { "Movable" });  // objects below a transform with this tag move
service.Mesh.BuildTree();

// ray
if (service.Raycast(camera.ScreenPointToRay(Input.mousePosition), 1000f, out RayHit hit) == QueryStatus.Hit)
{
    Vector3 p = hit.Position.ToVector3();
    Vector3 n = hit.Normal.ToVector3();             // geometric FRONT normal, unit length
    service.Mesh.TryGetRenderer(hit.UserData, out MeshRenderer renderer);
}

// height below a point (the dragged object ignores itself)
service.SetIgnoredRoot(draggedTransform);
service.HeightOverGround(pos, QueryAccuracy.ExactMesh, 0f, out float groundY);

// snapping: nearest point on any surface within 0.5 m, with feature type
if (service.ClosestPoint(pos, 0.5f, out ClosestPointHit cp) == QueryStatus.Hit)
{
    // cp.Feature: Vertex / Edge / Face,  cp.Position, cp.Normal, cp.Distance
}

// bounds-level overlap (instance AABBs, not triangle exact)
InstanceHandle[] buf = new InstanceHandle[32];
service.OverlapSphere(center, 2f, buf, out int count);
```

### 2.3 Height display that follows moving objects

```csharp
var probe = new HeightProbe(service) { Accuracy = QueryAccuracy.ExactMesh, DefaultY = 0f };

void Update()
{
    if (probe.Update(dragged.position))              // cheap: re-queries only if needed
        label.text = probe.Height.ToString("F2");
}
```

`Update` re-queries only when the position changed, the backend was switched, or a registered object changed
inside the vertical column below the position (e.g. a mover passes underneath).

### 2.4 Runtime add / remove

```csharp
service.Mesh.Add(networkObject);                    // GameObject: registers all MeshRenderers below it
service.Mesh.Remove(networkObject);                 // destroyed objects are also purged by Tick()
service.Mesh.SetIgnoredRoot(null);                  // clear the ignore set
```

---

## 3. Query reference

All queries are **allocation free** and **not thread safe** (main thread only).

| Query | Facade call | Notes |
|---|---|---|
| Raycast | `Raycast(Ray, maxDist, out RayHit, layerMask, RayMode)` | nearest hit, double sided by default |
| Height | `HeightOverGround(pos, accuracy, defaultY, out y, layerMask, snapRadius)` | downward ray |
| Closest point | `ClosestPoint(pos, maxDist, out ClosestPointHit, layerMask)` | feature type for snapping |
| Overlap | `OverlapSphere(...)`, `OverlapBounds(...)` | instance AABBs only; writes at most `results.Length` handles |

`layerMask` is the Unity bit mask (`-1` = everything). The instance layer is the GameObject layer.

**Status** (`QueryStatus`)

| Value | Meaning |
|---|---|
| `Hit` | exact result |
| `HitApproximate` | based on instance bounds, the mesh BVH is still building. `hit.Approximate == true`, no triangle data |
| `Miss` | nothing found (height queries then return `defaultY`) |
| `Unsupported` | the active backend cannot answer (e.g. splats) |

**`RayHit`**: `Instance`, `UserData` (renderer instance id), `T` (distance for normalized rays), `Position`,
`Normal`, `BackFace`, `Triangle` (BVH slot), `SourceTriangle` (original index, `-1` unless
`KeepSourceTriangleIds`), `V0/V1/V2` (vertex indices of the **source mesh**), `U/V` (barycentrics:
`p = (1-u-v)*V0 + u*V1 + v*V2`). Use `V0..V2` + barycentrics to interpolate mesh normals or UVs.

**Accuracy** (`QueryAccuracy`)

| Value | Behaviour |
|---|---|
| `BoundingBox` | instance AABBs only |
| `ExactMesh` | triangles, double sided |
| `NearestVertex` | exact hit, then snapped to the nearest vertex of the hit triangle if within `vertexSnapRadius` (default: unlimited) and not above the origin |

> **BoundingBox inside a box.** If the origin lies *inside* an instance AABB, the result is `origin.y`
> (identical to the old quadtree). Caveat: a large AABB that encloses the origin (terrain, building shell)
> wins over lower geometry. `MeshSceneQuery.Scene.BoundsHeightInsideUsesExit = true` makes the bottom face
> of the box count instead. `ExactMesh` is not affected.

**Sidedness.** "Front" is the side `Cross(v1 - v0, v2 - v0)` points to (Unity convention). For mirrored
instances (negative scale) front stays the visible side.

**Closest point and `maxDist`.** Always pass a sensible `maxDist` (snap radius). Unbounded queries over huge,
far apart meshes are the slowest query by far (measured: up to 54 us in WebGL vs 2 us for a raycast).

---

## 4. Build modes and per-frame work

```csharp
service.Mesh.MeshBuildMode = BuildMode.Sliced;      // set BEFORE BuildTree()/Add()
service.Mesh.BuildBudgetMs = 2.0;                   // per Tick()
service.Mesh.BuildTree();
// every frame:
service.Tick();                                     // or add SceneQueryRunner with Service assigned
```

| Mode | Behaviour | When |
|---|---|---|
| `Lazy` (default) | first query that needs the mesh builds it, blocking | simple, no `Tick()` needed |
| `Sliced` | `Tick()` builds within `BuildBudgetMs` per frame, queries are approximate until ready | no frame hitches |
| `Immediate` | blocking build inside `Add` | tools, tests |

Notes:
* The `Mesh` must stay alive and **unmodified** until its BVH is built (the builder reads its CPU data
  directly). `Tick()` cancels builds of destroyed meshes.
* Build throughput is CPU bound (measured: about 170k triangles/s in the Editor, 64k triangles/s in a WebGL
  Development build). With a 2 ms budget at 60 fps the wall time is therefore long for multi-million
  triangle meshes. Raise `BuildBudgetMs` while the user is idle.

---

## 5. Tuning

| Setting | Where | Default | Effect |
|---|---|---|---|
| `MeshOptions.Quantize16` | `MeshSceneQuery` | on | 16-bit positions relative to the mesh AABB. Max error per axis = extent / 131070 (100 m mesh: 0.76 mm). Turn off per mesh for sub-millimeter work |
| `MeshOptions.LeafSize` | `MeshSceneQuery` | 8 | 2..16 triangles per leaf. Smaller = more nodes (RAM), slightly faster queries |
| `MeshOptions.KeepSourceTriangleIds` | `MeshSceneQuery` | off | +4 B/triangle, fills `SourceTriangle` |
| `MeshOptions.ReleaseSource` | `MeshSceneQuery` | on | frees the mesh data reference after the build |
| `MeshBuildMode`, `BuildBudgetMs` | `MeshSceneQuery` | Lazy, 2 ms | see section 4 |
| `IncludeInactive` | `MeshSceneQuery` | on | `BuildTree()` also registers inactive objects (the quadtree did) |
| `AutoReleaseMeshes` | `Scene` | on | frees a mesh BVH when its last instance is removed |
| `BoundsHeightInsideUsesExit` | `Scene` | off | see section 3 |

Tags: `SetDynamicTags(...)` marks an instance dynamic if its transform **or any parent** has one of the tags.
The tags must exist in the Tag Manager. Changing the list updates registered instances. Dynamic instances are
refreshed at the start of every query (or once via `BeginBatch()` / `EndBatch()` around a group of queries).

---

## 6. Tests

### Running

1. `Window > General > Test Runner`.
2. Select the tab the tests appear in (in this project they run from the **PlayMode** tab, there is no asmdef).
   Run **All** or select a single test.
3. If the tests do not show up, create an asmdef for `SceneBvh.cs` + `SceneBvhUnity.cs` and an Editor test
   asmdef (references the first one, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll`).
   Move the `StaticMeshBvh` region to your main assembly then (it references `RayMeshUtility`).

The tests are **deterministic** (fixed random seeds): a failure is reproducible on every run.

### What is tested

The reference is independent of the core: double precision, own Moeller-Trumbore, own point-triangle distance,
world-space triangles built from the same matrices.

| Test | Checks |
|---|---|
| `Raycast_RandomSoup_IdentityInstance` | random soup, random rays, rays aimed at edges and vertices |
| `Raycast_RotatedNonUniformScaledMirroredInstances` | rotated, non-uniformly scaled and mirrored instances, with an ignored instance |
| `Raycast_LargeMesh_UsesUint32Indices` | >65535 vertices (uint index path) |
| `Raycast_GridMesh_NoCracksAtSharedEdgesAndVertices` | no rays slip through shared edges/vertices |
| `Raycast_OriginInsideClosedMesh_AndSidedness` | origin inside closed meshes, front/back only, mirrored |
| `Raycast_MaxDistAndLayerMask` | max distance, layer mask, zero direction, NaN |
| `Raycast_Quantized_StaysWithinPrecision` | 16-bit quantization within the documented precision |
| `Build_DegenerateAndInvalidTriangles` | zero-area, NaN, out-of-range and negative indices |
| `EmptyMeshes_AreHarmless`, `ZeroScaleInstance_IsIgnoredNotCrashing` | empty meshes, singular matrices |
| `TimeSlicedBuild_EqualsImmediateBuild_AndReportsProgress` | sliced == immediate build, progress monotonic |
| `MeshStillBuilding_ReportsApproximate_ThenExact`, `LazyMesh_IsBuiltByTheFirstQueryThatNeedsIt` | approximate results while building, lazy build |
| `ClosestPoint_MatchesBruteForce_IncludingNonUniformScale` | distance, position, feature type, `maxDist`, extreme stretch |
| `ClosestPoint_IgnoreAndSnappableFlag`, `Overlap_MatchesBruteForceOnInstanceBounds` | ignore, snappable flag, overlap vs brute force |
| `HeightOverGround_Accuracies_Ignore_AndInsideBounds`, `HeightOverGround_NearestVertex_Radius` | all accuracies, ignore, inside-bounds rule, snap radius |
| `RegisterUnregister_HandlesGenerationsAndMeshLifetime`, `AutoReleaseMeshes_CanBeDisabled` | handles, slot reuse, mesh lifetime |
| `DynamicInstances_FollowTheirTransformDuringUse`, `IgnoreSet_And_SetFlags`, `ChangeLog_OverflowReportsEverything` | moving instances, batches, change tracking, ignore set |
| `Queries_DoNotAllocate` | no GC collection over 100000 queries (rarely flaky: re-run once) |

### Reading a failure

* The message starts with the context: `ray#17` (random ray 17), `edge/vertex#42`, `inside#5`, `point#9`.
  `o=(...)` and `d=(...)` are the exact ray origin and direction (round-trip format), so you can paste them into
  a debug call.
* **Float vs double near edges.** Every ray is checked against a slightly shrunk (*inner*) and a slightly
  grown (*outer*) reference triangle:
  * `inner reference hit, BVH missed` – the BVH lost a clear hit (real bug).
  * `BVH hit, reference found nothing` – the BVH hit something the grown reference does not (real bug).
  * `BVH hit nearer than reference` / `BVH hit farther than reference` – wrong triangle or wrong `t`.
* Tolerances are relative (`1e-3`...`5e-3` of `1 + t`); quantized tests use wider ones and skip grazing rays.

---

## 7. Benchmark

### Running

* **Editor:** `Tools > TRACER > SceneBvh Benchmark (full)` or `(10 percent)`. The report goes to the console,
  the clipboard and `Library/SceneBvhBenchmark.md`. The Editor (Mono, safety checks) is **not representative**
  for IL2CPP/WebGL.
* **WebGL / device:** make a **Development build** with the scripting define `SCENEBVH_BENCH_AUTORUN`.
  After a scene loads, a panel "BVH Test Working..." (animated) shows scenario and progress. Every report line is
  also written to the browser console. When finished the report is shown with a "Copy report" button.
  On phones start with a smaller scale: `SceneBvhBenchmarkRunner.Launch(0.2f)` instead of the define.

### Scenarios

A: 1 mesh with 5M triangles. B: 3 meshes with 2M triangles. C: 20 instances of one 1M mesh.
D: static terrain + 5 moving instances (refresh + 10 height queries per frame). E: a real Unity `Mesh` through
the adapter. `BenchmarkConfig` sets scale, build budget, queries per kind, leaf size, quantization, scenarios.

### Reading the report

* **Build:** `CPU time` is the work, `wall time` includes the frames in between (depends on budget and frame
  rate). `calls over 16 ms` are frame hitches.
* **Queries:** microseconds per query, computed from batches. WebGL timers tick in about 100 us steps, so
  trust the **mean**, not p50/p95 (values like 0.00 / 5.00 are timer granularity).
* **Memory:** `B/tri` is the final BVH. `peak managed memory` is read from the GC heap (noisy in WebGL: negative
  differences are GC noise). The reliable WebGL figure is the growth of the Mono heap, which never shrinks.
* `managed alloc` / `GCs` in the query tables must be `0` / `0`.

### Reference numbers (measured 2026-11-18)

Editor = Windows, i9-9900K, Mono. WebGL = Development build. Scale 1.0, leaf size 8, quantized.

| | Editor | WebGL |
|---|---|---|
| BVH memory, 5M triangles | 121.9 MB (25.5 B/tri) | 121.9 MB (25.5 B/tri) |
| Build CPU time, 5M triangles | 29.0 s | 78.2 s |
| Raycast, 5M triangles (mean) | 3.3 us | 2.1 us |
| Height ExactMesh, 5M triangles (mean) | 3.0 us | 2.1 us |
| Height BoundingBox (mean) | 0.2 us | 0.2 us |
| ClosestPoint, unbounded, 5M triangles (mean) | 16.6 us | 8.3 us |
| Refresh of 5 dynamic instances, per frame (mean) | 1.4 us | 5.7 us |
| 10 height queries per frame, 5 movers (mean) | 34 us | 27 us |

The WebGL wall times of that run (A: 657 s) were most likely inflated by the log-heavy `OnGUI` of the old
runner (the CPU time per call was normal). Re-measure with the current runner.

---

## 8. Limits and known behaviour

* Not thread safe. Main thread only (no `System.Threading`, nothing runs in the background).
* Static instances never change; dynamic ones (tags) follow their transform. A mesh modified at runtime must be
  removed and re-added (no rebuild API yet).
* `SkinnedMeshRenderer` and non-triangle topologies are not supported.
* Overlap queries test instance AABBs, not triangles.
* Triangles with invalid indices or non-finite positions are dropped during the build. Zero-area triangles stay
  (they can still be snap targets).
* While a mesh builds, queries use its bounds: results are `HitApproximate` and carry no triangle data.
* Use local mesh coordinates close to the origin when quantization is on.
* Source of truth for precision and memory numbers is the benchmark, not this document.

## 9. Migration from `StaticMeshQuadtree`

1. Replace `new StaticMeshQuadtree()` with `new StaticMeshBvh()`; `BuildTree()` and `GetHeightOverGround(...)`
   keep their signatures.
2. `StaticMeshQuadtree.MeshData` is gone. Use `service.Mesh.TryGetRenderer(hit.UserData, ...)` instead.
3. Moving objects: assign tags (`SetDynamicTags`); the quadtree required them to be static.
4. Meshes must stay readable, as before.
5. Optional: switch to `SceneQueryService` to get all queries and the mesh/splat switch.

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


//! @file "SceneBvh.cs"
//! @brief Two-level BVH (per-mesh BVH + flat instance list) for raycasts and geometry snapping
//! @author Thomas Krüger
//! @version 1
//! @date 12.11.2026
//! @changed initial core: math, per-mesh BVH with time-sliced build, instance list, queries
//!
//! @note This file MUST NOT reference UnityEngine (so it can move into an asmdef).
//! @note Not thread safe: all queries share preallocated scratch buffers. Call from one thread only.
//! @note No unsafe code, no reflection, no LINQ, no recursion, no allocations in queries.
//!
//! MEMORY LAYOUT (per mesh, default settings: leaf size 8, 16-bit quantization)
//!   nodes      32 B each (6 float AABB + 2 uint). ~0.33 to 0.42 nodes per triangle => ~11 to 13.5 B/tri
//!   indices    12 B/tri (uint) or 6 B/tri (ushort, chosen automatically when vertices <= 65535)
//!   positions  6 B/vertex quantized (~3 B/tri on closed meshes) or 12 B/vertex float
//!   triIds     4 B/tri, only if BuildOptions.KeepSourceTriangleIds is set (default off)
//!   => about 26 to 29 B/tri in total (1M tris ~ 27 MB, 5M tris ~ 135 MB). Measure, do not trust.
//!
//! BUILD PEAK
//!   The builder permutes the triangle index triples IN PLACE (no permutation array, no per-triangle
//!   temporaries, centroids/bounds computed on the fly from the source positions). The working set is
//!   therefore the final index array (12 B/tri) + node array (with ~up to 25% slack while growing)
//!   + quantized positions (allocated at the very end). Peak ~= final size + node slack.
//!   The caller's source data (positions + indices) is NOT included and exists in addition.
//!   Binning scratch is constant (16 bins). The only large allocations are the 'uint[3*n]' index
//!   array (one block) and the node array.
//!
//! QUANTIZATION (BuildOptions.Quantize16)
//!   Positions are stored as 16 bit per axis relative to the mesh AABB: step = extent / 65535,
//!   max error per axis = step / 2 (100 m mesh: step 1.5 mm, max error 0.76 mm). All node AABBs are
//!   padded by one step so culling stays conservative. Use local coordinates close to the origin.
//!   Not meant for sub-millimeter measuring on huge meshes: switch it off per mesh in that case.
//!
//! CONVENTIONS
//!   - Front face: the side the geometric normal Cross(v1 - v0, v2 - v0) points to (same as Unity).
//!   - Ray direction is never normalized: 't' is in units of |direction| and stays valid in every space.
//!     With a normalized world ray, 't' is the world distance.
//!   - Barycentrics: position = (1-u-v)*V0 + u*V1 + v*V2. V0..V2 are vertex indices of the SOURCE mesh.
//!   - Mirrored instances (negative determinant): 'front' stays the VISIBLE side, like the renderer, which flips
//!     the winding for negative scale. Normals are the inverse transpose of the local normals (stay outward).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace tracer.spatial
{
    #region MATH

    //!
    //! minimal 3D vector (no UnityEngine dependency)
    //!
    public struct Float3
    {
        public float X, Y, Z;

        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator -(Float3 a) => new Float3(-a.X, -a.Y, -a.Z);
        public static Float3 operator *(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);
        public static Float3 operator *(float s, Float3 a) => new Float3(a.X * s, a.Y * s, a.Z * s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(in Float3 a, in Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float3 Cross(in Float3 a, in Float3 b) =>
            new Float3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Float3 Min(in Float3 a, in Float3 b) =>
            new Float3(a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y, a.Z < b.Z ? a.Z : b.Z);

        public static Float3 Max(in Float3 a, in Float3 b) =>
            new Float3(a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y, a.Z > b.Z ? a.Z : b.Z);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        //! returns zero vector for (near) zero length input
        public Float3 Normalized()
        {
            float m = Magnitude;
            return m > 1e-30f ? new Float3(X / m, Y / m, Z / m) : default(Float3);
        }

        public bool IsFinite =>
            !float.IsNaN(X) && !float.IsInfinity(X) && !float.IsNaN(Y) && !float.IsInfinity(Y) &&
            !float.IsNaN(Z) && !float.IsInfinity(Z);

        //! read-only component access (0 = X, 1 = Y, 2 = Z)
        public float this[int axis] => axis == 0 ? X : (axis == 1 ? Y : Z);
    }

    //!
    //! axis aligned bounding box. Default(Aabb) is NOT empty, use Aabb.Empty as accumulator start.
    //!
    public struct Aabb
    {
        public Float3 Min, Max;

        public Aabb(Float3 min, Float3 max) { Min = min; Max = max; }

        public static Aabb Empty =>
            new Aabb(new Float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                     new Float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));

        public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;
        public Float3 Center => (Min + Max) * 0.5f;

        public void Encapsulate(in Float3 p)
        {
            if (p.X < Min.X) Min.X = p.X; if (p.X > Max.X) Max.X = p.X;
            if (p.Y < Min.Y) Min.Y = p.Y; if (p.Y > Max.Y) Max.Y = p.Y;
            if (p.Z < Min.Z) Min.Z = p.Z; if (p.Z > Max.Z) Max.Z = p.Z;
        }

        public void Encapsulate(in Aabb b)
        {
            if (b.Min.X < Min.X) Min.X = b.Min.X; if (b.Max.X > Max.X) Max.X = b.Max.X;
            if (b.Min.Y < Min.Y) Min.Y = b.Min.Y; if (b.Max.Y > Max.Y) Max.Y = b.Max.Y;
            if (b.Min.Z < Min.Z) Min.Z = b.Min.Z; if (b.Max.Z > Max.Z) Max.Z = b.Max.Z;
        }

        //! only valid for non-empty boxes
        public float SurfaceArea
        {
            get
            {
                float dx = Max.X - Min.X, dy = Max.Y - Min.Y, dz = Max.Z - Min.Z;
                return 2f * (dx * dy + dy * dz + dz * dx);
            }
        }

        public bool Overlaps(in Aabb b) =>
            Min.X <= b.Max.X && Max.X >= b.Min.X && Min.Y <= b.Max.Y && Max.Y >= b.Min.Y &&
            Min.Z <= b.Max.Z && Max.Z >= b.Min.Z;

        public bool Contains(in Float3 p) =>
            p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

        public Float3 ClosestPoint(in Float3 p) =>
            new Float3(p.X < Min.X ? Min.X : (p.X > Max.X ? Max.X : p.X),
                       p.Y < Min.Y ? Min.Y : (p.Y > Max.Y ? Max.Y : p.Y),
                       p.Z < Min.Z ? Min.Z : (p.Z > Max.Z ? Max.Z : p.Z));

        public float SqrDistance(in Float3 p)
        {
            float dx = Math.Max(Math.Max(Min.X - p.X, 0f), p.X - Max.X);
            float dy = Math.Max(Math.Max(Min.Y - p.Y, 0f), p.Y - Max.Y);
            float dz = Math.Max(Math.Max(Min.Z - p.Z, 0f), p.Z - Max.Z);
            return dx * dx + dy * dy + dz * dz;
        }

        //!
        //! slab test. tEnter is clamped to >= 0 (0 if the origin is inside), tExit is the exit distance.
        //! NaN safe (ray origin exactly on a slab plane with zero direction component counts as inside).
        //! @param inv component wise 1/direction
        //!
        public bool IntersectRay(in Float3 o, in Float3 inv, float tMax, out float tEnter, out float tExit)
        {
            float tmin = 0f, tmax = float.PositiveInfinity;
            float t0 = (Min.X - o.X) * inv.X, t1 = (Max.X - o.X) * inv.X;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            t0 = (Min.Y - o.Y) * inv.Y; t1 = (Max.Y - o.Y) * inv.Y;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            t0 = (Min.Z - o.Z) * inv.Z; t1 = (Max.Z - o.Z) * inv.Z;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            tmax *= 1.0000012f; // conservative against rounding (see PBRT, gamma(3))
            tEnter = tmin;
            tExit = tmax;
            return tmin <= tmax && tmin <= tMax;
        }
    }

    //!
    //! ray with NOT necessarily normalized direction
    //!
    public struct Ray
    {
        public Float3 Origin, Direction;
        public Ray(Float3 origin, Float3 direction) { Origin = origin; Direction = direction; }
        public Float3 GetPoint(float t) => Origin + Direction * t;
    }

    //!
    //! affine 3x4 matrix, row major: p' = M * p + t. Rows: (M00 M01 M02 M03), (M10 ..), (M20 ..)
    //!
    public struct Affine3x4
    {
        public float M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23;

        public static Affine3x4 Identity =>
            new Affine3x4 { M00 = 1f, M11 = 1f, M22 = 1f };

        public Float3 TransformPoint(in Float3 p) =>
            new Float3(M00 * p.X + M01 * p.Y + M02 * p.Z + M03,
                       M10 * p.X + M11 * p.Y + M12 * p.Z + M13,
                       M20 * p.X + M21 * p.Y + M22 * p.Z + M23);

        public Float3 TransformVector(in Float3 v) =>
            new Float3(M00 * v.X + M01 * v.Y + M02 * v.Z,
                       M10 * v.X + M11 * v.Y + M12 * v.Z,
                       M20 * v.X + M21 * v.Y + M22 * v.Z);

        //! multiplies with the transposed 3x3 part. Applied to the world-to-local matrix this
        //! transforms a local normal into world space (inverse transpose rule).
        public Float3 TransformVectorTransposed(in Float3 v) =>
            new Float3(M00 * v.X + M10 * v.Y + M20 * v.Z,
                       M01 * v.X + M11 * v.Y + M21 * v.Z,
                       M02 * v.X + M12 * v.Y + M22 * v.Z);

        public float Determinant3() =>
            M00 * (M11 * M22 - M12 * M21) + M01 * (M12 * M20 - M10 * M22) + M02 * (M10 * M21 - M11 * M20);

        public bool TryInverse(out Affine3x4 inv)
        {
            float c00 = M11 * M22 - M12 * M21;
            float c01 = M12 * M20 - M10 * M22;
            float c02 = M10 * M21 - M11 * M20;
            float det = M00 * c00 + M01 * c01 + M02 * c02;
            inv = default(Affine3x4);
            if (!(Math.Abs(det) > 1e-30f) || float.IsInfinity(det)) return false;
            float r = 1f / det;
            inv.M00 = c00 * r;
            inv.M01 = (M02 * M21 - M01 * M22) * r;
            inv.M02 = (M01 * M12 - M02 * M11) * r;
            inv.M10 = c01 * r;
            inv.M11 = (M00 * M22 - M02 * M20) * r;
            inv.M12 = (M02 * M10 - M00 * M12) * r;
            inv.M20 = c02 * r;
            inv.M21 = (M01 * M20 - M00 * M21) * r;
            inv.M22 = (M00 * M11 - M01 * M10) * r;
            inv.M03 = -(inv.M00 * M03 + inv.M01 * M13 + inv.M02 * M23);
            inv.M13 = -(inv.M10 * M03 + inv.M11 * M13 + inv.M12 * M23);
            inv.M23 = -(inv.M20 * M03 + inv.M21 * M13 + inv.M22 * M23);
            return true;
        }

        //! exact world AABB of a transformed AABB (Arvo)
        public Aabb TransformAabb(in Aabb b)
        {
            Float3 mn = new Float3(M03, M13, M23), mx = mn;
            Acc(M00, b.Min.X, b.Max.X, ref mn.X, ref mx.X); Acc(M01, b.Min.Y, b.Max.Y, ref mn.X, ref mx.X); Acc(M02, b.Min.Z, b.Max.Z, ref mn.X, ref mx.X);
            Acc(M10, b.Min.X, b.Max.X, ref mn.Y, ref mx.Y); Acc(M11, b.Min.Y, b.Max.Y, ref mn.Y, ref mx.Y); Acc(M12, b.Min.Z, b.Max.Z, ref mn.Y, ref mx.Y);
            Acc(M20, b.Min.X, b.Max.X, ref mn.Z, ref mx.Z); Acc(M21, b.Min.Y, b.Max.Y, ref mn.Z, ref mx.Z); Acc(M22, b.Min.Z, b.Max.Z, ref mn.Z, ref mx.Z);
            return new Aabb(mn, mx);
        }

        private static void Acc(float m, float lo, float hi, ref float mn, ref float mx)
        {
            float a = m * lo, b = m * hi;
            if (a < b) { mn += a; mx += b; } else { mn += b; mx += a; }
        }

        //!
        //! largest singular value (maximum stretch of any vector) of the 3x3 part, closed form
        //! (largest eigenvalue of A^T A). Used for conservative pruning with non-uniform scale.
        //!
        public float MaxScale()
        {
            double s00 = (double)M00 * M00 + (double)M10 * M10 + (double)M20 * M20;
            double s11 = (double)M01 * M01 + (double)M11 * M11 + (double)M21 * M21;
            double s22 = (double)M02 * M02 + (double)M12 * M12 + (double)M22 * M22;
            double s01 = (double)M00 * M01 + (double)M10 * M11 + (double)M20 * M21;
            double s02 = (double)M00 * M02 + (double)M10 * M12 + (double)M20 * M22;
            double s12 = (double)M01 * M02 + (double)M11 * M12 + (double)M21 * M22;
            double p1 = s01 * s01 + s02 * s02 + s12 * s12;
            double eig;
            if (p1 <= 0.0)
                eig = Math.Max(s00, Math.Max(s11, s22));
            else
            {
                double q = (s00 + s11 + s22) / 3.0;
                double p2 = (s00 - q) * (s00 - q) + (s11 - q) * (s11 - q) + (s22 - q) * (s22 - q) + 2.0 * p1;
                double p = Math.Sqrt(p2 / 6.0);
                double b00 = (s00 - q) / p, b11 = (s11 - q) / p, b22 = (s22 - q) / p;
                double b01 = s01 / p, b02 = s02 / p, b12 = s12 / p;
                double detB = b00 * (b11 * b22 - b12 * b12) - b01 * (b01 * b22 - b12 * b02) + b02 * (b01 * b12 - b11 * b02);
                double r = Math.Max(-1.0, Math.Min(1.0, detB * 0.5));
                eig = q + 2.0 * p * Math.Cos(Math.Acos(r) / 3.0);
            }
            return (float)Math.Sqrt(Math.Max(eig, 0.0));
        }

        public bool ExactlyEquals(in Affine3x4 o) =>
            M00 == o.M00 && M01 == o.M01 && M02 == o.M02 && M03 == o.M03 &&
            M10 == o.M10 && M11 == o.M11 && M12 == o.M12 && M13 == o.M13 &&
            M20 == o.M20 && M21 == o.M21 && M22 == o.M22 && M23 == o.M23;
    }

    //!
    //! triangle math shared by BVH traversal and by the brute force reference in the tests
    //!
    public static class TriMath
    {
        //! barycentric tolerance: closes cracks between neighbouring triangles for rays through shared edges
        public const float BaryEps = 1e-6f;

        //!
        //! Moeller-Trumbore. 'd' needs no normalization. Hit if 0 <= t <= tMax.
        //! @param mode sidedness, front = Cross(b - a, c - a) faces the ray origin
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersect(in Float3 o, in Float3 d, in Float3 a, in Float3 b, in Float3 c,
                                     RayMode mode, float tMax, out float t, out float u, out float v)
        {
            t = 0f; u = 0f; v = 0f;
            Float3 e1 = b - a, e2 = c - a;
            Float3 h = Float3.Cross(d, e2);
            float det = Float3.Dot(e1, h);
            // det = -dot(d, n): det > 0 <=> ray runs against the geometric normal <=> front face
            if (mode == RayMode.DoubleSided) { if (det == 0f) return false; }
            else if (mode == RayMode.FrontOnly) { if (!(det > 0f)) return false; }
            else if (!(det < 0f)) return false;
            float f = 1f / det;
            Float3 s = o - a;
            u = f * Float3.Dot(s, h);
            if (u < -BaryEps || u > 1f + BaryEps) return false;
            Float3 q = Float3.Cross(s, e1);
            v = f * Float3.Dot(d, q);
            if (v < -BaryEps || u + v > 1f + BaryEps) return false;
            t = f * Float3.Dot(e2, q);
            if (!(t >= 0f) || t > tMax || float.IsPositiveInfinity(t)) return false;
            if (u < 0f) u = 0f; else if (u > 1f) u = 1f;
            if (v < 0f) v = 0f; else if (v > 1f) v = 1f;
            if (u + v > 1f) { float s2 = u + v; u /= s2; v /= s2; }
            return true;
        }

        //!
        //! closest point on a triangle (Ericson) with feature classification.
        //! feature index: vertex 0..2 = a,b,c; edge 0 = ab, 1 = bc, 2 = ca; face = -1
        //!
        public static Float3 ClosestPoint(in Float3 p, in Float3 a, in Float3 b, in Float3 c,
                                          out SnapFeature feature, out int featureIndex)
        {
            Float3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Float3.Dot(ab, ap), d2 = Float3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { feature = SnapFeature.Vertex; featureIndex = 0; return a; }
            Float3 bp = p - b;
            float d3 = Float3.Dot(ab, bp), d4 = Float3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { feature = SnapFeature.Vertex; featureIndex = 1; return b; }
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                feature = SnapFeature.Edge; featureIndex = 0;
                return a + ab * (d1 / (d1 - d3));
            }
            Float3 cp = p - c;
            float d5 = Float3.Dot(ab, cp), d6 = Float3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { feature = SnapFeature.Vertex; featureIndex = 2; return c; }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                feature = SnapFeature.Edge; featureIndex = 2;
                return a + ac * (d2 / (d2 - d6));
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                feature = SnapFeature.Edge; featureIndex = 1;
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            }
            float sum = va + vb + vc;
            if (!(Math.Abs(sum) > 1e-30f)) { feature = SnapFeature.Vertex; featureIndex = 0; return a; } // degenerate
            float inv = 1f / sum;
            feature = SnapFeature.Face; featureIndex = -1;
            return a + ab * (vb * inv) + ac * (vc * inv);
        }
    }

    #endregion

    #region PUBLIC TYPES

    public enum RayMode : byte { DoubleSided, FrontOnly, BackOnly }
    public enum QueryAccuracy : byte { BoundingBox, NearestVertex, ExactMesh }
    public enum SnapFeature : byte { None, Vertex, Edge, Face }
    public enum MeshState : byte { Queued, Building, Ready, Removed }
    public enum BuildMode : byte { Immediate, Sliced, Lazy }

    //!
    //! Miss = nothing found, Hit = exact result, HitApproximate = result is based on instance bounds because
    //! the mesh BVH is still building, Unsupported = backend can not answer this query (e.g. splats)
    //!
    public enum QueryStatus : byte { Miss, Hit, HitApproximate, Unsupported }

    [Flags] public enum InstanceFlags : byte { None = 0, Dynamic = 1, Snappable = 2 }

    [Flags]
    public enum QueryCapabilities
    {
        None = 0, Raycast = 1, Height = 2, ClosestPoint = 4, Snap = 8, Overlap = 16,
        All = Raycast | Height | ClosestPoint | Snap | Overlap
    }

    //!
    //! stable handle: slot index + generation. default(InstanceHandle) is invalid.
    //!
    public readonly struct InstanceHandle
    {
        public readonly int Index;
        public readonly int Generation;
        public InstanceHandle(int index, int generation) { Index = index; Generation = generation; }
        public bool IsValid => Generation != 0;
    }

    public struct RayHit
    {
        public InstanceHandle Instance;
        public int UserData;        //!< value given at Register (e.g. a Unity instance id)
        public float T;             //!< ray parameter (world distance for normalized rays)
        public Float3 Position;     //!< world
        public Float3 Normal;       //!< world, unit length, geometric FRONT normal (zero if Approximate)
        public bool BackFace;       //!< true if the ray hit the back side (Normal then faces away from the ray)
        public int Triangle;        //!< triangle slot in BVH order (-1 if Approximate)
        public int SourceTriangle;  //!< original triangle index, -1 unless KeepSourceTriangleIds
        public int V0, V1, V2;      //!< vertex indices of the SOURCE mesh (use them to interpolate normals/UVs)
        public float U, V;          //!< barycentrics of V1 and V2
        public bool Approximate;    //!< true: bounds hit of a mesh that is still building
    }

    public struct ClosestPointHit
    {
        public InstanceHandle Instance;
        public int UserData;
        public float Distance;      //!< world distance to Position
        public Float3 Position;     //!< world
        public Float3 Normal;       //!< world geometric front normal of the triangle (zero if Approximate)
        public int Triangle, SourceTriangle, V0, V1, V2;
        public SnapFeature Feature; //!< Vertex, Edge or Face
        public int FeatureIndex;    //!< vertex: 0..2 = V0..V2, edge: 0 = V0V1, 1 = V1V2, 2 = V2V0, face: -1
        public bool Approximate;
    }

    //!
    //! backend interface of the facade. A splat backend returns QueryStatus.Unsupported where it can not answer.
    //!
    public interface ISceneQuery
    {
        QueryCapabilities Capabilities { get; }

        QueryStatus Raycast(in Ray ray, float maxDist, uint layerMask, InstanceHandle ignore, RayMode mode, out RayHit hit);

        QueryStatus HeightOverGround(in Float3 origin, QueryAccuracy accuracy, float defaultY, out float groundY,
                                     InstanceHandle ignore, uint layerMask, float vertexSnapRadius);

        QueryStatus ClosestPoint(in Float3 point, float maxDist, uint layerMask, InstanceHandle ignore, out ClosestPointHit hit);

        QueryStatus OverlapSphere(in Float3 center, float radius, uint layerMask, InstanceHandle ignore,
                                  InstanceHandle[] results, out int count);

        QueryStatus OverlapAabb(in Aabb box, uint layerMask, InstanceHandle ignore,
                                InstanceHandle[] results, out int count);
    }

    //!
    //! per-mesh build settings
    //!
    public struct BuildOptions
    {
        public int LeafSize;                   //!< max triangles per leaf, clamped to 2..16 (default 8)
        public bool Quantize16;                //!< 16 bit positions relative to mesh AABB (default on)
        public bool KeepSourceTriangleIds;     //!< store original triangle index per triangle, 4 B/tri (default off)
        public bool ReleaseSource;             //!< call IMeshSource.Release() when the build is finished (default on)

        public static BuildOptions Default =>
            new BuildOptions { LeafSize = 8, Quantize16 = true, KeepSourceTriangleIds = false, ReleaseSource = true };
    }

    //!
    //! raw triangle data provider. Implemented as a STRUCT and used as generic argument, so that hot build
    //! loops are not virtual (IL2CPP creates one specialization per source type).
    //!
    public interface IMeshSource
    {
        int VertexCount { get; }
        int TriangleCount { get; }
        Float3 GetPosition(int vertex);
        void GetTriangle(int triangle, out int a, out int b, out int c);
        //! called once when the builder no longer needs the data (or was cancelled)
        void Release();
    }

    //!
    //! source over managed arrays (network data, tests)
    //!
    public struct ArraySource : IMeshSource
    {
        public Float3[] Positions;
        public int[] Indices32;
        public ushort[] Indices16;

        public ArraySource(Float3[] positions, int[] indices) { Positions = positions; Indices32 = indices; Indices16 = null; }
        public ArraySource(Float3[] positions, ushort[] indices) { Positions = positions; Indices32 = null; Indices16 = indices; }

        public int VertexCount => Positions == null ? 0 : Positions.Length;
        public int TriangleCount => (Indices32 != null ? Indices32.Length : (Indices16 != null ? Indices16.Length : 0)) / 3;
        public Float3 GetPosition(int vertex) => Positions[vertex];

        public void GetTriangle(int t, out int a, out int b, out int c)
        {
            int k = 3 * t;
            if (Indices32 != null) { a = Indices32[k]; b = Indices32[k + 1]; c = Indices32[k + 2]; }
            else { a = Indices16[k]; b = Indices16[k + 1]; c = Indices16[k + 2]; }
        }

        public void Release() { Positions = null; Indices32 = null; Indices16 = null; }
    }

    #endregion

    #region PER-MESH BVH

    //! 32 byte BVH node. Inner: A = index of left child (right = A + 1), B = split axis.
    //! Leaf: A = first triangle slot, B = LeafBit | triangle count.
    internal struct BvhNode
    {
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public uint A, B;
        public const uint LeafBit = 0x80000000u;

        public Aabb Bounds => new Aabb(new Float3(MinX, MinY, MinZ), new Float3(MaxX, MaxY, MaxZ));

        public void SetBounds(in Aabb b)
        {
            MinX = b.Min.X; MinY = b.Min.Y; MinZ = b.Min.Z; MaxX = b.Max.X; MaxY = b.Max.Y; MaxZ = b.Max.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Hit(float ox, float oy, float oz, float ix, float iy, float iz, float tMax, out float tEnter)
        {
            float tmin = 0f, tmax = float.PositiveInfinity;
            float t0 = (MinX - ox) * ix, t1 = (MaxX - ox) * ix;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            t0 = (MinY - oy) * iy; t1 = (MaxY - oy) * iy;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            t0 = (MinZ - oz) * iz; t1 = (MaxZ - oz) * iz;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            if (t0 > tmin) tmin = t0; if (t1 < tmax) tmax = t1;
            tmax *= 1.0000012f;
            tEnter = tmin;
            return tmin <= tmax && tmin <= tMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SqrDistance(in Float3 p)
        {
            float dx = Math.Max(Math.Max(MinX - p.X, 0f), p.X - MaxX);
            float dy = Math.Max(Math.Max(MinY - p.Y, 0f), p.Y - MaxY);
            float dz = Math.Max(Math.Max(MinZ - p.Z, 0f), p.Z - MaxZ);
            return dx * dx + dy * dy + dz * dz;
        }
    }

    internal interface IBuildJob
    {
        bool Step(int units);
        float Progress { get; }
        void Cancel();
    }

    //!
    //! BVH of one mesh in local space. Shared by all instances. Create with BeginBuild/Build or via SceneBvh.AddMesh.
    //!
    public sealed class MeshBvh
    {
        //! traversal stack size, must exceed the maximum tree depth (64) + 2
        public const int StackSize = 160;

        public long Key { get; internal set; }
        public MeshState State { get; internal set; }
        //! local bounds. Before the build finishes this is the caller's hint, afterwards the exact bounds.
        public Aabb Bounds { get; internal set; }
        public int TriangleCount { get; internal set; }
        public int VertexCount { get; internal set; }
        public int NodeCount { get; internal set; }
        public bool Lazy { get; internal set; }
        //! increments when the build finishes (bounds may change)
        public int Stamp { get; internal set; }
        public int BuildSteps { get; internal set; }
        public long BuildUnits { get; internal set; }
        public long BuildTicks { get; internal set; }    //!< Stopwatch ticks spent inside Step()

        internal int RefCount;
        internal SceneBvh Owner;
        internal IBuildJob job;
        internal BvhNode[] nodes;
        internal ushort[] idx16;
        internal uint[] idx32;
        internal ushort[] posQ;
        internal Float3[] posF;
        internal uint[] ids;
        internal Float3 qMin, qStep;

        public bool Quantized => posQ != null;
        public float Progress => job != null ? job.Progress : (State == MeshState.Ready ? 1f : 0f);

        //! heap bytes of the finished structure (without the caller's source data)
        public long MemoryBytes
        {
            get
            {
                long b = 0;
                if (nodes != null) b += (long)nodes.Length * 32;
                if (idx16 != null) b += (long)idx16.Length * 2;
                if (idx32 != null) b += (long)idx32.Length * 4;
                if (posQ != null) b += (long)posQ.Length * 2;
                if (posF != null) b += (long)posF.Length * 12;
                if (ids != null) b += (long)ids.Length * 4;
                return b;
            }
        }

        //! creates a mesh in state Queued. Drive it with Step/StepMs or let SceneBvh do it.
        public static MeshBvh BeginBuild<TSource>(TSource source, in Aabb boundsHint, BuildOptions options)
            where TSource : struct, IMeshSource
        {
            MeshBvh m = new MeshBvh();
            m.Bounds = boundsHint;
            m.State = MeshState.Queued;
            m.job = new MeshBvhBuilder<TSource>(m, source, options);
            return m;
        }

        //! blocking build
        public static MeshBvh Build<TSource>(TSource source, BuildOptions options) where TSource : struct, IMeshSource
        {
            MeshBvh m = BeginBuild(source, ComputeBounds(source), options);
            m.FinishBuild();
            return m;
        }

        //! O(vertices) helper to get a bounds hint from raw data
        public static Aabb ComputeBounds<TSource>(TSource source) where TSource : struct, IMeshSource
        {
            Aabb b = Aabb.Empty;
            int n = source.VertexCount;
            for (int i = 0; i < n; i++)
            {
                Float3 p = source.GetPosition(i);
                if (p.IsFinite) b.Encapsulate(p);
            }
            return b;
        }

        //!
        //! advance the build by roughly 'units' work units (about one unit = one triangle visit).
        //! @return true if the mesh is ready
        //!
        public bool Step(int units)
        {
            if (job == null) return true; // ready, or removed/cancelled: nothing left to do
            long t0 = Stopwatch.GetTimestamp();
            State = MeshState.Building;
            bool done = job.Step(units < 1 ? 1 : units);
            BuildTicks += Stopwatch.GetTimestamp() - t0;
            BuildSteps++;
            BuildUnits += units;
            if (done)
            {
                job = null;
                State = MeshState.Ready;
                Stamp++;
                if (Owner != null) Owner.OnMeshReady(this);
            }
            return done;
        }

        //! advance until the time budget is used up (at least one chunk is always processed)
        public bool StepMs(double milliseconds, int chunkUnits = 2048)
        {
            long t0 = Stopwatch.GetTimestamp();
            long limit = (long)(milliseconds * Stopwatch.Frequency / 1000.0);
            while (true)
            {
                if (Step(chunkUnits)) return true;
                if (Stopwatch.GetTimestamp() - t0 >= limit) return false;
            }
        }

        public void FinishBuild() { while (!Step(1 << 28)) { } }

        public void Cancel()
        {
            if (job != null) { job.Cancel(); job = null; }
            State = MeshState.Removed;
        }

        internal void Complete(BvhNode[] n, int nodeCount, ushort[] i16, uint[] i32, ushort[] pq, Float3[] pf,
                               uint[] triIds, in Aabb bounds, in Float3 qmin, in Float3 qstep, int triCount, int vertCount)
        {
            nodes = n; NodeCount = nodeCount; idx16 = i16; idx32 = i32; posQ = pq; posF = pf; ids = triIds;
            Bounds = bounds; qMin = qmin; qStep = qstep; TriangleCount = triCount; VertexCount = vertCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadTriangleIndices(int slot, out int i0, out int i1, out int i2)
        {
            int k = 3 * slot;
            if (idx16 != null) { i0 = idx16[k]; i1 = idx16[k + 1]; i2 = idx16[k + 2]; }
            else { i0 = (int)idx32[k]; i1 = (int)idx32[k + 1]; i2 = (int)idx32[k + 2]; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Float3 ReadVertex(int i)
        {
            if (posQ != null)
            {
                int k = 3 * i;
                return new Float3(qMin.X + posQ[k] * qStep.X, qMin.Y + posQ[k + 1] * qStep.Y, qMin.Z + posQ[k + 2] * qStep.Z);
            }
            return posF[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadTriangle(int slot, out Float3 a, out Float3 b, out Float3 c)
        {
            ReadTriangleIndices(slot, out int i0, out int i1, out int i2);
            a = ReadVertex(i0); b = ReadVertex(i1); c = ReadVertex(i2);
        }

        //!
        //! local space raycast. 'tMax' is shrunk to the nearest hit. Iterative, near child first.
        //!
        internal bool Raycast(in Float3 o, in Float3 d, ref float tMax, RayMode mode, int[] stN, float[] stT,
                              out int hitSlot, out float hitU, out float hitV)
        {
            hitSlot = -1; hitU = 0f; hitV = 0f;
            if (NodeCount == 0) return false;
            float ix = 1f / d.X, iy = 1f / d.Y, iz = 1f / d.Z;
            float nodeT;
            if (!nodes[0].Hit(o.X, o.Y, o.Z, ix, iy, iz, tMax, out nodeT)) return false;
            int sp = 0, node = 0;
            while (true)
            {
                if (nodeT <= tMax)
                {
                    ref BvhNode n = ref nodes[node];
                    if ((n.B & BvhNode.LeafBit) != 0u)
                    {
                        int s = (int)n.A, e = s + (int)(n.B & ~BvhNode.LeafBit);
                        for (int i = s; i < e; i++)
                        {
                            ReadTriangle(i, out Float3 a, out Float3 b, out Float3 c);
                            if (TriMath.Intersect(o, d, a, b, c, mode, tMax, out float t, out float u, out float v))
                            { tMax = t; hitSlot = i; hitU = u; hitV = v; }
                        }
                    }
                    else
                    {
                        int l = (int)n.A;
                        bool hl = nodes[l].Hit(o.X, o.Y, o.Z, ix, iy, iz, tMax, out float tl);
                        bool hr = nodes[l + 1].Hit(o.X, o.Y, o.Z, ix, iy, iz, tMax, out float tr);
                        if (hl && hr)
                        {
                            if (tl <= tr) { stN[sp] = l + 1; stT[sp] = tr; sp++; node = l; nodeT = tl; }
                            else { stN[sp] = l; stT[sp] = tl; sp++; node = l + 1; nodeT = tr; }
                            continue;
                        }
                        if (hl) { node = l; nodeT = tl; continue; }
                        if (hr) { node = l + 1; nodeT = tr; continue; }
                    }
                }
                if (sp == 0) break;
                sp--; node = stN[sp]; nodeT = stT[sp];
            }
            return hitSlot >= 0;
        }

        //!
        //! closest point in WORLD metric. Triangles are transformed to world space (3 point transforms), so
        //! non-uniform scale is exact. Nodes are pruned in local space with the conservative factor
        //! 's2' = (max singular value of world-to-local)^2: a point at local distance dl is at least dl / s
        //! away in world space.
        //!
        internal bool ClosestPoint(in Float3 qLocal, in Float3 qWorld, in Affine3x4 l2w, float s2, ref float bestSq,
                                   int[] stN, float[] stD, out int slot, out Float3 pWorld,
                                   out SnapFeature feature, out int featureIndex)
        {
            slot = -1; pWorld = default(Float3); feature = SnapFeature.None; featureIndex = -1;
            if (NodeCount == 0) return false;
            float nodeD = nodes[0].SqrDistance(qLocal);
            if (nodeD > bestSq * s2) return false;
            int sp = 0, node = 0;
            while (true)
            {
                if (nodeD <= bestSq * s2)
                {
                    ref BvhNode n = ref nodes[node];
                    if ((n.B & BvhNode.LeafBit) != 0u)
                    {
                        int s = (int)n.A, e = s + (int)(n.B & ~BvhNode.LeafBit);
                        for (int i = s; i < e; i++)
                        {
                            ReadTriangle(i, out Float3 a, out Float3 b, out Float3 c);
                            Float3 aw = l2w.TransformPoint(a), bw = l2w.TransformPoint(b), cw = l2w.TransformPoint(c);
                            Float3 pw = TriMath.ClosestPoint(qWorld, aw, bw, cw, out SnapFeature f, out int fi);
                            float d2 = (pw - qWorld).SqrMagnitude;
                            if (d2 <= bestSq)
                            { bestSq = d2; slot = i; pWorld = pw; feature = f; featureIndex = fi; }
                        }
                    }
                    else
                    {
                        int l = (int)n.A;
                        float dl = nodes[l].SqrDistance(qLocal), dr = nodes[l + 1].SqrDistance(qLocal);
                        float thr = bestSq * s2;
                        bool hl = dl <= thr, hr = dr <= thr;
                        if (hl && hr)
                        {
                            if (dl <= dr) { stN[sp] = l + 1; stD[sp] = dr; sp++; node = l; nodeD = dl; }
                            else { stN[sp] = l; stD[sp] = dl; sp++; node = l + 1; nodeD = dr; }
                            continue;
                        }
                        if (hl) { node = l; nodeD = dl; continue; }
                        if (hr) { node = l + 1; nodeD = dr; continue; }
                    }
                }
                if (sp == 0) break;
                sp--; node = stN[sp]; nodeD = stD[sp];
            }
            return slot >= 0;
        }
    }

    //!
    //! time-sliced binned SAH builder (16 bins, longest centroid axis, iterative).
    //!
    //! Phases (each resumable at triangle granularity, so a Step never overshoots its budget by more than
    //! a few triangles): Prepare (copy + validate indices, bounds) -> nodes (bin pass, in-place partition,
    //! per node) -> Positions (quantize or copy) -> finish (index width, trims).
    //! Triangles with out-of-range indices or non-finite positions are dropped. Degenerate (zero area)
    //! triangles are kept (they can still be snap targets).
    //! Child bounds come from the bin accumulators of the parent pass, so every node costs exactly one
    //! bin pass and one partition pass over its triangles.
    //!
    internal sealed class MeshBvhBuilder<TSource> : IBuildJob where TSource : struct, IMeshSource
    {
        private struct Work { public int Start, End, Node, Depth; public Aabb Cb; }

        private const int Bins = 16;
        private const int MaxDepth = 64;
        private const int PhPrepare = 0, PhNode = 1, PhBin = 2, PhPart = 3, PhPos = 4, PhDone = 5;

        private TSource src;
        private readonly MeshBvh mesh;
        private readonly int leafSize;
        private readonly bool quantize, keepIds, releaseSource;
        private readonly int srcTris, verts;

        private int phase, cursor, valid, leafTris;
        private uint[] tris, ids;
        private BvhNode[] nodes;
        private int nodeCount;
        private Work[] stack = new Work[MaxDepth * 2 + 8];
        private int sp;
        private Aabb meshBounds = Aabb.Empty, cbAll = Aabb.Empty;
        private Float3 pad, qMin, qStep;

        // per node state
        private Work cur;
        private int axis;
        private float cbMin, kBin;
        private readonly int[] binCount = new int[Bins];
        private readonly Aabb[] binBox = new Aabb[Bins], binCb = new Aabb[Bins];
        private readonly float[] rightArea = new float[Bins];
        private readonly int[] rightCnt = new int[Bins];
        private int splitBin, leftExpected;
        private Aabb lBox, lCb, rBox, rCb;
        private int pi, pj;
        private bool iKnown, iLeft;

        private ushort[] posQ;
        private Float3[] posF;

        public MeshBvhBuilder(MeshBvh target, TSource source, BuildOptions o)
        {
            mesh = target;
            src = source;
            leafSize = Math.Max(2, Math.Min(16, o.LeafSize <= 0 ? 8 : o.LeafSize));
            quantize = o.Quantize16;
            keepIds = o.KeepSourceTriangleIds;
            releaseSource = o.ReleaseSource;
            srcTris = source.TriangleCount;
            verts = source.VertexCount;
            phase = PhPrepare;
        }

        public float Progress
        {
            get
            {
                switch (phase)
                {
                    case PhPrepare: return srcTris > 0 ? 0.05f * cursor / srcTris : 0f;
                    case PhNode:
                    case PhBin:
                    case PhPart: return 0.05f + (valid > 0 ? 0.8f * leafTris / valid : 0f);
                    case PhPos: return 0.85f + (verts > 0 ? 0.15f * cursor / verts : 0f);
                    default: return 1f;
                }
            }
        }

        public void Cancel()
        {
            if (phase == PhDone) return;
            src.Release();
            src = default(TSource);
            tris = null; ids = null; nodes = null; posQ = null; posF = null;
            phase = PhDone;
        }

        public bool Step(int units)
        {
            int budget = units;
            while (budget > 0 && phase != PhDone)
            {
                switch (phase)
                {
                    case PhPrepare: budget -= PrepareChunk(budget); break;
                    case PhNode: BeginNode(); budget--; break;
                    case PhBin: budget -= BinChunk(budget); break;
                    case PhPart: budget -= PartitionChunk(budget); break;
                    case PhPos: budget -= PosChunk(budget); break;
                }
            }
            return phase == PhDone;
        }

        // ---- prepare ----

        private int PrepareChunk(int budget)
        {
            if (tris == null)
            {
                tris = new uint[3 * srcTris];
                if (keepIds) ids = new uint[srcTris];
            }
            int used = 0;
            while (cursor < srcTris && used < budget)
            {
                int t = cursor++; used++;
                src.GetTriangle(t, out int a, out int b, out int c);
                if ((uint)a >= (uint)verts || (uint)b >= (uint)verts || (uint)c >= (uint)verts) continue;
                Float3 pa = src.GetPosition(a), pb = src.GetPosition(b), pc = src.GetPosition(c);
                if (!pa.IsFinite || !pb.IsFinite || !pc.IsFinite) continue;
                int k = 3 * valid;
                tris[k] = (uint)a; tris[k + 1] = (uint)b; tris[k + 2] = (uint)c;
                if (keepIds) ids[valid] = (uint)t;
                Float3 mn = Float3.Min(Float3.Min(pa, pb), pc), mx = Float3.Max(Float3.Max(pa, pb), pc);
                meshBounds.Encapsulate(mn); meshBounds.Encapsulate(mx);
                cbAll.Encapsulate((mn + mx) * 0.5f);
                valid++;
            }
            if (cursor >= srcTris) EndPrepare();
            return used + 1;
        }

        private void EndPrepare()
        {
            if (valid == 0)
            {
                mesh.Complete(new BvhNode[0], 0, new ushort[0], null, null, null, null, Aabb.Empty,
                              default(Float3), default(Float3), 0, verts);
                ReleaseAndFinish();
                return;
            }
            if (quantize)
            {
                qMin = meshBounds.Min;
                qStep = (meshBounds.Max - meshBounds.Min) * (1f / 65535f);
                pad = qStep;
            }
            int cap = (int)(3.1f * valid / leafSize) + 16;
            nodes = new BvhNode[cap];
            nodeCount = 1;
            Aabb rb = new Aabb(meshBounds.Min - pad, meshBounds.Max + pad);
            Emit(0, 0, valid, rb, cbAll, 0);
            phase = PhNode;
        }

        // ---- node loop ----

        private void BeginNode()
        {
            if (sp == 0) { StartPositions(); return; }
            cur = stack[--sp];
            Float3 ext = cur.Cb.Max - cur.Cb.Min;
            axis = ext.X >= ext.Y ? (ext.X >= ext.Z ? 0 : 2) : (ext.Y >= ext.Z ? 1 : 2);
            float e = ext[axis];
            if (!(e > 0f)) { SplitMedian(); return; }
            cbMin = cur.Cb.Min[axis];
            kBin = Bins * (1f - 1e-6f) / e;
            for (int i = 0; i < Bins; i++) { binCount[i] = 0; binBox[i] = Aabb.Empty; binCb[i] = Aabb.Empty; }
            cursor = cur.Start;
            phase = PhBin;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BinOf(float c)
        {
            float f = (c - cbMin) * kBin;
            if (f >= Bins - 1) return Bins - 1;
            return f > 0f ? (int)f : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TriBounds(int slot, out Float3 mn, out Float3 mx)
        {
            int k = 3 * slot;
            Float3 a = src.GetPosition((int)tris[k]), b = src.GetPosition((int)tris[k + 1]), c = src.GetPosition((int)tris[k + 2]);
            mn = Float3.Min(Float3.Min(a, b), c) - pad;
            mx = Float3.Max(Float3.Max(a, b), c) + pad;
        }

        private int BinChunk(int budget)
        {
            int used = 0;
            while (cursor < cur.End && used < budget)
            {
                TriBounds(cursor, out Float3 mn, out Float3 mx);
                Float3 c = (mn + mx) * 0.5f;
                int b = BinOf(c[axis]);
                binCount[b]++;
                binBox[b].Encapsulate(mn); binBox[b].Encapsulate(mx);
                binCb[b].Encapsulate(c);
                cursor++; used++;
            }
            if (cursor >= cur.End) EndBin();
            return used + 1;
        }

        private void EndBin()
        {
            Aabb acc = Aabb.Empty;
            int cnt = 0;
            for (int i = Bins - 1; i >= 1; i--)
            {
                acc.Encapsulate(binBox[i]); cnt += binCount[i];
                rightCnt[i - 1] = cnt;
                rightArea[i - 1] = cnt > 0 ? acc.SurfaceArea : 0f;
            }
            acc = Aabb.Empty; cnt = 0;
            float best = float.PositiveInfinity;
            splitBin = -1;
            for (int i = 0; i < Bins - 1; i++)
            {
                acc.Encapsulate(binBox[i]); cnt += binCount[i];
                if (cnt == 0 || rightCnt[i] == 0) continue;
                float cost = acc.SurfaceArea * cnt + rightArea[i] * rightCnt[i];
                if (cost < best) { best = cost; splitBin = i; }
            }
            if (splitBin < 0) { SplitMedian(); return; }
            lBox = Aabb.Empty; lCb = Aabb.Empty; rBox = Aabb.Empty; rCb = Aabb.Empty;
            leftExpected = 0;
            for (int i = 0; i < Bins; i++)
            {
                if (i <= splitBin) { lBox.Encapsulate(binBox[i]); lCb.Encapsulate(binCb[i]); leftExpected += binCount[i]; }
                else { rBox.Encapsulate(binBox[i]); rCb.Encapsulate(binCb[i]); }
            }
            pi = cur.Start; pj = cur.End - 1; iKnown = false;
            phase = PhPart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLeft(int slot)
        {
            TriBounds(slot, out Float3 mn, out Float3 mx);
            return BinOf(((mn + mx) * 0.5f)[axis]) <= splitBin;
        }

        private void Swap(int x, int y)
        {
            int kx = 3 * x, ky = 3 * y;
            uint t0 = tris[kx], t1 = tris[kx + 1], t2 = tris[kx + 2];
            tris[kx] = tris[ky]; tris[kx + 1] = tris[ky + 1]; tris[kx + 2] = tris[ky + 2];
            tris[ky] = t0; tris[ky + 1] = t1; tris[ky + 2] = t2;
            if (ids != null) { uint t = ids[x]; ids[x] = ids[y]; ids[y] = t; }
        }

        private int PartitionChunk(int budget)
        {
            int used = 0;
            while (pi <= pj)
            {
                if (used >= budget) return used;
                if (!iKnown) { iLeft = IsLeft(pi); iKnown = true; used++; }
                if (iLeft) { pi++; iKnown = false; continue; }
                used++;
                if (!IsLeft(pj)) pj--;
                else { Swap(pi, pj); pi++; pj--; iKnown = false; }
            }
            EndPartition();
            return used + 1;
        }

        private void EndPartition()
        {
            int mid = pi;
            phase = PhNode;
            if (mid <= cur.Start || mid >= cur.End) { SplitMedian(); return; }
            if (mid - cur.Start == leftExpected) { Split(cur, mid, lBox, lCb, rBox, rCb, axis); return; }
            // should never happen (classification is deterministic): fall back to conservative parent bounds
            Aabb pb = nodes[cur.Node].Bounds;
            Split(cur, mid, pb, cur.Cb, pb, cur.Cb, axis);
        }

        //! all centroids identical (or degenerate): split by count, children inherit the parent's bounds
        private void SplitMedian()
        {
            Aabb pb = nodes[cur.Node].Bounds;
            Split(cur, cur.Start + (cur.End - cur.Start) / 2, pb, cur.Cb, pb, cur.Cb, 0);
            phase = PhNode;
        }

        private void Split(in Work w, int mid, in Aabb lb, in Aabb lcb, in Aabb rb, in Aabb rcb, int ax)
        {
            if (nodeCount + 2 > nodes.Length) Array.Resize(ref nodes, nodes.Length + nodes.Length / 4 + 16);
            int l = nodeCount;
            nodeCount += 2;
            nodes[w.Node].A = (uint)l;
            nodes[w.Node].B = (uint)ax;
            Emit(l, w.Start, mid, lb, lcb, w.Depth + 1);
            Emit(l + 1, mid, w.End, rb, rcb, w.Depth + 1);
        }

        private void Emit(int node, int s, int e, in Aabb box, in Aabb cb, int depth)
        {
            nodes[node].SetBounds(box);
            int cnt = e - s;
            if (cnt <= leafSize || depth >= MaxDepth)
            {
                nodes[node].A = (uint)s;
                nodes[node].B = BvhNode.LeafBit | (uint)cnt;
                leafTris += cnt;
                return;
            }
            if (sp == stack.Length) Array.Resize(ref stack, stack.Length * 2);
            stack[sp++] = new Work { Start = s, End = e, Node = node, Depth = depth, Cb = cb };
        }

        // ---- positions + finish ----

        private void StartPositions()
        {
            if (quantize) posQ = new ushort[3 * verts]; else posF = new Float3[verts];
            cursor = 0;
            phase = PhPos;
        }

        private static ushort Q(float v, float min, float inv)
        {
            float f = (v - min) * inv + 0.5f;
            if (!(f > 0f)) return 0;
            if (f >= 65535f) return 65535;
            return (ushort)f;
        }

        private int PosChunk(int budget)
        {
            int used = 0;
            if (quantize)
            {
                float ix = qStep.X > 0f ? 1f / qStep.X : 0f, iy = qStep.Y > 0f ? 1f / qStep.Y : 0f, iz = qStep.Z > 0f ? 1f / qStep.Z : 0f;
                while (cursor < verts && used < budget)
                {
                    Float3 p = src.GetPosition(cursor);
                    int k = 3 * cursor;
                    posQ[k] = Q(p.X, qMin.X, ix); posQ[k + 1] = Q(p.Y, qMin.Y, iy); posQ[k + 2] = Q(p.Z, qMin.Z, iz);
                    cursor++; used++;
                }
            }
            else
            {
                while (cursor < verts && used < budget) { posF[cursor] = src.GetPosition(cursor); cursor++; used++; }
            }
            if (cursor >= verts) Finish();
            return used + 1;
        }

        private void Finish()
        {
            ushort[] i16 = null;
            uint[] i32 = null;
            if (verts <= 65535)
            {
                i16 = new ushort[3 * valid];
                for (int i = 0; i < i16.Length; i++) i16[i] = (ushort)tris[i];
            }
            else
            {
                if (valid != srcTris) Array.Resize(ref tris, 3 * valid);
                i32 = tris;
            }
            tris = null;
            if (ids != null && valid != srcTris) Array.Resize(ref ids, valid);
            if (nodeCount < nodes.Length * 0.9f) Array.Resize(ref nodes, nodeCount);
            Aabb outBounds = meshBounds;
            mesh.Complete(nodes, nodeCount, i16, i32, posQ, posF, ids, outBounds, qMin, qStep, valid, verts);
            nodes = null; posQ = null; posF = null; ids = null;
            ReleaseAndFinish();
        }

        private void ReleaseAndFinish()
        {
            if (releaseSource) src.Release();
            src = default(TSource);
            tris = null;
            phase = PhDone;
        }
    }

    #endregion

    #region INSTANCE INDEX (TOP LEVEL)

    //!
    //! top level culling structure. The default is a flat array with linear AABB tests (instance counts are
    //! small). A tree can implement the same interface later. 'slots' / keys arrays are sized by the caller
    //! to at least (highest slot + 1).
    //!
    public interface IInstanceIndex
    {
        void Set(int slot, in Aabb box, uint layerMask);
        void Remove(int slot);
        //! slots whose AABB is hit by the ray, tEnter = entry distance (unsorted)
        int QueryRay(in Float3 origin, in Float3 invDir, float maxT, uint layerMask, int[] slots, float[] tEnter);
        int QueryAabb(in Aabb box, uint layerMask, int[] slots);
        //! slots whose AABB is within sqrt(maxSqrDist) of the point (also used for sphere overlap)
        int QueryNearest(in Float3 point, float maxSqrDist, uint layerMask, int[] slots, float[] sqrDist);
    }

    public sealed class LinearInstanceIndex : IInstanceIndex
    {
        private Aabb[] boxes = new Aabb[16];
        private uint[] layers = new uint[16];
        private bool[] used = new bool[16];
        private int count;

        public void Set(int slot, in Aabb box, uint layerMask)
        {
            if (slot >= boxes.Length)
            {
                int n = Math.Max(slot + 1, boxes.Length * 2);
                Array.Resize(ref boxes, n); Array.Resize(ref layers, n); Array.Resize(ref used, n);
            }
            boxes[slot] = box; layers[slot] = layerMask; used[slot] = box.IsValid;
            if (slot >= count) count = slot + 1;
        }

        public void Remove(int slot) { if (slot < count) used[slot] = false; }

        public int QueryRay(in Float3 o, in Float3 inv, float maxT, uint layerMask, int[] slots, float[] tEnter)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!used[i] || (layers[i] & layerMask) == 0u) continue;
                if (boxes[i].IntersectRay(o, inv, maxT, out float te, out float tx)) { slots[n] = i; tEnter[n] = te; n++; }
            }
            return n;
        }

        public int QueryAabb(in Aabb box, uint layerMask, int[] slots)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!used[i] || (layers[i] & layerMask) == 0u) continue;
                if (boxes[i].Overlaps(box)) slots[n++] = i;
            }
            return n;
        }

        public int QueryNearest(in Float3 p, float maxSqr, uint layerMask, int[] slots, float[] sqrDist)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!used[i] || (layers[i] & layerMask) == 0u) continue;
                float d = boxes[i].SqrDistance(p);
                if (d <= maxSqr) { slots[n] = i; sqrDist[n] = d; n++; }
            }
            return n;
        }
    }

    #endregion

    #region SCENE

    //! callback that delivers the current local-to-world matrix of a dynamic instance (false = not available)
    public delegate bool TransformProvider(int userData, out Affine3x4 localToWorld);

    //!
    //! the scene: mesh registry (dedupe by key), instances, dynamic refresh, queries.
    //! Mesh builds: Immediate (blocking), Sliced (call StepBuilds each frame) or Lazy (first query that
    //! needs the mesh finishes the build, blocking). While a mesh is not ready queries use instance bounds.
    //!
    public sealed class SceneBvh : ISceneQuery
    {
        private struct Instance
        {
            public MeshBvh Mesh;
            public Affine3x4 L2W, W2L;
            public Aabb World;
            public uint Layer;
            public InstanceFlags Flags;
            public int Generation;      // 0 = slot never used
            public bool Alive, Valid;
            public float W2LMaxScale;   // upper bound of the largest singular value of W2L
            public int MeshStamp;
            public int UserData;
        }

        private struct ChangeEntry { public Aabb Box; }
        private const int LogSize = 64;

        private Instance[] inst = new Instance[16];
        private int instCount, aliveCount;
        private int[] freeSlots = new int[16];
        private int freeCount;
        private int[] dynSlots = new int[8];
        private int dynCount;
        private readonly IInstanceIndex index;
        private readonly Dictionary<long, MeshBvh> meshes = new Dictionary<long, MeshBvh>();
        private readonly List<MeshBvh> buildQueue = new List<MeshBvh>();
        private int buildHead;
        private int[] candSlots = new int[16];
        private float[] candKeys = new float[16];
        private readonly int[] stackN = new int[MeshBvh.StackSize];
        private readonly float[] stackF = new float[MeshBvh.StackSize];
        private readonly ChangeEntry[] log = new ChangeEntry[LogSize];
        private bool inBatch;
        private InstanceHandle[] ignoreSet;
        private int ignoreCount;

        //! provides matrices of instances flagged Dynamic
        public TransformProvider DynamicTransformProvider;
        //! remove a mesh BVH (and its memory) when its last instance is unregistered
        public bool AutoReleaseMeshes = true;
        //!
        //! HeightOverGround with Accuracy.BoundingBox when the origin lies INSIDE an instance AABB:
        //! false (default, same as the old behaviour): that box counts as hit at distance 0, the result is
        //! origin.y. CAUTION: a large AABB that encloses the origin (terrain, building shell, scene root)
        //! therefore wins over geometry that is lower, and the object keeps its own height.
        //! true: the exit (bottom) face of the box counts, like a closed mesh would give with ExactMesh.
        //!
        public bool BoundsHeightInsideUsesExit;
        //! increments whenever an instance was added, removed, moved or a mesh finished building
        public int Version { get; private set; }

        public QueryCapabilities Capabilities => QueryCapabilities.All;
        public int InstanceCount => aliveCount;
        public int PendingBuilds { get { int n = 0; for (int i = buildHead; i < buildQueue.Count; i++) if (buildQueue[i].State < MeshState.Ready) n++; return n; } }

        public SceneBvh() : this(new LinearInstanceIndex()) { }
        public SceneBvh(IInstanceIndex instanceIndex) { index = instanceIndex; }

        #region meshes

        //!
        //! register (or fetch) the BVH of a mesh asset.
        //! @param key unique id of the mesh asset (dedupe). If it already exists, the existing BVH is returned
        //!            and 'source' is released.
        //! @param boundsHint local bounds used for instance AABBs until the build is finished
        //!
        public MeshBvh AddMesh<TSource>(long key, TSource source, in Aabb boundsHint, BuildOptions options, BuildMode mode)
            where TSource : struct, IMeshSource
        {
            if (meshes.TryGetValue(key, out MeshBvh existing)) { source.Release(); return existing; }
            MeshBvh m = MeshBvh.BeginBuild(source, boundsHint, options);
            m.Key = key; m.Owner = this; m.Lazy = mode == BuildMode.Lazy;
            meshes[key] = m;
            if (mode == BuildMode.Immediate) m.FinishBuild();
            else if (mode == BuildMode.Sliced) buildQueue.Add(m);
            return m;
        }

        //! adopt a BVH that was built elsewhere (e.g. directly from network arrays)
        public MeshBvh AddPrebuiltMesh(long key, MeshBvh mesh)
        {
            if (meshes.TryGetValue(key, out MeshBvh existing)) return existing;
            mesh.Key = key; mesh.Owner = this;
            meshes[key] = mesh;
            if (mesh.State != MeshState.Ready) buildQueue.Add(mesh);
            return mesh;
        }

        public bool TryGetMesh(long key, out MeshBvh mesh) => meshes.TryGetValue(key, out mesh);

        //! removes a mesh that has no instances. Returns false if it is still in use or unknown.
        public bool RemoveMesh(long key)
        {
            if (!meshes.TryGetValue(key, out MeshBvh m) || m.RefCount > 0) return false;
            meshes.Remove(key);
            m.Cancel();
            m.Owner = null;
            return true;
        }

        //!
        //! spend up to 'budgetMs' on queued (Sliced) builds, oldest first. Call once per frame.
        //! @return number of builds still pending
        //!
        public int StepBuilds(double budgetMs)
        {
            long t0 = Stopwatch.GetTimestamp();
            long limit = (long)(budgetMs * Stopwatch.Frequency / 1000.0);
            while (buildHead < buildQueue.Count)
            {
                MeshBvh m = buildQueue[buildHead];
                if (m.State == MeshState.Ready || m.State == MeshState.Removed) { buildHead++; continue; }
                long left = limit - (Stopwatch.GetTimestamp() - t0);
                if (left <= 0) break;
                m.StepMs(left * 1000.0 / Stopwatch.Frequency);
            }
            int pending = 0;
            for (int i = buildHead; i < buildQueue.Count; i++) if (buildQueue[i].State < MeshState.Ready) pending++;
            if (pending == 0) { buildQueue.Clear(); buildHead = 0; }
            return pending;
        }

        internal void OnMeshReady(MeshBvh m)
        {
            for (int s = 0; s < instCount; s++)
                if (inst[s].Alive && inst[s].Mesh == m) ApplyTransform(s, inst[s].L2W, true);
        }

        #endregion

        #region instances

        public InstanceHandle Register(MeshBvh mesh, in Affine3x4 localToWorld, uint layerMask = 1u,
                                       InstanceFlags flags = InstanceFlags.Snappable, int userData = 0)
        {
            int slot;
            if (freeCount > 0) slot = freeSlots[--freeCount];
            else
            {
                if (instCount == inst.Length)
                {
                    Array.Resize(ref inst, inst.Length * 2);
                    Array.Resize(ref candSlots, inst.Length);
                    Array.Resize(ref candKeys, inst.Length);
                }
                slot = instCount++;
            }
            ref Instance i = ref inst[slot];
            int gen = i.Generation == 0 ? 1 : i.Generation;
            i = default(Instance);
            i.Generation = gen; i.Alive = true; i.Mesh = mesh; i.Layer = layerMask; i.Flags = flags; i.UserData = userData;
            i.MeshStamp = -1;
            mesh.RefCount++;
            aliveCount++;
            if ((flags & InstanceFlags.Dynamic) != 0)
            {
                if (dynCount == dynSlots.Length) Array.Resize(ref dynSlots, dynSlots.Length * 2);
                dynSlots[dynCount++] = slot;
            }
            ApplyTransform(slot, localToWorld, true);
            return new InstanceHandle(slot, gen);
        }

        public bool Unregister(InstanceHandle h)
        {
            if (!TryGetSlot(h, out int slot)) return false;
            ref Instance i = ref inst[slot];
            Aabb old = i.World;
            index.Remove(slot);
            if ((i.Flags & InstanceFlags.Dynamic) != 0)
                for (int k = 0; k < dynCount; k++)
                    if (dynSlots[k] == slot) { dynSlots[k] = dynSlots[--dynCount]; break; }
            MeshBvh m = i.Mesh;
            i.Alive = false;
            i.Mesh = null;
            i.Generation = i.Generation == int.MaxValue ? 1 : i.Generation + 1;
            if (freeCount == freeSlots.Length) Array.Resize(ref freeSlots, freeSlots.Length * 2);
            freeSlots[freeCount++] = slot;
            aliveCount--;
            LogChange(old, old);
            if (--m.RefCount <= 0 && AutoReleaseMeshes) RemoveMesh(m.Key);
            return true;
        }

        public bool IsValid(InstanceHandle h) => TryGetSlot(h, out _);

        public bool TryGetUserData(InstanceHandle h, out int userData)
        {
            userData = 0;
            if (!TryGetSlot(h, out int s)) return false;
            userData = inst[s].UserData;
            return true;
        }

        public bool TryGetWorldBounds(InstanceHandle h, out Aabb bounds)
        {
            bounds = Aabb.Empty;
            if (!TryGetSlot(h, out int s)) return false;
            bounds = inst[s].World;
            return true;
        }

        //! set the matrix explicitly (for instances without provider or to force an update)
        public bool SetTransform(InstanceHandle h, in Affine3x4 localToWorld)
        {
            if (!TryGetSlot(h, out int s)) return false;
            ApplyTransform(s, localToWorld, false);
            return true;
        }

        //! pull the current matrix of one dynamic instance from the provider
        public bool Refresh(InstanceHandle h)
        {
            if (!TryGetSlot(h, out int s) || DynamicTransformProvider == null) return false;
            return RefreshSlot(s);
        }

        //! refresh all dynamic instances once, then run a group of queries without further refresh
        public void BeginBatch() { RefreshDynamics(); inBatch = true; }
        public void EndBatch() { inBatch = false; }

        //!
        //! change tracking for visualizations (e.g. height-over-ground display that must update when a moving
        //! object passes underneath). Remember 'Version', later ask for the union of all changed AABBs since then.
        //! @return false if nothing changed. If more than 64 changes happened, 'box' is the whole world.
        //!
        public bool TryGetChangedBoundsSince(int sinceVersion, out Aabb box)
        {
            box = Aabb.Empty;
            if (sinceVersion == Version) return false;
            if (Version - sinceVersion >= LogSize)
            {
                box = new Aabb(new Float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity),
                               new Float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity));
                return true;
            }
            for (int v = sinceVersion + 1; v <= Version; v++) box.Encapsulate(log[v & (LogSize - 1)].Box);
            return true;
        }

        private bool TryGetSlot(InstanceHandle h, out int slot)
        {
            slot = h.Index;
            return h.Generation != 0 && (uint)slot < (uint)instCount && inst[slot].Alive && inst[slot].Generation == h.Generation;
        }

        private bool IsIgnored(int s, InstanceHandle ign)
        {
            if (ign.Generation != 0 && ign.Index == s && inst[s].Generation == ign.Generation) return true;
            for (int i = 0; i < ignoreCount; i++)
            {
                InstanceHandle h = ignoreSet[i];
                if (h.Index == s && h.Generation != 0 && inst[s].Generation == h.Generation) return true;
            }
            return false;
        }

        //!
        //! persistent set of instances that ALL queries skip (e.g. every renderer of the dragged object),
        //! in addition to the 'ignore' argument of a query. The array is NOT copied, keep it alive and
        //! unchanged while it is set. Pass (null, 0) to clear.
        //!
        public void SetIgnoreSet(InstanceHandle[] handles, int count)
        {
            ignoreSet = handles;
            ignoreCount = handles == null ? 0 : Math.Max(0, Math.Min(count, handles.Length));
        }

        //! change the flags of an instance (e.g. when a tag list changed and an instance became dynamic)
        public bool SetFlags(InstanceHandle h, InstanceFlags flags)
        {
            if (!TryGetSlot(h, out int s)) return false;
            ref Instance i = ref inst[s];
            bool was = (i.Flags & InstanceFlags.Dynamic) != 0, now = (flags & InstanceFlags.Dynamic) != 0;
            i.Flags = flags;
            if (was && !now)
            {
                for (int k = 0; k < dynCount; k++)
                    if (dynSlots[k] == s) { dynSlots[k] = dynSlots[--dynCount]; break; }
            }
            else if (!was && now)
            {
                if (dynCount == dynSlots.Length) Array.Resize(ref dynSlots, dynSlots.Length * 2);
                dynSlots[dynCount++] = s;
            }
            return true;
        }

        private InstanceHandle HandleOf(int s) => new InstanceHandle(s, inst[s].Generation);

        private void LogChange(in Aabb a, in Aabb b)
        {
            Aabb u = a;
            u.Encapsulate(b);
            Version++;
            log[Version & (LogSize - 1)].Box = u;
        }

        private void ApplyTransform(int s, in Affine3x4 matrix, bool force)
        {
            Affine3x4 m = matrix; // 'matrix' may alias the instance's own field
            ref Instance i = ref inst[s];
            MeshBvh mesh = i.Mesh;
            if (!force && i.MeshStamp == mesh.Stamp && i.L2W.ExactlyEquals(m)) return;
            Aabb old = i.World;
            i.L2W = m;
            i.Valid = m.TryInverse(out Affine3x4 w2l);
            i.W2L = w2l;
            i.W2LMaxScale = i.Valid ? w2l.MaxScale() * 1.0001f : 0f;
            i.World = mesh.Bounds.IsValid ? m.TransformAabb(mesh.Bounds) : Aabb.Empty;
            i.MeshStamp = mesh.Stamp;
            if (i.World.IsValid) index.Set(s, i.World, i.Layer); else index.Remove(s);
            LogChange(old, i.World);
        }

        private bool RefreshSlot(int s)
        {
            if (!DynamicTransformProvider(inst[s].UserData, out Affine3x4 m)) return false;
            ApplyTransform(s, m, false);
            return true;
        }

        private void RefreshDynamics()
        {
            if (DynamicTransformProvider == null) return;
            for (int k = 0; k < dynCount; k++) RefreshSlot(dynSlots[k]);
        }

        private void BeginQuery() { if (!inBatch) RefreshDynamics(); }

        private void SortCandidates(int n)
        {
            for (int i = 1; i < n; i++)
            {
                float key = candKeys[i]; int slot = candSlots[i];
                int j = i - 1;
                while (j >= 0 && candKeys[j] > key) { candKeys[j + 1] = candKeys[j]; candSlots[j + 1] = candSlots[j]; j--; }
                candKeys[j + 1] = key; candSlots[j + 1] = slot;
            }
        }

        #endregion

        #region queries

        public QueryStatus Raycast(in Ray ray, float maxDist, uint layerMask, InstanceHandle ignore, RayMode mode, out RayHit hit) =>
            RaycastCore(ray, maxDist, layerMask, ignore, mode, false, false, out hit);

        public QueryStatus Raycast(in Ray ray, float maxDist, out RayHit hit) =>
            RaycastCore(ray, maxDist, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, false, false, out hit);

        //!
        //! @param boundsOnly use instance AABBs only (Accuracy.BoundingBox)
        //! @param insideUsesExit if the origin is inside a bounds, report the exit face (floor below the origin)
        //!
        private QueryStatus RaycastCore(in Ray ray, float maxDist, uint layerMask, InstanceHandle ignore, RayMode mode,
                                        bool boundsOnly, bool insideUsesExit, out RayHit hit)
        {
            hit = default(RayHit);
            BeginQuery();
            Float3 o = ray.Origin, d = ray.Direction;
            if (!o.IsFinite || !d.IsFinite || !(d.SqrMagnitude > 0f)) return QueryStatus.Miss;
            Float3 inv = new Float3(1f / d.X, 1f / d.Y, 1f / d.Z);
            int n = index.QueryRay(o, inv, maxDist, layerMask, candSlots, candKeys);
            SortCandidates(n);

            float best = maxDist;
            bool found = false, approx = false;
            int bestSlot = -1, bestTri = -1;
            float bestU = 0f, bestV = 0f;

            for (int k = 0; k < n; k++)
            {
                if (candKeys[k] > best) break; // sorted by entry distance, which is a lower bound
                int s = candSlots[k];
                if (IsIgnored(s, ignore)) continue;
                ref Instance it = ref inst[s];
                MeshBvh m = it.Mesh;
                bool useBox = boundsOnly, isApprox = false;
                if (!useBox && m.State != MeshState.Ready)
                {
                    if (m.Lazy && m.State == MeshState.Queued) m.FinishBuild();
                    if (m.State != MeshState.Ready) { useBox = true; isApprox = true; }
                }
                if (useBox)
                {
                    it.World.IntersectRay(o, inv, maxDist, out float te, out float tx);
                    float t = (insideUsesExit && te <= 0f && it.World.Contains(o)) ? tx : te;
                    if (t < best || (!found && t <= best))
                    { best = t; found = true; approx = isApprox; bestSlot = s; bestTri = -1; }
                    continue;
                }
                if (m.TriangleCount == 0 || !it.Valid) continue;
                Float3 ol = it.W2L.TransformPoint(o), dl = it.W2L.TransformVector(d);
                float tm = best;
                if (m.Raycast(ol, dl, ref tm, mode, stackN, stackF,
                              out int tri, out float u, out float v) && (tm < best || !found))
                { best = tm; found = true; approx = false; bestSlot = s; bestTri = tri; bestU = u; bestV = v; }
            }

            if (!found) return QueryStatus.Miss;
            ref Instance hi = ref inst[bestSlot];
            hit.Instance = HandleOf(bestSlot);
            hit.UserData = hi.UserData;
            hit.T = best;
            hit.Position = o + d * best;
            hit.Approximate = approx;
            hit.Triangle = -1; hit.SourceTriangle = -1; hit.V0 = hit.V1 = hit.V2 = -1;
            if (bestTri >= 0)
            {
                MeshBvh m = hi.Mesh;
                m.ReadTriangleIndices(bestTri, out hit.V0, out hit.V1, out hit.V2);
                hit.Triangle = bestTri;
                hit.SourceTriangle = m.ids != null ? (int)m.ids[bestTri] : -1;
                hit.U = bestU; hit.V = bestV;
                hit.Normal = WorldNormal(ref hi, bestTri);
                hit.BackFace = Float3.Dot(d, hit.Normal) > 0f;
            }
            return approx ? QueryStatus.HitApproximate : QueryStatus.Hit;
        }

        //! world space geometric front normal of a triangle (inverse transpose rule, stays on the visible side
        //! for mirrored instances)
        private Float3 WorldNormal(ref Instance it, int tri)
        {
            it.Mesh.ReadTriangle(tri, out Float3 a, out Float3 b, out Float3 c);
            Float3 nl = Float3.Cross(b - a, c - a);
            return it.W2L.TransformVectorTransposed(nl).Normalized();
        }

        //!
        //! height of the surface below 'origin' (downward ray). The instance 'ignore' (the dragged object) is skipped.
        //! BoundingBox: instance AABBs only; if the origin is inside an AABB the result is origin.y
        //! (see BoundsHeightInsideUsesExit for the caveat and the alternative).
        //! ExactMesh: triangles, double sided. NearestVertex: exact hit, then snapped to the nearest vertex of the
        //! hit triangle if it lies within 'vertexSnapRadius' (world units) and not above the origin.
        //! @return Miss => groundY = defaultY
        //!
        public QueryStatus HeightOverGround(in Float3 origin, QueryAccuracy accuracy, float defaultY, out float groundY,
                                            InstanceHandle ignore = default(InstanceHandle), uint layerMask = uint.MaxValue,
                                            float vertexSnapRadius = float.PositiveInfinity)
        {
            groundY = defaultY;
            Ray ray = new Ray(origin, new Float3(0f, -1f, 0f));
            QueryStatus st = RaycastCore(ray, float.PositiveInfinity, layerMask, ignore, RayMode.DoubleSided,
                                         accuracy == QueryAccuracy.BoundingBox, BoundsHeightInsideUsesExit, out RayHit h);
            if (st == QueryStatus.Miss) return st;
            float y = h.Position.Y;
            if (accuracy == QueryAccuracy.NearestVertex && st == QueryStatus.Hit &&
                SnapToNearestVertex(h, vertexSnapRadius, out Float3 vp, out _) && vp.Y <= origin.Y)
                y = vp.Y;
            groundY = y;
            return st;
        }

        //! snaps a hit to the closest of the three triangle vertices (world space) within 'radius'
        public bool SnapToNearestVertex(in RayHit hit, float radius, out Float3 vertexWorld, out int vertexIndex)
        {
            vertexWorld = default(Float3); vertexIndex = -1;
            if (hit.Approximate || hit.Triangle < 0 || !TryGetSlot(hit.Instance, out int s)) return false;
            ref Instance it = ref inst[s];
            it.Mesh.ReadTriangle(hit.Triangle, out Float3 a, out Float3 b, out Float3 c);
            Float3 wa = it.L2W.TransformPoint(a), wb = it.L2W.TransformPoint(b), wc = it.L2W.TransformPoint(c);
            float da = (wa - hit.Position).SqrMagnitude, db = (wb - hit.Position).SqrMagnitude, dc = (wc - hit.Position).SqrMagnitude;
            float best;
            if (da <= db && da <= dc) { best = da; vertexWorld = wa; vertexIndex = hit.V0; }
            else if (db <= dc) { best = db; vertexWorld = wb; vertexIndex = hit.V1; }
            else { best = dc; vertexWorld = wc; vertexIndex = hit.V2; }
            if (best > radius * radius) { vertexIndex = -1; return false; }
            return true;
        }

        //! closest point on any snappable instance within 'maxDist' (world units), with feature type
        public QueryStatus ClosestPoint(in Float3 point, float maxDist, uint layerMask, InstanceHandle ignore, out ClosestPointHit hit)
        {
            hit = default(ClosestPointHit);
            BeginQuery();
            if (!point.IsFinite) return QueryStatus.Miss;
            float bestSq = float.IsPositiveInfinity(maxDist) ? float.PositiveInfinity : maxDist * maxDist;
            int n = index.QueryNearest(point, bestSq, layerMask, candSlots, candKeys);
            SortCandidates(n);

            int bestSlot = -1, bestTri = -1, bestFi = -1;
            Float3 bestP = default(Float3);
            SnapFeature bestF = SnapFeature.None;
            bool approx = false;

            for (int k = 0; k < n; k++)
            {
                if (candKeys[k] > bestSq) break;
                int s = candSlots[k];
                if (IsIgnored(s, ignore)) continue;
                ref Instance it = ref inst[s];
                if ((it.Flags & InstanceFlags.Snappable) == 0) continue;
                MeshBvh m = it.Mesh;
                if (m.State != MeshState.Ready)
                {
                    if (m.Lazy && m.State == MeshState.Queued) m.FinishBuild();
                    if (m.State != MeshState.Ready)
                    {
                        float d2 = candKeys[k];
                        if (bestSlot < 0 || d2 < bestSq)
                        { bestSq = d2; bestSlot = s; bestTri = -1; bestP = it.World.ClosestPoint(point); bestF = SnapFeature.None; bestFi = -1; approx = true; }
                        continue;
                    }
                }
                if (m.TriangleCount == 0 || !it.Valid) continue;
                Float3 ql = it.W2L.TransformPoint(point);
                float tmp = bestSq;
                if (m.ClosestPoint(ql, point, it.L2W, it.W2LMaxScale * it.W2LMaxScale, ref tmp, stackN, stackF,
                                   out int tri, out Float3 pw, out SnapFeature f, out int fi) && (tmp < bestSq || bestSlot < 0 || approx))
                { bestSq = tmp; bestSlot = s; bestTri = tri; bestP = pw; bestF = f; bestFi = fi; approx = false; }
            }

            if (bestSlot < 0) return QueryStatus.Miss;
            ref Instance hi = ref inst[bestSlot];
            hit.Instance = HandleOf(bestSlot);
            hit.UserData = hi.UserData;
            hit.Distance = (float)Math.Sqrt(bestSq);
            hit.Position = bestP;
            hit.Feature = bestF; hit.FeatureIndex = bestFi;
            hit.Approximate = approx;
            hit.Triangle = -1; hit.SourceTriangle = -1; hit.V0 = hit.V1 = hit.V2 = -1;
            if (bestTri >= 0)
            {
                MeshBvh m = hi.Mesh;
                m.ReadTriangleIndices(bestTri, out hit.V0, out hit.V1, out hit.V2);
                hit.Triangle = bestTri;
                hit.SourceTriangle = m.ids != null ? (int)m.ids[bestTri] : -1;
                hit.Normal = WorldNormal(ref hi, bestTri);
            }
            return approx ? QueryStatus.HitApproximate : QueryStatus.Hit;
        }

        //! instances whose world AABB intersects the sphere (bounds level, not triangle exact).
        //! Writes at most results.Length handles; 'count' is the number written.
        public QueryStatus OverlapSphere(in Float3 center, float radius, uint layerMask, InstanceHandle ignore,
                                         InstanceHandle[] results, out int count)
        {
            count = 0;
            BeginQuery();
            if (results == null || !center.IsFinite) return QueryStatus.Miss;
            int n = index.QueryNearest(center, radius * radius, layerMask, candSlots, candKeys);
            for (int k = 0; k < n && count < results.Length; k++)
                if (!IsIgnored(candSlots[k], ignore)) results[count++] = HandleOf(candSlots[k]);
            return count > 0 ? QueryStatus.Hit : QueryStatus.Miss;
        }

        //! instances whose world AABB intersects 'box' (bounds level, not triangle exact)
        public QueryStatus OverlapAabb(in Aabb box, uint layerMask, InstanceHandle ignore,
                                       InstanceHandle[] results, out int count)
        {
            count = 0;
            BeginQuery();
            if (results == null || !box.IsValid) return QueryStatus.Miss;
            int n = index.QueryAabb(box, layerMask, candSlots);
            for (int k = 0; k < n && count < results.Length; k++)
                if (!IsIgnored(candSlots[k], ignore)) results[count++] = HandleOf(candSlots[k]);
            return count > 0 ? QueryStatus.Hit : QueryStatus.Miss;
        }

        #endregion
    }

    #endregion
}
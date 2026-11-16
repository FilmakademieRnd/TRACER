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


//! @file "SceneBvhTests.cs"
//! @brief EditMode tests for SceneBvh against an independent brute force reference
//! @author Thomas Krüger
//! @version 1
//! @date 16.11.2026
//! @changed initial tests
//!
//! The reference is written independently of the bvh core: double precision, own Moeller-Trumbore, own
//! point-triangle distance (plane projection + segments instead of Ericson regions), world space
//! triangles built from the SAME float matrices that are given to the scene.
//!
//! Hit/miss comparison near edges (float vs double): every ray is evaluated twice by the reference,
//! with an INNER triangle (barycentrics inset by eps) and an OUTER one (outset by eps).
//!   - if the INNER reference hits, the BVH has to hit and must not be farther than that hit
//!   - if the BVH hits, the OUTER reference has to hit and the BVH must not be nearer than that hit
//! Tests are deterministic (fixed seeds).
//!
//! @note Needs an EditMode test assembly (asmdef with UnityEngine.TestRunner / UnityEditor.TestRunner and
//!       nunit.framework.dll) that references the assembly containing SceneBvh.cs.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace tracer.spatial.tests
{
    #region REFERENCE (double precision, independent of the core)

    internal struct D3
    {
        public double X, Y, Z;
        public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(D3 a, D3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static D3 Cross(D3 a, D3 b) => new D3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Len => Math.Sqrt(X * X + Y * Y + Z * Z);
        public D3 Norm() { double l = Len; return l > 0 ? new D3(X / l, Y / l, Z / l) : this; }
        public static D3 From(Float3 f) => new D3(f.X, f.Y, f.Z);
        public Float3 ToFloat3() => new Float3((float)X, (float)Y, (float)Z);
    }

    internal sealed class RefMesh
    {
        public Float3[] Pos;
        public int[] Idx;
        public int TriCount => Idx.Length / 3;

        public bool ValidTri(int t)
        {
            for (int k = 0; k < 3; k++)
            {
                int i = Idx[3 * t + k];
                if (i < 0 || i >= Pos.Length || !Pos[i].IsFinite) return false;
            }
            return true;
        }

        public Aabb LocalBounds()
        {
            Aabb b = Aabb.Empty;
            for (int t = 0; t < TriCount; t++)
            {
                if (!ValidTri(t)) continue;
                for (int k = 0; k < 3; k++) b.Encapsulate(Pos[Idx[3 * t + k]]);
            }
            return b;
        }
    }

    internal sealed class RefInst
    {
        public RefMesh Mesh;
        public Affine3x4 M;
        public int Id;
        public InstanceHandle Handle;
        public bool Alive = true;
        public D3[] WV;       // world position per source vertex
        public double Det;

        public void Rebuild()
        {
            WV = new D3[Mesh.Pos.Length];
            for (int i = 0; i < WV.Length; i++) WV[i] = Ref.W(M, Mesh.Pos[i]);
            Det = (double)M.M00 * ((double)M.M11 * M.M22 - (double)M.M12 * M.M21)
                + (double)M.M01 * ((double)M.M12 * M.M20 - (double)M.M10 * M.M22)
                + (double)M.M02 * ((double)M.M10 * M.M21 - (double)M.M11 * M.M20);
        }

        //! visible-front normal in world space (outward for outward oriented meshes, also when mirrored)
        public D3 RefNormal(D3 a, D3 b, D3 c)
        {
            D3 n = D3.Cross(b - a, c - a);
            if (Det < 0) n = n * -1.0;
            return n.Norm();
        }

        public Aabb WorldBounds()
        {
            Aabb lb = Mesh.LocalBounds();
            Aabb wb = Aabb.Empty;
            for (int i = 0; i < 8; i++)
            {
                Float3 c = new Float3((i & 1) != 0 ? lb.Max.X : lb.Min.X, (i & 2) != 0 ? lb.Max.Y : lb.Min.Y, (i & 4) != 0 ? lb.Max.Z : lb.Min.Z);
                wb.Encapsulate(Ref.W(M, c).ToFloat3());
            }
            return wb;
        }
    }

    internal struct RefHit
    {
        public double T, U, V;
        public RefInst Inst;
        public int Tri;
        public D3 Normal;
    }

    internal static class Ref
    {
        public static D3 W(Affine3x4 m, Float3 p) =>
            new D3((double)m.M00 * p.X + (double)m.M01 * p.Y + (double)m.M02 * p.Z + m.M03,
                   (double)m.M10 * p.X + (double)m.M11 * p.Y + (double)m.M12 * p.Z + m.M13,
                   (double)m.M20 * p.X + (double)m.M21 * p.Y + (double)m.M22 * p.Z + m.M23);

        //! plain double sided Moeller-Trumbore, evaluated twice (outer / inner barycentric tolerance)
        public static void NearestBoth(List<RefInst> refs, D3 o, D3 d, double eps, int ignoreId,
                                       out RefHit inner, out bool hasInner, out RefHit outer, out bool hasOuter)
        {
            inner = default(RefHit); outer = default(RefHit);
            inner.T = double.PositiveInfinity; outer.T = double.PositiveInfinity;
            hasInner = false; hasOuter = false;
            foreach (RefInst ri in refs)
            {
                if (!ri.Alive || ri.Id == ignoreId) continue;
                RefMesh m = ri.Mesh;
                int n = m.TriCount;
                for (int t = 0; t < n; t++)
                {
                    if (!m.ValidTri(t)) continue;
                    D3 a = ri.WV[m.Idx[3 * t]], b = ri.WV[m.Idx[3 * t + 1]], c = ri.WV[m.Idx[3 * t + 2]];
                    D3 e1 = b - a, e2 = c - a, h = D3.Cross(d, e2);
                    double det = D3.Dot(e1, h);
                    if (det == 0.0) continue;
                    double f = 1.0 / det;
                    D3 sv = o - a;
                    double u = f * D3.Dot(sv, h);
                    if (u < -eps || u > 1.0 + eps) continue;
                    D3 q = D3.Cross(sv, e1);
                    double v = f * D3.Dot(d, q);
                    if (v < -eps || u + v > 1.0 + eps) continue;
                    double tt = f * D3.Dot(e2, q);
                    if (tt < 0.0) continue;
                    if (tt < outer.T)
                    { outer = new RefHit { T = tt, U = u, V = v, Inst = ri, Tri = t, Normal = ri.RefNormal(a, b, c) }; hasOuter = true; }
                    if (u >= eps && v >= eps && u + v <= 1.0 - eps && tt < inner.T)
                    { inner = new RefHit { T = tt, U = u, V = v, Inst = ri, Tri = t, Normal = ri.RefNormal(a, b, c) }; hasInner = true; }
                }
            }
        }

        public static D3 SegClosest(D3 p, D3 a, D3 b)
        {
            D3 ab = b - a;
            double l2 = D3.Dot(ab, ab);
            double t = l2 > 0 ? D3.Dot(p - a, ab) / l2 : 0.0;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return a + ab * t;
        }

        //! point-triangle distance by plane projection + inside test, else the three segments
        public static double DistPointTri(D3 p, D3 a, D3 b, D3 c, out D3 q)
        {
            D3 n = D3.Cross(b - a, c - a);
            double nn = D3.Dot(n, n);
            if (nn > 1e-24)
            {
                D3 pr = p - n * (D3.Dot(p - a, n) / nn);
                if (D3.Dot(D3.Cross(b - a, pr - a), n) >= 0 && D3.Dot(D3.Cross(c - b, pr - b), n) >= 0 &&
                    D3.Dot(D3.Cross(a - c, pr - c), n) >= 0)
                { q = pr; return (p - pr).Len; }
            }
            D3 q1 = SegClosest(p, a, b), q2 = SegClosest(p, b, c), q3 = SegClosest(p, c, a);
            double d1 = (p - q1).Len, d2 = (p - q2).Len, d3 = (p - q3).Len;
            if (d1 <= d2 && d1 <= d3) { q = q1; return d1; }
            if (d2 <= d3) { q = q2; return d2; }
            q = q3; return d3;
        }

        public static double NearestPoint(List<RefInst> refs, D3 p, int ignoreId, out RefInst inst, out int tri)
        {
            double best = double.PositiveInfinity;
            inst = null; tri = -1;
            foreach (RefInst ri in refs)
            {
                if (!ri.Alive || ri.Id == ignoreId) continue;
                RefMesh m = ri.Mesh;
                for (int t = 0; t < m.TriCount; t++)
                {
                    if (!m.ValidTri(t)) continue;
                    double d = DistPointTri(p, ri.WV[m.Idx[3 * t]], ri.WV[m.Idx[3 * t + 1]], ri.WV[m.Idx[3 * t + 2]], out D3 _);
                    if (d < best) { best = d; inst = ri; tri = t; }
                }
            }
            return best;
        }
    }

    #endregion

    //! scene + reference instances, always changed together
    internal sealed class Scn
    {
        public readonly SceneBvh Scene = new SceneBvh();
        public readonly List<RefInst> Refs = new List<RefInst>();
        private int nextId = 1;

        public RefInst Add(RefMesh rm, long key, Affine3x4 m, BuildOptions opt, InstanceFlags flags = InstanceFlags.Snappable)
        {
            if (!Scene.TryGetMesh(key, out MeshBvh mesh))
            {
                ArraySource src = new ArraySource(rm.Pos, rm.Idx);
                mesh = Scene.AddMesh(key, src, MeshBvh.ComputeBounds(src), opt, BuildMode.Immediate);
            }
            RefInst ri = new RefInst { Mesh = rm, M = m, Id = nextId++ };
            ri.Handle = Scene.Register(mesh, m, 1u, flags, ri.Id);
            ri.Rebuild();
            Refs.Add(ri);
            return ri;
        }

        public RefInst ById(int id)
        {
            foreach (RefInst r in Refs) if (r.Id == id) return r;
            return null;
        }
    }

    [TestFixture]
    public class SceneBvhTests
    {
        #region helpers

        private static double R(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();
        private static double LogUniform(Random r, double lo, double hi) => Math.Exp(R(r, Math.Log(lo), Math.Log(hi)));

        private static D3 RandDir(Random r)
        {
            while (true)
            {
                D3 v = new D3(R(r, -1, 1), R(r, -1, 1), R(r, -1, 1));
                double l = v.Len;
                if (l > 0.1 && l <= 1.0) return v * (1.0 / l);
            }
        }

        private static BuildOptions Exact()
        {
            BuildOptions o = BuildOptions.Default;
            o.Quantize16 = false;
            o.KeepSourceTriangleIds = true;
            return o;
        }

        private static Affine3x4 Trs(D3 pos, D3 axis, double angle, double sx, double sy, double sz)
        {
            D3 k = axis.Norm();
            double c = Math.Cos(angle), s = Math.Sin(angle), t = 1 - c;
            double r00 = c + k.X * k.X * t, r01 = k.X * k.Y * t - k.Z * s, r02 = k.X * k.Z * t + k.Y * s;
            double r10 = k.Y * k.X * t + k.Z * s, r11 = c + k.Y * k.Y * t, r12 = k.Y * k.Z * t - k.X * s;
            double r20 = k.Z * k.X * t - k.Y * s, r21 = k.Z * k.Y * t + k.X * s, r22 = c + k.Z * k.Z * t;
            Affine3x4 m = default(Affine3x4);
            m.M00 = (float)(r00 * sx); m.M01 = (float)(r01 * sy); m.M02 = (float)(r02 * sz); m.M03 = (float)pos.X;
            m.M10 = (float)(r10 * sx); m.M11 = (float)(r11 * sy); m.M12 = (float)(r12 * sz); m.M13 = (float)pos.Y;
            m.M20 = (float)(r20 * sx); m.M21 = (float)(r21 * sy); m.M22 = (float)(r22 * sz); m.M23 = (float)pos.Z;
            return m;
        }

        private static Affine3x4 Translate(double x, double y, double z) => Trs(new D3(x, y, z), new D3(0, 1, 0), 0, 1, 1, 1);

        private static Affine3x4 RandomMatrix(Random r, bool nonUniform, bool mirror, double posRange)
        {
            double sx, sy, sz;
            if (nonUniform) { sx = LogUniform(r, 0.3, 3); sy = LogUniform(r, 0.3, 3); sz = LogUniform(r, 0.3, 3); }
            else { sx = sy = sz = LogUniform(r, 0.5, 2); }
            if (mirror) sx = -sx;
            return Trs(new D3(R(r, -posRange, posRange), R(r, -posRange, posRange), R(r, -posRange, posRange)),
                       RandDir(r), R(r, 0, 2 * Math.PI), sx, sy, sz);
        }

        //! unshared triangle soup. wellShaped: roughly equilateral triangles (no slivers), else random vertices
        private static RefMesh MakeSoup(Random r, int n, double range, double minSize, double maxSize, bool wellShaped)
        {
            Float3[] pos = new Float3[3 * n];
            int[] idx = new int[3 * n];
            for (int t = 0; t < n; t++)
            {
                D3 c = new D3(R(r, -range, range), R(r, -range, range), R(r, -range, range));
                double s = R(r, minSize, maxSize);
                if (wellShaped)
                {
                    D3 e1 = RandDir(r);
                    D3 e2 = D3.Cross(e1, RandDir(r)).Norm();
                    if (e2.Len < 0.5) e2 = D3.Cross(e1, new D3(0, 0, 1)).Norm();
                    double phi = R(r, 0, 2 * Math.PI);
                    for (int k = 0; k < 3; k++)
                    {
                        double a = phi + k * 2.0 * Math.PI / 3.0, rad = s * R(r, 0.85, 1.15);
                        pos[3 * t + k] = (c + e1 * (Math.Cos(a) * rad) + e2 * (Math.Sin(a) * rad)).ToFloat3();
                    }
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                        pos[3 * t + k] = (c + new D3(R(r, -s, s), R(r, -s, s), R(r, -s, s))).ToFloat3();
                }
                idx[3 * t] = 3 * t; idx[3 * t + 1] = 3 * t + 1; idx[3 * t + 2] = 3 * t + 2;
            }
            return new RefMesh { Pos = pos, Idx = idx };
        }

        //! flips triangles so that Cross(b - a, c - a) points away from 'center' (convex shapes)
        private static void OrientOutward(RefMesh m, D3 center)
        {
            for (int t = 0; t < m.TriCount; t++)
            {
                D3 a = D3.From(m.Pos[m.Idx[3 * t]]), b = D3.From(m.Pos[m.Idx[3 * t + 1]]), c = D3.From(m.Pos[m.Idx[3 * t + 2]]);
                D3 n = D3.Cross(b - a, c - a);
                D3 mid = (a + b + c) * (1.0 / 3.0) - center;
                if (D3.Dot(n, mid) < 0) { int tmp = m.Idx[3 * t + 1]; m.Idx[3 * t + 1] = m.Idx[3 * t + 2]; m.Idx[3 * t + 2] = tmp; }
            }
        }

        private static RefMesh MakeBox(D3 center, D3 half)
        {
            Float3[] pos = new Float3[8];
            for (int i = 0; i < 8; i++)
                pos[i] = new D3(center.X + ((i & 1) != 0 ? half.X : -half.X), center.Y + ((i & 2) != 0 ? half.Y : -half.Y),
                                center.Z + ((i & 4) != 0 ? half.Z : -half.Z)).ToFloat3();
            int[] idx = {
                0, 1, 3, 0, 3, 2,  4, 5, 7, 4, 7, 6,  0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,  0, 2, 6, 0, 6, 4,  1, 3, 7, 1, 7, 5 };
            RefMesh m = new RefMesh { Pos = pos, Idx = idx };
            OrientOutward(m, center);
            return m;
        }

        private static RefMesh MakeSphere(double radius, int seg, int rings)
        {
            Float3[] pos = new Float3[(rings + 1) * seg];
            for (int i = 0; i <= rings; i++)
            {
                double th = Math.PI * i / rings;
                for (int j = 0; j < seg; j++)
                {
                    double ph = 2 * Math.PI * j / seg;
                    pos[i * seg + j] = new D3(radius * Math.Sin(th) * Math.Cos(ph), radius * Math.Cos(th), radius * Math.Sin(th) * Math.Sin(ph)).ToFloat3();
                }
            }
            List<int> idx = new List<int>();
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int v00 = i * seg + j, v01 = i * seg + (j + 1) % seg, v10 = (i + 1) * seg + j, v11 = (i + 1) * seg + (j + 1) % seg;
                    idx.Add(v00); idx.Add(v10); idx.Add(v11);
                    idx.Add(v00); idx.Add(v11); idx.Add(v01);
                }
            RefMesh m = new RefMesh { Pos = pos, Idx = idx.ToArray() };
            OrientOutward(m, new D3(0, 0, 0));
            return m;
        }

        //! plane y = 0 with shared vertices, n x n quads of size 'cell'
        private static RefMesh MakeGrid(int n, double cell)
        {
            Float3[] pos = new Float3[(n + 1) * (n + 1)];
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                    pos[i * (n + 1) + j] = new Float3((float)((i - n / 2) * cell), 0f, (float)((j - n / 2) * cell));
            int[] idx = new int[n * n * 6];
            int k = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int a = i * (n + 1) + j, b = a + 1, c = a + (n + 1), d = c + 1;
                    idx[k++] = a; idx[k++] = c; idx[k++] = d;
                    idx[k++] = a; idx[k++] = d; idx[k++] = b;
                }
            return new RefMesh { Pos = pos, Idx = idx };
        }

        private static RefMesh MakeQuad(double half)
        {
            Float3[] pos = {
                new Float3((float)-half, 0f, (float)-half), new Float3((float)half, 0f, (float)-half),
                new Float3((float)half, 0f, (float)half), new Float3((float)-half, 0f, (float)half) };
            return new RefMesh { Pos = pos, Idx = new[] { 0, 1, 2, 0, 2, 3 } };
        }

        private static string Fmt(Float3 f) => "(" + f.X.ToString("R") + "," + f.Y.ToString("R") + "," + f.Z.ToString("R") + ")";

        //! checks one ray against the reference (see file header for the inner/outer rule)
        private static void CheckRay(Scn s, Float3 o, Float3 d, double eps, double tolRel, bool quant, int ignoreId, string ctx)
        {
            D3 O = D3.From(o), Dd = D3.From(d);
            Ref.NearestBoth(s.Refs, O, Dd, eps, ignoreId, out RefHit inner, out bool hasInner, out RefHit outer, out bool hasOuter);
            InstanceHandle ign = default(InstanceHandle);
            if (ignoreId != 0) { RefInst ri = s.ById(ignoreId); if (ri != null) ign = ri.Handle; }
            QueryStatus st = s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, ign, RayMode.DoubleSided, out RayHit h);
            string msg = ctx + " o=" + Fmt(o) + " d=" + Fmt(d);
            if (hasInner) Assert.AreEqual(QueryStatus.Hit, st, msg + " (inner reference hit, BVH missed)");
            if (st == QueryStatus.Miss) return;
            Assert.AreEqual(QueryStatus.Hit, st, msg);
            Assert.IsTrue(hasOuter, msg + " (BVH hit, reference found nothing)");
            double tol = tolRel * (1.0 + h.T);
            bool grazing = hasInner && Math.Abs(D3.Dot(Dd.Norm(), inner.Normal)) < 0.25;
            if (!(quant && grazing))
            {
                Assert.GreaterOrEqual((double)h.T, outer.T - tol, msg + " (BVH hit nearer than reference)");
                if (hasInner) Assert.LessOrEqual((double)h.T, inner.T + tol, msg + " (BVH hit farther than reference)");
            }
            if (!quant) VerifyHit(s, h, O, Dd, msg);
        }

        //! checks the secondary hit data: instance, source triangle, vertex indices, barycentrics, normal, back face
        private static void VerifyHit(Scn s, RayHit h, D3 O, D3 Dd, string msg)
        {
            RefInst ri = s.ById(h.UserData);
            Assert.IsNotNull(ri, msg + " unknown user data");
            Assert.GreaterOrEqual(h.SourceTriangle, 0, msg);
            int t = h.SourceTriangle;
            Assert.AreEqual(ri.Mesh.Idx[3 * t], h.V0, msg); Assert.AreEqual(ri.Mesh.Idx[3 * t + 1], h.V1, msg); Assert.AreEqual(ri.Mesh.Idx[3 * t + 2], h.V2, msg);
            D3 w0 = ri.WV[h.V0], w1 = ri.WV[h.V1], w2 = ri.WV[h.V2];
            D3 p = w0 * (1.0 - h.U - h.V) + w1 * h.U + w2 * h.V;
            D3 hp = D3.From(h.Position);
            double posTol = 2e-3 * (1.0 + hp.Len);
            Assert.Less((hp - (O + Dd * h.T)).Len, posTol, msg + " position vs ray point");
            Assert.Less((hp - p).Len, posTol, msg + " position vs barycentrics");
            D3 n = ri.RefNormal(w0, w1, w2);
            D3 hn = D3.From(h.Normal);
            Assert.AreEqual(1.0, hn.Len, 1e-3, msg);
            if (n.Len > 0.5) Assert.Greater(D3.Dot(n, hn), 0.999, msg + " normal");
            double dn = D3.Dot(Dd.Norm(), n);
            if (Math.Abs(dn) > 1e-3) Assert.AreEqual(dn > 0, h.BackFace, msg + " back face");
        }

        private static void RunRayBatch(Scn s, Random r, int count, double range, double tolRel, bool quant, double eps = 2e-4, bool useIgnore = false)
        {
            for (int i = 0; i < count; i++)
            {
                Float3 o = (new D3(R(r, -range, range), R(r, -range, range), R(r, -range, range))).ToFloat3();
                Float3 d = (RandDir(r) * R(r, 0.5, 3.0)).ToFloat3();
                int ign = useIgnore ? s.Refs[r.Next(s.Refs.Count)].Id : 0;
                CheckRay(s, o, d, eps, tolRel, quant, ign, "ray#" + i);
            }
        }

        //! rays aimed exactly at vertices / edge points of random triangles
        private static void RunEdgeVertexBatch(Scn s, Random r, int count, double tolRel)
        {
            for (int i = 0; i < count; i++)
            {
                RefInst ri = s.Refs[r.Next(s.Refs.Count)];
                int t = r.Next(ri.Mesh.TriCount);
                if (!ri.Mesh.ValidTri(t)) continue;
                D3[] w = { ri.WV[ri.Mesh.Idx[3 * t]], ri.WV[ri.Mesh.Idx[3 * t + 1]], ri.WV[ri.Mesh.Idx[3 * t + 2]] };
                int kind = r.Next(3);
                D3 target;
                if (kind == 0) target = w[r.Next(3)];
                else { int e = r.Next(3); double f = kind == 1 ? 0.5 : R(r, 0, 1); target = w[e] + (w[(e + 1) % 3] - w[e]) * f; }
                D3 origin = target + RandDir(r) * R(r, 3, 10);
                D3 dir = (target - origin) * R(r, 0.5, 2.0);
                CheckRay(s, origin.ToFloat3(), dir.ToFloat3(), 2e-4, tolRel, false, 0, "edge/vertex#" + i);
            }
        }

        #endregion

        #region raycast

        [Test]
        public void Raycast_RandomSoup_IdentityInstance()
        {
            Random r = new Random(1234);
            Scn s = new Scn();
            s.Add(MakeSoup(r, 3000, 8, 0.5, 3, false), 1, Affine3x4.Identity, Exact());
            RunRayBatch(s, r, 2000, 14, 1e-3, false);
            RunEdgeVertexBatch(s, r, 500, 1e-3);
        }

        [Test]
        public void Raycast_RotatedNonUniformScaledMirroredInstances()
        {
            Random r = new Random(77);
            Scn s = new Scn();
            for (int m = 0; m < 3; m++)
            {
                RefMesh rm = MakeSoup(r, 800, 4, 0.3, 1.5, false);
                for (int k = 0; k < 4; k++)
                    s.Add(rm, 100 + m, RandomMatrix(r, k != 0, k == 3, 8), Exact());
            }
            RunRayBatch(s, r, 1500, 16, 2e-3, false);
            RunEdgeVertexBatch(s, r, 400, 2e-3);
            RunRayBatch(s, r, 500, 16, 2e-3, false, 2e-4, true); // with an ignored instance
        }

        [Test]
        public void Raycast_LargeMesh_UsesUint32Indices()
        {
            // 30000 triangles with unshared vertices = 90000 vertices > 65535: exercises the uint index path
            Random r = new Random(5);
            Scn s = new Scn();
            s.Add(MakeSoup(r, 30000, 12, 0.3, 1.2, false), 1, Affine3x4.Identity, Exact());
            RunRayBatch(s, r, 400, 16, 1e-3, false);
        }

        [Test]
        public void Raycast_GridMesh_NoCracksAtSharedEdgesAndVertices()
        {
            Scn s = new Scn();
            int n = 20; double cell = 0.5;
            RefMesh grid = MakeGrid(n, cell);
            s.Add(grid, 1, Affine3x4.Identity, Exact());
            Random r = new Random(9);
            int rays = 0;
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                    for (int kind = 0; kind < 3; kind++)
                    {
                        // vertex, edge midpoint (x direction), diagonal midpoint
                        double x = (i - n / 2) * cell, z = (j - n / 2) * cell;
                        if (kind == 1) { if (i == n) continue; x += cell * 0.5; }
                        if (kind == 2) { if (i == n || j == n) continue; x += cell * 0.5; z += cell * 0.5; }
                        D3 target = new D3(x, 0, z);
                        D3 origin = target + new D3(R(r, -1, 1), R(r, 2, 6), R(r, -1, 1));
                        QueryStatus st = s.Scene.Raycast(new Ray(origin.ToFloat3(), ((target - origin) * R(r, 0.5, 2)).ToFloat3()),
                                                         float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit h);
                        Assert.AreEqual(QueryStatus.Hit, st, "crack at (" + x + "," + z + ") kind " + kind);
                        Assert.Less(Math.Abs(h.Position.X - x) + Math.Abs(h.Position.Z - z), 1e-3);
                        rays++;
                    }
            Assert.Greater(rays, 500);
        }

        [Test]
        public void Raycast_OriginInsideClosedMesh_AndSidedness()
        {
            Random r = new Random(21);
            Scn s = new Scn();
            RefMesh sphere = MakeSphere(5, 24, 16);
            RefInst plain = s.Add(sphere, 1, Affine3x4.Identity, Exact());
            RefInst ellipsoid = s.Add(sphere, 1, Trs(new D3(40, 0, 0), new D3(1, 2, 3), 0.7, 2.0, 0.5, 1.5), Exact());
            RefInst mirrored = s.Add(sphere, 1, Trs(new D3(-40, 0, 0), new D3(3, 1, 2), 1.1, -1.0, 1.0, 1.0), Exact());
            foreach (RefInst ri in new[] { plain, ellipsoid, mirrored })
            {
                for (int i = 0; i < 300; i++)
                {
                    D3 inside = RandDir(r) * (4.0 * Math.Pow(r.NextDouble(), 1.0 / 3.0));
                    Float3 o = Ref.W(ri.M, inside.ToFloat3()).ToFloat3();
                    Float3 d = (RandDir(r) * R(r, 0.5, 3)).ToFloat3();
                    // origin inside: double sided and back-only must hit (from the back), front-only must miss
                    Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit hd));
                    Assert.IsTrue(hd.BackFace, "ray leaving a closed mesh from inside hits the back face");
                    Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.BackOnly, out RayHit hb));
                    Assert.AreEqual(hd.T, hb.T, 1e-4 * (1 + hd.T));
                    Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.FrontOnly, out RayHit hf),
                                    "front only from inside must miss (also for mirrored instances)");
                    CheckRay(s, o, d, 2e-4, 2e-3, false, 0, "inside#" + i);
                }
                // from outside towards the center: front-only hits the near side, back-only the far side
                Float3 center = Ref.W(ri.M, new Float3(0, 0, 0)).ToFloat3();
                for (int i = 0; i < 50; i++)
                {
                    Float3 o = (D3.From(center) + RandDir(r) * 30.0).ToFloat3();
                    Float3 d = (D3.From(center) - D3.From(o)).ToFloat3();
                    Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.FrontOnly, out RayHit hf));
                    Assert.IsFalse(hf.BackFace);
                    Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(o, d), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.BackOnly, out RayHit hb));
                    Assert.IsTrue(hb.BackFace);
                    Assert.Greater(hb.T, hf.T);
                }
            }
        }

        [Test]
        public void Raycast_MaxDistAndLayerMask()
        {
            Scn s = new Scn();
            RefInst q = s.Add(MakeQuad(1), 1, Translate(0, 0, 0), Exact());
            Ray ray = new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(ray, 5.5f, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit h));
            Assert.AreEqual(5f, h.T, 1e-4f);
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(ray, 4.5f, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out h));
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(ray, 100f, 2u, default(InstanceHandle), RayMode.DoubleSided, out h), "layer mask 1 instance must not match mask 2");
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(new Ray(new Float3(0, 5, 0), new Float3(0, 0, 0)), 100f, out h), "zero direction");
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(new Ray(new Float3(float.NaN, 5, 0), new Float3(0, -1, 0)), 100f, out h), "NaN origin");
        }

        [Test]
        public void Raycast_Quantized_StaysWithinPrecision()
        {
            Random r = new Random(31);
            Scn s = new Scn();
            BuildOptions o = BuildOptions.Default;      // Quantize16 = true
            o.KeepSourceTriangleIds = true;
            RefInst ri = s.Add(MakeSoup(r, 3000, 10, 1.0, 3.0, true), 1, Affine3x4.Identity, o);
            Assert.IsTrue(s.Scene.TryGetMesh(1, out MeshBvh mesh));
            Assert.IsTrue(mesh.Quantized);
            // 16 bit positions: max error ~ extent / 131070 (~1.7e-4 for a 22 unit mesh), tolerances are widened accordingly
            RunRayBatch(s, r, 1500, 14, 5e-3, true, 4e-3);
            MeshBvh floatMesh = MeshBvh.Build(new ArraySource(ri.Mesh.Pos, ri.Mesh.Idx), Exact());
            Assert.Less(mesh.MemoryBytes, floatMesh.MemoryBytes, "quantized mesh must be smaller than the float version");
        }

        #endregion

        #region build robustness

        [Test]
        public void Build_DegenerateAndInvalidTriangles()
        {
            Random r = new Random(3);
            RefMesh soup = MakeSoup(r, 200, 6, 0.5, 2, true);
            List<Float3> pos = new List<Float3>(soup.Pos);
            List<int> idx = new List<int>(soup.Idx);
            int v = pos.Count;
            pos.Add(new Float3(0, 0, 0)); pos.Add(new Float3(1, 0, 0)); pos.Add(new Float3(2, 0, 0));   // v..v+2 collinear
            pos.Add(new Float3(float.NaN, 0, 0)); pos.Add(new Float3(5, 5, 5));                         // v+3 NaN, v+4 ok
            idx.AddRange(new[] { 0, 0, 1 });                  // zero area (kept)
            idx.AddRange(new[] { v, v + 1, v + 2 });          // collinear (kept)
            idx.AddRange(new[] { v + 4, v + 4, v + 4 });      // identical vertices (kept)
            idx.AddRange(new[] { v + 3, v + 4, 0 });          // NaN vertex (dropped)
            idx.AddRange(new[] { 0, 1, 99999 });              // out of range (dropped)
            idx.AddRange(new[] { 0, -1, 2 });                 // negative (dropped)
            RefMesh rm = new RefMesh { Pos = pos.ToArray(), Idx = idx.ToArray() };

            ArraySource src = new ArraySource(rm.Pos, rm.Idx);
            MeshBvh mesh = MeshBvh.Build(src, Exact());
            Assert.AreEqual(MeshState.Ready, mesh.State);
            Assert.AreEqual(203, mesh.TriangleCount);

            Scn s = new Scn();
            s.Add(rm, 1, RandomMatrix(r, true, false, 3), Exact());
            RunRayBatch(s, r, 800, 8, 2e-3, false);
            // closest point must not return NaN for degenerate triangles
            for (int i = 0; i < 100; i++)
            {
                Float3 p = new D3(R(r, -8, 8), R(r, -8, 8), R(r, -8, 8)).ToFloat3();
                if (s.Scene.ClosestPoint(p, float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit cp) == QueryStatus.Hit)
                    Assert.IsTrue(cp.Position.IsFinite && !float.IsNaN(cp.Distance));
            }
        }

        [Test]
        public void EmptyMeshes_AreHarmless()
        {
            Scn s = new Scn();
            RefMesh none = new RefMesh { Pos = new Float3[0], Idx = new int[0] };
            RefMesh allInvalid = new RefMesh { Pos = new[] { new Float3(0, 0, 0), new Float3(float.NaN, 0, 0) }, Idx = new[] { 0, 1, 1, 0, 0, 7 } };
            ArraySource nullSource = default(ArraySource);
            MeshBvh m0 = MeshBvh.Build(nullSource, BuildOptions.Default);
            Assert.AreEqual(MeshState.Ready, m0.State);
            Assert.AreEqual(0, m0.TriangleCount);
            Assert.IsFalse(m0.Bounds.IsValid);
            s.Add(none, 1, Affine3x4.Identity, Exact());
            s.Add(allInvalid, 2, Affine3x4.Identity, Exact());
            RefInst real = s.Add(MakeQuad(1), 3, Translate(0, -2, 0), Exact());
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0)), 100f, out RayHit h));
            Assert.AreEqual(real.Id, h.UserData);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(new Float3(0, 0, 0), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit cp));
            Assert.AreEqual(real.Id, cp.UserData);
            // only empty meshes: everything misses, height returns the default
            Scn e = new Scn();
            e.Add(none, 1, Affine3x4.Identity, Exact());
            Assert.AreEqual(QueryStatus.Miss, e.Scene.Raycast(new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0)), 100f, out h));
            Assert.AreEqual(QueryStatus.Miss, e.Scene.ClosestPoint(new Float3(0, 0, 0), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out cp));
            Assert.AreEqual(QueryStatus.Miss, e.Scene.HeightOverGround(new Float3(0, 5, 0), QueryAccuracy.ExactMesh, -7f, out float y));
            Assert.AreEqual(-7f, y);
        }

        [Test]
        public void ZeroScaleInstance_IsIgnoredNotCrashing()
        {
            Scn s = new Scn();
            s.Add(MakeQuad(1), 1, Trs(new D3(0, 0, 0), new D3(0, 1, 0), 0, 0, 0, 0), Exact());   // singular matrix
            RefInst ok = s.Add(MakeQuad(1), 1, Translate(0, -1, 0), Exact());
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0)), 100f, out RayHit h));
            Assert.AreEqual(ok.Id, h.UserData);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(new Float3(0.2f, 3, 0.1f), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit cp));
            Assert.AreEqual(ok.Id, cp.UserData);
        }

        [Test]
        public void TimeSlicedBuild_EqualsImmediateBuild_AndReportsProgress()
        {
            Random r = new Random(41);
            RefMesh rm = MakeSoup(r, 4000, 8, 0.4, 1.5, false);
            ArraySource src = new ArraySource(rm.Pos, rm.Idx);
            MeshBvh a = MeshBvh.Build(src, Exact());
            MeshBvh b = MeshBvh.BeginBuild(src, MeshBvh.ComputeBounds(src), Exact());
            Assert.AreEqual(MeshState.Queued, b.State);
            int steps = 0;
            float last = 0f;
            while (!b.Step(300))
            {
                steps++;
                Assert.GreaterOrEqual(b.Progress, last - 1e-6f, "progress must not go backwards");
                Assert.Less(b.Progress, 1f);
                last = b.Progress;
                Assert.Less(steps, 1000000);
            }
            Assert.Greater(steps, 20, "a 300 unit budget must slice a 4000 triangle build into many steps");
            Assert.AreEqual(MeshState.Ready, b.State);
            Assert.AreEqual(1f, b.Progress);
            Assert.AreEqual(a.NodeCount, b.NodeCount);
            Assert.AreEqual(a.MemoryBytes, b.MemoryBytes);
            Assert.AreEqual(a.TriangleCount, b.TriangleCount);

            SceneBvh sa = new SceneBvh(), sb = new SceneBvh();
            sa.Register(sa.AddPrebuiltMesh(1, a), Affine3x4.Identity);
            sb.Register(sb.AddPrebuiltMesh(1, b), Affine3x4.Identity);
            for (int i = 0; i < 500; i++)
            {
                Ray ray = new Ray(new D3(R(r, -10, 10), R(r, -10, 10), R(r, -10, 10)).ToFloat3(), (RandDir(r) * 1.5).ToFloat3());
                QueryStatus s1 = sa.Raycast(ray, float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit h1);
                QueryStatus s2 = sb.Raycast(ray, float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit h2);
                Assert.AreEqual(s1, s2);
                if (s1 == QueryStatus.Hit) { Assert.AreEqual(h1.T, h2.T); Assert.AreEqual(h1.SourceTriangle, h2.SourceTriangle); }
            }
            // time budget driven
            MeshBvh c = MeshBvh.BeginBuild(src, MeshBvh.ComputeBounds(src), Exact());
            int guard = 0;
            while (!c.StepMs(0.2)) Assert.Less(++guard, 1000000);
            Assert.AreEqual(a.NodeCount, c.NodeCount);
        }

        [Test]
        public void MeshStillBuilding_ReportsApproximate_ThenExact()
        {
            Random r = new Random(51);
            RefMesh rm = MakeSoup(r, 3000, 6, 0.5, 1.5, false);
            ArraySource src = new ArraySource(rm.Pos, rm.Idx);
            Aabb exact = MeshBvh.ComputeBounds(src);
            Aabb hint = new Aabb(exact.Min - new Float3(1, 1, 1), exact.Max + new Float3(1, 1, 1));
            SceneBvh scene = new SceneBvh();
            MeshBvh mesh = scene.AddMesh(1, src, hint, Exact(), BuildMode.Sliced);
            InstanceHandle h = scene.Register(mesh, Affine3x4.Identity, 1u, InstanceFlags.Snappable, 1);
            Assert.AreEqual(1, scene.PendingBuilds);
            int v0 = scene.Version;

            Ray down = new Ray(new Float3(0, 50, 0), new Float3(0, -1, 0));
            Assert.AreEqual(QueryStatus.HitApproximate, scene.Raycast(down, 1000f, uint.MaxValue, default(InstanceHandle), RayMode.DoubleSided, out RayHit ah));
            Assert.IsTrue(ah.Approximate);
            Assert.AreEqual(-1, ah.Triangle);
            Assert.AreEqual(50f - hint.Max.Y, ah.T, 1e-3f);
            Assert.AreEqual(QueryStatus.HitApproximate, scene.HeightOverGround(new Float3(0, 50, 0), QueryAccuracy.ExactMesh, 0f, out float gy));
            Assert.AreEqual(QueryStatus.HitApproximate, scene.ClosestPoint(new Float3(0, 50, 0), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit ac));
            Assert.IsTrue(ac.Approximate);

            int guard = 0;
            while (scene.StepBuilds(1.0) > 0) Assert.Less(++guard, 1000000);
            Assert.AreEqual(MeshState.Ready, mesh.State);
            Assert.Greater(scene.Version, v0, "a finished build changes instance bounds and must bump Version");
            Assert.IsTrue(scene.TryGetWorldBounds(h, out Aabb wb));
            Assert.AreEqual(exact.Max.Y, wb.Max.Y, 1e-4f);
            Assert.AreEqual(exact.Min.X, wb.Min.X, 1e-4f);

            // exact now: compare with the brute force reference
            Scn s = new Scn();
            s.Add(rm, 1, Affine3x4.Identity, Exact());
            RunRayBatch(s, r, 300, 8, 1e-3, false);
        }

        [Test]
        public void LazyMesh_IsBuiltByTheFirstQueryThatNeedsIt()
        {
            Random r = new Random(61);
            RefMesh rm = MakeSoup(r, 1000, 5, 0.5, 1.5, false);
            ArraySource src = new ArraySource(rm.Pos, rm.Idx);
            SceneBvh scene = new SceneBvh();
            MeshBvh mesh = scene.AddMesh(1, src, MeshBvh.ComputeBounds(src), Exact(), BuildMode.Lazy);
            scene.Register(mesh, Affine3x4.Identity);
            Assert.AreEqual(MeshState.Queued, mesh.State);
            Assert.AreEqual(QueryStatus.Miss, scene.Raycast(new Ray(new Float3(500, 500, 500), new Float3(0, 1, 0)), 10f, out RayHit h), "a query that misses the bounds must not build");
            Assert.AreEqual(MeshState.Queued, mesh.State);
            Assert.AreEqual(QueryStatus.Hit, scene.ClosestPoint(new Float3(0, 0, 0), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit cp));
            Assert.AreEqual(MeshState.Ready, mesh.State);
            Assert.IsFalse(cp.Approximate);
        }

        #endregion

        #region closest point, overlap

        [Test]
        public void ClosestPoint_MatchesBruteForce_IncludingNonUniformScale()
        {
            Random r = new Random(81);
            Scn s = new Scn();
            RefMesh a = MakeSoup(r, 400, 3, 0.3, 1.2, false), b = MakeSoup(r, 400, 3, 0.3, 1.2, true);
            for (int k = 0; k < 4; k++) s.Add(a, 1, RandomMatrix(r, k > 0, k == 2, 6), Exact());
            for (int k = 0; k < 3; k++) s.Add(b, 2, RandomMatrix(r, true, k == 1, 6), Exact());
            s.Add(b, 2, Trs(new D3(0, 8, 0), new D3(1, 1, 0), 0.4, 0.05, 5.0, 1.0), Exact());   // extreme stretch
            int features = 0;
            for (int i = 0; i < 250; i++)
            {
                Float3 p = new D3(R(r, -14, 14), R(r, -14, 14), R(r, -14, 14)).ToFloat3();
                double bd = Ref.NearestPoint(s.Refs, D3.From(p), 0, out RefInst bi, out int bt);
                Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(p, float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit h), "point#" + i);
                double tol = 1e-3 * (1 + bd);
                Assert.AreEqual(bd, h.Distance, tol, "distance, point#" + i);

                RefInst ri = s.ById(h.UserData);
                int t = h.SourceTriangle;
                Assert.AreEqual(ri.Mesh.Idx[3 * t], h.V0); Assert.AreEqual(ri.Mesh.Idx[3 * t + 1], h.V1); Assert.AreEqual(ri.Mesh.Idx[3 * t + 2], h.V2);
                D3 w0 = ri.WV[h.V0], w1 = ri.WV[h.V1], w2 = ri.WV[h.V2];
                double dTri = Ref.DistPointTri(D3.From(p), w0, w1, w2, out D3 q);
                Assert.AreEqual(dTri, h.Distance, tol, "reported triangle is not at the reported distance");
                D3 pos = D3.From(h.Position);
                Assert.Less((pos - q).Len, tol, "position is not the closest point of its triangle");
                Assert.Greater(D3.Dot(D3.From(h.Normal), ri.RefNormal(w0, w1, w2)), 0.99);

                D3[] w = { w0, w1, w2 };
                switch (h.Feature)
                {
                    case SnapFeature.Vertex: Assert.Less((pos - w[h.FeatureIndex]).Len, tol); break;
                    case SnapFeature.Edge: Assert.Less((pos - Ref.SegClosest(pos, w[h.FeatureIndex], w[(h.FeatureIndex + 1) % 3])).Len, tol); break;
                    case SnapFeature.Face:
                        Assert.Less(Math.Abs(D3.Dot(pos - w0, D3.Cross(w1 - w0, w2 - w0).Norm())), tol); break;
                    default: Assert.Fail("feature must be set for an exact result"); break;
                }
                features++;

                // maxDist: far below -> miss, comfortably above -> hit
                Assert.AreEqual(QueryStatus.Miss, s.Scene.ClosestPoint(p, (float)(bd * 0.5), uint.MaxValue, default(InstanceHandle), out ClosestPointHit m));
                Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(p, (float)(bd * 1.1 + 0.01), uint.MaxValue, default(InstanceHandle), out m));
                Assert.AreEqual(bd, m.Distance, tol);
            }
            Assert.AreEqual(250, features);
        }

        [Test]
        public void ClosestPoint_IgnoreAndSnappableFlag()
        {
            Scn s = new Scn();
            RefInst near = s.Add(MakeQuad(2), 1, Translate(0, 1, 0), Exact());
            RefInst far = s.Add(MakeQuad(2), 1, Translate(0, -3, 0), Exact());
            RefInst notSnappable = s.Add(MakeQuad(2), 1, Translate(0, 0.5, 0), Exact(), InstanceFlags.None);
            Float3 p = new Float3(0, 2, 0);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(p, float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit h));
            Assert.AreEqual(near.Id, h.UserData, "non snappable instances are no snap targets");
            Assert.AreEqual(1f, h.Distance, 1e-4f);
            Assert.AreEqual(SnapFeature.Face, h.Feature);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.ClosestPoint(p, float.PositiveInfinity, uint.MaxValue, near.Handle, out h));
            Assert.AreEqual(far.Id, h.UserData);
        }

        [Test]
        public void Overlap_MatchesBruteForceOnInstanceBounds()
        {
            Random r = new Random(91);
            Scn s = new Scn();
            RefMesh a = MakeSoup(r, 100, 2, 0.3, 1, false);
            for (int k = 0; k < 20; k++) s.Add(a, 1, RandomMatrix(r, true, k % 5 == 0, 12), Exact());
            InstanceHandle[] buf = new InstanceHandle[64];
            for (int i = 0; i < 200; i++)
            {
                Float3 c = new D3(R(r, -14, 14), R(r, -14, 14), R(r, -14, 14)).ToFloat3();
                float rad = (float)R(r, 0.5, 8);
                Aabb box = new Aabb(c - new Float3(rad, rad * 0.5f, rad * 1.5f), c + new Float3(rad * 1.2f, rad, rad * 0.7f));
                HashSet<int> expSphere = new HashSet<int>(), expBox = new HashSet<int>();
                foreach (RefInst ri in s.Refs)
                {
                    Aabb wb = ri.WorldBounds();
                    if (wb.SqrDistance(c) <= rad * rad) expSphere.Add(ri.Id);
                    if (wb.Overlaps(box)) expBox.Add(ri.Id);
                }
                QueryStatus st = s.Scene.OverlapSphere(c, rad, uint.MaxValue, default(InstanceHandle), buf, out int n);
                Assert.AreEqual(expSphere.Count > 0 ? QueryStatus.Hit : QueryStatus.Miss, st);
                HashSet<int> got = new HashSet<int>();
                for (int k = 0; k < n; k++) { Assert.IsTrue(s.Scene.TryGetUserData(buf[k], out int ud)); got.Add(ud); }
                Assert.IsTrue(expSphere.SetEquals(got), "sphere overlap #" + i);

                s.Scene.OverlapAabb(box, uint.MaxValue, default(InstanceHandle), buf, out n);
                got.Clear();
                for (int k = 0; k < n; k++) { s.Scene.TryGetUserData(buf[k], out int ud); got.Add(ud); }
                Assert.IsTrue(expBox.SetEquals(got), "aabb overlap #" + i);
            }
            // buffer smaller than the result: truncated, no exception
            InstanceHandle[] small = new InstanceHandle[2];
            s.Scene.OverlapAabb(new Aabb(new Float3(-100, -100, -100), new Float3(100, 100, 100)), uint.MaxValue, default(InstanceHandle), small, out int cnt);
            Assert.AreEqual(2, cnt);
        }

        #endregion

        #region height over ground

        [Test]
        public void HeightOverGround_Accuracies_Ignore_AndInsideBounds()
        {
            Scn s = new Scn();
            RefInst cube = s.Add(MakeBox(new D3(0, 1, 0), new D3(1, 1, 1)), 1, Affine3x4.Identity, Exact());   // y from 0 to 2
            Float3 above = new Float3(0.3f, 10, 0.2f);
            foreach (QueryAccuracy acc in new[] { QueryAccuracy.BoundingBox, QueryAccuracy.ExactMesh, QueryAccuracy.NearestVertex })
            {
                Assert.AreEqual(QueryStatus.Hit, s.Scene.HeightOverGround(above, acc, -5f, out float y), acc.ToString());
                Assert.AreEqual(2f, y, 1e-4f, acc.ToString());
            }
            // below everything: default
            Assert.AreEqual(QueryStatus.Miss, s.Scene.HeightOverGround(new Float3(0, -10, 0), QueryAccuracy.ExactMesh, -5f, out float yd));
            Assert.AreEqual(-5f, yd);
            // the dragged object ignores itself
            Assert.AreEqual(QueryStatus.Miss, s.Scene.HeightOverGround(above, QueryAccuracy.ExactMesh, -5f, out yd, cube.Handle, uint.MaxValue, float.PositiveInfinity));
            Assert.AreEqual(-5f, yd);

            // origin INSIDE the box
            Float3 inside = new Float3(0.3f, 1f, 0.2f);
            s.Scene.HeightOverGround(inside, QueryAccuracy.ExactMesh, -5f, out float yExact);
            Assert.AreEqual(0f, yExact, 1e-4f, "exact: the floor of the closed mesh below the origin");
            s.Scene.HeightOverGround(inside, QueryAccuracy.BoundingBox, -5f, out float yBox);
            Assert.AreEqual(1f, yBox, 1e-4f, "bounding box inside: origin.y (documented behaviour, see BoundsHeightInsideUsesExit)");
            s.Scene.BoundsHeightInsideUsesExit = true;
            s.Scene.HeightOverGround(inside, QueryAccuracy.BoundingBox, -5f, out yBox);
            Assert.AreEqual(0f, yBox, 1e-4f, "bounding box inside with exit rule: bottom face");
            s.Scene.BoundsHeightInsideUsesExit = false;

            // the documented caveat: a large box that ENCLOSES the origin wins with BoundingBox (result = origin.y),
            // while ExactMesh still finds the cube below
            s.Add(MakeBox(new D3(0, 0, 0), new D3(50, 50, 50)), 2, Affine3x4.Identity, Exact());
            s.Scene.HeightOverGround(above, QueryAccuracy.BoundingBox, -5f, out yBox);
            Assert.AreEqual(above.Y, yBox, 1e-4f, "enclosing box: origin.y (caveat of BoundsHeightInsideUsesExit = false)");
            s.Scene.HeightOverGround(above, QueryAccuracy.ExactMesh, -5f, out float yBig);
            Assert.AreEqual(2f, yBig, 1e-3f, "exact mesh is not affected by the enclosing box");
        }

        [Test]
        public void HeightOverGround_NearestVertex_Radius()
        {
            Scn s = new Scn();
            RefMesh tri = new RefMesh { Pos = new[] { new Float3(0, 0, 0), new Float3(10, 5, 0), new Float3(0, 0, 10) }, Idx = new[] { 0, 1, 2 } };
            s.Add(tri, 1, Affine3x4.Identity, Exact());
            Float3 o = new Float3(1, 20, 1);   // plane y = 0.5 x, hit at y = 0.5, nearest corner (0,0,0) is 1.5 away
            s.Scene.HeightOverGround(o, QueryAccuracy.ExactMesh, -1f, out float y);
            Assert.AreEqual(0.5f, y, 1e-4f);
            s.Scene.HeightOverGround(o, QueryAccuracy.NearestVertex, -1f, out y);
            Assert.AreEqual(0f, y, 1e-4f, "unlimited radius snaps to the nearest corner");
            s.Scene.HeightOverGround(o, QueryAccuracy.NearestVertex, -1f, out y, default(InstanceHandle), uint.MaxValue, 1.0f);
            Assert.AreEqual(0.5f, y, 1e-4f, "corner out of radius: plain hit height");
        }

        #endregion

        #region instances

        [Test]
        public void RegisterUnregister_HandlesGenerationsAndMeshLifetime()
        {
            Scn s = new Scn();
            RefInst a = s.Add(MakeQuad(1), 1, Translate(0, 0, 0), Exact());
            RefInst b = s.Add(MakeQuad(1), 1, Translate(100, 0, 0), Exact());   // shares the mesh
            Assert.AreEqual(2, s.Scene.InstanceCount);
            Ray atA = new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0)), atB = new Ray(new Float3(100, 5, 0), new Float3(0, -1, 0));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(atA, 100f, out RayHit h)); Assert.AreEqual(a.Id, h.UserData);
            int v0 = s.Scene.Version;

            Assert.IsTrue(s.Scene.Unregister(a.Handle));
            Assert.IsFalse(s.Scene.Unregister(a.Handle), "second unregister");
            Assert.IsFalse(s.Scene.IsValid(a.Handle));
            Assert.Greater(s.Scene.Version, v0);
            Assert.IsTrue(s.Scene.TryGetChangedBoundsSince(v0, out Aabb changed));
            Assert.IsTrue(changed.Contains(new Float3(0, 0, 0)), "the removed instance must be inside the changed bounds");
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(atA, 100f, out h));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(atB, 100f, out h)); Assert.AreEqual(b.Id, h.UserData);

            // slot reuse: new generation, stale handle stays invalid and does not ignore the new instance
            MeshBvh mesh; Assert.IsTrue(s.Scene.TryGetMesh(1, out mesh));
            InstanceHandle c = s.Scene.Register(mesh, Translate(0, 0, 0), 1u, InstanceFlags.Snappable, 99);
            Assert.AreEqual(a.Handle.Index, c.Index);
            Assert.AreNotEqual(a.Handle.Generation, c.Generation);
            Assert.IsFalse(s.Scene.IsValid(a.Handle));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(atA, 100f, uint.MaxValue, a.Handle, RayMode.DoubleSided, out h));
            Assert.AreEqual(99, h.UserData);
            Assert.AreEqual(QueryStatus.Miss, s.Scene.Raycast(atA, 100f, uint.MaxValue, c, RayMode.DoubleSided, out h));

            // mesh BVH is released with the last instance (AutoReleaseMeshes)
            s.Scene.Unregister(b.Handle); s.Scene.Unregister(c);
            Assert.IsFalse(s.Scene.TryGetMesh(1, out mesh));
            Assert.AreEqual(0, s.Scene.InstanceCount);
        }

        [Test]
        public void AutoReleaseMeshes_CanBeDisabled()
        {
            Scn s = new Scn();
            s.Scene.AutoReleaseMeshes = false;
            RefInst a = s.Add(MakeQuad(1), 1, Translate(0, 0, 0), Exact());
            s.Scene.Unregister(a.Handle);
            Assert.IsTrue(s.Scene.TryGetMesh(1, out MeshBvh mesh));
            Assert.IsTrue(s.Scene.RemoveMesh(1));
            Assert.IsFalse(s.Scene.TryGetMesh(1, out mesh));
        }

        [Test]
        public void DynamicInstances_FollowTheirTransformDuringUse()
        {
            Dictionary<int, Affine3x4> mats = new Dictionary<int, Affine3x4>();
            Scn s = new Scn();
            s.Scene.DynamicTransformProvider = (int ud, out Affine3x4 m) => mats.TryGetValue(ud, out m);
            RefMesh quad = MakeQuad(1);
            RefInst ground = s.Add(quad, 1, Translate(0, -2, 0), Exact());
            mats[7] = Translate(0, 0, 0);
            ArraySource src = new ArraySource(quad.Pos, quad.Idx);
            Assert.IsTrue(s.Scene.TryGetMesh(1, out MeshBvh mesh));
            InstanceHandle mover = s.Scene.Register(mesh, mats[7], 1u, InstanceFlags.Snappable | InstanceFlags.Dynamic, 7);

            Ray down = new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out RayHit h));
            Assert.AreEqual(5f, h.T, 1e-4f);

            int v0 = s.Scene.Version;
            mats[7] = Translate(0, 3, 0);                     // no explicit Refresh: the next query refreshes lazily
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out h));
            Assert.AreEqual(2f, h.T, 1e-4f);
            Assert.Greater(s.Scene.Version, v0);
            Assert.IsTrue(s.Scene.TryGetChangedBoundsSince(v0, out Aabb changed));
            Assert.IsTrue(changed.Contains(new Float3(0, 0, 0)) && changed.Contains(new Float3(0, 3, 0)), "old and new position must be covered");
            Assert.IsFalse(s.Scene.TryGetChangedBoundsSince(s.Scene.Version, out changed), "no change since the current version");

            // batch: matrices are refreshed once at BeginBatch, not per query
            s.Scene.BeginBatch();
            mats[7] = Translate(0, 1, 0);
            Assert.AreEqual(2f, s.Scene.Raycast(down, 100f, out h) == QueryStatus.Hit ? h.T : -1f, 1e-4f, "stale inside the batch");
            s.Scene.EndBatch();
            s.Scene.BeginBatch();
            Assert.AreEqual(4f, s.Scene.Raycast(down, 100f, out h) == QueryStatus.Hit ? h.T : -1f, 1e-4f, "refreshed by the next BeginBatch");
            s.Scene.EndBatch();

            // explicit refresh of one instance
            mats[7] = Translate(0, 0, 0);
            Assert.IsTrue(s.Scene.Refresh(mover));
            s.Scene.BeginBatch();
            Assert.AreEqual(5f, s.Scene.Raycast(down, 100f, out h) == QueryStatus.Hit ? h.T : -1f, 1e-4f);
            s.Scene.EndBatch();

            // the object under the cursor ignores itself, the height display still sees the ground it is above
            Assert.AreEqual(QueryStatus.Hit, s.Scene.HeightOverGround(new Float3(0, 5, 0), QueryAccuracy.ExactMesh, -9f, out float y, mover, uint.MaxValue, float.PositiveInfinity));
            Assert.AreEqual(-2f, y, 1e-4f);
            // a mover passing underneath changes the height: detected through the version / changed bounds
            int vSeen = s.Scene.Version;
            mats[7] = Translate(0, 1, 0);
            s.Scene.Refresh(mover);
            Assert.IsTrue(s.Scene.TryGetChangedBoundsSince(vSeen, out changed));
            Assert.IsTrue(changed.Contains(new Float3(0, 1, 0)));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.HeightOverGround(new Float3(0, 5, 0), QueryAccuracy.ExactMesh, -9f, out y));
            Assert.AreEqual(1f, y, 1e-4f);
        }

        [Test]
        public void IgnoreSet_And_SetFlags()
        {
            Dictionary<int, Affine3x4> mats = new Dictionary<int, Affine3x4>();
            Scn s = new Scn();
            s.Scene.DynamicTransformProvider = (int ud, out Affine3x4 m) => mats.TryGetValue(ud, out m);
            RefInst a = s.Add(MakeQuad(1), 1, Translate(0, 0, 0), Exact());
            RefInst b = s.Add(MakeQuad(1), 1, Translate(0, -1, 0), Exact());
            RefInst c = s.Add(MakeQuad(1), 1, Translate(0, -2, 0), Exact());
            Ray down = new Ray(new Float3(0, 5, 0), new Float3(0, -1, 0));
            InstanceHandle[] set = { a.Handle, b.Handle };
            s.Scene.SetIgnoreSet(set, 2);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out RayHit h));
            Assert.AreEqual(c.Id, h.UserData, "a and b are in the ignore set");
            s.Scene.SetIgnoreSet(null, 0);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out h));
            Assert.AreEqual(a.Id, h.UserData);

            // flags: only Dynamic instances are refreshed from the provider
            mats[a.Id] = Translate(0, 3, 0);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out h));
            Assert.AreEqual(5f, h.T, 1e-4f, "static instance is not refreshed");
            Assert.IsTrue(s.Scene.SetFlags(a.Handle, InstanceFlags.Snappable | InstanceFlags.Dynamic));
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out h));
            Assert.AreEqual(2f, h.T, 1e-4f, "now dynamic");
            Assert.IsTrue(s.Scene.SetFlags(a.Handle, InstanceFlags.Snappable));
            mats[a.Id] = Translate(0, 4, 0);
            Assert.AreEqual(QueryStatus.Hit, s.Scene.Raycast(down, 100f, out h));
            Assert.AreEqual(2f, h.T, 1e-4f, "static again");
        }

        [Test]
        public void ChangeLog_OverflowReportsEverything()
        {
            SceneBvh scene = new SceneBvh();
            RefMesh quad = MakeQuad(1);
            ArraySource src = new ArraySource(quad.Pos, quad.Idx);
            MeshBvh mesh = scene.AddMesh(1, src, MeshBvh.ComputeBounds(src), Exact(), BuildMode.Immediate);
            InstanceHandle h = scene.Register(mesh, Affine3x4.Identity);
            int v0 = scene.Version;
            for (int i = 0; i < 100; i++) scene.SetTransform(h, Translate(i, 0, 0));
            Assert.IsTrue(scene.TryGetChangedBoundsSince(v0, out Aabb box));
            Assert.IsTrue(box.Contains(new Float3(1e6f, 1e6f, 1e6f)), "more changes than the log holds: whole world");
        }

        #endregion

        #region allocations

        [Test]
        public void Queries_DoNotAllocate()
        {
            Random r = new Random(101);
            Scn s = new Scn();
            RefMesh m = MakeSoup(r, 500, 3, 0.3, 1.2, false);
            for (int k = 0; k < 10; k++) s.Add(m, 1, RandomMatrix(r, true, k == 3, 6), Exact());
            InstanceHandle[] buf = new InstanceHandle[16];
            Ray ray = new Ray(new Float3(0, 20, 0), new Float3(0.01f, -1, 0.02f));
            // warm up (JIT, first-use paths)
            s.Scene.Raycast(ray, 100f, out RayHit h);
            s.Scene.HeightOverGround(new Float3(0, 20, 0), QueryAccuracy.NearestVertex, 0f, out float y);
            s.Scene.ClosestPoint(new Float3(1, 1, 1), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out ClosestPointHit cp);
            s.Scene.OverlapSphere(new Float3(0, 0, 0), 5f, uint.MaxValue, default(InstanceHandle), buf, out int n);
            s.Scene.OverlapAabb(new Aabb(new Float3(-5, -5, -5), new Float3(5, 5, 5)), uint.MaxValue, default(InstanceHandle), buf, out n);
            GC.Collect();
            int gen0 = GC.CollectionCount(0);
            for (int i = 0; i < 20000; i++)
            {
                float f = (i % 100) * 0.1f - 5f;
                s.Scene.Raycast(new Ray(new Float3(f, 20, -f), new Float3(0.01f, -1, 0.02f)), 100f, out h);
                s.Scene.HeightOverGround(new Float3(f, 20, f), QueryAccuracy.ExactMesh, 0f, out y);
                s.Scene.ClosestPoint(new Float3(f, 1, -f), float.PositiveInfinity, uint.MaxValue, default(InstanceHandle), out cp);
                s.Scene.OverlapSphere(new Float3(f, 0, 0), 5f, uint.MaxValue, default(InstanceHandle), buf, out n);
                s.Scene.OverlapAabb(new Aabb(new Float3(f, -5, -5), new Float3(f + 5, 5, 5)), uint.MaxValue, default(InstanceHandle), buf, out n);
            }
            Assert.AreEqual(gen0, GC.CollectionCount(0), "queries must not allocate (a gen0 collection happened)");
        }

        #endregion
    }
}
#endif
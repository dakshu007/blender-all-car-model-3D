// PropKit.cs - building blocks for the village / culture / nature props (WorldProps):
//  * PropModel   - one prop as low-poly geometry split by render kind (plain, swaying cloth, glow...)
//  * PropBucket  - merged world-space mesh data for one kind in one map chunk
//  * PropMeshes  - all buckets; stamping models into them and turning them into a few renderers
//  * PG          - extra geometry helpers on top of LifeGeo (oriented boxes, beams, curved roofs...)
//  * Pal         - shared pastel palette (sRGB, alpha = wind weight for BlossomRally/VertexColorWind)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    /// <summary>Render kinds: each becomes one material (and one renderer per map chunk).</summary>
    internal enum PK
    {
        Props = 0,      // small vertex-coloured props (layer 9, culled with the trees)
        Landmark = 1,   // big buildings / landmarks (layer 0, never distance culled)
        Strong = 2,     // cloth, awnings, willow strands: strong wind sway
        Glow = 3,       // lanterns / lit windows (BlossomRally/PropGlow, swaying)
        Water = 4,      // rice paddies and ponds (BlossomRally/Water instance)
        Stream = 5,     // brooks and mini waterfalls (BlossomRally/FlowFoam instance)
        Blades = 6,     // seedlings / flower stems in the BR_Grass vertex format (layer 8)
        Shafts = 7,     // additive light shafts in the bamboo forest
        Karst = 8       // far limestone pillars (layer 0)
    }

    internal sealed class PropModel
    {
        public const int Kinds = 9;
        public readonly LifeGeo[] geo = new LifeGeo[Kinds];

        public LifeGeo this[PK k]
        {
            get
            {
                int i = (int)k;
                if (geo[i] == null) geo[i] = new LifeGeo();
                return geo[i];
            }
        }

        public void Append(PropModel src, Matrix4x4 m)
        {
            for (int i = 0; i < Kinds; i++)
                if (src.geo[i] != null && src.geo[i].v.Count > 0) PG.Append(this[(PK)i], src.geo[i], m);
        }
    }

    internal sealed class PropBucket
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Color32> c = new List<Color32>();
        public readonly List<Vector4> uv = new List<Vector4>();
        public readonly List<int> t = new List<int>();
        public readonly bool hasUv;
        public readonly PK kind;
        public readonly int cx, cz;

        public PropBucket(PK k, bool uvs, int x, int z) { kind = k; hasUv = uvs; cx = x; cz = z; }

        public void Add(LifeGeo g, Matrix4x4 m)
        {
            int o = v.Count;
            for (int i = 0; i < g.v.Count; i++)
            {
                v.Add(m.MultiplyPoint3x4(g.v[i]));
                n.Add(m.MultiplyVector(g.n[i]).normalized);
                c.Add(g.c[i]);
                if (hasUv) uv.Add(Vector4.zero);
            }
            for (int i = 0; i < g.t.Count; i++) t.Add(g.t[i] + o);
        }

        public int Vert(Vector3 p, Vector3 nn, Color32 col, Vector4 u)
        {
            v.Add(p); n.Add(nn); c.Add(col);
            if (hasUv) uv.Add(u);
            return v.Count - 1;
        }

        public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }
    }

    internal sealed class PropMeshes
    {
        struct KindInfo
        {
            public int layer; public bool shadows, uv, chunked; public float expand;
            public KindInfo(int l, bool s, bool u, bool c, float e) { layer = l; shadows = s; uv = u; chunked = c; expand = e; }
        }

        static readonly KindInfo[] Info =
        {
            new KindInfo(WorldBuilder.LayerTrees, true, false, true, 0.6f),   // Props
            new KindInfo(0, true, false, true, 0.6f),                          // Landmark
            new KindInfo(WorldBuilder.LayerTrees, true, false, true, 1.5f),   // Strong
            new KindInfo(0, false, false, false, 0.8f),                        // Glow
            new KindInfo(WorldBuilder.LayerTrees, false, true, false, 0.2f),  // Water
            new KindInfo(WorldBuilder.LayerTrees, false, true, false, 0.2f),  // Stream
            new KindInfo(WorldBuilder.LayerGrass, false, true, true, 1.5f),   // Blades
            new KindInfo(WorldBuilder.LayerTrees, false, true, false, 0.5f),  // Shafts
            new KindInfo(0, false, false, false, 1f),                          // Karst
        };

        readonly Dictionary<int, PropBucket> buckets = new Dictionary<int, PropBucket>();
        readonly List<PropBucket> order = new List<PropBucket>();
        public readonly Material[] mats = new Material[PropModel.Kinds];
        public int Renderers { get; private set; }
        public int Vertices { get; private set; }
        public int Triangles { get; private set; }

        /// <summary>World grid of 334 m chunks (three per axis across each 1 km region): few draw calls, still frustum culled.</summary>
        static int Cell(float x) { return Mathf.Clamp(Mathf.FloorToInt((x + 1840f) / 334f), 0, 99); }

        public PropBucket Get(PK k, Vector3 at)
        {
            var inf = Info[(int)k];
            int cx = inf.chunked ? Cell(at.x) : 0, cz = inf.chunked ? Cell(at.z) : 0;
            int key = (int)k * 10000 + cx * 100 + cz;
            PropBucket b;
            if (!buckets.TryGetValue(key, out b))
            {
                b = new PropBucket(k, inf.uv, cx, cz);
                buckets[key] = b;
                order.Add(b);
            }
            return b;
        }

        public void Stamp(PropModel m, Matrix4x4 mat, Vector3 at)
        {
            for (int i = 0; i < PropModel.Kinds; i++)
            {
                var g = m.geo[i];
                if (g != null && g.v.Count > 0) Get((PK)i, at).Add(g, mat);
            }
        }

        public void Stamp(PropModel m, Vector3 pos, float yaw, float scale = 1f)
        {
            Stamp(m, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale), pos);
        }

        public void Finish(Transform parent)
        {
            foreach (var b in order)
            {
                if (b.v.Count == 0 || b.t.Count == 0) continue;
                var inf = Info[(int)b.kind];
                var mesh = new Mesh { name = "Props_" + b.kind + "_" + b.cx + "_" + b.cz };
                if (b.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.v);
                mesh.SetNormals(b.n);
                mesh.SetColors(b.c);
                if (b.hasUv) mesh.SetUVs(0, b.uv);
                mesh.SetTriangles(b.t, 0);
                if (b.kind == PK.Water) mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                var bnd = mesh.bounds;
                bnd.Expand(inf.expand);
                mesh.bounds = bnd;
                mesh.UploadMeshData(false);
                var go = new GameObject(mesh.name);
                go.layer = inf.layer;
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[(int)b.kind];
                mr.shadowCastingMode = inf.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = b.kind != PK.Shafts && b.kind != PK.Stream;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = b.kind == PK.Water ? ReflectionProbeUsage.BlendProbes : ReflectionProbeUsage.Off;
                Renderers++;
                Vertices += b.v.Count;
                Triangles += b.t.Count / 3;
            }
            buckets.Clear();
            order.Clear();
        }
    }

    /// <summary>Pastel palette (sRGB). Alpha is the wind weight for VertexColorWind / PropGlow.</summary>
    internal static class Pal
    {
        public static readonly Color32 Red = LifeGeo.C(186, 44, 38), RedBright = LifeGeo.C(218, 56, 44), RedDeep = LifeGeo.C(150, 30, 32);
        public static readonly Color32 Gold = LifeGeo.C(232, 184, 78), GoldDark = LifeGeo.C(184, 136, 54);
        public static readonly Color32 Plaster = LifeGeo.C(238, 232, 218), PlasterShade = LifeGeo.C(214, 206, 190);
        public static readonly Color32 Brick = LifeGeo.C(158, 154, 150), BrickDark = LifeGeo.C(122, 118, 116);
        public static readonly Color32 Tile = LifeGeo.C(72, 78, 92), Tile2 = LifeGeo.C(92, 98, 112), TileUnder = LifeGeo.C(120, 70, 52);
        public static readonly Color32 GreenTile = LifeGeo.C(62, 122, 98), GreenTile2 = LifeGeo.C(80, 142, 112);
        public static readonly Color32 Wood = LifeGeo.C(136, 92, 60), WoodDark = LifeGeo.C(90, 60, 42), WoodLight = LifeGeo.C(184, 142, 96);
        public static readonly Color32 Stone = LifeGeo.C(178, 174, 164), StoneDark = LifeGeo.C(132, 128, 122), StoneLight = LifeGeo.C(204, 200, 190);
        public static readonly Color32 Bronze = LifeGeo.C(126, 100, 60), BronzeDark = LifeGeo.C(84, 68, 46);
        public static readonly Color32 Ink = LifeGeo.C(38, 32, 34), Black = LifeGeo.C(30, 30, 34);
        public static readonly Color32 Earth = LifeGeo.C(130, 104, 74), EarthGrass = LifeGeo.C(122, 140, 80);
        public static readonly Color32 Lattice = LifeGeo.C(70, 50, 40), WindowWarm = LifeGeo.C(255, 214, 140);
        public static readonly Color32 Porcelain = LifeGeo.C(238, 240, 246), PorcelainBlue = LifeGeo.C(70, 100, 170);
        public static readonly Color32 Bamboo = LifeGeo.C(196, 176, 112);
    }

    /// <summary>Geometry helpers on top of LifeGeo (all in the model's local space).</summary>
    internal static class PG
    {
        public static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        public static Color32 A(Color32 c, int a) { c.a = (byte)Mathf.Clamp(a, 0, 255); return c; }
        public static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }

        public static void Append(LifeGeo dst, LifeGeo src, Matrix4x4 m)
        {
            int o = dst.v.Count;
            for (int i = 0; i < src.v.Count; i++)
            {
                dst.v.Add(m.MultiplyPoint3x4(src.v[i]));
                dst.n.Add(m.MultiplyVector(src.n[i]).normalized);
                dst.c.Add(src.c[i]);
            }
            for (int i = 0; i < src.t.Count; i++) dst.t.Add(src.t[i] + o);
        }

        /// <summary>Axis-aligned box from min / max corners.</summary>
        public static void BoxMM(LifeGeo g, Vector3 min, Vector3 max, Color32 col)
        {
            g.Box((min + max) * 0.5f, (max - min) * 0.5f, col);
        }

        /// <summary>Axis-aligned box with its own top colour.</summary>
        public static void Box2(LifeGeo g, Vector3 c, Vector3 h, Color32 side, Color32 top)
        {
            Vector3 x = new Vector3(h.x, 0f, 0f), y = new Vector3(0f, h.y, 0f), z = new Vector3(0f, 0f, h.z);
            for (int s = -1; s <= 1; s += 2)
            {
                g.QuadOut(c + x * s - y - z, c + x * s + y - z, c + x * s + y + z, c + x * s - y + z, x * s, side, side, side, side);
                g.QuadOut(c + z * s - x - y, c + z * s + x - y, c + z * s + x + y, c + z * s - x + y, z * s, side, side, side, side);
            }
            g.QuadOut(c + y - x - z, c + y + x - z, c + y + x + z, c + y - x + z, Vector3.up, top, top, top, top);
            Color32 b = Sh(side, 0.7f);
            g.QuadOut(c - y - x - z, c - y + x - z, c - y + x + z, c - y - x + z, Vector3.down, b, b, b, b);
        }

        /// <summary>Oriented box.</summary>
        public static void OBox(LifeGeo g, Vector3 c, Vector3 h, Quaternion q, Color32 col)
        {
            Vector3 x = q * new Vector3(h.x, 0f, 0f), y = q * new Vector3(0f, h.y, 0f), z = q * new Vector3(0f, 0f, h.z);
            for (int s = -1; s <= 1; s += 2)
            {
                g.QuadOut(c + x * s - y - z, c + x * s + y - z, c + x * s + y + z, c + x * s - y + z, x * s, col, col, col, col);
                g.QuadOut(c + y * s - x - z, c + y * s + x - z, c + y * s + x + z, c + y * s - x + z, y * s, col, col, col, col);
                g.QuadOut(c + z * s - x - y, c + z * s + x - y, c + z * s + x + y, c + z * s - x + y, z * s, col, col, col, col);
            }
        }

        /// <summary>Rectangular beam between two points (hw sideways, hh roughly vertical).</summary>
        public static void Beam(LifeGeo g, Vector3 a, Vector3 b, float hw, float hh, Color32 col)
        {
            const float k = 1.41421356f;
            g.Frustum(a, b, hw * k, hh * k, hw * k, hh * k, 4, Mathf.PI * 0.25f, col, col, true, true);
        }

        /// <summary>Thin open rod (no caps).</summary>
        public static void Rod(LifeGeo g, Vector3 a, Vector3 b, float r, Color32 col, int sides = 4)
        {
            g.Frustum(a, b, r, r, r, r, sides, 0.3f, col, col, false, false);
        }

        /// <summary>Round column / cylinder (capped top).</summary>
        public static void Column(LifeGeo g, Vector3 a, Vector3 b, float r, Color32 col, int sides = 6)
        {
            g.Frustum(a, b, r, r, r, r, sides, 0f, col, col, false, true);
        }

        /// <summary>Double sided quad (two faces, opposite normals).</summary>
        public static void Quad2(LifeGeo g, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (nrm.sqrMagnitude < 1e-12f) nrm = Vector3.Cross(c - a, d - a);
            g.QuadOut(a, b, c, d, nrm, ca, cb, cc, cd);
            g.QuadOut(a, b, c, d, -nrm, ca, cb, cc, cd);
        }

        static float Prof(float t) { return 0.5f * t + 0.5f * (2f * t - t * t); }

        /// <summary>
        /// Chinese hip roof with concave slopes, tile stripes, upturned corners and ridges. c = centre of the
        /// wall top, hx / hz = wall half sizes, over = eave overhang, curl = corner upturn (m).
        /// </summary>
        public static void Roof(LifeGeo g, Vector3 c, float hx, float hz, float rise, float over, float curl,
                                Color32 tileA, Color32 tileB, Color32 under, Color32 ridge, bool finial)
        {
            const int NT = 4, NS = 6, NP = NS * 4;
            float X = hx + over, Z = hz + over;
            float rx = Mathf.Max(0.02f, X - Z);
            float tw = Mathf.Clamp(hz / Z, 0.05f, 0.95f);
            float top = c.y + rise * Prof(tw);
            var P = new Vector3[NT + 1, NP];
            for (int i = 0; i <= NT; i++)
            {
                float t = i / (float)NT;
                float ax = Mathf.Lerp(rx, X, t), az = Mathf.Lerp(0.001f, Z, t);
                float y = top - rise * Prof(t);
                float tc = Mathf.Max(0f, (t - tw) / (1f - tw));
                for (int k = 0; k < NP; k++)
                {
                    int side = k / NS;
                    float f = (k % NS) / (float)NS;
                    Vector2 q = Vector2.Lerp(Corner(side, ax, az), Corner(side + 1, ax, az), f);
                    float corner = Mathf.Pow(Mathf.Abs(f * 2f - 1f), 3f);
                    P[i, k] = new Vector3(c.x + q.x, y + curl * corner * tc * tc, c.z + q.y);
                }
            }
            Vector3 drop = new Vector3(0f, -0.13f, 0f);
            Color32 fascia = Sh(under, 0.8f);
            for (int i = 0; i < NT; i++)
                for (int k = 0; k < NP; k++)
                {
                    int k1 = (k + 1) % NP;
                    Vector3 a = P[i, k], b = P[i, k1], d = P[i + 1, k1], e = P[i + 1, k];
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 hor = new Vector3(mid.x - c.x, 0f, mid.z - c.z);
                    Vector3 outward = Vector3.up * 0.7f + hor.normalized * 0.5f;
                    Color32 col = (k & 1) == 0 ? tileA : tileB;
                    if (i == NT - 1) col = Sh(col, 0.92f);
                    g.QuadOut(a, b, d, e, outward, col, col, col, col);
                    g.QuadOut(a + drop, b + drop, d + drop, e + drop, -outward, under, under, under, under);
                    if (i == NT - 1) g.QuadOut(e, d, d + drop, e + drop, hor, fascia, fascia, fascia, fascia);
                }
            // main ridge with upturned ends
            Vector3 r0 = new Vector3(c.x - rx, top + 0.08f, c.z), r1 = new Vector3(c.x + rx, top + 0.08f, c.z);
            if (rx > 0.3f)
            {
                Beam(g, r0, r1, 0.13f, 0.16f, ridge);
                Beam(g, r0, r0 + new Vector3(-0.25f, 0.42f, 0f), 0.09f, 0.12f, ridge);
                Beam(g, r1, r1 + new Vector3(0.25f, 0.42f, 0f), 0.09f, 0.12f, ridge);
            }
            // hip ridges following the curve to the curled corners
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < NT; i++)
                    Beam(g, P[i, j * NS] + Vector3.up * 0.07f, P[i + 1, j * NS] + Vector3.up * 0.07f, 0.07f, 0.08f, ridge);
            if (finial)
            {
                Vector3 f0 = new Vector3(c.x, top, c.z);
                g.Frustum(f0, f0 + Vector3.up * 0.45f, 0.22f, 0.22f, 0.08f, 0.08f, 6, 0f, Pal.GoldDark, Pal.Gold, true, false);
                g.Ellipsoid(f0 + Vector3.up * 0.62f, new Vector3(0.18f, 0.2f, 0.18f), 6, 4, Pal.Gold, Pal.GoldDark);
                g.Frustum(f0 + Vector3.up * 0.78f, f0 + Vector3.up * 1.15f, 0.06f, 0.06f, 0.01f, 0.01f, 4, 0f, Pal.Gold, Pal.Gold, false, false);
            }
        }

        static Vector2 Corner(int i, float ax, float az)
        {
            switch (i & 3)
            {
                case 0: return new Vector2(-ax, -az);
                case 1: return new Vector2(ax, -az);
                case 2: return new Vector2(ax, az);
                default: return new Vector2(-ax, az);
            }
        }

        /// <summary>Paper lantern hanging from 'top' (wind weight 'sway' on every vertex so it moves rigidly).</summary>
        public static void Lantern(LifeGeo g, Vector3 top, float r, float h, Color32 body, int sway, bool round = true)
        {
            Color32 gold = A(Pal.Gold, sway), dark = A(LifeGeo.C(52, 36, 30), sway), b = A(body, sway);
            Vector3 c = top - Vector3.up * (0.07f + h * 0.5f);
            g.Frustum(top, top - Vector3.up * 0.07f, r * 0.3f, r * 0.3f, r * 0.42f, r * 0.42f, 6, 0f, dark, gold, true, false);
            if (round) g.Ellipsoid(c, new Vector3(r, h * 0.5f, r), 7, 5, b, Sh(b, 0.82f));
            else g.Frustum(c + Vector3.up * h * 0.5f, c - Vector3.up * h * 0.5f, r * 0.8f, r * 0.8f, r * 0.8f, r * 0.8f, 6, 0f, b, Sh(b, 0.85f), true, true);
            Vector3 bt = c - Vector3.up * h * 0.5f;
            g.Frustum(bt, bt - Vector3.up * 0.06f, r * 0.42f, r * 0.42f, r * 0.3f, r * 0.3f, 6, 0f, gold, dark, false, true);
            g.Frustum(bt - Vector3.up * 0.06f, bt - Vector3.up * (0.06f + h * 0.55f), 0.035f, 0.035f, 0.07f, 0.07f, 4, 0f, A(Pal.RedDeep, sway), A(Pal.Red, sway), false, true);
        }

        /// <summary>Displace the vertices added since 'start' by a smooth noise along (v - ctr) and recompute flat normals.</summary>
        public static void Lumpy(LifeGeo g, int start, Vector3 ctr, float amount, float seed)
        {
            for (int i = start; i < g.v.Count; i++)
            {
                Vector3 p = g.v[i];
                Vector3 d = p - ctr;
                float nz = Mathf.Sin(p.x * 3.1f + seed) * Mathf.Sin(p.y * 2.7f + seed * 1.7f) * Mathf.Sin(p.z * 3.3f + seed * 0.6f);
                nz += 0.5f * Mathf.Sin(p.x * 7.3f - p.z * 5.9f + seed * 2.3f);
                g.v[i] = p + d * (nz * amount);
            }
            for (int i = start; i + 2 < g.v.Count; i += 3)
            {
                Vector3 nn = Vector3.Cross(g.v[i + 1] - g.v[i], g.v[i + 2] - g.v[i]).normalized;
                g.n[i] = nn; g.n[i + 1] = nn; g.n[i + 2] = nn;
            }
        }

        /// <summary>Lumpy boulder.</summary>
        public static void Boulder(LifeGeo g, Vector3 c, Vector3 r, Color32 top, Color32 bottom, float seed)
        {
            int s = g.v.Count;
            g.Ellipsoid(c, r, 7, 4, top, bottom);
            Lumpy(g, s, c, 0.22f, seed);
        }

        /// <summary>Flat petal / leaf blade from base towards tip with half width w (double sided).</summary>
        public static void Petal(LifeGeo g, Vector3 a, Vector3 tip, Vector3 side, float w, Color32 cBase, Color32 cTip)
        {
            Vector3 m = Vector3.Lerp(a, tip, 0.45f);
            Vector3 l = m - side * w, r = m + side * w;
            Color32 cm = Color32.Lerp(cBase, cTip, 0.5f);
            Vector3 nrm = Vector3.Cross(tip - a, side);
            if (nrm.y < 0f) nrm = -nrm;
            g.TriOut(a, l, r, nrm, cBase, cm, cm);
            g.TriOut(l, tip, r, nrm, cm, cTip, cm);
            g.TriOut(a, l, r, -nrm, cBase, cm, cm);
            g.TriOut(l, tip, r, -nrm, cm, cTip, cm);
        }
    }
}

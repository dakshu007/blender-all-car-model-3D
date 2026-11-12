// CityRegion.Harbour.Build.cs - the harbour of Jade Harbour City: sea and basin water, granite quay walls
// (with a walkable top and a railing that keeps cars out of the water), the cable-stayed Jade Bridge that
// carries Jade Avenue over the basin, the container terminal with ship-to-shore cranes, moored and sailing
// ships and junks (bobbing in Tick) and the lighthouse breakwater with its turning beam.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CityRegion
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        static readonly Vector3 Up = Vector3.up;

        // ------------------------------------------------------------------ shoreline
        /// <summary>Shore polyline (quay faces) with the direction towards the water at each point.</summary>
        static void ShorePath(List<Vector3> pts, List<Vector3> water)
        {
            System.Action<Vector2, Vector2, Vector2> line = (a, b, wn) =>
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)n);
                    pts.Add(new Vector3(p.x, 0f, p.y));
                    water.Add(new Vector3(wn.x, 0f, wn.y));
                }
            };
            System.Action<Vector2, float, float, float> arc = (c, r, a0, a1) =>
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r / 4f));
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)n);
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 p = c + d * r;
                    pts.Add(new Vector3(p.x, 0f, p.y));
                    water.Add(new Vector3(-d.x, 0f, -d.y));
                }
            };
            float q = CityPlan.QuayZ, x0 = CityPlan.BasinX0, x1 = CityPlan.BasinX1, top = CityPlan.BasinTop, r0 = CityPlan.BasinR;
            line(new Vector2(CityPlan.SeaW, q), new Vector2(x0, q), new Vector2(0f, -1f));
            line(new Vector2(x0, q), new Vector2(x0, top - r0), new Vector2(1f, 0f));
            arc(new Vector2(x0 + r0, top - r0), r0, Mathf.PI, Mathf.PI * 0.5f);
            line(new Vector2(x0 + r0, top), new Vector2(x1 - r0, top), new Vector2(0f, -1f));
            arc(new Vector2(x1 - r0, top - r0), r0, Mathf.PI * 0.5f, 0f);
            line(new Vector2(x1, top - r0), new Vector2(x1, q), new Vector2(-1f, 0f));
            line(new Vector2(x1, q), new Vector2(CityPlan.SeaE + 0.01f, q), new Vector2(0f, -1f));
            pts.Add(new Vector3(CityPlan.SeaE, 0f, q));
            water.Add(new Vector3(0f, 0f, -1f));
        }

        void BuildWater(RegionContext ctx)
        {
            var mat = new Material(MaterialLibrary.Water) { name = "BR_CityHarbourWater" };
            mat.SetColor("_Shallow", new Color(0.42f, 0.76f, 0.78f, 0.72f));
            mat.SetColor("_Deep", new Color(0.10f, 0.38f, 0.50f, 0.93f));
            var v = new List<Vector3>();
            var t = new List<int>();
            System.Action<float, float, float, float, float> rect = (xa, za, xb, zb, step) =>
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt((xb - xa) / step)), nz = Mathf.Max(1, Mathf.CeilToInt((zb - za) / step));
                int b = v.Count;
                for (int j = 0; j <= nz; j++)
                    for (int i = 0; i <= nx; i++)
                        v.Add(new Vector3(Mathf.Lerp(xa, xb, i / (float)nx), WorldField.WaterY, Mathf.Lerp(za, zb, j / (float)nz)));
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = b + j * (nx + 1) + i;
                        t.Add(a); t.Add(a + nx + 1); t.Add(a + 1);
                        t.Add(a + 1); t.Add(a + nx + 1); t.Add(a + nx + 2);
                    }
            };
            rect(540f, -840f, 1800f, -441f, 60f);
            rect(CityPlan.BasinX0 - 4f, -441f, CityPlan.BasinX1 + 4f, CityPlan.BasinTop + 4f, 30f);
            var m = new Mesh { name = "City_HarbourWater" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            var uv = new List<Vector2>();
            foreach (var p in v) uv.Add(new Vector2(p.x * 0.05f, p.z * 0.05f));
            m.SetUVs(0, uv);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            var go = ctx.AddMesh("City_HarbourWater", m, mat, 0, false, true);
            go.GetComponent<MeshRenderer>().reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            renderers++;
        }

        void BuildQuays(RegionContext ctx)
        {
            var pts = new List<Vector3>();
            var wn = new List<Vector3>();
            ShorePath(pts, wn);
            int n = pts.Count;
            var tops = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 inland = pts[i] - wn[i] * 10f;
                tops[i] = ctx.Ground(inland.x, inland.z) + 0.05f;
            }
            Color32 granite = C(150, 148, 142), wet = C(88, 100, 96), coping = C(206, 202, 194), fender = C(30, 30, 32);
            var colV = new List<Vector3>();
            var colT = new List<int>();
            const float bottom = -6.5f, depth = 8.2f;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 na = wn[i], nb = wn[i + 1];
                float ya = tops[i], yb = tops[i + 1];
                Vector3 ai = a - na * depth, bi = b - nb * depth;
                Vector3 fa = a + na * 0.02f, fb = b + nb * 0.02f;
                Vector3 nm = (na + nb).normalized;
                // face: wet band below the water line, granite above
                geo.Quad(PK.Landmark, new Vector3(fa.x, bottom, fa.z), new Vector3(fb.x, bottom, fb.z), new Vector3(fb.x, -0.7f, fb.z), new Vector3(fa.x, -0.7f, fa.z), nm, wet);
                geo.Quad(PK.Landmark, new Vector3(fa.x, -0.7f, fa.z), new Vector3(fb.x, -0.7f, fb.z), new Vector3(fb.x, yb - 0.35f, fb.z), new Vector3(fa.x, ya - 0.35f, fa.z), nm, Sh(granite, 0.86f), Sh(granite, 0.86f), granite, granite);
                // coping lip
                Vector3 la = a + na * 0.3f, lb = b + nb * 0.3f;
                geo.Quad(PK.Landmark, new Vector3(la.x, ya - 0.35f, la.z), new Vector3(lb.x, yb - 0.35f, lb.z), new Vector3(lb.x, yb + 0.02f, lb.z), new Vector3(la.x, ya + 0.02f, la.z), nm, coping);
                geo.Quad(PK.Landmark, new Vector3(fa.x, ya - 0.35f, fa.z), new Vector3(fb.x, yb - 0.35f, fb.z), new Vector3(lb.x, yb - 0.35f, lb.z), new Vector3(la.x, ya - 0.35f, la.z), Vector3.down, coping);
                // walkable top (paving) back to solid ground
                geo.Quad(PK.Landmark, new Vector3(ai.x, ya, ai.z), new Vector3(bi.x, yb, bi.z), new Vector3(lb.x, yb + 0.02f, lb.z), new Vector3(la.x, ya + 0.02f, la.z), Vector3.up, C(184, 182, 176), C(184, 182, 176), coping, coping);
                int c0 = colV.Count;
                colV.Add(new Vector3(ai.x, ya, ai.z)); colV.Add(new Vector3(bi.x, yb, bi.z)); colV.Add(new Vector3(lb.x, yb, lb.z)); colV.Add(new Vector3(la.x, ya, la.z));
                colT.Add(c0); colT.Add(c0 + 2); colT.Add(c0 + 1); colT.Add(c0); colT.Add(c0 + 3); colT.Add(c0 + 2);
                // fenders on the low quays
                if (i % 3 == 0 && ya < 6f)
                {
                    Vector3 f = a + na * 0.35f;
                    geo.Box(PK.Props, new Vector3(f.x, -0.2f, f.z), new Vector3(0.6f, 1.1f, 0.25f), Mathf.Atan2(na.x, na.z) * Mathf.Rad2Deg, fender);
                }
            }
            var cm = new Mesh { name = "City_QuayTopCollider" };
            cm.SetVertices(colV);
            cm.SetTriangles(colT, 0);
            cm.RecalculateNormals();
            cm.RecalculateBounds();
            ctx.MeshColliderFor("City_QuayTop", cm, Surface.Paving);

            // railing / balustrade along the edge (box colliders every ~10 m), mooring bollards on the low quays
            var balPost = new PropModel();
            PG.Box2(balPost[PK.Props], new Vector3(0f, 0.55f, 0f), new Vector3(0.18f, 0.55f, 0.18f), coping, C(226, 222, 214));
            balPost[PK.Props].Ellipsoid(new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 0.12f, 0.14f), 6, 3, CityKit.Gold, Sh(CityKit.Gold, 0.8f));
            var moor = new PropModel();
            moor[PK.Props].Frustum(Vector3.zero, Up * 0.55f, 0.28f, 0.28f, 0.22f, 0.22f, 8, 0f, C(40, 44, 48), C(60, 64, 70), false, true);
            moor[PK.Props].Ellipsoid(Up * 0.6f, new Vector3(0.32f, 0.12f, 0.32f), 8, 3, C(60, 64, 70), C(40, 44, 48));
            float acc = 0f;
            int runStart = 0;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = pts[i] - wn[i] * 0.55f, b = pts[i + 1] - wn[i + 1] * 0.55f;
                float ya = tops[i], yb = tops[i + 1];
                bool terminal = pts[i].x > CityPlan.BasinX1 - 1f && (pts[i].z < -340f);
                // two rails (beams) between posts
                if (!terminal)
                {
                    geo.Beam(PK.Props, new Vector3(a.x, ya + 1.0f, a.z), new Vector3(b.x, yb + 1.0f, b.z), 0.07f, 0.06f, coping);
                    geo.Beam(PK.Props, new Vector3(a.x, ya + 0.45f, a.z), new Vector3(b.x, yb + 0.45f, b.z), 0.04f, 0.04f, CityKit.Steel);
                    if (i % 1 == 0) ctx.Stamp(balPost, new Vector3(a.x, ya, a.z), 0f);
                }
                else
                {
                    // terminal: low concrete kerb with yellow paint and mooring bollards
                    geo.Beam(PK.Props, new Vector3(a.x, ya + 0.25f, a.z), new Vector3(b.x, yb + 0.25f, b.z), 0.3f, 0.25f, C(226, 196, 60));
                    if (i % 4 == 0) ctx.Stamp(moor, new Vector3(a.x, ya, a.z) - wn[i] * 1.2f, 0f);
                }
                if (!terminal && ya < 6f && i % 5 == 2) ctx.Stamp(moor, new Vector3(a.x, ya, a.z) - wn[i] * 1.6f, 0f);
                acc += Vector3.Distance(a, b);
                bool corner = i < n - 2 && Vector3.Dot(wn[i], wn[i + 1]) < 0.999f;
                if (acc >= 10f || i == n - 2 || corner)
                {
                    Vector3 s = pts[runStart] - wn[runStart] * 0.55f, e = b;
                    Vector3 mid = (s + e) * 0.5f;
                    float len = Mathf.Max(1f, Vector3.Distance(s, e));
                    Vector3 d = (e - s) / len;
                    float y = (tops[runStart] + yb) * 0.5f;
                    ctx.Box(new Vector3(mid.x, y + 0.6f, mid.z), new Vector3(0.5f, 1.5f + Mathf.Abs(tops[runStart] - yb), len + 0.4f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    acc = 0f;
                    runStart = i + 1;
                }
            }
            // rocky shore where the seafront ends in the west and under the east headland
            var rocks = new PropModel();
            var rr = new System.Random(77);
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f;
                Vector3 c = new Vector3(Mathf.Lerp(CityPlan.SeaW - 4f, CityPlan.SeaW - 110f, a), 0f, CityPlan.QuayZ - 4f - a * 95f);
                c.y = Mathf.Max(-2.5f, ctx.Ground(c.x, c.z)) - 0.6f;
                PG.Boulder(rocks[PK.Landmark], c - new Vector3(640f, 0f, -490f), new Vector3(CityKit.R(rr, 2f, 4f), CityKit.R(rr, 1.4f, 2.6f), CityKit.R(rr, 2f, 3.6f)), C(150, 146, 136), C(104, 104, 100), i * 1.7f);
            }
            ctx.Stamp(rocks, new Vector3(640f, 0f, -490f), 0f);
        }

        // ------------------------------------------------------------------ the Jade Bridge (cable-stayed)
        void BuildBridge(RegionContext ctx)
        {
            float y = CityPlan.Plat, z = CityPlan.BridgeZ, x0 = CityPlan.BridgeX0 - 8f, x1 = CityPlan.BridgeX1 + 8f, px = CityPlan.PylonX;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 white = C(238, 240, 236), under = C(150, 158, 160), jade = CityKit.Jade, cable = C(232, 240, 238);
            Vector3 o = new Vector3(px, 0f, z);
            System.Func<float, float, float, Vector3> L = (wx, wy, wz) => new Vector3(wx - o.x, wy, wz - o.z);
            // deck girder: box with a jade fascia and a glowing LED line
            PG.Box2(g, L((x0 + x1) * 0.5f, y - 1.3f, z), new Vector3((x1 - x0) * 0.5f, 1.25f, 7.8f), under, white);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, L((x0 + x1) * 0.5f, y - 1.0f, z + s * 7.85f), new Vector3((x1 - x0) * 0.5f, 0.55f, 0.08f), jade, jade);
                PG.Box2(gl, L((x0 + x1) * 0.5f, y - 1.65f, z + s * 7.9f), new Vector3((x1 - x0) * 0.5f, 0.07f, 0.05f), C(140, 255, 220), C(140, 255, 220));
                // parapet with posts and a handrail
                PG.Box2(g, L((x0 + x1) * 0.5f, y + 0.5f, z + s * 7.0f), new Vector3((x1 - x0) * 0.5f, 0.5f, 0.3f), white, C(220, 222, 220));
                for (float x = x0 + 2f; x < x1; x += 4f) PG.Box2(g, L(x, y + 1.2f, z + s * 7.0f), new Vector3(0.08f, 0.25f, 0.08f), CityKit.Steel, CityKit.Steel);
                PG.Beam(g, L(x0, y + 1.45f, z + s * 7.0f), L(x1, y + 1.45f, z + s * 7.0f), 0.08f, 0.06f, CityKit.SteelLight);
                for (int k = 0; k < 4; k++)
                {
                    float xa = x0 + (x1 - x0) * k / 4f, xb = x0 + (x1 - x0) * (k + 1) / 4f;
                    ctx.Box(new Vector3((xa + xb) * 0.5f, y + 0.8f, z + s * 7.0f), new Vector3(0.7f, 1.8f, xb - xa), 90f);
                }
            }
            // inverted-Y pylon: legs from the footing straddle the deck and meet above it, then one mast
            float apex = y + 50f, top = y + 96f;
            PG.Box2(g, L(px, -1.5f, z), new Vector3(9f, 2.2f, 16f), C(170, 168, 160), C(196, 194, 186));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = L(px, 0.5f, z + s * 12.5f), knee = L(px, y - 3f, z + s * 10.2f), head = L(px, apex, z + s * 1.6f);
                PG.Beam(g, foot, knee, 1.4f, 1.6f, white);
                PG.Beam(g, knee, head, 1.25f, 1.4f, white);
                PG.Beam(gl, knee + new Vector3(1.45f, 0f, 0f), head + new Vector3(1.3f, 0f, 0f), 0.1f, 0.1f, C(160, 255, 220));
                ctx.Box(new Vector3(px, (y + 6f) * 0.5f, z + s * 10.6f), new Vector3(3f, y + 6f, 3.6f), 0f);
            }
            PG.Beam(g, L(px, y - 3f, z - 10.2f), L(px, y - 3f, z + 10.2f), 1.0f, 1.2f, white);
            PG.Beam(g, L(px, apex, z), L(px, top, z), 1.5f, 1.8f, white);
            // jade band and a lantern crown on the mast
            g.Frustum(L(px, top - 22f, z), L(px, top - 18f, z), 2.2f, 2.5f, 2.2f, 2.5f, 4, Mathf.PI * 0.25f, jade, jade, true, true);
            Vector3 lc = L(px, top + 6f, z);
            g.Frustum(L(px, top, z), L(px, top + 1.2f, z), 2.6f, 2.6f, 2.2f, 2.2f, 8, 0f, CityKit.Gold, CityKit.Gold, true, true);
            gl.Ellipsoid(lc, new Vector3(3.2f, 4.6f, 3.2f), 10, 6, C(255, 90, 60), C(220, 50, 40));
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PG.Beam(g, lc + d * 1.2f + Up * 4.5f, lc + d * 3.3f, 0.09f, 0.09f, CityKit.Gold);
                PG.Beam(g, lc + d * 3.3f, lc + d * 1.2f - Up * 4.5f, 0.09f, 0.09f, CityKit.Gold);
            }
            g.Frustum(lc + Up * 4.4f, lc + Up * 5.6f, 1.6f, 1.6f, 0.9f, 0.9f, 8, 0f, CityKit.Gold, CityKit.Gold, true, true);
            g.Frustum(lc - Up * 4.4f, lc - Up * 5.4f, 1.6f, 1.6f, 0.9f, 0.9f, 8, 0f, CityKit.Gold, CityKit.Gold, true, true);
            g.Frustum(lc - Up * 5.4f, lc - Up * 9f, 0.25f, 0.25f, 0.5f, 0.5f, 6, 0f, C(220, 40, 40), C(240, 70, 50), false, true);
            neon.AddBeacon(new Vector3(px, top + 12f, z));
            g.Frustum(L(px, top + 11.4f, z), L(px, top + 12f, z), 0.3f, 0.3f, 0.1f, 0.1f, 6, 0f, CityKit.Gold, CityKit.Gold, false, true);
            // fan of stay cables to both deck edges (they glow softly at night)
            for (int k = 0; k < 11; k++)
            {
                float ay = apex + 4f + k * 3.6f;
                float dx = 14f + k * 8.4f;
                for (int dir = -1; dir <= 1; dir += 2)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 a = L(px, ay, z + s * 0.9f), b = L(px + dir * dx, y + 0.2f, z + s * 6.5f);
                        PG.Rod(gl, a, b, 0.11f, cable, 4);
                    }
            }
            // abutment portals with lanterns
            foreach (float ex in new[] { CityPlan.BridgeX0 - 4f, CityPlan.BridgeX1 + 4f })
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 b = L(ex, y, z + s * 8.6f);
                    PG.Box2(g, b + Up * 3.2f, new Vector3(1.1f, 3.2f, 1.1f), white, jade);
                    PG.Box2(g, b + Up * 6.6f, new Vector3(1.4f, 0.2f, 1.4f), CityKit.Gold, CityKit.Gold);
                    PG.Lantern(gl, b + Up * 8.0f, 0.7f, 1.1f, C(232, 58, 46), 0);
                    g.Frustum(b + Up * 6.8f, b + Up * 8.0f, 0.08f, 0.08f, 0.08f, 0.08f, 4, 0f, CityKit.Gold, CityKit.Gold, false, false);
                    ctx.Box(new Vector3(ex, y + 3.2f, z + s * 8.6f), new Vector3(2.4f, 6.4f, 2.4f), 0f);
                }
            ctx.StampSkyline(m, o, 0f);
        }

        // ------------------------------------------------------------------ container terminal
        static PropModel Crane(Color32 body, Color32 accent)
        {
            // local: +z towards the water, legs at x = +-8 (along the quay), z = 0 (water side) and -18 (land side)
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            float H = 32f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                foreach (float lz in new[] { 0f, -18f })
                {
                    PG.Box2(g, new Vector3(sx * 8f, 0.8f, lz), new Vector3(1.3f, 0.8f, 2.4f), CityKit.SteelDark, CityKit.Steel);
                    PG.Beam(g, new Vector3(sx * 8f, 1.4f, lz), new Vector3(sx * 7.4f, H, lz), 0.7f, 0.7f, body);
                }
                PG.Beam(g, new Vector3(sx * 7.4f, H, 0f), new Vector3(sx * 7.4f, H, -18f), 0.6f, 0.8f, body);
                PG.Beam(g, new Vector3(sx * 7.8f, 12f, 0f), new Vector3(sx * 7.8f, 12f, -18f), 0.4f, 0.5f, body);
                PG.Beam(g, new Vector3(sx * 7.7f, 13f, -1f), new Vector3(sx * 7.5f, H - 1f, -17f), 0.25f, 0.25f, body);
                // boom girders
                PG.Beam(g, new Vector3(sx * 1.8f, H + 4f, -24f), new Vector3(sx * 1.8f, H + 4f, 44f), 0.5f, 1.0f, body);
                // A-frame and tie bars
                PG.Beam(g, new Vector3(sx * 7.4f, H, -2f), new Vector3(sx * 2f, H + 20f, -6f), 0.5f, 0.5f, body);
                PG.Beam(g, new Vector3(sx * 7.4f, H, -16f), new Vector3(sx * 2f, H + 20f, -6f), 0.5f, 0.5f, body);
                PG.Beam(g, new Vector3(sx * 2f, H + 20f, -6f), new Vector3(sx * 1.8f, H + 5f, 42f), 0.12f, 0.12f, CityKit.Steel);
                PG.Beam(g, new Vector3(sx * 2f, H + 20f, -6f), new Vector3(sx * 1.8f, H + 5f, -22f), 0.12f, 0.12f, CityKit.Steel);
            }
            PG.Beam(g, new Vector3(-7.4f, H, 0f), new Vector3(7.4f, H, 0f), 0.7f, 0.9f, accent);
            PG.Beam(g, new Vector3(-7.4f, H, -18f), new Vector3(7.4f, H, -18f), 0.7f, 0.9f, accent);
            PG.Beam(g, new Vector3(-2f, H + 20f, -6f), new Vector3(2f, H + 20f, -6f), 0.4f, 0.4f, accent);
            for (float zz = -20f; zz <= 40f; zz += 6f) PG.Beam(g, new Vector3(-1.8f, H + 3.2f, zz), new Vector3(1.8f, H + 3.2f, zz), 0.15f, 0.15f, body);
            // machinery house, operator cab, trolley and spreader
            PG.Box2(g, new Vector3(0f, H + 6.5f, -15f), new Vector3(4.2f, 2.6f, 5f), C(236, 236, 230), C(210, 210, 206));
            PG.Box2(g, new Vector3(0f, H + 1.6f, 14f), new Vector3(1.6f, 1.4f, 1.8f), C(236, 236, 230), C(210, 210, 206));
            PG.Box2(gl, new Vector3(0f, H + 1.9f, 15.85f), new Vector3(1.3f, 0.6f, 0.05f), C(255, 226, 170), C(255, 226, 170));
            PG.Box2(g, new Vector3(0f, H + 3.2f, 26f), new Vector3(2.2f, 0.8f, 2.6f), accent, accent);
            for (int s = -1; s <= 1; s += 2) PG.Rod(g, new Vector3(s * 1.2f, H + 2.5f, 26f), new Vector3(s * 1.2f, H - 12f, 26f), 0.05f, CityKit.SteelDark, 3);
            PG.Box2(g, new Vector3(0f, H - 12.4f, 26f), new Vector3(1.4f, 0.4f, 6.1f), C(226, 196, 60), C(226, 196, 60));
            // warning lights
            gl.Ellipsoid(new Vector3(0f, H + 20.6f, -6f), new Vector3(0.4f, 0.4f, 0.4f), 6, 3, C(255, 60, 40), C(255, 60, 40));
            gl.Ellipsoid(new Vector3(0f, H + 5.2f, 44f), new Vector3(0.35f, 0.35f, 0.35f), 6, 3, C(255, 60, 40), C(255, 60, 40));
            return m;
        }

        void BuildTerminal(RegionContext ctx)
        {
            var r = new System.Random(4401);
            var craneA = Crane(C(46, 140, 120), C(232, 196, 64));
            var craneB = Crane(C(214, 66, 52), C(240, 240, 236));
            // cranes: two on the basin side (facing west), two on the sea quay (facing south)
            Vector2[] pos = { new Vector2(CityPlan.BasinX1 + 9f, -378f), new Vector2(CityPlan.BasinX1 + 9f, -420f), new Vector2(1372f, CityPlan.QuayZ + 9f), new Vector2(1428f, CityPlan.QuayZ + 9f) };
            float[] yaws = { -90f, -90f, 180f, 180f };
            for (int i = 0; i < pos.Length; i++)
            {
                float gy = ctx.Ground(pos[i].x, pos[i].y);
                ctx.StampSkyline(i % 2 == 0 ? craneA : craneB, new Vector3(pos[i].x, gy, pos[i].y), yaws[i]);
                var q = Quaternion.Euler(0f, yaws[i], 0f);
                for (int sx = -1; sx <= 1; sx += 2)
                    foreach (float lz in new[] { 0f, -18f })
                    {
                        Vector3 lp = new Vector3(pos[i].x, gy, pos[i].y) + q * new Vector3(sx * 8f, 0f, lz);
                        ctx.Box(lp + Up * 6f, new Vector3(2.6f, 12f, 4.8f), yaws[i]);
                    }
            }
            // crane rails along both quays
            Color32 rail = C(70, 72, 76);
            geo.Box(PK.Props, new Vector3(CityPlan.BasinX1 + 9f, 2.55f, -395f), new Vector3(0.25f, 0.05f, 52f), 0f, rail);
            geo.Box(PK.Props, new Vector3(CityPlan.BasinX1 + 27f, 2.55f, -395f), new Vector3(0.25f, 0.05f, 52f), 0f, rail);
            geo.Box(PK.Props, new Vector3(1400f, 2.55f, CityPlan.QuayZ + 9f), new Vector3(52f, 0.05f, 0.25f), 0f, rail);
            geo.Box(PK.Props, new Vector3(1400f, 2.55f, CityPlan.QuayZ + 27f), new Vector3(52f, 0.05f, 0.25f), 0f, rail);
            // container stacks (rows of 40 ft boxes), one collider per row
            Color32[] boxes = { C(196, 54, 44), C(46, 96, 168), C(52, 150, 124), C(236, 148, 52), C(236, 236, 230), C(232, 200, 70), C(120, 128, 136), C(150, 70, 110) };
            var ribs = new System.Random(9);
            System.Action<float, float, int, int, bool> field = (fx, fz, rows, cols, alongZ) =>
            {
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        float cx = alongZ ? fx + i * 2.9f : fx + j * 12.8f, cz = alongZ ? fz + j * 12.8f : fz + i * 2.9f;
                        int tiers = 1 + ribs.Next(4);
                        float gy = ctx.Ground(cx, cz);
                        for (int t = 0; t < tiers; t++)
                        {
                            Color32 col = boxes[ribs.Next(boxes.Length)];
                            Vector3 c = new Vector3(cx, gy + 1.3f + t * 2.62f, cz);
                            Vector3 h = alongZ ? new Vector3(1.22f, 1.3f, 6.06f) : new Vector3(6.06f, 1.3f, 1.22f);
                            geo.Box(PK.Landmark, c, h, 0f, col);
                            // corrugation ribs on the long sides
                            Vector3 rib = alongZ ? new Vector3(1.24f, 1.18f, 0.06f) : new Vector3(0.06f, 1.18f, 1.24f);
                            for (int k = -2; k <= 2; k += 2) geo.Box(PK.Props, c + (alongZ ? new Vector3(0f, 0f, k * 2.2f) : new Vector3(k * 2.2f, 0f, 0f)), rib, 0f, Sh(col, 0.85f));
                        }
                        if (j == cols - 1 || true)
                        {
                            // collider per stack (cheap: only the first row of each column)
                        }
                    }
                // one collider for the whole field block
                float lx = alongZ ? rows * 2.9f : cols * 12.8f, lz = alongZ ? cols * 12.8f : rows * 2.9f;
                float ccx = fx + (alongZ ? (rows - 1) * 2.9f : (cols - 1) * 12.8f) * 0.5f, ccz = fz + (alongZ ? (cols - 1) * 12.8f : (rows - 1) * 2.9f) * 0.5f;
                ctx.Box(new Vector3(ccx, ctx.Ground(ccx, ccz) + 3f, ccz), new Vector3(lx, 6f, lz), 0f);
            };
            field(1352f, -428f, 6, 5, true);
            field(1414f, -436f, 4, 3, false);
            field(1428f, -392f, 5, 3, true);
            // reefer stacks light poles
            var mast = new PropModel();
            mast[PK.Props].Frustum(Vector3.zero, Up * 24f, 0.35f, 0.35f, 0.2f, 0.2f, 6, 0f, CityKit.Steel, CityKit.SteelLight, false, true);
            PG.Box2(mast[PK.Props], Up * 24.4f, new Vector3(1.6f, 0.3f, 0.5f), CityKit.SteelDark, CityKit.SteelDark);
            PG.Box2(mast[PK.Glow], Up * 24.05f, new Vector3(1.4f, 0.06f, 0.4f), CityKit.LampCool, CityKit.LampCool);
            foreach (var p in new[] { new Vector2(1392f, -440f), new Vector2(1445f, -350f), new Vector2(1342f, -350f) })
            {
                ctx.Stamp(mast, new Vector3(p.x, ctx.Ground(p.x, p.y), p.y), 0f);
                ctx.Capsule(new Vector3(p.x, ctx.Ground(p.x, p.y), p.y), 0.4f, 24f);
            }
        }

        // ------------------------------------------------------------------ ships (separate objects: they bob)
        struct Boat
        {
            public Transform t;
            public Vector3 basePos;
            public float yaw, phase, amp, roll;
            public bool sailing;
            public Vector2 c, rad;
            public float speed, ang;
        }
        readonly List<Boat> boats = new List<Boat>();
        Material shipMat, shipGlowMat;

        void AddBoat(RegionContext ctx, PropModel model, string name, Vector3 pos, float yaw, float amp, float roll, bool sailing = false, Vector2 c = default(Vector2), Vector2 rad = default(Vector2), float speed = 0f, float ang = 0f)
        {
            var body = new LifeGeo();
            var lights = new LifeGeo();
            for (int k = 0; k < PropModel.Kinds; k++)
            {
                var src = model.geo[k];
                if (src == null || src.v.Count == 0) continue;
                PG.Append(k == (int)PK.Glow ? lights : body, src, Matrix4x4.identity);
            }
            var go = ctx.AddMesh(name, body.ToMesh(name), shipMat, 0, true);
            renderers++;
            if (lights.v.Count > 0)
            {
                var lg = ctx.AddMesh(name + "_Lights", lights.ToMesh(name + "_Lights"), shipGlowMat, 0, false, false, go.transform);
                renderers++;
            }
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            boats.Add(new Boat { t = go.transform, basePos = pos, yaw = yaw, phase = (float)rng.NextDouble() * 6.28f, amp = amp, roll = roll, sailing = sailing, c = c, rad = rad, speed = speed, ang = ang });
        }

        static PropModel ContainerShip(System.Random r)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            Color32 hullTop = C(36, 70, 96), hullBot = C(170, 52, 44), deck = C(120, 124, 120), white = C(240, 240, 236);
            float L = 72f, B = 12f;
            // hull: box with a pointed bow (local +z = bow)
            PG.Box2(g, new Vector3(0f, -1.5f, -6f), new Vector3(B, 2.5f, L - 6f), hullBot, hullBot);
            PG.Box2(g, new Vector3(0f, 3.5f, -6f), new Vector3(B, 2.5f, L - 6f), hullTop, deck);
            Vector3 bowTip = new Vector3(0f, 6f, L + 8f);
            for (int s = -1; s <= 1; s += 2)
            {
                g.QuadOut(new Vector3(s * B, -4f, L - 12f), new Vector3(0f, -4f, L + 2f), bowTip, new Vector3(s * B, 6f, L - 12f), new Vector3(s, 0f, 0.6f), hullTop, hullBot, hullTop, hullTop);
            }
            g.TriOut(new Vector3(-B, 6f, L - 12f), new Vector3(B, 6f, L - 12f), bowTip, Up, deck, deck, deck);
            // stern accommodation block with glowing windows and funnel
            PG.Box2(g, new Vector3(0f, 12f, -L + 8f), new Vector3(B - 1f, 6f, 6f), white, white);
            PG.Box2(g, new Vector3(0f, 19.5f, -L + 8f), new Vector3(B + 1.5f, 1.5f, 3f), white, white);
            for (int f = 0; f < 5; f++)
                gl.QuadOut(new Vector3(-B + 1.5f, 7.6f + f * 2.4f, -L + 14.05f), new Vector3(B - 1.5f, 7.6f + f * 2.4f, -L + 14.05f), new Vector3(B - 1.5f, 8.6f + f * 2.4f, -L + 14.05f), new Vector3(-B + 1.5f, 8.6f + f * 2.4f, -L + 14.05f), Vector3.forward, C(255, 220, 160), C(255, 220, 160), C(255, 220, 160), C(255, 220, 160));
            g.Frustum(new Vector3(0f, 18f, -L + 3f), new Vector3(0f, 26f, -L + 2f), 2.2f, 3f, 2f, 2.8f, 6, 0f, CityKit.Jade, CityKit.JadeDeep, false, true);
            // container bays
            Color32[] boxes = { C(196, 54, 44), C(46, 96, 168), C(52, 150, 124), C(236, 148, 52), C(236, 236, 230), C(232, 200, 70) };
            for (float zz = -L + 20f; zz < L - 14f; zz += 13.2f)
                for (int col = -3; col <= 3; col++)
                {
                    int tiers = 2 + r.Next(3);
                    for (int t = 0; t < tiers; t++)
                        PG.Box2(g, new Vector3(col * 2.95f, 7.3f + t * 2.62f, zz), new Vector3(1.4f, 1.3f, 6.1f), boxes[r.Next(boxes.Length)], Sh(boxes[r.Next(boxes.Length)], 1.05f));
                }
            gl.Ellipsoid(new Vector3(0f, 27f, -L + 8f), new Vector3(0.4f, 0.4f, 0.4f), 6, 3, C(255, 240, 200), C(255, 240, 200));
            gl.Ellipsoid(bowTip + Up * 1f, new Vector3(0.4f, 0.4f, 0.4f), 6, 3, C(255, 240, 200), C(255, 240, 200));
            return m;
        }

        static PropModel Ferry()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            Color32 white = C(242, 242, 238), jade = CityKit.Jade, dark = C(40, 50, 60);
            float L = 30f, B = 7f;
            PG.Box2(g, new Vector3(0f, -0.5f, 0f), new Vector3(B, 2f, L), dark, white);
            PG.Box2(g, new Vector3(0f, 1.6f, 0f), new Vector3(B * 1.02f, 0.5f, L * 0.98f), jade, white);
            g.QuadOut(new Vector3(-B, 2.1f, L), new Vector3(B, 2.1f, L), new Vector3(0f, 2.1f, L + 9f), new Vector3(0f, 2.1f, L + 9f), Up, white, white, white, white);
            for (int s = -1; s <= 1; s += 2) g.QuadOut(new Vector3(s * B, -2.5f, L), new Vector3(0f, -2.5f, L + 7f), new Vector3(0f, 2.1f, L + 9f), new Vector3(s * B, 2.1f, L), new Vector3(s, 0f, 0.7f), dark, dark, white, white);
            for (int d = 0; d < 2; d++)
            {
                float hw = B - 0.6f - d * 1.2f, hl = L - 6f - d * 8f, y0 = 2.1f + d * 3f;
                PG.Box2(g, new Vector3(0f, y0 + 1.5f, -2f), new Vector3(hw, 1.5f, hl), white, C(220, 226, 228));
                for (int s = -1; s <= 1; s += 2)
                    gl.QuadOut(new Vector3(s * (hw + 0.05f), y0 + 0.8f, -2f - hl + 1f), new Vector3(s * (hw + 0.05f), y0 + 0.8f, -2f + hl - 1f), new Vector3(s * (hw + 0.05f), y0 + 2.2f, -2f + hl - 1f), new Vector3(s * (hw + 0.05f), y0 + 2.2f, -2f - hl + 1f), new Vector3(s, 0f, 0f), C(150, 220, 230), C(150, 220, 230), C(255, 230, 180), C(255, 230, 180));
            }
            PG.Box2(g, new Vector3(0f, 9.4f, 6f), new Vector3(3f, 1.2f, 2.5f), white, white);
            gl.QuadOut(new Vector3(-2.8f, 9.2f, 8.55f), new Vector3(2.8f, 9.2f, 8.55f), new Vector3(2.8f, 10.2f, 8.55f), new Vector3(-2.8f, 10.2f, 8.55f), Vector3.forward, C(150, 220, 230), C(150, 220, 230), C(150, 220, 230), C(150, 220, 230));
            g.Frustum(new Vector3(0f, 8f, -10f), new Vector3(0f, 12f, -11f), 1.4f, 1.8f, 1.2f, 1.6f, 6, 0f, jade, CityKit.Gold, false, true);
            return m;
        }

        static PropModel Junk(System.Random r, Color32 sail)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            Color32 wood = C(120, 78, 48), woodD = C(80, 52, 36), trim = C(196, 60, 44);
            float L = 9f;
            // hull: tapering sections, raised stern (local +z = bow)
            float[] zs = { -L, -L * 0.5f, 0f, L * 0.55f, L };
            float[] ws = { 2.2f, 2.8f, 3.0f, 2.4f, 0.5f };
            float[] ts = { 3.0f, 1.9f, 1.6f, 1.8f, 2.4f };
            for (int i = 0; i < zs.Length - 1; i++)
            {
                g.Frustum(new Vector3(0f, -0.6f + ts[i] * 0.3f, zs[i]), new Vector3(0f, -0.6f + ts[i + 1] * 0.3f, zs[i + 1]), ws[i], ts[i] * 0.7f, ws[i + 1], ts[i + 1] * 0.7f, 6, Mathf.PI / 6f, wood, woodD, i == 0, i == zs.Length - 2);
            }
            PG.Box2(g, new Vector3(0f, 1.35f, 0f), new Vector3(2.6f, 0.08f, L * 0.8f), C(160, 118, 76), C(170, 128, 84));
            PG.Box2(g, new Vector3(0f, 2.4f, -L + 2.2f), new Vector3(2.0f, 1.0f, 2.0f), woodD, trim);
            PG.Box2(g, new Vector3(0f, 3.5f, -L + 1.8f), new Vector3(2.2f, 0.1f, 2.4f), trim, trim);
            PG.Lantern(gl, new Vector3(1.6f, 3.4f, -L + 0.2f), 0.35f, 0.55f, C(240, 70, 50), 0);
            PG.Lantern(gl, new Vector3(-1.6f, 3.4f, -L + 0.2f), 0.35f, 0.55f, C(240, 70, 50), 0);
            gl.QuadOut(new Vector3(-1.2f, 2.0f, -L + 4.21f), new Vector3(1.2f, 2.0f, -L + 4.21f), new Vector3(1.2f, 2.8f, -L + 4.21f), new Vector3(-1.2f, 2.8f, -L + 4.21f), Vector3.forward, C(255, 210, 140), C(255, 210, 140), C(255, 210, 140), C(255, 210, 140));
            // masts and battened sails (sail vertices carry wind weight in alpha)
            float[] mz = { -2.5f, 2.6f, 6.4f };
            float[] mh = { 11f, 14f, 8f };
            for (int k = 0; k < 3; k++)
            {
                float h = mh[k];
                g.Frustum(new Vector3(0f, 1.4f, mz[k]), new Vector3(0f, 1.4f + h, mz[k]), 0.14f, 0.14f, 0.08f, 0.08f, 5, 0f, woodD, wood, false, true);
                float w = h * 0.45f, y0 = 2.6f, y1 = 1.4f + h - 0.4f;
                int bat = 5;
                for (int b = 0; b < bat; b++)
                {
                    float t0 = b / (float)bat, t1 = (b + 1) / (float)bat;
                    float ya = Mathf.Lerp(y0, y1, t0), yb = Mathf.Lerp(y0, y1, t1);
                    float wa = Mathf.Lerp(w, w * 0.62f, t0), wb = Mathf.Lerp(w, w * 0.62f, t1);
                    float fa = 0.25f + 0.2f * Mathf.Sin(t0 * Mathf.PI), fb = 0.25f + 0.2f * Mathf.Sin(t1 * Mathf.PI);
                    Color32 sa = PG.A(Sh(sail, 0.92f + 0.12f * (b % 2)), (int)(40 + 120 * t0)), sb = PG.A(Sh(sail, 0.92f + 0.12f * (b % 2)), (int)(40 + 120 * t1));
                    Vector3 a0 = new Vector3(fa, ya, mz[k] - wa * 0.35f), a1 = new Vector3(fa, ya, mz[k] + wa * 0.65f);
                    Vector3 b0 = new Vector3(fb, yb, mz[k] - wb * 0.35f), b1 = new Vector3(fb, yb, mz[k] + wb * 0.65f);
                    PG.Quad2(g, a0, a1, b1, b0, sa, sa, sb, sb);
                    PG.Beam(g, a0 + Vector3.right * 0.05f, a1 + Vector3.right * 0.05f, 0.04f, 0.04f, PG.A(woodD, (int)(40 + 120 * t0)));
                }
            }
            return m;
        }

        static PropModel Sampan(Color32 hood)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 wood = C(130, 90, 58);
            g.Frustum(new Vector3(0f, 0.1f, -3.2f), new Vector3(0f, 0.1f, 3.2f), 1.1f, 0.6f, 0.3f, 0.5f, 6, Mathf.PI / 6f, wood, Sh(wood, 0.8f), true, true);
            g.Frustum(new Vector3(0f, 0.5f, -1.2f), new Vector3(0f, 0.5f, 1.2f), 1.0f, 1.1f, 1.0f, 1.1f, 8, 0f, hood, Sh(hood, 0.85f), false, false);
            return m;
        }

        static PropModel Tug()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            Color32 red = C(200, 54, 44), black = C(36, 36, 40), white = C(240, 240, 236);
            PG.Box2(g, new Vector3(0f, 0f, 0f), new Vector3(3.6f, 1.6f, 9f), black, red);
            g.Frustum(new Vector3(0f, 0f, 9f), new Vector3(0f, 0.6f, 12f), 3.6f, 1.6f, 0.6f, 1f, 4, Mathf.PI * 0.25f, black, red, false, true);
            PG.Box2(g, new Vector3(0f, 2.8f, 1f), new Vector3(2.4f, 1.3f, 3.2f), white, white);
            PG.Box2(g, new Vector3(0f, 4.6f, 2f), new Vector3(1.8f, 0.6f, 1.8f), white, red);
            gl.QuadOut(new Vector3(-1.7f, 4.4f, 3.85f), new Vector3(1.7f, 4.4f, 3.85f), new Vector3(1.7f, 4.9f, 3.85f), new Vector3(-1.7f, 4.9f, 3.85f), Vector3.forward, C(255, 220, 160), C(255, 220, 160), C(255, 220, 160), C(255, 220, 160));
            g.Frustum(new Vector3(0f, 4f, -1.5f), new Vector3(0f, 7f, -1.8f), 0.6f, 0.6f, 0.5f, 0.5f, 6, 0f, red, black, false, true);
            for (int i = -2; i <= 2; i++) PG.Box2(g, new Vector3(3.7f, 0.6f, i * 3f), new Vector3(0.2f, 0.5f, 1.1f), black, black);
            return m;
        }

        void BuildShips(RegionContext ctx)
        {
            shipMat = new Material(MaterialLibrary.VertexColorWind) { name = "BR_CityShips" };
            shipMat.SetFloat("_WindStrength", 0.22f);
            shipGlowMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_CityShipLights" };
            shipGlowMat.SetFloat("_WindStrength", 0f);
            float wy = WorldField.WaterY;
            var r = new System.Random(3131);
            AddBoat(ctx, ContainerShip(r), "City_ContainerShip", new Vector3(1392f, wy - 0.4f, CityPlan.QuayZ - 15f), 90f, 0.07f, 0.25f);
            AddBoat(ctx, Ferry(), "City_Ferry", new Vector3(CityPlan.FerryPier.x, wy - 0.2f, CityPlan.QuayZ - 10f), -90f, 0.12f, 0.6f);
            Color32[] sails = { C(196, 70, 48), C(214, 128, 60), C(176, 52, 60), C(222, 170, 90) };
            // moored junks in the basin and sampans
            AddBoat(ctx, Junk(r, sails[0]), "City_Junk0", new Vector3(CityPlan.BasinX0 + 12f, wy - 0.1f, -300f), 180f, 0.15f, 1.6f);
            AddBoat(ctx, Junk(r, sails[1]), "City_Junk1", new Vector3(CityPlan.BasinX0 + 14f, wy - 0.1f, -262f), 4f, 0.15f, 1.6f);
            AddBoat(ctx, Junk(r, sails[2]), "City_Junk2", new Vector3(1236f, wy - 0.1f, -205f), 140f, 0.15f, 1.6f);
            AddBoat(ctx, Sampan(C(150, 110, 70)), "City_Sampan0", new Vector3(CityPlan.BasinX0 + 8f, wy, -350f), 170f, 0.12f, 3f);
            AddBoat(ctx, Sampan(C(60, 100, 120)), "City_Sampan1", new Vector3(CityPlan.BasinX0 + 9f, wy, -338f), 8f, 0.12f, 3f);
            AddBoat(ctx, Tug(), "City_Tug", new Vector3(CityPlan.BasinX1 - 12f, wy - 0.2f, -300f), 0f, 0.1f, 1f);
            // junks sailing slowly across the outer harbour
            AddBoat(ctx, Junk(r, sails[3]), "City_JunkSail0", Vector3.zero, 0f, 0.2f, 2f, true, new Vector2(1010f, -600f), new Vector2(230f, 62f), 2.6f, 0.3f);
            AddBoat(ctx, Junk(r, sails[0]), "City_JunkSail1", Vector3.zero, 0f, 0.2f, 2f, true, new Vector2(1180f, -640f), new Vector2(260f, 70f), -2.2f, 2.8f);
        }

        void TickBoats(float t, float dt)
        {
            float wy = WorldField.WaterY;
            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                if (b.t == null) continue;
                float bob = Mathf.Sin(t * 0.9f + b.phase) * b.amp;
                float roll = Mathf.Sin(t * 0.7f + b.phase * 1.3f) * b.roll;
                float pitch = Mathf.Sin(t * 0.55f + b.phase * 0.7f) * b.roll * 0.4f;
                if (b.sailing)
                {
                    b.ang += dt * b.speed / Mathf.Max(30f, (b.rad.x + b.rad.y) * 0.5f);
                    boats[i] = b;
                    Vector3 p = new Vector3(b.c.x + Mathf.Cos(b.ang) * b.rad.x, wy - 0.1f + bob, b.c.y + Mathf.Sin(b.ang) * b.rad.y);
                    Vector2 tan = new Vector2(-Mathf.Sin(b.ang) * b.rad.x, Mathf.Cos(b.ang) * b.rad.y) * Mathf.Sign(b.speed);
                    float yaw = Mathf.Atan2(tan.x, tan.y) * Mathf.Rad2Deg;
                    b.t.SetPositionAndRotation(p, Quaternion.Euler(pitch, yaw, roll + 4f * Mathf.Sign(b.speed)));
                }
                else b.t.SetPositionAndRotation(b.basePos + new Vector3(0f, bob, 0f), Quaternion.Euler(pitch, b.yaw, roll));
            }
        }

        // ------------------------------------------------------------------ lighthouse breakwater
        Transform beam;
        Material beamMat;

        void BuildLighthouse(RegionContext ctx)
        {
            Vector2 root = new Vector2(CityPlan.BasinX1 + 6f, CityPlan.QuayZ - 4f), tip = CityPlan.Lighthouse;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Vector3 o = new Vector3(tip.x, 0f, tip.y);
            var r = new System.Random(515);
            int n = 34;
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(root, tip, i / (float)n);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 d = (tip - root).normalized;
                    Vector2 side = new Vector2(-d.y, d.x) * s * CityKit.R(r, 3.2f, 5.0f);
                    Vector3 c = new Vector3(p.x + side.x, CityKit.R(r, -1.2f, 0.4f), p.y + side.y) - o;
                    PG.Boulder(g, c, new Vector3(CityKit.R(r, 2f, 3f), CityKit.R(r, 1.5f, 2.2f), CityKit.R(r, 2f, 3f)), C(160, 156, 148), C(110, 110, 106), i * 2.3f + s);
                }
            }
            Vector3 ra = new Vector3(root.x, 1.6f, root.y) - o, ta = new Vector3(tip.x, 1.6f, tip.y) - o;
            PG.Beam(g, ra, ta, 2.6f, 1.2f, C(204, 200, 190));
            for (int i = 1; i < 12; i++)
            {
                Vector3 lp = Vector3.Lerp(ra, ta, i / 12f) + Up * 1.2f;
                PG.Rod(g, lp, lp + Up * 2.4f, 0.06f, C(50, 54, 60), 4);
                gl.Ellipsoid(lp + Up * 2.5f, new Vector3(0.22f, 0.22f, 0.22f), 6, 3, CityKit.LampWarm, CityKit.LampWarm);
            }
            // lighthouse: base, tapered striped tower, gallery, lantern room, dome
            PG.Box2(g, Up * 1.8f, new Vector3(5f, 1.8f, 5f), C(196, 192, 184), C(210, 206, 198));
            float H = 26f;
            int bands = 6;
            for (int b = 0; b < bands; b++)
            {
                float y0 = 3.6f + b * H / bands, y1 = y0 + H / bands;
                float r0 = Mathf.Lerp(3.2f, 2.0f, b / (float)bands), r1 = Mathf.Lerp(3.2f, 2.0f, (b + 1f) / bands);
                Color32 col = b % 2 == 0 ? C(244, 244, 240) : C(206, 52, 44);
                g.Frustum(Up * y0, Up * y1, r0, r0, r1, r1, 12, 0f, col, col, false, false);
            }
            float ty = 3.6f + H;
            g.Frustum(Up * ty, Up * (ty + 0.5f), 3.4f, 3.4f, 3.4f, 3.4f, 12, 0f, C(60, 66, 72), C(60, 66, 72), true, true);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f;
                PG.Rod(g, new Vector3(Mathf.Cos(a) * 3.3f, ty + 0.5f, Mathf.Sin(a) * 3.3f), new Vector3(Mathf.Cos(a) * 3.3f, ty + 1.6f, Mathf.Sin(a) * 3.3f), 0.05f, C(60, 66, 72), 3);
            }
            gl.Frustum(Up * (ty + 0.5f), Up * (ty + 3.4f), 1.8f, 1.8f, 1.8f, 1.8f, 10, 0f, C(255, 240, 190), C(255, 230, 170), false, false);
            g.Frustum(Up * (ty + 3.4f), Up * (ty + 5.2f), 2.2f, 2.2f, 0.3f, 0.3f, 10, 0f, C(206, 52, 44), C(170, 40, 36), true, false);
            g.Frustum(Up * (ty + 5.2f), Up * (ty + 6.6f), 0.08f, 0.08f, 0.04f, 0.04f, 4, 0f, C(60, 66, 72), C(60, 66, 72), false, false);
            ctx.StampSkyline(m, o, 0f);

            // turning beam: two long additive cones on a pivot (fades in at dusk)
            beamMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_CityLighthouseBeam" };
            beamMat.SetTexture("_MainTex", Texture2D.whiteTexture);
            beamMat.SetVector("_Fade", new Vector4(2f, 10f, 500f, 900f));
            beamMat.SetFloat("_EdgeFade", 0f);
            beamMat.SetFloat("_VertexGamma", 1f);
            var bv = new List<Vector3>();
            var bc = new List<Color32>();
            var bu = new List<Vector2>();
            var bt = new List<int>();
            for (int dir = -1; dir <= 1; dir += 2)
                for (int plane = 0; plane < 2; plane++)
                {
                    int b0 = bv.Count;
                    float len = 160f, w0 = 0.8f, w1 = 14f;
                    Vector3 side = plane == 0 ? Vector3.right : Vector3.up;
                    bv.Add(-side * w0); bv.Add(side * w0); bv.Add(new Vector3(0f, 0f, dir * len) + side * w1); bv.Add(new Vector3(0f, 0f, dir * len) - side * w1);
                    bu.Add(new Vector2(0f, 0f)); bu.Add(new Vector2(1f, 0f)); bu.Add(new Vector2(1f, 1f)); bu.Add(new Vector2(0f, 1f));
                    Color32 hot = C(255, 244, 200, 200), cold = C(255, 244, 210, 0);
                    bc.Add(hot); bc.Add(hot); bc.Add(cold); bc.Add(cold);
                    bt.Add(b0); bt.Add(b0 + 1); bt.Add(b0 + 2); bt.Add(b0); bt.Add(b0 + 2); bt.Add(b0 + 3);
                    bt.Add(b0); bt.Add(b0 + 2); bt.Add(b0 + 1); bt.Add(b0); bt.Add(b0 + 3); bt.Add(b0 + 2);
                }
            var bm = new Mesh { name = "City_LighthouseBeam" };
            bm.SetVertices(bv);
            bm.SetColors(bc);
            bm.SetUVs(0, bu);
            bm.SetTriangles(bt, 0);
            bm.RecalculateBounds();
            var bgo = ctx.AddMesh("City_LighthouseBeam", bm, beamMat, 0, false, true);
            bgo.transform.position = new Vector3(tip.x, ty + 2f, tip.y);
            beam = bgo.transform;
            renderers++;
        }

        void TickLighthouse(float t, float night)
        {
            if (beam == null) return;
            beam.rotation = Quaternion.Euler(0f, t * 36f, 0f);
            if (beamMat != null)
            {
                float a = Mathf.Clamp01((night - 0.15f) * 1.6f) * (1f - 0.5f * Atmosphere.RainIntensity);
                beamMat.SetColor("_Tint", new Color(1f, 0.95f, 0.82f, a * 0.7f));
            }
        }
    }
}

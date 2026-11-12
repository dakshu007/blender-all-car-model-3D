// CoastRegion.Sea.Build.cs - the open sea: a turquoise-to-deep-blue water plane out to the horizon (always
// visible, in the skyline root) over a sea-floor skirt beyond the terrain, animated surf foam and incoming
// wave lines that follow the traced waterline (marching squares on the terrain), karst islets with pines,
// granite sea stacks off the headland and far islands on the horizon; the jade lotus lagoons.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        Material seaMat, lagoonMat, foamMat, waveMat;

        // ------------------------------------------------------------------ sea plane + floor skirt
        void BuildSea()
        {
            seaMat = new Material(MaterialLibrary.Water) { name = "BR_CoastSea" };
            seaMat.SetColor("_Shallow", new Color(0.24f, 0.86f, 0.80f, 0.50f));
            seaMat.SetColor("_Deep", new Color(0.03f, 0.30f, 0.52f, 0.95f));
            seaMat.SetFloat("_RippleScale", 0.17f);
            seaMat.SetFloat("_RippleSpeed", 0.42f);
            seaMat.SetColor("_FoamColor", new Color(1f, 1f, 0.98f, 1f));

            float[] xs = { -6000f, -4200f, -3200f, -2600f, -2200f, -1950f, -1800f, -1700f, -1600f, -1500f, -1420f, -1340f, -1260f, -1180f, -1100f, -1000f };
            float[] zs = { -3000f, -2000f, -1400f, -1050f, -800f, -650f, -520f, -400f, -280f, -160f, -40f, 80f, 200f, 320f, 440f, 560f, 700f, 900f, 1200f, 1700f, 2300f, 3000f };
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var nrm = new List<Vector3>();
            var t = new List<int>();
            const float y = WorldField.WaterY;
            for (int j = 0; j < zs.Length; j++)
                for (int i = 0; i < xs.Length; i++)
                {
                    v.Add(new Vector3(xs[i], y, zs[j]));
                    uv.Add(new Vector2(xs[i] * 0.05f, zs[j] * 0.05f));
                    nrm.Add(Vector3.up);
                }
            int nx = xs.Length;
            for (int j = 0; j < zs.Length - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i;
                    t.Add(a); t.Add(a + nx); t.Add(a + 1);
                    t.Add(a + 1); t.Add(a + nx); t.Add(a + nx + 1);
                }
            var m = new Mesh { name = "Coast_Sea", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            var go = cx.AddMesh("Coast_Sea", m, seaMat, 0, false, true);
            var mr = go.GetComponent<MeshRenderer>();
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

            // sea-floor skirt beyond the terrain edge keeps the water column (and its colour) continuous to the horizon
            var fv = new List<Vector3>();
            var fc = new List<Color32>();
            var fn = new List<Vector3>();
            var ft = new List<int>();
            float[] fx = { -6000f, -3000f, -2200f, -1900f, -1780f, -1000f };
            float[] fz = { -3000f, -1600f, -900f, -760f, 1760f, 3000f };
            Color32 bed = LifeGeo.C(52, 92, 98), bedFar = LifeGeo.C(28, 58, 78);
            for (int j = 0; j < fz.Length; j++)
                for (int i = 0; i < fx.Length; i++)
                {
                    fv.Add(new Vector3(fx[i], WorldLayout.SeaFloor - 0.4f - (fx[i] < -2000f ? 6f : 0f), fz[j]));
                    fn.Add(Vector3.up);
                    fc.Add(fx[i] < -2500f || Mathf.Abs(fz[j]) > 2000f ? bedFar : bed);
                }
            int fnx = fx.Length;
            for (int j = 0; j < fz.Length - 1; j++)
                for (int i = 0; i < fnx - 1; i++)
                {
                    int a = j * fnx + i;
                    ft.Add(a); ft.Add(a + fnx); ft.Add(a + 1);
                    ft.Add(a + 1); ft.Add(a + fnx); ft.Add(a + fnx + 1);
                }
            var fm = new Mesh { name = "Coast_SeaFloor" };
            fm.SetVertices(fv); fm.SetNormals(fn); fm.SetColors(fc); fm.SetTriangles(ft, 0);
            fm.RecalculateBounds();
            cx.AddMesh("Coast_SeaFloor", fm, MaterialLibrary.Terrain, 0, false, true);
        }

        // ------------------------------------------------------------------ waterline tracing (marching squares)
        List<List<Vector2>> TraceWaterline(float x0, float z0, float x1, float z1, float step, float iso)
        {
            int nx = Mathf.FloorToInt((x1 - x0) / step) + 1, nz = Mathf.FloorToInt((z1 - z0) / step) + 1;
            var h = new float[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    h[j * nx + i] = Gnd(x0 + i * step, z0 + j * step) - iso;
            var pos = new Dictionary<int, Vector2>();
            var adj = new Dictionary<int, List<int>>();
            int vOff = nx * nz;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    float a = h[j * nx + i], b = h[j * nx + i + 1], c = h[(j + 1) * nx + i + 1], d = h[(j + 1) * nx + i];
                    int k = (a > 0f ? 1 : 0) | (b > 0f ? 2 : 0) | (c > 0f ? 4 : 0) | (d > 0f ? 8 : 0);
                    if (k == 0 || k == 15) continue;
                    int e0 = j * nx + i, e1 = vOff + j * nx + i + 1, e2 = (j + 1) * nx + i, e3 = vOff + j * nx + i;
                    // positions of the crossed edges
                    if (((k & 1) != 0) != ((k & 2) != 0) && !pos.ContainsKey(e0)) pos[e0] = new Vector2(x0 + (i + a / (a - b)) * step, z0 + j * step);
                    if (((k & 2) != 0) != ((k & 4) != 0) && !pos.ContainsKey(e1)) pos[e1] = new Vector2(x0 + (i + 1) * step, z0 + (j + b / (b - c)) * step);
                    if (((k & 8) != 0) != ((k & 4) != 0) && !pos.ContainsKey(e2)) pos[e2] = new Vector2(x0 + (i + d / (d - c)) * step, z0 + (j + 1) * step);
                    if (((k & 1) != 0) != ((k & 8) != 0) && !pos.ContainsKey(e3)) pos[e3] = new Vector2(x0 + i * step, z0 + (j + a / (a - d)) * step);
                    switch (k)
                    {
                        case 1: case 14: Link(adj, e3, e0); break;
                        case 2: case 13: Link(adj, e0, e1); break;
                        case 3: case 12: Link(adj, e3, e1); break;
                        case 4: case 11: Link(adj, e1, e2); break;
                        case 6: case 9: Link(adj, e0, e2); break;
                        case 7: case 8: Link(adj, e3, e2); break;
                        case 5: case 10:
                            {
                                bool centre = (a + b + c + d) * 0.25f > 0f;
                                if ((k == 5) == centre) { Link(adj, e3, e2); Link(adj, e0, e1); }
                                else { Link(adj, e3, e0); Link(adj, e1, e2); }
                                break;
                            }
                    }
                }
            // walk the chains (open ones from their ends first, then the closed rings)
            var chains = new List<List<Vector2>>();
            var seen = new HashSet<int>();
            for (int pass = 0; pass < 2; pass++)
                foreach (var kv in adj)
                {
                    if (seen.Contains(kv.Key)) continue;
                    if (pass == 0 && kv.Value.Count != 1) continue;
                    var chain = new List<Vector2>();
                    int cur = kv.Key, prev = -1;
                    while (cur >= 0 && !seen.Contains(cur))
                    {
                        seen.Add(cur);
                        chain.Add(pos[cur]);
                        int next = -1;
                        foreach (int n in adj[cur]) if (n != prev && !seen.Contains(n)) { next = n; break; }
                        prev = cur;
                        cur = next;
                    }
                    if (pass == 1 && chain.Count > 2) chain.Add(chain[0]);
                    if (chain.Count > 3) chains.Add(chain);
                }
            return chains;
        }

        static void Link(Dictionary<int, List<int>> adj, int a, int b)
        {
            List<int> l;
            if (!adj.TryGetValue(a, out l)) { l = new List<int>(2); adj[a] = l; }
            l.Add(b);
            if (!adj.TryGetValue(b, out l)) { l = new List<int>(2); adj[b] = l; }
            l.Add(a);
        }

        /// <summary>Smooths and re-spaces a traced line.</summary>
        static List<Vector2> Relax(List<Vector2> src, float spacing)
        {
            var p = new List<Vector2>(src);
            for (int pass = 0; pass < 2; pass++)
            {
                var q = new List<Vector2>(p);
                for (int i = 1; i < p.Count - 1; i++) q[i] = (p[i - 1] + p[i] * 2f + p[i + 1]) * 0.25f;
                p = q;
            }
            var outp = new List<Vector2> { p[0] };
            float carry = 0f;
            for (int i = 1; i < p.Count; i++)
            {
                Vector2 a = p[i - 1], b = p[i];
                float seg = Vector2.Distance(a, b);
                float t = spacing - carry;
                while (t <= seg) { outp.Add(Vector2.Lerp(a, b, t / seg)); t += spacing; }
                carry = seg - (t - spacing);
            }
            if (Vector2.Distance(outp[outp.Count - 1], p[p.Count - 1]) > spacing * 0.3f) outp.Add(p[p.Count - 1]);
            return outp;
        }

        // ------------------------------------------------------------------ surf foam and wave lines
        readonly List<List<Vector2>> shoreLines = new List<List<Vector2>>();

        void BuildShoreFoam()
        {
            const float iso = WorldField.WaterY + 0.03f;
            var chains = TraceWaterline(-1764f, -588f, -1000f, 588f, 4f, iso);
            var fv = new List<Vector3>(); var fc = new List<Color32>(); var fu = new List<Vector2>(); var ft = new List<int>();
            var wv = new List<Vector3>(); var wc = new List<Color32>(); var wu = new List<Vector2>(); var wt = new List<int>();
            float y = WorldField.WaterY + 0.035f;
            foreach (var raw in chains)
            {
                var line = Relax(raw, 3f);
                if (line.Count < 6) continue;
                float len = 0f;
                for (int i = 1; i < line.Count; i++) len += Vector2.Distance(line[i - 1], line[i]);
                if (len < 24f) continue;
                // which side is land? (majority vote along the line)
                int vote = 0;
                for (int i = 1; i < line.Count - 1; i += 3)
                {
                    Vector2 tg = (line[i + 1] - line[i - 1]).normalized, n = new Vector2(-tg.y, tg.x);
                    vote += Gnd(line[i] + n * 3f) > Gnd(line[i] - n * 3f) ? 1 : -1;
                }
                float sgn = vote >= 0 ? 1f : -1f;
                shoreLines.Add(line);
                float[] fd = { 1.2f, -1.4f, -6.5f };
                byte[] fa = { 120, 235, 0 };
                float[] fV = { 1f, 0.72f, 0f };
                float[] wd = { -4.5f, -13f, -21f, -34f };
                byte[] wa = { 0, 190, 150, 0 };
                float[] wV = { 1f, 0.7f, 0.45f, 0f };
                AddStrip(line, sgn, fd, fa, fV, y, 7f, fv, fc, fu, ft);
                AddStrip(line, sgn, wd, wa, wV, y - 0.005f, 9f, wv, wc, wu, wt);
            }
            foamMat = new Material(MaterialLibrary.FindShader("BlossomRally/FlowFoam")) { name = "BR_CoastSurf" };
            foamMat.SetColor("_WaterColor", new Color(0.90f, 0.98f, 1f, 0.30f));
            foamMat.SetColor("_FoamColor", new Color(1f, 1f, 1f, 0.95f));
            foamMat.SetVector("_Tiling", new Vector4(1f, 2f, 0f, 0f));
            foamMat.SetFloat("_Speed", 0.32f);
            foamMat.SetFloat("_Streak", 1f);
            foamMat.SetFloat("_Lit", 0.75f);
            waveMat = new Material(foamMat) { name = "BR_CoastWaves" };
            waveMat.SetColor("_WaterColor", new Color(0.85f, 0.96f, 1f, 0f));
            waveMat.SetColor("_FoamColor", new Color(1f, 1f, 1f, 0.6f));
            waveMat.SetVector("_Tiling", new Vector4(0.55f, 5f, 0f, 0f));
            waveMat.SetFloat("_Speed", 0.16f);
            MakeStripMesh("Coast_Surf", fv, fc, fu, ft, foamMat);
            MakeStripMesh("Coast_WaveLines", wv, wc, wu, wt, waveMat);
        }

        static void AddStrip(List<Vector2> line, float sgn, float[] offs, byte[] alpha, float[] vv, float y, float uLen,
                             List<Vector3> v, List<Color32> c, List<Vector2> uv, List<int> t)
        {
            int rows = offs.Length;
            int start = v.Count;
            float s = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0) s += Vector2.Distance(line[i - 1], line[i]);
                Vector2 tg = (line[Mathf.Min(line.Count - 1, i + 1)] - line[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new Vector2(-tg.y, tg.x) * sgn;                          // towards land
                float endFade = Mathf.Clamp01(Mathf.Min(i, line.Count - 1 - i) / 3f);
                for (int r = 0; r < rows; r++)
                {
                    Vector2 p = line[i] + n * offs[r];
                    v.Add(new Vector3(p.x, y, p.y));
                    c.Add(new Color32(255, 255, 255, (byte)(alpha[r] * endFade)));
                    uv.Add(new Vector2(s / uLen, vv[r]));
                }
            }
            for (int i = 0; i < line.Count - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = start + i * rows + r, b = a + rows;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
        }

        void MakeStripMesh(string name, List<Vector3> v, List<Color32> c, List<Vector2> uv, List<int> t, Material mat)
        {
            if (v.Count == 0) return;
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            m.normals = n;
            m.RecalculateBounds();
            var go = cx.AddMesh(name, m, mat, WorldBuilder.LayerTrees, false, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        // ------------------------------------------------------------------ islets, sea stacks, far islands
        void BuildIslands()
        {
            var karst = new PropModel();
            var kg = karst[PK.Karst];
            var sky = new PropModel();
            var sg = sky[PK.Karst];
            var pine = new PropModel[4];
            for (int i = 0; i < pine.Length; i++) pine[i] = NatureShapes.Pine(new System.Random(900 + i), 0.9f + i * 0.12f);
            // karst pillars on the terrain islets
            foreach (var il in Islets)
            {
                Vector2 c = new Vector2(il.x, il.y);
                int n = 1 + rnd.Next(3);
                for (int k = 0; k < n; k++)
                {
                    float a = Rn(0f, 6.28f);
                    Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * il.z * (k == 0 ? 0.1f : 0.55f);
                    float H = Rn(26f, 54f) * (k == 0 ? 1f : 0.6f), Rb = il.z * Rn(0.55f, 0.75f) * (k == 0 ? 1f : 0.6f);
                    NatureShapes.Karst(kg, V3(p, Gnd(p) - 4f), H, Rb, rnd);
                    rocks++;
                }
                for (int k = 0; k < 4; k++)
                {
                    float a = Rn(0f, 6.28f);
                    Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * il.z * Rn(0.8f, 1.05f);
                    float g = Gnd(p);
                    if (g < WorldField.WaterY + 0.2f) continue;
                    Stamp(pine[rnd.Next(pine.Length)], p, g - 0.2f, Rn(0f, 360f), Rn(0.7f, 1.1f));
                }
            }
            // horizon islands beyond the terrain (mesh only, rising from the deep)
            for (int i = 0; i < 16; i++)
            {
                float a = Rn(-1.25f, 1.25f);
                float dist = Rn(500f, 2300f);
                Vector2 c = new Vector2(-1760f - dist * Mathf.Cos(a) * 0.9f, dist * Mathf.Sin(a) * 1.1f);
                int n = 2 + rnd.Next(4);
                for (int k = 0; k < n; k++)
                {
                    Vector2 p = c + new Vector2(Rn(-60f, 60f), Rn(-60f, 60f));
                    float scale = Mathf.Lerp(1f, 1.6f, Mathf.InverseLerp(500f, 2300f, dist));
                    NatureShapes.Karst(sg, V3(p, WorldLayout.SeaFloor - 6f), Rn(40f, 95f) * scale, Rn(14f, 26f) * scale, rnd);
                }
            }
            // granite sea stacks around the headland tip and the north point
            Vector2[] stacks =
            {
                new Vector2(-1446f, 18f), new Vector2(-1458f, -34f), new Vector2(-1432f, 58f), new Vector2(-1404f, -84f), new Vector2(-1480f, -6f),
                new Vector2(-1376f, 86f), new Vector2(-1336f, 432f), new Vector2(-1318f, 458f), new Vector2(-1356f, 410f),
                new Vector2(-1296f, -452f), new Vector2(-1330f, -420f), new Vector2(-1290f, -410f),
            };
            foreach (var p in stacks)
            {
                float g = Gnd(p);
                if (g > WorldField.WaterY - 0.5f) continue;
                CoastShapes.SeaStack(kg, V3(p, g - 1f), Rn(13f, 28f) + (WorldField.WaterY - g), Rn(4.5f, 8.5f), rnd);
                rocks++;
            }
            cx.Stamp(karst, Matrix4x4.identity);
            cx.StampSkyline(sky, Matrix4x4.identity);
        }

        // ------------------------------------------------------------------ lotus lagoons
        void BuildLagoons()
        {
            lagoonMat = new Material(MaterialLibrary.Water) { name = "BR_CoastLagoon" };
            lagoonMat.SetColor("_Shallow", new Color(0.50f, 0.74f, 0.58f, 0.70f));
            lagoonMat.SetColor("_Deep", new Color(0.20f, 0.42f, 0.36f, 0.92f));
            lagoonMat.SetFloat("_RippleScale", 0.9f);
            lagoonMat.SetFloat("_RippleSpeed", 0.25f);
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var lotus = new PropModel();
            var lg = lotus[PK.Props];
            foreach (var L in Lagoons)
            {
                int c0 = v.Count;
                v.Add(new Vector3(L.x, LagoonY, L.y)); nn.Add(Vector3.up); uv.Add(new Vector2(L.x * 0.05f, L.y * 0.05f));
                const int N = 28;
                for (int k = 0; k <= N; k++)
                {
                    float a = k * Mathf.PI * 2f / N;
                    Vector3 p = new Vector3(L.x + Mathf.Cos(a) * L.z * 1.02f, LagoonY, L.y + Mathf.Sin(a) * L.w * 1.02f);
                    v.Add(p); nn.Add(Vector3.up); uv.Add(new Vector2(p.x * 0.05f, p.z * 0.05f));
                    if (k > 0) { t.Add(c0); t.Add(c0 + k + 1); t.Add(c0 + k); }
                }
                // lily pads and lotus flowers where the water is
                int pads = (int)(L.z * L.w * 0.32f);
                Vector2 patch = Vector2.zero;
                for (int i = 0; i < pads; i++)
                {
                    if (i % 40 == 0) { float pa = Rn(0f, 6.28f), pr = Rn(0.2f, 0.7f); patch = new Vector2(L.x + Mathf.Cos(pa) * L.z * pr, L.y + Mathf.Sin(pa) * L.w * pr); }
                    float a = Rn(0f, 6.28f), r = Mathf.Sqrt(Rn(0f, 1f)) * 0.45f;
                    Vector2 p = patch + new Vector2(Mathf.Cos(a) * L.z * r, Mathf.Sin(a) * L.w * r);
                    if (Gnd(p) > LagoonY - 0.15f) continue;
                    if (Ch(0.75f)) NatureShapes.LilyPad(lg, V3(p, LagoonY + 0.03f), Rn(0.4f, 0.75f), Rn(0f, 6.28f));
                    else NatureShapes.LotusFlower(lg, V3(p, LagoonY), Rn(1.0f, 1.45f), Rn(0f, 6.28f), Ch(0.3f));
                }
            }
            var m = new Mesh { name = "Coast_Lagoons" };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            cx.AddMesh("Coast_Lagoons", m, lagoonMat, WorldBuilder.LayerTrees, false, false).GetComponent<MeshRenderer>().receiveShadows = true;
            cx.Stamp(lotus, Matrix4x4.identity);

            // lotus-viewing pavilion with a zig-zag boardwalk on the big lagoon
            var big = Lagoons[0];
            Vector2 pav = new Vector2(big.x - big.z * 0.25f, big.y + big.w * 0.15f);
            Vector2 shore = new Vector2(big.x + big.z * 1.15f, big.y + big.w * 0.35f);
            var pm = CoastShapes.Pavilion(2.2f, 0.9f, Pal.GreenTile, Pal.GreenTile2);
            Stamp(pm, pav, LagoonY - 0.6f, 90f);
            BoxCol(pav, LagoonY, new Vector3(5.4f, 4f, 5.4f), 90f);
            var walk = new PropModel();
            var wg = walk[PK.Props];
            Vector2 mid = Vector2.Lerp(pav, shore, 0.5f) + new Vector2(0f, 6f);
            Boardwalk(wg, shore, mid, LagoonY + 0.35f);
            Boardwalk(wg, mid, pav + new Vector2(3f, 0f), LagoonY + 0.35f);
            cx.Stamp(walk, Matrix4x4.identity);
            houses++;
        }

        void Boardwalk(LifeGeo g, Vector2 a, Vector2 b, float y)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            d /= len;
            Vector2 s = new Vector2(-d.y, d.x) * 0.9f;
            int n = Mathf.CeilToInt(len / 1.1f);
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = a + d * (i * len / n), p1 = a + d * ((i + 1) * len / n - 0.08f);
                Color32 c = (i & 1) == 0 ? CoastShapes.PlankA : CoastShapes.PlankB;
                Vector3 q0 = V3(p0 - s, y), q1 = V3(p1 - s, y), q2 = V3(p1 + s, y), q3 = V3(p0 + s, y);
                g.QuadOut(q0, q1, q2, q3, Vector3.up, c, c, c, c);
                if (i % 3 == 0)
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector2 pp = p0 + s * k;
                        PG.Rod(g, V3(pp, y - 1.6f), V3(pp, y + 0.75f), 0.06f, CoastShapes.Pile, 4);
                    }
            }
            for (int k = -1; k <= 1; k += 2) PG.Beam(g, V3(a + s * k, y + 0.72f), V3(b + s * k, y + 0.72f), 0.04f, 0.035f, Pal.WoodDark);
        }
    }
}

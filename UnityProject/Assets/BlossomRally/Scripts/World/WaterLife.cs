// WaterLife.cs - life on and around the water: koi gliding just under the lake surface, clusters of
// lily pads with pink lotus flowers, a rocky waterfall where the river leaves the northern hills
// (scrolling white-water ribbon, churning foam pool, mist and spray), foam where the river runs
// against the bridge abutments and gentle rings around the lake pavilion's stilts.
//
// Cheap by design: koi and lotus are single CPU-posed meshes (LifeBatch, see ValleyLife.cs) only
// updated while the camera is near the lake; all foam rings / strips are merged into two static meshes;
// small foam flecks come from one shared particle system emitted by hand. No per-frame allocations.
// Everything advances with Time.deltaTime (or scaled shader / particle time), so pausing freezes it.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class WaterLife : MonoBehaviour
    {
        /// <summary>Build all water life. Call once after WorldBuilder.Build() has finished.</summary>
        public static WaterLife Create(WorldBuilder w)
        {
            var go = new GameObject("WaterLife");
            var life = go.AddComponent<WaterLife>();
            life.world = w;
            life.StartCoroutine(life.Build());
            return life;
        }

        public bool Ready { get; private set; }
        /// <summary>World position of the waterfall's foam pool (valid once Ready and HasWaterfall).</summary>
        public Vector3 WaterfallPosition { get; private set; }
        public bool HasWaterfall { get; private set; }

        WorldBuilder world;
        readonly System.Random rng = new System.Random(77813);
        float clock;
        List<Vector2> river;
        const float WaterY = WorldBuilder.WaterY;

        Material koiMat, fallMat, ringMat, stripMat;
        readonly FxMesh rings = new FxMesh();
        readonly FxMesh strips = new FxMesh();
        readonly List<Vector3> foamPts = new List<Vector3>();
        readonly List<Vector3> foamVel = new List<Vector3>();
        Vector3[] foamP, foamV;
        ParticleSystem flecks;
        ParticleSystem.EmitParams emit;
        float emitAcc, foamRate;

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }

        IEnumerator Build()
        {
            while (world != null && !world.Done) yield return null;
            if (world == null) yield break;
            river = Spline.Sample(MapData.River, false, 6f);
            MakeMaterials();
            BuildKoi();
            yield return null;
            BuildLotus();
            yield return null;
            BuildWaterfall();
            yield return null;
            BuildBridgeFoam();
            BuildPavilionRipples();
            FinishFx();
            Ready = true;
            Step(0f, true);
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;      // paused
            clock += dt;
            Step(dt, false);
        }

        void Step(float dt, bool force)
        {
            Camera cam = Camera.main;
            bool nearLake = force || cam == null;
            if (!nearLake)
            {
                Vector3 e = cam.transform.position;
                nearLake = (new Vector2(e.x, e.z) - MapData.LakeCenter).sqrMagnitude < 230f * 230f;
            }
            if (nearLake)
            {
                UpdateKoi(dt);
                UpdateLotus();
            }
            EmitFoam(dt);
        }

        void MakeMaterials()
        {
            koiMat = new Material(MaterialLibrary.FindShader("BlossomRally/Koi")) { name = "BR_Koi" };
            Shader flow = MaterialLibrary.FindShader("BlossomRally/FlowFoam");
            fallMat = FlowMat(flow, "BR_Waterfall", new Color(0.78f, 0.9f, 0.97f, 0.5f), new Color(1f, 1f, 1f, 0.96f), 1.5f, 0.8f);
            ringMat = FlowMat(flow, "BR_FoamRings", new Color(0.9f, 0.96f, 1f, 0f), new Color(1f, 1f, 1f, 0.85f), 0.25f, 1f);
            stripMat = FlowMat(flow, "BR_FoamStrips", new Color(0.9f, 0.96f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.8f), 0.45f, 1f);
        }

        static Material FlowMat(Shader s, string name, Color water, Color foam, float speed, float streak)
        {
            var m = new Material(s) { name = name };
            m.SetColor("_WaterColor", water);
            m.SetColor("_FoamColor", foam);
            m.SetVector("_Tiling", new Vector4(1f, 1f, 0f, 0f));
            m.SetFloat("_Speed", speed);
            m.SetFloat("_Streak", streak);
            return m;
        }

        Vector2 LakePoint(float kMax)
        {
            Vector2 c = MapData.LakeCenter, r = MapData.LakeRadii;
            for (int tries = 0; tries < 40; tries++)
            {
                float a = Rand(0f, Mathf.PI * 2f), k = Mathf.Sqrt(Rand(0f, 1f)) * kMax;
                Vector2 p = c + new Vector2(Mathf.Cos(a) * r.x * k, Mathf.Sin(a) * r.y * k);
                if (LifeShapes.ClearOfPavilion(p, 0.8f) && world.TerrainHeight(p.x, p.y) < WaterY - 0.6f) return p;
            }
            return c + new Vector2(r.x * 0.45f, r.y * 0.2f);
        }

        // ================================================================== koi
        class Koi
        {
            public int part;
            public Vector2 pos, target;
            public float heading, speed, cruise, depth, depthTarget, depthTimer, retarget, wig, scale;
        }

        LifeBatch koiBatch;
        Koi[] koi;

        void BuildKoi()
        {
            koiBatch = new LifeBatch { BendK = 9f };
            koi = new Koi[14];
            for (int i = 0; i < koi.Length; i++)
            {
                var k = new Koi { part = koiBatch.AddPart(koiBatch.AddTemplate(KoiShape(i % 5, i))) };
                k.pos = LakePoint(0.75f);
                k.target = LakePoint(0.75f);
                k.heading = Rand(0f, Mathf.PI * 2f);
                k.cruise = Rand(0.35f, 0.8f);
                k.speed = k.cruise;
                k.depth = Rand(0.18f, 0.3f);
                k.depthTarget = k.depth;
                k.depthTimer = Rand(2f, 8f);
                k.retarget = Rand(6f, 14f);
                k.wig = Rand(0f, 10f);
                k.scale = Rand(1.1f, 1.55f);
                koi[i] = k;
            }
            koiBatch.Finish("Water_Koi", koiMat, transform, WorldBuilder.LayerTrees, false);
        }

        void UpdateKoi(float dt)
        {
            if (koi == null) return;
            for (int i = 0; i < koi.Length; i++)
            {
                var k = koi[i];
                k.retarget -= dt;
                Vector2 to = k.target - k.pos;
                if (to.sqrMagnitude < 2.25f || k.retarget < 0f)
                {
                    k.target = LakePoint(0.75f);
                    k.retarget = Rand(8f, 16f);
                    to = k.target - k.pos;
                }
                float diff = Mathf.DeltaAngle(k.heading * Mathf.Rad2Deg, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg);
                float turn = Mathf.Clamp(diff * Mathf.Deg2Rad, -0.9f * dt, 0.9f * dt);
                k.heading += turn;
                k.speed = Mathf.MoveTowards(k.speed, k.cruise * (1f - 0.6f * Mathf.Abs(diff) / 180f), 0.4f * dt);
                Vector2 np = k.pos + new Vector2(Mathf.Sin(k.heading), Mathf.Cos(k.heading)) * (k.speed * dt);
                if (MapData.LakeK(np) < 0.86f && LifeShapes.ClearOfPavilion(np, 0.3f)) k.pos = np;
                else { k.target = LakePoint(0.6f); k.retarget = Rand(8f, 16f); }
                k.depthTimer -= dt;
                if (k.depthTimer < 0f)
                {
                    k.depthTarget = rng.NextDouble() < 0.25 ? 0.07f : Rand(0.18f, 0.32f);   // sometimes rise to the surface
                    k.depthTimer = Rand(4f, 10f);
                }
                k.depth = Mathf.MoveTowards(k.depth, k.depthTarget, 0.06f * dt);
                k.wig += dt * (3f + 8f * k.speed);
                float roll = Mathf.Clamp(-turn / Mathf.Max(dt, 1e-4f) * 10f, -12f, 12f);
                Quaternion r = Quaternion.Euler(0f, k.heading * Mathf.Rad2Deg, roll);
                koiBatch.SetBend(k.part, 0.03f + 0.05f * k.speed, k.wig);
                koiBatch.Set(k.part, Matrix4x4.TRS(new Vector3(k.pos.x, WaterY - k.depth, k.pos.y), r, new Vector3(k.scale, k.scale, k.scale)));
                koiBatch.SetAlpha(k.part, (byte)(Mathf.InverseLerp(0.04f, 0.3f, k.depth) * 255f));
            }
            koiBatch.Upload();
        }

        /// <summary>Low-poly koi (forward +Z, ~0.6 m), pattern painted per facet; bend weight grows towards the tail.</summary>
        static LifeGeo KoiShape(int pattern, int seed)
        {
            Color32 white = C(250, 248, 240), red = C(234, 72, 40), orange = C(250, 140, 40), black = C(34, 32, 36);
            Color32 gold = C(255, 202, 84), cream = C(250, 236, 212), belly = C(252, 196, 120);
            var g = new LifeGeo();
            float s1 = seed * 1.37f, s2 = seed * 2.11f;
            g.Paint = p =>
            {
                float nz = Mathf.Sin(p.z * 21f + s1) + Mathf.Sin(p.z * 9f - p.x * 14f + s2) * 0.8f + Mathf.Sin(p.x * 25f + s1 * 0.5f) * 0.4f;
                bool top = p.y > -0.01f;
                switch (pattern)
                {
                    case 0: return top && nz > 0.1f ? red : white;                                    // kohaku
                    case 1: return top ? (nz > 1.2f ? cream : orange) : belly;                         // orange ogon
                    case 2: return !top ? white : (nz > 0.7f ? black : (nz > -0.4f ? red : white));    // sanke
                    case 3: return top ? (nz > 0.4f ? gold : cream) : cream;                           // yamabuki
                    default: return top ? (nz > 0.9f ? white : (nz > -0.6f ? orange : black)) : white;  // showa-ish
                }
            };
            Vector4[] rings =
            {
                new Vector4(0.25f, 0f, 0f, 0f),
                new Vector4(0.22f, 0.003f, 0.035f, 0.03f),
                new Vector4(0.15f, 0.005f, 0.06f, 0.05f),
                new Vector4(0.05f, 0.005f, 0.078f, 0.062f),
                new Vector4(-0.06f, 0.004f, 0.062f, 0.05f),
                new Vector4(-0.15f, 0.002f, 0.036f, 0.03f),
                new Vector4(-0.2f, 0f, 0.018f, 0.016f),
            };
            for (int i = 0; i < rings.Length - 1; i++)
            {
                Vector4 a = rings[i + 1], b = rings[i];
                g.Frustum(new Vector3(0f, a.y, a.x), new Vector3(0f, b.y, b.x), a.z, a.w, b.z, b.w, 6, 0f, white, white, i == rings.Length - 2, false);
            }
            g.Paint = null;
            Color32 fin = pattern == 1 ? C(252, 170, 90) : (pattern == 3 ? C(255, 226, 150) : C(244, 238, 228));
            // flowing tail fin (horizontal so it reads from above)
            Vector3 bl = new Vector3(-0.016f, 0f, -0.19f), br = new Vector3(0.016f, 0f, -0.19f), notch = new Vector3(0f, 0f, -0.27f);
            Vector3 tl = new Vector3(-0.1f, 0.005f, -0.35f), tr = new Vector3(0.1f, 0.005f, -0.35f);
            g.Tri2(bl, br, notch, fin, fin, fin);
            g.Tri2(bl, notch, tl, fin, fin, fin);
            g.Tri2(br, tr, notch, fin, fin, fin);
            // pectoral + dorsal fins, eyes
            for (int s = -1; s <= 1; s += 2)
                g.Tri2(new Vector3(0.055f * s, -0.02f, 0.11f), new Vector3(0.135f * s, -0.03f, 0.05f), new Vector3(0.06f * s, -0.02f, 0.035f), fin, fin, fin);
            g.Tri2(new Vector3(0f, 0.055f, 0.09f), new Vector3(0f, 0.055f, -0.08f), new Vector3(0f, 0.09f, -0.03f), fin, fin, fin);
            g.Box(new Vector3(-0.031f, 0.014f, 0.2f), new Vector3(0.006f, 0.006f, 0.006f), black);
            g.Box(new Vector3(0.031f, 0.014f, 0.2f), new Vector3(0.006f, 0.006f, 0.006f), black);
            g.ComputeBend(p => Mathf.Pow(Mathf.Clamp01((0.1f - p.z) / 0.45f), 1.4f));
            return g;
        }

        // ================================================================== lotus
        class Pad
        {
            public int part, flower = -1;
            public Vector3 pos;
            public float yaw, tx, tz, scale, fscale, phase;
        }

        LifeBatch lotusBatch;
        Pad[] pads;

        void BuildLotus()
        {
            lotusBatch = new LifeBatch();
            var padT = new int[4];
            for (int i = 0; i < 4; i++) padT[i] = lotusBatch.AddTemplate(LilyPad(i));
            int flowerT = lotusBatch.AddTemplate(LotusFlower(false)), budT = lotusBatch.AddTemplate(LotusFlower(true));
            var list = new List<Pad>();
            Vector2 c = MapData.LakeCenter, r = MapData.LakeRadii;
            const int clusters = 7;
            float a0 = Rand(0f, Mathf.PI * 2f);
            for (int ci = 0; ci < clusters && list.Count < 60; ci++)
            {
                float a = a0 + ci * Mathf.PI * 2f / clusters + Rand(-0.3f, 0.3f);
                float k = Rand(0.8f, 0.93f);
                Vector2 cc = c + new Vector2(Mathf.Cos(a) * r.x * k, Mathf.Sin(a) * r.y * k);
                int want = rng.Next(6, 10);
                for (int tries = 0; tries < 80 && want > 0 && list.Count < 60; tries++)
                {
                    Vector2 p = cc + new Vector2(Rand(-4.5f, 4.5f), Rand(-3.5f, 3.5f));
                    float s = Rand(0.35f, 0.8f);
                    if (MapData.LakeK(p) > 1.0f || !LifeShapes.ClearOfPavilion(p, 1f)) continue;
                    if (world.TerrainHeight(p.x, p.y) > WaterY - 0.15f) continue;
                    bool ok = true;
                    for (int q = 0; q < list.Count && ok; q++)
                    {
                        float md = (list[q].scale + s) * 0.92f;
                        if ((new Vector2(list[q].pos.x, list[q].pos.z) - p).sqrMagnitude < md * md) ok = false;
                    }
                    if (!ok) continue;
                    list.Add(new Pad
                    {
                        part = lotusBatch.AddPart(padT[rng.Next(4)]),
                        pos = new Vector3(p.x, 0f, p.y),
                        yaw = Rand(0f, 360f), tx = Rand(-3f, 3f), tz = Rand(-3f, 3f),
                        scale = s, phase = Rand(0f, 50f)
                    });
                    want--;
                }
            }
            // lotus flowers on some of the bigger pads
            int flowers = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < list.Count && flowers < 18; i++)
                {
                    var pd = list[i];
                    if (pd.flower >= 0 || pd.scale < (pass == 0 ? 0.5f : 0.4f) || (pass == 0 && rng.NextDouble() < 0.45)) continue;
                    if (pass == 1 && flowers >= 12) break;
                    bool bud = rng.NextDouble() < 0.2;
                    pd.flower = lotusBatch.AddPart(bud ? budT : flowerT);
                    pd.fscale = bud ? Rand(0.9f, 1.1f) : Rand(1.0f, 1.35f);
                    flowers++;
                }
            pads = list.ToArray();
            if (pads.Length == 0) return;
            lotusBatch.Finish("Water_Lotus", MaterialLibrary.VertexColorWind, transform, WorldBuilder.LayerTrees, false);
        }

        void UpdateLotus()
        {
            if (pads == null || pads.Length == 0) return;
            for (int i = 0; i < pads.Length; i++)
            {
                var pd = pads[i];
                float t = clock + pd.phase;
                Vector3 p = new Vector3(pd.pos.x, WaterY + 0.085f + Mathf.Sin(t * 0.9f) * 0.012f, pd.pos.z);
                Quaternion r = Quaternion.Euler(pd.tx + Mathf.Sin(t * 0.7f) * 1.5f, pd.yaw + Mathf.Sin(t * 0.21f) * 3f, pd.tz + Mathf.Cos(t * 0.6f) * 1.5f);
                lotusBatch.Set(pd.part, Matrix4x4.TRS(p, r, new Vector3(pd.scale, pd.scale, pd.scale)));
                if (pd.flower >= 0) lotusBatch.Set(pd.flower, Matrix4x4.TRS(p + r * new Vector3(0f, 0.012f, 0f), r, new Vector3(pd.fscale, pd.fscale, pd.fscale)));
            }
            lotusBatch.Upload();
        }

        /// <summary>Unit-radius lily pad with a notch, slightly cupped, with a thin rim.</summary>
        static LifeGeo LilyPad(int variant)
        {
            var g = new LifeGeo();
            Color32 mid = variant % 2 == 0 ? C(128, 178, 86) : C(104, 162, 78);
            Color32 rim = variant < 2 ? C(66, 124, 60) : C(86, 138, 64);
            Color32 skirt = C(52, 96, 50);
            const int seg = 14;
            float notch = 0.38f + variant * 0.05f;
            Vector3 ctr = new Vector3(0f, 0.008f, 0f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = notch * 0.5f + (Mathf.PI * 2f - notch) * i / seg;
                float a1 = notch * 0.5f + (Mathf.PI * 2f - notch) * (i + 1) / seg;
                float w0 = 1f + 0.05f * Mathf.Sin(a0 * 3f + variant), w1 = 1f + 0.05f * Mathf.Sin(a1 * 3f + variant);
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * w0, 0.022f, Mathf.Sin(a0) * w0);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * w1, 0.022f, Mathf.Sin(a1) * w1);
                g.TriOut(ctr, p0, p1, Vector3.up, mid, rim, rim);
                float am = (a0 + a1) * 0.5f;
                g.QuadOut(p0, p1, p1 + Vector3.down * 0.03f, p0 + Vector3.down * 0.03f, new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)), skirt, skirt, skirt, skirt);
            }
            return g;
        }

        /// <summary>Layered pink lotus (open) or closed bud, sitting at the origin.</summary>
        static LifeGeo LotusFlower(bool bud)
        {
            var g = new LifeGeo();
            Color32 tip = C(236, 104, 156), mid = C(248, 168, 198), bas = C(255, 234, 240), seed = C(240, 204, 80), sepal = C(98, 150, 74);
            if (!bud) g.Frustum(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.085f, 0f), 0.035f, 0.035f, 0.05f, 0.05f, 8, 0f, seed, seed, false, true);
            // layers: count, length, elevation (deg), half width, yaw offset
            float[,] layers = bud
                ? new float[,] { { 5f, 0.17f, 80f, 0.06f, 0f }, { 6f, 0.19f, 70f, 0.075f, 0.5f }, { 5f, 0.1f, 15f, 0.06f, 0.2f } }
                : new float[,] { { 7f, 0.15f, 64f, 0.06f, 0f }, { 9f, 0.19f, 38f, 0.08f, 0.5f }, { 6f, 0.12f, 12f, 0.07f, 0.25f } };
            for (int l = 0; l < layers.GetLength(0); l++)
            {
                int cnt = (int)layers[l, 0];
                float len = layers[l, 1], el = layers[l, 2] * Mathf.Deg2Rad, hw = layers[l, 3], off = layers[l, 4];
                bool green = l == 2;
                Color32 ct = green ? sepal : tip, cm = green ? sepal : mid, cb = green ? LifeGeo.Shade(sepal, 0.85f) : bas;
                for (int k = 0; k < cnt; k++)
                {
                    float yaw = (k + off) * Mathf.PI * 2f / cnt;
                    Vector3 h = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    Vector3 side = new Vector3(-h.z, 0f, h.x);
                    Vector3 dir = h * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el);
                    Vector3 b = new Vector3(0f, 0.03f, 0f) + h * 0.025f;
                    Vector3 t = b + dir * len;
                    Vector3 m = b + dir * (len * 0.5f) + Vector3.up * 0.012f;
                    g.Tri2(b, m - side * hw, t, cb, cm, ct);
                    g.Tri2(b, t, m + side * hw, cb, ct, cm);
                }
            }
            return g;
        }

        // ================================================================== waterfall
        static float Noise(float x, float y)
        {
            return Mathf.Sin(x * 1.7f + Mathf.Sin(y * 0.9f) * 1.3f) * 0.5f + Mathf.Sin(y * 2.3f + x * 0.6f) * 0.35f + Mathf.Sin((x - y) * 3.7f) * 0.15f;
        }

        static readonly Vector3[] IcoV =
        {
            new Vector3(-1f, 1.618f, 0f), new Vector3(1f, 1.618f, 0f), new Vector3(-1f, -1.618f, 0f), new Vector3(1f, -1.618f, 0f),
            new Vector3(0f, -1f, 1.618f), new Vector3(0f, 1f, 1.618f), new Vector3(0f, -1f, -1.618f), new Vector3(0f, 1f, -1.618f),
            new Vector3(1.618f, 0f, -1f), new Vector3(1.618f, 0f, 1f), new Vector3(-1.618f, 0f, -1f), new Vector3(-1.618f, 0f, 1f)
        };
        static readonly int[] IcoF =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        static void Boulder(LifeGeo g, Vector3 c, Vector3 r, float seed)
        {
            var p = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                Vector3 q = IcoV[i].normalized;
                float k = 1f + 0.22f * Noise(q.x * 2.3f + seed, q.y * 2.1f + q.z * 1.7f - seed);
                q = new Vector3(q.x * r.x, q.y * r.y, q.z * r.z) * k + c;
                if (q.y < c.y - r.y * 0.55f) q.y = c.y - r.y * 0.55f;
                p[i] = q;
            }
            for (int f = 0; f < IcoF.Length; f += 3)
            {
                Vector3 a = p[IcoF[f]], b = p[IcoF[f + 1]], d = p[IcoF[f + 2]];
                Color32 w = C(255, 255, 255);
                g.TriOut(a, b, d, (a + b + d) / 3f - c, w, w, w);
            }
        }

        float TerrainAt(Vector2 p, Vector3 right, float s)
        {
            return world.TerrainHeight(p.x + right.x * s, p.y + right.z * s);
        }

        void BuildWaterfall()
        {
            // where the river leaves the northern hills: the most downstream point of the first stretch whose
            // banks stand >= 9.5 m high (river samples run upstream -> downstream)
            int n = river.Count, best = -1, fallback = -1;
            float fbBank = -999f;
            for (int i = 1; i < n - 1; i++)
            {
                Vector2 p = river[i];
                if (Mathf.Abs(p.x) > 600f || Mathf.Abs(p.y) > 600f) continue;
                Vector2 d = (river[i + 1] - river[i - 1]).normalized;
                Vector3 rt = new Vector3(d.y, 0f, -d.x);
                float bank = Mathf.Min(TerrainAt(p, rt, 24f), TerrainAt(p, rt, -24f));
                RoadSample rs;
                if (world.NearestRoad(p, out rs, 48f) < 45f) { if (best >= 0) break; continue; }
                if (bank > fbBank) { fbBank = bank; fallback = i; }
                if (bank >= 9.5f) best = i;
                else if (best >= 0) break;
            }
            if (best < 0) best = fallback;
            if (best < 0) return;

            Vector2 rp = river[best];
            Vector2 dd = (river[best + 1] - river[best - 1]).normalized;
            Vector3 fwd = new Vector3(dd.x, 0f, dd.y);               // downstream
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            const float W = 24f, L = 26f, yBot = -3.4f;
            float topY = Mathf.Clamp(Mathf.Min(TerrainAt(rp, right, -W), TerrainAt(rp, right, W)) - 1f, 3f, 10.5f);
            var root = new GameObject("Waterfall").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(rp.x, 0f, rp.y), Quaternion.LookRotation(fwd, Vector3.up));

            // ---- local shape functions (x across, y up, z downstream; the cliff face is near z = 0)
            System.Func<float, float, float> topH = (x, z) =>
            {
                float ax = Mathf.Abs(x);
                float chan = 1f - Spline.SmoothStep(4.5f, 7.5f, ax);
                return topY + Noise(x * 0.35f + 5f, z * 0.3f) * 0.3f + Spline.SmoothStep(8f, W, ax) * 1.5f - chan * 0.75f;
            };
            System.Func<float, float, float> faceZ = (x, y) =>
            {
                float mid = 1f - Spline.SmoothStep(5f, 8.5f, Mathf.Abs(x));
                float rough = Mathf.Lerp(1f, 0.3f, mid);
                float talus = Spline.SmoothStep(topY, yBot, y) * Mathf.Lerp(1.3f, 0.5f, mid);
                float z = (Noise(x * 0.45f + 3f, y * 0.6f) * 0.8f + Mathf.Sin(y * 1.9f + x * 0.3f) * 0.3f) * rough + talus - mid * 1.5f;
                return Mathf.Clamp(z, -2.2f, 1.6f);
            };

            // ---- rock mass: front face, top, back wall, side curtains
            Color32 rockA = C(124, 122, 116), rockB = C(178, 172, 160), wet = C(88, 98, 104), moss = C(112, 146, 84), grass = C(134, 170, 94);
            var g = new LifeGeo();
            g.Paint = p =>
            {
                float nv = Noise(p.x * 0.9f + p.z * 0.3f, p.y * 0.8f + p.z * 0.5f);
                float ax = Mathf.Abs(p.x);
                bool onTop = p.y > topY - 1.3f && p.z < -2.4f;
                if (onTop)
                {
                    if (ax < 5.5f) return LifeGeo.Shade(wet, 1f + 0.1f * nv);
                    return Color32.Lerp(moss, grass, 0.5f + 0.5f * nv);
                }
                if (ax < 7f && p.z > -3f && p.y < topY) return LifeGeo.Shade(wet, 1f + 0.12f * nv);
                if (p.y > topH(p.x, p.z) - 0.9f) return Color32.Lerp(moss, rockA, 0.35f + 0.3f * nv);
                Color32 rc = Color32.Lerp(rockA, rockB, 0.5f + 0.5f * nv);
                return p.y < WaterY + 0.4f ? LifeGeo.Shade(rc, 0.78f) : rc;
            };
            const int nx = 31;
            int ny = Mathf.CeilToInt((topY - yBot) / 1.1f);
            const int nz = 12;
            var face = new Vector3[nx, ny + 1];
            var top = new Vector3[nx, nz + 1];
            var back = new Vector3[nx, ny + 1];
            for (int i = 0; i < nx; i++)
            {
                float x = -W + i * (2f * W / (nx - 1));
                float yt = topH(x, 0f);
                for (int j = 0; j <= ny; j++)
                {
                    float y = Mathf.Lerp(yBot, yt, j / (float)ny);
                    float xj = (i == 0 || i == nx - 1) ? x : x + Noise(y * 0.7f, x) * 0.25f;
                    face[i, j] = new Vector3(xj, y, faceZ(x, y));
                }
                top[i, 0] = face[i, ny];
                for (int k = 1; k <= nz; k++)
                {
                    float z = -3f - (k - 1) * (L - 3f) / (nz - 1);
                    top[i, k] = new Vector3(x, topH(x, z), z);
                }
                back[i, 0] = top[i, nz];
                for (int j = 1; j <= ny; j++)
                {
                    float y = Mathf.Lerp(top[i, nz].y, yBot, j / (float)ny);
                    back[i, j] = new Vector3(x, y, -L + Noise(x * 0.5f, y * 0.7f) * 0.6f);
                }
            }
            for (int i = 0; i < nx - 1; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    g.QuadOut(face[i, j], face[i + 1, j], face[i + 1, j + 1], face[i, j + 1], Vector3.forward, rockA, rockA, rockA, rockA);
                    g.QuadOut(back[i, j], back[i + 1, j], back[i + 1, j + 1], back[i, j + 1], Vector3.back, rockA, rockA, rockA, rockA);
                }
                for (int k = 0; k < nz; k++)
                    g.QuadOut(top[i, k], top[i + 1, k], top[i + 1, k + 1], top[i, k + 1], Vector3.up, moss, moss, moss, moss);
            }
            for (int sIdx = 0; sIdx < 2; sIdx++)
            {
                int i = sIdx == 0 ? 0 : nx - 1;
                Vector3 outward = sIdx == 0 ? Vector3.left : Vector3.right;
                var chain = new List<Vector3>();
                for (int j = 0; j <= ny; j++) chain.Add(face[i, j]);
                for (int k = 1; k <= nz; k++) chain.Add(top[i, k]);
                for (int j = 1; j <= ny; j++) chain.Add(back[i, j]);
                for (int m = 0; m < chain.Count - 1; m++)
                {
                    Vector3 a = chain[m], b = chain[m + 1];
                    g.QuadOut(a, b, new Vector3(b.x, yBot - 0.1f, b.z), new Vector3(a.x, yBot - 0.1f, a.z), outward, rockA, rockA, rockA, rockA);
                }
            }
            // boulders at the foot, on the rim and two in the pool
            float lipZ = faceZ(0f, topH(0f, 0f));
            var poolRocks = new List<Vector4>();
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 4; k++)
                {
                    float x = s * Rand(6.5f, 20f), r = Rand(1.1f, 2.4f);
                    Boulder(g, new Vector3(x, WaterY - Rand(0.2f, 0.9f), faceZ(x, WaterY) + Rand(0.4f, 2.2f)), new Vector3(r, r * 0.75f, r), Rand(0f, 9f));
                }
                for (int k = 0; k < 3; k++)
                {
                    float x = s * Rand(7f, 19f), z = Rand(-12f, -4f), r = Rand(0.9f, 1.8f);
                    Boulder(g, new Vector3(x, topH(x, z) + 0.2f, z), new Vector3(r, r * 0.7f, r), Rand(0f, 9f));
                }
                float px = s * Rand(3f, 4.5f), pz = lipZ + Rand(4f, 6.5f), pr = Rand(0.6f, 0.95f);
                Boulder(g, new Vector3(px, WaterY - 0.45f, pz), new Vector3(pr, pr * 0.8f, pr), Rand(0f, 9f));
                poolRocks.Add(new Vector4(px, pz, pr, 0f));
            }
            var rockMesh = g.ToMesh("Waterfall_Rocks");
            var rocks = new GameObject("Waterfall_Rocks");
            rocks.transform.SetParent(root, false);
            rocks.AddComponent<MeshFilter>().sharedMesh = rockMesh;
            var mr = rocks.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.VertexColorWind;
            mr.shadowCastingMode = ShadowCastingMode.On;
            rocks.AddComponent<MeshCollider>().sharedMesh = rockMesh;
            rocks.AddComponent<SurfaceTag>().surface = Surface.Gravel;

            // ---- upper stream on top of the cliff
            float wTop = topY - 0.33f;
            BuildUpperWater(root, -5.6f, 5.6f, lipZ + 0.25f, -L + 0.6f, wTop);

            // ---- falling water: main sheet, a lighter front veil and two side trickles
            var fall = new FxMesh();
            AddSheet(fall, 0f, 3.3f, 4.3f, lipZ - 1.4f, lipZ + 0.12f, wTop, WaterY - 0.2f, 1.6f, 0.92f, 0f);
            AddSheet(fall, 0f, 3.6f, 4.8f, lipZ - 0.9f, lipZ + 0.45f, wTop - 0.04f, WaterY - 0.2f, 1.85f, 0.45f, 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 9.5f, zMax = -9f, yt = topH(x, 0f);
                for (int j = 0; j <= 12; j++) zMax = Mathf.Max(zMax, faceZ(x, Mathf.Lerp(yBot, yt, j / 12f)));
                AddSheet(fall, x, 0.7f, 0.95f, faceZ(x, yt) - 0.9f, zMax + 0.3f, yt + 0.06f, WaterY - 0.2f, 0f, 0.55f, 0.25f);
            }
            var fallGo = MakeFx("Waterfall_Water", fall, fallMat, root);
            if (fallGo != null) fallGo.transform.localPosition = Vector3.zero;

            // ---- churning pool, foam trail downstream, mist and spray
            Vector3 poolC = root.TransformPoint(new Vector3(0f, 0f, lipZ + 2.4f));
            poolC.y = WaterY + 0.035f;
            rings.Ring(poolC, 0.6f, 8f, 0.95f, 0f);
            rings.Ring(poolC + fwd * 0.5f, 0f, 3.6f, 0.8f, 0.2f);
            strips.Strip(poolC + fwd * 2f - right * 5.5f, fwd, right, 34f, 11f, 0f, 0.75f, 0f, 0.05f, 0.6f);
            foreach (var pr in poolRocks)
            {
                Vector3 rc = root.TransformPoint(new Vector3(pr.x, 0f, pr.y));
                rc.y = WaterY + 0.03f;
                rings.Ring(rc, pr.z * 0.85f, pr.z * 0.85f + 1.6f, 0.75f, 0f);
                strips.Strip(rc + fwd * pr.z * 0.6f - right * pr.z, fwd, right, 6f, pr.z * 2f, 0f, 0.6f, 0f, 0.1f, 0.7f);
            }
            for (int k = 0; k < 6; k++)
            {
                Vector3 fp = poolC + right * Rand(-3.5f, 3.5f) + fwd * Rand(-0.5f, 2.5f);
                for (int rep = 0; rep < 3; rep++) { foamPts.Add(fp); foamVel.Add(fwd * Rand(0.6f, 1.1f)); }
            }
            MakeSpray(poolC + Vector3.up * 0.3f, fwd, right, 8f);
            WaterfallPosition = poolC;
            HasWaterfall = true;
        }

        void BuildUpperWater(Transform parent, float x0, float x1, float zFront, float zBack, float y)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            const int seg = 8;
            for (int k = 0; k <= seg; k++)
            {
                float z = Mathf.Lerp(zFront, zBack, k / (float)seg);
                v.Add(new Vector3(x0, y, z));
                v.Add(new Vector3(x1, y, z));
                if (k > 0)
                {
                    int b = (k - 1) * 2;
                    // winding chosen so the face points up (z decreases with k)
                    t.Add(b); t.Add(b + 1); t.Add(b + 2);
                    t.Add(b + 1); t.Add(b + 3); t.Add(b + 2);
                }
            }
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 w = parent.TransformPoint(v[i]);
                uv.Add(new Vector2(w.x * 0.05f, w.z * 0.05f));
            }
            // make sure the first triangle faces up; flip all if not
            Vector3 nrm = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (nrm.y < 0f)
                for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            var m = new Mesh { name = "Waterfall_UpperStream" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            var go = new GameObject("Waterfall_UpperStream");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.Water;
            r.shadowCastingMode = ShadowCastingMode.Off;
            // a little foam streaming towards the lip
            var lip = new FxMesh();
            lip.Strip(new Vector3(x0 + 0.6f, y + 0.02f, zFront - 9f), Vector3.forward, Vector3.right, 9.2f, (x1 - x0) - 1.2f, 0f, 0.55f, 0f, 0.6f, 0.05f);
            MakeFx("Waterfall_LipFoam", lip, stripMat, parent);
        }

        /// <summary>A falling sheet: an approach row lying on the stream, then an arc down to the pool.</summary>
        static void AddSheet(FxMesh m, float xc, float h0, float h1, float zApp, float z0, float yTop, float yBottom, float arc, float alpha, float uOff)
        {
            const int cols = 6, rows = 14;
            float uCols = (h0 + h1) / 0.9f;
            int b = m.Count;
            float dist = 0f;
            Vector2 prev = new Vector2(zApp, yTop);
            for (int j = 0; j <= rows + 1; j++)
            {
                float y, z, hw, rowA;
                if (j == 0) { y = yTop + 0.02f; z = zApp; hw = h0 * 0.9f; rowA = 0.3f; }
                else
                {
                    float t = (j - 1) / (float)rows;
                    y = Mathf.Lerp(yTop, yBottom, t);
                    z = z0 + arc * Mathf.Sqrt(t);
                    hw = Mathf.Lerp(h0, h1, t);
                    rowA = j == rows + 1 ? 0.75f : 0.95f;
                }
                Vector2 cur = new Vector2(z, y);
                dist += Vector2.Distance(prev, cur);
                prev = cur;
                for (int i = 0; i <= cols; i++)
                {
                    float u = i / (float)cols;
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 5f);
                    m.Add(new Vector3(xc + (u * 2f - 1f) * hw, y, z), new Vector2(u * uCols + uOff, dist / 3f), alpha * rowA * edge);
                    if (i > 0 && j > 0)
                    {
                        int a = b + (j - 1) * (cols + 1) + i;
                        m.Quad(a - 1, a, a + cols + 1, a + cols);
                    }
                }
            }
        }

        void MakeSpray(Vector3 pos, Vector3 fwd, Vector3 right, float width)
        {
            Material mat = MaterialLibrary.Particle("WaterSpray", ProceduralTextures.Soft, Color.white, 0.45f);
            // soft mist rolling off the pool
            var go = new GameObject("Waterfall_Mist");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.26f), new Color(0.92f, 0.96f, 1f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 14f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(width, 0.6f, 2.5f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            FadeAndGrow(ps, 0.6f, 1.5f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.maxParticleSize = 1.5f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();

            // spray droplets thrown up where the sheet hits the pool
            var go2 = new GameObject("Waterfall_Spray");
            go2.transform.SetParent(transform, false);
            go2.transform.SetPositionAndRotation(pos - fwd * 1.2f, Quaternion.LookRotation(Vector3.up, fwd));
            var ps2 = go2.AddComponent<ParticleSystem>();
            ps2.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = ps2.main;
            m2.duration = 5f;
            m2.loop = true;
            m2.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m2.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            m2.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            m2.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.85f), new Color(0.9f, 0.95f, 1f, 0.55f));
            m2.gravityModifier = 1f;
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            m2.maxParticles = 160;
            m2.prewarm = true;
            var em2 = ps2.emission;
            em2.rateOverTime = 70f;
            var sh2 = ps2.shape;
            sh2.shapeType = ParticleSystemShapeType.Cone;
            sh2.angle = 28f;
            sh2.radius = 1f;
            sh2.scale = new Vector3(width * 0.45f, 0.8f, 1f);
            FadeAndGrow(ps2, 1f, 0.6f);
            var r2 = go2.GetComponent<ParticleSystemRenderer>();
            r2.renderMode = ParticleSystemRenderMode.Billboard;
            r2.sharedMaterial = mat;
            r2.shadowCastingMode = ShadowCastingMode.Off;
            r2.receiveShadows = false;
            ps2.Play();
        }

        static void FadeAndGrow(ParticleSystem ps, float s0, float s1)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, s0, 1f, s1));
        }

        // ================================================================== bridges + pavilion
        int NearestRiver(Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < river.Count; i++)
            {
                float d = (river[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void BuildBridgeFoam()
        {
            foreach (var bm in world.BridgeMarks)
            {
                Vector2 c = new Vector2(bm.x, bm.z);
                int i = NearestRiver(c);
                if ((river[i] - c).sqrMagnitude > 30f * 30f) continue;
                Vector2 d = (river[Mathf.Min(i + 1, river.Count - 1)] - river[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 nr = new Vector2(d.y, -d.x);
                Vector3 flow = new Vector3(d.x, 0f, d.y);
                Vector2 rp = river[i] + d * Vector2.Dot(c - river[i], d);     // river centre right under the bridge
                for (int side = -1; side <= 1; side += 2)
                {
                    float s = 2f;
                    bool found = false;
                    for (; s < 18f; s += 0.4f)
                    {
                        Vector2 q = rp + nr * (side * s);
                        if (world.TerrainHeight(q.x, q.y) > WaterY - 0.03f) { found = true; break; }
                    }
                    if (!found) continue;
                    Vector2 sh = rp + nr * (side * (s - 0.5f));
                    Vector3 shore = new Vector3(sh.x, WaterY + 0.03f, sh.y);
                    Vector3 inward = new Vector3(-nr.x * side, 0f, -nr.y * side);
                    // foam hugging the abutment under the arch, a churn ring where the current meets it
                    strips.Strip(shore - flow * 7f, flow, inward, 14f, 2.4f, 0.85f, 0.35f, 0f, 0.2f, 0.35f);
                    rings.Ring(shore - flow * 2.5f + inward * 0.3f, 0.2f, 2.1f, 0.7f, 0f);
                    for (int k = 0; k < 4; k++)
                    {
                        foamPts.Add(shore + flow * Rand(-5f, 4f) + inward * Rand(0.3f, 1.4f));
                        foamVel.Add(flow * Rand(0.4f, 0.8f));
                    }
                }
            }
        }

        void BuildPavilionRipples()
        {
            Vector2 pc = LifeShapes.PavilionCentre;
            const float sc = 1.3f;                                    // pavilion spawn scale
            var posts = new List<Vector2>();
            for (int k = 0; k < 6; k++)
            {
                float a = Mathf.PI / 6f + k * Mathf.PI / 3f;          // hexagonal stilts, radius 2.6 (model units)
                posts.Add(pc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2.6f * sc));
            }
            float[] walk = { -3f, -5.4f, -9.4f, -11.5f };             // straight walkway posts (+/-0.65 either side)
            foreach (float wz in walk)
                for (int s = -1; s <= 1; s += 2) posts.Add(pc + new Vector2(0.65f * s, wz) * sc);
            foreach (var p in posts)
            {
                if (world.TerrainHeight(p.x, p.y) > WaterY - 0.1f) continue;
                Vector3 c = new Vector3(p.x, WaterY + 0.03f, p.y);
                rings.Ring(c, 0.15f, 1.25f, 0.6f, 0f);
                foamPts.Add(c);
                foamVel.Add(Vector3.zero);
            }
        }

        // ================================================================== shared fx
        GameObject MakeFx(string name, FxMesh m, Material mat, Transform parent)
        {
            if (m.Count == 0) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m.ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        void FinishFx()
        {
            MakeFx("Water_FoamRings", rings, ringMat, transform);
            MakeFx("Water_FoamStrips", strips, stripMat, transform);
            foamP = foamPts.ToArray();
            foamV = foamVel.ToArray();
            foamRate = Mathf.Min(45f, foamP.Length * 1.2f);
            if (foamP.Length == 0) return;
            var go = new GameObject("Water_FoamFlecks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            FadeAndGrow(ps, 0.6f, 1.5f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = MaterialLibrary.Particle("FoamFleck", ProceduralTextures.Soft, Color.white, 0.6f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            flecks = ps;
        }

        void EmitFoam(float dt)
        {
            if (flecks == null) return;
            emitAcc += dt * foamRate;
            int guard = 0;
            while (emitAcc >= 1f && guard++ < 8)
            {
                emitAcc -= 1f;
                int k = rng.Next(foamP.Length);
                emit.position = foamP[k] + new Vector3(Rand(-0.5f, 0.5f), 0f, Rand(-0.5f, 0.5f));
                emit.velocity = foamV[k] + new Vector3(Rand(-0.08f, 0.08f), 0f, Rand(-0.08f, 0.08f));
                emit.startSize = Rand(0.25f, 0.6f);
                emit.startLifetime = Rand(1.6f, 2.8f);
                emit.startColor = new Color32(255, 255, 255, 190);
                flecks.Emit(emit, 1);
            }
            if (emitAcc > 8f) emitAcc = 0f;
        }
    }

    /// <summary>Unlit FX mesh builder (position, uv, vertex alpha) for the FlowFoam shader.</summary>
    internal sealed class FxMesh
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<int> t = new List<int>();
        public int Count { get { return v.Count; } }

        public void Add(Vector3 p, Vector2 u, float alpha)
        {
            v.Add(p);
            uv.Add(u);
            c.Add(new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f)));
        }

        public void Quad(int a, int b, int d, int e)
        {
            t.Add(a); t.Add(b); t.Add(d);
            t.Add(a); t.Add(d); t.Add(e);
        }

        /// <summary>Flat annulus on the water; V runs outwards (foam streams away from the centre).</summary>
        public void Ring(Vector3 ctr, float r0, float r1, float a0, float a1)
        {
            int seg = Mathf.Clamp(Mathf.RoundToInt(r1 * 6f), 12, 40);
            float cols = Mathf.Max(6f, Mathf.Round(Mathf.PI * 2f * r1 / 0.9f));
            float rows = Mathf.Max(1.2f, (r1 - r0) / 1.6f);
            int b = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float u = i / (float)seg * cols;
                Add(ctr + d * r0, new Vector2(u, 0f), a0);
                Add(ctr + d * r1, new Vector2(u, rows), a1);
                if (i > 0)
                {
                    int k = b + (i - 1) * 2;
                    Quad(k, k + 2, k + 3, k + 1);
                }
            }
        }

        /// <summary>Foam strip from 'start' along 'flow' (len), spanning 'across' (width) with alpha near/mid/far.</summary>
        public void Strip(Vector3 start, Vector3 flow, Vector3 across, float len, float width, float aNear, float aMid, float aFar, float fadeIn, float fadeOut)
        {
            int segs = Mathf.Clamp(Mathf.RoundToInt(len / 1.5f), 2, 40);
            float cols = Mathf.Max(2f, Mathf.Round(width / 0.8f));
            int b = v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float s = i / (float)segs;
                float env = Mathf.Clamp01(s / Mathf.Max(fadeIn, 1e-3f)) * Mathf.Clamp01((1f - s) / Mathf.Max(fadeOut, 1e-3f));
                Vector3 p = start + flow * (len * s);
                Add(p, new Vector2(0f, len * s / 3.5f), aNear * env);
                Add(p + across * (width * 0.5f), new Vector2(cols * 0.5f, len * s / 3.5f), aMid * env);
                Add(p + across * width, new Vector2(cols, len * s / 3.5f), aFar * env);
                if (i > 0)
                {
                    int k = b + (i - 1) * 3;
                    Quad(k, k + 1, k + 4, k + 3);
                    Quad(k + 1, k + 2, k + 5, k + 4);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}

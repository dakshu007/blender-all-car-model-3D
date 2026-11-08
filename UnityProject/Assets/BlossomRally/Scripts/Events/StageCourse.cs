// StageCourse.cs - rally event definitions, the course laid along a road's samples (start,
// checkpoints every ~175 m, finish) and the procedural checkpoint gates: red lacquer posts just
// outside the road edges, a dark crossbeam, a hanging banner and two glowing lanterns.
// Gates have no colliders - cars drive straight through them.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum StageKind { TimeAttack, Drift }

    public class StageDef
    {
        public string key, name, blurb;
        public int road;                 // index into WorldBuilder.roads / roadSamples
        public StageKind kind;
        public float startOffset;        // start line, metres along the road from its first sample
        public float endOffset;          // open roads: finish line this far before the last sample (closed: one full lap)
        public float goldSpeed = 20f;    // time attack: average speed (m/s) needed for gold
        public float limitSpeed = 12.5f; // drift: time limit = length / limitSpeed
        public int bronzePts, silverPts, goldPts;

        public const float SilverFactor = 1.15f;   // silver time = gold time x 1.15
        public const float BronzeFactor = 1.35f;   // bronze time = gold time x 1.35

        public static readonly StageDef[] All =
        {
            new StageDef { key = "ValleyLoop", name = "Valley Loop", blurb = "Time attack - one lap of the asphalt valley loop",
                           road = 0, kind = StageKind.TimeAttack, startOffset = 45f, goldSpeed = 30f },
            new StageDef { key = "PagodaClimb", name = "Pagoda Climb", blurb = "Time attack - asphalt hairpins up to the pagoda",
                           road = 3, kind = StageKind.TimeAttack, startOffset = 44f, endOffset = 14f, goldSpeed = 13.5f },
            new StageDef { key = "TeaTerrace", name = "Tea Terrace Sprint", blurb = "Time attack - gravel switchbacks through the tea terraces",
                           road = 1, kind = StageKind.TimeAttack, startOffset = 20f, endOffset = 45f, goldSpeed = 14f },
            new StageDef { key = "BambooRun", name = "Bamboo Run", blurb = "Time attack - fast dirt road through the bamboo forest",
                           road = 2, kind = StageKind.TimeAttack, startOffset = 20f, endOffset = 20f, goldSpeed = 18f },
            new StageDef { key = "PagodaDrift", name = "Pagoda Drift Zone", blurb = "Drift challenge - score drift points up the hairpins before time runs out",
                           road = 3, kind = StageKind.Drift, startOffset = 44f, endOffset = 14f, limitSpeed = 12.5f,
                           bronzePts = 2500, silverPts = 6000, goldPts = 10000 },
        };

        public float GoldTime(float length) { return length / goldSpeed; }
        public float SilverTime(float length) { return GoldTime(length) * SilverFactor; }
        public float BronzeTime(float length) { return GoldTime(length) * BronzeFactor; }
        public float TimeLimit(float length) { return length / limitSpeed; }

        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int MedalForTime(float t, float length)
        {
            if (t <= GoldTime(length)) return 3;
            if (t <= SilverTime(length)) return 2;
            if (t <= BronzeTime(length)) return 1;
            return 0;
        }

        public int MedalForPoints(float pts)
        {
            if (pts >= goldPts) return 3;
            if (pts >= silverPts) return 2;
            if (pts >= bronzePts) return 1;
            return 0;
        }
    }

    public class StageGate
    {
        public int sample;
        public float courseDist;      // metres from the start line
        public Vector3 centre, fwd, right;
        public float halfWidth;
        public GateVisual visual;
        public Texture2D texture;     // banner texture when this gate is the next one
    }

    public class StageCourse
    {
        public StageDef def;
        public List<RoadSample> samples;
        public bool closed;
        public float roadLength, startD, endD, length;
        public readonly List<StageGate> gates = new List<StageGate>();   // [0] start, [Count-1] finish
        public Vector3 spawnPos;
        public Quaternion spawnRot;
        public int spawnSample;
        public GameObject root;

        public int Checkpoints { get { return gates.Count - 1; } }

        public static StageCourse Build(StageDef def, WorldBuilder world, bool visuals)
        {
            if (world == null || def.road < 0 || def.road >= world.roadSamples.Count) return null;
            var s = world.roadSamples[def.road];
            if (s == null || s.Count < 4) return null;
            var c = new StageCourse { def = def, samples = s, closed = world.roads[def.road].closed };
            int n = s.Count;
            c.roadLength = c.closed ? s[n - 1].distance + Vector2.Distance(s[n - 1].pos, s[0].pos) : s[n - 1].distance;
            c.startD = def.startOffset;
            c.endD = c.closed ? c.startD + c.roadLength : c.roadLength - def.endOffset;
            c.length = c.endD - c.startD;
            if (c.length < 50f) return null;

            int segs = Mathf.Max(2, Mathf.RoundToInt(c.length / 175f));
            for (int k = 0; k <= segs; k++)
            {
                float d = c.startD + c.length * k / segs;
                int idx = c.IndexAt(d);
                if (k > 0 && k < segs) idx = c.AvoidBridge(c.Straightest(idx, 12));
                var rs = s[idx];
                var g = new StageGate
                {
                    sample = idx,
                    courseDist = c.length * k / segs,
                    centre = new Vector3(rs.pos.x, rs.height + 0.05f, rs.pos.y),
                    fwd = new Vector3(rs.tangent.x, 0f, rs.tangent.y).normalized,
                    right = new Vector3(rs.tangent.y, 0f, -rs.tangent.x).normalized,
                    halfWidth = rs.halfWidth
                };
                c.gates.Add(g);
            }
            c.spawnSample = c.IndexAt(c.startD - 6f);
            c.PoseAt(c.spawnSample, out c.spawnPos, out c.spawnRot);
            if (visuals) c.BuildVisuals(world);
            return c;
        }

        void BuildVisuals(WorldBuilder world)
        {
            root = new GameObject("BR_Stage_" + def.key);
            Texture2D start = GateBuilder.StartTexture, cp = GateBuilder.CheckpointTexture, fin = GateBuilder.FinishTexture;
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                bool isStart = i == 0, isFinish = i == gates.Count - 1;
                g.texture = isStart ? start : (isFinish ? fin : cp);
                if (isFinish && closed)
                {
                    g.visual = gates[0].visual;      // a lap finishes under the start gate
                    continue;
                }
                g.visual = GateBuilder.Build(root.transform, g, world, isStart ? "Start" : isFinish ? "Finish" : "Checkpoint" + i);
            }
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
            root = null;
        }

        float Dist(int i) { return samples[i].distance; }

        /// <summary>Sample index nearest to a distance along the road (wraps on closed roads).</summary>
        public int IndexAt(float d)
        {
            int n = samples.Count;
            if (closed) d = Mathf.Repeat(d, roadLength);
            else d = Mathf.Clamp(d, 0f, roadLength);
            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (Dist(mid) <= d) lo = mid; else hi = mid - 1;
            }
            if (lo + 1 < n) return Dist(lo + 1) - d < d - Dist(lo) ? lo + 1 : lo;
            if (closed && roadLength - d < d - Dist(lo)) return 0;
            return lo;
        }

        int Wrap(int i)
        {
            int n = samples.Count;
            return closed ? (i % n + n) % n : Mathf.Clamp(i, 0, n - 1);
        }

        /// <summary>How much the road turns (degrees) over +-10 m around a sample.</summary>
        float Turn(int i)
        {
            Vector2 a = samples[Wrap(i - 5)].tangent, b = samples[Wrap(i + 5)].tangent;
            return Vector2.Angle(a, b);
        }

        /// <summary>Keeps checkpoints out of hairpins: the straightest sample within +-range.</summary>
        int Straightest(int idx, int range)
        {
            int best = idx;
            float bt = Turn(idx);
            for (int k = 1; k <= range && bt > 4f; k++)
                for (int sgn = 1; sgn >= -1; sgn -= 2)
                {
                    int j = idx + k * sgn;
                    if (!closed && (j < 0 || j >= samples.Count)) continue;
                    j = Wrap(j);
                    float t = Turn(j);
                    if (t < bt - 0.5f) { bt = t; best = j; }
                }
            return best;
        }

        int AvoidBridge(int idx)
        {
            if (!samples[idx].bridge) return idx;
            int n = samples.Count;
            for (int k = 1; k < 40; k++)
            {
                int a = idx + k, b = idx - k;
                if (closed) { a %= n; b = (b + n) % n; }
                if (a < n && !samples[a].bridge) return a;
                if (b >= 0 && !samples[b].bridge) return b;
            }
            return idx;
        }

        /// <summary>Car pose on the road at a sample, facing along the course.</summary>
        public void PoseAt(int idx, out Vector3 pos, out Quaternion rot)
        {
            var rs = samples[Mathf.Clamp(idx, 0, samples.Count - 1)];
            pos = new Vector3(rs.pos.x, rs.height + 0.6f, rs.pos.y);
            Vector3 t = new Vector3(rs.tangent.x, 0f, rs.tangent.y);
            rot = Quaternion.LookRotation(t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward, Vector3.up);
        }

        /// <summary>Nearest sample to p. window &lt;= 0 searches the whole road, otherwise +-window around 'around'.</summary>
        public int Nearest(Vector3 p, int around, int window, out float dist)
        {
            int n = samples.Count, best = Mathf.Clamp(around, 0, n - 1);
            float bd = float.MaxValue;
            int from = window <= 0 ? 0 : around - window, to = window <= 0 ? n - 1 : around + window;
            for (int j = from; j <= to; j++)
            {
                int i = j;
                if (closed) i = (i % n + n) % n;
                else if (i < 0 || i >= n) continue;
                var rs = samples[i];
                float dx = rs.pos.x - p.x, dz = rs.pos.y - p.z, dy = (rs.height - p.y) * 2f;
                float d = dx * dx + dz * dz + dy * dy;
                if (d < bd) { bd = d; best = i; }
            }
            var b = samples[best];
            dist = new Vector2(b.pos.x - p.x, b.pos.y - p.z).magnitude;
            return best;
        }
    }

    /// <summary>Gate visual state: hidden, dimmed, or the pulsing "next" gate.</summary>
    public class GateVisual
    {
        public GameObject root;
        public Renderer[] banners;
        public Renderer[] lanterns;
        int state = -1;
        Texture lastTex;
        static MaterialPropertyBlock block;

        public const int Hidden = 0, Dim = 1, Next = 2;

        public void Apply(int s, Texture2D tex, float pulse)
        {
            if (root == null) return;
            bool changed = s != state || tex != lastTex;
            if (s != state) root.SetActive(s != Hidden);
            state = s;
            lastTex = tex;
            if (s == Hidden || (!changed && s != Next)) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color tint, emis, lamp;
            if (s == Next)
            {
                float k = 0.5f + 0.5f * pulse;
                tint = new Color(1f, 0.93f, 0.78f);
                emis = new Color(1f, 0.72f, 0.22f) * (0.25f + 0.85f * k);
                lamp = new Color(2.4f, 1.05f, 0.45f) * (0.85f + 0.3f * k);
            }
            else
            {
                tint = new Color(0.58f, 0.52f, 0.52f);
                emis = Color.black;
                lamp = new Color(0.9f, 0.38f, 0.18f);
            }
            foreach (var r in banners)
            {
                if (r == null) continue;
                block.Clear();
                if (tex != null) block.SetTexture("_MainTex", tex);
                block.SetColor("_Color", tint);
                block.SetColor("_EmissionColor", emis);
                r.SetPropertyBlock(block);
            }
            foreach (var r in lanterns)
            {
                if (r == null) continue;
                block.Clear();
                block.SetColor("_EmissionColor", lamp);
                r.SetPropertyBlock(block);
            }
        }
    }

    public static class GateBuilder
    {
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
        static Texture2D cpTex, finTex, startTex;
        static bool startLoaded;

        static Mesh Prim(PrimitiveType t)
        {
            Mesh m;
            if (meshes.TryGetValue(t, out m) && m != null) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            meshes[t] = m;
            return m;
        }

        static Material Lacquer { get { return MaterialLibrary.Standard("GateLacquer", new Color(0.62f, 0.06f, 0.08f), 0.72f, 0f); } }
        static Material Dark { get { return MaterialLibrary.Standard("GateDark", new Color(0.13f, 0.07f, 0.07f), 0.55f, 0f); } }
        static Material Stone { get { return MaterialLibrary.Standard("GateStone", new Color(0.46f, 0.43f, 0.40f), 0.15f, 0f); } }
        static Material Gold { get { return MaterialLibrary.Standard("GateGold", new Color(0.86f, 0.66f, 0.26f), 0.7f, 0.65f); } }
        static Material Banner { get { return MaterialLibrary.Standard("GateBanner", Color.white, 0.15f, 0f, null, Color.black); } }
        static Material Lantern { get { return MaterialLibrary.Standard("GateLantern", new Color(1f, 0.5f, 0.32f), 0.3f, 0f, null, new Color(0.9f, 0.38f, 0.18f)); } }

        static Renderer Part(Transform parent, string name, PrimitiveType t, Vector3 pos, Vector3 scale, Material m, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Prim(t);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        public static GateVisual Build(Transform parent, StageGate g, WorldBuilder world, string name)
        {
            var root = new GameObject("Gate_" + name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(g.centre, Quaternion.LookRotation(g.fwd, Vector3.up));
            Transform t = root.transform;
            float hw = g.halfWidth, px = hw + 0.8f, top = 5.4f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 w = g.centre + g.right * (side * px);
                float ground = world != null ? world.HeightAt(w.x, w.z) - g.centre.y : 0f;
                float bottom = Mathf.Clamp(Mathf.Min(-0.3f, ground - 0.3f), -6f, -0.3f);
                Part(t, "Post", PrimitiveType.Cylinder, new Vector3(side * px, (bottom + top) * 0.5f, 0f),
                     new Vector3(0.34f, (top - bottom) * 0.5f, 0.34f), Lacquer);
                Part(t, "PostBase", PrimitiveType.Cube, new Vector3(side * px, Mathf.Max(bottom + 0.25f, ground + 0.22f), 0f),
                     new Vector3(0.62f, 0.5f, 0.62f), Stone);
                Part(t, "PostCap", PrimitiveType.Cube, new Vector3(side * px, top - 0.05f, 0f), new Vector3(0.44f, 0.12f, 0.44f), Gold);
            }
            Part(t, "Kasagi", PrimitiveType.Cube, new Vector3(0f, 5.3f, 0f), new Vector3(2f * px + 1.5f, 0.32f, 0.52f), Dark);
            Part(t, "Trim", PrimitiveType.Cube, new Vector3(0f, 5.12f, 0f), new Vector3(2f * px + 1.52f, 0.06f, 0.54f), Gold);
            Part(t, "Nuki", PrimitiveType.Cube, new Vector3(0f, 4.72f, 0f), new Vector3(2f * px + 0.6f, 0.24f, 0.32f), Lacquer);

            // banner hanging from the lower beam (front face towards the approaching car, back face for the way back)
            float bw = Mathf.Min(2f * hw * 0.78f, 6.4f), bh = Mathf.Min(bw * 0.25f, 1.5f);
            float by = 4.58f - 0.1f - bh * 0.5f;
            Part(t, "BannerFrame", PrimitiveType.Cube, new Vector3(0f, by, 0f), new Vector3(bw + 0.14f, bh + 0.14f, 0.04f), Dark);
            var front = Part(t, "BannerFront", PrimitiveType.Quad, new Vector3(0f, by, -0.03f), new Vector3(bw, bh, 1f), Banner);
            var back = Part(t, "BannerBack", PrimitiveType.Quad, new Vector3(0f, by, 0.03f), new Vector3(bw, bh, 1f), Banner);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            for (int side = -1; side <= 1; side += 2)
                Part(t, "Tie", PrimitiveType.Cube, new Vector3(side * bw * 0.4f, (by + bh * 0.5f + 4.6f) * 0.5f, 0f),
                     new Vector3(0.04f, 4.6f - (by + bh * 0.5f), 0.04f), Dark, false);

            // two small lanterns between the banner and the posts
            var lamps = new List<Renderer>();
            float lx = Mathf.Max(bw * 0.5f + 0.4f, px - 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * lx;
                Part(t, "LanternCord", PrimitiveType.Cube, new Vector3(x, 4.5f, 0f), new Vector3(0.03f, 0.3f, 0.03f), Dark, false);
                Part(t, "LanternTop", PrimitiveType.Cylinder, new Vector3(x, 4.36f, 0f), new Vector3(0.3f, 0.04f, 0.3f), Dark, false);
                lamps.Add(Part(t, "Lantern", PrimitiveType.Sphere, new Vector3(x, 4.04f, 0f), new Vector3(0.5f, 0.62f, 0.5f), Lantern, false));
                Part(t, "LanternBottom", PrimitiveType.Cylinder, new Vector3(x, 3.72f, 0f), new Vector3(0.28f, 0.04f, 0.28f), Dark, false);
            }
            return new GateVisual { root = root, banners = new Renderer[] { front, back }, lanterns = lamps.ToArray() };
        }

        // ---------------------------------------------------------------- banner textures
        public static Texture2D StartTexture
        {
            get
            {
                if (!startLoaded)
                {
                    startLoaded = true;
                    startTex = Resources.Load<Texture2D>("Env/ENV_Banner_Start");
                }
                return startTex != null ? startTex : CheckpointTexture;
            }
        }

        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Red = new Color(0.74f, 0.09f, 0.13f);
        static readonly Color GoldC = new Color(0.86f, 0.66f, 0.26f);
        static readonly Color Pink = new Color(0.96f, 0.58f, 0.70f);
        static readonly Color Ink = new Color(0.08f, 0.06f, 0.07f);

        static Texture2D NewTex(string name)
        {
            return new Texture2D(256, 64, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
        }

        public static Texture2D CheckpointTexture
        {
            get
            {
                if (cpTex != null) return cpTex;
                cpTex = NewTex("BR_GateCheckpoint");
                var px = new Color[256 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        Color c = Cream;
                        if (y < 9 || y >= 55) c = Red;
                        else if (y < 11 || y >= 53) c = GoldC;
                        else
                        {
                            for (int f = 0; f < 3; f++)
                            {
                                float dx = x - (64f + f * 64f) + 0.5f, dy = y - 32f + 0.5f;
                                float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                                float petal = 15f * (0.55f + 0.45f * Mathf.Abs(Mathf.Cos(2.5f * a + 0.3f)));
                                c = Color.Lerp(c, Pink, Mathf.Clamp01(petal - r + 0.5f));
                                c = Color.Lerp(c, GoldC, Mathf.Clamp01(3.5f - r + 0.5f));
                            }
                        }
                        px[y * 256 + x] = c;
                    }
                cpTex.SetPixels(px);
                cpTex.Apply(true, true);
                return cpTex;
            }
        }

        public static Texture2D FinishTexture
        {
            get
            {
                if (finTex != null) return finTex;
                finTex = NewTex("BR_GateFinish");
                var px = new Color[256 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        Color c;
                        if (y < 16 || y >= 48) c = (((x / 16) + (y / 16)) & 1) == 0 ? Ink : Cream;
                        else if (y < 18 || y >= 46) c = GoldC;
                        else c = Red;
                        px[y * 256 + x] = c;
                    }
                // checkered flag square in the middle band
                for (int y = 20; y < 44; y++)
                    for (int x = 104; x < 152; x++)
                        px[y * 256 + x] = ((((x - 104) / 8) + ((y - 20) / 8)) & 1) == 0 ? Ink : Cream;
                finTex.SetPixels(px);
                finTex.Apply(true, true);
                return finTex;
            }
        }
    }
}

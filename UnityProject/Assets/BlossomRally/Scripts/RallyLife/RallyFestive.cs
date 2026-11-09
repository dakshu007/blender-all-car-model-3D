// RallyFestive.cs - festival touches: fireworks over the lake at night (NightFactor > 0.6) and a
// celebration burst over the finish gantry when an event ends with a medal (GameEvents.Finished,
// medal >= 1, any time of day); wind chimes under village eaves whose rods swing with Atmosphere.Gust
// and ring softly (procedural, 3D) near the camera; strings of prayer flags between blossom trees and
// bamboo poles around the pagoda, swaying through the vertex-wind shader (which follows the gust).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyFestive : RallyModule
    {
        // ---- fireworks
        struct Rocket { public Vector3 pos, vel; public float fuse; public Color32 a, b; public bool live, big; }
        struct PendingSound { public float at; public Vector3 pos; public bool burst; public float vol; }
        ParticleSystem ps;
        readonly Rocket[] rockets = new Rocket[12];
        readonly List<PendingSound> sounds = new List<PendingSound>();
        float nextShow, sparkScale = 1f;
        int celebrateLeft;
        float celebrateNext;
        Vector3 celebratePos;
        int celebrateMedal;
        static readonly Color32[] Palette =
        {
            new Color32(255, 70, 80, 255), new Color32(255, 200, 70, 255), new Color32(255, 140, 200, 255),
            new Color32(120, 200, 255, 255), new Color32(140, 255, 150, 255), new Color32(255, 250, 230, 255), new Color32(200, 120, 255, 255)
        };

        // ---- chimes
        sealed class Chime { public int top; public int[] rods = new int[5]; public Vector3 hook; public float phase; }
        LifeBatch chimeBatch;
        readonly List<Chime> chimes = new List<Chime>();
        static readonly float[] RodLen = { 0.42f, 0.36f, 0.31f, 0.27f, 0.23f };
        float chimeTimer, chimeCheck;
        int nearChime = -1;
        float nearDist = 999f;

        public int ChimeCount { get { return chimes.Count; } }
        public int FlagStrings { get; private set; }
        public override string Summary { get { return chimes.Count + " chimes, " + FlagStrings + " prayer-flag strings"; } }

        public override void Build()
        {
            BuildFireworks();
            BuildChimes();
            BuildPrayerFlags();
        }

        // ================================================================ fireworks
        void BuildFireworks()
        {
            var go = new GameObject("Fireworks");
            go.transform.SetParent(Root, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.32f;
            main.maxParticles = 4000;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(200f);
            lim.drag = new ParticleSystem.MinMaxCurve(1.1f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(MaterialLibrary.FindShader("BlossomRally/Firework")) { name = "BR_Firework" };
            mat.mainTexture = ProceduralTextures.Soft;
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            sparkScale = L.Density;
            nextShow = R(2f, 5f);
        }

        void Launch(Vector3 from, Vector3 vel, float fuse, bool big)
        {
            for (int i = 0; i < rockets.Length; i++)
            {
                if (rockets[i].live) continue;
                Color32 a = Palette[RI(Palette.Length)], b = Chance(0.4f) ? Palette[RI(Palette.Length)] : a;
                rockets[i] = new Rocket { pos = from, vel = vel, fuse = fuse, a = a, b = b, live = true, big = big };
                Sound(from, false, big ? 0.6f : 0.45f);
                return;
            }
        }

        /// <summary>Medal fireworks over the finish (called from RallyLife on GameEvents.Finished).</summary>
        public void Celebrate(Vector3 pos, int medal)
        {
            celebratePos = pos;
            celebrateMedal = Mathf.Clamp(medal, 1, 3);
            celebrateLeft = 2 + celebrateMedal * 2;
            celebrateNext = 0f;
        }

        void Sound(Vector3 pos, bool burst, float vol)
        {
            float delay = (pos - L.Eye).magnitude / 343f;
            sounds.Add(new PendingSound { at = L.Clock + delay, pos = pos, burst = burst, vol = vol });
        }

        void Burst(ref Rocket rk)
        {
            var ep = new ParticleSystem.EmitParams();
            int n = Mathf.RoundToInt((rk.big ? 110 : 70) * Mathf.Clamp(sparkScale, 0.5f, 1.25f));
            float speed = rk.big ? R(13f, 17f) : R(9f, 12f);
            bool ring = Chance(0.25f);
            Vector3 ringN = Random.onUnitSphere;
            for (int k = 0; k < n; k++)
            {
                Vector3 d = Random.onUnitSphere;
                if (ring) d = Vector3.ProjectOnPlane(d, ringN).normalized;
                ep.position = rk.pos;
                ep.velocity = d * speed * R(0.85f, 1.05f);
                ep.startColor = k % 3 == 0 ? rk.b : rk.a;
                ep.startSize = rk.big ? R(1.1f, 1.6f) : R(0.8f, 1.2f);
                ep.startLifetime = R(1.5f, 2.3f);
                ps.Emit(ep, 1);
            }
            // a few glittering slow sparks
            for (int k = 0; k < n / 4; k++)
            {
                ep.position = rk.pos;
                ep.velocity = Random.onUnitSphere * R(2f, 6f);
                ep.startColor = new Color32(255, 236, 190, 255);
                ep.startSize = R(0.4f, 0.7f);
                ep.startLifetime = R(2f, 3f);
                ps.Emit(ep, 1);
            }
            Sound(rk.pos, true, rk.big ? 1f : 0.8f);
        }

        void UpdateFireworks(float dt)
        {
            if (ps == null) return;
            // lake show at night
            if (Atmosphere.NightFactor > 0.6f)
            {
                nextShow -= dt;
                if (nextShow <= 0f)
                {
                    Vector2 lc = MapData.LakeCenter;
                    Vector3 from = new Vector3(lc.x + R(-20f, 20f), WorldBuilder.WaterY + 0.5f, lc.y + R(2f, 16f));
                    Launch(from, new Vector3(R(-3f, 3f), R(30f, 37f), R(-3f, 3f)), R(1.5f, 2.1f), Chance(0.35f));
                    nextShow = Chance(0.25f) ? R(0.3f, 0.7f) : R(1.4f, 3.6f);
                }
            }
            // medal celebration
            if (celebrateLeft > 0)
            {
                celebrateNext -= dt;
                if (celebrateNext <= 0f)
                {
                    celebrateLeft--;
                    celebrateNext = R(0.25f, 0.5f);
                    Vector3 from = celebratePos + new Vector3(R(-7f, 7f), -2f, R(-7f, 7f));
                    Launch(from, new Vector3(R(-2f, 2f), R(22f, 27f), R(-2f, 2f)), R(0.9f, 1.3f), celebrateMedal == 3 || Chance(0.4f));
                }
            }
            Vector3 g = Physics.gravity * 0.3f;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < rockets.Length; i++)
            {
                if (!rockets[i].live) continue;
                rockets[i].vel += g * dt;
                rockets[i].pos += rockets[i].vel * dt;
                rockets[i].fuse -= dt;
                // trail
                ep.position = rockets[i].pos;
                ep.velocity = -rockets[i].vel * 0.05f + Random.insideUnitSphere * 0.4f;
                ep.startColor = new Color32(255, 196, 120, 200);
                ep.startSize = 0.45f;
                ep.startLifetime = 0.45f;
                ps.Emit(ep, 1);
                if (rockets[i].fuse <= 0f)
                {
                    Burst(ref rockets[i]);
                    rockets[i].live = false;
                }
            }
            for (int i = sounds.Count - 1; i >= 0; i--)
            {
                if (sounds[i].at > L.Clock) continue;
                var s = sounds[i];
                L.Audio.Play(s.burst ? RallyAudio.Burst : RallyAudio.Launch, s.pos, s.vol, R(0.9f, 1.1f), s.burst ? 40f : 15f, s.burst ? 900f : 300f);
                sounds.RemoveAt(i);
            }
        }

        // ================================================================ wind chimes
        void BuildChimes()
        {
            var root = GameObject.Find("World_Static");
            if (root == null) return;
            int want = Mathf.Clamp(Mathf.RoundToInt(8 * L.Density), 3, 10);
            var hooks = new List<Vector3>();
            Transform rt = root.transform;
            var houses = new List<Transform>();
            for (int i = 0; i < rt.childCount; i++)
            {
                Transform h = rt.GetChild(i);
                if (h.name.StartsWith("ENV_HouseTwoStorey") || h.name.StartsWith("ENV_HouseShop")) houses.Add(h);
            }
            for (int i = houses.Count - 1; i > 0; i--) { int j = RI(i + 1); var t = houses[i]; houses[i] = houses[j]; houses[j] = t; }
            foreach (var h in houses)
            {
                if (hooks.Count >= want) break;
                Vector3 mn, mx;
                if (!LocalBounds(h, out mn, out mx)) continue;
                Vector3 ext = mx - mn;
                if (ext.x < ext.z) continue;                     // unexpected orientation: skip
                float halfLong = ext.x * 0.5f, halfShort = ext.z * 0.5f;
                float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f;
                int side = Chance(0.5f) ? 1 : -1;
                Vector3 local = new Vector3(cx + side * halfLong * 0.45f, 3.12f, cz - halfShort + 0.5f);
                hooks.Add(h.TransformPoint(local));
            }
            if (hooks.Count == 0) return;
            chimeBatch = new LifeBatch();
            int topT = chimeBatch.AddTemplate(RallyShapes.ChimeTop());
            var rodT = new int[5];
            for (int k = 0; k < 5; k++) rodT[k] = chimeBatch.AddTemplate(RallyShapes.ChimeRod(RodLen[k]));
            foreach (var hk in hooks)
            {
                var c = new Chime { top = chimeBatch.AddPart(topT), hook = hk, phase = R(0f, 20f) };
                for (int k = 0; k < 5; k++) c.rods[k] = chimeBatch.AddPart(rodT[k]);
                chimes.Add(c);
            }
            chimeBatch.Finish("WindChimes", MaterialLibrary.VertexColorWind, Root, 0, false);
            for (int i = 0; i < chimes.Count; i++) PoseChime(chimes[i]);
            chimeBatch.Upload();
        }

        static bool LocalBounds(Transform h, out Vector3 mn, out Vector3 mx)
        {
            mn = Vector3.one * 1e9f;
            mx = -Vector3.one * 1e9f;
            Matrix4x4 w2l = h.worldToLocalMatrix;
            foreach (var mf in h.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = w2l * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 q = m.MultiplyPoint3x4(c);
                    mn = Vector3.Min(mn, q);
                    mx = Vector3.Max(mx, q);
                }
            }
            return mx.x > mn.x && mx.y > 3.5f;
        }

        void PoseChime(Chime c)
        {
            float t = L.Clock + c.phase;
            float gust = Atmosphere.Gust;
            float amp = 3f + gust * 14f;
            Quaternion sway = Quaternion.Euler(Mathf.Sin(t * 0.9f) * amp * 0.4f, 0f, Mathf.Cos(t * 0.7f) * amp * 0.4f);
            Matrix4x4 top = Matrix4x4.TRS(c.hook, sway, Vector3.one);
            chimeBatch.Set(c.top, top);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f;
                Vector3 at = new Vector3(Mathf.Cos(a) * 0.09f, -0.26f, Mathf.Sin(a) * 0.09f);
                float w = t * (2.1f + k * 0.37f);
                Quaternion r = Quaternion.Euler(Mathf.Sin(w) * amp, 0f, Mathf.Cos(w * 1.13f) * amp);
                chimeBatch.Set(c.rods[k], top * Matrix4x4.TRS(at, r, Vector3.one));
            }
        }

        void UpdateChimes(float dt)
        {
            if (chimeBatch == null) return;
            chimeCheck -= dt;
            if (chimeCheck <= 0f)
            {
                chimeCheck = 0.3f;
                nearChime = -1;
                nearDist = 999f;
                for (int i = 0; i < chimes.Count; i++)
                {
                    float d = (chimes[i].hook - L.Eye).magnitude;
                    if (d < nearDist) { nearDist = d; nearChime = i; }
                }
            }
            bool any = false;
            for (int i = 0; i < chimes.Count; i++)
            {
                if ((chimes[i].hook - L.Eye).sqrMagnitude > 70f * 70f) continue;
                PoseChime(chimes[i]);
                any = true;
            }
            if (any) chimeBatch.Upload();
            // soft notes near the camera, more often in a gust
            if (nearChime >= 0 && nearDist < 30f)
            {
                chimeTimer -= dt * (0.35f + Atmosphere.Gust * 2.2f);
                if (chimeTimer <= 0f)
                {
                    chimeTimer = R(0.4f, 1.6f);
                    float vol = 0.32f * Mathf.Clamp01(1.1f - nearDist / 30f);
                    L.Audio.Play(RallyAudio.Chime(RI(5)), chimes[nearChime].hook, vol, 1f, 3f, 45f);
                }
            }
        }

        // ================================================================ prayer flags
        void BuildPrayerFlags()
        {
            var anchors = new List<Vector3>();      // top points to string between
            var pairs = new List<KeyValuePair<Vector3, Vector3>>();
            var root = GameObject.Find("World_Static");
            Vector2 pg = MapData.PagodaTop;
            if (root != null)
            {
                Transform rt = root.transform;
                var trees = new List<Vector3>();
                for (int i = 0; i < rt.childCount; i++)
                {
                    Transform ch = rt.GetChild(i);
                    if (!ch.name.StartsWith("ENV_Cherry")) continue;
                    Vector2 p = new Vector2(ch.position.x, ch.position.z);
                    if (Vector2.Distance(p, pg) < 140f || MapData.InVillage(p) || Vector2.Distance(p, MapData.Gate) < 80f) trees.Add(ch.position);
                }
                int want = Mathf.Clamp(Mathf.RoundToInt(8 * L.Density), 3, 10);
                var used = new HashSet<int>();
                for (int i = 0; i < trees.Count && pairs.Count < want; i++)
                {
                    if (used.Contains(i)) continue;
                    for (int j = i + 1; j < trees.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        float d = Vector3.Distance(trees[i], trees[j]);
                        if (d < 7f || d > 15f) continue;
                        if (!LineClear(trees[i], trees[j])) continue;
                        float h = 3.6f;
                        pairs.Add(new KeyValuePair<Vector3, Vector3>(trees[i] + Vector3.up * h, trees[j] + Vector3.up * h));
                        used.Add(i); used.Add(j);
                        break;
                    }
                }
            }
            // a ring of bamboo poles round the pagoda, flags strung pole to pole
            var poles = new List<Vector3>();
            var pole = RallyShapes.BambooPole(5.2f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.2f;
                Vector2 p = pg + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17f;
                if (!TryPlace(p, 0.4f, 2.5f, 1.5f)) { poles.Add(Vector3.positiveInfinity); continue; }
                Vector3 g = L.Ground(p);
                L.Static.Add(pole, g - Vector3.up * 0.2f, Quaternion.identity);
                poles.Add(g + Vector3.up * 4.9f);
            }
            for (int k = 0; k < poles.Count; k++)
            {
                Vector3 a = poles[k], b = poles[(k + 1) % poles.Count];
                if (float.IsInfinity(a.x) || float.IsInfinity(b.x)) continue;
                if (Vector3.Distance(a, b) > 16f || !LineClear(a, b)) continue;
                pairs.Add(new KeyValuePair<Vector3, Vector3>(a, b));
            }
            foreach (var pr in pairs) FlagString(pr.Key, pr.Value);
            FlagStrings = pairs.Count;
        }

        bool LineClear(Vector3 a, Vector3 b)
        {
            for (int k = 0; k <= 6; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / 6f);
                if (L.EdgeDist(new Vector2(p.x, p.z)) < 1f) return false;
            }
            return true;
        }

        static readonly Color32[] FlagCols =
        {
            new Color32(40, 90, 200, 0), new Color32(246, 246, 240, 0), new Color32(214, 40, 40, 0), new Color32(40, 160, 80, 0), new Color32(246, 206, 50, 0)
        };

        void FlagString(Vector3 a, Vector3 b)
        {
            var g = L.Static.At(a);
            float len = Vector3.Distance(a, b);
            float sag = len * 0.07f;
            Vector3 dir = (b - a) / len;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Color32 cord = LifeGeo.C(200, 190, 170, 60);
            int segs = Mathf.Max(4, Mathf.RoundToInt(len / 0.8f));
            for (int k = 0; k < segs; k++)
            {
                float t0 = k / (float)segs, t1 = (k + 1) / (float)segs;
                Vector3 p0 = Vector3.Lerp(a, b, t0) + Vector3.down * (sag * 4f * t0 * (1f - t0));
                Vector3 p1 = Vector3.Lerp(a, b, t1) + Vector3.down * (sag * 4f * t1 * (1f - t1));
                g.Tri2(p0, p1, p1 + Vector3.down * 0.02f, cord, cord, cord);
                g.Tri2(p0, p1 + Vector3.down * 0.02f, p0 + Vector3.down * 0.02f, cord, cord, cord);
            }
            int n = Mathf.FloorToInt(len / 0.48f);
            for (int k = 1; k < n; k++)
            {
                float t = k / (float)n;
                Vector3 top = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                Color32 c = FlagCols[k % FlagCols.Length];
                Color32 ct = c, cb = c;
                ct.a = 70;
                cb.a = 255;
                Vector3 l = top - dir * 0.15f, r = top + dir * 0.15f;
                Vector3 lb = l + Vector3.down * 0.36f + side * 0.02f, rb = r + Vector3.down * 0.36f + side * 0.02f;
                g.Tri2(l, r, rb, ct, ct, cb);
                g.Tri2(l, rb, lb, ct, cb, cb);
            }
        }

        // ================================================================ update
        public override void Tick(float dt)
        {
            UpdateFireworks(dt);
            UpdateChimes(dt);
        }

        public override void Shutdown()
        {
            if (ps != null) ps.Clear();
        }
    }
}

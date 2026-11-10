// PropFX.cs - cheap ambient effects for WorldProps: tea steam / noodle steam / incense smoke, butterflies
// over flowers (day), dragonflies over water (day), fireflies (night), falling bamboo leaves around the
// camera inside the bamboo forest, plus the procedural temple bell and tower drum clips.
// One particle system per effect (5 draw calls max), particles emitted by hand only near the camera,
// emission checks throttled to a few times per second, no per-frame allocations.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    internal sealed class PropFX
    {
        public readonly List<Vector3> steam = new List<Vector3>();
        public readonly List<Vector3> incense = new List<Vector3>();
        public readonly List<Vector3> flowers = new List<Vector3>();
        public readonly List<Vector3> water = new List<Vector3>();
        public readonly List<Vector3> meadow = new List<Vector3>();   // extra firefly spots (grass / paddies)

        Vector3[] steamA, incA, flowA, waterA, nightA;
        ParticleSystem smoke, butterflies, dragonflies, fireflies, leaves;
        ParticleSystem.EmitParams ep;
        readonly System.Random rng = new System.Random(4711);
        float smokeT, bugT;
        public int Systems { get; private set; }

        float Rnd(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        public void Build(Transform parent)
        {
            steamA = steam.ToArray(); incA = incense.ToArray(); flowA = flowers.ToArray(); waterA = water.ToArray();
            var night = new List<Vector3>(water); night.AddRange(meadow); night.AddRange(flowers);
            nightA = night.ToArray();
            int q = GameSettings.Quality;

            smoke = Make(parent, "Props_FX_Smoke", 260 + q * 60, MaterialLibrary.Particle("PropSmoke", ProceduralTextures.Soft, Color.white, 0.75f));
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sz = smoke.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));
            var nz = smoke.noise;
            nz.enabled = true;
            nz.strength = 0.35f;
            nz.frequency = 0.6f;
            nz.scrollSpeed = 0.3f;
            nz.quality = ParticleSystemNoiseQuality.Low;

            butterflies = Make(parent, "Props_FX_Butterflies", 40 + q * 12, MaterialLibrary.Particle("PropButterfly", ButterflyTex(), Color.white, 0.5f));
            Flipbook(butterflies, 90f);
            Wander(butterflies, 1.1f, 0.35f);
            FadeInOut(butterflies);

            dragonflies = Make(parent, "Props_FX_Dragonflies", 24 + q * 8, MaterialLibrary.Particle("PropDragonfly", DragonflyTex(), Color.white, 0.5f));
            Flipbook(dragonflies, 160f);
            Wander(dragonflies, 2.4f, 0.7f);
            FadeInOut(dragonflies);

            var fm = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_PropFirefly" };
            fm.SetTexture("_MainTex", ProceduralTextures.Soft);
            fm.SetColor("_Tint", new Color(1f, 1f, 1f, 1f));
            fm.SetVector("_Fade", new Vector4(0.5f, 2f, 90f, 120f));
            fm.SetFloat("_EdgeFade", 0f);
            fm.SetFloat("_VertexGamma", 0f);
            fireflies = Make(parent, "Props_FX_Fireflies", 80 + q * 30, fm);
            Wander(fireflies, 0.5f, 0.3f);
            var fc = fireflies.colorOverLifetime;
            fc.enabled = true;
            var fg = new Gradient();
            fg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.15f, 0.35f), new GradientAlphaKey(1f, 0.55f),
                               new GradientAlphaKey(0.2f, 0.75f), new GradientAlphaKey(0.9f, 0.88f), new GradientAlphaKey(0f, 1f) });
            fc.color = new ParticleSystem.MinMaxGradient(fg);

            leaves = Make(parent, "Props_FX_BambooLeaves", 160 + q * 60, MaterialLibrary.Particle("PropLeaf", LeafTex(), Color.white, 0.7f));
            var lm = leaves.main;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            lm.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            lm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.82f, 0.38f, 1f), new Color(0.86f, 0.74f, 0.4f, 1f));
            lm.gravityModifier = 0.025f;
            var le = leaves.emission;
            le.enabled = true;
            le.rateOverTime = 0f;
            var ls = leaves.shape;
            ls.enabled = true;
            ls.shapeType = ParticleSystemShapeType.Box;
            ls.scale = new Vector3(60f, 2f, 60f);
            var lv = leaves.velocityOverLifetime;
            lv.enabled = true;
            lv.space = ParticleSystemSimulationSpace.World;
            lv.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            lv.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.2f);
            lv.z = new ParticleSystem.MinMaxCurve(0.0f, 0.25f);
            var lr = leaves.rotationOverLifetime;
            lr.enabled = true;
            lr.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            Wander(leaves, 0.6f, 0.4f);
            FadeInOut(leaves);
            foreach (var ps in new[] { smoke, butterflies, dragonflies, fireflies, leaves }) ps.Play();
            Systems = 5;
        }

        static ParticleSystem Make(Transform parent, string name, int max, Material mat)
        {
            var go = new GameObject(name);
            go.layer = WorldBuilder.LayerTrees;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            main.playOnAwake = false;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        static void Flipbook(ParticleSystem ps, float cycles)
        {
            var ts = ps.textureSheetAnimation;
            ts.enabled = true;
            ts.numTilesX = 2;
            ts.numTilesY = 1;
            ts.animation = ParticleSystemAnimationType.WholeSheet;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            ts.cycleCount = Mathf.RoundToInt(cycles);
        }

        static void Wander(ParticleSystem ps, float strength, float freq)
        {
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = strength;
            nz.frequency = freq;
            nz.scrollSpeed = 0.25f;
            nz.damping = true;
            nz.quality = ParticleSystemNoiseQuality.Low;
        }

        static void FadeInOut(ParticleSystem ps)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        static readonly Color[] Wings =
        {
            new Color(1f, 1f, 0.96f), new Color(1f, 0.9f, 0.35f), new Color(1f, 0.62f, 0.25f), new Color(0.55f, 0.75f, 1f), new Color(1f, 0.72f, 0.85f)
        };
        static readonly Color[] Darts = { new Color(0.35f, 0.85f, 0.9f), new Color(0.95f, 0.35f, 0.3f), new Color(0.4f, 0.55f, 1f) };

        public void Tick(float dt, Vector3 eye, bool inBamboo)
        {
            if (smoke == null) return;
            float night = Atmosphere.NightFactor, rain = Atmosphere.RainIntensity;
            int q = GameSettings.Quality;
            // steam & incense: a few times per second, only sources near the camera
            smokeT += dt;
            if (smokeT >= 0.1f)
            {
                float step = smokeT;
                smokeT = 0f;
                EmitSmoke(steamA, eye, step, 3.2f, new Color(1f, 1f, 1f, 0.45f), 0.38f, 2.6f, 0.45f);
                EmitSmoke(incA, eye, step, 2.4f, new Color(0.84f, 0.85f, 0.9f, 0.4f), 1.1f, 5.5f, 0.5f);
            }
            bugT += dt;
            if (bugT >= 0.4f)
            {
                bugT = 0f;
                bool day = night < 0.35f && rain < 0.25f;
                if (day && flowA.Length > 0 && butterflies.particleCount < butterflies.main.maxParticles - 2)
                    EmitAround(butterflies, flowA, eye, 70f, 1 + q / 2, 0.4f, 2.0f, 12f, 0.32f, Wings);
                if (day && waterA.Length > 0 && dragonflies.particleCount < dragonflies.main.maxParticles - 2)
                    EmitAround(dragonflies, waterA, eye, 70f, 1, 0.5f, 1.6f, 9f, 0.34f, Darts);
                if (night > 0.45f && rain < 0.4f && nightA.Length > 0 && fireflies.particleCount < fireflies.main.maxParticles - 4)
                    EmitAround(fireflies, nightA, eye, 80f, 3 + q, 0.3f, 2.2f, 6.5f, 0.16f, null);
            }
            var em = leaves.emission;
            if (inBamboo)
            {
                leaves.transform.position = eye + Vector3.up * 13f;
                em.rateOverTime = 14f + q * 6f;
            }
            else em.rateOverTime = 0f;
        }

        void EmitSmoke(Vector3[] src, Vector3 eye, float step, float rate, Color c, float size, float life, float rise)
        {
            if (src == null) return;
            float wx = ValleyLife.Wind.x * 0.25f, wz = ValleyLife.Wind.z * 0.25f;
            for (int i = 0; i < src.Length; i++)
            {
                if ((src[i] - eye).sqrMagnitude > 90f * 90f) continue;
                if (rng.NextDouble() > rate * step) continue;
                ep = new ParticleSystem.EmitParams();
                ep.position = src[i] + new Vector3(Rnd(-0.05f, 0.05f), 0f, Rnd(-0.05f, 0.05f));
                ep.velocity = new Vector3(wx + Rnd(-0.05f, 0.05f), rise * Rnd(0.8f, 1.2f), wz + Rnd(-0.05f, 0.05f));
                ep.startColor = c;
                ep.startSize = size * Rnd(0.8f, 1.25f);
                ep.startLifetime = life * Rnd(0.8f, 1.2f);
                ep.rotation = Rnd(0f, 360f);
                smoke.Emit(ep, 1);
            }
        }

        void EmitAround(ParticleSystem ps, Vector3[] spots, Vector3 eye, float radius, int count, float hMin, float hMax, float life, float size, Color[] cols)
        {
            float r2 = radius * radius;
            for (int n = 0; n < count; n++)
            {
                // a few random picks; keep the first one near the camera
                for (int tries = 0; tries < 6; tries++)
                {
                    Vector3 s = spots[rng.Next(spots.Length)];
                    if ((s - eye).sqrMagnitude > r2) continue;
                    ep = new ParticleSystem.EmitParams();
                    ep.position = s + new Vector3(Rnd(-2.5f, 2.5f), Rnd(hMin, hMax), Rnd(-2.5f, 2.5f));
                    ep.velocity = new Vector3(Rnd(-0.3f, 0.3f), Rnd(-0.05f, 0.1f), Rnd(-0.3f, 0.3f));
                    ep.startLifetime = life * Rnd(0.85f, 1.15f);
                    ep.startSize = size * Rnd(0.8f, 1.2f);
                    ep.startColor = cols != null ? cols[rng.Next(cols.Length)] : new Color(0.85f, 1f, 0.45f, 1f);
                    ps.Emit(ep, 1);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ textures
        static Texture2D butterfly, dragonfly, leaf;

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static float Ell(float x, float y, float cx, float cy, float rx, float ry, float rot)
        {
            float dx = x - cx, dy = y - cy;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float u = (dx * cs + dy * sn) / rx, v = (-dx * sn + dy * cs) / ry;
            return Mathf.Clamp01((1f - (u * u + v * v)) * 6f);
        }

        static Texture2D ButterflyTex()
        {
            if (butterfly != null) return butterfly;
            const int W = 128, H = 64;
            var px = new Color32[W * H];
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float u = (x + 0.5f) / 64f * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                        float sx = f == 0 ? 1f : 0.32f;                       // wings open / folded
                        float au = Mathf.Abs(u) / sx;
                        float up = Ell(au, v, 0.42f, 0.25f, 0.42f, 0.38f, 0.35f);
                        float lo = Ell(au, v, 0.32f, -0.3f, 0.3f, 0.28f, -0.4f);
                        float wing = Mathf.Max(up, lo);
                        float body = Ell(u, v, 0f, 0f, 0.07f, 0.6f, 0f);
                        float edge = Mathf.Clamp01((1f - wing) * 3f);
                        float spot = Ell(au, v, 0.55f, 0.35f, 0.1f, 0.1f, 0f);
                        float shade = Mathf.Lerp(1f, 0.45f, edge * 0.8f) * (1f - spot * 0.5f);
                        Color c = new Color(shade, shade, shade, wing * 0.95f);
                        if (body > 0.01f) c = Color.Lerp(c, new Color(0.15f, 0.12f, 0.12f, 1f), body);
                        px[y * W + f * 64 + x] = c;
                    }
            butterfly = NewTex(W, H);
            butterfly.SetPixels32(px);
            butterfly.Apply(true);
            return butterfly;
        }

        static Texture2D DragonflyTex()
        {
            if (dragonfly != null) return dragonfly;
            const int W = 128, H = 64;
            var px = new Color32[W * H];
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float u = (x + 0.5f) / 64f * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                        float tilt = f == 0 ? 0.12f : -0.2f;
                        float au = Mathf.Abs(u);
                        float w1 = Ell(au, v, 0.45f, 0.2f + tilt, 0.48f, 0.1f, 0.08f);
                        float w2 = Ell(au, v, 0.42f, -0.02f - tilt, 0.44f, 0.09f, -0.12f);
                        float wing = Mathf.Max(w1, w2) * 0.5f;
                        float body = Mathf.Max(Ell(u, v, 0f, -0.25f, 0.045f, 0.72f, 0f), Ell(u, v, 0f, 0.42f, 0.1f, 0.09f, 0f));
                        Color c = new Color(0.9f, 0.95f, 1f, wing);
                        if (body > 0.01f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 1f), body);
                        px[y * W + f * 64 + x] = c;
                    }
            dragonfly = NewTex(W, H);
            dragonfly.SetPixels32(px);
            dragonfly.Apply(true);
            return dragonfly;
        }

        static Texture2D LeafTex()
        {
            if (leaf != null) return leaf;
            const int S = 64;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float a = Ell(u, v, 0f, 0f, 0.95f, 0.2f, 0.785f);
                    float rib = Mathf.Clamp01(1f - Mathf.Abs(u - v) * 12f);
                    float s = 1f - rib * 0.25f;
                    px[y * S + x] = new Color(s, s, s * 0.95f, a);
                }
            leaf = NewTex(S, S);
            leaf.SetPixels32(px);
            leaf.Apply(true);
            return leaf;
        }

        // ------------------------------------------------------------------ bell & drum synthesis
        public static AudioClip TempleBell()
        {
            const int rate = 22050;
            var b = new float[rate * 7];
            float f = 138f;
            float[] ratio = { 0.5f, 1f, 1.19f, 1.5f, 2.0f, 2.52f, 3.03f, 4.1f };
            float[] amp = { 0.55f, 1f, 0.45f, 0.4f, 0.35f, 0.22f, 0.15f, 0.08f };
            float[] dec = { 2.4f, 1.6f, 1.1f, 0.9f, 0.7f, 0.45f, 0.3f, 0.2f };
            for (int i = 0; i < ratio.Length; i++)
            {
                ProcAudio.Tone(b, 0, rate, f * ratio[i], amp[i] * 0.5f, dec[i], 0.004f);
                ProcAudio.Tone(b, 0, rate, f * ratio[i] * 1.004f + 0.35f, amp[i] * 0.45f, dec[i] * 0.9f, 0.004f);   // slow beating
            }
            var xr = new XRng(991);
            ProcAudio.NoiseBurst(b, 0, rate, 420f, 0.8f, 0.03f, 0.5f, ref xr);   // the wooden log's thud
            ProcAudio.Finish(b, 0.9f);
            return ProcAudio.MakeClip("BR_TempleBell", b, rate, 1);
        }

        public static AudioClip TowerDrum()
        {
            const int rate = 22050;
            var b = new float[(int)(rate * 3.2f)];
            float[] hits = { 0f, 0.55f, 1.1f, 1.38f };
            float[] loud = { 1f, 0.8f, 1f, 0.7f };
            var xr = new XRng(313);
            for (int h = 0; h < hits.Length; h++)
            {
                int s = (int)(hits[h] * rate);
                ProcAudio.Tone(b, s, rate, 58f, loud[h], 0.32f, 0.003f);
                ProcAudio.Tone(b, s, rate, 92f, loud[h] * 0.5f, 0.18f, 0.002f);
                ProcAudio.Tone(b, s, rate, 151f, loud[h] * 0.25f, 0.08f, 0.002f);
                ProcAudio.NoiseBurst(b, s, rate, 260f, 0.7f, 0.035f, loud[h] * 0.6f, ref xr);
            }
            ProcAudio.Finish(b, 0.95f);
            return ProcAudio.MakeClip("BR_TowerDrum", b, rate, 1);
        }
    }
}

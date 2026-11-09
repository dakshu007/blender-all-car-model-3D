// JuiceFX.cs - shared world-space particle systems for the juice layer (one set for the whole game, so
// effects keep lingering after a car is swapped): lingering dust clouds that drift with the wind,
// petal / leaf blasts, water splash sheets + droplets, and finish-line confetti. Cars push particles in
// with Emit(EmitParams) (no allocations); this component keeps the wind drift and quality budget updated.
using UnityEngine;

namespace BlossomRally
{
    public class JuiceFX : MonoBehaviour
    {
        public static JuiceFX Instance { get; private set; }

        ParticleSystem dust, petals, sheets, droplets, confetti;
        ParticleSystem.EmitParams ep;
        float windTimer;
        int quality = -1;

        /// <summary>Particle budget multiplier from the quality setting (Low 0.35 .. Ultra 1.3).</summary>
        public static float Density { get; private set; } = 1f;

        void Awake()
        {
            Instance = this;
            dust = MakeDust();
            petals = MakePetals();
            sheets = MakeSheets();
            droplets = MakeDroplets();
            confetti = MakeConfetti();
            ApplyQuality();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void ApplyQuality()
        {
            quality = GameSettings.Quality;
            Density = quality == 0 ? 0.35f : quality == 1 ? 0.6f : quality == 2 ? 1f : 1.3f;
            var m = dust.main; m.maxParticles = (int)(420 * Density);
            m = petals.main; m.maxParticles = (int)(700 * Density);
            m = sheets.main; m.maxParticles = (int)(320 * Density);
            m = droplets.main; m.maxParticles = (int)(600 * Density);
            var n = dust.noise; n.enabled = quality > 0;
            n = petals.noise; n.enabled = quality > 0;
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (quality != GameSettings.Quality) ApplyQuality();
            windTimer -= Time.deltaTime;
            if (windTimer > 0f) return;
            windTimer = 0.2f;
            // dust drifts downwind (valley wind direction, stronger in gusts), rising lazily; the velocity
            // module is an added (animated) velocity, so the slow kick from the tyres is kept as well
            Vector3 w = ValleyLife.Wind * (0.6f + 2.2f * Atmosphere.Gust);
            var v = dust.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(w.x);
            v.y = new ParticleSystem.MinMaxCurve(0.12f - 0.25f * Atmosphere.RainIntensity);
            v.z = new ParticleSystem.MinMaxCurve(w.z);
        }

        // ------------------------------------------------------------------ emit API
        public void Dust(Vector3 pos, Vector3 vel, Color32 col, float size)
        {
            ep.position = pos; ep.velocity = vel; ep.startColor = col; ep.startSize = size;
            ep.rotation = Random.Range(0f, 360f);
            ep.startLifetime = Random.Range(5f, 8.5f);
            dust.Emit(ep, 1);
        }

        public void Petal(Vector3 pos, Vector3 vel, Color32 col, float size)
        {
            ep.position = pos; ep.velocity = vel; ep.startColor = col; ep.startSize = size;
            ep.rotation = Random.Range(0f, 360f);
            ep.startLifetime = Random.Range(1.6f, 3.2f);
            petals.Emit(ep, 1);
        }

        public void Sheet(Vector3 pos, Vector3 vel, float size, float alpha)
        {
            ep.position = pos; ep.velocity = vel; ep.startSize = size;
            ep.startColor = new Color(0.93f, 0.97f, 1f, alpha);
            ep.rotation = 0f;
            ep.startLifetime = Random.Range(0.35f, 0.7f);
            sheets.Emit(ep, 1);
        }

        public void Droplet(Vector3 pos, Vector3 vel, float size)
        {
            ep.position = pos; ep.velocity = vel; ep.startSize = size;
            ep.startColor = new Color(0.9f, 0.95f, 1f, 0.85f);
            ep.rotation = 0f;
            ep.startLifetime = Random.Range(0.5f, 1.1f);
            droplets.Emit(ep, 1);
        }

        static readonly Color32[] ConfettiCols =
        {
            new Color32(255, 170, 200, 255), new Color32(255, 120, 165, 255), new Color32(255, 250, 248, 255),
            new Color32(255, 214, 110, 255), new Color32(255, 196, 64, 255), new Color32(255, 225, 235, 255)
        };

        /// <summary>A burst of pink / white / gold confetti petals shot upwards from pos.</summary>
        public void ConfettiBurst(Vector3 pos, Vector3 up, int count, float power)
        {
            count = Mathf.Max(8, (int)(count * Mathf.Min(1f, Density + 0.2f)));
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (up + Random.insideUnitSphere * 0.75f).normalized;
                ep.position = pos + Random.insideUnitSphere * 0.4f;
                ep.velocity = dir * Random.Range(5f, 11f) * power;
                ep.startColor = ConfettiCols[Random.Range(0, ConfettiCols.Length)];
                ep.startSize = Random.Range(0.13f, 0.24f);
                ep.rotation = Random.Range(0f, 360f);
                ep.startLifetime = Random.Range(4f, 6.5f);
                confetti.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ systems
        ParticleSystem NewSystem(string name, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            return ps;
        }

        static Gradient Fade(float a0, float aIn, float tIn, float aMid, float tMid)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(aIn, tIn), new GradientAlphaKey(aMid, tMid), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void Finish(ParticleSystem ps, Material m, ParticleSystemSortMode sort)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = sort;
            ps.Play();
        }

        ParticleSystem MakeDust()
        {
            var ps = NewSystem("JuiceDust", 420);
            var main = ps.main;
            main.gravityModifier = -0.004f;
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(0f); v.z = new ParticleSystem.MinMaxCurve(0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 2.2f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0f, 1f, 0.06f, 0.65f, 0.5f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            Finish(ps, MaterialLibrary.Particle("JuiceDust", ProceduralTextures.Soft, Color.white, 0.7f), ParticleSystemSortMode.Distance);
            return ps;
        }

        ParticleSystem MakePetals()
        {
            var ps = NewSystem("JuicePetals", 700);
            var main = ps.main;
            main.gravityModifier = 0.11f;
            var lim = ps.limitVelocityOverLifetime;   // air drag: flung fast, then float
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(1.6f);
            lim.dampen = 0.08f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.5f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(1f, 1f, 0.05f, 1f, 0.75f));
            Finish(ps, MaterialLibrary.Particle("JuicePetal", JuiceTextures.Petal, Color.white, 0.8f), ParticleSystemSortMode.None);
            return ps;
        }

        ParticleSystem MakeSheets()
        {
            var ps = NewSystem("JuiceSplashSheets", 320);
            var main = ps.main;
            main.gravityModifier = 1.5f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.6f, 1f, 0.1f, 0.7f, 0.5f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.07f;
            r.lengthScale = 1.4f;
            Finish(ps, MaterialLibrary.Particle("JuiceSplash", ProceduralTextures.Soft, Color.white, 0.55f), ParticleSystemSortMode.None);
            return ps;
        }

        ParticleSystem MakeDroplets()
        {
            var ps = NewSystem("JuiceDroplets", 600);
            var main = ps.main;
            main.gravityModifier = 2.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(1f, 1f, 0.05f, 1f, 0.7f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1f;
            Finish(ps, MaterialLibrary.Particle("JuiceDroplet", ProceduralTextures.Soft, Color.white, 0.5f), ParticleSystemSortMode.None);
            return ps;
        }

        ParticleSystem MakeConfetti()
        {
            var ps = NewSystem("JuiceConfetti", 900);
            var main = ps.main;
            main.gravityModifier = 0.2f;
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(1.4f);
            lim.dampen = 0.07f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.1f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-7f, 7f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(1f, 1f, 0.02f, 1f, 0.8f));
            Finish(ps, MaterialLibrary.Particle("JuiceConfetti", JuiceTextures.Confetti, Color.white, 0.35f), ParticleSystemSortMode.None);
            return ps;
        }
    }

    /// <summary>Small white textures (tinted per particle): petal / leaf and a confetti flake.</summary>
    internal static class JuiceTextures
    {
        static Texture2D petal, confetti, disc, seal;

        public static Texture2D Petal
        {
            get
            {
                if (petal != null) return petal;
                const int s = 32;
                petal = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "BR_JuicePetal", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        float w = 0.6f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (0.7f + 0.3f * (1f - v));
                        float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 10f);
                        float shade = 0.8f + 0.2f * (1f - Mathf.Abs(u) / 0.6f) - (Mathf.Abs(u) < 0.05f ? 0.08f : 0f);
                        petal.SetPixel(x, y, new Color(shade, shade, shade, a));
                    }
                petal.Apply(true);
                return petal;
            }
        }

        public static Texture2D Confetti
        {
            get
            {
                if (confetti != null) return confetti;
                const int s = 32;
                confetti = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "BR_JuiceConfetti", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        // rounded rectangle flake, half petal-shaped for variety in rotation
                        float u = Mathf.Abs((x + 0.5f) / s * 2f - 1f), v = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                        float dx = Mathf.Max(0f, u - 0.45f), dy = Mathf.Max(0f, v - 0.75f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((0.2f - d) * 14f) * (u < 0.65f && v < 0.95f ? 1f : 0f);
                        float shade = 0.85f + 0.15f * (1f - u);
                        confetti.SetPixel(x, y, new Color(shade, shade, shade, a));
                    }
                confetti.Apply(true);
                return confetti;
            }
        }

        /// <summary>Medal disc (white, shaded rim) for the results stamp.</summary>
        public static Texture2D Disc
        {
            get
            {
                if (disc != null) return disc;
                const int s = 128;
                disc = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "BR_JuiceDisc", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Clamp01((0.98f - r) * 60f);
                        // raised rim, engraved inner ring, soft top-left light
                        float light = 0.78f + 0.22f * Mathf.Clamp01(0.5f - 0.5f * (u - v) * 0.7f);
                        float shade = r > 0.84f ? 1.05f - (r - 0.84f) * 1.5f : (r > 0.74f && r < 0.78f ? 0.72f : 0.92f);
                        float c = Mathf.Clamp01(shade * light);
                        disc.SetPixel(x, y, new Color(c, c, c, a));
                    }
                disc.Apply(true);
                return disc;
            }
        }

        /// <summary>Red seal (Chinese chop style): square with a rough edge and an inner border, white = tint.</summary>
        public static Texture2D Seal
        {
            get
            {
                if (seal != null) return seal;
                const int s = 128;
                seal = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "BR_JuiceSeal", wrapMode = TextureWrapMode.Clamp };
                var rng = new XRng(77u);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / s * 2f - 1f), v = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                        float edge = Mathf.Max(u, v);
                        float rough = 0.9f + 0.03f * Mathf.Sin(x * 0.9f + y * 0.37f) + 0.02f * rng.Signed;
                        float a = Mathf.Clamp01((rough - edge) * 40f);
                        bool innerLine = edge > 0.72f && edge < 0.78f;
                        float ink = innerLine ? 0.35f : 1f;
                        ink *= 0.85f + 0.15f * rng.Value;      // uneven ink
                        seal.SetPixel(x, y, new Color(ink, ink, ink, a * (innerLine ? 0.6f : 1f)));
                    }
                seal.Apply(true);
                return seal;
            }
        }
    }
}

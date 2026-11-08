// MaterialLibrary.cs - shared runtime materials + remapping of imported FBX materials to the
// Blossom Rally shaders (vertex-colour wind, lantern glow, glass, emissive lights...).
// Remapping at runtime makes the look independent of asset-import order or settings.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static Material Get(string key, System.Func<Material> make)
        {
            Material m;
            if (!cache.TryGetValue(key, out m) || m == null)
            {
                m = make();
                m.name = "BR_" + key;
                cache[key] = m;
            }
            return m;
        }

        public static Material VertexColorWind
        {
            get { return Get("VertexColorWind", () => new Material(FindShader("BlossomRally/VertexColorWind"))); }
        }

        public static Material Terrain
        {
            get
            {
                return Get("Terrain", () =>
                {
                    var m = new Material(FindShader("BlossomRally/VertexColorWind"));
                    m.SetFloat("_WindStrength", 0f);
                    m.SetFloat("_Glossiness", 0.05f);
                    return m;
                });
            }
        }

        public static Material Grass
        {
            get { return Get("Grass", () => new Material(FindShader("BlossomRally/Grass"))); }
        }

        public static Material Glow
        {
            get { return Get("Glow", () => new Material(FindShader("BlossomRally/VertexColorGlow"))); }
        }

        public static Material Water
        {
            get { return Get("Water", () => new Material(FindShader("BlossomRally/Water"))); }
        }

        public static Material Glass
        {
            get { return Get("Glass", () => new Material(FindShader("BlossomRally/Glass"))); }
        }

        public static Material Sky
        {
            get { return Get("Sky", () => new Material(FindShader("BlossomRally/Sky"))); }
        }

        public static Material Particle(string key, Texture2D tex, Color tint, float lit)
        {
            return Get("Particle_" + key, () =>
            {
                var m = new Material(FindShader("BlossomRally/Particle"));
                m.SetTexture("_MainTex", tex);
                m.SetColor("_Tint", tint);
                m.SetFloat("_Lit", lit);
                return m;
            });
        }

        public static Material Standard(string key, Color c, float smooth, float metal, Texture2D tex = null, Color? emission = null)
        {
            return Get("Std_" + key, () =>
            {
                var m = new Material(FindShader("Standard"));
                m.SetColor("_Color", c);
                m.SetFloat("_Glossiness", smooth);
                m.SetFloat("_Metallic", metal);
                if (tex != null) m.SetTexture("_MainTex", tex);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return m;
            });
        }

        public static Material TryGet(string key)
        {
            Material m;
            return cache.TryGetValue(key, out m) ? m : null;
        }

        /// <summary>Road surface with wet / puddle / rain-ring support (BR_Road).</summary>
        public static Material Road(string key, Texture2D tex, float smooth)
        {
            return Get(key, () =>
            {
                var sh = Shader.Find("BlossomRally/Road");
                if (sh == null) return Standard(key + "_Fallback", Color.white, smooth, 0f, tex);
                var m = new Material(sh);
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_Glossiness", smooth);
                return m;
            });
        }

        /// <summary>Per-car paint (livery recolour, patterns, dirt) - one material per car id.</summary>
        public static Material CarPaint(string carId)
        {
            return Get("Paint_" + carId, () =>
            {
                var t = Resources.Load<Texture2D>("Cars/" + carId + "/" + carId + "_BaseColor");
                var sh = Shader.Find("BlossomRally/CarPaint");
                Material m;
                if (sh == null) { m = new Material(FindShader("Standard")); m.SetFloat("_Glossiness", 0.55f); }
                else m = new Material(sh);
                if (t != null) m.SetTexture("_MainTex", t);
                m.SetColor("_PaintKey", LiveryLibrary.PaintKey(carId));
                return m;
            });
        }

        static float nightLevel = -1f;
        static bool brakeOn;

        /// <summary>Headlights / tail lights glow brighter at night.</summary>
        public static void SetNightLights(float night)
        {
            if (Mathf.Abs(night - nightLevel) < 0.01f) return;
            nightLevel = night;
            var h = TryGet("M_Light_Head");
            if (h != null) h.SetColor("_EmissionColor", new Color(1.6f, 1.5f, 1.3f) * (1f + night * 3f));
            var a = TryGet("M_Light_Aux");
            if (a != null) a.SetColor("_EmissionColor", new Color(1.8f, 1.5f, 0.9f) * (1f + night * 2.5f));
            ApplyTail();
        }

        public static void SetBrakeLights(bool on)
        {
            if (on == brakeOn) return;
            brakeOn = on;
            ApplyTail();
        }

        static void ApplyTail()
        {
            var t = TryGet("M_Light_Tail");
            if (t == null) return;
            float k = (1f + Mathf.Max(0f, nightLevel) * 1.5f) * (brakeOn ? 2.6f : 1f);
            t.SetColor("_EmissionColor", new Color(1.4f, 0.1f, 0.06f) * k);
        }

        static string CleanName(Material m)
        {
            return m == null ? "" : m.name.Replace(" (Instance)", "").Trim();
        }

        /// <summary>Swap every imported material under root for the matching Blossom Rally material.</summary>
        public static void Remap(GameObject root, string carId = null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Map(mats[i], carId);
                r.sharedMaterials = mats;
            }
        }

        public static Material Map(Material src, string carId)
        {
            string n = CleanName(src);
            Color c = (src != null && src.HasProperty("_Color")) ? src.color : Color.white;
            if (n == "M_Env_VertexColor" || n == "M_Env_Terrain") return VertexColorWind;
            if (n == "M_Env_LanternGlow") return Glow;
            if (n == "M_Env_Water") return Water;
            if (n == "M_Glass_Tint") return Glass;
            if (n.StartsWith("M_ENV_"))
            {
                var t = Resources.Load<Texture2D>("Env/" + n.Substring(2));
                return Standard(n, Color.white, 0.2f, 0f, t);
            }
            if (n.StartsWith("M_Env_Road_"))
            {
                var t = Resources.Load<Texture2D>("Env/ENV_Road_" + n.Substring("M_Env_Road_".Length));
                return Standard(n, Color.white, 0.12f, 0f, t);
            }
            if (carId != null && n.EndsWith("_Paint")) return CarPaint(carId);
            if (carId != null && n.EndsWith("_Plate"))
            {
                var t = Resources.Load<Texture2D>("Cars/" + carId + "/" + carId + "_BaseColor");
                return Standard(n, Color.white, n.EndsWith("_Paint") ? 0.55f : 0.3f, 0f, t);
            }
            if (n == "M_Light_Head") return Standard(n, new Color(1f, 0.97f, 0.9f), 0.8f, 0f, null, new Color(1.6f, 1.5f, 1.3f));
            if (n == "M_Light_Aux") return Standard(n, new Color(1f, 0.93f, 0.75f), 0.8f, 0f, null, new Color(1.8f, 1.5f, 0.9f));
            if (n == "M_Light_Tail") return Standard(n, new Color(0.7f, 0.06f, 0.06f), 0.7f, 0f, null, new Color(1.4f, 0.1f, 0.06f));
            if (n == "M_Light_Indicator") return Standard(n, new Color(0.95f, 0.5f, 0.1f), 0.7f, 0f, null, new Color(1.2f, 0.5f, 0.08f));
            if (n == "M_Chrome") return Standard(n, new Color(0.85f, 0.85f, 0.83f), 0.85f, 1f);
            if (n == "M_Brake_Disc") return Standard(n, new Color(0.45f, 0.44f, 0.43f), 0.55f, 0.8f);
            if (n.EndsWith("_Rim")) return Standard(n + carId, c, 0.6f, 0.8f);
            if (n == "M_Tire_Rubber") return Standard(n, new Color(0.13f, 0.12f, 0.12f), 0.12f, 0f);
            if (n.StartsWith("M_Trim")) return Standard(n, c, 0.3f, 0f);
            if (src == null) return Standard("Fallback", Color.gray, 0.3f, 0f);
            return Standard(n + (carId ?? ""), c, 0.3f, 0f);
        }
    }

    public static class ProceduralTextures
    {
        static Texture2D petal, soft, skid;

        public static Texture2D Petal
        {
            get
            {
                if (petal != null) return petal;
                const int s = 64;
                petal = new Texture2D(s, s, TextureFormat.RGBA32, true);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        // rounded petal with a small notch at the tip
                        float w = 0.62f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (0.75f + 0.25f * (1f - v));
                        float notch = v > 0.72f ? Mathf.Clamp01(1f - Mathf.Abs(u) * 6f) : 0f;
                        float a = Mathf.Clamp01((w - Mathf.Abs(u)) * 14f) * (1f - notch);
                        float shade = 0.82f + 0.18f * (1f - Mathf.Abs(u) / 0.62f);
                        petal.SetPixel(x, y, new Color(1f, 0.80f * shade + 0.1f, 0.86f * shade + 0.08f, a));
                    }
                petal.Apply(true);
                petal.wrapMode = TextureWrapMode.Clamp;
                return petal;
            }
        }

        public static Texture2D Soft
        {
            get
            {
                if (soft != null) return soft;
                const int s = 64;
                soft = new Texture2D(s, s, TextureFormat.RGBA32, true);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = Mathf.Clamp01(1f - d);
                        soft.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                    }
                soft.Apply(true);
                soft.wrapMode = TextureWrapMode.Clamp;
                return soft;
            }
        }

        public static Texture2D Skid
        {
            get
            {
                if (skid != null) return skid;
                skid = new Texture2D(32, 8, TextureFormat.RGBA32, false);
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float v = (y + 0.5f) / 8f;
                        float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2.2f) * (0.75f + 0.25f * Mathf.Sin(x * 1.3f));
                        skid.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                skid.Apply();
                skid.wrapMode = TextureWrapMode.Repeat;
                return skid;
            }
        }
    }
}

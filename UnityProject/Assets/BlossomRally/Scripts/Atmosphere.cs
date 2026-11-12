// Atmosphere.cs - light, sky, fog and weather for the valley.
// Two independent choices are combined: TIME OF DAY (Morning, Spring Day, Golden Sunset, Blue Hour,
// Night) and WEATHER (Clear Spring, Spring Haze, Cloudy, Rain, After Rain, Fog Off). Changes blend
// smoothly (~2.5 s). Also drives: drifting cloud shadows (sun cookie), stars + moon, lantern glow,
// wet roads (global _BR_Wetness / _BR_Rain), a car reflection probe, wind gusts.
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class Atmosphere : MonoBehaviour
    {
        public static float Gust { get; private set; }
        public static Light Sun { get; private set; }
        /// <summary>0 = full day .. 1 = night (lanterns glow, headlights on).</summary>
        public static float NightFactor { get; private set; }
        /// <summary>0 = dry .. 1 = soaked roads.</summary>
        public static float Wetness { get; private set; }
        /// <summary>0 = no rain .. 1 = heavy rain.</summary>
        public static float RainIntensity { get; private set; }
        /// <summary>0 = clear sky .. 1 = overcast.</summary>
        public static float CloudCover { get; private set; }
        /// <summary>Mist particle amount the petal system should use (particles / second).</summary>
        public static float MistRate { get; private set; }
        public static Atmosphere Instance { get; private set; }

        public class TimeOfDay
        {
            public string name;
            public float elevation, azimuth, sun, night, exposure, fogMul;
            public Color sunColor, top, horizon, fog, ambSky, ambEq, ambGround;
        }

        public class Weather
        {
            public string name;
            public float fogDensity, clouds, mist, sunMul, shadowMul, grey, rain, wet, wind;
            public Color tint;
        }

        public static readonly TimeOfDay[] Times =
        {
            new TimeOfDay { name = "Morning", elevation = 20f, azimuth = 112f, sun = 1.15f, night = 0f, exposure = 1f, fogMul = 1.15f,
                sunColor = new Color(1f, 0.86f, 0.70f), top = new Color(0.47f, 0.66f, 0.92f), horizon = new Color(1f, 0.88f, 0.80f),
                fog = new Color(0.94f, 0.88f, 0.86f), ambSky = new Color(0.62f, 0.72f, 0.90f), ambEq = new Color(0.96f, 0.84f, 0.78f), ambGround = new Color(0.44f, 0.50f, 0.37f) },
            new TimeOfDay { name = "Spring Day", elevation = 34f, azimuth = 218f, sun = 1.25f, night = 0f, exposure = 1f, fogMul = 1f,
                sunColor = new Color(1f, 0.89f, 0.78f), top = new Color(0.45f, 0.64f, 0.90f), horizon = new Color(1f, 0.87f, 0.86f),
                fog = new Color(0.93f, 0.85f, 0.87f), ambSky = new Color(0.66f, 0.74f, 0.90f), ambEq = new Color(0.95f, 0.83f, 0.82f), ambGround = new Color(0.46f, 0.52f, 0.38f) },
            new TimeOfDay { name = "Golden Sunset", elevation = 7f, azimuth = 292f, sun = 1.15f, night = 0.12f, exposure = 1f, fogMul = 1.1f,
                sunColor = new Color(1f, 0.62f, 0.34f), top = new Color(0.36f, 0.42f, 0.70f), horizon = new Color(1f, 0.66f, 0.50f),
                fog = new Color(0.96f, 0.72f, 0.66f), ambSky = new Color(0.56f, 0.52f, 0.74f), ambEq = new Color(0.98f, 0.68f, 0.56f), ambGround = new Color(0.40f, 0.36f, 0.30f) },
            new TimeOfDay { name = "Blue Hour", elevation = 16f, azimuth = 300f, sun = 0.38f, night = 0.7f, exposure = 1f, fogMul = 1.15f,
                sunColor = new Color(0.62f, 0.68f, 1f), top = new Color(0.10f, 0.16f, 0.38f), horizon = new Color(0.62f, 0.50f, 0.66f),
                fog = new Color(0.36f, 0.38f, 0.56f), ambSky = new Color(0.30f, 0.36f, 0.62f), ambEq = new Color(0.42f, 0.38f, 0.56f), ambGround = new Color(0.16f, 0.18f, 0.22f) },
            new TimeOfDay { name = "Night", elevation = 38f, azimuth = 140f, sun = 0.22f, night = 1f, exposure = 1f, fogMul = 1.25f,
                sunColor = new Color(0.66f, 0.74f, 1f), top = new Color(0.02f, 0.04f, 0.12f), horizon = new Color(0.16f, 0.17f, 0.30f),
                fog = new Color(0.12f, 0.13f, 0.22f), ambSky = new Color(0.16f, 0.20f, 0.38f), ambEq = new Color(0.20f, 0.19f, 0.30f), ambGround = new Color(0.08f, 0.09f, 0.11f) },
        };

        public static readonly Weather[] Weathers =
        {
            new Weather { name = "Clear Spring", fogDensity = 0.0013f, clouds = 0.20f, mist = 2f, sunMul = 1.08f, shadowMul = 1.05f, grey = 0f, rain = 0f, wet = 0f, wind = 1f, tint = new Color(0.9f, 0.9f, 0.95f) },
            new Weather { name = "Spring Haze", fogDensity = 0.0038f, clouds = 0.34f, mist = 7f, sunMul = 1f, shadowMul = 1f, grey = 0f, rain = 0f, wet = 0f, wind = 1f, tint = new Color(0.93f, 0.85f, 0.87f) },
            new Weather { name = "Cloudy", fogDensity = 0.0056f, clouds = 0.86f, mist = 11f, sunMul = 0.58f, shadowMul = 0.5f, grey = 0.55f, rain = 0f, wet = 0f, wind = 1.35f, tint = new Color(0.80f, 0.79f, 0.84f) },
            new Weather { name = "Rain", fogDensity = 0.0072f, clouds = 0.97f, mist = 14f, sunMul = 0.42f, shadowMul = 0.3f, grey = 0.72f, rain = 1f, wet = 1f, wind = 1.6f, tint = new Color(0.66f, 0.68f, 0.74f) },
            new Weather { name = "After Rain", fogDensity = 0.0034f, clouds = 0.55f, mist = 12f, sunMul = 1f, shadowMul = 0.9f, grey = 0.18f, rain = 0f, wet = 0.75f, wind = 0.8f, tint = new Color(0.86f, 0.88f, 0.92f) },
            new Weather { name = "Fog Off", fogDensity = 0f, clouds = 0.26f, mist = 0f, sunMul = 1.05f, shadowMul = 1.05f, grey = 0f, rain = 0f, wet = 0f, wind = 1f, tint = new Color(0.9f, 0.88f, 0.9f) },
        };

        // kept for older callers
        public static Weather[] Presets { get { return Weathers; } }

        public int WeatherIndex { get; private set; } = 1;
        public int TimeIndex { get; private set; } = 1;
        public Weather Current { get { return Weathers[WeatherIndex]; } }
        public TimeOfDay CurrentTime { get { return Times[TimeIndex]; } }

        struct Look
        {
            public Quaternion sunRot;
            public Color sunColor, top, horizon, fog, ambSky, ambEq, ambGround, cloudLit, cloudShade;
            public float sun, shadow, fogD, clouds, mist, night, rain, wet, refl, stars, wind;

            public static Look Lerp(Look a, Look b, float t)
            {
                return new Look
                {
                    sunRot = Quaternion.Slerp(a.sunRot, b.sunRot, t),
                    sunColor = Color.Lerp(a.sunColor, b.sunColor, t), top = Color.Lerp(a.top, b.top, t), horizon = Color.Lerp(a.horizon, b.horizon, t),
                    fog = Color.Lerp(a.fog, b.fog, t), ambSky = Color.Lerp(a.ambSky, b.ambSky, t), ambEq = Color.Lerp(a.ambEq, b.ambEq, t),
                    ambGround = Color.Lerp(a.ambGround, b.ambGround, t), cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, t),
                    cloudShade = Color.Lerp(a.cloudShade, b.cloudShade, t), sun = Mathf.Lerp(a.sun, b.sun, t), shadow = Mathf.Lerp(a.shadow, b.shadow, t),
                    fogD = Mathf.Lerp(a.fogD, b.fogD, t), clouds = Mathf.Lerp(a.clouds, b.clouds, t), mist = Mathf.Lerp(a.mist, b.mist, t),
                    night = Mathf.Lerp(a.night, b.night, t), rain = Mathf.Lerp(a.rain, b.rain, t), wet = Mathf.Lerp(a.wet, b.wet, t),
                    refl = Mathf.Lerp(a.refl, b.refl, t), stars = Mathf.Lerp(a.stars, b.stars, t), wind = Mathf.Lerp(a.wind, b.wind, t)
                };
            }
        }

        static Color Grey(Color c, float amount, Color tint)
        {
            float l = c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
            Color g = new Color(l, l, l) * new Color(tint.r * 1.05f, tint.g * 1.05f, tint.b * 1.08f);
            return Color.Lerp(c, g, amount);
        }

        static Look Combine(TimeOfDay t, Weather w)
        {
            var k = new Look();
            k.sunRot = Quaternion.Euler(t.elevation, t.azimuth, 0f);
            k.sunColor = Color.Lerp(t.sunColor, Color.Lerp(t.sunColor, Color.white, 0.5f), w.grey);
            k.sun = t.sun * w.sunMul;
            k.shadow = Mathf.Clamp01(0.74f * w.shadowMul * (t.night > 0.5f ? 0.8f : 1f));
            k.top = Grey(t.top, w.grey, w.tint);
            k.horizon = Grey(t.horizon, w.grey * 0.85f, w.tint);
            k.fog = Grey(Color.Lerp(t.fog, w.tint * Mathf.Lerp(1f, 0.35f, t.night), 0.25f), w.grey * 0.8f, w.tint);
            float lift = w.grey * 0.25f;     // overcast light is flatter: lift the shadowed side
            k.ambSky = Color.Lerp(t.ambSky, Grey(t.ambSky, 1f, w.tint), w.grey * 0.6f) * (1f + lift);
            k.ambEq = Color.Lerp(t.ambEq, Grey(t.ambEq, 1f, w.tint), w.grey * 0.6f) * (1f + lift);
            k.ambGround = t.ambGround * (1f + lift * 0.5f);
            k.fogD = w.fogDensity * t.fogMul;
            k.clouds = w.clouds;
            float daylight = 1f - t.night * 0.85f;
            k.cloudLit = Color.Lerp(Color.Lerp(Color.white, t.horizon, 0.35f), w.tint, w.grey * 0.6f) * daylight;
            k.cloudShade = Color.Lerp(t.horizon * 0.75f, new Color(0.55f, 0.56f, 0.66f), 0.5f + w.grey * 0.3f) * Mathf.Lerp(daylight, 0.6f, w.grey) * (1f - w.rain * 0.25f);
            k.mist = w.mist;
            k.night = t.night;
            k.rain = w.rain;
            k.wet = w.wet;
            k.refl = Mathf.Lerp(0.6f, 0.35f, t.night) + w.wet * 0.25f;
            k.stars = t.night * (1f - w.clouds * 0.9f);
            k.wind = w.wind;
            return k;
        }

        Material sky;
        Look live, from, target;
        float blend = 1f;
        Texture2D cookie;
        float cookieCover = -1f, cookieTimer;
        ReflectionProbe probe;
        float probeTimer;
        Camera cam;
        float camTimer;
        static readonly int IdWindGust = Shader.PropertyToID("_BR_WindGust");
        static readonly int IdSunDir = Shader.PropertyToID("_SunDir");

        void Awake()
        {
            Instance = this;
            GameSettings.Load();
            // shadows, cascades, pixel lights and LOD bias are per preset (GameSettings.Apply)
            GameSettings.Apply();

            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;

            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            sky = MaterialLibrary.Sky;
            sky.SetFloat("_CloudSpeed", 0.004f);
            RenderSettings.skybox = sky;

            int w = 1, t = 1;
            try { w = PlayerPrefs.GetInt("BR_Weather2", 1); t = PlayerPrefs.GetInt("BR_TimeOfDay", 1); } catch (System.Exception) { }
            WeatherIndex = Mathf.Clamp(w, 0, Weathers.Length - 1);
            TimeIndex = Mathf.Clamp(t, 0, Times.Length - 1);
            live = target = from = Combine(CurrentTime, Current);
            Apply(live);
            BuildCookie(live.clouds);

            var pgo = new GameObject("CarReflectionProbe");
            probe = pgo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(400f, 200f, 400f);
            probe.boxProjection = false;
            probe.importance = 1;
            probe.cullingMask = ~((1 << WorldBuilder.LayerGrass) | (1 << 31) | (1 << CarExtras.CarLayer));
            probe.farClipPlane = 600f;
            probe.hdr = true;
            ApplyProbeQuality();
            GameSettings.Changed += OnSettingsChanged;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            if (Instance == this) Instance = null;
        }

        int probeQ = -1;

        void OnSettingsChanged()
        {
            if (Sun == null || sky == null) return;
            Apply(live);                       // fog density follows the preset (FogBoost)
            ApplyProbeQuality();
        }

        // High / Ultra: the probe sees the world (time sliced, every few seconds). Low / Medium: sky only at
        // 64 px, re-rendered only when the sky changes - cars keep a pretty sky reflection for almost nothing.
        void ApplyProbeQuality()
        {
            if (probe == null) return;
            int q = GameSettings.Quality;
            if (q == probeQ) return;
            probeQ = q;
            bool full = q >= 2;
            probe.cullingMask = full ? ~((1 << WorldBuilder.LayerGrass) | (1 << 31) | (1 << CarExtras.CarLayer)) : 0;
            probe.resolution = full ? 128 : 64;
            probe.size = full ? new Vector3(400f, 200f, 400f) : new Vector3(6000f, 2000f, 6000f);
            probeTimer = 0f;
        }

        void Apply(Look k)
        {
            Sun.transform.rotation = k.sunRot;
            Sun.color = k.sunColor;
            Sun.intensity = k.sun;
            Sun.shadowStrength = k.shadow;
            RenderSettings.ambientSkyColor = k.ambSky;
            RenderSettings.ambientEquatorColor = k.ambEq;
            RenderSettings.ambientGroundColor = k.ambGround;
            // Low / Medium draw less far away: a slightly thicker haze (and a faint floor even with
            // "Fog Off" on Low) melts the streaming and cull edges into the sky colour instead of popping
            float fogD = k.fogD * GameSettings.FogBoost;
            if (GameSettings.Quality == 0) fogD = Mathf.Max(fogD, 0.0009f);
            RenderSettings.fog = fogD > 0.00005f;
            RenderSettings.fogColor = k.fog;
            RenderSettings.fogDensity = fogD;
            RenderSettings.reflectionIntensity = k.refl;
            sky.SetColor("_Top", k.top);
            sky.SetColor("_Horizon", k.horizon);
            sky.SetColor("_Bottom", Color.Lerp(k.horizon, k.fog, 0.6f));
            sky.SetColor("_SunColor", Color.Lerp(k.sunColor, Color.white, 0.1f) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(k.sun - 0.4f)) * (1f - k.clouds * 0.35f) + k.sunColor * k.night * 0.6f);
            sky.SetFloat("_Clouds", k.clouds);
            sky.SetColor("_CloudColor", k.cloudLit);
            sky.SetColor("_CloudShade", k.cloudShade);
            sky.SetFloat("_Stars", k.stars);
            sky.SetFloat("_MoonDisk", k.night);
            MistRate = k.mist;
            NightFactor = k.night;
            RainIntensity = k.rain;
            Wetness = k.wet;
            CloudCover = k.clouds;
            Shader.SetGlobalFloat("_BR_Wetness", k.wet);
            Shader.SetGlobalFloat("_BR_Rain", k.rain);
            Shader.SetGlobalFloat("_BR_Night", k.night);
            // brightness of the scene relative to the bright day the tree impostors were captured in
            float lum = Mathf.Clamp(k.sun * 0.55f + (k.ambSky.grayscale + k.ambEq.grayscale) * 0.5f, 0.12f, 1.6f) / 1.27f;
            Shader.SetGlobalColor("_BR_SceneLight", Color.Lerp(Color.white, (k.sunColor + k.ambSky) * 0.5f, 0.35f) * lum);
            var glow = MaterialLibrary.Glow;
            if (glow != null) glow.SetFloat("_Glow", 2.2f + k.night * 3.2f);
            MaterialLibrary.SetNightLights(k.night);
#pragma warning disable 0618   // cookieSize2D only exists in newer Unity 6 releases
            Sun.cookieSize = 520f;
#pragma warning restore 0618
        }

        void Update()
        {
            float t = Time.time;
            float g = Mathf.Max(0f, 0.45f * Mathf.Sin(t * 0.21f) + 0.3f * Mathf.Sin(t * 0.53f + 1.7f) + 0.15f * Mathf.Sin(t * 1.3f));
            Gust = g * live.wind;
            Shader.SetGlobalVector(IdWindGust, new Vector4(Gust, 0f, 0f, 0f));
            if (blend < 1f)
            {
                blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / 2.5f);
                live = Look.Lerp(from, target, Mathf.SmoothStep(0f, 1f, blend));
                Apply(live);
            }
            if (sky != null) sky.SetVector(IdSunDir, -Sun.transform.forward);

            // drifting cloud shadows: a tiling cookie scrolled by moving the light along its own plane
            cookieTimer -= Time.unscaledDeltaTime;
            if (cookieTimer <= 0f && Mathf.Abs(live.clouds - cookieCover) > 0.04f) { BuildCookie(live.clouds); cookieTimer = 0.5f; }
            Vector3 drift = new Vector3(1f, 0f, 0.35f) * (Time.time * (2.2f + Gust * 2f));
            Sun.transform.position = drift;

            // keep the reflection probe around the camera, refresh every few seconds (time sliced)
            if (cam == null || camTimer <= 0f) { cam = Camera.main; camTimer = 1f; }
            camTimer -= Time.unscaledDeltaTime;
            if (probe != null && cam != null)
            {
                probe.enabled = true;
                bool full = GameSettings.Quality >= 2;
                probeTimer -= Time.unscaledDeltaTime;
                if (probeTimer <= 0f)
                {
                    probe.transform.position = cam.transform.position + Vector3.up * 2f;
                    probe.RenderProbe();
                    // sky-only probe: only while the look blends (plus a slow safety refresh)
                    probeTimer = blend < 1f ? (full ? 0.6f : 1.2f) : (full ? (GameSettings.Quality >= 3 ? 2f : 3f) : 20f);
                }
                else if (!full) probe.transform.position = cam.transform.position;
            }
        }

        void BuildCookie(float cover)
        {
            cookieCover = cover;
            const int n = 128;
            if (cookie == null)
            {
                cookie = new Texture2D(n, n, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "BR_CloudShadows" };
            }
            var px = new Color32[n * n];
            float thr = 1f - cover * 0.9f;
            float strength = Mathf.Lerp(0.25f, 0.62f, cover) * (1f - live.night * 0.6f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = TileFbm(x / (float)n, y / (float)n);
                    float c = Mathf.SmoothStep(thr - 0.12f, thr + 0.14f, v);
                    if (cover > 0.9f) c = Mathf.Lerp(c, 0.85f, (cover - 0.9f) * 10f);   // full overcast: flat light, no patches
                    byte a = (byte)(Mathf.Clamp01(1f - c * strength) * 255f);
                    px[y * n + x] = new Color32(a, a, a, a);
                }
            cookie.SetPixels32(px);
            cookie.Apply(false);
            Sun.cookie = cover > 0.05f ? cookie : null;
        }

        // seamless (tiling) value-noise fbm on the unit square
        static float TileFbm(float u, float v)
        {
            float sum = 0f, amp = 0.5f;
            int period = 4;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * TileNoise(u * period, v * period, period);
                period *= 2;
                amp *= 0.5f;
            }
            return sum / 0.9375f;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        static float TileNoise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % period) + period) % period, xb = (xa + 1) % period;
            int ya = ((y0 % period) + period) % period, yb = (ya + 1) % period;
            float a = Hash(xa, ya), b = Hash(xb, ya), c = Hash(xa, yb), d = Hash(xb, yb);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        void StartBlend()
        {
            from = live;
            target = Combine(CurrentTime, Current);
            blend = 0f;
            probeTimer = 0f;
        }

        public void SetWeather(int index)
        {
            index = (index % Weathers.Length + Weathers.Length) % Weathers.Length;
            if (index == WeatherIndex) return;
            WeatherIndex = index;
            try { PlayerPrefs.SetInt("BR_Weather2", index); } catch (System.Exception) { }
            StartBlend();
        }

        public void SetTimeOfDay(int index)
        {
            index = (index % Times.Length + Times.Length) % Times.Length;
            if (index == TimeIndex) return;
            TimeIndex = index;
            try { PlayerPrefs.SetInt("BR_TimeOfDay", index); } catch (System.Exception) { }
            StartBlend();
        }

        public void CycleWeather() { SetWeather(WeatherIndex + 1); }
        public void CycleTimeOfDay() { SetTimeOfDay(TimeIndex + 1); }
    }
}

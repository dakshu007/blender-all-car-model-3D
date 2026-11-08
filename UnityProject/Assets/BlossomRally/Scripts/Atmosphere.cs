// Atmosphere.cs - golden spring light, soft real-time shadows, pastel fog, cloud sky, wind gusts and
// selectable weather: Clear / Spring Haze / Cloudy / Fog Off (smooth 2.5 s transitions).
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class Atmosphere : MonoBehaviour
    {
        public static float Gust { get; private set; }
        public static Light Sun { get; private set; }

        public class Weather
        {
            public string name;
            public bool fog;
            public float fogDensity;
            public Color fogColor, top, horizon, sunColor, ambSky, ambEq, ambGround, cloudLit, cloudShade;
            public float sun, shadow, clouds, mist, reflections;
        }

        public static readonly Weather[] Presets =
        {
            new Weather
            {
                name = "Clear Spring", fog = true, fogDensity = 0.0013f, fogColor = new Color(0.90f, 0.87f, 0.89f),
                top = new Color(0.36f, 0.59f, 0.93f), horizon = new Color(0.98f, 0.89f, 0.87f), sunColor = new Color(1f, 0.90f, 0.79f),
                ambSky = new Color(0.64f, 0.73f, 0.92f), ambEq = new Color(0.95f, 0.85f, 0.83f), ambGround = new Color(0.46f, 0.53f, 0.38f),
                cloudLit = new Color(1f, 0.97f, 0.97f), cloudShade = new Color(0.80f, 0.79f, 0.87f),
                sun = 1.38f, shadow = 0.78f, clouds = 0.20f, mist = 2f, reflections = 0.65f
            },
            new Weather
            {
                name = "Spring Haze", fog = true, fogDensity = 0.0038f, fogColor = new Color(0.93f, 0.85f, 0.87f),
                top = new Color(0.45f, 0.64f, 0.90f), horizon = new Color(1.0f, 0.87f, 0.86f), sunColor = new Color(1f, 0.89f, 0.78f),
                ambSky = new Color(0.66f, 0.74f, 0.90f), ambEq = new Color(0.95f, 0.83f, 0.82f), ambGround = new Color(0.46f, 0.52f, 0.38f),
                cloudLit = new Color(1f, 0.95f, 0.95f), cloudShade = new Color(0.82f, 0.76f, 0.84f),
                sun = 1.25f, shadow = 0.72f, clouds = 0.34f, mist = 7f, reflections = 0.6f
            },
            new Weather
            {
                name = "Cloudy", fog = true, fogDensity = 0.0056f, fogColor = new Color(0.80f, 0.79f, 0.84f),
                top = new Color(0.55f, 0.60f, 0.71f), horizon = new Color(0.87f, 0.84f, 0.87f), sunColor = new Color(0.96f, 0.93f, 0.92f),
                ambSky = new Color(0.74f, 0.76f, 0.84f), ambEq = new Color(0.84f, 0.81f, 0.84f), ambGround = new Color(0.50f, 0.52f, 0.46f),
                cloudLit = new Color(0.93f, 0.92f, 0.95f), cloudShade = new Color(0.58f, 0.58f, 0.67f),
                sun = 0.72f, shadow = 0.38f, clouds = 0.86f, mist = 11f, reflections = 0.45f
            },
            new Weather
            {
                name = "Fog Off", fog = false, fogDensity = 0f, fogColor = new Color(0.90f, 0.87f, 0.89f),
                top = new Color(0.36f, 0.59f, 0.93f), horizon = new Color(0.96f, 0.90f, 0.90f), sunColor = new Color(1f, 0.90f, 0.79f),
                ambSky = new Color(0.64f, 0.73f, 0.92f), ambEq = new Color(0.95f, 0.85f, 0.83f), ambGround = new Color(0.46f, 0.53f, 0.38f),
                cloudLit = new Color(1f, 0.97f, 0.97f), cloudShade = new Color(0.80f, 0.79f, 0.87f),
                sun = 1.35f, shadow = 0.78f, clouds = 0.26f, mist = 0f, reflections = 0.65f
            },
        };

        public int WeatherIndex { get; private set; }
        public Weather Current { get { return Presets[WeatherIndex]; } }
        /// <summary>Mist particle amount the petal system should use (particles / second).</summary>
        public static float MistRate { get; private set; }

        Material sky;
        // live (blended) values
        float fogD, sunI, shadowS, clouds, mist, refl;
        Color fogC, topC, horC, sunC, aSky, aEq, aGround, cLit, cShade;
        float blend = 1f;
        Weather from;

        void Awake()
        {
            // quality: real shadows, MSAA, smooth frame pacing
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = 170f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.06f, 0.16f, 0.4f);
            QualitySettings.antiAliasing = 4;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.vSyncCount = 1;
            QualitySettings.lodBias = 1.5f;
            Application.targetFrameRate = 60;

            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;
            sunGo.transform.rotation = Quaternion.Euler(34f, 218f, 0f);

            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            sky = MaterialLibrary.Sky;
            sky.SetFloat("_CloudSpeed", 0.004f);
            RenderSettings.skybox = sky;

            int saved = 1;
            try { saved = PlayerPrefs.GetInt("BR_Weather", 1); } catch (System.Exception) { }
            WeatherIndex = Mathf.Clamp(saved, 0, Presets.Length - 1);
            Capture(Current);
            from = Current;
            blend = 1f;
            ApplyLive();
        }

        void Capture(Weather w)
        {
            fogD = w.fog ? w.fogDensity : 0f; fogC = w.fogColor; topC = w.top; horC = w.horizon; sunC = w.sunColor;
            aSky = w.ambSky; aEq = w.ambEq; aGround = w.ambGround; cLit = w.cloudLit; cShade = w.cloudShade;
            sunI = w.sun; shadowS = w.shadow; clouds = w.clouds; mist = w.mist; refl = w.reflections;
        }

        void Blend(Weather a, Weather b, float t)
        {
            float fa = a.fog ? a.fogDensity : 0f, fb = b.fog ? b.fogDensity : 0f;
            fogD = Mathf.Lerp(fa, fb, t);
            fogC = Color.Lerp(a.fogColor, b.fogColor, t);
            topC = Color.Lerp(a.top, b.top, t);
            horC = Color.Lerp(a.horizon, b.horizon, t);
            sunC = Color.Lerp(a.sunColor, b.sunColor, t);
            aSky = Color.Lerp(a.ambSky, b.ambSky, t);
            aEq = Color.Lerp(a.ambEq, b.ambEq, t);
            aGround = Color.Lerp(a.ambGround, b.ambGround, t);
            cLit = Color.Lerp(a.cloudLit, b.cloudLit, t);
            cShade = Color.Lerp(a.cloudShade, b.cloudShade, t);
            sunI = Mathf.Lerp(a.sun, b.sun, t);
            shadowS = Mathf.Lerp(a.shadow, b.shadow, t);
            clouds = Mathf.Lerp(a.clouds, b.clouds, t);
            mist = Mathf.Lerp(a.mist, b.mist, t);
            refl = Mathf.Lerp(a.reflections, b.reflections, t);
        }

        void ApplyLive()
        {
            Sun.color = sunC;
            Sun.intensity = sunI;
            Sun.shadowStrength = shadowS;
            RenderSettings.ambientSkyColor = aSky;
            RenderSettings.ambientEquatorColor = aEq;
            RenderSettings.ambientGroundColor = aGround;
            RenderSettings.fog = fogD > 0.00005f;
            RenderSettings.fogColor = fogC;
            RenderSettings.fogDensity = fogD;
            RenderSettings.reflectionIntensity = refl;
            sky.SetColor("_Top", topC);
            sky.SetColor("_Horizon", horC);
            sky.SetColor("_Bottom", Color.Lerp(horC, fogC, 0.6f));
            sky.SetColor("_SunColor", Color.Lerp(sunC, Color.white, 0.1f) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(sunI - 0.4f)));
            sky.SetFloat("_Clouds", clouds);
            sky.SetColor("_CloudColor", cLit);
            sky.SetColor("_CloudShade", cShade);
            MistRate = mist;
        }

        void Update()
        {
            float t = Time.time;
            Gust = Mathf.Max(0f, 0.45f * Mathf.Sin(t * 0.21f) + 0.3f * Mathf.Sin(t * 0.53f + 1.7f) + 0.15f * Mathf.Sin(t * 1.3f));
            if (WeatherIndex == 2) Gust *= 1.35f;    // a breezier day under the clouds
            Shader.SetGlobalVector("_BR_WindGust", new Vector4(Gust, 0f, 0f, 0f));
            if (Sun != null && sky != null) sky.SetVector("_SunDir", -Sun.transform.forward);
            if (blend < 1f)
            {
                blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / 2.5f);
                Blend(from, Current, Mathf.SmoothStep(0f, 1f, blend));
                ApplyLive();
            }
        }

        public void SetWeather(int index)
        {
            index = (index % Presets.Length + Presets.Length) % Presets.Length;
            if (index == WeatherIndex) return;
            // start the new blend from whatever is on screen right now
            from = new Weather
            {
                name = "live", fog = fogD > 0f, fogDensity = fogD, fogColor = fogC, top = topC, horizon = horC, sunColor = sunC,
                ambSky = aSky, ambEq = aEq, ambGround = aGround, cloudLit = cLit, cloudShade = cShade,
                sun = sunI, shadow = shadowS, clouds = clouds, mist = mist, reflections = refl
            };
            WeatherIndex = index;
            blend = 0f;
            try { PlayerPrefs.SetInt("BR_Weather", index); } catch (System.Exception) { }
        }

        /// <summary>Cycle Clear / Spring Haze / Cloudy / Fog Off.</summary>
        public void CycleWeather()
        {
            SetWeather(WeatherIndex + 1);
        }
    }
}

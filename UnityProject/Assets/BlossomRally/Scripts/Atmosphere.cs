// Atmosphere.cs - golden spring light, soft real-time shadows, pastel fog, gradient sky, wind gusts.
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class Atmosphere : MonoBehaviour
    {
        public static float Gust { get; private set; }
        public static Light Sun { get; private set; }

        public Color fogColor = new Color(0.93f, 0.85f, 0.87f);
        public float fogDensity = 0.0038f;

        Material sky;

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
            Sun.color = new Color(1f, 0.89f, 0.78f);
            Sun.intensity = 1.25f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.72f;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;
            sunGo.transform.rotation = Quaternion.Euler(34f, 218f, 0f);

            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.74f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.95f, 0.83f, 0.82f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.52f, 0.38f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            sky = MaterialLibrary.Sky;
            sky.SetColor("_Top", new Color(0.45f, 0.64f, 0.90f));
            sky.SetColor("_Horizon", new Color(1.0f, 0.87f, 0.86f));
            sky.SetColor("_Bottom", fogColor);
            sky.SetColor("_SunColor", new Color(1f, 0.85f, 0.66f));
            RenderSettings.skybox = sky;
            RenderSettings.reflectionIntensity = 0.6f;
        }

        void Update()
        {
            float t = Time.time;
            Gust = Mathf.Max(0f, 0.45f * Mathf.Sin(t * 0.21f) + 0.3f * Mathf.Sin(t * 0.53f + 1.7f) + 0.15f * Mathf.Sin(t * 1.3f));
            Shader.SetGlobalVector("_BR_WindGust", new Vector4(Gust, 0f, 0f, 0f));
            if (Sun != null && sky != null) sky.SetVector("_SunDir", -Sun.transform.forward);
        }

        /// <summary>Cycle fog density (calm clear day / spring haze / dreamy mist).</summary>
        public void CycleFog()
        {
            if (RenderSettings.fogDensity < 0.003f) RenderSettings.fogDensity = fogDensity;
            else if (RenderSettings.fogDensity < 0.005f) RenderSettings.fogDensity = 0.0062f;
            else RenderSettings.fogDensity = 0.0022f;
        }
    }
}

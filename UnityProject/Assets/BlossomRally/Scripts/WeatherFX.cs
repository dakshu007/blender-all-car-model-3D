// WeatherFX.cs - rain streaks + ground splashes around the camera (scaled by Atmosphere.RainIntensity)
// and a windscreen overlay in the cockpit / hood views: raindrops that run off with speed, a wiper,
// and cherry petals that land on the glass and slide away.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class RainFX : MonoBehaviour
    {
        public Transform follow;
        ParticleSystem rain, splash;
        ParticleSystem.EmitParams ep;
        float splashAcc, rainRate = -1f;

        void Start()
        {
            rain = MakeRain();
            splash = MakeSplash();
        }

        ParticleSystem MakeRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.3f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = new Color(0.82f, 0.86f, 0.95f, 0.38f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 7000;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(56f, 1f, 56f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-21f, -18f);
            vel.z = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = MaterialLibrary.Particle("Rain", ProceduralTextures.Soft, Color.white, 0.45f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeSplash()
        {
            var go = new GameObject("RainSplash");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.28f;
            main.startSpeed = 0f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.9f, 0.93f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = MaterialLibrary.Particle("Splash", ProceduralTextures.Soft, Color.white, 0.6f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void LateUpdate()
        {
            if (follow == null || rain == null) return;
            float k = Atmosphere.RainIntensity * RegionRuntime.RainVisual;
            Vector3 f = follow.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            f.Normalize();
            rain.transform.position = follow.position + f * 10f + Vector3.up * 16f;
            // particle budget follows the preset (Low: 40 %); only touch the emitter when the rate changes
            float rate = 5200f * k * GameSettings.ParticleScale;
            if (Mathf.Abs(rate - rainRate) > 1f)
            {
                rainRate = rate;
                var em = rain.emission;
                em.rateOverTime = rate;
            }
            if (k < 0.01f) return;
            var gm = GameManager.Instance;
            var world = gm != null ? gm.World : null;
            splashAcc += Time.deltaTime * 260f * k * GameSettings.ParticleScale;
            int n = Mathf.Min(40, Mathf.FloorToInt(splashAcc));
            splashAcc -= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 o = Random.insideUnitCircle * 22f;
                Vector3 p = follow.position + f * 8f + new Vector3(o.x, 0f, o.y);
                p.y = world != null && world.Done ? world.TerrainHeight(p.x, p.z) + 0.1f : follow.position.y - 1.4f;
                RoadSample rs;
                if (world != null && world.NearestRoad(new Vector2(p.x, p.z), out rs, 12f) < rs.halfWidth) p.y = rs.height + 0.1f;
                ep.position = p;
                splash.Emit(ep, 1);
            }
        }
    }

    /// <summary>Raindrops, wiper and petals on the windscreen (cockpit and hood cameras only).</summary>
    public class WindscreenFX : MonoBehaviour
    {
        class Drop { public Vector2 pos; public float size, life, slide; public bool petal; public float rot; }
        readonly List<Drop> drops = new List<Drop>();
        Texture2D dropTex;
        float wiper = -1f, wiperTimer;

        void Awake() { useGUILayout = false; }     // plain GUI calls only: skip the layout pass (and its garbage)

        Texture2D DropTex()
        {
            if (dropTex != null) return dropTex;
            const int s = 48;
            dropTex = new Texture2D(s, s, TextureFormat.RGBA32, true);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((1f - d) * 6f);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 8f);
                    float hi = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.3f, 0.35f)) * 4f);
                    float l = 0.75f + rim * 0.2f + hi * 0.5f;
                    dropTex.SetPixel(x, y, new Color(l, l, Mathf.Min(1f, l * 1.05f), a * (0.18f + rim * 0.45f + hi * 0.6f)));
                }
            dropTex.Apply(true);
            return dropTex;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null || gm.Rig == null) return;
            bool inside = gm.Rig.mode == CamMode.Cockpit || gm.Rig.mode == CamMode.Hood;
            if (!inside) { drops.Clear(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = gm.Car.SpeedKmh;
            float rain = Atmosphere.RainIntensity * RegionRuntime.RainVisual;
            // new raindrops
            if (rain > 0.01f && Random.value < rain * 45f * dt && drops.Count < 140)
                drops.Add(new Drop { pos = new Vector2(Random.value, Random.value * 0.85f), size = Random.Range(0.008f, 0.022f), life = Random.Range(4f, 9f) });
            // petals hitting the glass while driving under the blossoms
            var pw = FindPetals();
            int level = pw != null ? pw.level : 1;
            if (speed > 15f && Random.value < (0.08f + level * 0.12f) * dt * Mathf.Clamp01(speed / 60f) * RegionRuntime.PetalAmount && drops.Count < 140)
                drops.Add(new Drop { pos = new Vector2(Random.Range(0.1f, 0.9f), Random.Range(0.15f, 0.75f)), size = Random.Range(0.018f, 0.03f), life = Random.Range(3f, 7f), petal = true, rot = Random.Range(0f, 360f) });
            // wiper in the rain (cockpit view)
            if (rain > 0.05f && gm.Rig.mode == CamMode.Cockpit)
            {
                wiperTimer -= dt;
                if (wiperTimer <= 0f && wiper < 0f) { wiper = 0f; wiperTimer = Mathf.Lerp(2.2f, 0.9f, rain); }
            }
            float prevW = wiper;
            if (wiper >= 0f)
            {
                wiper += dt * 1.6f;
                if (wiper > 2f) wiper = -1f;
            }
            float airflow = Mathf.Clamp01((speed - 30f) / 90f);
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.life -= dt;
                // airflow pushes drops up and outwards; slow speeds let them run down
                Vector2 dir = new Vector2((d.pos.x - 0.5f) * 0.6f, -1f) * airflow + new Vector2(0f, 0.12f) * (1f - airflow);
                float slip = d.petal ? (speed > 55f ? 0.35f : 0f) : (0.04f + airflow * 0.5f);
                d.pos += dir * slip * dt;
                if (wiper >= 0f && WiperCovers(prevW, wiper, d.pos)) d.life = 0f;
                if (d.life <= 0f || d.pos.y < -0.05f || d.pos.y > 1.05f || d.pos.x < -0.05f || d.pos.x > 1.05f) drops.RemoveAt(i);
            }
        }

        PetalWeather petalCache;
        PetalWeather FindPetals()
        {
            if (petalCache == null) petalCache = FindAnyPetals();
            return petalCache;
        }

        static PetalWeather FindAnyPetals()
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindAnyObjectByType<PetalWeather>();
#else
            return Object.FindObjectOfType<PetalWeather>();
#endif
        }

        static float WiperAngle(float w) { return Mathf.Lerp(-80f, 80f, w <= 1f ? w : 2f - w); }

        static bool WiperCovers(float w0, float w1, Vector2 p)
        {
            // wiper pivot bottom-centre of the screen, sweeping -80..80 deg (screen-space, 0..1)
            Vector2 rel = new Vector2((p.x - 0.5f) * 1.6f, p.y);
            if (rel.magnitude > 0.95f) return false;
            float ang = Mathf.Atan2(rel.x, rel.y) * Mathf.Rad2Deg;
            float a0 = WiperAngle(Mathf.Max(0f, w0)), a1 = WiperAngle(w1);
            return ang >= Mathf.Min(a0, a1) - 3f && ang <= Mathf.Max(a0, a1) + 3f;
        }

        void OnGUI()
        {
            if (drops.Count == 0 && wiper < 0f) return;
            if (GameManager.HideHud) return;
            GUI.depth = 10;
            float W = Screen.width, H = Screen.height;
            var tex = DropTex();
            var petal = ProceduralTextures.Petal;
            var old = GUI.color;
            foreach (var d in drops)
            {
                float a = Mathf.Clamp01(d.life);
                float s = d.size * H;
                Rect r = new Rect(d.pos.x * W - s * 0.5f, (1f - d.pos.y) * H - s * 0.5f, s, s * (d.petal ? 1.3f : 1f));
                GUI.color = new Color(1f, 1f, 1f, a);
                if (d.petal)
                {
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(d.rot, r.center);
                    GUI.DrawTexture(r, petal);
                    GUI.matrix = m;
                }
                else GUI.DrawTexture(r, tex);
            }
            if (wiper >= 0f)
            {
                var m = GUI.matrix;
                Vector2 pivot = new Vector2(W * 0.5f, H * 1.02f);
                GUIUtility.RotateAroundPivot(WiperAngle(wiper), pivot);
                GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.92f);
                GUI.DrawTexture(new Rect(pivot.x - 4f, pivot.y - H * 0.98f, 8f, H * 0.98f), Texture2D.whiteTexture);
                GUI.matrix = m;
            }
            GUI.color = old;
        }
    }
}

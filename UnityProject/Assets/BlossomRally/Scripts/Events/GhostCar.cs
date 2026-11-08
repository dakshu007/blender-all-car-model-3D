// GhostCar.cs - records the player's run at 20 Hz (pose + steer + wheel spin), saves the best
// run per stage and car to <persistentDataPath>/ghosts/<stage>_<car>.json and plays it back as a
// translucent pale pink-white copy of the car (shader BlossomRally/Ghost, no colliders/physics).
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlossomRally
{
    [System.Serializable]
    public class GhostData
    {
        public int version = 1;
        public string stage;
        public string car;
        public float time;                 // finish time of the run
        public float interval = 0.05f;     // seconds between frames
        public int count;
        public float[] pos;                // count * 3
        public float[] rot;                // count * 4 (x y z w)
        public float[] wheels;             // count * 5 (steer, spin FL FR RL RR)

        public bool Valid
        {
            get
            {
                return count >= 2 && interval > 0f && pos != null && rot != null && wheels != null &&
                       pos.Length >= count * 3 && rot.Length >= count * 4 && wheels.Length >= count * 5;
            }
        }
    }

    public class GhostRecorder
    {
        public const float Interval = 0.05f;
        static readonly string[] Slots = { "FL", "FR", "RL", "RR" };
        readonly List<float> pos = new List<float>(), rot = new List<float>(), wh = new List<float>();
        readonly int[] slotIndex = { -1, -1, -1, -1 };
        float next;
        int count;

        public void Begin(ArcadeCar car)
        {
            pos.Clear(); rot.Clear(); wh.Clear();
            count = 0;
            next = 0f;
            for (int s = 0; s < 4; s++)
            {
                slotIndex[s] = -1;
                if (car == null || car.wheels == null) continue;
                for (int i = 0; i < car.wheels.Length; i++)
                    if (car.wheels[i] != null && car.wheels[i].slot == Slots[s]) slotIndex[s] = i;
            }
        }

        /// <summary>Call every frame with the run time; adds a frame for every 50 ms slot that is due.</summary>
        public void Tick(ArcadeCar car, float runTime)
        {
            if (car == null) return;
            int guard = 0;
            while (runTime >= next && guard++ < 40)
            {
                Add(car);
                next += Interval;
            }
        }

        void Add(ArcadeCar car)
        {
            Transform t = car.transform;
            Vector3 p = t.position;
            Quaternion q = t.rotation;
            pos.Add(p.x); pos.Add(p.y); pos.Add(p.z);
            rot.Add(q.x); rot.Add(q.y); rot.Add(q.z); rot.Add(q.w);
            wh.Add(car.SteerAngle);
            for (int s = 0; s < 4; s++)
            {
                int i = slotIndex[s];
                wh.Add(i >= 0 && i < car.wheels.Length && car.wheels[i] != null ? car.wheels[i].spin : 0f);
            }
            count++;
        }

        public GhostData Finish(ArcadeCar car, string stage, string carId, float time)
        {
            if (car != null) Add(car);
            return new GhostData
            {
                stage = stage, car = carId, time = time, interval = Interval, count = count,
                pos = pos.ToArray(), rot = rot.ToArray(), wheels = wh.ToArray()
            };
        }
    }

    public static class GhostStore
    {
        static readonly Dictionary<string, GhostData> cache = new Dictionary<string, GhostData>();

        static string Key(string stage, string car) { return stage + "_" + car; }

        static string PathFor(string stage, string car)
        {
            return Path.Combine(Path.Combine(Application.persistentDataPath, "ghosts"), Key(stage, car) + ".json");
        }

        public static GhostData Load(string stage, string car)
        {
            string k = Key(stage, car);
            GhostData g;
            if (cache.TryGetValue(k, out g)) return g;
            g = null;
            try
            {
                string p = PathFor(stage, car);
                if (File.Exists(p))
                {
                    g = JsonUtility.FromJson<GhostData>(File.ReadAllText(p));
                    if (g != null && !g.Valid) g = null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BlossomRally] could not read ghost " + k + ": " + e.Message);
                g = null;
            }
            cache[k] = g;
            return g;
        }

        /// <summary>Keeps the ghost in memory and writes it to disk (IO errors are logged, never thrown).</summary>
        public static void Save(GhostData g)
        {
            if (g == null || !g.Valid) return;
            cache[Key(g.stage, g.car)] = g;
            try
            {
                string p = PathFor(g.stage, g.car);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, JsonUtility.ToJson(g));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BlossomRally] could not save ghost " + Key(g.stage, g.car) + ": " + e.Message);
            }
        }
    }

    public class GhostCar
    {
        GameObject root;
        GhostData data;
        Material mat;
        readonly Transform[] wheels = new Transform[4];
        readonly Quaternion[] rest = new Quaternion[4];
        static readonly string[] Slots = { "FL", "FR", "RL", "RR" };
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        bool customShader;

        public float Time { get { return data != null ? data.time : 0f; } }

        public static GhostCar Create(GhostData d)
        {
            if (d == null || !d.Valid || string.IsNullOrEmpty(d.car)) return null;
            var prefab = Resources.Load<GameObject>("Cars/" + d.car + "/" + d.car + "_Export");
            if (prefab == null) return null;
            var g = new GhostCar { data = d };
            g.root = new GameObject("Ghost_" + d.car);
            var model = Object.Instantiate(prefab, g.root.transform, false);
            model.name = d.car + "_Ghost";
            AxisFix.Calibrate();
            AxisFix.Apply(model.transform);
            foreach (var c in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(c);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var c in model.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(c);
            foreach (var c in model.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(c);
            foreach (var c in model.GetComponentsInChildren<Light>(true)) Object.Destroy(c);
            foreach (var c in model.GetComponentsInChildren<ParticleSystem>(true)) Object.Destroy(c.gameObject);

            g.mat = MakeMaterial(out g.customShader);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) { r.enabled = false; continue; }
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = g.mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            for (int s = 0; s < 4; s++)
            {
                var w = CarFactory.FindDeep(model.transform, d.car + "_Wheel_" + Slots[s]);
                g.wheels[s] = w;
                if (w != null) g.rest[s] = Quaternion.Inverse(g.root.transform.rotation) * w.rotation;
            }
            g.Show(0f, new Vector3(1e6f, 0f, 0f));
            return g;
        }

        static Material MakeMaterial(out bool custom)
        {
            Shader sh = Shader.Find("BlossomRally/Ghost");
            custom = sh != null;
            Material m;
            if (custom)
            {
                m = new Material(sh);
            }
            else
            {
                // fallback: Standard in Fade mode
                m = new Material(MaterialLibrary.FindShader("Standard"));
                m.SetFloat("_Mode", 2f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.45f, 0.36f, 0.40f));
                m.renderQueue = 3000;
                m.SetColor("_Color", new Color(1f, 0.9f, 0.94f, 0.35f));
            }
            m.name = "BR_Ghost";
            return m;
        }

        /// <summary>Pose the ghost at run time t; fades it out near the player's car and hides it after its finish.</summary>
        public void Show(float t, Vector3 player)
        {
            if (root == null || data == null) return;
            bool visible = t <= data.time + 1.5f;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;
            int n = data.count;
            float f = Mathf.Clamp(t / data.interval, 0f, n - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), n - 2);
            float a = Mathf.Clamp01(f - i);
            int j = i + 1;
            Vector3 p0 = new Vector3(data.pos[i * 3], data.pos[i * 3 + 1], data.pos[i * 3 + 2]);
            Vector3 p1 = new Vector3(data.pos[j * 3], data.pos[j * 3 + 1], data.pos[j * 3 + 2]);
            Quaternion q0 = Norm(data.rot[i * 4], data.rot[i * 4 + 1], data.rot[i * 4 + 2], data.rot[i * 4 + 3]);
            Quaternion q1 = Norm(data.rot[j * 4], data.rot[j * 4 + 1], data.rot[j * 4 + 2], data.rot[j * 4 + 3]);
            // a teleport (checkpoint respawn) between two frames: snap instead of sliding across the map
            if ((p1 - p0).sqrMagnitude > 30f * 30f) { p1 = p0; q1 = q0; }
            Transform rt = root.transform;
            rt.SetPositionAndRotation(Vector3.Lerp(p0, p1, a), Quaternion.Slerp(q0, q1, a));
            float steer = Mathf.Lerp(data.wheels[i * 5], data.wheels[j * 5], a);
            for (int s = 0; s < 4; s++)
            {
                if (wheels[s] == null) continue;
                float spin = Mathf.LerpAngle(data.wheels[i * 5 + 1 + s], data.wheels[j * 5 + 1 + s], a);
                float st = s < 2 ? steer : 0f;
                wheels[s].rotation = rt.rotation * Quaternion.AngleAxis(st, Vector3.up) * Quaternion.AngleAxis(spin, Vector3.right) * rest[s];
            }
            float playerDist = Vector3.Distance(rt.position, player);
            float fade = Mathf.Lerp(0.08f, 0.38f, Mathf.InverseLerp(2.5f, 9f, playerDist));
            if (t > data.time) fade *= Mathf.Clamp01(1f - (t - data.time) / 1.5f);
            if (customShader) mat.SetFloat(IdAlpha, fade);
            else mat.SetColor(IdColor, new Color(1f, 0.9f, 0.94f, fade));
        }

        static Quaternion Norm(float x, float y, float z, float w)
        {
            float m = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            if (m < 1e-5f) return Quaternion.identity;
            return new Quaternion(x / m, y / m, z / m, w / m);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
            if (mat != null) Object.Destroy(mat);
            root = null;
            mat = null;
        }
    }
}

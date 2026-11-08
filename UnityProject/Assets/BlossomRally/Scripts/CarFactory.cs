// CarFactory.cs - spawns a playable car from Resources/Cars/<ID>: model, Rigidbody, collider,
// ArcadeCar physics, drift FX and procedural engine audio.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class CarFactory
    {
        public static ArcadeCar Create(CarSpec spec, Vector3 pos, Quaternion rot)
        {
            var root = new GameObject("PlayerCar_" + spec.id);
            var prefab = Resources.Load<GameObject>("Cars/" + spec.id + "/" + spec.id + "_Export");
            if (prefab == null)
            {
                Debug.LogError("[BlossomRally] car model not found: Cars/" + spec.id);
                return null;
            }
            var model = Object.Instantiate(prefab, root.transform);
            model.name = spec.id + "_Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var an in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(an);
            MaterialLibrary.Remap(model, spec.id);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // wheels from the exported vehicle data (falls back to the wheel pivots in the model)
            var data = LoadData(spec.id);
            var wheels = new List<ArcadeCar.Wheel>();
            foreach (string slot in new[] { "FL", "FR", "RL", "RR" })
            {
                var w = new ArcadeCar.Wheel { slot = slot, front = slot[0] == 'F' };
                w.visual = FindDeep(model.transform, spec.id + "_Wheel_" + slot);
                WheelData wd = null;
                if (data != null && data.wheel_list != null)
                    foreach (var d in data.wheel_list) if (d.slot == slot) wd = d;
                if (wd != null && wd.unity_local_position != null && wd.unity_local_position.Length == 3)
                {
                    w.local = new Vector3(wd.unity_local_position[0], wd.unity_local_position[1], wd.unity_local_position[2]);
                    w.radius = wd.radius > 0.1f ? wd.radius : 0.32f;
                }
                else if (w.visual != null)
                {
                    w.local = root.transform.InverseTransformPoint(w.visual.position);
                    w.radius = 0.32f;
                }
                else
                {
                    w.local = new Vector3(slot[1] == 'L' ? -0.75f : 0.75f, 0.32f, w.front ? 1.25f : -1.2f);
                }
                w.driven = spec.drive == Drive.AWD || (spec.drive == Drive.RWD && !w.front) || (spec.drive == Drive.FWD && w.front);
                wheels.Add(w);
            }

            // body collider from the body mesh bounds (kept above the wheels' reach)
            Bounds b = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 1.2f, 4.2f));
            var body = FindDeep(model.transform, spec.id + "_Body");
            if (body != null)
            {
                var r = body.GetComponent<Renderer>();
                if (r != null)
                {
                    b = r.bounds;
                    b.center = root.transform.InverseTransformPoint(b.center);
                }
            }
            var box = root.AddComponent<BoxCollider>();
            float bottom = 0.36f;
            box.center = new Vector3(b.center.x, (bottom + b.max.y) * 0.5f, b.center.z);
            box.size = new Vector3(b.size.x * 0.92f, Mathf.Max(0.4f, b.max.y - bottom), b.size.z * 0.95f);
#if UNITY_6000_0_OR_NEWER
            var pm = new PhysicsMaterial("CarBody") { dynamicFriction = 0.15f, staticFriction = 0.15f, bounciness = 0.05f,
                frictionCombine = PhysicsMaterialCombine.Minimum };
#else
            var pm = new PhysicMaterial("CarBody") { dynamicFriction = 0.15f, staticFriction = 0.15f, bounciness = 0.05f,
                frictionCombine = PhysicMaterialCombine.Minimum };
#endif
            box.sharedMaterial = pm;

            root.AddComponent<Rigidbody>();
            var car = root.AddComponent<ArcadeCar>();
            car.Init(spec, wheels.ToArray());
            root.AddComponent<CarFX>();
            root.AddComponent<EngineAudio>();
            root.transform.SetPositionAndRotation(pos, rot);
            return car;
        }

        static VehicleData LoadData(string id)
        {
            var ta = Resources.Load<TextAsset>("Cars/" + id + "/" + id + "_vehicle_data");
            if (ta == null) return null;
            try { return JsonUtility.FromJson<VehicleData>(ta.text); }
            catch (System.Exception e) { Debug.LogWarning("[BlossomRally] vehicle data for " + id + ": " + e.Message); return null; }
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }

    /// <summary>Tyre smoke (coloured by surface) and skid marks.</summary>
    public class CarFX : MonoBehaviour
    {
        ArcadeCar car;
        readonly List<ParticleSystem> smoke = new List<ParticleSystem>();
        readonly List<TrailRenderer> skids = new List<TrailRenderer>();
        readonly List<ArcadeCar.Wheel> fxWheels = new List<ArcadeCar.Wheel>();

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            foreach (var w in car.wheels)
            {
                if (w.front) continue;
                fxWheels.Add(w);
                smoke.Add(MakeSmoke());
                skids.Add(MakeSkid());
            }
        }

        ParticleSystem MakeSmoke()
        {
            var go = new GameObject("TyreSmoke");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.gravityModifier = -0.03f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.8f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.0f, 0f), new GradientAlphaKey(0.55f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle("Smoke", ProceduralTextures.Soft, Color.white, 0.7f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        TrailRenderer MakeSkid()
        {
            var go = new GameObject("SkidMark");
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 9f;
            tr.minVertexDistance = 0.25f;
            tr.widthMultiplier = 0.24f;
            tr.alignment = LineAlignment.TransformZ;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.textureMode = LineTextureMode.Tile;
            tr.sharedMaterial = MaterialLibrary.Particle("Skid", ProceduralTextures.Skid, new Color(0.08f, 0.07f, 0.07f, 0.55f), 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            return tr;
        }

        static Color SmokeColor(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: return new Color(1f, 0.94f, 0.96f);
                case Surface.Paving: return new Color(0.96f, 0.93f, 0.92f);
                case Surface.Gravel: return new Color(0.86f, 0.78f, 0.62f);
                case Surface.Dirt: return new Color(0.66f, 0.52f, 0.38f);
                default: return new Color(0.62f, 0.74f, 0.45f);
            }
        }

        void LateUpdate()
        {
            if (car == null) return;
            for (int i = 0; i < fxWheels.Count; i++)
            {
                var w = fxWheels[i];
                bool sliding = w.grounded && car.SpeedKmh > 12f && (w.lateralSlip > 2.2f || (car.handbrake > 0.5f && car.SpeedKmh > 15f));
                float amount = sliding ? Mathf.Clamp01(Mathf.Max(car.DriftIntensity, (w.lateralSlip - 2f) / 8f)) : 0f;
                var ps = smoke[i];
                ps.transform.position = w.grounded ? w.contact + Vector3.up * 0.25f : transform.position;
                var em = ps.emission;
                bool offRoad = w.surface == Surface.Grass || w.surface == Surface.Dirt || w.surface == Surface.Gravel;
                em.rateOverTime = amount * (offRoad ? 70f : 55f) + (offRoad && w.grounded && car.SpeedKmh > 30f ? 6f : 0f);
                var main = ps.main;
                main.startColor = SmokeColor(w.surface);
                var tr = skids[i];
                if (w.grounded) { tr.transform.position = w.contact + w.normal * 0.025f; tr.transform.rotation = Quaternion.LookRotation(-w.normal); }
                tr.emitting = sliding && amount > 0.15f;
            }
        }
    }

    /// <summary>Procedural engine note + tyre squeal (no audio files needed).</summary>
    [RequireComponent(typeof(ArcadeCar))]
    public class EngineAudio : MonoBehaviour
    {
        ArcadeCar car;
        AudioSource engine, squeal;

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            int rate = 44100;
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = MakeEngineClip(rate);
            engine.loop = true;
            engine.spatialBlend = 0.6f;
            engine.volume = 0.25f;
            engine.dopplerLevel = 0f;
            engine.Play();
            squeal = gameObject.AddComponent<AudioSource>();
            squeal.clip = MakeNoiseClip(rate);
            squeal.loop = true;
            squeal.spatialBlend = 0.6f;
            squeal.volume = 0f;
            squeal.dopplerLevel = 0f;
            squeal.Play();
        }

        static AudioClip MakeEngineClip(int rate)
        {
            // 1 second loop, 55 Hz fundamental: rich, slightly gritty four-cylinder buzz
            var data = new float[rate];
            var rnd = new System.Random(3);
            for (int i = 0; i < rate; i++)
            {
                double t = i / (double)rate;
                double s = 0.55 * System.Math.Sin(2 * System.Math.PI * 55 * t) + 0.30 * System.Math.Sin(2 * System.Math.PI * 110 * t + 0.5)
                           + 0.18 * System.Math.Sin(2 * System.Math.PI * 165 * t + 1.1) + 0.12 * System.Math.Sin(2 * System.Math.PI * 220 * t + 0.2)
                           + 0.06 * System.Math.Sin(2 * System.Math.PI * 330 * t);
                double pulse = 0.75 + 0.25 * System.Math.Sin(2 * System.Math.PI * 27.5 * t);
                data[i] = (float)(s * pulse * 0.45 + (rnd.NextDouble() - 0.5) * 0.04);
            }
            var clip = AudioClip.Create("BR_Engine", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoiseClip(int rate)
        {
            var data = new float[rate];
            var rnd = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < rate; i++)
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.55f + n * 0.45f;
                double t = i / (double)rate;
                data[i] = lp * 0.5f * (float)(0.8 + 0.2 * System.Math.Sin(2 * System.Math.PI * 880 * t));
            }
            var clip = AudioClip.Create("BR_Squeal", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Update()
        {
            if (car == null || engine == null) return;
            engine.pitch = Mathf.Lerp(0.55f, 2.6f, Mathf.InverseLerp(850f, 7800f, car.EngineRpm));
            engine.volume = 0.16f + 0.16f * car.throttle;
            bool onRoad = false;
            foreach (var w in car.wheels) if (w.grounded && (w.surface == Surface.Asphalt || w.surface == Surface.Paving)) onRoad = true;
            float target = onRoad ? car.DriftIntensity * 0.32f : car.DriftIntensity * 0.08f;
            squeal.volume = Mathf.Lerp(squeal.volume, target, Time.deltaTime * 8f);
            squeal.pitch = 0.9f + car.DriftIntensity * 0.25f;
        }
    }
}

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
            var model = Object.Instantiate(prefab, root.transform, false);
            model.name = spec.id + "_Model";
            AxisFix.Calibrate();
            AxisFix.Apply(model.transform);
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

        internal static VehicleData LoadData(string id)
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

    /// <summary>
    /// Self-calibrating Blender->Unity orientation fix. Depending on the Unity version, the FBX
    /// importer may drop the axis-conversion rotation stored on the single top node (it collapses
    /// that node into the prefab root), which stands every model on its end. We instantiate one
    /// car, compare where its wheel pivots really are with where the exported vehicle data says
    /// they must be (+Z forward, +Y up), and derive the correcting rotation (snapped to 90 deg).
    /// All Blossom Rally FBX files share the same export settings, so the same fix applies to
    /// every car and environment model. If the import is already correct, the fix is identity.
    /// </summary>
    public static class AxisFix
    {
        static bool calibrated;
        public static Quaternion Rotation { get; private set; } = Quaternion.identity;

        public static void Calibrate()
        {
            if (calibrated) return;
            calibrated = true;
            foreach (var spec in CarCatalog.Cars)
                if (TryCar(spec.id)) return;
            Debug.LogWarning("[BlossomRally] axis calibration failed - using identity");
        }

        static bool TryCar(string id)
        {
            var prefab = Resources.Load<GameObject>("Cars/" + id + "/" + id + "_Export");
            var data = CarFactory.LoadData(id);
            if (prefab == null || data == null || data.wheel_list == null) return false;
            var tmp = new GameObject("BR_AxisCalibration");
            var m = Object.Instantiate(prefab, tmp.transform, false);
            Transform fl = CarFactory.FindDeep(m.transform, id + "_Wheel_FL");
            Transform fr = CarFactory.FindDeep(m.transform, id + "_Wheel_FR");
            Transform rl = CarFactory.FindDeep(m.transform, id + "_Wheel_RL");
            Vector3? eFL = null, eFR = null, eRL = null;
            foreach (var w in data.wheel_list)
            {
                if (w.unity_local_position == null || w.unity_local_position.Length != 3) continue;
                var v = new Vector3(w.unity_local_position[0], w.unity_local_position[1], w.unity_local_position[2]);
                if (w.slot == "FL") eFL = v;
                else if (w.slot == "FR") eFR = v;
                else if (w.slot == "RL") eRL = v;
            }
            bool ok = fl != null && fr != null && rl != null && eFL.HasValue && eFR.HasValue && eRL.HasValue;
            if (ok)
            {
                Vector3 aFL = tmp.transform.InverseTransformPoint(fl.position);
                Vector3 aFR = tmp.transform.InverseTransformPoint(fr.position);
                Vector3 aRL = tmp.transform.InverseTransformPoint(rl.position);
                Vector3 fa = aFL - aRL, ra = aFR - aFL;
                Vector3 fe = eFL.Value - eRL.Value, re = eFR.Value - eFL.Value;
                ok = fa.sqrMagnitude > 0.01f && ra.sqrMagnitude > 0.01f;
                if (ok)
                {
                    Quaternion qa = Quaternion.LookRotation(fa, Vector3.Cross(fa, ra));
                    Quaternion qe = Quaternion.LookRotation(fe, Vector3.Cross(fe, re));
                    Vector3 e = (qe * Quaternion.Inverse(qa)).eulerAngles;
                    e = new Vector3(Mathf.Round(e.x / 90f) * 90f, Mathf.Round(e.y / 90f) * 90f, Mathf.Round(e.z / 90f) * 90f);
                    Rotation = Quaternion.Euler(e);
                    Debug.Log("[BlossomRally] axis calibration from " + id + ": correction " + e +
                              (e == Vector3.zero ? " (import already correct)" : " (applied to all models)"));
                }
            }
            Object.Destroy(tmp);
            return ok;
        }

        /// <summary>Rotate an instantiated model (child of a holder in Unity axes) into place.</summary>
        public static void Apply(Transform model)
        {
            if (Rotation == Quaternion.identity) return;
            model.localPosition = Rotation * model.localPosition;
            model.localRotation = Rotation * model.localRotation;
        }
    }

    /// <summary>Tyre smoke (coloured by surface), skid marks, flying grass clippings and the
    /// shader globals that let grass / plants bend and shake around the car.</summary>
    public class CarFX : MonoBehaviour
    {
        ArcadeCar car;
        readonly List<ParticleSystem> smoke = new List<ParticleSystem>();
        readonly List<ParticleSystem> clippings = new List<ParticleSystem>();
        readonly List<TrailRenderer> skids = new List<TrailRenderer>();
        readonly List<ArcadeCar.Wheel> fxWheels = new List<ArcadeCar.Wheel>();

        static readonly int IdCarA = Shader.PropertyToID("_BR_CarA");
        static readonly int IdCarB = Shader.PropertyToID("_BR_CarB");
        static readonly int IdTrail = Shader.PropertyToID("_BR_Trail");
        static readonly int IdTrailSpeed = Shader.PropertyToID("_BR_TrailSpeed");
        readonly Vector4[] trail = new Vector4[6];
        readonly float[] trailSpeed = new float[6];
        float trailTimer;

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            foreach (var w in car.wheels)
            {
                if (w.front) continue;
                fxWheels.Add(w);
                smoke.Add(MakeSmoke());
                skids.Add(MakeSkid());
                clippings.Add(MakeClippings());
            }
            Vector3 p = transform.position;
            for (int i = 0; i < trail.Length; i++) trail[i] = new Vector4(p.x, p.y, p.z, -100f);
        }

        void OnDestroy()
        {
            // effects live in world space (not parented), so clean them up with the car
            foreach (var ps in smoke) if (ps != null) Destroy(ps.gameObject);
            foreach (var ps in clippings) if (ps != null) Destroy(ps.gameObject);
            foreach (var tr in skids) if (tr != null) Destroy(tr.gameObject);
        }

        void Update()
        {
            if (car == null) return;
            // footprint capsule between the axles, on the ground
            Vector3 f = Vector3.zero, r = Vector3.zero;
            int nf = 0, nr = 0;
            foreach (var w in car.wheels)
            {
                Vector3 g = transform.TransformPoint(w.local - Vector3.up * w.radius);
                if (w.front) { f += g; nf++; } else { r += g; nr++; }
            }
            if (nf > 0) f /= nf; else f = transform.position;
            if (nr > 0) r /= nr; else r = transform.position;
            float speed = car.SpeedKmh / 3.6f;
            Shader.SetGlobalVector(IdCarA, new Vector4(f.x, f.y, f.z, 1.35f));
            Shader.SetGlobalVector(IdCarB, new Vector4(r.x, r.y, r.z, speed));
            Vector3 c = (f + r) * 0.5f;
            float now = Time.timeSinceLevelLoad;
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.2f;
                for (int i = trail.Length - 1; i >= 2; i--) { trail[i] = trail[i - 1]; trailSpeed[i] = trailSpeed[i - 1]; }
                trail[1] = new Vector4(c.x, c.y, c.z, now);
                trailSpeed[1] = speed;
            }
            trail[0] = new Vector4(c.x, c.y, c.z, now);
            trailSpeed[0] = speed;
            Shader.SetGlobalVectorArray(IdTrail, trail);
            Shader.SetGlobalFloatArray(IdTrailSpeed, trailSpeed);
        }

        ParticleSystem MakeClippings()
        {
            var go = new GameObject("GrassClippings");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.40f, 0.62f, 0.20f), new Color(0.70f, 0.84f, 0.36f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.9f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.15f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rr = go.GetComponent<ParticleSystemRenderer>();
            rr.sharedMaterial = MaterialLibrary.Particle("Clippings", ProceduralTextures.Petal, Color.white, 0.85f);
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
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
                // grass clippings thrown up behind the tyres
                var cp = clippings[i];
                bool onGrass = w.grounded && w.surface == Surface.Grass && car.SpeedKmh > 12f;
                var cem = cp.emission;
                cem.rateOverTime = onGrass ? Mathf.Min(140f, car.SpeedKmh * 0.9f + car.DriftIntensity * 80f + (car.throttle > 0.5f ? 20f : 0f)) : 0f;
                if (w.grounded)
                {
                    cp.transform.position = w.contact + Vector3.up * 0.1f;
                    Vector3 back = -transform.forward + Vector3.up * 0.9f + transform.right * (w.local.x > 0f ? 0.25f : -0.25f);
                    cp.transform.rotation = Quaternion.LookRotation(back.normalized, Vector3.up);
                }
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

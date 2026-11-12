// CarExtras.cs - per-car visual extras:
//  * headlights at night / in rain (one spot light), brake lights
//  * mud & dust build-up by surface (washes off slowly in the rain), fed to the CarPaint shader
//  * cherry petals swirling up behind the car in drifts
//  * petals settling on the bonnet / roof / boot when slow, blown off at speed
//  * spray from the tyres on wet roads
//  * continuous gamepad rumble from the surface and drifting
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    [RequireComponent(typeof(ArcadeCar))]
    public class CarExtras : MonoBehaviour
    {
        public const int CarLayer = 10;
        static readonly Dictionary<string, float> dirtByCar = new Dictionary<string, float>();
        static readonly Dictionary<string, Color> dirtColourByCar = new Dictionary<string, Color>();

        ArcadeCar car;
        string carId;
        Vector3 extents = new Vector3(0.9f, 1.35f, 2.1f);
        Light headlight;
        ParticleSystem swirl;
        readonly List<ParticleSystem> spray = new List<ParticleSystem>();
        readonly List<ArcadeCar.Wheel> rear = new List<ArcadeCar.Wheel>();
        float dirt;
        Color dirtColour = new Color(0.45f, 0.36f, 0.26f);

        // bonnet petals
        readonly List<Vector3> spots = new List<Vector3>(), spotNormals = new List<Vector3>();
        struct Petal { public int spot; public float size, rot; public Color col; }
        readonly List<Petal> petals = new List<Petal>();
        Mesh petalMesh;
        bool petalsDirty;
        float petalTimer, blowTimer;
        PetalWeather weather;

        public bool ShowroomMode;     // garage turntable: no dirt / petals accumulation

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            carId = car.spec != null ? car.spec.id : name;
            var box = GetComponent<BoxCollider>();
            if (box != null) extents = new Vector3(box.size.x / 0.92f * 0.5f, box.center.y + box.size.y * 0.5f, box.size.z / 0.95f * 0.5f);
            foreach (var w in car.wheels) if (!w.front) rear.Add(w);
            float d;
            dirt = dirtByCar.TryGetValue(carId, out d) ? d : 0f;
            Color c;
            if (dirtColourByCar.TryGetValue(carId, out c)) dirtColour = c;
            LiveryLibrary.ApplySaved(carId);

            var hl = new GameObject("Headlights");
            hl.transform.SetParent(transform, false);
            hl.transform.localPosition = new Vector3(0f, 0.75f + car.FrameExtra * 0.6f, extents.z - 0.2f);   // higher on SUVs
            hl.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            headlight = hl.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.range = 55f;
            headlight.spotAngle = 78f;
            headlight.color = new Color(1f, 0.93f, 0.80f);
            headlight.shadows = LightShadows.None;
            headlight.renderMode = LightRenderMode.ForcePixel;
            headlight.intensity = 0f;
            headlight.enabled = false;

            swirl = MakeSwirl();
            foreach (var w in rear) spray.Add(MakeSpray());
            SampleBodySurfaces();
            var pgo = new GameObject("BonnetPetals");
            pgo.transform.SetParent(transform, false);
            pgo.layer = CarLayer;
            petalMesh = new Mesh { name = "BonnetPetals" };
            petalMesh.MarkDynamic();
            pgo.AddComponent<MeshFilter>().sharedMesh = petalMesh;
            var mr = pgo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Particle("BonnetPetal", ProceduralTextures.Petal, Color.white, 0.85f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#if UNITY_2022_2_OR_NEWER
            weather = Object.FindAnyObjectByType<PetalWeather>();
#else
            weather = Object.FindObjectOfType<PetalWeather>();
#endif
        }

        void OnDestroy()
        {
            dirtByCar[carId ?? name] = dirt;
            dirtColourByCar[carId ?? name] = dirtColour;
            if (swirl != null) Destroy(swirl.gameObject);
            foreach (var p in spray) if (p != null) Destroy(p.gameObject);
            MaterialLibrary.SetBrakeLights(false);
        }

        /// <summary>Wash the car (garage).</summary>
        public void Clean() { dirt = 0f; petals.Clear(); petalsDirty = true; }

        ParticleSystem MakeSwirl()
        {
            var go = new GameObject("PetalSwirl");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.80f, 0.86f), new Color(1f, 0.93f, 0.95f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.05f;
            main.maxParticles = 500;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.8f, 0.3f, 0.6f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.6f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle("PetalSwirl", ProceduralTextures.Petal, Color.white, 0.85f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeSpray()
        {
            var go = new GameObject("TyreSpray");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startColor = new Color(0.86f, 0.89f, 0.95f, 0.30f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle("Spray", ProceduralTextures.Soft, Color.white, 0.7f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Find upward-facing body surfaces (bonnet, roof, boot) by raycasting temporary mesh colliders.</summary>
        void SampleBodySurfaces()
        {
            const int tmpLayer = 31;
            var made = new List<MeshCollider>();
            var layers = new Dictionary<GameObject, int>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<Renderer>();
                if (r == null || mf.sharedMesh == null) continue;
                string n = mf.name;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (n.Contains("Wheel") || n.Contains("Tire") || n.Contains("Rim") || mat.Contains("Glass") || mat.Contains("Light")) continue;
                if (!mf.sharedMesh.isReadable) continue;
                layers[mf.gameObject] = mf.gameObject.layer;
                mf.gameObject.layer = tmpLayer;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                made.Add(mc);
            }
            Physics.SyncTransforms();
            for (float z = -extents.z * 0.92f; z <= extents.z * 0.92f; z += 0.16f)
                for (float x = -extents.x * 0.8f; x <= extents.x * 0.8f; x += 0.13f)
                {
                    Vector3 o = transform.TransformPoint(new Vector3(x, extents.y + 1.5f, z));
                    RaycastHit hit;
                    if (!Physics.Raycast(o, -transform.up, out hit, extents.y + 2f, 1 << tmpLayer, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 ln = transform.InverseTransformDirection(hit.normal);
                    if (ln.y < 0.72f) continue;
                    spots.Add(transform.InverseTransformPoint(hit.point) + ln * 0.006f);
                    spotNormals.Add(ln);
                }
            foreach (var mc in made) if (mc != null) DestroyImmediate(mc);
            foreach (var kv in layers) if (kv.Key != null) kv.Key.layer = kv.Value;
        }

        void Update()
        {
            if (car == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = car.SpeedKmh;
            float night = Atmosphere.NightFactor, rain = Atmosphere.RainIntensity, wet = Atmosphere.Wetness;

            // lights
            float hlWant = Mathf.Clamp01((night - 0.12f) * 1.6f) * 2.4f + rain * 0.6f;
            headlight.enabled = hlWant > 0.02f;
            headlight.intensity = hlWant;
            MaterialLibrary.SetBrakeLights(car.controlsEnabled && car.brake > 0.1f);

            // paint shader frame
            LiveryLibrary.SetCarFrame(carId, transform, extents);

            // dirt build-up by surface; rain slowly washes it
            if (!ShowroomMode)
            {
                int onGround = 0;
                Surface s = Surface.Asphalt;
                foreach (var w in car.wheels) if (w.grounded) { onGround++; s = w.surface; }
                if (onGround > 0 && speed > 8f)
                {
                    float k = Mathf.Clamp01(speed / 70f);
                    float rate = 0f;
                    Color c = dirtColour;
                    switch (s)
                    {
                        case Surface.Gravel: rate = 0.010f; c = new Color(0.66f, 0.58f, 0.45f); break;
                        case Surface.Dirt: rate = 0.016f; c = new Color(0.42f, 0.32f, 0.22f); break;
                        case Surface.Grass: rate = 0.006f; c = new Color(0.36f, 0.36f, 0.20f); break;
                        case Surface.Sand: rate = 0.012f; c = new Color(0.78f, 0.56f, 0.38f); break;
                        case Surface.Snow: rate = 0.004f; c = new Color(0.86f, 0.88f, 0.92f); break;
                        default: rate = wet > 0.3f ? 0.002f : 0f; break;
                    }
                    if (wet > 0.3f && (s == Surface.Dirt || s == Surface.Grass || s == Surface.Gravel)) { rate *= 1.8f; c *= 0.7f; c.a = 1f; }
                    dirt = Mathf.Clamp01(dirt + rate * k * dt * (1f + car.DriftIntensity));
                    if (rate > 0f) dirtColour = Color.Lerp(dirtColour, c, dt * 0.08f * k);
                }
                if (rain > 0.1f) dirt = Mathf.Max(0f, dirt - rain * 0.0025f * dt);
            }
            LiveryLibrary.SetDirt(carId, dirt, dirtColour);

            // petals swirling behind the car in drifts
            Vector3 back = transform.TransformPoint(new Vector3(0f, 0.3f, -extents.z));
            swirl.transform.position = back;
            swirl.transform.rotation = transform.rotation;
            int level = weather != null ? weather.level : 1;
            float swirlRate = car.Grounded && speed > 20f ? Mathf.Clamp01((car.DriftIntensity - 0.2f) * 1.6f) * (25f + level * 35f) : 0f;
            var em = swirl.emission;
            em.rateOverTime = swirlRate * (1f - rain * 0.8f);

            // tyre spray on wet ground
            for (int i = 0; i < rear.Count && i < spray.Count; i++)
            {
                var w = rear[i];
                var ps = spray[i];
                bool on = w.grounded && wet > 0.2f && speed > 25f;
                var sem = ps.emission;
                sem.rateOverTime = on ? wet * Mathf.Min(120f, speed * 1.2f) : 0f;
                if (w.grounded)
                {
                    ps.transform.position = w.contact + Vector3.up * 0.2f;
                    ps.transform.rotation = Quaternion.LookRotation((-transform.forward + Vector3.up * 0.35f).normalized);
                }
            }

            // rumble: surface texture + drifting
            if (car.controlsEnabled && car.Grounded)
            {
                float k = Mathf.Clamp01(speed / 90f);
                float lo = 0f, hi = 0f;
                switch (car.RearSurface)
                {
                    case Surface.Gravel: lo = 0.10f * k; hi = 0.06f * k; break;
                    case Surface.Dirt: lo = 0.12f * k; hi = 0.03f * k; break;
                    case Surface.Grass: lo = 0.08f * k; hi = 0.02f * k; break;
                    case Surface.Snow: lo = 0.06f * k; hi = 0.02f * k; break;
                    case Surface.Sand: lo = 0.09f * k; hi = 0.03f * k; break;
                }
                hi = Mathf.Max(hi, car.DriftIntensity * 0.18f);
                if (lo > 0.01f || hi > 0.01f) InputReader.RumbleContinuous(lo, hi);
            }

            UpdatePetals(dt, speed, level);
        }

        void UpdatePetals(float dt, float speed, int level)
        {
            if (spots.Count == 0) return;
            if (!ShowroomMode && speed < 45f && Atmosphere.RainIntensity < 0.3f && petals.Count < 70)
            {
                petalTimer -= dt;
                if (petalTimer <= 0f)
                {
                    petalTimer = Random.Range(0.6f, 2.2f) / (0.5f + level * 0.8f) * (speed < 5f ? 0.6f : 1f);
                    petals.Add(new Petal
                    {
                        spot = Random.Range(0, spots.Count), size = Random.Range(0.06f, 0.1f), rot = Random.Range(0f, 360f),
                        col = Color.Lerp(new Color(1f, 0.82f, 0.88f), new Color(1f, 0.95f, 0.96f), Random.value)
                    });
                    petalsDirty = true;
                }
            }
            if (petals.Count > 0 && (speed > 65f || Atmosphere.RainIntensity > 0.5f))
            {
                blowTimer -= dt;
                if (blowTimer <= 0f)
                {
                    blowTimer = Mathf.Lerp(0.15f, 0.03f, Mathf.Clamp01((speed - 65f) / 60f));
                    int i = Random.Range(0, petals.Count);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.TransformPoint(spots[petals[i].spot]),
                        velocity = -transform.forward * (2f + speed * 0.05f) + Vector3.up * 1.5f,
                        startColor = petals[i].col
                    };
                    swirl.Emit(ep, 1);
                    petals.RemoveAt(i);
                    petalsDirty = true;
                }
            }
            if (petalsDirty) RebuildPetals();
        }

        readonly List<Vector3> pv = new List<Vector3>();
        readonly List<Vector2> puv = new List<Vector2>();
        readonly List<Color> pc = new List<Color>();
        readonly List<int> pt = new List<int>();

        void RebuildPetals()
        {
            petalsDirty = false;
            pv.Clear(); puv.Clear(); pc.Clear(); pt.Clear();
            foreach (var p in petals)
            {
                Vector3 c = spots[p.spot], n = spotNormals[p.spot];
                Vector3 t = Vector3.Cross(n, Vector3.forward).normalized;
                if (t.sqrMagnitude < 0.01f) t = Vector3.right;
                t = Quaternion.AngleAxis(p.rot, n) * t;
                Vector3 b = Vector3.Cross(n, t);
                float s = p.size * 0.5f;
                int i0 = pv.Count;
                pv.Add(c - t * s - b * s * 1.3f); pv.Add(c + t * s - b * s * 1.3f); pv.Add(c + t * s + b * s * 1.3f); pv.Add(c - t * s + b * s * 1.3f);
                puv.Add(new Vector2(0, 0)); puv.Add(new Vector2(1, 0)); puv.Add(new Vector2(1, 1)); puv.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) pc.Add(p.col);
                pt.Add(i0); pt.Add(i0 + 2); pt.Add(i0 + 1); pt.Add(i0); pt.Add(i0 + 3); pt.Add(i0 + 2);
            }
            petalMesh.Clear();
            petalMesh.SetVertices(pv);
            petalMesh.SetUVs(0, puv);
            petalMesh.SetColors(pc);
            petalMesh.SetTriangles(pt, 0);
            petalMesh.RecalculateBounds();
        }
    }
}

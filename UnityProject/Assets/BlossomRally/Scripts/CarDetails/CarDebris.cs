// CarDebris.cs - stones and gravel kicked up by the tyres on gravel / dirt, and dirt / turf clods on dirt and
// grass. One world-space particle system per car, emitted from code per wheel (rate by surface, speed, wheel
// slip, launch wheelspin and drifting), fast and heavy so they arc back down and bounce on the ground
// (world collision at Quality High+). Not built at Quality Low; CarFX keeps doing the grass clippings.
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarDebris : CarDetailSystem
    {
        ParticleSystem ps;
        float[] acc;
        float rateScale = 1f;

        public override void Build(CarDetails d)
        {
            rateScale = d.quality >= 3 ? 1.5f : d.quality == 2 ? 1f : 0.5f;
            acc = new float[d.car.wheels.Length];
            var go = new GameObject("TyreDebris");
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.15f;
            main.maxParticles = d.quality >= 3 ? 260 : d.quality == 2 ? 180 : 90;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            if (d.quality >= 2)
            {
                var c = ps.collision;
                c.enabled = true;
                c.type = ParticleSystemCollisionType.World;
                c.mode = ParticleSystemCollisionMode.Collision3D;
                c.quality = ParticleSystemCollisionQuality.Low;
                c.bounce = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                c.dampen = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                c.lifetimeLoss = 0.2f;
                c.collidesWith = ~((1 << CarExtras.CarLayer) | (1 << WorldBuilder.LayerGrass));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle("TyreDebris", CarDetailTextures.Rock, Color.white, 0.85f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        public override void Dispose()
        {
            if (ps != null) Object.Destroy(ps.gameObject);
        }

        public override void Tick(CarDetails d, float dt)
        {
            if (ps == null || dt <= 0f || d.showroom || d.frozen) return;
            var car = d.car;
            float kmh = car.SpeedKmh;
            if (kmh < 10f) return;
            Transform t = d.transform;
            Vector3 carVel = t.TransformDirection(d.velLocal);
            bool awd = car.spec != null && car.spec.drive == Drive.AWD;
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var w = car.wheels[i];
                if (!w.grounded) { acc[i] = 0f; continue; }
                float stones = 0f, clods = 0f;
                switch (w.surface)
                {
                    case Surface.Gravel: stones = 60f; break;
                    case Surface.Dirt: stones = 30f; clods = 22f; break;
                    case Surface.Grass: clods = 22f; break;
                    default: continue;
                }
                float slip = Mathf.Clamp01(w.lateralSlip / 6f);
                float spin = d.Driven(w) && car.throttle > 0.5f && kmh < 60f ? 0.5f : 0f;
                float k = Mathf.Clamp01(kmh / 80f) * 0.6f + slip * 0.6f + spin + car.DriftIntensity * 0.5f;
                if (w.front) k *= awd ? 0.7f : 0.35f;
                float rate = (stones + clods) * k * rateScale;
                acc[i] += rate * dt;
                int n = Mathf.Min(6, Mathf.FloorToInt(acc[i]));
                if (n <= 0) continue;
                acc[i] -= n;
                Vector3 fwd = t.forward;
                if (w.front) fwd = Quaternion.AngleAxis(car.SteerAngle, t.up) * fwd;
                float pClod = clods / (stones + clods);
                for (int e = 0; e < n; e++)
                {
                    bool clod = Random.value < pClod;
                    Vector3 v = carVel * 0.35f - fwd * Random.Range(2f, 3f + kmh * 0.08f + slip * 4f)
                                + Vector3.up * Random.Range(1.2f, 3.2f + kmh * 0.025f)
                                + t.right * Random.Range(-1.4f, 1.4f) + t.right * (Vector3.Dot(carVel, t.right) * 0.4f);
                    Color c;
                    if (w.surface == Surface.Gravel) c = Color.Lerp(new Color(0.55f, 0.52f, 0.47f), new Color(0.78f, 0.72f, 0.62f), Random.value);
                    else if (clod && w.surface == Surface.Grass) c = Random.value < 0.5f ? new Color(0.30f, 0.42f, 0.17f) : new Color(0.36f, 0.27f, 0.17f);
                    else if (clod) c = Color.Lerp(new Color(0.30f, 0.21f, 0.13f), new Color(0.44f, 0.33f, 0.22f), Random.value);
                    else c = Color.Lerp(new Color(0.40f, 0.33f, 0.26f), new Color(0.60f, 0.52f, 0.42f), Random.value);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = w.contact + Vector3.up * 0.06f - fwd * (w.radius * 0.4f) + Random.insideUnitSphere * 0.06f,
                        velocity = v,
                        startColor = c,
                        startSize = clod ? Random.Range(0.05f, 0.1f) : Random.Range(0.022f, 0.05f),
                        startLifetime = Random.Range(0.7f, 1.5f)
                    };
                    ps.Emit(ep, 1);
                }
            }
        }
    }
}

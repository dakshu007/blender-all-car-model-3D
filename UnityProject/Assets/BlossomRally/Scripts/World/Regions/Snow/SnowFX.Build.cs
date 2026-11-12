// SnowFX.Build.cs - everything that moves in Frost Summit:
//  * wind-turbine rotors and cable-car gondolas (one LifeBatch, CPU posed),
//  * blinking red beacons on turbines, pylons and the observatory mast, the plough's amber light (one LifeBatch, own glow),
//  * four particle systems: snowfall around the camera (a blizzard when it rains elsewhere), blowing snow streaks
//    across the ground with the gusts, glitter sparkle on sunny snow near the camera, chimney smoke in the village.
// No per-frame allocations; emission work is limited to sources near the camera.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public sealed partial class SnowRegion
    {
        // ---- animated parts
        LifeBatch movers, blinkers;
        Material blinkMat;
        readonly List<Vector3> blinkPos = new List<Vector3>();
        readonly List<int> blinkGroup = new List<int>();
        bool[] blinkOn;
        int[] gondolaPart;
        float[] gondolaS;
        float cableYaw;
        float clock;

        // ---- particles
        ParticleSystem snowfall, drift, sparkle, smoke;
        ParticleSystem.EmitParams ep;
        readonly System.Random fxRng = new System.Random(9157);
        Vector3[] chimneyArr;
        float driftAcc, sparkAcc, smokeT;

        void AddBlinker(Vector3 p, int group)
        {
            blinkPos.Add(p);
            blinkGroup.Add(group);
        }

        float FxR(float a, float b) { return a + (float)fxRng.NextDouble() * (b - a); }

        void BuildFX()
        {
            // ---- rotors + gondolas
            movers = new LifeBatch();
            int rotorT = movers.AddTemplate(SnowShapes.Rotor());
            for (int i = 0; i < rotors.Count; i++)
            {
                var r = rotors[i];
                r.part = movers.AddPart(rotorT);
                rotors[i] = r;
            }
            if (cableSup.Count >= 2)
            {
                Color32[] cols = { LifeGeo.C(214, 52, 46), LifeGeo.C(246, 196, 44), LifeGeo.C(48, 118, 196), LifeGeo.C(236, 238, 242) };
                var tpl = new int[cols.Length];
                for (int i = 0; i < cols.Length; i++) tpl[i] = movers.AddTemplate(SnowShapes.Gondola(cols[i]));
                int n = Mathf.Clamp(Mathf.RoundToInt(cableLen * 2f / 55f), 4, 14);
                gondolaPart = new int[n];
                gondolaS = new float[n];
                for (int i = 0; i < n; i++)
                {
                    gondolaPart[i] = movers.AddPart(tpl[i % tpl.Length]);
                    gondolaS[i] = cableLen * 2f * i / n;
                }
                Vector3 d = cableSup[cableSup.Count - 1] - cableSup[0];
                cableYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            movers.Finish("Snow_Movers", MaterialLibrary.VertexColorWind, ctx.Skyline, 0, true);

            // ---- beacons
            blinkers = new LifeBatch();
            var bulb = new LifeGeo();
            bulb.Ellipsoid(Vector3.zero, new Vector3(0.32f, 0.26f, 0.32f), 6, 4, LifeGeo.C(255, 60, 40), LifeGeo.C(230, 30, 24));
            var amber = new LifeGeo();
            amber.Ellipsoid(Vector3.zero, new Vector3(0.16f, 0.14f, 0.16f), 6, 3, LifeGeo.C(255, 190, 60), LifeGeo.C(255, 150, 30));
            int tb = blinkers.AddTemplate(bulb), ta = blinkers.AddTemplate(amber);
            for (int i = 0; i < blinkPos.Count; i++) blinkers.AddPart(blinkGroup[i] == 3 ? ta : tb);
            blinkMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_SnowBeacons" };
            blinkMat.SetFloat("_WindStrength", 0f);
            blinkers.Finish("Snow_Beacons", blinkMat, ctx.Skyline, 0, false);
            blinkOn = new bool[blinkPos.Count];
            for (int i = 0; i < blinkPos.Count; i++) blinkers.Set(i, Matrix4x4.TRS(blinkPos[i], Quaternion.identity, Vector3.zero));
            blinkers.Upload();

            // ---- particles (4 systems)
            chimneyArr = chimneys.ToArray();
            int q = GameSettings.Quality;
            var flakeMat = MaterialLibrary.Particle("SnowFlake", ProceduralTextures.Soft, Color.white, 0.55f);
            snowfall = MakePS("Snow_FX_Snowfall", 2500 + q * 1500, flakeMat);
            var sm = snowfall.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.9f, 0.95f, 1f, 0.75f));
            var se = snowfall.emission;
            se.enabled = true;
            se.rateOverTime = 0f;
            var ss = snowfall.shape;
            ss.enabled = true;
            ss.shapeType = ParticleSystemShapeType.Box;
            ss.scale = new Vector3(64f, 2f, 64f);
            var sv = snowfall.velocityOverLifetime;
            sv.enabled = true;
            sv.space = ParticleSystemSimulationSpace.World;
            sv.x = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            sv.y = new ParticleSystem.MinMaxCurve(-2.1f, -1.3f);
            sv.z = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            var sn = snowfall.noise;
            sn.enabled = true;
            sn.strength = 0.55f;
            sn.frequency = 0.35f;
            sn.scrollSpeed = 0.2f;
            sn.quality = ParticleSystemNoiseQuality.Low;
            FadeInOut(snowfall, 0.06f, 0.9f);

            drift = MakePS("Snow_FX_Drift", 500 + q * 200, MaterialLibrary.Particle("SnowDrift", ProceduralTextures.Soft, Color.white, 0.7f));
            var dr = drift.GetComponent<ParticleSystemRenderer>();
            dr.renderMode = ParticleSystemRenderMode.Stretch;
            dr.velocityScale = 0.12f;
            dr.lengthScale = 2.2f;
            FadeInOut(drift, 0.2f, 0.7f);

            var sparkMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_SnowSparkle" };
            sparkMat.SetTexture("_MainTex", ProceduralTextures.Soft);
            sparkMat.SetColor("_Tint", Color.white);
            sparkMat.SetVector("_Fade", new Vector4(0.6f, 2.5f, 26f, 36f));
            sparkMat.SetFloat("_EdgeFade", 0f);
            sparkMat.SetFloat("_VertexGamma", 0f);
            sparkle = MakePS("Snow_FX_Sparkle", 220 + q * 60, sparkMat);
            var spc = sparkle.colorOverLifetime;
            spc.enabled = true;
            var spg = new Gradient();
            spg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            spc.color = new ParticleSystem.MinMaxGradient(spg);

            smoke = MakePS("Snow_FX_ChimneySmoke", 300 + q * 80, MaterialLibrary.Particle("SnowChimney", ProceduralTextures.Soft, Color.white, 0.75f));
            var szl = smoke.sizeOverLifetime;
            szl.enabled = true;
            szl.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f));
            var sml = smoke.colorOverLifetime;
            sml.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            sml.color = new ParticleSystem.MinMaxGradient(sg);
            var smn = smoke.noise;
            smn.enabled = true;
            smn.strength = 0.4f;
            smn.frequency = 0.5f;
            smn.scrollSpeed = 0.3f;
            smn.quality = ParticleSystemNoiseQuality.Low;
            foreach (var ps in new[] { snowfall, drift, sparkle, smoke }) ps.Play();
        }

        ParticleSystem MakePS(string name, int max, Material mat)
        {
            var go = new GameObject(name);
            go.layer = WorldBuilder.LayerTrees;
            go.transform.SetParent(ctx.Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            main.playOnAwake = false;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        static void FadeInOut(ParticleSystem ps, float inT, float outT)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inT), new GradientAlphaKey(1f, outT), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        // ================================================================== per frame
        public override void Tick(float dt, Vector3 eye, float presence)
        {
            if (movers == null) return;
            clock += dt;
            float gust = Mathf.Clamp01(Atmosphere.Gust);
            float night = Atmosphere.NightFactor, rain = Mathf.Clamp01(Atmosphere.RainIntensity), cloud = Atmosphere.CloudCover;

            // ---- turbines + gondolas
            for (int i = 0; i < rotors.Count; i++)
            {
                var r = rotors[i];
                r.angle = Mathf.Repeat(r.angle + r.speed * (0.7f + 0.6f * gust + 0.4f * rain) * dt, 360f);
                rotors[i] = r;
                movers.Set(r.part, Matrix4x4.TRS(r.hub, Quaternion.Euler(0f, r.yaw, 0f) * Quaternion.Euler(0f, 0f, r.angle), Vector3.one));
            }
            if (gondolaPart != null && cableLen > 1f)
            {
                Quaternion qy = Quaternion.Euler(0f, cableYaw, 0f);
                float L2 = cableLen * 2f;
                for (int i = 0; i < gondolaPart.Length; i++)
                {
                    float s = Mathf.Repeat(gondolaS[i] + clock * 4.2f, L2);
                    int side = s < cableLen ? 1 : -1;
                    float along = side > 0 ? s : L2 - s;
                    int span = 0;
                    while (span < cableCum.Count - 2 && along > cableCum[span + 1]) span++;
                    float len = Mathf.Max(0.1f, cableCum[span + 1] - cableCum[span]);
                    float t = Mathf.Clamp01((along - cableCum[span]) / len);
                    Vector3 p = CablePoint(span, t, side);
                    float swing = Mathf.Sin(clock * 1.3f + i * 1.7f) * (1.5f + 4f * gust);
                    movers.Set(gondolaPart[i], Matrix4x4.TRS(p, qy * Quaternion.Euler(swing, 0f, 0f), Vector3.one));
                }
            }
            movers.Upload();

            // ---- beacons: short red flashes (groups out of phase), amber plough light rotating
            bool changed = false;
            for (int i = 0; i < blinkOn.Length; i++)
            {
                int gi = blinkGroup[i];
                float ph = Mathf.Repeat(clock / (gi == 3 ? 0.7f : 1.6f) + gi * 0.31f, 1f);
                bool on = ph < (gi == 3 ? 0.45f : 0.16f);
                if (on == blinkOn[i]) continue;
                blinkOn[i] = on;
                changed = true;
                blinkers.Set(i, Matrix4x4.TRS(blinkPos[i], Quaternion.identity, on ? Vector3.one : Vector3.zero));
            }
            if (changed) blinkers.Upload();
            blinkMat.SetFloat("_Glow", 0.9f + 3.2f * night);

            // ---- particles
            if (snowfall == null) return;
            Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 wind = new Vector3(1f, 0f, 0.35f).normalized;
            float pres = Mathf.Clamp01(presence * 1.15f);
            int q = GameSettings.Quality;
            float qk = 0.6f + 0.25f * q;

            // snowfall: light flurries always, thicker under cloud, a blizzard in 'rain' weather
            snowfall.transform.position = eye + fwd * 12f + Vector3.up * 15f;
            float rate = pres * qk * (90f + 420f * cloud + 2600f * rain);
            var em = snowfall.emission;
            em.rateOverTime = rate;
            var vel = snowfall.velocityOverLifetime;
            float blow = 0.8f + 2.5f * gust + 9f * rain;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x * blow * 0.7f, wind.x * blow * 1.2f);
            vel.z = new ParticleSystem.MinMaxCurve(wind.z * blow * 0.7f, wind.z * blow * 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-2.2f - 1.5f * rain, -1.2f - 0.8f * rain);
            if (pres < 0.01f) return;

            // blowing snow streaks skimming the ground
            driftAcc += dt * pres * qk * (12f + 150f * gust + 260f * rain);
            int nd = Mathf.Min(30, Mathf.FloorToInt(driftAcc));
            driftAcc -= nd;
            for (int i = 0; i < nd; i++)
            {
                Vector2 o = new Vector2(FxR(-34f, 34f), FxR(-34f, 34f));
                Vector3 p = eye + fwd * 14f + new Vector3(o.x, 0f, o.y) - wind * 10f;
                p.y = ctx.Ground(p.x, p.z) + FxR(0.05f, 0.9f + 2f * rain);
                float sp = FxR(6f, 12f) * (0.6f + gust + rain);
                ep = new ParticleSystem.EmitParams();
                ep.position = p;
                ep.velocity = wind * sp + new Vector3(FxR(-0.6f, 0.6f), FxR(-0.1f, 0.35f), FxR(-0.6f, 0.6f));
                ep.startSize = FxR(0.12f, 0.3f);
                ep.startLifetime = FxR(0.9f, 1.8f);
                ep.startColor = new Color(1f, 1f, 1f, FxR(0.25f, 0.5f));
                drift.Emit(ep, 1);
            }

            // sun glitter on the snow near the camera
            float sunny = (1f - night) * (1f - cloud * 0.85f) * (1f - rain);
            sparkAcc += dt * pres * sunny * qk * 70f;
            int ns = Mathf.Min(12, Mathf.FloorToInt(sparkAcc));
            sparkAcc -= ns;
            for (int i = 0; i < ns; i++)
            {
                Vector2 o = new Vector2(FxR(-16f, 16f), FxR(-4f, 26f));
                Vector3 p = eye + fwd * o.y + new Vector3(fwd.z, 0f, -fwd.x) * o.x;
                p.y = ctx.Ground(p.x, p.z) + 0.06f;
                ep = new ParticleSystem.EmitParams();
                ep.position = p;
                ep.velocity = Vector3.zero;
                ep.startSize = FxR(0.04f, 0.11f);
                ep.startLifetime = FxR(0.25f, 0.6f);
                ep.startColor = fxRng.NextDouble() < 0.3 ? new Color(0.75f, 0.9f, 1f, 1f) : new Color(1f, 1f, 0.96f, 1f);
                sparkle.Emit(ep, 1);
            }

            // chimney smoke drifting with the wind
            smokeT += dt;
            if (smokeT >= 0.12f && chimneyArr != null)
            {
                float step = smokeT;
                smokeT = 0f;
                for (int i = 0; i < chimneyArr.Length; i++)
                {
                    Vector3 c = chimneyArr[i];
                    if ((c - eye).sqrMagnitude > 260f * 260f) continue;
                    if (fxRng.NextDouble() > 2.6f * step) continue;
                    ep = new ParticleSystem.EmitParams();
                    ep.position = c + new Vector3(FxR(-0.1f, 0.1f), 0f, FxR(-0.1f, 0.1f));
                    ep.velocity = new Vector3(0f, FxR(0.5f, 0.8f), 0f) + wind * (0.5f + 1.5f * gust);
                    ep.startSize = FxR(0.9f, 1.4f);
                    ep.startLifetime = FxR(6f, 9f);
                    ep.startColor = new Color(0.86f, 0.87f, 0.9f, 0.5f);
                    ep.rotation = FxR(0f, 360f);
                    smoke.Emit(ep, 1);
                }
            }
        }
    }
}

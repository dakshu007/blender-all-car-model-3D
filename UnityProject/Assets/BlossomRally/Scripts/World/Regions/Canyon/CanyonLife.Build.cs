// CanyonLife.Build.cs - what moves in the Red Canyon: the jade Crescent Spring (own water material), a camel
// caravan pacing round the dune sea, tumbleweeds rolling with the wind, two wandering dust devils, sand
// streaming off the dunes, tea steam / beacon smoke, and a heat shimmer over the road ahead at midday.
// Four particle systems, two CPU-posed batches, one haze quad. Tick does no allocations.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public sealed partial class CanyonRegion
    {
        // caravan loop inside the dune drift ring (clear of the track by 30+ m)
        static readonly Vector2[] CaravanLoop =
        {
            new Vector2(-1280f, 1290f), new Vector2(-1290f, 1338f), new Vector2(-1325f, 1358f), new Vector2(-1362f, 1328f),
            new Vector2(-1394f, 1296f), new Vector2(-1372f, 1264f), new Vector2(-1322f, 1262f)
        };
        static readonly Vector2 WindDir = new Vector2(0.8f, -0.6f);   // the dunes migrate south-east

        LifeBatch caravan, weeds;
        Vector2[] loopPts; float[] loopLen; float loopTotal;
        const int Camels = 5;
        float caravanS, caravanT;
        int[] camelBody, camelLeg, driverBody, driverLeg;

        const int Weeds = 8;
        readonly Vector3[] weedPos = new Vector3[Weeds];
        readonly float[] weedSpeed = new float[Weeds], weedAng = new float[Weeds], weedR = new float[Weeds], weedPhase = new float[Weeds];
        bool weedsHidden;
        System.Random lifeRng;

        ParticleSystem[] devils = new ParticleSystem[2];
        readonly Vector3[] devilPos = new Vector3[2];
        readonly Vector2[] devilVel = new Vector2[2];
        readonly float[] devilLife = new float[2];
        ParticleSystem sand, smoke;
        ParticleSystem.EmitParams ep;
        Vector3[] steamA, fireA;
        float smokeT, hazeT;

        Transform haze;
        MeshRenderer hazeR;
        Material hazeMat;
        bool hazeOn;
        Material poolMat;

        float LR(float a, float b) { return a + (float)lifeRng.NextDouble() * (b - a); }

        void BuildLife()
        {
            lifeRng = new System.Random(9091);
            BuildPool();
            BuildCaravan();
            BuildWeeds();
            BuildParticles();
            BuildHaze();
        }

        // ------------------------------------------------------------------ jade pool
        void BuildPool()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var uv = new List<Vector2>(); var t = new List<int>();
            const float step = 2f;
            float y = PoolLevel;
            for (float z = OasisA.y - OasisRA - 4f; z < OasisA.y + OasisRA + 4f; z += step)
                for (float x = OasisA.x - OasisRA - 4f; x < OasisA.x + OasisRA + 4f; x += step)
                {
                    float s0 = OasisSdf(x, z), s1 = OasisSdf(x + step, z), s2 = OasisSdf(x + step, z + step), s3 = OasisSdf(x, z + step);
                    if (Mathf.Min(Mathf.Min(s0, s1), Mathf.Min(s2, s3)) > 2.4f) continue;
                    int b = v.Count;
                    Vector3[] q = { new Vector3(x, y, z), new Vector3(x + step, y, z), new Vector3(x + step, y, z + step), new Vector3(x, y, z + step) };
                    foreach (var p in q)
                    {
                        v.Add(p); n.Add(Vector3.up); c.Add(new Color32(255, 255, 255, 255)); uv.Add(new Vector2(p.x * 0.05f, p.z * 0.05f));
                    }
                    t.Add(b); t.Add(b + 2); t.Add(b + 1);
                    t.Add(b); t.Add(b + 3); t.Add(b + 2);
                }
            if (v.Count == 0) return;
            var mesh = new Mesh { name = "Canyon_CrescentSpring" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            poolMat = new Material(MaterialLibrary.Water) { name = "BR_CanyonJadePool" };
            poolMat.SetColor("_Shallow", new Color(0.42f, 0.80f, 0.66f, 0.7f));
            poolMat.SetColor("_Deep", new Color(0.10f, 0.46f, 0.42f, 0.9f));
            poolMat.SetFloat("_RippleScale", 0.5f);
            poolMat.SetFloat("_RippleSpeed", 0.35f);
            var go = ctx.AddMesh("Canyon_Pool", mesh, poolMat, WorldBuilder.LayerTrees, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        // ------------------------------------------------------------------ camel caravan
        void BuildCaravan()
        {
            var pts = Spline.Sample(CaravanLoop, true, 1.5f);
            loopPts = pts.ToArray();
            loopLen = new float[loopPts.Length + 1];
            for (int i = 1; i <= loopPts.Length; i++) loopLen[i] = loopLen[i - 1] + Vector2.Distance(loopPts[i - 1], loopPts[i % loopPts.Length]);
            loopTotal = loopLen[loopPts.Length];
            caravan = new LifeBatch();
            camelBody = new int[Camels]; camelLeg = new int[Camels * 4];
            for (int i = 0; i < Camels; i++)
            {
                int tb = caravan.AddTemplate(CanyonBuildings.CamelBody(lifeRng, i, false, i > 0));
                int tl = caravan.AddTemplate(CanyonBuildings.CamelLeg(i));
                camelBody[i] = caravan.AddPart(tb);
                for (int k = 0; k < 4; k++) camelLeg[i * 4 + k] = caravan.AddPart(tl);
            }
            int td = caravan.AddTemplate(CanyonBuildings.Driver(lifeRng)), tdl = caravan.AddTemplate(CanyonBuildings.DriverLeg());
            driverBody = new[] { caravan.AddPart(td) };
            driverLeg = new[] { caravan.AddPart(tdl), caravan.AddPart(tdl) };
            caravan.Finish("Canyon_Caravan", MaterialLibrary.VertexColorWind, ctx.Root, 0, true);
            caravanS = 0f;
            PoseCaravan(0f, true);
        }

        void LoopAt(float s, out Vector2 p, out Vector2 dir)
        {
            s = Mathf.Repeat(s, loopTotal);
            int lo = 0, hi = loopPts.Length;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (loopLen[mid] <= s) lo = mid; else hi = mid; }
            Vector2 a = loopPts[lo], b = loopPts[(lo + 1) % loopPts.Length];
            float seg = loopLen[lo + 1] - loopLen[lo];
            p = Vector2.Lerp(a, b, seg > 1e-4f ? (s - loopLen[lo]) / seg : 0f);
            dir = (b - a).normalized;
        }

        void PoseCaravan(float time, bool force)
        {
            const float gap = 4.6f;
            for (int i = 0; i <= Camels; i++)
            {
                Vector2 p, d;
                LoopAt(caravanS - i * gap + (i == 0 ? 0f : -1.5f), out p, out d);
                // smooth the heading over a few metres so turns look natural
                Vector2 p2, d2;
                LoopAt(caravanS - i * gap - 2f, out p2, out d2);
                Vector2 fwd = (p - p2).sqrMagnitude > 1e-4f ? (p - p2).normalized : d;
                float hf = ctx.Ground(p.x + fwd.x * 1.1f, p.y + fwd.y * 1.1f), hb = ctx.Ground(p.x - fwd.x * 1.1f, p.y - fwd.y * 1.1f);
                float pitch = Mathf.Atan2(hb - hf, 2.2f) * Mathf.Rad2Deg;
                float yaw = YawOf(fwd);
                float phase = time * 2.6f + i * 1.3f;
                Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 pos = new Vector3(p.x, (hf + hb) * 0.5f, p.y);
                if (i == 0)
                {
                    // the driver walks ahead holding the lead rope
                    Matrix4x4 bm = Matrix4x4.TRS(pos + Vector3.up * (0.04f * Mathf.Abs(Mathf.Sin(phase * 2f))), q, Vector3.one);
                    caravan.Set(driverBody[0], bm);
                    for (int k = 0; k < 2; k++)
                    {
                        float sw = Mathf.Sin(phase * 2f + k * Mathf.PI) * 28f;
                        caravan.Set(driverLeg[k], bm * Matrix4x4.TRS(new Vector3(k == 0 ? 0.11f : -0.11f, 0.78f, 0f), Quaternion.Euler(sw, 0f, 0f), Vector3.one));
                    }
                    continue;
                }
                int c = i - 1;
                float bob = 0.06f * Mathf.Sin(phase * 2f);
                float roll = 3f * Mathf.Sin(phase);
                Matrix4x4 body = Matrix4x4.TRS(pos + Vector3.up * bob, q * Quaternion.Euler(0f, 0f, roll), Vector3.one);
                caravan.Set(camelBody[c], body);
                for (int k = 0; k < 4; k++)
                {
                    bool left = (k & 1) == 0, front = k < 2;
                    // pacing gait: both legs on one side move together
                    float sw = Mathf.Sin(phase + (left ? 0f : Mathf.PI)) * 24f;
                    Vector3 hip = new Vector3(left ? -0.3f : 0.3f, 1.55f, front ? 0.72f : -0.7f);
                    caravan.Set(camelLeg[c * 4 + k], body * Matrix4x4.TRS(hip, Quaternion.Euler(sw, 0f, 0f), Vector3.one));
                }
            }
            caravan.Upload();
        }

        // ------------------------------------------------------------------ tumbleweeds
        void BuildWeeds()
        {
            weeds = new LifeBatch();
            int t0 = weeds.AddTemplate(CanyonKit.Tumbleweed(lifeRng)), t1 = weeds.AddTemplate(CanyonKit.Tumbleweed(lifeRng));
            for (int i = 0; i < Weeds; i++)
            {
                weeds.AddPart(i % 2 == 0 ? t0 : t1);
                weedR[i] = LR(0.35f, 0.6f);
                weedSpeed[i] = LR(2.5f, 5.5f);
                weedPhase[i] = LR(0f, 6.28f);
                weedPos[i] = new Vector3(1e5f, 0f, 1e5f);
            }
            weeds.Finish("Canyon_Tumbleweeds", MaterialLibrary.VertexColorWind, ctx.Root, WorldBuilder.LayerTrees, true);
            weedsHidden = true;
        }

        void RespawnWeed(int i, Vector3 eye, bool anywhere)
        {
            Vector2 side = new Vector2(-WindDir.y, WindDir.x);
            for (int tries = 0; tries < 6; tries++)
            {
                Vector2 p = new Vector2(eye.x, eye.z) - WindDir * (anywhere ? LR(-60f, 80f) : LR(60f, 110f)) + side * LR(-70f, 70f);
                if (!ctx.InRegion(p, 50f) || OasisSdf(p.x, p.y) < 4f) continue;
                weedPos[i] = new Vector3(p.x, ctx.Ground(p.x, p.y) + weedR[i], p.y);
                weedSpeed[i] = LR(2.5f, 5.5f);
                return;
            }
            weedPos[i] = new Vector3(1e5f, 0f, 1e5f);
        }

        void TickWeeds(float dt, Vector3 eye, float presence)
        {
            if (presence < 0.05f)
            {
                if (!weedsHidden)
                {
                    for (int i = 0; i < Weeds; i++) weeds.Set(i, Matrix4x4.zero);
                    weeds.Upload();
                    weedsHidden = true;
                }
                return;
            }
            float gust = 0.75f + 0.6f * Atmosphere.Gust;
            float wet = 1f - 0.8f * Atmosphere.Wetness;
            Vector3 wd = new Vector3(WindDir.x, 0f, WindDir.y);
            Vector3 axis = Vector3.Cross(Vector3.up, wd);
            for (int i = 0; i < Weeds; i++)
            {
                Vector3 p = weedPos[i];
                float dx = p.x - eye.x, dz = p.z - eye.z;
                if (weedsHidden || dx * dx + dz * dz > 130f * 130f) { RespawnWeed(i, eye, weedsHidden); p = weedPos[i]; }
                float sp = weedSpeed[i] * gust * wet;
                weedPhase[i] += dt * sp * 0.9f;
                p += wd * (sp * dt) + new Vector3(-wd.z, 0f, wd.x) * (Mathf.Sin(weedPhase[i] * 0.37f) * 0.6f * dt);
                float g = ctx.Ground(p.x, p.z);
                float hop = Mathf.Abs(Mathf.Sin(weedPhase[i])) * 0.55f * weedR[i] * (0.5f + 0.5f * gust);
                p.y = g + weedR[i] + hop;
                weedPos[i] = p;
                weedAng[i] += sp * dt / weedR[i] * Mathf.Rad2Deg;
                weeds.Set(i, Matrix4x4.TRS(p, Quaternion.AngleAxis(weedAng[i], axis), Vector3.one * weedR[i]));
            }
            weedsHidden = false;
            weeds.Upload();
        }

        // ------------------------------------------------------------------ particles
        static ParticleSystem MakePS(Transform parent, string name, int max, Material mat, bool local)
        {
            var go = new GameObject(name);
            go.layer = WorldBuilder.LayerTrees;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
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

        static void FadeCurve(ParticleSystem ps, float a0, float a1)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, a0), new GradientAlphaKey(0.7f, a1), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        void BuildParticles()
        {
            int q = GameSettings.Quality;
            var dust = MaterialLibrary.Particle("CanyonDust", ProceduralTextures.Soft, Color.white, 0.85f);
            for (int d = 0; d < 2; d++)
            {
                var ps = MakePS(ctx.Root, "Canyon_FX_DustDevil" + d, 120 + q * 40, dust, true);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.62f, 0.44f, 0.32f), new Color(0.92f, 0.74f, 0.54f, 0.22f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var em = ps.emission;
                em.enabled = true;
                em.rateOverTime = 0f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(2.6f, 0.2f, 2.6f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                // all curves of a group in the same mode (two constants), or Unity ignores them
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(2.6f, 4.2f);
                vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.orbitalY = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
                vel.radial = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.4f));
                FadeCurve(ps, 0.15f, 0.6f);
                ps.Play();
                devils[d] = ps;
                devilLife[d] = 0f;
            }
            sand = MakePS(ctx.Root, "Canyon_FX_SandDrift", 140 + q * 50, dust, false);
            {
                var main = sand.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.98f, 0.78f, 0.54f, 0.22f), new Color(0.95f, 0.70f, 0.48f, 0.14f));
                var em = sand.emission;
                em.enabled = true;
                em.rateOverTime = 0f;
                var sh = sand.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(70f, 0.6f, 70f);
                var vel = sand.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(WindDir.x * 4f, WindDir.x * 7f);
                vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
                vel.z = new ParticleSystem.MinMaxCurve(WindDir.y * 4f, WindDir.y * 7f);
                var sz = sand.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
                FadeCurve(sand, 0.2f, 0.6f);
                sand.Play();
            }
            smoke = MakePS(ctx.Root, "Canyon_FX_Smoke", 90 + q * 30, MaterialLibrary.Particle("CanyonSmoke", ProceduralTextures.Soft, Color.white, 0.75f), false);
            {
                var sz = smoke.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));
                FadeCurve(smoke, 0.15f, 0.6f);
                var nz = smoke.noise;
                nz.enabled = true;
                nz.strength = 0.35f;
                nz.frequency = 0.6f;
                nz.scrollSpeed = 0.3f;
                nz.quality = ParticleSystemNoiseQuality.Low;
                smoke.Play();
            }
            steamA = steamSpots.ToArray();
            fireA = fireSpots.ToArray();
        }

        bool OpenGround(Vector2 p)
        {
            if (!ctx.InRegion(p, 60f)) return false;
            if (OasisSdf(p.x, p.y) < 15f || DuneMask(p.x, p.y) > 0.5f) return false;
            float h = ctx.Ground(p.x, p.y);
            return h - Plane(p.x, p.y) < 4f;
        }

        void TickDevils(float dt, Vector3 eye, float presence)
        {
            bool weather = Atmosphere.RainIntensity < 0.2f && Atmosphere.NightFactor < 0.5f;
            for (int d = 0; d < 2; d++)
            {
                var ps = devils[d];
                if (ps == null) continue;
                devilLife[d] -= dt;
                Vector3 p = devilPos[d];
                float dx = p.x - eye.x, dz = p.z - eye.z;
                if (devilLife[d] <= 0f || dx * dx + dz * dz > 320f * 320f)
                {
                    // relocate: an open patch 90..240 m from the camera
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = LR(0f, 6.28f), r = LR(90f, 240f);
                        Vector2 c = new Vector2(eye.x + Mathf.Cos(a) * r, eye.z + Mathf.Sin(a) * r);
                        if (!OpenGround(c)) continue;
                        p = new Vector3(c.x, ctx.Ground(c.x, c.y), c.y);
                        devilVel[d] = WindDir * LR(1.5f, 3f) + new Vector2(LR(-1f, 1f), LR(-1f, 1f));
                        devilLife[d] = LR(25f, 45f);
                        break;
                    }
                }
                devilVel[d] += new Vector2(LR(-1f, 1f), LR(-1f, 1f)) * dt * 0.8f;
                p.x += devilVel[d].x * dt; p.z += devilVel[d].y * dt;
                p.y = ctx.Ground(p.x, p.z) - 0.2f;
                devilPos[d] = p;
                ps.transform.position = p;
                float life = devilLife[d];
                float fade = Mathf.Clamp01(life / 4f) * Mathf.Clamp01((45f - life) / 4f);
                var em = ps.emission;
                em.rateOverTime = weather && presence > 0.05f ? (14f + 6f * GameSettings.Quality) * fade * (0.7f + 0.5f * Atmosphere.Gust) : 0f;
            }
        }

        void TickSand(Vector3 eye, float presence)
        {
            var em = sand.emission;
            float dm = DuneMask(eye.x, eye.z);
            if (dm < 0.2f || presence < 0.05f || Atmosphere.Wetness > 0.4f) { em.rateOverTime = 0f; return; }
            sand.transform.position = new Vector3(eye.x - WindDir.x * 15f, ctx.Ground(eye.x, eye.z) + 0.3f, eye.z - WindDir.y * 15f);
            em.rateOverTime = (10f + 6f * GameSettings.Quality) * dm * (0.6f + 0.8f * Atmosphere.Gust);
        }

        void TickSmoke(float dt, Vector3 eye)
        {
            smokeT += dt;
            if (smokeT < 0.1f) return;
            float step = smokeT;
            smokeT = 0f;
            EmitSmoke(steamA, eye, step, 3f, new Color(1f, 1f, 1f, 0.42f), 0.35f, 2.6f, 0.45f);
            if (Atmosphere.NightFactor > 0.3f) EmitSmoke(fireA, eye, step, 4f, new Color(0.62f, 0.56f, 0.52f, 0.5f), 1.1f, 6f, 1.2f);
        }

        void EmitSmoke(Vector3[] src, Vector3 eye, float step, float rate, Color c, float size, float life, float rise)
        {
            if (src == null) return;
            for (int i = 0; i < src.Length; i++)
            {
                if ((src[i] - eye).sqrMagnitude > 150f * 150f) continue;
                if (lifeRng.NextDouble() > rate * step) continue;
                ep = new ParticleSystem.EmitParams();
                ep.position = src[i] + new Vector3(LR(-0.05f, 0.05f), 0f, LR(-0.05f, 0.05f));
                ep.velocity = new Vector3(WindDir.x * 0.3f + LR(-0.05f, 0.05f), rise * LR(0.8f, 1.2f), WindDir.y * 0.3f);
                ep.startColor = c;
                ep.startSize = size * LR(0.8f, 1.25f);
                ep.startLifetime = life * LR(0.8f, 1.2f);
                ep.rotation = LR(0f, 360f);
                smoke.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ heat shimmer over the road ahead
        void BuildHaze()
        {
            if (GameSettings.Quality < 1) return;
            var sh = MaterialLibrary.FindShader("BlossomRally/HeatHaze");
            if (sh == null) return;
            hazeMat = new Material(sh) { name = "BR_CanyonRoadHaze" };
            hazeMat.SetTexture("_NoiseTex", CarDetailTextures.Noise);
            hazeMat.SetFloat("_Strength", 0f);
            var mesh = new Mesh { name = "Canyon_HazeQuad" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            var go = ctx.AddMesh("Canyon_RoadHaze", mesh, hazeMat, 0, false);
            hazeR = go.GetComponent<MeshRenderer>();
            hazeR.receiveShadows = false;
            hazeR.enabled = false;
            haze = go.transform;
        }

        void TickHaze(float dt, Vector3 eye, float presence)
        {
            if (haze == null) return;
            var atm = Atmosphere.Instance;
            int tod = atm != null ? atm.TimeIndex : 1;
            float day = tod == 1 ? 1f : tod == 0 ? 0.35f : 0f;
            float k = day * presence * (1f - Atmosphere.Wetness) * (1f - Atmosphere.CloudCover * 0.7f) * (1f - Atmosphere.NightFactor);
            hazeT -= dt;
            if (hazeT <= 0f)
            {
                hazeT = 0.2f;
                hazeOn = false;
                var cam = Camera.main;
                if (k > 0.05f && cam != null)
                {
                    Vector3 f = cam.transform.forward; f.y = 0f;
                    if (f.sqrMagnitude > 1e-3f)
                    {
                        f.Normalize();
                        Vector2 ahead = new Vector2(eye.x + f.x * 38f, eye.z + f.z * 38f);
                        RoadSample rs;
                        if (ctx.Field.NearestRoad(ahead, out rs, 12f) < 12f)
                        {
                            haze.position = new Vector3(rs.pos.x, rs.height + 0.15f, rs.pos.y);
                            haze.rotation = Quaternion.LookRotation(f, Vector3.up);
                            haze.localScale = new Vector3(rs.halfWidth * 2f + 10f, 1.6f, 1f);
                            hazeOn = true;
                        }
                    }
                }
            }
            if (hazeR.enabled != hazeOn) hazeR.enabled = hazeOn;
            if (hazeOn) hazeMat.SetFloat("_Strength", 0.55f * k);
        }

        // ------------------------------------------------------------------ per frame
        public override void Tick(float dt, Vector3 eye, float presence)
        {
            if (ctx == null || caravan == null) return;
            float cdx = eye.x - (-1335f), cdz = eye.z - 1320f;
            caravanS += dt * 1.05f;
            caravanT += dt;
            // the caravan is slow: pose it at ~30 Hz, and only when it can be seen
            if (cdx * cdx + cdz * cdz < 320f * 320f && caravanT >= 0.033f)
            {
                caravanT = 0f;
                PoseCaravan(Time.time, false);
            }
            TickWeeds(dt, eye, presence);
            TickDevils(dt, eye, presence);
            TickSand(eye, presence);
            TickSmoke(dt, eye);
            TickHaze(dt, eye, presence);
        }
    }
}

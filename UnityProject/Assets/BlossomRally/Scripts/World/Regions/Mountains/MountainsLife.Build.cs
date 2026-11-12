// MountainsLife.Build.cs - what moves in the Cloud Peak Mountains: eagles circling the peaks and the cable car
// cabins (one CPU-posed LifeBatch, one draw call), the drifting cloud sea, waterfall spray, incense smoke at the
// monastery, falling maple leaves (3 particle systems), the roar of the falls and the monastery bell that rings
// when a car arrives at the summit. Tick is allocation free and throttled.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public sealed partial class MountainsRegion
    {
        LifeBatch life;
        int[] eagleBody, eagleL, eagleR;
        Vector4[] eagleOrbit;            // centre x, centre z, radius, height
        float[] eagleAng, eagleSpeed, eaglePhase;
        int[] cabinPart;
        float[] cabinS;
        ParticleSystem spray, smoke, leaves;
        ParticleSystem.EmitParams ep;
        Vector3[] sprayA, incA, mapleA;
        float lifeT, fxT, leafT, matT, bellCool;
        bool bellArmed = true;
        AudioSource fallsSrc, bellSrc;
        readonly System.Random fxRng = new System.Random(5303);

        float FR(float a, float b) { return a + (float)fxRng.NextDouble() * (b - a); }

        IEnumerator BuildLife()
        {
            // ---- eagles + cable car cabins in one animated batch
            life = new LifeBatch();
            int tBody = life.AddTemplate(MtnShapes.EagleBody()), tL = life.AddTemplate(MtnShapes.EagleWing(-1)), tR = life.AddTemplate(MtnShapes.EagleWing(1));
            eagleOrbit = new[]
            {
                new Vector4(236f, 1420f, 70f, 236f), new Vector4(20f, 760f, 95f, 104f), new Vector4(-300f, 1240f, 80f, 128f), new Vector4(260f, 820f, 110f, 150f)
            };
            int ne = eagleOrbit.Length;
            eagleBody = new int[ne]; eagleL = new int[ne]; eagleR = new int[ne];
            eagleAng = new float[ne]; eagleSpeed = new float[ne]; eaglePhase = new float[ne];
            for (int i = 0; i < ne; i++)
            {
                eagleBody[i] = life.AddPart(tBody); eagleL[i] = life.AddPart(tL); eagleR[i] = life.AddPart(tR);
                eagleAng[i] = FR(0f, 6.28f);
                eagleSpeed[i] = (i & 1) == 0 ? 0.11f : -0.09f;
                eaglePhase[i] = FR(0f, 20f);
            }
            if (cableA != null)
            {
                int tc0 = life.AddTemplate(MtnShapes.Cabin(PG.C(204, 52, 44))), tc1 = life.AddTemplate(MtnShapes.Cabin(PG.C(52, 140, 120)));
                cabinPart = new int[4];
                cabinS = new float[4];
                for (int i = 0; i < 4; i++) { cabinPart[i] = life.AddPart((i & 1) == 0 ? tc0 : tc1); cabinS[i] = i * 0.5f; }
            }
            life.Finish("Mtn_Life", MaterialLibrary.VertexColorWind, ctx.Root, 0, true);
            PoseLife(0f);
            yield return null;

            // ---- particles
            sprayA = sprayPts.ToArray();
            incA = incensePts.ToArray();
            mapleA = mapleTrees.ToArray();
            spray = MakePS("Mtn_FX_Spray", 260, MaterialLibrary.Particle("MtnSpray", ProceduralTextures.Soft, Color.white, 0.7f));
            var sm = spray.main;
            sm.gravityModifier = -0.02f;
            FadeIO(spray, 0.12f, 0.6f);
            var ss = spray.sizeOverLifetime;
            ss.enabled = true;
            ss.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.4f));
            smoke = MakePS("Mtn_FX_Incense", 160, MaterialLibrary.Particle("MtnIncense", ProceduralTextures.Soft, Color.white, 0.75f));
            FadeIO(smoke, 0.15f, 0.6f);
            var zs = smoke.sizeOverLifetime;
            zs.enabled = true;
            zs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));
            var nz = smoke.noise;
            nz.enabled = true; nz.strength = 0.35f; nz.frequency = 0.6f; nz.scrollSpeed = 0.3f; nz.quality = ParticleSystemNoiseQuality.Low;
            leaves = MakePS("Mtn_FX_MapleLeaves", 140, MaterialLibrary.Particle("MtnMapleLeaf", LeafTex(), Color.white, 0.7f));
            var lm = leaves.main;
            lm.gravityModifier = 0.03f;
            var lr = leaves.rotationOverLifetime;
            lr.enabled = true;
            lr.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);
            var ln = leaves.noise;
            ln.enabled = true; ln.strength = 0.6f; ln.frequency = 0.4f; ln.scrollSpeed = 0.25f; ln.damping = true; ln.quality = ParticleSystemNoiseQuality.Low;
            FadeIO(leaves, 0.06f, 0.85f);
            foreach (var ps in new[] { spray, smoke, leaves }) ps.Play();
            yield return null;

            // ---- audio: the roar of the valley falls, the monastery bell
            fallsSrc = Source3D("Mtn_FallsAudio", new Vector3(-30f, 4f, 612f), FallsClip(), 12f, 190f);
            fallsSrc.loop = true;
            fallsSrc.volume = 0f;
            fallsSrc.Play();
            if (bellPos != Vector3.zero) bellSrc = Source3D("Mtn_BellAudio", bellPos, PropFX.TempleBell(), 20f, 520f);
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
            main.startSize = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
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

        static void FadeIO(ParticleSystem ps, float inT, float outT)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inT), new GradientAlphaKey(0.8f, outT), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        AudioSource Source3D(string name, Vector3 pos, AudioClip clip, float minD, float maxD)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Root, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minD;
            s.maxDistance = maxD;
            s.priority = 110;
            return s;
        }

        /// <summary>Seamless 4 s loop of filtered noise: the deep rush of falling water.</summary>
        static AudioClip FallsClip()
        {
            const int rate = 22050, len = rate * 4;
            var b = new float[len];
            var r = new System.Random(77);
            float lo = 0f, mid = 0f;
            for (int i = 0; i < len; i++)
            {
                float w = (float)r.NextDouble() * 2f - 1f;
                lo += (w - lo) * 0.02f;
                mid += (w - mid) * 0.18f;
                float sw = 0.8f + 0.2f * Mathf.Sin(i * 2f * Mathf.PI / len * 3f);
                b[i] = (lo * 2.6f + mid * 0.55f + w * 0.05f) * 0.55f * sw;
            }
            // crossfade the ends so the loop has no click
            const int fade = rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                b[i] = Mathf.Lerp(b[len - fade + i], b[i], t);
            }
            var clip = AudioClip.Create("Mtn_Falls", len - fade, 1, rate, false);
            var outB = new float[len - fade];
            System.Array.Copy(b, outB, len - fade);
            clip.SetData(outB, 0);
            return clip;
        }

        static Texture2D leafTex;
        static Texture2D LeafTex()
        {
            if (leafTex != null) return leafTex;
            const int s = 32;
            leafTex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "BR_MtnMapleLeaf", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                    float lobe = 0.55f + 0.38f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f + 0.3f)), 1.6f);
                    float alpha = Mathf.Clamp01((lobe - r) * 10f);
                    float vein = 1f - 0.25f * Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(a * 2.5f + 0.3f)) * 6f) * (r < lobe ? 1f : 0f);
                    leafTex.SetPixel(x, y, new Color(vein, vein, vein, alpha));
                }
            leafTex.Apply(true);
            return leafTex;
        }

        // ------------------------------------------------------------------ per frame
        public override void Tick(float dt, Vector3 eye, float presence)
        {
            lifeT += dt;
            // cloud sea drifts slowly and follows the light
            if (cloudLayers != null)
            {
                for (int L = 0; L < cloudLayers.Length; L++)
                {
                    if (cloudLayers[L] == null) continue;
                    float t = lifeT * (0.018f + 0.007f * L) + L * 2.1f;
                    cloudLayers[L].transform.localPosition = cloudBase[L] + new Vector3(Mathf.Sin(t) * 5.5f, Mathf.Sin(t * 0.7f) * 0.4f, Mathf.Cos(t * 0.83f) * 5.5f);
                }
            }
            matT -= dt;
            if (matT <= 0f)
            {
                matT = 0.5f;
                float night = Atmosphere.NightFactor;
                if (cloudMat != null)
                {
                    float mist = Mathf.Clamp01(0.85f + 0.3f * Atmosphere.CloudCover + 0.25f * Atmosphere.RainIntensity);
                    Color c = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.42f, 0.48f, 0.62f), night);
                    c.a = mist;
                    cloudMat.SetColor("_Tint", c);
                }
                if (fallsSrc != null)
                {
                    float d = Vector3.Distance(eye, fallsSrc.transform.position);
                    fallsSrc.volume = d < 220f ? 0.55f * GameSettings.SfxVolume : 0f;
                }
            }
            if (life != null && life.Mesh != null) PoseLife(dt);
            // effects near the camera only
            fxT += dt;
            if (fxT >= 0.1f && spray != null)
            {
                float step = fxT;
                fxT = 0f;
                EmitSpray(eye, step);
                EmitIncense(eye, step);
            }
            leafT += dt;
            if (leafT >= 0.3f && leaves != null)
            {
                leafT = 0f;
                EmitLeaves(eye);
            }
            UpdateBell(dt);
        }

        void PoseLife(float dt)
        {
            for (int i = 0; i < eagleOrbit.Length; i++)
            {
                var o = eagleOrbit[i];
                eagleAng[i] += eagleSpeed[i] * dt;
                float a = eagleAng[i];
                float t = lifeT + eaglePhase[i];
                Vector3 p = new Vector3(o.x + Mathf.Cos(a) * o.z, o.w + 4f * Mathf.Sin(a * 2f + eaglePhase[i]), o.y + Mathf.Sin(a) * o.z * 0.8f);
                float sgn = eagleSpeed[i] > 0f ? 1f : -1f;
                Vector3 tan = new Vector3(-Mathf.Sin(a) * o.z, 0f, Mathf.Cos(a) * o.z * 0.8f) * sgn;
                Quaternion q = Quaternion.LookRotation(tan.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 18f * sgn);
                Matrix4x4 body = Matrix4x4.TRS(p, q, Vector3.one * 1.7f);
                float cyc = Mathf.Repeat(t, 14f);
                float flap = cyc < 2.4f ? 32f * Mathf.Sin(cyc * 7.5f) : 4f * Mathf.Sin(t * 1.3f) + 6f;
                life.Set(eagleBody[i], body);
                life.Set(eagleL[i], body * Matrix4x4.TRS(new Vector3(-0.1f, 0.05f, 0f), Quaternion.Euler(0f, 0f, -flap), Vector3.one));
                life.Set(eagleR[i], body * Matrix4x4.TRS(new Vector3(0.1f, 0.05f, 0f), Quaternion.Euler(0f, 0f, flap), Vector3.one));
            }
            if (cabinPart != null && cableA != null)
            {
                for (int i = 0; i < cabinPart.Length; i++)
                {
                    cabinS[i] = Mathf.Repeat(cabinS[i] + dt * 0.016f, 2f);
                    float s = cabinS[i];
                    bool up = s < 1f;
                    float u = up ? s : 2f - s;
                    // pause briefly in the stations
                    u = Mathf.Clamp01((u - 0.02f) / 0.96f);
                    Vector3 dir;
                    Vector3 p = CableAt(up ? cableA : cableB, u, out dir);
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                    float swing = 2.5f * Mathf.Sin(lifeT * 0.9f + i * 1.7f);
                    life.Set(cabinPart[i], Matrix4x4.TRS(p, Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(swing, 0f, 0f), Vector3.one));
                }
            }
            life.Upload();
        }

        void EmitSpray(Vector3 eye, float step)
        {
            if (sprayA == null) return;
            for (int i = 0; i < sprayA.Length; i++)
            {
                Vector3 p = sprayA[i];
                float d2 = (p - eye).sqrMagnitude;
                if (d2 > 200f * 200f) continue;
                bool big = i < 2;
                int n = Mathf.Max(1, Mathf.RoundToInt((big ? 26f : 4f) * step));
                if (spray.particleCount > spray.main.maxParticles - n) return;
                for (int k = 0; k < n; k++)
                {
                    ep.position = p + new Vector3(FR(-2.5f, 2.5f), FR(0f, 1f), FR(-2.5f, 2.5f)) * (big ? 1.4f : 0.5f);
                    ep.velocity = new Vector3(FR(-0.9f, 0.9f), FR(0.4f, 1.6f), FR(-0.9f, 0.9f)) * (big ? 1.2f : 0.6f);
                    ep.startSize = big ? FR(2.5f, 5.5f) : FR(0.8f, 1.8f);
                    ep.startLifetime = FR(2.2f, 3.8f);
                    ep.startColor = new Color(1f, 1f, 1f, big ? FR(0.18f, 0.32f) : FR(0.12f, 0.22f));
                    spray.Emit(ep, 1);
                }
            }
        }

        void EmitIncense(Vector3 eye, float step)
        {
            if (incA == null) return;
            for (int i = 0; i < incA.Length; i++)
            {
                Vector3 p = incA[i];
                if ((p - eye).sqrMagnitude > 220f * 220f) continue;
                if (smoke.particleCount > smoke.main.maxParticles - 2) return;
                if (fxRng.NextDouble() > 2.6f * step) continue;
                ep.position = p + new Vector3(FR(-0.3f, 0.3f), 0f, FR(-0.3f, 0.3f));
                ep.velocity = new Vector3(FR(-0.1f, 0.1f), FR(0.5f, 0.8f), FR(-0.1f, 0.1f));
                ep.startSize = FR(0.9f, 1.4f);
                ep.startLifetime = FR(4.5f, 6.5f);
                ep.startColor = new Color(0.86f, 0.87f, 0.9f, 0.42f);
                smoke.Emit(ep, 1);
            }
        }

        void EmitLeaves(Vector3 eye)
        {
            if (mapleA == null || Atmosphere.RainIntensity > 0.6f) return;
            int emitted = 0;
            for (int i = 0; i < mapleA.Length && emitted < 4; i++)
            {
                Vector3 p = mapleA[i];
                if ((p - eye).sqrMagnitude > 70f * 70f) continue;
                if (fxRng.NextDouble() > 0.3) continue;
                if (leaves.particleCount > leaves.main.maxParticles - 2) return;
                ep.position = p + new Vector3(FR(-1.6f, 1.6f), FR(-0.5f, 1f), FR(-1.6f, 1.6f));
                ep.velocity = new Vector3(FR(-0.3f, 0.3f), FR(-0.6f, -0.3f), FR(-0.3f, 0.3f));
                ep.startSize = FR(0.16f, 0.28f);
                ep.startLifetime = FR(6f, 9f);
                ep.rotation = FR(0f, 360f);
                float k = (float)fxRng.NextDouble();
                ep.startColor = k < 0.45f ? new Color(0.86f, 0.22f, 0.14f, 1f) : k < 0.8f ? new Color(0.95f, 0.5f, 0.16f, 1f) : new Color(0.98f, 0.76f, 0.24f, 1f);
                leaves.Emit(ep, 1);
                ep.rotation = 0f;
                emitted++;
            }
        }

        void UpdateBell(float dt)
        {
            bellCool -= dt;
            if (bellSrc == null) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || gm.Car == null) return;
            Vector3 car = gm.Car.transform.position;
            float d = Vector2.Distance(new Vector2(car.x, car.z), MountainsMap.Monastery);
            if (d > 150f) bellArmed = true;
            if (bellArmed && d < 48f && bellCool <= 0f)
            {
                bellArmed = false;
                bellCool = 20f;
                bellSrc.volume = 0.9f * GameSettings.SfxVolume;
                bellSrc.Play();
            }
        }
    }
}

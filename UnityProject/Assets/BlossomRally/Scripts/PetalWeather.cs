// PetalWeather.cs - cherry blossom petals sprinkling from the sky around the camera, plus soft
// drifting mist. Both emitters follow the camera so the effect is everywhere you drive.
using UnityEngine;

namespace BlossomRally
{
    public class PetalWeather : MonoBehaviour
    {
        public Transform follow;
        public int level = 1;          // 0 calm, 1 spring, 2 blossom storm
        ParticleSystem petals, mist;
        static readonly float[] Rates = { 90f, 320f, 900f };
        float mistRate = -1f;

        void Start()
        {
            petals = MakePetals();
            mist = MakeMist();
            ApplyLevel();
        }

        public void CycleLevel()
        {
            level = (level + 1) % 3;
            ApplyLevel();
        }

        public void SetLevel(int l)
        {
            level = (l % 3 + 3) % 3;
            if (petals != null) ApplyLevel();
        }

        void ApplyLevel()
        {
            var em = petals.emission;
            em.rateOverTime = Rates[level];
            var main = petals.main;
            main.maxParticles = level == 2 ? 9000 : 4000;
        }

        void LateUpdate()
        {
            if (follow == null) return;
            Vector3 f = follow.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            f.Normalize();
            petals.transform.position = follow.position + f * 22f + Vector3.up * 16f;
            mist.transform.position = follow.position + f * 60f + Vector3.up * 3f;
            // mist amount follows the weather preset (none when fog is off)
            if (Mathf.Abs(Atmosphere.MistRate - mistRate) > 0.05f)
            {
                mistRate = Atmosphere.MistRate;
                var em = mist.emission;
                em.rateOverTime = mistRate;
                if (mistRate < 0.1f) mist.Clear();
            }
            var mainM = mist.main;
            mainM.startColor = new ParticleSystem.MinMaxGradient(
                new Color(RenderSettings.fogColor.r, RenderSettings.fogColor.g * 1.02f, RenderSettings.fogColor.b * 1.03f, 0.10f),
                new Color(0.95f, 0.9f, 1f, 0.06f));
        }

        ParticleSystem MakePetals()
        {
            var go = new GameObject("Petals");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.88f), new Color(1f, 0.95f, 0.97f));
            main.gravityModifier = 0.035f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.prewarm = true;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(90f, 4f, 90f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.25f);
            vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.7f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.4f;
            noise.octaveCount = 2;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
            rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = QuadMesh();
            r.alignment = ParticleSystemRenderSpace.World;
            r.sharedMaterial = MaterialLibrary.Particle("Petal", ProceduralTextures.Petal, Color.white, 0.55f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeMist()
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 20f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(28f, 48f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.95f, 0.10f), new Color(0.95f, 0.9f, 1f, 0.06f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(260f, 5f, 180f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = MaterialLibrary.Particle("Mist", ProceduralTextures.Soft, Color.white, 0.2f);
            r.maxParticleSize = 4f;
            r.minParticleSize = 0f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static Mesh quad;

        public static Mesh QuadMesh()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "PetalQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }
}

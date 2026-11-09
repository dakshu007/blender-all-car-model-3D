// DappledLight.cs - soft sun flecks and leaf mottling under the blossom trees, by day only.
// One merged decal mesh: an ellipse per cherry tree, laid on the ground (terrain / road surface) where
// the canopy's shadow falls for the current sun direction (stretched for a low sun). Rebuilt over a few
// frames when the sun moves (time of day changes), invisible at night, in rain and under heavy cloud.
// BR_Dapple blends 2x-multiply: flecks brighten the real tree shadow, mottles darken it slightly.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class DappledLight : MonoBehaviour
    {
        public WorldBuilder world;

        struct Tree { public Vector3 pos; public float radius, height, seed; }

        readonly List<Tree> trees = new List<Tree>();
        Material mat;
        MeshRenderer mr;
        Mesh mesh;
        Vector3 builtSun;
        bool building, enabledQ = true;
        float checkTimer;
        const int G = 4;

        static readonly int IdStrength = Shader.PropertyToID("_BR_DappleStrength");

        public IEnumerator Build()
        {
            var sh = Shader.Find("BlossomRally/Dapple");
            if (sh == null) yield break;
            mat = new Material(sh) { name = "BR_Dapple" };
            mat.SetTexture("_NoiseTex", GfxTextures.Noise);
            var rng = new System.Random(5);
            foreach (var rootName in new[] { "World_Static", "World_Grass" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform t in root.transform)
                {
                    string n = t.name;
                    if (!n.StartsWith("ENV_Cherry")) continue;
                    Vector3 s = t.lossyScale;
                    if (s.y < 0.6f) continue;
                    float r = n == "ENV_CherryKing" ? 6.6f : (n == "ENV_CherryTree" ? 4.8f : 2.9f);
                    float h = n == "ENV_CherryKing" ? 6.2f : (n == "ENV_CherryTree" ? 4.3f : 3.0f);
                    trees.Add(new Tree { pos = t.position, radius = r * s.x, height = h * s.y, seed = (float)rng.NextDouble() });
                }
            }
            if (trees.Count == 0) yield break;
            mesh = new Mesh { name = "BR_Dapple", indexFormat = trees.Count * G * G > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var go = new GameObject("BR_DappledLight");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            yield return Rebuild();
        }

        static Vector3 SunDir()
        {
            return Atmosphere.Sun != null ? -Atmosphere.Sun.transform.forward : Vector3.up;
        }

        IEnumerator Rebuild()
        {
            building = true;
            Vector3 sun = SunDir();
            float sy = Mathf.Max(0.17f, sun.y);
            Vector2 away = new Vector2(-sun.x, -sun.z);
            if (away.sqrMagnitude < 1e-4f) away = new Vector2(0f, 1f);
            away.Normalize();
            Vector2 across = new Vector2(-away.y, away.x);
            float stretch = Mathf.Min(2.4f, 1f / sy);
            var verts = new List<Vector3>(trees.Count * G * G);
            var uv0 = new List<Vector2>(trees.Count * G * G);
            var uv1 = new List<Vector2>(trees.Count * G * G);
            var tris = new List<int>(trees.Count * (G - 1) * (G - 1) * 6);
            for (int k = 0; k < trees.Count; k++)
            {
                var t = trees[k];
                // canopy shadow centre on the ground, shifted away from the sun
                float shift = t.height / Mathf.Tan(Mathf.Asin(sy));
                Vector2 c = new Vector2(t.pos.x, t.pos.z) + away * Mathf.Min(shift, t.radius * 2.2f);
                float ra = t.radius * stretch * 1.05f, rc = t.radius * 1.05f;
                int b = verts.Count;
                for (int j = 0; j < G; j++)
                    for (int i = 0; i < G; i++)
                    {
                        float u = i / (float)(G - 1) * 2f - 1f, v = j / (float)(G - 1) * 2f - 1f;
                        Vector2 p = c + away * (v * ra) + across * (u * rc);
                        float y = world != null ? GraphicsHub.GroundY(world, p.x, p.y) : t.pos.y;
                        verts.Add(new Vector3(p.x, y + 0.06f, p.y));
                        uv0.Add(new Vector2(u, v));
                        uv1.Add(new Vector2(t.seed, 1f));
                    }
                for (int j = 0; j < G - 1; j++)
                    for (int i = 0; i < G - 1; i++)
                    {
                        int a = b + j * G + i, cc = a + G;
                        tris.Add(a); tris.Add(cc); tris.Add(a + 1);
                        tris.Add(a + 1); tris.Add(cc); tris.Add(cc + 1);
                    }
                if (k % 60 == 59) yield return null;
            }
            if (mesh != null)
            {
                mesh.Clear();
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uv0);
                mesh.SetUVs(1, uv1);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
            }
            builtSun = sun;
            building = false;
        }

        public void ApplyQuality()
        {
            enabledQ = GraphicsFX.Q >= 1;
            if (mat != null) mat.SetFloat("_FadeDist", GraphicsFX.Q >= 3 ? 120f : (GraphicsFX.Q == 2 ? 90f : 65f));
        }

        void Update()
        {
            if (mr == null) return;
            Vector3 sun = SunDir();
            float elev = Mathf.Asin(Mathf.Clamp(sun.y, -1f, 1f)) * Mathf.Rad2Deg;
            float night = Atmosphere.NightFactor;
            float strength = (1f - night) * (1f - night) * (1f - Atmosphere.CloudCover * 0.8f) * (1f - Atmosphere.RainIntensity)
                             * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 16f, elev)) * (enabledQ ? 1f : 0f);
            Shader.SetGlobalFloat(IdStrength, strength);
            mr.enabled = strength > 0.02f;
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.5f;
                if (!building && strength > 0.02f && Vector3.Angle(sun, builtSun) > 2f) StartCoroutine(Rebuild());
            }
        }
    }
}

// DistantScenery.cs - the far horizon in the classic Chinese landscape (shan shui) style:
//  * five rings of mountain silhouettes beyond the terrain edge (~0.76 .. 1.9 km), rounded karst peaks,
//    blue-grey ink fading towards the sky with distance, mist pooling at their feet
//  * a soft cloud band at ~110-240 m that the tall far peaks poke through
//  * drifting cumulus over the valley, seen from below, moving with the wind (Atmosphere.Gust)
// One draw call per ring / band / cloud layer; colours follow Atmosphere every frame (few SetColor calls).
// Fog never erases the mountains completely: the shader uses its own capped haze, not scene fog.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class DistantScenery : MonoBehaviour
    {
        struct RingDef
        {
            public float radius, superP, hMin, hMax;
            public int peaks, seed;
            public RingDef(float r, float p, float a, float b, int n, int s) { radius = r; superP = p; hMin = a; hMax = b; peaks = n; seed = s; }
        }

        static readonly RingDef[] Rings =
        {
            new RingDef(760f, 4.0f, 46f, 112f, 38, 3),
            new RingDef(935f, 3.4f, 70f, 175f, 34, 7),
            new RingDef(1160f, 3.0f, 95f, 250f, 30, 13),
            new RingDef(1460f, 2.6f, 140f, 340f, 26, 19),
            new RingDef(1830f, 2.3f, 210f, 455f, 22, 29),
        };

        const int Segments = 384;
        readonly List<MeshRenderer> ringRenderers = new List<MeshRenderer>();
        MeshRenderer bandRenderer, puffRenderer;
        Material mountainMat, bandMat, puffMat;
        Vector2 drift;
        float colourTimer;
        int puffCount;
        const int MaxPuffClouds = 56;

        static float SuperRadius(float r, float p, float ang)
        {
            float c = Mathf.Abs(Mathf.Cos(ang)), s = Mathf.Abs(Mathf.Sin(ang));
            return r / Mathf.Pow(Mathf.Pow(c, p) + Mathf.Pow(s, p), 1f / p);
        }

        public IEnumerator Build()
        {
            var msh = Shader.Find("BlossomRally/DistantMountains");
            var bsh = Shader.Find("BlossomRally/CloudBand");
            var psh = Shader.Find("BlossomRally/CloudPuffs");
            if (msh != null) mountainMat = new Material(msh) { name = "BR_DistantMountains" };
            if (bsh != null) bandMat = new Material(bsh) { name = "BR_CloudBand" };
            if (psh != null) { puffMat = new Material(psh) { name = "BR_CloudPuffs" }; puffMat.SetTexture("_MainTex", GfxTextures.Puff); }

            if (mountainMat != null)
                for (int k = 0; k < Rings.Length; k++)
                {
                    ringRenderers.Add(MakeRing(k));
                    yield return null;
                }
            if (bandMat != null) bandRenderer = MakeBand();
            yield return null;
            if (puffMat != null) puffRenderer = MakePuffs();
            ApplyQuality();
            UpdateColours();
        }

        MeshRenderer MakeRing(int k)
        {
            var def = Rings[k];
            var rng = new System.Random(def.seed * 7919 + 17);
            // peaks: rounded karst humps of different widths and heights
            int n = def.peaks;
            var pAng = new float[n];
            var pH = new float[n];
            var pW = new float[n];
            for (int i = 0; i < n; i++)
            {
                pAng[i] = (i + (float)rng.NextDouble() * 0.8f) / n * Mathf.PI * 2f;
                float r = (float)rng.NextDouble();
                pH[i] = Mathf.Lerp(def.hMin * 0.55f, def.hMax, r * r * 0.6f + (float)rng.NextDouble() * 0.4f);
                pW[i] = Mathf.Lerp(0.035f, 0.11f, (float)rng.NextDouble()) * (760f / def.radius) * 1.4f;
            }
            float layer = k / (float)(Rings.Length - 1);
            float phase = (float)rng.NextDouble() * 10f;
            var verts = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[Segments * 6];
            for (int s = 0; s <= Segments; s++)
            {
                float ang = s / (float)Segments * Mathf.PI * 2f;
                float h = def.hMin * 0.45f + def.hMin * 0.12f * Mathf.Sin(ang * 7f + phase) + def.hMin * 0.08f * Mathf.Sin(ang * 19f + phase * 2f);
                for (int i = 0; i < n; i++)
                {
                    float d = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, pAng[i] * Mathf.Rad2Deg) * Mathf.Deg2Rad / pW[i];
                    // karst: steep sides, rounded top
                    float bump = Mathf.Exp(-d * d * d * d * 0.5f) * 0.75f + Mathf.Exp(-d * d * 0.6f) * 0.25f;
                    h = Mathf.Max(h, pH[i] * bump + def.hMin * 0.2f);
                }
                float rad = SuperRadius(def.radius, def.superP, ang) * (1f + 0.03f * Mathf.Sin(ang * 5f + phase));
                float x = Mathf.Cos(ang) * rad, z = Mathf.Sin(ang) * rad;
                verts[s * 2] = new Vector3(x, -30f, z);
                verts[s * 2 + 1] = new Vector3(x, h, z);
                uvs[s * 2] = new Vector2(layer, 0f);
                uvs[s * 2 + 1] = new Vector2(layer, 1f);
                if (s < Segments)
                {
                    int b = s * 2, t = s * 6;
                    tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                    tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
                }
            }
            var mesh = new Mesh { name = "BR_MountainRing_" + k };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("BR_MountainRing_" + k);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mountainMat;
            // "On" only so the rings reach the camera depth texture (sky masks); the shader drops them
            // from directional shadow maps, so they never shade the valley
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        MeshRenderer MakeBand()
        {
            const int seg = 160;
            float[] ys = { 105f, 165f, 245f };
            var verts = new Vector3[(seg + 1) * ys.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new List<int>(seg * 12);
            for (int s = 0; s <= seg; s++)
            {
                float ang = s / (float)seg * Mathf.PI * 2f;
                float rad = SuperRadius(1330f, 2.6f, ang);
                for (int r = 0; r < ys.Length; r++)
                {
                    float y = ys[r] + 18f * Mathf.Sin(ang * 3f + r) * (r == 1 ? 1f : 0.5f);
                    verts[s * ys.Length + r] = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                    uvs[s * ys.Length + r] = new Vector2(s / (float)seg * 6f, r / (float)(ys.Length - 1));
                }
                if (s < seg)
                    for (int r = 0; r < ys.Length - 1; r++)
                    {
                        int a = s * ys.Length + r, b = a + ys.Length;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
            }
            var mesh = new Mesh { name = "BR_CloudBand" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("BR_CloudBand");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bandMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        MeshRenderer MakePuffs()
        {
            var rng = new System.Random(4242);
            var verts = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var tris = new List<int>();
            for (int c = 0; c < MaxPuffClouds; c++)
            {
                Vector3 centre = new Vector3(((float)rng.NextDouble() * 2f - 1f) * 1100f, 175f + (float)rng.NextDouble() * 90f, ((float)rng.NextDouble() * 2f - 1f) * 1100f);
                float size = 55f + (float)rng.NextDouble() * 70f;
                int puffs = 3 + rng.Next(3);
                float idx = (c + 0.5f) / MaxPuffClouds;
                for (int p = 0; p < puffs; p++)
                {
                    Vector3 o = new Vector3(((float)rng.NextDouble() * 2f - 1f) * size * 0.7f, ((float)rng.NextDouble()) * size * 0.12f, ((float)rng.NextDouble() * 2f - 1f) * size * 0.35f);
                    float ps = size * (p == 0 ? 0.75f : Mathf.Lerp(0.4f, 0.65f, (float)rng.NextDouble()));
                    int b = verts.Count;
                    for (int v = 0; v < 4; v++)
                    {
                        verts.Add(centre + o);
                        uv0.Add(new Vector2(v == 1 || v == 2 ? 1f : 0f, v >= 2 ? 1f : 0f));
                        uv1.Add(new Vector4(ps * 1.5f, ps, (float)rng.NextDouble(), idx));
                    }
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }
            }
            var mesh = new Mesh { name = "BR_CloudPuffs" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(tris, 0);
            // positions wrap and billboard in the shader: never cull
            mesh.bounds = new Bounds(new Vector3(0f, 200f, 0f), new Vector3(5000f, 800f, 5000f));
            var go = new GameObject("BR_CloudPuffs");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = puffMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        public void ApplyQuality()
        {
            int q = GraphicsFX.Q;
            int rings = q < 0 ? 0 : (q == 0 ? 3 : (q == 1 ? 4 : 5));
            for (int i = 0; i < ringRenderers.Count; i++) if (ringRenderers[i] != null) ringRenderers[i].enabled = i < rings;
            if (bandRenderer != null) bandRenderer.enabled = q >= 0;
            puffCount = q < 1 ? 0 : (q == 1 ? 20 : (q == 2 ? 36 : MaxPuffClouds));
            if (puffRenderer != null) puffRenderer.enabled = puffCount > 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // wind drift (integrated so gusts speed the clouds up without jumps)
            drift += new Vector2(1f, 0.35f) * ((3.5f + Atmosphere.Gust * 6f) * dt);
            if (drift.x > 2200f) drift.x -= 2200f;
            if (drift.y > 2200f) drift.y -= 2200f;
            colourTimer -= Time.unscaledDeltaTime;
            if (colourTimer <= 0f) { colourTimer = 0.1f; UpdateColours(); }
            if (bandMat != null) bandMat.SetVector("_Drift", new Vector4(drift.x / 2200f * 1.5f, drift.y / 2200f, 0f, 0f));
            if (puffMat != null) puffMat.SetVector("_Drift", new Vector4(drift.x, 0f, drift.y, 0f));
        }

        void UpdateColours()
        {
            var sky = MaterialLibrary.Sky;
            Color hor = sky != null ? sky.GetColor("_Horizon") : new Color(1f, 0.87f, 0.86f);
            Color top = sky != null ? sky.GetColor("_Top") : new Color(0.45f, 0.64f, 0.9f);
            Color cLit = sky != null ? sky.GetColor("_CloudColor") : Color.white;
            Color cShade = sky != null ? sky.GetColor("_CloudShade") : new Color(0.74f, 0.72f, 0.8f);
            Color fog = RenderSettings.fogColor;
            float night = Atmosphere.NightFactor;
            float light = Mathf.Clamp(RenderSettings.ambientSkyColor.grayscale / 0.72f, 0.12f, 1.25f);
            Color near = Color.Lerp(new Color(0.40f, 0.48f, 0.60f), top, 0.25f) * light;
            near = Color.Lerp(near, hor, 0.15f);
            near.a = 1f;
            Color far = Color.Lerp(near, hor, 0.55f);
            Color mist = Color.Lerp(fog, Color.white, 0.25f * (1f - night));
            float d = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            float x = d * 250f;
            float haze0 = 1f - Mathf.Exp(-x * x) + 0.06f;
            float hazeMax = Mathf.Clamp(0.62f + d * 45f, 0.62f, 0.96f);
            Vector3 sd = Atmosphere.Sun != null ? -Atmosphere.Sun.transform.forward : Vector3.up;
            if (mountainMat != null)
            {
                mountainMat.SetColor("_NearCol", near);
                mountainMat.SetColor("_FarCol", far);
                mountainMat.SetColor("_MistCol", mist);
                mountainMat.SetColor("_Horizon", hor);
                mountainMat.SetVector("_Haze", new Vector4(haze0, 0.11f, hazeMax, 0.55f + 0.3f * Atmosphere.Wetness));
                mountainMat.SetVector("_SunDir", new Vector4(sd.x, sd.y, sd.z, (1f - night) * (1f - Atmosphere.CloudCover * 0.7f)));
                mountainMat.SetColor("_SunCol", Atmosphere.Sun != null ? Atmosphere.Sun.color : Color.white);
            }
            float cover = Atmosphere.CloudCover;
            if (bandMat != null)
            {
                bandMat.SetColor("_Lit", Color.Lerp(cLit, hor, 0.3f));
                bandMat.SetColor("_Shade", Color.Lerp(cShade, mist, 0.4f));
                bandMat.SetColor("_Horizon", hor);
                bandMat.SetVector("_Params", new Vector4(0.55f + 0.4f * cover, 0.62f + 0.3f * cover, Mathf.Min(0.6f, haze0), 0f));
            }
            if (puffMat != null)
            {
                puffMat.SetColor("_Lit", cLit);
                puffMat.SetColor("_Shade", cShade);
                puffMat.SetColor("_Horizon", hor);
                float count = puffCount * Mathf.Clamp01(0.3f + 0.7f * cover) * (1f - Atmosphere.RainIntensity * 0.4f);
                puffMat.SetVector("_Params", new Vector4(count / MaxPuffClouds, 0.85f, 0f, 0f));
            }
        }
    }
}

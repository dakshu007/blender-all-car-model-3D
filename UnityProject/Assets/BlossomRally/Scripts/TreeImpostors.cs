// TreeImpostors.cs - performance: each tree type is rendered once into a texture at load time, and
// every tree gets a LODGroup: full mesh up close, two crossed textured quads (an "impostor") far away.
// Far trees then cost 4 triangles instead of thousands, which matters a lot on laptop GPUs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class TreeImpostors
    {
        const int CaptureLayer = 31;
        const int Res = 256;

        class Impostor { public Mesh mesh; public Material mat; }

        public static IEnumerator Build(List<KeyValuePair<string, GameObject>> trees)
        {
            var shader = Shader.Find("BlossomRally/Impostor");
            if (shader == null || trees.Count == 0) yield break;
            var byRes = new Dictionary<string, Impostor>();
            foreach (var kv in trees)
                if (kv.Value != null && !byRes.ContainsKey(kv.Key))
                {
                    var imp = Capture(kv.Value, shader, kv.Key.Contains("Bamboo"));
                    byRes[kv.Key] = imp;
                    yield return null;
                }
            int n = 0;
            foreach (var kv in trees)
            {
                Impostor imp;
                if (kv.Value == null || !byRes.TryGetValue(kv.Key, out imp) || imp == null) continue;
                var go = kv.Value;
                var full = go.GetComponentsInChildren<Renderer>();
                if (full.Length == 0) continue;
                var q = new GameObject("Impostor");
                q.transform.SetParent(go.transform, false);
                q.layer = WorldBuilder.LayerTrees;
                q.AddComponent<MeshFilter>().sharedMesh = imp.mesh;
                var mr = q.AddComponent<MeshRenderer>();
                mr.sharedMaterial = imp.mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.075f, full), new LOD(0.006f, new Renderer[] { mr }) });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
                if (++n % 200 == 0) yield return null;
            }
            Debug.Log("[BlossomRally] tree impostors: " + byRes.Count + " types, " + n + " trees");
        }

        static void SetLayer(GameObject g, int layer)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static Impostor Capture(GameObject sample, Shader shader, bool bamboo)
        {
            var clone = Object.Instantiate(sample);
            clone.transform.SetPositionAndRotation(new Vector3(0f, -2000f, 0f), Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            foreach (var c in clone.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            SetLayer(clone, CaptureLayer);
            var rs = clone.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.Destroy(clone); return null; }
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 centreLocal = b.center - clone.transform.position;
            float half = Mathf.Max(b.extents.y, Mathf.Max(b.extents.x, b.extents.z)) * 1.04f;

            // look along the sunlight so the captured side is the lit one
            Vector3 look = Atmosphere.Sun != null ? Atmosphere.Sun.transform.forward : Vector3.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
            look.Normalize();

            var camGo = new GameObject("ImpostorCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.cullingMask = 1 << CaptureLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bamboo ? new Color(0.42f, 0.56f, 0.28f, 0f) : new Color(0.96f, 0.78f, 0.84f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.transform.SetPositionAndRotation(b.center - look * 80f, Quaternion.LookRotation(look, Vector3.up));
            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, true) { name = "Impostor_" + sample.name, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0, false);
            RenderTexture.active = prev;
            Dilate(tex);
            tex.Apply(true);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(camGo);
            Object.Destroy(clone);

            var mat = new Material(shader) { name = "BR_Impostor_" + sample.name };
            mat.SetTexture("_MainTex", tex);
            // two crossed quads covering the captured square, centred like the real tree
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int k = 0; k < 2; k++)
            {
                Vector3 right = k == 0 ? Vector3.right : Vector3.forward;
                int i0 = v.Count;
                Vector3 c = centreLocal;
                v.Add(c - right * half - Vector3.up * half); v.Add(c + right * half - Vector3.up * half);
                v.Add(c + right * half + Vector3.up * half); v.Add(c - right * half + Vector3.up * half);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var mesh = new Mesh { name = "ImpostorQuads" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            var nrm = new List<Vector3>();
            for (int i = 0; i < v.Count; i++) nrm.Add(Vector3.up);
            mesh.SetNormals(nrm);
            mesh.RecalculateBounds();
            return new Impostor { mesh = mesh, mat = mat };
        }

        /// <summary>Bleed edge colours into transparent pixels so mip-mapped edges don't turn dark.</summary>
        static void Dilate(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int n = tex.width;
            var tmp = new Color32[px.Length];
            for (int pass = 0; pass < 4; pass++)
            {
                System.Array.Copy(px, tmp, px.Length);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int i = y * n + x;
                        if (px[i].a > 8) continue;
                        int r = 0, g = 0, b = 0, c = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                                var q = px[yy * n + xx];
                                if (q.a <= 8) continue;
                                r += q.r; g += q.g; b += q.b; c++;
                            }
                        if (c > 0) tmp[i] = new Color32((byte)(r / c), (byte)(g / c), (byte)(b / c), 0);
                    }
                System.Array.Copy(tmp, px, px.Length);
                for (int i = 0; i < px.Length; i++) if (px[i].a <= 8 && (px[i].r | px[i].g | px[i].b) != 0) px[i].a = 1;
            }
            for (int i = 0; i < px.Length; i++) if (px[i].a == 1) px[i].a = 0;
            tex.SetPixels32(px);
        }
    }
}

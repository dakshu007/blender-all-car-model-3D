// ShaderWarmup.cs - removes the "first time it appears" hitch. Everything in Blossom Rally is generated
// at runtime, so there is no ShaderVariantCollection asset to preload; and Shader.WarmupAllShaders would
// compile every variant of every loaded shader (slow) and is only partly effective on Metal / D3D12.
// Instead, at the end of loading, every distinct material in the scene (including the streamed regions'
// inactive content and particle systems) is drawn once into a tiny offscreen target by a camera that
// copies the main camera (same HDR target format, MSAA, fog, sun + shadows). That compiles exactly the
// shader variants and GPU pipeline states the game really uses, while the loading screen is still up.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public static class ShaderWarmup
    {
        static readonly HashSet<Material> warmed = new HashSet<Material>();
        const int MaxPerCall = 900;

        /// <summary>Draw every not-yet-warmed scene material once (returns how many).</summary>
        public static int Run()
        {
            var main = Camera.main;
            if (main == null) return 0;
            var mats = new List<Material>();
            try
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (r == null || !r.gameObject.scene.IsValid()) continue;
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var m = ms[i];
                        if (m == null || m.shader == null || !m.shader.isSupported || warmed.Contains(m)) continue;
                        warmed.Add(m);
                        mats.Add(m);
                        if (mats.Count >= MaxPerCall) break;
                    }
                    if (mats.Count >= MaxPerCall) break;
                }
            }
            catch (System.Exception e) { Debug.LogException(e); }
            if (mats.Count == 0) return 0;

            GameObject go = null, quads = null;
            Mesh warmMesh = null;
            RenderTexture rt = null;
            try
            {
                go = new GameObject("BR_ShaderWarmup") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.depthTextureMode = DepthTextureMode.None;
                cam.farClipPlane = 50f;
                cam.nearClipPlane = 0.1f;
                cam.cullingMask = 1 << 30;            // nothing of the scene: only our quads (drawn on layer 30)
                var fmt = cam.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                    ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                rt = new RenderTexture(64, 64, 24, fmt) { name = "BR_WarmupRT", antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
                rt.Create();
                cam.targetTexture = rt;
                Transform ct = go.transform;
                ct.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                // one tiny quad per material (one submesh each) on a single renderer in front of the camera
                var mesh = new Mesh { name = "BR_WarmupQuads", hideFlags = HideFlags.HideAndDontSave };
                int n = mats.Count;
                var v = new Vector3[n * 4];
                var uv = new Vector2[n * 4];
                var nr = new Vector3[n * 4];
                for (int i = 0; i < n; i++)
                {
                    float x = (i % 30) * 0.01f - 0.15f, y = (i / 30) * 0.01f - 0.15f;
                    v[i * 4] = new Vector3(x, y, 0f); v[i * 4 + 1] = new Vector3(x + 0.01f, y, 0f);
                    v[i * 4 + 2] = new Vector3(x + 0.01f, y + 0.01f, 0f); v[i * 4 + 3] = new Vector3(x, y + 0.01f, 0f);
                    uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1);
                    for (int k = 0; k < 4; k++) nr[i * 4 + k] = Vector3.back;
                }
                mesh.vertices = v;
                mesh.uv = uv;
                mesh.uv2 = uv;
                mesh.normals = nr;
                var col = new Color32[n * 4];
                for (int i = 0; i < col.Length; i++) col[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = col;
                mesh.subMeshCount = n;
                for (int i = 0; i < n; i++)
                {
                    int b0 = i * 4;
                    mesh.SetTriangles(new[] { b0, b0 + 2, b0 + 1, b0, b0 + 3, b0 + 2 }, i, false);
                }
                mesh.RecalculateBounds();
                quads = new GameObject("BR_WarmupQuads") { hideFlags = HideFlags.HideAndDontSave, layer = 30 };
                quads.transform.SetPositionAndRotation(ct.position + ct.forward * 1f, ct.rotation);
                quads.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = quads.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats.ToArray();
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                warmMesh = mesh;
                cam.Render();
            }
            catch (System.Exception e) { Debug.LogException(e); }
            finally
            {
                if (go != null) Object.Destroy(go);
                if (quads != null) Object.Destroy(quads);
                if (warmMesh != null) Object.Destroy(warmMesh);
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
            }
            return mats.Count;
        }
    }
}

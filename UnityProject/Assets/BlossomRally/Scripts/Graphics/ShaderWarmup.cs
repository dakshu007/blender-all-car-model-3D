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
        static Mesh quad;
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

            if (quad == null) quad = PetalWeather.QuadMesh();
            GameObject go = null;
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
                var props = new MaterialPropertyBlock();
                // a couple of rows of tiny quads in front of the camera (all in view, all drawn)
                Matrix4x4 baseM = Matrix4x4.TRS(ct.position + ct.forward * 3f, ct.rotation, Vector3.one * 0.05f);
                for (int i = 0; i < mats.Count; i++)
                {
                    var m = mats[i];
                    int passes = Mathf.Max(1, m.passCount);
                    for (int s = 0; s < Mathf.Min(passes, 1); s++)
                        Graphics.DrawMesh(quad, baseM, m, 30, cam, 0, props, ShadowCastingMode.On, true);
                }
                cam.Render();
            }
            catch (System.Exception e) { Debug.LogException(e); }
            finally
            {
                if (go != null) Object.Destroy(go);
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
            }
            return mats.Count;
        }
    }
}

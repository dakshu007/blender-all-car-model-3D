// PostFX.cs - built-in pipeline camera effect: soft bloom + filmic tonemap + dreamy pastel grade
// ("colourful with a slightly faded shade") + vignette. Toggle with F.
using UnityEngine;

namespace BlossomRally
{
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public bool effectEnabled = true;
        [Range(0f, 2f)] public float bloomIntensity = 0.55f;
        public float threshold = 0.95f;
        public float exposure = 1.08f;
        public float saturation = 1.08f;
        public float contrast = 1.04f;
        public Color lift = new Color(0.86f, 0.80f, 0.90f, 0.10f);       // pastel fade
        public Color tint = new Color(1.03f, 0.99f, 0.99f, 1f);          // gentle warmth
        public Color shadowTint = new Color(0.90f, 0.88f, 1.06f, 0.5f);  // lilac shadows
        public float vignette = 0.9f;

        Material mat;

        void OnEnable()
        {
            var sh = Shader.Find("Hidden/BlossomRally/PostFX");
            if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!effectEnabled || mat == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat.SetFloat("_Threshold", threshold);
            mat.SetFloat("_Knee", 0.5f);
            mat.SetFloat("_BloomIntensity", bloomIntensity);
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Contrast", contrast);
            mat.SetColor("_Lift", lift);
            mat.SetColor("_Tint", tint);
            mat.SetColor("_ShadowTint", shadowTint);
            mat.SetFloat("_Vignette", vignette);

            const int levels = 5;
            var rts = new RenderTexture[levels];
            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            rts[0] = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0, fmt);
            mat.SetFloat("_BlurOffset", 1f);
            Graphics.Blit(src, rts[0], mat, 0);
            int count = 1;
            for (int i = 1; i < levels; i++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                rts[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(rts[i - 1], rts[i], mat, 1);
                count++;
            }
            mat.SetFloat("_BlurOffset", 0.5f);
            for (int i = count - 2; i >= 0; i--)
            {
                var up = RenderTexture.GetTemporary(rts[i].width, rts[i].height, 0, fmt);
                Graphics.Blit(rts[i + 1], up, mat, 1);
                RenderTexture.ReleaseTemporary(rts[i]);
                rts[i] = up;
            }
            mat.SetTexture("_BloomTex", rts[0]);
            Graphics.Blit(src, dst, mat, 2);
            for (int i = 0; i < count; i++)
                if (rts[i] != null) RenderTexture.ReleaseTemporary(rts[i]);
        }
    }
}

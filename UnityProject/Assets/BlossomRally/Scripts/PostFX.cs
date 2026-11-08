// PostFX.cs - built-in pipeline camera effect: soft bloom + filmic tonemap + dreamy pastel grade
// ("colourful with a slightly faded shade") + vignette. Toggle with F.
// Photo mode adds: filters, exposure / saturation / vignette trims, depth of field (gather blur
// driven by the camera depth texture), letterbox bars and a "Blossom Rally" watermark.
// With the photo fields at their defaults the output is exactly the normal look.
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

        // ---- photo mode controls (defaults reproduce the normal look) ----
        public static readonly string[] FilterNames = { "Natural", "Warm Spring", "Film", "Ink", "Pastel Dream", "Night Blue" };
        [Header("Photo mode")]
        public int filter;                       // index into FilterNames
        public float exposureEV;                 // extra exposure in stops
        public float saturationMul = 1f;         // multiplies saturation
        public float vignetteAdd;                // added to vignette
        public bool dof;                         // depth of field
        public float focusDistance = 10f;        // metres (eye depth)
        public float aperture = 2.8f;            // f-stop: smaller = shallower focus
        [Range(0f, 0.45f)] public float letterbox;   // bar height per side, fraction of the screen
        public bool showLogo;
        public Texture logoTexture;
        public float logoOpacity = 0.92f;

        // per filter: x saturation, y contrast, z monochrome, w grain
        static readonly Vector4[] FParams =
        {
            new Vector4(1f, 1f, 0f, 0f),
            new Vector4(1.12f, 1.03f, 0f, 0f),
            new Vector4(0.86f, 0.93f, 0f, 0.045f),
            new Vector4(1f, 1.16f, 1f, 0.03f),
            new Vector4(0.80f, 0.86f, 0f, 0f),
            new Vector4(0.55f, 1.06f, 0f, 0.02f),
        };
        static readonly Color[] FTint =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1.07f, 1.0f, 0.90f, 1f),
            new Color(1.04f, 1.0f, 0.93f, 1f),
            new Color(1.0f, 0.95f, 0.85f, 1f),          // warm paper
            new Color(1.02f, 0.98f, 1.04f, 1f),
            new Color(0.56f, 0.70f, 1.0f, 1f),
        };
        static readonly Color[] FShadow =
        {
            new Color(1f, 1f, 1f, 0f),
            new Color(1.0f, 0.9f, 0.95f, 0.2f),
            new Color(0.84f, 1.0f, 1.02f, 0.35f),       // teal shadows
            new Color(1f, 1f, 1f, 0f),
            new Color(0.95f, 0.9f, 1.1f, 0.4f),
            new Color(0.7f, 0.8f, 1.2f, 0.5f),
        };
        static readonly Color[] FLift =
        {
            new Color(0f, 0f, 0f, 0f),
            new Color(1.0f, 0.85f, 0.80f, 0.05f),
            new Color(0.18f, 0.20f, 0.22f, 0.08f),      // faded blacks
            new Color(0.98f, 0.94f, 0.86f, 0.06f),
            new Color(0.98f, 0.86f, 0.96f, 0.22f),
            new Color(0.05f, 0.08f, 0.18f, 0.08f),
        };

        Material mat;
        Camera cam;
        bool addedDepth;

        /// <summary>Put every photo-mode control back to its default (normal look).</summary>
        public void ResetPhoto()
        {
            filter = 0;
            exposureEV = 0f;
            saturationMul = 1f;
            vignetteAdd = 0f;
            dof = false;
            focusDistance = 10f;
            aperture = 2.8f;
            letterbox = 0f;
            showLogo = false;
            UpdateDepthMode();
        }

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            var sh = Shader.Find("Hidden/BlossomRally/PostFX");
            if (sh != null && sh.isSupported) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
            if (addedDepth && cam != null) cam.depthTextureMode &= ~DepthTextureMode.Depth;
            addedDepth = false;
        }

        bool WantDof { get { return dof && effectEnabled && mat != null; } }

        // the depth texture is only rendered while depth of field is on
        void UpdateDepthMode()
        {
            if (cam == null) return;
            if (WantDof)
            {
                if (!addedDepth && (cam.depthTextureMode & DepthTextureMode.Depth) == 0)
                {
                    cam.depthTextureMode |= DepthTextureMode.Depth;
                    addedDepth = true;
                }
            }
            else if (addedDepth)
            {
                cam.depthTextureMode &= ~DepthTextureMode.Depth;
                addedDepth = false;
            }
        }

        void OnPreCull()
        {
            UpdateDepthMode();
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
            mat.SetFloat("_Exposure", exposure * Mathf.Pow(2f, exposureEV));
            mat.SetFloat("_Saturation", saturation * saturationMul);
            mat.SetFloat("_Contrast", contrast);
            mat.SetColor("_Lift", lift);
            mat.SetColor("_Tint", tint);
            mat.SetColor("_ShadowTint", shadowTint);
            mat.SetFloat("_Vignette", Mathf.Max(0f, vignette + vignetteAdd));
            SetupPhoto(src);

            // ---- depth of field: CoC + downsample, gather blur, combine (full res) ----
            RenderTexture source = src;
            RenderTexture dofOut = null;
            if (WantDof && cam != null && (cam.depthTextureMode & DepthTextureMode.Depth) != 0)
            {
                int hw = Mathf.Max(1, src.width / 2), hh = Mathf.Max(1, src.height / 2);
                var cocFmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                var a = RenderTexture.GetTemporary(hw, hh, 0, cocFmt);
                var b = RenderTexture.GetTemporary(hw, hh, 0, cocFmt);
                a.filterMode = FilterMode.Bilinear;
                b.filterMode = FilterMode.Bilinear;
                float strength = Mathf.Clamp(2.8f / Mathf.Max(0.5f, aperture), 0.05f, 3f) * 0.85f;
                float maxRadius = Mathf.Clamp(hh * 0.016f, 2f, 18f);
                mat.SetVector("_DofParams", new Vector4(Mathf.Max(0.1f, focusDistance), strength, maxRadius, 0f));
                Graphics.Blit(src, a, mat, 3);
                Graphics.Blit(a, b, mat, 4);
                mat.SetTexture("_DofTex", b);
                dofOut = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                Graphics.Blit(src, dofOut, mat, 5);
                RenderTexture.ReleaseTemporary(a);
                RenderTexture.ReleaseTemporary(b);
                source = dofOut;
            }

            const int levels = 5;
            var rts = new RenderTexture[levels];
            int w = source.width / 2, h = source.height / 2;
            var fmt = source.format;
            rts[0] = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0, fmt);
            mat.SetFloat("_BlurOffset", 1f);
            Graphics.Blit(source, rts[0], mat, 0);
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
            Graphics.Blit(source, dst, mat, 2);
            for (int i = 0; i < count; i++)
                if (rts[i] != null) RenderTexture.ReleaseTemporary(rts[i]);
            if (dofOut != null) RenderTexture.ReleaseTemporary(dofOut);
        }

        void SetupPhoto(RenderTexture src)
        {
            bool logo = showLogo && logoTexture != null;
            bool photo = filter > 0 || letterbox > 0.0005f || logo;
            if (!photo)
            {
                mat.DisableKeyword("BR_PHOTO");
                return;
            }
            mat.EnableKeyword("BR_PHOTO");
            int f = Mathf.Clamp(filter, 0, FilterNames.Length - 1);
            mat.SetVector("_FParams", FParams[f]);
            mat.SetColor("_FTint", FTint[f]);
            mat.SetColor("_FShadow", FShadow[f]);
            mat.SetColor("_FLift", FLift[f]);
            float lb = Mathf.Clamp(letterbox, 0f, 0.45f);
            mat.SetFloat("_Letterbox", lb);
            if (logo)
            {
                float aspect = (float)src.width / Mathf.Max(1, src.height);
                float texAspect = (float)logoTexture.width / Mathf.Max(1, logoTexture.height);
                float lh = 0.06f;
                float lw = lh * texAspect / aspect;
                float margin = 0.028f;
                mat.SetTexture("_LogoTex", logoTexture);
                mat.SetVector("_LogoRect", new Vector4(1f - margin / aspect - lw, lb + margin, lw, lh));
                mat.SetFloat("_LogoAlpha", Mathf.Clamp01(logoOpacity));
            }
            else
            {
                mat.SetVector("_LogoRect", new Vector4(2f, 2f, 1f, 1f));   // off screen, never divides by zero
                mat.SetFloat("_LogoAlpha", 0f);
            }
        }
    }
}

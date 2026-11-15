// PostFX.cs - built-in pipeline camera effect: soft bloom + filmic tonemap + dreamy pastel grade
// ("colourful with a slightly faded shade") + vignette. Toggle with F.
// Photo mode adds: filters, exposure / saturation / vignette trims, depth of field (gather blur
// driven by the camera depth texture), letterbox bars and a "Blossom Rally" watermark.
// With the photo fields at their defaults the output is exactly the normal look.
//
// Graphics upgrade (all quality gated, all off = today's look, see GraphicsFX.Enhanced):
//  * sun shafts (screen-space radial scattering from the sun, masked by depth / sky)
//  * valley height fog (mist pooling over the river and valley floor at dawn / after rain)
//  * colour grade per region and time of day (ColorGrading.cs)
//  * lens: sun flare (only when the sun is visible), lens dirt glow on bloom, rain / mud on the lens
//  * motion blur + speed lines at high speed (MotionBlur toggle)
//  * Ink & Watercolour art mode (InkMode toggle): a living Chinese ink painting
//
// Cinematic upgrade (Hidden/BlossomRally/PostFXPlus, toggles in GfxPrefs; Low never runs any of it except
// sharpening below 100% render scale; if that shader is unsupported everything falls back to the passes above):
//  * screen-space ambient occlusion (half res, depth-aware blur)          High / Ultra
//  * HQ bloom: 13-tap Karis prefilter + tent upsample (tighter lamp glow) High / Ultra
//  * aerial perspective (sky-coloured distance haze) + desert heat shimmer Medium+
//  * filmic highlight roll-off, softer bloom threshold at night            Medium+
//  * film grain                                                            High / Ultra
//  * cinematic depth of field (cinematic camera, replays, showroom, finish) High / Ultra
//  * upscale sharpening while the render scale is below 100%               any preset
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

        // ---------------------------------------------------------------- graphics settings (static)
        static bool prefsLoaded, ink, lensFx = true;
        static int mblurPref = -1;     // -1 = never chosen: on at High and Ultra

        static void LoadPrefs()
        {
            if (prefsLoaded) return;
            prefsLoaded = true;
            try
            {
                ink = PlayerPrefs.GetInt("BR_InkMode", 0) == 1;
                lensFx = PlayerPrefs.GetInt("BR_LensFX", 1) == 1;
                mblurPref = PlayerPrefs.GetInt("BR_MotionBlur", -1);
            }
            catch (System.Exception) { }
        }

        static void SavePref(string key, int v)
        {
            try { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        /// <summary>Ink &amp; Watercolour art mode (saved). Settings menu + photo mode toggle.</summary>
        public static bool InkMode
        {
            get { LoadPrefs(); return ink; }
            set { LoadPrefs(); ink = value; SavePref("BR_InkMode", value ? 1 : 0); }
        }

        /// <summary>Motion blur + speed lines at high speed (saved; default on at High / Ultra).</summary>
        public static bool MotionBlur
        {
            get { LoadPrefs(); return mblurPref < 0 ? GameSettings.Quality >= 2 : mblurPref == 1; }
            set { LoadPrefs(); mblurPref = value ? 1 : 0; SavePref("BR_MotionBlur", mblurPref); }
        }

        /// <summary>Motion blur back to "follow the preset" (GfxPrefs.ResetToPreset).</summary>
        public static void ResetMotionBlurPref()
        {
            LoadPrefs();
            mblurPref = -1;
            try { PlayerPrefs.DeleteKey("BR_MotionBlur"); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        /// <summary>Sun flare, lens dirt glow and rain / mud on the chase-camera lens (saved, default on).</summary>
        public static bool LensEffects
        {
            get { LoadPrefs(); return lensFx; }
            set { LoadPrefs(); lensFx = value; SavePref("BR_LensFX", value ? 1 : 0); }
        }

        // feature switches (not saved; for debugging / A-B comparisons)
        public static bool SunShafts = true;
        public static bool HeightFog = true;
        public static bool RegionGrading = true;
        public static bool SpeedLines = true;

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

        // shader passes
        const int PassPrefilter = 0, PassBlur = 1, PassComposite = 2, PassCoc = 3, PassDofBlur = 4, PassDofCombine = 5,
                  PassShaftMask = 6, PassShaftBlur = 7, PassInk = 8, PassSunVis = 9, PassLensDrop = 10, PassLite = 11;

        /// <summary>
        /// Low preset: the cheap grade - quarter-res 3-level bloom + one lite composite pass (tonemap, pastel
        /// grade incl. the region grade, vignette). Same palette as the full chain, ~1/3 of its GPU cost.
        /// Photo filters, depth of field and ink mode still use the full chain.
        /// </summary>
        public static bool LiteGrade { get { return GameSettings.Quality == 0; } }

        Material mat, matPlus;
        Camera cam;
        bool addedDepth;
        readonly RenderTexture[] bloomRts = new RenderTexture[5];
        readonly Vector3[] corners = new Vector3[4];
        readonly RegionGrader grader = new RegionGrader();
        readonly LensDrops lens = new LensDrops();
        Grade grade = Grade.Identity;
        float hfogT0 = -1f, hfogLive, shaftLive, frameDt;
        float mbLive, linesLive;
        int lastFrame = -1;

        static readonly int IdHasDepth = Shader.PropertyToID("_BR_HasDepth");

        // Hidden/BlossomRally/PostFXPlus passes
        const int PlusComposite = 0, PlusLiteSharp = 1, PlusAO = 2, PlusAOBlur = 3, PlusPrefilterHQ = 4, PlusDownHQ = 5, PlusUpHQ = 6;
        bool PlusOK { get { return matPlus != null && matPlus.passCount > PlusUpHQ; } }

        // cinematic depth of field (camera driven, see UpdateAutoDof)
        bool autoDof;
        float autoFocus = 10f, autoAperture = 4f;
        // region weights at the camera (desert heat), refreshed a few times a second
        static readonly float[] regionW = new float[6];
        float regionTimer, heatLive;

        // every uniform goes to both materials so either composite can run
        void SetF(string n, float v) { mat.SetFloat(n, v); if (matPlus != null) matPlus.SetFloat(n, v); }
        void SetV(string n, Vector4 v) { mat.SetVector(n, v); if (matPlus != null) matPlus.SetVector(n, v); }
        void SetC(string n, Color v) { mat.SetColor(n, v); if (matPlus != null) matPlus.SetColor(n, v); }
        void SetT(string n, Texture v) { mat.SetTexture(n, v); if (matPlus != null) matPlus.SetTexture(n, v); }

        void SetPhotoKeyword(bool on)
        {
            if (on) mat.EnableKeyword("BR_PHOTO"); else mat.DisableKeyword("BR_PHOTO");
            if (matPlus != null) { if (on) matPlus.EnableKeyword("BR_PHOTO"); else matPlus.DisableKeyword("BR_PHOTO"); }
        }

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
            if (sh != null && sh.isSupported)
            {
                mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                var shp = Shader.Find("Hidden/BlossomRally/PostFXPlus");
                if (shp != null && shp.isSupported) matPlus = new Material(shp) { hideFlags = HideFlags.HideAndDontSave };
                SetT("_NoiseTex", GfxTextures.Noise);
            }
        }

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
            if (matPlus != null) DestroyImmediate(matPlus);
            mat = null;
            matPlus = null;
            if (addedDepth && cam != null) cam.depthTextureMode &= ~DepthTextureMode.Depth;
            addedDepth = false;
            lens.Release();
            Shader.SetGlobalFloat(IdHasDepth, 0f);
        }

        bool WantDof { get { return (dof || autoDof) && effectEnabled && mat != null; } }

        /// <summary>True when the new screen effects run (master switch + a camera effect to run them).</summary>
        bool Enhanced { get { return GraphicsFX.Enhanced && mat != null; } }

        bool WantDepth
        {
            get
            {
                if (mat == null) return false;
                if (WantDof) return true;
                if (InkMode) return true;
                // valley mist, shaft masks and soft water edges read the depth texture. In forward with
                // directional shadows Unity renders it anyway for screen-space shadows, so this is cheap.
                return Enhanced && GameSettings.Quality >= 1;
            }
        }

        // the depth texture is rendered only while something needs it
        void UpdateDepthMode()
        {
            if (cam == null) return;
            if (WantDepth)
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

        bool DepthOn { get { return cam != null && (cam.depthTextureMode & DepthTextureMode.Depth) != 0; } }

        void OnPreCull()
        {
            UpdateDepthMode();
        }

        // the water shader only trusts the depth texture while this camera renders
        void OnPreRender()
        {
            Shader.SetGlobalFloat(IdHasDepth, DepthOn && Enhanced ? 1f : 0f);
        }

        void OnPostRender()
        {
            Shader.SetGlobalFloat(IdHasDepth, 0f);
        }

        void Update()
        {
            // once per frame (OnRenderImage may not run if the effect is off)
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            frameDt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            lens.Update(frameDt, Enhanced && LensEffects && GameSettings.Quality >= 1 && effectEnabled);
            UpdateAutoDof();
        }

        // ---------------------------------------------------------------- cinematic depth of field
        // Cinematic chase camera, instant replay, finish-line orbit / results and the showroom turntable focus
        // on the car with a gentle aperture. Photo mode's own depth of field always wins.
        void UpdateAutoDof()
        {
            bool want = false;
            Vector3 focusPt = Vector3.zero;
            if (Enhanced && effectEnabled && !dof && cam != null && GfxPrefs.On(GfxFeature.CinematicDof))
            {
                var pm = PhotoMode.Instance;
                var gm = GameManager.Instance;
                bool photo = pm != null && pm.Active;
                if (!photo && gm != null && gm.Rig != null && gm.Car != null)
                {
                    var rig = gm.Rig;
                    bool cine = rig.mode == CamMode.Cinematic && gm.IsDriving && !rig.externalControl && !PlayerState.OnFoot;
                    bool replay = ReplaySystem.Instance != null && ReplaySystem.Instance.Active;
                    bool finish = ResultsCamera.Active || FinishMoment.Running;
                    want = cine || replay || finish || rig.showroom;
                    focusPt = gm.Car.transform.position + Vector3.up * 0.6f;
                    autoAperture = rig.showroom ? 2.4f : 4f;
                }
            }
            if (want)
            {
                Transform ct = cam.transform;
                float d = Vector3.Dot(focusPt - ct.position, ct.forward);
                d = Mathf.Clamp(d > 0.5f ? d : (focusPt - ct.position).magnitude, 0.5f, 300f);
                autoFocus = autoDof ? Mathf.Lerp(autoFocus, d, 1f - Mathf.Exp(-frameDt / 0.12f)) : d;
            }
            autoDof = want;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            bool inkOn = InkMode && mat != null;
            if (mat == null || (!effectEnabled && !inkOn))
            {
                Graphics.Blit(src, dst);
                return;
            }
            bool photo = SetupPhoto(src);
            if (!effectEnabled)
            {
                // ink mode on the raw image (colour grade switched off)
                SetF("_Overlay", 0f);
                RenderInk(src, dst);
                return;
            }

            // ---- base grade (+ region / time-of-day grade) ----
            if (Enhanced && RegionGrading) grade = grader.Update(cam.transform.position, frameDt);
            else grade = Grade.Identity;
            if (LiteGrade && !inkOn && !photo && !WantDof && mat.passCount > PassLite)
            {
                RenderLite(src, dst);
                return;
            }
            bool cine = Enhanced && GameSettings.Quality >= 1;
            // street lamps, neon and lanterns bloom a little more readily at night
            SetF("_Threshold", cine ? threshold * Mathf.Lerp(1f, 0.82f, Atmosphere.NightFactor) : threshold);
            SetF("_Knee", 0.5f);
            SetF("_BloomIntensity", bloomIntensity);
            SetF("_Exposure", exposure * Mathf.Pow(2f, exposureEV));
            SetF("_Filmic", cine ? 1f : 0f);
            SetF("_Saturation", saturation * saturationMul * grade.sat);
            SetF("_Contrast", contrast * grade.con);
            // SetColor linearises (^2.2) in linear colour space; the grade values are authored in the
            // shader's perceptual space, so pre-compensate them to keep their intended strength
            Vector3 gt = Perceptual(grade.tint), gs = Perceptual(grade.shadow), glc = Perceptual(grade.lift);
            float la = lift.a + grade.liftAmt;
            Color gl = la > 1e-4f ? (lift * lift.a + new Color(glc.x, glc.y, glc.z) * grade.liftAmt) * (1f / la) : lift;
            gl.a = la;
            SetC("_Lift", gl);
            SetC("_Tint", new Color(tint.r * gt.x, tint.g * gt.y, tint.b * gt.z, tint.a));
            SetC("_ShadowTint", new Color(shadowTint.r * gs.x, shadowTint.g * gs.y, shadowTint.b * gs.z, shadowTint.a + grade.shadowAmt));
            SetF("_Vignette", Mathf.Max(0f, vignette + vignetteAdd));
            SetF("_Overlay", inkOn ? 0f : 1f);
            bool depthOK = DepthOn;
            SetF("_DepthOK", depthOK ? 1f : 0f);
            SetupFrustum();

            // ---- depth of field: CoC + downsample, gather blur, combine (full res) ----
            RenderTexture source = src;
            RenderTexture dofOut = null;
            if (WantDof && depthOK)
            {
                int hw = Mathf.Max(1, src.width / 2), hh = Mathf.Max(1, src.height / 2);
                var cocFmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                var a = RenderTexture.GetTemporary(hw, hh, 0, cocFmt);
                var b = RenderTexture.GetTemporary(hw, hh, 0, cocFmt);
                a.filterMode = FilterMode.Bilinear;
                b.filterMode = FilterMode.Bilinear;
                float fd = dof ? focusDistance : autoFocus, ap = dof ? aperture : autoAperture;
                float strength = Mathf.Clamp(2.8f / Mathf.Max(0.5f, ap), 0.05f, 3f) * 0.85f;
                float maxRadius = Mathf.Clamp(hh * 0.016f, 2f, 18f);
                SetV("_DofParams", new Vector4(Mathf.Max(0.1f, fd), strength, maxRadius, 0f));
                Graphics.Blit(src, a, mat, PassCoc);
                Graphics.Blit(a, b, mat, PassDofBlur);
                SetT("_DofTex", b);
                dofOut = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                Graphics.Blit(src, dofOut, mat, PassDofCombine);
                RenderTexture.ReleaseTemporary(a);
                RenderTexture.ReleaseTemporary(b);
                source = dofOut;
            }

            // ---- bloom ----
            // High / Ultra: 13-tap Karis prefilter + downsample and a tent upsample that keeps part of every
            // level (tight cores round lamps and neon, the same soft pastel haze around them)
            bool hq = cine && GameSettings.Quality >= 2 && PlusOK;
            const int levels = 5;
            int w = source.width / 2, h = source.height / 2;
            var fmt = source.format;
            bloomRts[0] = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0, fmt);
            SetF("_BlurOffset", 1f);
            if (hq) Graphics.Blit(source, bloomRts[0], matPlus, PlusPrefilterHQ);
            else Graphics.Blit(source, bloomRts[0], mat, PassPrefilter);
            int count = 1;
            for (int i = 1; i < levels; i++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                bloomRts[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                if (hq) Graphics.Blit(bloomRts[i - 1], bloomRts[i], matPlus, PlusDownHQ);
                else Graphics.Blit(bloomRts[i - 1], bloomRts[i], mat, PassBlur);
                count++;
            }
            if (hq) matPlus.SetFloat("_Scatter", Mathf.Lerp(0.8f, 0.7f, Atmosphere.NightFactor));
            else SetF("_BlurOffset", 0.5f);
            for (int i = count - 2; i >= 0; i--)
            {
                var up = RenderTexture.GetTemporary(bloomRts[i].width, bloomRts[i].height, 0, fmt);
                if (hq)
                {
                    matPlus.SetTexture("_BloomHigh", bloomRts[i]);
                    Graphics.Blit(bloomRts[i + 1], up, matPlus, PlusUpHQ);
                }
                else Graphics.Blit(bloomRts[i + 1], up, mat, PassBlur);
                RenderTexture.ReleaseTemporary(bloomRts[i]);
                bloomRts[i] = up;
            }
            SetT("_BloomTex", bloomRts[0]);

            // ---- new effects ----
            RenderTexture shaftA = null, shaftB = null, shaftMask = null, sunVis = null;
            SetupHeightFog(depthOK);
            SetupMotion();
            RenderTexture aoRt = SetupAO(depthOK, source);
            SetupCinematic(depthOK);
            bool sunOk = SetupSun(depthOK, source, ref shaftA, ref shaftB, ref shaftMask, ref sunVis);
            if (!sunOk)
            {
                SetF("_ShaftOn", 0f);
                SetV("_Flare", Vector4.zero);
            }
            bool lensOn = Enhanced && LensEffects && GameSettings.Quality >= 1;
            // lens dirt mostly shows on bright lights at night; by day only a hint against the sun / sky
            float dirt = lensOn ? (GameSettings.Quality >= 2 ? 0.75f : 0.5f) * (0.3f + 0.7f * Atmosphere.NightFactor) : 0f;
            SetV("_LensFx", new Vector4(dirt, 0f, 0f, 0f));
            if (lensOn) SetT("_DirtTex", GfxTextures.LensDirt);
            RenderTexture lensRt = lensOn ? lens.Render(mat, PassLensDrop, src.width, src.height) : null;
            SetF("_LensOn", lensRt != null ? 1f : 0f);
            if (lensRt != null)
            {
                SetT("_LensTex", lensRt);
                Color sl = Shader.GetGlobalColor("_BR_SceneLight");
                float lum = Mathf.Clamp((sl.r + sl.g + sl.b) / 3f, 0.08f, 1.4f);
                SetV("_LensParams", new Vector4(0.035f, 0.34f * lum, 0.24f * lum, 0.15f * lum));
            }

            // ---- composite (+ ink) ----
            Material cm = PlusOK ? matPlus : mat;
            int cp = PlusOK ? PlusComposite : PassComposite;
            if (inkOn)
            {
                var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
                Graphics.Blit(source, tmp, cm, cp);
                RenderInk(tmp, dst);
                RenderTexture.ReleaseTemporary(tmp);
            }
            else Graphics.Blit(source, dst, cm, cp);

            for (int i = 0; i < count; i++)
                if (bloomRts[i] != null) { RenderTexture.ReleaseTemporary(bloomRts[i]); bloomRts[i] = null; }
            if (dofOut != null) RenderTexture.ReleaseTemporary(dofOut);
            if (shaftA != null) RenderTexture.ReleaseTemporary(shaftA);
            if (shaftB != null) RenderTexture.ReleaseTemporary(shaftB);
            if (shaftMask != null) RenderTexture.ReleaseTemporary(shaftMask);
            if (sunVis != null) RenderTexture.ReleaseTemporary(sunVis);
            if (aoRt != null) RenderTexture.ReleaseTemporary(aoRt);
        }

        // ---------------------------------------------------------------- lite chain (Low)
        void RenderLite(RenderTexture src, RenderTexture dst)
        {
            SetF("_Threshold", threshold);
            SetF("_Knee", 0.5f);
            SetF("_BloomIntensity", bloomIntensity);
            SetF("_Exposure", exposure * Mathf.Pow(2f, exposureEV));
            // no realtime shadows on Low: a touch more contrast / colour keeps the image from looking flat
            SetF("_Saturation", saturation * saturationMul * grade.sat * 1.03f);
            SetF("_Contrast", contrast * grade.con * 1.04f);
            Vector3 gt = Perceptual(grade.tint), gs = Perceptual(grade.shadow), glc = Perceptual(grade.lift);
            float la = lift.a + grade.liftAmt;
            Color gl = la > 1e-4f ? (lift * lift.a + new Color(glc.x, glc.y, glc.z) * grade.liftAmt) * (1f / la) : lift;
            gl.a = la;
            SetC("_Lift", gl);
            SetC("_Tint", new Color(tint.r * gt.x, tint.g * gt.y, tint.b * gt.z, tint.a));
            SetC("_ShadowTint", new Color(shadowTint.r * gs.x, shadowTint.g * gs.y, shadowTint.b * gs.z, shadowTint.a + grade.shadowAmt));
            SetF("_Vignette", Mathf.Max(0f, vignette + vignetteAdd));

            // bloom: prefilter straight to quarter res (4 bilinear taps cover the 4x4 block), 2 more levels, back up
            var fmt = src.format;
            int w = Mathf.Max(4, src.width / 4), h = Mathf.Max(4, src.height / 4);
            var b0 = RenderTexture.GetTemporary(w, h, 0, fmt);
            var b1 = RenderTexture.GetTemporary(Mathf.Max(2, w / 2), Mathf.Max(2, h / 2), 0, fmt);
            var b2 = RenderTexture.GetTemporary(Mathf.Max(2, w / 4), Mathf.Max(2, h / 4), 0, fmt);
            b0.filterMode = b1.filterMode = b2.filterMode = FilterMode.Bilinear;
            SetF("_BlurOffset", 1f);
            Graphics.Blit(src, b0, mat, PassPrefilter);
            Graphics.Blit(b0, b1, mat, PassBlur);
            Graphics.Blit(b1, b2, mat, PassBlur);
            SetF("_BlurOffset", 0.5f);
            Graphics.Blit(b2, b1, mat, PassBlur);
            Graphics.Blit(b1, b0, mat, PassBlur);
            SetT("_BloomTex", b0);
            float sharpen = GfxPrefs.SharpenAmount;
            if (sharpen > 0f && PlusOK)
            {
                matPlus.SetFloat("_Sharpen", sharpen);
                Graphics.Blit(src, dst, matPlus, PlusLiteSharp);
            }
            else Graphics.Blit(src, dst, mat, PassLite);
            RenderTexture.ReleaseTemporary(b0);
            RenderTexture.ReleaseTemporary(b1);
            RenderTexture.ReleaseTemporary(b2);
        }

        static Vector3 Perceptual(Vector3 v)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return v;
            const float k = 1f / 2.2f;
            return new Vector3(Mathf.Pow(Mathf.Max(0f, v.x), k), Mathf.Pow(Mathf.Max(0f, v.y), k), Mathf.Pow(Mathf.Max(0f, v.z), k));
        }

        static Vector4 Lin(Color c, float k)
        {
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            return new Vector4(l.r * k, l.g * k, l.b * k, 0f);
        }

        // world-space view rays for eye depth 1 at the four screen corners (height fog reconstruction)
        void SetupFrustum()
        {
            cam.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), 1f, Camera.MonoOrStereoscopicEye.Mono, corners);
            Transform t = cam.transform;
            // corners: bottom-left, top-left, top-right, bottom-right (view space, z = 1)
            SetV("_FrustumBL", t.TransformVector(corners[0]));
            SetV("_FrustumTL", t.TransformVector(corners[1]));
            SetV("_FrustumTR", t.TransformVector(corners[2]));
            SetV("_FrustumBR", t.TransformVector(corners[3]));
        }

        // ---------------------------------------------------------------- valley mist
        // extinction (1/m) at the mist base per time of day, and multipliers per weather
        static readonly float[] MistTime = { 0.0085f, 0.0022f, 0.0045f, 0.0065f, 0.0048f };
        static readonly float[] MistWeather = { 1f, 1.5f, 1.25f, 0.9f, 1.9f, 0f };

        void SetupHeightFog(bool depthOK)
        {
            var atm = Atmosphere.Instance;
            float target = 0f;
            if (Enhanced && HeightFog && depthOK && GameSettings.Quality >= 1 && atm != null)
                target = MistTime[Mathf.Clamp(atm.TimeIndex, 0, 4)] * MistWeather[Mathf.Clamp(atm.WeatherIndex, 0, 5)];
            if (hfogT0 < 0f) { hfogLive = target; hfogT0 = 0f; }
            hfogLive = Mathf.Lerp(hfogLive, target, 1f - Mathf.Exp(-frameDt / 1.1f));
            if (hfogLive < 0.0002f)
            {
                SetV("_HFog", Vector4.zero);
                return;
            }
            // thinner, taller mist after rain; a lower, denser layer at dawn
            float wet = Atmosphere.Wetness;
            float k = Mathf.Lerp(0.13f, 0.085f, wet);
            SetV("_HFog", new Vector4(hfogLive, -1.6f, k, Mathf.Lerp(0.82f, 0.9f, wet)));
            Color fc = RenderSettings.fogColor;
            fc = Color.Lerp(fc, Color.white, 0.12f * (1f - Atmosphere.NightFactor));
            SetC("_HFogColor", fc);
            Color sunC = Atmosphere.Sun != null ? Atmosphere.Sun.color : Color.black;
            float sunI = Atmosphere.Sun != null ? Mathf.Min(1.4f, Atmosphere.Sun.intensity) : 0f;
            SetV("_HFogSun", Lin(sunC, sunI * 0.55f * (1f - Atmosphere.NightFactor) * (1f - Atmosphere.CloudCover * 0.6f)));
            Vector3 sd = Atmosphere.Sun != null ? -Atmosphere.Sun.transform.forward : Vector3.up;
            SetV("_HFogParams", new Vector4(sd.x, sd.y, sd.z, 900f));
            SetV("_HFogDrift", new Vector4(Time.time * 0.0016f, Time.time * 0.0007f, 0f, 0f));
            SetT("_NoiseTex", GfxTextures.Noise);
        }

        // ---------------------------------------------------------------- motion blur + speed lines
        void SetupMotion()
        {
            float mbTarget = 0f, linesTarget = 0f;
            Vector2 focus = new Vector2(0.5f, 0.5f);
            var gm = GameManager.Instance;
            bool driving = gm != null && gm.Car != null && gm.Rig != null && gm.IsDriving && !gm.Rig.externalControl && !gm.Rig.showroom
                           && Time.timeScale > 0f && cam != null;
            if (Enhanced && driving && MotionBlur && GameSettings.Quality >= 1)
            {
                var car = gm.Car;
                float kmh = car.SpeedKmh;
                mbTarget = Mathf.Clamp01((kmh - 85f) / 120f);
                if (gm.Rig.mode == CamMode.Chase && SpeedLines) linesTarget = Mathf.Clamp01((kmh - 135f) / 70f);
                Rigidbody rb = car.Body;
                Vector3 v = rb != null ? rb.GetVelocity() : car.transform.forward;
                if (v.sqrMagnitude > 1f)
                {
                    Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + v.normalized * 60f);
                    if (vp.z > 0f) focus = new Vector2(Mathf.Clamp(vp.x, 0.2f, 0.8f), Mathf.Clamp(vp.y, 0.25f, 0.75f));
                }
            }
            float k = 1f - Mathf.Exp(-frameDt / 0.25f);
            mbLive = Mathf.Lerp(mbLive, mbTarget, k);
            linesLive = Mathf.Lerp(linesLive, linesTarget, k);
            int taps = GameSettings.Quality >= 3 ? 8 : (GameSettings.Quality >= 2 ? 6 : 4);
            SetV("_MBlur", new Vector4(mbLive < 0.01f ? 0f : mbLive * 0.055f, focus.x, focus.y, taps));
            SetV("_SpeedLines", new Vector4(linesLive < 0.01f ? 0f : linesLive * 0.22f, Time.time, cam != null ? cam.aspect : 1.6f, 0f));
        }

        // ---------------------------------------------------------------- ambient occlusion
        // Half-res SSAO from the depth texture: 8 samples (12 on Ultra, 6 when forced on Medium) on a
        // golden-angle disc of ~1.2 m, normals rebuilt from depth, then a depth-aware blur on each axis.
        // Faded out by 140 m and on the sky. Returns the AO target (caller releases it) or null when off.
        RenderTexture SetupAO(bool depthOK, RenderTexture source)
        {
            if (!PlusOK || !depthOK || !GfxPrefs.On(GfxFeature.AmbientOcclusion))
            {
                if (matPlus != null) matPlus.SetFloat("_AOOn", 0f);
                return null;
            }
            int aw = Mathf.Max(4, source.width / 2), ah = Mathf.Max(4, source.height / 2);
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            var a = RenderTexture.GetTemporary(aw, ah, 0, fmt);
            var b = RenderTexture.GetTemporary(aw, ah, 0, fmt);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            Matrix4x4 p = cam.projectionMatrix;
            float p00 = Mathf.Abs(p.m00) > 1e-5f ? p.m00 : 1f, p11 = Mathf.Abs(p.m11) > 1e-5f ? p.m11 : 1f;
            int q = GameSettings.Quality;
            matPlus.SetVector("_AOProj", new Vector4(1f / p00, 1f / p11, p00 * 0.5f, p11 * 0.5f));
            matPlus.SetVector("_AOTexel", new Vector4(1f / Mathf.Max(1, source.width), 1f / Mathf.Max(1, source.height), 0f, 0f));
            matPlus.SetVector("_AOParams", new Vector4(1.2f, 1.45f, 140f, q >= 3 ? 12f : (q >= 2 ? 8f : 6f)));
            Graphics.Blit(source, a, matPlus, PlusAO);
            matPlus.SetVector("_AODir", new Vector4(1.25f / aw, 0f, 0f, 0f));
            Graphics.Blit(a, b, matPlus, PlusAOBlur);
            matPlus.SetVector("_AODir", new Vector4(0f, 1.25f / ah, 0f, 0f));
            Graphics.Blit(b, a, matPlus, PlusAOBlur);
            RenderTexture.ReleaseTemporary(b);
            matPlus.SetTexture("_AOTex", a);
            matPlus.SetFloat("_AOOn", 1f);
            // occluded corners keep a little cool sky light (pastel, never black)
            float night = Atmosphere.NightFactor;
            matPlus.SetVector("_AOTint", Vector4.Lerp(new Vector4(0.30f, 0.30f, 0.38f, 0f), new Vector4(0.42f, 0.44f, 0.56f, 0f), night));
            return a;
        }

        // ---------------------------------------------------------------- aerial perspective, heat, grain, sharpening
        static readonly Vector4 CoolAir = new Vector4(0.90f, 0.96f, 1.10f, 0f);

        void SetupCinematic(bool depthOK)
        {
            if (matPlus == null) return;
            float night = Atmosphere.NightFactor;
            // aerial perspective (needs the depth texture and some fog; "Fog Off" stays crystal clear)
            float fogD = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            bool atmos = PlusOK && depthOK && GfxPrefs.On(GfxFeature.Atmospherics);
            if (atmos && fogD > 1e-6f)
            {
                Color fog = RenderSettings.fogColor;
                Vector4 fogL = Lin(fog, 1f);
                Vector4 haze = Vector4.Scale(fogL, Vector4.Lerp(CoolAir, new Vector4(1f, 1f, 1f, 0f), night));
                Vector4 sunL = Vector4.zero;
                Vector3 sd = Vector3.up;
                var sun = Atmosphere.Sun;
                if (sun != null)
                {
                    sd = -sun.transform.forward;
                    float elevK = Mathf.Clamp01(sd.y * 4f + 0.3f);
                    sunL = Lin(sun.color, Mathf.Min(1.3f, sun.intensity) * 0.5f * (1f - night) * (1f - Atmosphere.CloudCover * 0.7f) * elevK);
                }
                matPlus.SetVector("_Aerial", new Vector4(0.85f, 0.0003f, Mathf.Lerp(0.26f, 0.12f, night), fogD));
                matPlus.SetVector("_AerialFog", fogL);
                matPlus.SetVector("_AerialHaze", haze);
                matPlus.SetVector("_AerialSun", sunL);
                matPlus.SetVector("_AerialSunDir", new Vector4(sd.x, sd.y, sd.z, 0f));
            }
            else matPlus.SetVector("_Aerial", Vector4.zero);

            // desert heat shimmer: Red Canyon, by day, under a clear sky
            float heatTarget = 0f;
            if (atmos && cam != null)
            {
                regionTimer -= frameDt;
                if (regionTimer <= 0f)
                {
                    regionTimer = 0.25f;
                    Vector3 cp = cam.transform.position;
                    WorldLayout.Weights(new Vector2(cp.x, cp.z), regionW);
                }
                float elev = Atmosphere.Sun != null ? -Atmosphere.Sun.transform.forward.y : 0f;
                heatTarget = regionW[(int)RegionId.Canyon] * (1f - night) * (1f - Atmosphere.CloudCover) * (1f - Atmosphere.RainIntensity)
                             * Mathf.Clamp01(elev * 3f) * (1f - Atmosphere.Wetness);
            }
            heatLive = Mathf.Lerp(heatLive, heatTarget, 1f - Mathf.Exp(-frameDt / 1.5f));
            matPlus.SetVector("_Heat", new Vector4(heatLive < 0.02f ? 0f : heatLive, Time.time, cam != null ? cam.aspect : 1.6f, 0f));

            // film grain (High / Ultra by default), re-seeded every frame
            float grain = GfxPrefs.On(GfxFeature.FilmGrain) ? (GameSettings.Quality >= 3 ? 0.022f : 0.018f) * (1f + night * 0.4f) : 0f;
            matPlus.SetVector("_Grain", new Vector4(grain, (Time.frameCount % 61) * 13.37f, 0f, 0f));
            matPlus.SetFloat("_Sharpen", GfxPrefs.SharpenAmount);
        }

        // ---------------------------------------------------------------- sun shafts + flare
        bool SetupSun(bool depthOK, RenderTexture source, ref RenderTexture shaftA, ref RenderTexture shaftB, ref RenderTexture shaftMask, ref RenderTexture sunVis)
        {
            if (!Enhanced || GameSettings.Quality < 1 || Atmosphere.Sun == null || cam == null) return false;
            Vector3 sunDir = -Atmosphere.Sun.transform.forward;
            float elev = Mathf.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + sunDir * 1000f);
            if (vp.z <= 0f || elev < -2f) { shaftLive = 0f; return false; }
            // strongest with a low sun (morning, sunset), gone at night and in rain
            float lowSun = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 48f, elev));
            float night = Atmosphere.NightFactor;
            float target = (0.35f + 0.65f * lowSun) * (1f - night) * (1f - night) * (1f - Atmosphere.RainIntensity)
                           * (1f - 0.7f * Atmosphere.CloudCover * Atmosphere.CloudCover) * (1f + Atmosphere.Wetness * 0.25f);
            float off = Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f));
            float screenFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.25f, off));
            target *= screenFade;
            shaftLive = Mathf.Lerp(shaftLive, target, 1f - Mathf.Exp(-frameDt / 0.35f));
            if (shaftLive < 0.01f) return false;

            float aspect = (float)source.width / Mathf.Max(1, source.height);
            SetV("_SunScreen", new Vector4(vp.x, vp.y, 0.9f, aspect));
            Color sc = Atmosphere.Sun.color;
            float lum = Mathf.Max(0.0001f, sc.r * 0.3f + sc.g * 0.59f + sc.b * 0.11f);
            SetF("_ShaftThreshold", 0.32f);

            bool shafts = SunShafts;
            int div = GameSettings.Quality >= 3 ? 3 : 4;
            int sw = Mathf.Max(8, source.width / div), sh = Mathf.Max(8, source.height / div);
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            shaftMask = RenderTexture.GetTemporary(sw, sh, 0, fmt);
            shaftMask.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, shaftMask, mat, PassShaftMask);
            if (shafts)
            {
                shaftA = RenderTexture.GetTemporary(sw, sh, 0, fmt);
                shaftB = RenderTexture.GetTemporary(sw, sh, 0, fmt);
                shaftA.filterMode = shaftB.filterMode = FilterMode.Bilinear;
                int iters = GameSettings.Quality >= 2 ? 3 : 2;
                float radius = 0.55f;
                RenderTexture from = shaftMask, to = shaftA;
                for (int i = 0; i < iters; i++)
                {
                    SetF("_ShaftRadius", radius);
                    Graphics.Blit(from, to, mat, PassShaftBlur);
                    from = to;
                    to = to == shaftA ? shaftB : shaftA;
                    radius *= 0.42f;
                }
                SetT("_ShaftTex", from);
                SetF("_ShaftOn", 1f);
                // shafts tinted by the sun, a touch warmer, scaled so a bright sun does not blow out
                SetV("_ShaftColor", Lin(Color.Lerp(sc, new Color(1f, 0.82f, 0.62f), 0.25f), 0.9f * shaftLive / Mathf.Max(0.6f, lum)));
            }
            else SetF("_ShaftOn", 0f);

            if (LensEffects)
            {
                sunVis = RenderTexture.GetTemporary(1, 1, 0, fmt);
                Graphics.Blit(shaftMask, sunVis, mat, PassSunVis);
                SetT("_SunVisTex", sunVis);
                float fl = shaftLive * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, off)));
                Vector4 fc = Lin(sc, 1f);
                SetV("_Flare", new Vector4(fl * 0.55f, fc.x, fc.y, fc.z));
            }
            else SetV("_Flare", Vector4.zero);
            return true;
        }

        // ---------------------------------------------------------------- ink & watercolour
        void RenderInk(RenderTexture src, RenderTexture dst)
        {
            int hw = Mathf.Max(4, src.width / 2), hh = Mathf.Max(4, src.height / 2);
            var half = RenderTexture.GetTemporary(hw, hh, 0, src.format);
            var quarter = RenderTexture.GetTemporary(Mathf.Max(2, hw / 2), Mathf.Max(2, hh / 2), 0, src.format);
            var eighth = RenderTexture.GetTemporary(Mathf.Max(2, hw / 4), Mathf.Max(2, hh / 4), 0, src.format);
            half.filterMode = quarter.filterMode = eighth.filterMode = FilterMode.Bilinear;
            SetF("_BlurOffset", 1f);
            Graphics.Blit(src, half, mat, PassBlur);
            Graphics.Blit(half, quarter, mat, PassBlur);
            Graphics.Blit(quarter, eighth, mat, PassBlur);
            SetF("_BlurOffset", 0.5f);
            Graphics.Blit(eighth, quarter, mat, PassBlur);
            SetT("_BleedTex", quarter);
            SetT("_PaperTex", GfxTextures.Paper);
            SetT("_NoiseTex", GfxTextures.Noise);
            bool depthOK = DepthOn;
            SetF("_DepthOK", depthOK ? 1f : 0f);
            float px = Mathf.Max(1f, src.height / 720f);
            float aspect = (float)src.width / Mathf.Max(1, src.height);
            // boil the line work at ~7 drawings per second like a hand-animated painting
            float boil = Mathf.Floor(Time.unscaledTime * 7f);
            SetV("_InkParams", new Vector4(px, aspect, boil, Atmosphere.NightFactor));
            Graphics.Blit(src, dst, mat, PassInk);
            RenderTexture.ReleaseTemporary(half);
            RenderTexture.ReleaseTemporary(quarter);
            RenderTexture.ReleaseTemporary(eighth);
        }

        bool SetupPhoto(RenderTexture src)
        {
            bool logo = showLogo && logoTexture != null;
            bool photo = filter > 0 || letterbox > 0.0005f || logo;
            if (!photo)
            {
                SetPhotoKeyword(false);
                return false;
            }
            SetPhotoKeyword(true);
            int f = Mathf.Clamp(filter, 0, FilterNames.Length - 1);
            SetV("_FParams", FParams[f]);
            SetC("_FTint", FTint[f]);
            SetC("_FShadow", FShadow[f]);
            SetC("_FLift", FLift[f]);
            float lb = Mathf.Clamp(letterbox, 0f, 0.45f);
            SetF("_Letterbox", lb);
            if (logo)
            {
                float aspect = (float)src.width / Mathf.Max(1, src.height);
                float texAspect = (float)logoTexture.width / Mathf.Max(1, logoTexture.height);
                float lh = 0.06f;
                float lw = lh * texAspect / aspect;
                float margin = 0.028f;
                SetT("_LogoTex", logoTexture);
                SetV("_LogoRect", new Vector4(1f - margin / aspect - lw, lb + margin, lw, lh));
                SetF("_LogoAlpha", Mathf.Clamp01(logoOpacity));
            }
            else
            {
                SetV("_LogoRect", new Vector4(2f, 2f, 1f, 1f));   // off screen, never divides by zero
                SetF("_LogoAlpha", 0f);
            }
            return true;
        }
    }
}

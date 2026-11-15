// GfxPrefs.cs - the cinematic graphics toggles of the Visual effects menu, tied to the quality presets.
// Each toggle is "follow the preset" until the player flips it (then saved). A feature also needs its
// minimum preset (e.g. the depth texture the screen-space effects read only exists from Medium up), so
// Low never pays for any of them:
//   Ambient occlusion        High / Ultra   (allowed from Medium)   screen-space AO from the depth texture
//   Atmospheric scattering   Medium and up                          aerial perspective + desert heat shimmer
//   Cinematic depth of field High / Ultra   (allowed from Medium)   cinematic camera, replays, showroom, finish
//   Film grain               High / Ultra   (allowed from Medium)
//   Upscale sharpening       every preset, only while the render scale is below 100%
using UnityEngine;

namespace BlossomRally
{
    public enum GfxFeature { AmbientOcclusion = 0, Atmospherics = 1, CinematicDof = 2, FilmGrain = 3, Sharpening = 4 }

    public static class GfxPrefs
    {
        public const int Count = 5;
        static readonly string[] Keys = { "BR_GfxAO", "BR_GfxAtmos", "BR_GfxCineDof", "BR_GfxGrain", "BR_GfxSharpen" };
        static readonly int[] MinQ = { 1, 1, 1, 1, 0 };            // below this preset the feature is never on
        static readonly int[] DefaultFromQ = { 2, 1, 2, 2, 0 };    // preset default: on from this preset up
        static readonly int[] prefs = { -1, -1, -1, -1, -1 };      // -1 = follow the preset, 0 off, 1 on
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i = 0; i < Count; i++)
            {
                try { prefs[i] = Mathf.Clamp(PlayerPrefs.GetInt(Keys[i], -1), -1, 1); } catch (System.Exception) { prefs[i] = -1; }
            }
        }

        /// <summary>The preset (and the master "Enhanced graphics" switch) allows this feature.</summary>
        public static bool Available(GfxFeature f)
        {
            return GraphicsFX.Enhanced && GameSettings.Quality >= MinQ[(int)f];
        }

        /// <summary>The player's choice, or the preset default when never chosen.</summary>
        public static bool Wanted(GfxFeature f)
        {
            Load();
            int p = prefs[(int)f];
            return p < 0 ? GameSettings.Quality >= DefaultFromQ[(int)f] : p == 1;
        }

        /// <summary>True when the feature runs this frame.</summary>
        public static bool On(GfxFeature f) { return Available(f) && Wanted(f); }

        public static void Toggle(GfxFeature f)
        {
            Load();
            int v = Wanted(f) ? 0 : 1;
            prefs[(int)f] = v;
            sharpenFrame = -1000;
            try { PlayerPrefs.SetInt(Keys[(int)f], v); PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        /// <summary>Menu value text.</summary>
        public static string Label(GfxFeature f)
        {
            if (!GraphicsFX.Enhanced) return "Off (enhanced off)";
            if (GameSettings.Quality < MinQ[(int)f]) return "Off (" + GameSettings.QualityNames[MinQ[(int)f]] + "+)";
            if (f == GfxFeature.Sharpening && Wanted(f)) return SharpenAmount > 0f ? "On (active)" : "On (below 100%)";
            return Wanted(f) ? "On" : "Off";
        }

        /// <summary>Every effect toggle back to "follow the preset" (motion blur included).</summary>
        public static void ResetToPreset()
        {
            Load();
            for (int i = 0; i < Count; i++)
            {
                prefs[i] = -1;
                try { PlayerPrefs.DeleteKey(Keys[i]); } catch (System.Exception) { }
            }
            sharpenFrame = -1000;
            PostFX.ResetMotionBlurPref();
            try { PlayerPrefs.Save(); } catch (System.Exception) { }
        }

        // ---------------------------------------------------------------- sharpening amount
        static float sharpenCache = -1f;
        static int sharpenFrame = -1000;

        /// <summary>
        /// 0 = off. Fullscreen below the native resolution (render scale or an explicit lower resolution)
        /// the display scaler softens the image: a light sharpen before it brings the detail back.
        /// Re-evaluated about once a second.
        /// </summary>
        public static float SharpenAmount
        {
            get
            {
                if (Time.frameCount - sharpenFrame < 60 && sharpenCache >= 0f) return sharpenCache;
                sharpenFrame = Time.frameCount;
                float k = 0f;
                if (On(GfxFeature.Sharpening) && Screen.fullScreen && !Application.isEditor)
                {
                    int nw = 0;
                    try { nw = Display.main.systemWidth; } catch (System.Exception) { nw = 0; }
                    if (nw <= 0) nw = Screen.currentResolution.width;
                    float s = nw > 0 ? Screen.width / (float)nw : 1f;
                    if (s < 0.985f) k = Mathf.Clamp((1f - s) * 1.6f, 0.22f, 0.6f);
                }
                sharpenCache = k;
                return k;
            }
        }
    }
}

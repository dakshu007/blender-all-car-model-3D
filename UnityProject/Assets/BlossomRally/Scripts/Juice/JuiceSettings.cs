// JuiceSettings.cs - player toggles for the "juice" layer (camera motion, slow motion, pop-ups),
// stored in PlayerPrefs. The settings menu only has to read / write these properties.
using UnityEngine;

namespace BlossomRally
{
    public static class JuiceSettings
    {
        const string KeyReduced = "BR_ReducedMotion", KeySlowMo = "BR_SlowMoMoments", KeyPopups = "BR_ScorePopups";
        static bool loaded, reduced, slowMo = true, popups = true;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            reduced = PlayerPrefs.GetInt(KeyReduced, 0) != 0;
            slowMo = PlayerPrefs.GetInt(KeySlowMo, 1) != 0;
            popups = PlayerPrefs.GetInt(KeyPopups, 1) != 0;
        }

        static void Store(string key, bool v)
        {
            PlayerPrefs.SetInt(key, v ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Accessibility: no camera shakes, rolls, vibration or impact jolts; landing dips are softened.</summary>
        public static bool ReducedMotion
        {
            get { Load(); return reduced; }
            set { Load(); if (reduced == value) return; reduced = value; Store(KeyReduced, value); }
        }

        /// <summary>Brief slow motion on Big Air, Perfect Drift and the finish line.</summary>
        public static bool SlowMoMoments
        {
            get { Load(); return slowMo; }
            set { Load(); if (slowMo == value) return; slowMo = value; Store(KeySlowMo, value); }
        }

        /// <summary>Animated "PERFECT DRIFT!" / "BIG AIR!" / "NEAR MISS!" score pop-ups.</summary>
        public static bool ScorePopups
        {
            get { Load(); return popups; }
            set { Load(); if (popups == value) return; popups = value; Store(KeyPopups, value); }
        }
    }
}

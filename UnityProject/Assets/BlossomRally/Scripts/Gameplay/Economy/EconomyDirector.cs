// EconomyDirector.cs - bootstraps the economy module (one hidden "BR_Economy" object carrying the wallet HUD,
// jobs, job board, showroom, shop and world markers), wires the GameplayHooks screens, pays rally prizes
// (GameEvents.Finished), turns drifts and near misses into small change, tracks distance / play time,
// autosaves, and puts the car back where the player left it on the first drive of a session.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(-50)]      // before GameManager (900): menu release / input guards are settled first
    public class EconomyDirector : MonoBehaviour
    {
        public static EconomyDirector Instance { get; private set; }

        public const float AutosaveInterval = 120f;

        float autosaveT, nearMissQuiet, nearMissCooldown;
        int nearMissCount, nearMissCash;
        bool saveRequested, restoredPosition;
        Vector3 lastCarPos;
        bool haveLastPos;
        ArcadeCar lastCar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            EconomySave.Load();
            var go = new GameObject("BR_Economy");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<EconomyDirector>();
            go.AddComponent<WalletHud>().useGUILayout = false;
            go.AddComponent<JobSystem>().useGUILayout = false;
            go.AddComponent<JobBoard>().useGUILayout = false;
            go.AddComponent<Showroom>().useGUILayout = false;
            go.AddComponent<ShopMenu>().useGUILayout = false;
            go.AddComponent<EconomyWorld>().useGUILayout = false;
            Instance.useGUILayout = false;

            GameplayHooks.OpenShowroom = id => { if (Showroom.Instance != null) Showroom.Instance.Open(id); };
            GameplayHooks.OpenJobBoard = id => { if (JobBoard.Instance != null) JobBoard.Instance.Open(id, false); };
            GameplayHooks.OpenShop = def => { if (ShopMenu.Instance != null) ShopMenu.Instance.Open(def); };

            GameEvents.Finished += OnFinished;
            GameEvents.DriftBanked += OnDrift;
            GameEvents.NearMiss += OnNearMiss;
            WorldHooks.WorldBuilt += w =>
            {
                // GameManager sets the toast itself; this only fills the gap if it doesn't
                if (GameplayHooks.Toast == null) GameplayHooks.Toast = Toast;
            };
        }

        /// <summary>Show a toast through whoever owns it (GameManager).</summary>
        public static void Toast(string s)
        {
            if (GameplayHooks.Toast != null && GameplayHooks.Toast != (System.Action<string>)Toast) { GameplayHooks.Toast(s); return; }
            var gm = GameManager.Instance;
            if (gm != null) gm.Toast(s);
        }

        /// <summary>Save at the end of this frame (coalesces several changes into one write).</summary>
        public static void RequestSave() { if (Instance != null) Instance.saveRequested = true; }

        /// <summary>Pause menu "Save game".</summary>
        public static void SaveFromMenu()
        {
            bool ok = EconomySave.Save();
            UI.Sound(ok ? "MenuSelect" : "MenuBack");
            Toast(ok ? "Game saved" : "Could not save the game");
        }

        static bool EventRunning { get { var sm = StageManager.Instance; return sm != null && sm.Running; } }

        // ------------------------------------------------------------------ earnings
        static int MedalPrize(int medal, StageKind kind)
        {
            switch (medal)
            {
                case 3: return 900;
                case 2: return 600;
                case 1: return 400;
                default: return 150;
            }
        }

        static void OnFinished(int medal, float result)
        {
            var sm = StageManager.Instance;
            int ev = sm != null ? sm.CurrentEvent : -1;
            var defs = StageDef.All;
            if (ev < 0 || ev >= defs.Length) return;
            var def = defs[ev];
            if (def.kind == StageKind.Drift && medal == 0 && result <= 0f) return;
            float regionK = def.region == RegionId.Valley ? 1f : 1.2f;
            int prize = Mathf.RoundToInt(MedalPrize(medal, def.kind) * regionK / 10f) * 10;
            // first time reaching a medal on an event pays the difference again as a bonus
            int paid = EconomySave.MedalPaid(def.key);
            int bonus = 0;
            if (medal > 0 && medal > paid)
            {
                int before = paid > 0 ? Mathf.RoundToInt(MedalPrize(paid, def.kind) * regionK / 10f) * 10 : 0;
                bonus = prize - before;
                EconomySave.SetMedalPaid(def.key, medal);
            }
            string reason = (medal > 0 ? MenuArt.MedalName(medal) + " prize" : "Finisher's purse") + (bonus > 0 ? " + first " + MenuArt.MedalName(medal).ToLowerInvariant() + " bonus" : "");
            EconomySave.Data.rallyEarned += prize + bonus;
            Wallet.Add(prize + bonus, reason);
            RequestSave();
        }

        static void OnDrift(float points, float peak)
        {
            if (EventRunning || Instance == null) return;
            int cash = Mathf.Clamp(Mathf.RoundToInt(points / 50f), 0, 200);
            if (cash < 3) return;
            EconomySave.Data.streetEarned += cash;
            Wallet.Add(cash, points > 3000f ? "Legendary drift" : points > 1200f ? "Great drift" : "Drift");
        }

        static void OnNearMiss(Vector3 p, float clearance)
        {
            var d = Instance;
            if (d == null || EventRunning || d.nearMissCooldown > 0f) return;
            d.nearMissCooldown = 0.45f;
            d.nearMissCount++;
            d.nearMissCash += clearance < 0.6f ? 6 : 4;
            d.nearMissQuiet = 2.2f;
            if (JobSystem.Instance != null) JobSystem.Instance.OnNearMiss();
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            EcoMenus.Tick();
            float dt = Time.unscaledDeltaTime;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.World != null && gm.World.Done) EconomySave.Data.playHours += dt / 3600f;

            if (nearMissCooldown > 0f) nearMissCooldown -= Time.deltaTime;
            if (nearMissCount > 0)
            {
                nearMissQuiet -= Time.deltaTime;
                if (nearMissQuiet <= 0f)
                {
                    EconomySave.Data.streetEarned += nearMissCash;
                    Wallet.Add(nearMissCash, nearMissCount == 1 ? "Near miss" : "Near misses ×" + nearMissCount);
                    nearMissCount = 0; nearMissCash = 0;
                }
            }

            var car = gm.Car;
            if (car != lastCar) { lastCar = car; haveLastPos = false; }
            if (gm.IsDriving && car != null)
            {
                if (!restoredPosition) RestorePosition(gm);
                Vector3 p = car.transform.position;
                if (haveLastPos)
                {
                    float d = (p - lastCarPos).magnitude;
                    if (d < 60f) EconomySave.Data.distanceKm += d * 0.001f;      // bigger jumps are teleports
                }
                lastCarPos = p;
                haveLastPos = true;
                autosaveT += dt;
                if (autosaveT >= AutosaveInterval) { autosaveT = 0f; saveRequested = true; }
            }
            else haveLastPos = false;
        }

        void LateUpdate()
        {
            if (!saveRequested) return;
            saveRequested = false;
            autosaveT = 0f;
            EconomySave.Save();
        }

        /// <summary>First drive of the session: continue near the saved spot (snapped to the nearest road).</summary>
        void RestorePosition(GameManager gm)
        {
            restoredPosition = true;
            var d = EconomySave.Data;
            var w = gm.World;
            if (d == null || !d.hasPos || w == null || EventRunning || PlayerState.OnFoot) return;
            // only when the drive just started at the world spawn (not after an event start or a fast travel)
            if ((gm.Car.transform.position - w.SpawnPosition).sqrMagnitude > 40f * 40f) return;
            Vector3 saved = new Vector3(d.px, d.py, d.pz);
            if ((saved - w.SpawnPosition).sqrMagnitude < 30f * 30f) return;
            Vector3 fwd = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.forward;
            Vector3 p; Quaternion r;
            if (!w.RespawnPoint(saved, fwd, out p, out r)) return;
            RegionRuntime.ShowAll(false);
            gm.TeleportCar(p, r);
            haveLastPos = false;
            var reg = w.Field != null ? w.Field.RegionOf(WorldLayout.At(new Vector2(p.x, p.z))) : null;
            Toast("Welcome back" + (reg != null ? " - " + reg.Name : "") + "   ·   " + Wallet.Format(Wallet.Balance));
        }

        void OnApplicationQuit() { EconomySave.Save(); }
        void OnApplicationPause(bool p) { if (p) EconomySave.Save(); }
    }

    /// <summary>
    /// One economy screen at a time. Opening sets GameplayHooks.MenuOpen and parks the car's controls;
    /// closing releases MenuOpen one frame later so the key that closed the menu (ESC / B) does not also
    /// reach GameManager (pause) or the interaction system.
    /// </summary>
    public static class EcoMenus
    {
        public static object Current { get; private set; }
        public static bool IsOpen { get { return Current != null; } }
        /// <summary>Frame the current screen opened (screens ignore input on that frame).</summary>
        public static int OpenFrame { get; private set; }
        static int releaseFrame = -1;
        static bool othersHeld;
        static ArcadeCar heldCar;

        /// <summary>True when something else owns the screen (pause, settings, map, photo, replay, loading, garage).</summary>
        public static bool Blocked
        {
            get
            {
                var gm = GameManager.Instance;
                if (gm == null || !gm.IsDriving) return true;
                if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return true;
                if (ValleyMap.IsOpen || PhotoAlbum.IsOpen || PhotoMode.BlockingInput || ReplaySystem.BlockingInput) return true;
                return false;
            }
        }

        public static bool TryOpen(object screen)
        {
            if (screen == null) return false;
            if (Current == screen) return true;
            if (Current != null || Blocked) return false;
            // a direct hook call is intentional even while another module holds MenuOpen (e.g. an interior):
            // remember its hold and give it back on close
            if (releaseFrame < 0) othersHeld = GameplayHooks.MenuOpen;
            Current = screen;
            OpenFrame = Time.frameCount;
            releaseFrame = -1;
            GameplayHooks.MenuOpen = true;
            var car = PlayerState.Vehicle;
            heldCar = null;
            if (car != null && !PlayerState.OnFoot && car.controlsEnabled)
            {
                car.controlsEnabled = false;
                heldCar = car;
            }
            return true;
        }

        public static void Close(object screen)
        {
            if (Current != screen || screen == null) return;
            Current = null;
            releaseFrame = Time.frameCount;
            var gm = GameManager.Instance;
            if (heldCar != null && gm != null && gm.Car == heldCar && !PlayerState.OnFoot) heldCar.controlsEnabled = true;
            heldCar = null;
        }

        /// <summary>The player's vehicle was swapped while a menu is open: keep the new one parked too.</summary>
        public static void Rehold()
        {
            if (heldCar == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null) { heldCar = null; return; }
            heldCar = gm.Car;
            heldCar.controlsEnabled = false;
        }

        /// <summary>Screens read input only when this is true (open, not on the opening frame).</summary>
        public static bool InputFor(object screen) { return Current == screen && Time.frameCount != OpenFrame; }

        public static void Tick()
        {
            if (Current == null && releaseFrame >= 0 && Time.frameCount > releaseFrame)
            {
                GameplayHooks.MenuOpen = othersHeld;
                othersHeld = false;
                releaseFrame = -1;
            }
            // the screen went away underneath us (pause forced, scene change)
            if (Current is Object && (Object)Current == null) { Current = null; releaseFrame = Time.frameCount; }
        }
    }
}

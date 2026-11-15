// EconomySave.cs - the player's progress as one JSON file (Application.persistentDataPath/blossom_save.json):
// money, owned vehicles, the selected vehicle, stats (jobs, distance, earnings) and the last position.
// Best times and medals stay in GameSettings (PlayerPrefs). Loaded before the first scene; saved by the
// autosave in EconomyDirector, after purchases / jobs / prizes, on quit, and from the pause menu ("Save game").
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlossomRally
{
    [System.Serializable]
    public class EconomySaveData
    {
        public int version = 1;
        public int money = Wallet.StartingMoney;
        public string[] owned = new string[0];
        public string selected = "";
        // stats
        public int jobsDone, jobsEarned, rallyEarned, streetEarned, spent, vehiclesBought, photosTaken;
        public float distanceKm, playHours;
        public int[] jobsByKind = new int[6];
        // last position (the vehicle)
        public bool hasPos;
        public float px, py, pz, yaw;
        // rally prizes already paid per event ("key=medal") for the first-time medal bonus
        public string[] medals = new string[0];
        public string savedAt = "";
    }

    public static class EconomySave
    {
        public const string FileName = "blossom_save.json";
        public static EconomySaveData Data { get; private set; }
        public static bool Loaded { get; private set; }
        /// <summary>True when something changed since the last write (the autosave writes it).</summary>
        public static bool Dirty;
        /// <summary>Raised after every successful write (HUD "saved" blossom).</summary>
        public static event System.Action Saved;
        public static float LastSaveTime { get; private set; } = -100f;

        static readonly Dictionary<string, int> medalPaid = new Dictionary<string, int>();

        public static string PathName
        {
            get
            {
                try { return Path.Combine(Application.persistentDataPath, FileName); }
                catch (System.Exception) { return null; }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Load();
        }

        public static void Load()
        {
            if (Loaded) return;
            Loaded = true;
            Data = null;
            string p = PathName;
            try
            {
                if (p != null && File.Exists(p)) Data = JsonUtility.FromJson<EconomySaveData>(File.ReadAllText(p));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BlossomRally] save file unreadable, starting fresh: " + e.Message);
                try { if (p != null) File.Copy(p, p + ".broken", true); } catch (System.Exception) { }
                Data = null;
            }
            bool fresh = Data == null;
            if (fresh) Data = new EconomySaveData();
            if (Data.owned == null) Data.owned = new string[0];
            if (Data.medals == null) Data.medals = new string[0];
            if (Data.jobsByKind == null || Data.jobsByKind.Length < 6) Data.jobsByKind = new int[6];
            medalPaid.Clear();
            foreach (var m in Data.medals)
            {
                if (string.IsNullOrEmpty(m)) continue;
                int eq = m.LastIndexOf('=');
                int v;
                if (eq > 0 && int.TryParse(m.Substring(eq + 1), out v)) medalPaid[m.Substring(0, eq)] = v;
            }
            Wallet.Load(fresh ? Wallet.StartingMoney : Data.money, Data.owned);
            Wallet.Changed += (d, r) => Dirty = true;
        }

        /// <summary>Catalogue index of the saved vehicle (if still owned), else 0.</summary>
        public static int StartCarIndex()
        {
            if (Data == null) return 0;
            int i = VehiclePrices.IndexOf(Data.selected);
            return i >= 0 && Wallet.Owns(Data.selected) ? i : 0;
        }

        public static int MedalPaid(string key) { int m; return key != null && medalPaid.TryGetValue(key, out m) ? m : -1; }
        public static void SetMedalPaid(string key, int medal) { if (key == null) return; medalPaid[key] = medal; Dirty = true; }

        /// <summary>Write the save file now. Returns false (and logs) when the disk refuses.</summary>
        public static bool Save()
        {
            if (Data == null) return false;
            Capture();
            string p = PathName;
            if (p == null) return false;
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                string tmpPath = p + ".tmp";
                File.WriteAllText(tmpPath, json);
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                    File.Move(tmpPath, p);
                }
                catch (System.Exception)
                {
                    File.WriteAllText(p, json);
                }
                Dirty = false;
                LastSaveTime = Time.unscaledTime;
                if (Saved != null) Saved();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BlossomRally] could not save: " + e.Message);
                return false;
            }
        }

        static readonly List<string> scratch = new List<string>();

        /// <summary>Copy the live state (wallet, vehicle, position) into Data.</summary>
        static void Capture()
        {
            Data.money = Wallet.Balance;
            scratch.Clear();
            foreach (var id in Wallet.Owned) scratch.Add(id);
            scratch.Sort(System.StringComparer.Ordinal);
            Data.owned = scratch.ToArray();
            scratch.Clear();
            foreach (var kv in medalPaid) scratch.Add(kv.Key + "=" + kv.Value);
            Data.medals = scratch.ToArray();
            var gm = GameManager.Instance;
            var car = gm != null ? gm.Car : null;
            if (car != null && car.spec != null && Wallet.Owns(car.spec.id)) Data.selected = car.spec.id;
            var garage = gm != null ? gm.GetComponent<Garage>() : null;
            if (car != null && (garage == null || !garage.Active))      // not while the car sits on the garage turntable
            {
                Vector3 p = car.transform.position;
                if (p.y > -20f && !float.IsNaN(p.x))
                {
                    Data.hasPos = true;
                    Data.px = p.x; Data.py = p.y; Data.pz = p.z;
                    Data.yaw = car.transform.eulerAngles.y;
                }
            }
            Data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Start over: fresh wallet and stats (keeps settings and best times).</summary>
        public static void ResetProgress()
        {
            Data = new EconomySaveData();
            medalPaid.Clear();
            Wallet.Load(Wallet.StartingMoney, null);
            Dirty = true;
        }
    }
}

// JuiceDirector.cs - wires the "juice" layer in without touching GameManager:
//  * registers on WorldHooks (world built -> director + shared FX, car spawned -> CarJuice / CarJuiceAudio)
//  * turns GameEvents into moments: score pop-ups, slow motion, crowd cheers and the finish-line moment
//      DriftBanked  -> "PERFECT DRIFT!" (>= 1500 pts, or >= 40 deg held ~1 s) + slow-mo; cheer when > 800
//      TakeOff / Landed -> "BIG AIR!  1.4 s" (> 0.8 s) + slow-mo while still airborne + cheer
//      NearMiss     -> "NEAR MISS!" (+ chain count) + cheer          (raised by CarJuice)
//      Checkpoint   -> "CLEAN SECTION" (no hits / resets since the last gate), "SPEED TRAP 152 km/h"
//      Finished     -> FinishMoment (slow-mo, camera orbit, confetti, medal stamp)
using System.Globalization;
using UnityEngine;

namespace BlossomRally
{
    public class JuiceDirector : MonoBehaviour
    {
        public static JuiceDirector Instance { get; private set; }

        static bool registered;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly Color PopPink = new Color(1f, 0.72f, 0.82f, 1f);
        public static readonly Color PopGold = new Color(1f, 0.84f, 0.42f, 1f);
        public static readonly Color PopMint = new Color(0.66f, 1f, 0.8f, 1f);
        public static readonly Color PopWhite = new Color(1f, 0.97f, 0.96f, 1f);

        FinishMoment finish;
        AudioSource voice;

        // big air
        bool airborne, airSlowDone;
        float takeOffTime;
        // perfect drift: time held at >= 40 degrees in the current chain
        float held40, notDrifting;
        // near miss chain
        int nearChain;
        float nearChainTimer;
        // clean sections / speed traps
        bool sectionClean = true, wasRunning;
        float lastImpactTime = -10f;
        Vector3 lastCarPos;
        bool hasLastPos;
        float peakRun, shownTop, topSpeedCooldown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += OnWorldBuilt;
            WorldHooks.CarSpawned += OnCarSpawned;
        }

        static void OnWorldBuilt(WorldBuilder w)
        {
            JuiceWorld.Build();
            Ensure();
        }

        static void OnCarSpawned(ArcadeCar car)
        {
            if (car == null) return;
            Ensure();
            if (car.GetComponent<CarJuice>() == null) car.gameObject.AddComponent<CarJuice>();
            if (car.GetComponent<CarJuiceAudio>() == null) car.gameObject.AddComponent<CarJuiceAudio>();
        }

        public static JuiceDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BR_Juice");
            return go.AddComponent<JuiceDirector>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            JuiceAudio.Ensure();
            gameObject.AddComponent<SlowMo>();
            gameObject.AddComponent<ScorePopups>();
            gameObject.AddComponent<JuiceFX>();
            finish = gameObject.AddComponent<FinishMoment>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
        }

        void OnEnable()
        {
            GameEvents.TakeOff += OnTakeOff;
            GameEvents.Landed += OnLanded;
            GameEvents.DriftBanked += OnDriftBanked;
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.Checkpoint += OnCheckpoint;
            GameEvents.Finished += OnFinished;
            GameEvents.Impact += OnImpact;
        }

        void OnDisable()
        {
            GameEvents.TakeOff -= OnTakeOff;
            GameEvents.Landed -= OnLanded;
            GameEvents.DriftBanked -= OnDriftBanked;
            GameEvents.NearMiss -= OnNearMiss;
            GameEvents.Checkpoint -= OnCheckpoint;
            GameEvents.Finished -= OnFinished;
            GameEvents.Impact -= OnImpact;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static ArcadeCar PlayerCar
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.IsDriving ? gm.Car : null;
            }
        }

        void Accent(AudioClip c, float vol)
        {
            if (c == null || voice == null) return;
            voice.PlayOneShot(c, Mathf.Clamp01(vol * GameSettings.SfxVolume));
        }

        void SlowMoment(float hold, float scale)
        {
            bool was = SlowMo.Active;
            if (SlowMo.Trigger(hold, scale) && !was) Accent(JuiceAudio.SlowMoIn, 0.5f);
        }

        // ------------------------------------------------------------------ events
        void OnTakeOff(Vector3 p)
        {
            var car = PlayerCar;
            if (car == null || (p - car.transform.position).sqrMagnitude > 100f) return;
            airborne = true;
            airSlowDone = false;
            takeOffTime = Time.time;
        }

        void OnLanded(float air, float vy)
        {
            var car = PlayerCar;
            bool wasAir = airborne;
            airborne = false;
            if (car == null || !wasAir || air <= 0.8f) return;
            ScorePopups.Show("BIG AIR!", air.ToString("0.0", Inv) + " s", PopGold, Mathf.Clamp01(0.7f + (air - 0.8f) * 0.4f));
            GameEvents.RaiseCheer(car.transform.position, Mathf.Clamp01(0.55f + (air - 0.8f) * 0.35f));
            if (!airSlowDone) SlowMoment(0.4f, 0.4f);   // missed the mid-air trigger (very short hang): a beat on landing
            airSlowDone = true;
        }

        void OnDriftBanked(float points, float peak)
        {
            var car = PlayerCar;
            if (car == null) return;
            bool perfect = points >= 1500f || (peak >= 40f && held40 >= 0.9f);
            held40 = 0f;
            if (points > 800f) GameEvents.RaiseCheer(car.transform.position, Mathf.Clamp01(0.35f + points / 4000f));
            if (!perfect) return;
            ScorePopups.Show("PERFECT DRIFT!", "+" + Mathf.RoundToInt(points).ToString("N0", Inv) + "   ·   " + Mathf.RoundToInt(peak) + "°",
                             PopPink, Mathf.Clamp01(0.8f + points / 6000f));
            SlowMoment(0.5f, 0.35f);
        }

        void OnNearMiss(Vector3 p, float clearance)
        {
            var car = PlayerCar;
            if (car == null) return;
            nearChain = nearChainTimer > 0f ? nearChain + 1 : 1;
            nearChainTimer = 3f;
            string head = nearChain > 1 ? "NEAR MISS  x" + nearChain : "NEAR MISS!";
            ScorePopups.Show(head, clearance.ToString("0.0", Inv) + " m", PopWhite, 0.6f + 0.1f * Mathf.Min(nearChain, 4));
            GameEvents.RaiseCheer(p, Mathf.Clamp01(0.4f + (1.2f - clearance) * 0.35f + 0.08f * nearChain));
            Accent(JuiceAudio.NearMiss, 0.35f);
        }

        void OnImpact(float speed, Vector3 p)
        {
            if (speed > 6f) { sectionClean = false; lastImpactTime = Time.time; }
        }

        void OnCheckpoint(int index, float delta)
        {
            var car = PlayerCar;
            if (car == null) return;
            if (sectionClean && index > 0)
                ScorePopups.Show("CLEAN SECTION", float.IsNaN(delta) ? null : (delta < 0f ? "-" : "+") + Mathf.Abs(delta).ToString("0.00", Inv) + " s",
                                 PopMint, 0.55f);
            float kmh = car.SpeedKmh;
            if (kmh >= 100f)
                ScorePopups.Show("SPEED TRAP", Mathf.RoundToInt(kmh) + " km/h", PopGold, 0.5f);
            sectionClean = true;
        }

        void OnFinished(int medal, float result)
        {
            if (finish != null) finish.Begin(medal);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (nearChainTimer > 0f) nearChainTimer -= dt;
            if (topSpeedCooldown > 0f) topSpeedCooldown -= dt;
            var car = PlayerCar;
            var sm = StageManager.Instance;
            bool running = sm != null && sm.Running;
            if (running && !wasRunning) { sectionClean = true; ScorePopups.Clear(); }
            wasRunning = running;
            if (car == null) { hasLastPos = false; airborne = false; return; }

            // resets / teleports count as an unclean section
            Vector3 cp = car.transform.position;
            if (hasLastPos && (cp - lastCarPos).sqrMagnitude > 64f) { sectionClean = false; airborne = false; held40 = 0f; }
            lastCarPos = cp;
            hasLastPos = true;

            // big air: slow motion kicks in mid-air once the hang time passes 0.8 s
            if (airborne)
            {
                float hang = Time.time - takeOffTime;
                if (hang > 6f && car.Grounded) airborne = false;                   // a Landed event was missed
                else if (!airSlowDone && hang > 0.8f && !car.Grounded)
                {
                    airSlowDone = true;
                    SlowMoment(0.45f, 0.4f);
                }
            }

            // drift angle held >= 40 deg during the current chain
            bool drifting = car.IsDrifting || Mathf.Abs(car.SlipAngle) > 12f;
            float ang = Mathf.Max(car.IsDrifting ? Mathf.Abs(car.DriftAngle) : 0f, Mathf.Abs(car.SlipAngle));
            if (drifting && car.Grounded && ang >= 40f && ang < 100f) held40 += dt;
            if (drifting) notDrifting = 0f;
            else if ((notDrifting += dt) > 1.5f) held40 = 0f;

            // free-roam top speed record (session), shown sparingly
            // (shown once the car stops accelerating, so the number is the real peak)
            float kmh = car.SpeedKmh;
            if (running) peakRun = 0f;
            else if (kmh > peakRun) peakRun = kmh;
            else if (peakRun - kmh > 4f)
            {
                if (peakRun > 150f && peakRun > shownTop + 10f && topSpeedCooldown <= 0f)
                {
                    shownTop = peakRun;
                    topSpeedCooldown = 6f;
                    ScorePopups.Show("TOP SPEED", Mathf.RoundToInt(peakRun) + " km/h", PopGold, 0.45f);
                }
                peakRun = kmh;
            }
        }

        /// <summary>Seconds since the last real impact (CarJuice uses it to veto near misses).</summary>
        public float SinceImpact { get { return Time.time - lastImpactTime; } }
    }
}

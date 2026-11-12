// StageManager.cs - Blossom Rally events: time-attack stages with checkpoint gates, splits,
// medals and a best-run ghost, plus the Pagoda Drift Zone challenge. Draws its own IMGUI HUD
// (timer, checkpoints, splits, countdown, wrong way, results) above the base HUD.
//
// GameManager adds this component once. Hooks:
//   StartEvent(i) from a menu, HandleRespawn() on the reset key, OnSound -> Sfx, OnToast -> toast,
//   OnExitToFreeRoam when the player leaves the results screen.
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlossomRally
{
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        /// <summary>Sound cue by SfxId name: Countdown, CountdownGo, Checkpoint, CheckpointBest, Finish,
        /// MedalBronze, MedalSilver, MedalGold, MenuSelect, MenuBack.</summary>
        public static System.Action<string> OnSound;
        /// <summary>Short messages meant for the base HUD toast (e.g. "Event cancelled").</summary>
        public static System.Action<string> OnToast;
        /// <summary>Invoked after the player leaves the results screen (Enter / A / Esc).</summary>
        public System.Action OnExitToFreeRoam;

        enum Phase { Idle, Countdown, Running, Results }

        // ---- tuning ----
        const float CountdownTime = 3.8f;       // 0.8 s settle + 3-2-1
        const float RespawnPenalty = 2f;
        const float WrongWayDelay = 2f;
        const float DriftFactor = 0.06f;        // same scale as the free-roam drift score
        const float ComboStep = 2.5f;
        const float ChainBreak = 0.8f;
        const float CrashDrop = 25f;            // km/h lost within 0.2 s = hard hit

        Phase phase = Phase.Idle;
        int eventIndex = -1;
        StageDef def;
        StageCourse course;
        ArcadeCar car;
        string carId = "";

        float countdown, runTime, penalty, timeLimit, bestRef;
        int lastCount, nextGate, lastPassed;
        float[] splits, bestSplits;
        Vector3 prevPos;
        int nearIdx;
        float nearDist, wrongTimer, goFlash;

        string msg = "";
        float msgTimer;
        Color msgColor = Color.white;
        float splitDelta, splitTimer;

        // results
        bool finished, newBest, ghostSaved;
        int medal;
        float result, prevBest, resultsTimer;
        bool medalSound;

        // drift
        float score, chainPts, chainHold, breakTimer, combo = 1f, chainFlash, lastBank;
        bool chainActive;
        readonly List<Vector2> speedHist = new List<Vector2>();

        // ghost
        readonly GhostRecorder recorder = new GhostRecorder();
        GhostCar ghost;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        string[] names;
        readonly Dictionary<int, float> lengthCache = new Dictionary<int, float>();

        // ------------------------------------------------------------------ public API
        public int EventCount { get { return StageDef.All.Length; } }

        public string[] EventNames
        {
            get
            {
                if (names == null || names.Length != StageDef.All.Length)
                {
                    names = new string[StageDef.All.Length];
                    for (int i = 0; i < names.Length; i++) names[i] = StageDef.All[i].name;
                }
                return names;
            }
        }

        /// <summary>True from the countdown until the results screen is dismissed.</summary>
        public bool Running { get { return phase != Phase.Idle; } }
        public bool ShowingResults { get { return phase == Phase.Results; } }
        /// <summary>The number the countdown is showing (3, 2, 1) - read by the announcer.</summary>
        public int CountdownNumber { get { return lastCount; } }
        /// <summary>True while the "WRONG WAY" warning is on screen.</summary>
        public bool WrongWay { get { return phase == Phase.Running && wrongTimer > WrongWayDelay; } }
        /// <summary>The last finished run beat the saved best.</summary>
        public bool NewBest { get { return newBest; } }
        /// <summary>Index of the running event, -1 when none.</summary>
        public int CurrentEvent { get { return phase != Phase.Idle ? eventIndex : -1; } }

        public string EventDescription(int i)
        {
            if (i < 0 || i >= StageDef.All.Length) return "";
            var d = StageDef.All[i];
            float len = StageLength(i);
            string where = "";
            var gm = GameManager.Instance;
            if (len > 0f)
            {
                where = (len / 1000f).ToString("0.00", Inv) + " km";
                if (gm != null && gm.World != null && d.road < gm.World.roads.Count)
                    where += " " + gm.World.roads[d.road].surface.ToString().ToLowerInvariant();
            }
            string k = RecordKey(d);
            float best = GameSettings.BestTime(k);
            int m = GameSettings.BestMedal(k);
            string rec;
            if (d.kind == StageKind.TimeAttack)
            {
                rec = best > 0f ? "Best " + FormatTime(best) : "No time set";
                if (len > 0f) rec += "  ·  Gold " + FormatTime(d.GoldTime(len));
            }
            else
            {
                rec = best > 0f ? "Best " + FormatPts(best) + " pts" : "No score set";
                rec += "  ·  Gold " + FormatPts(d.goldPts) + " pts";
                if (len > 0f) rec += "  ·  " + Mathf.RoundToInt(d.TimeLimit(len)) + " s limit";
            }
            if (m > 0) rec += "  ·  " + MedalName(m).ToUpperInvariant();
            return d.blurb + (where.Length > 0 ? "  ·  " + where : "") + "\n" + rec;
        }

        public void StartEvent(int index)
        {
            if (index < 0 || index >= StageDef.All.Length) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.World == null || !gm.World.Done || gm.Car == null || gm.Car.Body == null)
            {
                Toast("The valley is still loading");
                return;
            }
            Cleanup(false);
            def = StageDef.All[index];
            course = StageCourse.Build(def, gm.World, true);
            if (course == null)
            {
                eventIndex = -1;
                Toast(def.name + " is not available");
                return;
            }
            eventIndex = index;
            lengthCache[index] = course.length;
            car = gm.Car;
            carId = car.spec != null ? car.spec.id : "car";
            splits = new float[course.gates.Count];
            bestSplits = def.kind == StageKind.TimeAttack ? LoadSplits(def, course.gates.Count) : null;
            timeLimit = def.kind == StageKind.Drift ? def.TimeLimit(course.length) : 0f;
            bestRef = GameSettings.BestTime(RecordKey(def));
            if (def.kind == StageKind.TimeAttack) ghost = GhostCar.Create(GhostStore.Load(def.key, carId));

            gm.TeleportCar(course.spawnPos, course.spawnRot);
            car.controlsEnabled = false;
            phase = Phase.Countdown;
            countdown = CountdownTime;
            lastCount = 4;
            runTime = penalty = 0f;
            nextGate = 1;
            lastPassed = 0;
            prevPos = course.spawnPos;
            nearIdx = course.Nearest(prevPos, 0, 0, out nearDist);
            wrongTimer = goFlash = msgTimer = splitTimer = 0f;
            finished = newBest = ghostSaved = medalSound = false;
            medal = 0;
            ResetDrift();
            recorder.Begin(car);
            UpdateGates(0f);
        }

        public void Restart()
        {
            if (eventIndex >= 0) StartEvent(eventIndex);
        }

        /// <summary>Stops the event and removes gates / ghost. Restores car controls when back in free roam.</summary>
        public void Abort()
        {
            Cleanup(true);
        }

        /// <summary>Reset key: during a run, back to the last passed checkpoint with a +2 s penalty.
        /// Returns true when the event handled (or swallowed) the reset.</summary>
        public bool HandleRespawn()
        {
            if (phase == Phase.Idle) return false;
            if (phase != Phase.Running) return true;
            var gm = GameManager.Instance;
            if (gm == null || car == null || course == null) return true;
            Vector3 pos;
            Quaternion rot;
            if (lastPassed <= 0) { pos = course.spawnPos; rot = course.spawnRot; }
            else course.PoseAt(course.gates[lastPassed].sample, out pos, out rot);
            gm.TeleportCar(pos, rot);
            runTime += RespawnPenalty;
            penalty += RespawnPenalty;
            prevPos = pos;
            nearIdx = course.Nearest(pos, 0, 0, out nearDist);
            wrongTimer = 0f;
            if (def.kind == StageKind.Drift)
            {
                speedHist.Clear();
                if (chainActive || chainPts > 0f) LoseChain(null);
            }
            Message("+" + RespawnPenalty.ToString("0.0", Inv) + " s  penalty", new Color(1f, 0.45f, 0.45f));
            return true;
        }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            useGUILayout = false;      // plain GUI calls only: skip the layout pass
        }

        void OnDestroy()
        {
            Cleanup(false);
            if (Instance == this) Instance = null;
        }

        void Cleanup(bool restoreControls)
        {
            if (ghost != null) ghost.Destroy();
            ghost = null;
            if (course != null) course.Destroy();
            course = null;
            if (restoreControls && phase != Phase.Idle && car != null)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Car == car && (gm.IsDriving || Time.timeScale < 0.01f)) car.controlsEnabled = true;
            }
            phase = Phase.Idle;
            speedHist.Clear();
        }

        void Cancel(string why)
        {
            Abort();
            Toast(why);
        }

        void ExitToFreeRoam()
        {
            Abort();
            if (OnExitToFreeRoam != null) OnExitToFreeRoam();
        }

        void Update()
        {
            if (phase == Phase.Idle) return;
            var gm = GameManager.Instance;
            if (gm == null || car == null || gm.Car != car || course == null) { Cancel("Event cancelled"); return; }
            if (!gm.IsDriving)
            {
                // paused: everything freezes; car select / loading: the event ends
                if (Time.timeScale > 0.01f) Cancel("Event cancelled");
                return;
            }
            float dt = Time.deltaTime;
            if (msgTimer > 0f) msgTimer -= dt;
            if (splitTimer > 0f) splitTimer -= dt;
            if (goFlash > 0f) goFlash -= dt;
            if (chainFlash > 0f) chainFlash -= dt;

            switch (phase)
            {
                case Phase.Countdown: UpdateCountdown(dt); break;
                case Phase.Running: UpdateRun(dt); break;
                case Phase.Results: UpdateResults(dt); break;
            }
        }

        void FixedUpdate()
        {
            if (phase == Phase.Idle || car == null || car.Body == null || Time.timeScale < 0.01f) return;
            var rb = car.Body;
            if (phase == Phase.Countdown)
            {
                // hold the car on the line (it may still settle on its springs)
                Vector3 v = rb.GetVelocity();
                rb.SetVelocity(new Vector3(0f, v.y, 0f));
                Vector3 av = rb.angularVelocity;
                rb.angularVelocity = new Vector3(av.x, 0f, av.z);
            }
            else if (phase == Phase.Results)
            {
                // roll gently to a stop
                Vector3 v = rb.GetVelocity();
                float k = 1f - Mathf.Min(1f, 1.2f * Time.fixedDeltaTime);
                rb.SetVelocity(new Vector3(v.x * k, v.y, v.z * k));
            }
        }

        void LateUpdate()
        {
            if (phase == Phase.Idle || course == null) return;
            UpdateGates(Mathf.Sin(Time.time * 4.5f));
            if (ghost != null && car != null) ghost.Show(phase == Phase.Countdown ? 0f : runTime, car.transform.position);
        }

        // ------------------------------------------------------------------ phases
        void UpdateCountdown(float dt)
        {
            car.controlsEnabled = false;
            countdown -= dt;
            int c = Mathf.CeilToInt(countdown);
            if (c < lastCount && c >= 1 && c <= 3)
            {
                lastCount = c;
                Sound("Countdown");
                InputReader.Rumble(0.08f, 0.12f, 0.08f);
            }
            if (countdown <= 0f) Go();
        }

        void Go()
        {
            phase = Phase.Running;
            car.controlsEnabled = true;
            runTime = 0f;
            goFlash = 1f;
            prevPos = car.transform.position;
            recorder.Begin(car);
            Sound("CountdownGo");
            InputReader.Rumble(0.45f, 0.55f, 0.25f);
        }

        void UpdateRun(float dt)
        {
            runTime += dt;
            Vector3 p = car.transform.position;
            if (def.kind == StageKind.TimeAttack) recorder.Tick(car, runTime);

            // where are we on the road, and which way are we going?
            nearIdx = course.Nearest(p, nearIdx, 25, out nearDist);
            if (nearDist > 40f) nearIdx = course.Nearest(p, 0, 0, out nearDist);
            var rs = course.samples[nearIdx];
            Vector3 v = car.Body != null ? car.Body.GetVelocity() : Vector3.zero;
            float along = v.x * rs.tangent.x + v.z * rs.tangent.y;
            if (along < -3f) wrongTimer += dt;
            else if (along > 1f) wrongTimer = 0f;

            if (dt > 0f && Crossed(course.gates[nextGate], prevPos, p))
            {
                prevPos = p;
                PassGate();
                if (phase != Phase.Running) return;
            }
            prevPos = p;

            if (def.kind == StageKind.Drift)
            {
                UpdateDrift(dt, rs);
                if (runTime >= timeLimit) Finish(true);
            }
        }

        static bool Crossed(StageGate g, Vector3 a, Vector3 b)
        {
            Vector3 ra = a - g.centre, rb = b - g.centre;
            float sa = Vector3.Dot(ra, g.fwd), sb = Vector3.Dot(rb, g.fwd);
            if (!(sa < 0f && sb >= 0f)) return false;
            float t = sa / (sa - sb);
            Vector3 hit = Vector3.Lerp(ra, rb, t);
            return Mathf.Abs(Vector3.Dot(hit, g.right)) < g.halfWidth + 3f && Mathf.Abs(hit.y) < 6f;
        }

        void PassGate()
        {
            int i = nextGate;
            splits[i] = runTime;
            lastPassed = i;
            bool final = i == course.gates.Count - 1;
            bool hasSplit = bestSplits != null && bestSplits.Length == splits.Length && bestSplits[i] > 0f;
            if (hasSplit)
            {
                splitDelta = runTime - bestSplits[i];
                splitTimer = 4f;
            }
            InputReader.Rumble(0.25f, 0.4f, 0.15f);
            if (final) { Finish(false); return; }
            Sound(hasSplit && splitDelta < 0f ? "CheckpointBest" : "Checkpoint");
            GameEvents.RaiseCheckpoint(nextGate, hasSplit ? splitDelta : float.NaN);
            Message("CHECKPOINT  " + i + " / " + course.Checkpoints, new Color(1f, 0.97f, 0.96f));
            nextGate++;
        }

        void Finish(bool timeUp)
        {
            phase = Phase.Results;
            resultsTimer = 0f;
            medalSound = false;
            car.controlsEnabled = false;
            wrongTimer = 0f;
            Sound("Finish");
            InputReader.Rumble(0.5f, 0.6f, 0.35f);
            string k = RecordKey(def);
            prevBest = GameSettings.BestTime(k);
            if (def.kind == StageKind.TimeAttack)
            {
                finished = true;
                result = runTime;
                newBest = prevBest <= 0f || result < prevBest;
                medal = def.MedalForTime(result, course.length);
                if (newBest)
                {
                    GameSettings.SetBestTime(k, result);
                    SaveSplits(def, splits);
                }
                var existing = GhostStore.Load(def.key, carId);
                if (existing == null || result < existing.time)
                {
                    GhostStore.Save(recorder.Finish(car, def.key, carId, result));
                    ghostSaved = true;
                }
            }
            else
            {
                if (!timeUp) BankChain();
                finished = !timeUp;
                result = Mathf.Round(score);
                medal = finished ? def.MedalForPoints(result) : 0;
                newBest = finished && result > prevBest;
                if (newBest) GameSettings.SetBestTime(k, result);
            }
            if (medal > GameSettings.BestMedal(k)) GameSettings.SetBestMedal(k, medal);
            if (ghost != null) { ghost.Destroy(); ghost = null; }
        }

        void UpdateResults(float dt)
        {
            resultsTimer += dt;
            if (!medalSound && resultsTimer > 0.7f)
            {
                medalSound = true;
                if (medal > 0) Sound(medal == 3 ? "MedalGold" : medal == 2 ? "MedalSilver" : "MedalBronze");
                GameEvents.RaiseFinished(medal, result);
            }
            if (resultsTimer < 0.8f) return;      // don't let a held handbrake / throttle skip the screen
            if (InputReader.Down(KeyCode.R) || InputReader.ActionDown(GameAction.Reset)) { Sound("MenuSelect"); Restart(); return; }
            if (InputReader.Down(KeyCode.Return) || InputReader.Down(KeyCode.KeypadEnter) || GamepadBridge.ButtonDown(PadButton.South))
            {
                Sound("MenuSelect");
                ExitToFreeRoam();
                return;
            }
            if (InputReader.Down(KeyCode.Escape) || InputReader.Down(KeyCode.Backspace) || GamepadBridge.ButtonDown(PadButton.East))
            {
                Sound("MenuBack");
                ExitToFreeRoam();
            }
        }

        // ------------------------------------------------------------------ drift scoring
        void ResetDrift()
        {
            score = chainPts = chainHold = breakTimer = chainFlash = lastBank = 0f;
            combo = 1f;
            chainActive = false;
            speedHist.Clear();
        }

        void UpdateDrift(float dt, RoadSample rs)
        {
            if (dt <= 0f) return;
            // hard hit: a sudden loss of more than 25 km/h within 0.2 s
            float spd = car.SpeedKmh;
            speedHist.Add(new Vector2(runTime, spd));
            while (speedHist.Count > 0 && speedHist[0].x < runTime - 0.2f) speedHist.RemoveAt(0);
            float maxS = 0f;
            foreach (var h in speedHist) maxS = Mathf.Max(maxS, h.y);
            if (maxS - spd > CrashDrop)
            {
                speedHist.Clear();
                if (chainActive || chainPts > 0f) LoseChain("CRASH  -  chain lost");
            }

            bool onRoad = nearDist < rs.halfWidth + 6f;
            bool active = car.IsDrifting && car.Grounded && spd > 15f && onRoad;
            if (active)
            {
                chainActive = true;
                breakTimer = 0f;
                chainHold += dt;
                combo = Mathf.Min(8f, 1f + Mathf.Floor(chainHold / ComboStep));
                float angle = Mathf.Clamp(Mathf.Abs(car.DriftAngle), 0f, 70f);
                chainPts += angle * spd * DriftFactor * dt * combo;
            }
            else if (chainActive)
            {
                breakTimer += dt;
                if (breakTimer > ChainBreak) BankChain();
            }
        }

        void BankChain()
        {
            if (chainPts >= 1f)
            {
                score += chainPts;
                lastBank = chainPts;
                chainFlash = 2f;
            }
            chainPts = chainHold = breakTimer = 0f;
            combo = 1f;
            chainActive = false;
        }

        void LoseChain(string why)
        {
            chainPts = chainHold = breakTimer = 0f;
            combo = 1f;
            chainActive = false;
            if (why != null) Message(why, new Color(1f, 0.45f, 0.45f));
        }

        // ------------------------------------------------------------------ gates
        void UpdateGates(float pulse)
        {
            if (course == null) return;
            int last = course.gates.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                var g = course.gates[i];
                if (g.visual == null || (course.closed && i == last)) continue;
                int state;
                Texture2D tex = g.texture;
                if (i == 0)
                {
                    state = GateVisual.Dim;
                    if (course.closed && (nextGate == last || phase == Phase.Results))
                    {
                        tex = course.gates[last].texture;
                        if (phase == Phase.Running) state = GateVisual.Next;
                    }
                }
                else if (phase == Phase.Results) state = i == last ? GateVisual.Dim : GateVisual.Hidden;
                else if (i < nextGate) state = GateVisual.Hidden;
                else if (i == nextGate) state = GateVisual.Next;
                else state = GateVisual.Dim;
                g.visual.Apply(state, tex, pulse);
            }
        }

        // ------------------------------------------------------------------ records
        static string RecordKey(StageDef d) { return "Stage_" + d.key; }

        float StageLength(int i)
        {
            float len;
            if (lengthCache.TryGetValue(i, out len)) return len;
            var gm = GameManager.Instance;
            if (gm == null || gm.World == null || !gm.World.Done) return -1f;
            var c = StageCourse.Build(StageDef.All[i], gm.World, false);
            len = c != null ? c.length : -1f;
            lengthCache[i] = len;
            return len;
        }

        static float[] LoadSplits(StageDef d, int count)
        {
            try
            {
                string s = PlayerPrefs.GetString("BR_Splits_" + d.key, "");
                if (string.IsNullOrEmpty(s)) return null;
                var parts = s.Split(';');
                if (parts.Length != count) return null;
                var o = new float[count];
                for (int i = 0; i < count; i++)
                    if (!float.TryParse(parts[i], NumberStyles.Float, Inv, out o[i])) return null;
                return o;
            }
            catch (System.Exception) { return null; }
        }

        static void SaveSplits(StageDef d, float[] sp)
        {
            try
            {
                var parts = new string[sp.Length];
                for (int i = 0; i < sp.Length; i++) parts[i] = sp[i].ToString("0.000", Inv);
                PlayerPrefs.SetString("BR_Splits_" + d.key, string.Join(";", parts));
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }
        }

        // ------------------------------------------------------------------ helpers
        static void Sound(string id)
        {
            if (OnSound == null) return;
            try { OnSound(id); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        static void Toast(string s)
        {
            if (OnToast == null) return;
            try { OnToast(s); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void Message(string s, Color c)
        {
            msg = s;
            msgColor = c;
            msgTimer = 2.2f;
        }

        public static string FormatTime(float t)
        {
            int cs = Mathf.FloorToInt(Mathf.Max(0f, t) * 100f + 0.0001f);
            int m = cs / 6000, s = (cs / 100) % 60, c = cs % 100;
            return m.ToString(Inv) + ":" + s.ToString("00", Inv) + "." + c.ToString("00", Inv);
        }

        static string FormatDelta(float d)
        {
            return (d < 0f ? "-" : "+") + Mathf.Abs(d).ToString("0.00", Inv);
        }

        static string FormatPts(float p)
        {
            return Mathf.RoundToInt(p).ToString("N0", Inv);
        }

        static string MedalName(int m)
        {
            return m == 3 ? "Gold" : m == 2 ? "Silver" : m == 1 ? "Bronze" : "None";
        }

        static readonly Color White = new Color(1f, 0.97f, 0.96f, 1f);
        static readonly Color Pink = new Color(1f, 0.72f, 0.82f, 1f);
        static readonly Color Good = new Color(0.55f, 1f, 0.6f, 1f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.45f, 1f);
        static readonly Color GoldCol = new Color(1f, 0.82f, 0.32f, 1f);
        static readonly Color SilverCol = new Color(0.86f, 0.89f, 0.95f, 1f);
        static readonly Color BronzeCol = new Color(0.88f, 0.58f, 0.38f, 1f);

        static Color MedalColor(int m)
        {
            return m == 3 ? GoldCol : m == 2 ? SilverCol : m == 1 ? BronzeCol : new Color(0.7f, 0.62f, 0.66f, 1f);
        }

        // ------------------------------------------------------------------ HUD
        GUIStyle sTimer, sHuge, sTitle, sTitleL, sMid, sMidL, sMidR, sSmall, sSmallC, sSmallR, sCount;

        void Styles()
        {
            if (sTimer != null) return;
            sTimer = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sHuge = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sMid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            sSmallC = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            sSmallR = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight };
            sMidL = new GUIStyle(sMid) { alignment = TextAnchor.MiddleLeft };
            sMidR = new GUIStyle(sMid) { alignment = TextAnchor.MiddleRight };
            sTitleL = new GUIStyle(sTitle) { alignment = TextAnchor.MiddleLeft };
            sCount = new GUIStyle(GUI.skin.label) { fontSize = 132, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        static void Label(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.08f, 0.12f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        static void Panel(Rect r, float alpha)
        {
            Fill(r, new Color(0.20f, 0.10f, 0.16f, alpha));
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (phase == Phase.Idle || course == null || GameManager.HideHud) return;
            GUI.depth = -10;
            Styles();
            float W = Screen.width, H = Screen.height;

            if (phase != Phase.Results) DrawTimerPanel(W);
            if (def.kind == StageKind.Drift && phase != Phase.Results) DrawDriftPanel(W);

            if (phase == Phase.Countdown)
            {
                Panel(new Rect(0, H * 0.18f, W, 92), 0.35f);
                Label(new Rect(0, H * 0.18f + 6, W, 46), def.name.ToUpperInvariant(), sTitle, Pink);
                Label(new Rect(0, H * 0.18f + 54, W, 28), def.blurb, sSmallC, White);
                if (countdown <= 3f)
                {
                    int c = Mathf.Max(1, Mathf.CeilToInt(countdown));
                    float frac = countdown - Mathf.Floor(countdown);
                    if (frac <= 0f) frac = 1f;
                    Label(new Rect(0, H * 0.36f, W, 170), c.ToString(Inv), sCount, new Color(White.r, White.g, White.b, Mathf.Clamp01(0.35f + frac)));
                }
            }
            else if (phase == Phase.Running && goFlash > 0f)
            {
                Label(new Rect(0, H * 0.36f, W, 170), "GO!", sCount, new Color(Pink.r, Pink.g, Pink.b, Mathf.Clamp01(goFlash * 1.5f)));
            }

            if (phase == Phase.Running && wrongTimer > WrongWayDelay)
            {
                float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);
                Panel(new Rect(W * 0.5f - 210, H * 0.36f - 6, 420, 92), 0.55f);
                Label(new Rect(0, H * 0.36f, W, 54), "WRONG WAY", sTitle, new Color(Bad.r, Bad.g, Bad.b, a));
                Label(new Rect(0, H * 0.36f + 50, W, 26), "turn around  ·  R = back to checkpoint (+2 s)", sSmallC, White);
            }

            if (msgTimer > 0f && phase != Phase.Results)
                Label(new Rect(0, H * 0.30f, W, 34), msg, sMid, new Color(msgColor.r, msgColor.g, msgColor.b, Mathf.Clamp01(msgTimer * 1.5f)));

            if (phase == Phase.Results) DrawResults(W, H);
        }

        readonly CachedText cTimer = new CachedText(), cGates = new CachedText(), cPenalty = new CachedText(), cSplit = new CachedText(),
                            cBest = new CachedText(), cGhost = new CachedText(), cScore = new CachedText(), cChain = new CachedText(),
                            cBank = new CachedText(), cNext = new CachedText();
        readonly float[] marks = new float[3];
        string cName = "", cNameFor;

        void DrawTimerPanel(float W)
        {
            float pw = 440f, ph = 66f, px = (W - pw) * 0.5f, py = 10f;
            Panel(new Rect(px, py, pw, ph), 0.6f);
            bool drift = def.kind == StageKind.Drift;
            float shown = drift ? Mathf.Max(0f, timeLimit - runTime) : runTime;
            if (phase == Phase.Countdown) shown = drift ? timeLimit : 0f;
            Color tc = White;
            if (drift && shown < 10f && phase == Phase.Running) tc = Color.Lerp(White, Bad, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            // HUD strings only rebuilt when the shown value changes (the timer: once per centisecond)
            if (cTimer.Changed(Mathf.FloorToInt(Mathf.Max(0f, shown) * 100f + 0.0001f))) cTimer.Text = FormatTime(shown);
            Label(new Rect(px, py + 2, pw, 48), cTimer.Text, sTimer, tc);
            if (!ReferenceEquals(cNameFor, def.name)) { cNameFor = def.name; cName = def.name.ToUpperInvariant(); }
            Label(new Rect(px, py + 44, pw, 20), drift ? "TIME LEFT" : cName, sSmallC, Pink);

            // left: checkpoint counter (+ penalties)
            int done = Mathf.Clamp(nextGate - 1, 0, course.Checkpoints);
            Label(new Rect(px + 14, py + 6, 120, 20), "CHECKPOINT", sSmall, Pink);
            if (cGates.Changed(done * 1000L + course.Checkpoints)) cGates.Text = done + " / " + course.Checkpoints;
            Label(new Rect(px + 14, py + 24, 120, 26), cGates.Text, sMidL, White);
            if (penalty > 0f)
            {
                if (cPenalty.Changed(Mathf.RoundToInt(penalty * 10f))) cPenalty.Text = "+" + penalty.ToString("0.0", Inv) + " s";
                Label(new Rect(px + 14, py + 46, 120, 18), cPenalty.Text, sSmall, Bad);
            }

            // right: split delta, or best / ghost reference
            Rect rr = new Rect(px + pw - 134, py + 6, 120, 20);
            var right = sSmallR;
            if (!drift && splitTimer > 0f)
            {
                Label(rr, "SPLIT", right, Pink);
                if (cSplit.Changed(Mathf.RoundToInt(splitDelta * 100f))) cSplit.Text = FormatDelta(splitDelta);
                Label(new Rect(rr.x, py + 24, 120, 26), cSplit.Text, sMidR,
                      new Color(splitDelta < 0f ? Good.r : Bad.r, splitDelta < 0f ? Good.g : Bad.g, splitDelta < 0f ? Good.b : Bad.b, Mathf.Clamp01(splitTimer)));
            }
            else
            {
                Label(rr, "BEST", right, Pink);
                if (cBest.Changed(Mathf.RoundToInt(bestRef * 100f) * 2L + (drift ? 1 : 0)))
                    cBest.Text = bestRef > 0f ? (drift ? FormatPts(bestRef) : FormatTime(bestRef)) : "--";
                Label(new Rect(rr.x, py + 24, 120, 26), cBest.Text, sMidR, White);
                if (ghost != null)
                {
                    if (cGhost.Changed(Mathf.RoundToInt(ghost.Time * 100f))) cGhost.Text = "ghost " + FormatTime(ghost.Time);
                    Label(new Rect(rr.x, py + 46, 120, 18), cGhost.Text, right, new Color(1f, 0.9f, 0.94f, 0.8f));
                }
            }
        }

        void DrawDriftPanel(float W)
        {
            float pw = 300f, ph = 162f, px = W - pw - 16f, py = 10f;
            Panel(new Rect(px, py, pw, ph), 0.55f);
            Label(new Rect(px + 16, py + 8, pw - 32, 20), "DRIFT ZONE SCORE", sSmall, Pink);
            if (cScore.Changed(Mathf.RoundToInt(score))) cScore.Text = FormatPts(score);
            Label(new Rect(px + 16, py + 26, pw - 32, 44), cScore.Text, sTitleL, White);
            if (chainActive && chainPts > 1f)
            {
                if (cChain.Changed(Mathf.RoundToInt(chainPts) * 100L + Mathf.RoundToInt(combo))) cChain.Text = "+" + FormatPts(chainPts) + "   x" + combo.ToString("0", Inv);
                Label(new Rect(px + 16, py + 70, pw - 32, 26), cChain.Text, sMidL, Pink);
            }
            else if (chainFlash > 0f)
            {
                if (cBank.Changed(Mathf.RoundToInt(lastBank))) cBank.Text = "+" + FormatPts(lastBank) + "  banked";
                Label(new Rect(px + 16, py + 70, pw - 32, 26), cBank.Text, sMidL,
                      new Color(Good.r, Good.g, Good.b, Mathf.Clamp01(chainFlash)));
            }
            else
                Label(new Rect(px + 16, py + 70, pw - 32, 26), "hold a drift to build combo", sSmall, new Color(1f, 1f, 1f, 0.6f));

            // medal progress bar with bronze / silver / gold ticks
            float bx = px + 16, by = py + 106, bw = pw - 32, bh = 12;
            float total = score + chainPts;
            Fill(new Rect(bx - 2, by - 2, bw + 4, bh + 4), new Color(0.1f, 0.05f, 0.08f, 0.6f));
            int m = def.MedalForPoints(total);
            Fill(new Rect(bx, by, bw * Mathf.Clamp01(total / def.goldPts), bh), m > 0 ? MedalColor(m) : Pink);
            marks[0] = def.bronzePts; marks[1] = def.silverPts; marks[2] = def.goldPts;
            for (int i = 0; i < 3; i++)
            {
                float x = bx + bw * marks[i] / def.goldPts;
                Fill(new Rect(x - 1.5f, by - 5, 3, bh + 10), MedalColor(i + 1));
            }
            if (m >= 3) cNext.Text = "GOLD secured!";
            else if (cNext.Changed(m * 10000000L + Mathf.RoundToInt(marks[m] - total))) cNext.Text = MedalName(m + 1) + " in " + FormatPts(marks[m] - total);
            if (m >= 3) cNext.Key = long.MinValue;
            Label(new Rect(bx, by + 18, bw, 22), cNext.Text, sSmall, m >= 3 ? GoldCol : White);
        }

        void DrawResults(float W, float H)
        {
            Panel(new Rect(0, 0, W, H), 0.25f);
            float pw = Mathf.Min(580f, W - 32f), ph = 420f, px = (W - pw) * 0.5f, py = Mathf.Max(10f, (H - ph) * 0.5f);
            Panel(new Rect(px, py, pw, ph), 0.78f);
            bool drift = def.kind == StageKind.Drift;
            Label(new Rect(px, py + 14, pw, 42), def.name.ToUpperInvariant(), sTitle, Pink);
            string head = !finished ? "TIME UP" : newBest ? (drift ? "NEW BEST SCORE!" : "NEW BEST TIME!") : "FINISH";
            Label(new Rect(px, py + 58, pw, 30), head, sMid, newBest ? GoldCol : (!finished ? Bad : White));
            Label(new Rect(px, py + 92, pw, 66), drift ? FormatPts(result) + " pts" : FormatTime(result), sHuge, White);

            float y = py + 160;
            if (!drift && penalty > 0f)
            {
                Label(new Rect(px, y, pw, 22), "includes +" + penalty.ToString("0.0", Inv) + " s of reset penalties", sSmallC, Bad);
            }
            else if (drift && !finished)
            {
                Label(new Rect(px, y, pw, 22), "reach the finish gate in time to earn a medal", sSmallC, Bad);
            }
            y += 26;
            if (prevBest > 0f)
            {
                string bl;
                if (drift) bl = "Previous best " + FormatPts(prevBest) + " pts";
                else bl = "Previous best " + FormatTime(prevBest) + "   (" + FormatDelta(result - prevBest) + ")";
                bool better = drift ? result > prevBest : result < prevBest;
                Label(new Rect(px, y, pw, 24), bl, sSmallC, better ? Good : White);
            }
            else Label(new Rect(px, y, pw, 24), "First record on this stage", sSmallC, White);
            y += 34;

            // medal
            Color mc = MedalColor(medal);
            string mt = medal > 0 ? MedalName(medal).ToUpperInvariant() + " MEDAL" : "NO MEDAL";
            Vector2 sz = sMid.CalcSize(new GUIContent(mt));
            float mx = px + (pw - sz.x - 34f) * 0.5f;
            Fill(new Rect(mx, y + 4, 24, 24), new Color(0.1f, 0.05f, 0.08f, 0.8f));
            Fill(new Rect(mx + 3, y + 7, 18, 18), mc);
            Label(new Rect(mx + 34, y, sz.x + 10, 32), mt, sMidL, mc);
            y += 44;

            // targets
            float colW = (pw - 40f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int m = 3 - i;
                string target = drift ? FormatPts(m == 3 ? def.goldPts : m == 2 ? def.silverPts : def.bronzePts)
                                      : FormatTime(m == 3 ? def.GoldTime(course.length) : m == 2 ? def.SilverTime(course.length) : def.BronzeTime(course.length));
                Color c = MedalColor(m);
                if (medal < m) c = new Color(c.r, c.g, c.b, 0.45f);
                Rect r = new Rect(px + 20 + i * colW, y, colW, 22);
                Label(r, MedalName(m).ToUpperInvariant(), sSmallC, c);
                Label(new Rect(r.x, y + 20, colW, 24), target, sSmallC, c);
            }
            y += 54;
            if (ghostSaved) Label(new Rect(px, y, pw, 22), "Ghost saved - race it next time with this car", sSmallC, new Color(1f, 0.9f, 0.94f, 0.85f));

            // footer (also clickable)
            float fy = py + ph - 44, fw = (pw - 40f) / 3f;
            string[] items = { "R   restart", "ENTER   continue", "ESC   free roam" };
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(px + 20 + i * fw, fy, fw - 8, 32);
                Panel(r, 0.5f);
                Label(r, items[i], sSmallC, i == 1 ? Pink : White);
                if (resultsTimer > 0.8f && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (i == 0) { Sound("MenuSelect"); Restart(); }
                    else { Sound(i == 1 ? "MenuSelect" : "MenuBack"); ExitToFreeRoam(); }
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}

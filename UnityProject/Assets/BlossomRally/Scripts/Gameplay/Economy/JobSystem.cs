// JobSystem.cs - the active job: accept → (back to the vehicle / drive to the pickup) → deliver (stops,
// timer) → pay. GPS waypoint through the map module (Gps.Set), a light-pillar beacon in the world plus an
// on-screen marker, a HUD objective card (timer, distance, warmth / comfort meter, pay) and a results card.
//
// Kinds: Tea (warmth fades with time, hard knocks spill), Taxi (passenger NPC; comfort + punctuality = tip),
// Courier (time bonus), Photo (reach the spot, open photo mode, take the shot), Snow supply (fragile + time),
// Desert expedition (multi-stop + time). Late deliveries still pay (cosy: no failing), just less.
using UnityEngine;

namespace BlossomRally
{
    public class JobSystem : MonoBehaviour
    {
        public static JobSystem Instance { get; private set; }

        public enum Stage { None, ToVehicle, Pickup, Deliver, Photo, Done }

        public const float ArriveRadius = 15f, ViaRadius = 22f, PhotoRadius = 48f, ArriveSpeedKmh = 28f;

        public JobDef Job { get; private set; }
        public Stage CurrentStage { get; private set; }
        public static bool HasJob { get { return Instance != null && Instance.Job != null && Instance.CurrentStage != Stage.Done; } }

        int pay, stop;
        float limit, timeLeft, route, spill, spillMsgT, doneT;
        int nearMisses;
        bool atBoard, gpsOurs;
        string photoBaseline;
        Vector3 target;
        bool hasTarget;
        float targetRadius;

        // passenger (taxi)
        PersonRig passenger;
        float passengerT;
        int passengerMode;          // 0 none, 1 waiting at the rank, 2 walking to the car, 3 aboard, 4 leaving
        Vector3 passengerFrom;

        // beacon
        Transform beacon;
        Material beamMat, ringMat;
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        // HUD cache
        string objective = "", payText = "", titleLine = "";
        readonly EcoGui.Clock clock = new EcoGui.Clock();
        readonly DistText dist = new DistText();
        readonly CachedText lateText = new CachedText();
        // results
        string resultTitle = "", resultTotal = "";
        readonly string[] resultLines = new string[4];
        readonly string[] resultValues = new string[4];
        int resultCount;

        void Awake()
        {
            Instance = this;
            GameEvents.Impact += OnImpact;
            GameEvents.Landed += OnLanded;
            WorldHooks.WorldBuilt += w => { JobData.Resolve(w); BuildBeacon(); };
        }

        void OnDestroy()
        {
            GameEvents.Impact -= OnImpact;
            GameEvents.Landed -= OnLanded;
        }

        // ------------------------------------------------------------------ accept / abandon
        /// <summary>Is this job on cooldown (done recently)?</summary>
        public static bool Resting(JobDef j) { return j != null && Time.time - j.lastDone < 150f; }

        /// <summary>Estimated pay and time limit when starting from 'start'.</summary>
        public static void Estimate(JobDef j, Vector3 start, out int estPay, out float estLimit, out float estRoute)
        {
            estRoute = j.RouteLength(start);
            estPay = JobData.Pay(j, estRoute);
            estLimit = JobData.TimeLimit(j, estRoute);
        }

        /// <summary>Start a job. fromBoard: accepted at a board / desk here (the pickup is here); otherwise
        /// (phone) the player first drives to the job's pickup place.</summary>
        public bool Accept(JobDef j, bool fromBoard)
        {
            if (j == null || HasJob) return false;
            ClearPassenger();
            JobData.Resolve(WorldHooks.World);
            Job = j;
            atBoard = fromBoard;
            spill = 0f; nearMisses = 0; stop = 0;
            Vector3 here = PlayerState.Position;
            bool nearPickup = Flat(here, j.from.pos) < 160f;
            bool skipPickup = fromBoard || nearPickup || j.kind == JobKind.Photo;
            Vector3 start = skipPickup ? here : j.from.pos;
            route = j.RouteLength(start);
            pay = JobData.Pay(j, route);
            limit = JobData.TimeLimit(j, route);
            timeLeft = limit;
            payText = Wallet.Format(pay);
            titleLine = j.title;
            if (!skipPickup)
            {
                SetStage(Stage.Pickup);
                if (j.kind == JobKind.Taxi) SpawnWaitingPassenger();
            }
            else if (PlayerState.OnFoot && j.kind != JobKind.Photo && PlayerState.Vehicle != null) SetStage(Stage.ToVehicle);
            else BeginDelivery();
            UI.Sound("MenuSelect");
            EconomyDirector.Toast(JobData.KindNames[(int)j.kind] + " - " + j.title);
            return true;
        }

        public void Abandon()
        {
            if (Job == null) return;
            string t = Job.title;
            EndJob();
            UI.Sound("MenuBack");
            EconomyDirector.Toast("Job cancelled - " + t);
        }

        void EndJob()
        {
            if (gpsOurs && Gps.Active) Gps.Clear();
            gpsOurs = false;
            Job = null;
            CurrentStage = Stage.None;
            hasTarget = false;
            if (passengerMode != 4) ClearPassenger();
            ShowBeacon(false);
        }

        void BeginDelivery()
        {
            if (Job.kind == JobKind.Taxi && passengerMode == 0) passengerMode = 3;      // accepted at the rank: aboard
            SetStage(Stage.Deliver);
        }

        void SetStage(Stage s)
        {
            CurrentStage = s;
            var j = Job;
            if (j == null) return;
            hasTarget = true;
            switch (s)
            {
                case Stage.ToVehicle:
                    hasTarget = PlayerState.Vehicle != null;
                    if (hasTarget) target = PlayerState.Vehicle.transform.position;
                    targetRadius = 4f;
                    objective = j.kind == JobKind.Taxi ? "Head back to your vehicle - " + j.cargo + " will meet you there"
                              : j.kind == JobKind.Photo ? "Head back to your vehicle"
                              : "Take " + j.cargo + " to your vehicle";
                    break;
                case Stage.Pickup:
                    target = j.from.pos;
                    targetRadius = ArriveRadius;
                    objective = (j.kind == JobKind.Taxi ? "Pick up " + j.cargo + " at " : "Collect " + j.cargo + " at ") + j.from.name;
                    SetGps(j.from.pos, j.from.name);
                    break;
                case Stage.Deliver:
                {
                    JobPlace p = CurrentStop();
                    bool via = j.via != null && stop < j.via.Length;
                    target = p.pos;
                    targetRadius = via ? ViaRadius : j.kind == JobKind.Photo ? PhotoRadius : ArriveRadius;
                    if (j.kind == JobKind.Photo) objective = "Drive to " + p.name + " for the photo";
                    else if (via) objective = "Stop " + (stop + 1) + " of " + (j.via.Length + 1) + ":  " + p.name;
                    else if (j.kind == JobKind.Taxi) objective = "Drive " + j.cargo + " to " + p.name;
                    else objective = "Deliver " + j.cargo + " to " + p.name;
                    SetGps(p.pos, p.name);
                    break;
                }
                case Stage.Photo:
                    target = j.to.pos;
                    targetRadius = PhotoRadius;
                    objective = (InputReader.PadConnected ? "X" : KeyName(GameAction.Photo)) + "  photo mode, then take the shot of " + j.to.name;
                    photoBaseline = PhotoMode.LastSavedPath;
                    break;
                case Stage.Done:
                    hasTarget = false;
                    break;
            }
            ShowBeacon(hasTarget && s != Stage.ToVehicle);
        }

        static string KeyName(GameAction a)
        {
            string k = GameSettings.Binding(a).ToString();
            return k.Length > 6 ? k.Substring(0, 6) : k;
        }

        JobPlace CurrentStop()
        {
            var j = Job;
            if (j.via != null && stop < j.via.Length) return j.via[stop];
            return j.to;
        }

        void SetGps(Vector3 p, string name)
        {
            Gps.Set(new Vector2(p.x, p.z), name);
            gpsOurs = true;
        }

        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // ------------------------------------------------------------------ driving events
        bool CargoFragile { get { return Job != null && (Job.kind == JobKind.Tea || Job.kind == JobKind.Taxi || Job.kind == JobKind.Snow); } }
        bool Carrying { get { return Job != null && (CurrentStage == Stage.Deliver || CurrentStage == Stage.Photo) && !PlayerState.OnFoot; } }

        void OnImpact(float speed, Vector3 p)
        {
            if (!Carrying || !CargoFragile || speed < 6f) return;
            spill = Mathf.Clamp01(spill + Mathf.Clamp((speed - 6f) * 0.022f, 0.02f, 0.3f));
            SpillMessage();
        }

        void OnLanded(float air, float vy)
        {
            if (!Carrying || !CargoFragile || vy < 4.5f) return;
            spill = Mathf.Clamp01(spill + Mathf.Clamp((vy - 4.5f) * 0.03f, 0.01f, 0.25f));
            SpillMessage();
        }

        void SpillMessage()
        {
            if (Time.time < spillMsgT) return;
            spillMsgT = Time.time + 6f;
            switch (Job.kind)
            {
                case JobKind.Tea: EconomyDirector.Toast("Splash! Easy - the tea is sloshing"); break;
                case JobKind.Taxi: EconomyDirector.Toast("\"Oof! Gently, please...\""); break;
                default: EconomyDirector.Toast("Careful - the supplies are rattling"); break;
            }
        }

        public void OnNearMiss()
        {
            if (!Carrying || Job.kind != JobKind.Taxi) return;
            nearMisses++;
            if (nearMisses == 1 || nearMisses == 4) EconomyDirector.Toast(nearMisses == 1 ? "Your passenger grips the seat" : "\"Is this a taxi or a rally car?\"");
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            TickPassenger();
            if (Job == null) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (CurrentStage == Stage.Done)
            {
                doneT -= Time.unscaledDeltaTime;
                if (doneT <= 0f) EndJob();
                return;
            }
            var sm = StageManager.Instance;
            bool eventOn = sm != null && sm.Running;
            float dt = Time.deltaTime;
            if (!eventOn && limit > 0f && (CurrentStage == Stage.Deliver || CurrentStage == Stage.Photo)) timeLeft -= dt;

            bool inVehicle = !PlayerState.OnFoot && PlayerState.Vehicle != null;
            Vector3 me = PlayerState.Position;
            var car = PlayerState.Vehicle;
            float kmh = car != null ? car.SpeedKmh : 0f;
            switch (CurrentStage)
            {
                case Stage.ToVehicle:
                    if (car != null) target = car.transform.position;
                    if (inVehicle) BeginDelivery();
                    break;
                case Stage.Pickup:
                    if (inVehicle && Flat(me, target) < ArriveRadius && kmh < ArriveSpeedKmh)
                    {
                        UI.Sound("Checkpoint");
                        if (Job.kind == JobKind.Taxi) { BoardPassenger(); EconomyDirector.Toast(Cap(Job.cargo) + " hops in"); }
                        else EconomyDirector.Toast("Collected " + Job.cargo);
                        BeginDelivery();
                    }
                    break;
                case Stage.Deliver:
                {
                    bool via = Job.via != null && stop < Job.via.Length;
                    float d = Flat(me, target);
                    if (Job.kind == JobKind.Photo)
                    {
                        if (d < PhotoRadius) { UI.Sound("Camera"); SetStage(Stage.Photo); }
                    }
                    else if (via)
                    {
                        if (inVehicle && d < ViaRadius)
                        {
                            UI.Sound("Checkpoint");
                            EconomyDirector.Toast("Stop reached - " + Job.via[stop].name);
                            stop++;
                            SetStage(Stage.Deliver);
                        }
                    }
                    else if (inVehicle && d < ArriveRadius && kmh < ArriveSpeedKmh) Complete();
                    break;
                }
                case Stage.Photo:
                    if (Flat(me, target) > PhotoRadius * 1.6f) { SetStage(Stage.Deliver); break; }
                    string shot = PhotoMode.LastSavedPath;
                    if (shot != null && !ReferenceEquals(shot, photoBaseline) && shot != photoBaseline) Complete();
                    break;
            }
            UpdateBeacon();
        }

        static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ------------------------------------------------------------------ payout
        void Complete()
        {
            var j = Job;
            int k = (int)j.kind;
            float frac = limit > 0f ? Mathf.Clamp01(timeLeft / limit) : 1f;
            bool late = limit > 0f && timeLeft < 0f;
            resultCount = 0;
            int total = 0;
            switch (j.kind)
            {
                case JobKind.Tea:
                {
                    float warmth = late ? 0.4f : 0.55f + 0.45f * frac;
                    float smooth = Mathf.Clamp(1f - spill, 0.3f, 1f);
                    int basePay = Mathf.RoundToInt(pay * warmth);
                    int spillCut = -Mathf.RoundToInt(basePay * (1f - smooth));
                    Line("Delivery", pay);
                    Line(late ? "Tea went cold" : "Warmth " + Mathf.RoundToInt(warmth * 100f) + "%", basePay - pay);
                    if (spill < 0.02f) { int b = Mathf.RoundToInt(pay * 0.15f); Line("Not a drop spilled!", b); total += b; }
                    else Line("Spilled " + Mathf.RoundToInt(spill * 100f) + "%", spillCut);
                    total += basePay + spillCut;
                    break;
                }
                case JobKind.Taxi:
                {
                    float comfort = Mathf.Clamp01(1f - spill - 0.06f * nearMisses);
                    float punctual = late ? 0.25f : 0.5f + 0.5f * frac;
                    int tip = Mathf.RoundToInt(pay * 0.4f * comfort * punctual);
                    Line("Fare", pay);
                    Line(tip > pay * 0.25f ? "Generous tip" : tip > 0 ? "Tip" : "No tip", tip);
                    if (comfort > 0.97f && !late) { int b = Mathf.RoundToInt(pay * 0.1f); Line("Five-star ride", b); total += b; }
                    total += pay + tip;
                    break;
                }
                case JobKind.Photo:
                {
                    Line("Commission", pay);
                    total += pay;
                    if (Atmosphere.NightFactor > 0.5f && (j.region == RegionId.City || j.region == RegionId.Snow))
                    { int b = Mathf.RoundToInt(pay * 0.25f); Line("Night lights bonus", b); total += b; }
                    EconomySave.Data.photosTaken++;
                    break;
                }
                default:
                {
                    Line("Delivery", pay);
                    total += pay;
                    if (j.kind == JobKind.Snow)
                    {
                        int cut = -Mathf.RoundToInt(pay * 0.3f * Mathf.Clamp01(spill));
                        if (spill < 0.02f) { int b = Mathf.RoundToInt(pay * 0.1f); Line("Cargo in perfect shape", b); total += b; }
                        else { Line("Dented supplies", cut); total += cut; }
                    }
                    float bonusK = j.kind == JobKind.Courier ? 0.5f : j.kind == JobKind.Desert ? 0.4f : 0.35f;
                    int tb = late ? 0 : Mathf.RoundToInt(pay * bonusK * frac);
                    Line(late ? "Arrived late (no bonus)" : "Time bonus", tb);
                    total += tb;
                    break;
                }
            }
            // places module buffs (a good meal / tea): "BlossomRally.Buffs.JobPayMultiplier", read without a compile dependency
            float buff = BuffPay();
            if (buff > 1.001f) { int bb = Mathf.RoundToInt(total * (buff - 1f)); Line("Well-fed bonus", bb); total += bb; }
            total = Mathf.Max(20, total);
            resultTitle = j.kind == JobKind.Photo ? "PHOTO DELIVERED" : j.kind == JobKind.Taxi ? "FARE COMPLETE" : j.via != null ? "EXPEDITION COMPLETE" : "DELIVERED";
            resultTotal = "+" + Wallet.Format(total);
            var d = EconomySave.Data;
            d.jobsDone++;
            d.jobsEarned += total;
            if (k < d.jobsByKind.Length) d.jobsByKind[k]++;
            j.lastDone = Time.time;
            Wallet.Add(total, JobData.KindNames[k] + " · " + j.title);
            UI.Sound(total >= pay ? "MedalGold" : "MedalSilver");
            InputReader.Rumble(0.2f, 0.4f, 0.2f);
            if (j.kind == JobKind.Taxi) DropPassenger();
            if (gpsOurs && Gps.Active) Gps.Clear();
            gpsOurs = false;
            SetStage(Stage.Done);
            doneT = 5f;
            EconomyDirector.RequestSave();
        }

        static System.Reflection.PropertyInfo buffProp;
        static bool buffLooked;
        static float BuffPay()
        {
            if (!buffLooked)
            {
                buffLooked = true;
                try
                {
                    var t = typeof(JobSystem).Assembly.GetType("BlossomRally.Buffs");
                    if (t != null) buffProp = t.GetProperty("JobPayMultiplier", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (buffProp != null && buffProp.PropertyType != typeof(float)) buffProp = null;
                }
                catch (System.Exception) { buffProp = null; }
            }
            if (buffProp == null) return 1f;
            try { return Mathf.Clamp((float)buffProp.GetValue(null, null), 1f, 2f); }
            catch (System.Exception) { return 1f; }
        }

        void Line(string label, int amount)
        {
            if (resultCount >= resultLines.Length) return;
            resultLines[resultCount] = label;
            resultValues[resultCount] = amount == 0 ? "-" : (amount > 0 ? "+" : "−") + Wallet.Format(Mathf.Abs(amount));
            resultCount++;
        }

        // ------------------------------------------------------------------ taxi passenger
        void SpawnWaitingPassenger()
        {
            var j = Job;
            Vector3 p = j.from.pos + Vector3.Cross(Vector3.up, j.from.roadDir).normalized * 1.5f;
            var w = WorldHooks.World;
            if (w != null) p.y = w.TerrainHeight(p.x, p.z);
            float yaw = Mathf.Atan2(-j.from.roadDir.x, -j.from.roadDir.z) * Mathf.Rad2Deg;
            try
            {
                passenger = PersonFactory.Create(PersonLook.Random(j.index * 31 + 7), p, yaw, transform);
                if (passenger != null) passenger.SetPose(PersonPose.Wave);
                passengerMode = passenger != null ? 1 : 0;
            }
            catch (System.Exception e) { Debug.LogException(e); passenger = null; passengerMode = 0; }
        }

        void BoardPassenger()
        {
            if (passenger == null) { passengerMode = 3; return; }
            passengerMode = 2;
            passengerT = 0f;
            passengerFrom = passenger.transform.position;
            passenger.SetPose(PersonPose.Walk);
        }

        void DropPassenger()
        {
            var car = PlayerState.Vehicle;
            if (car == null) { ClearPassenger(); return; }
            Vector3 side = car.transform.right;
            Vector3 p = car.transform.position + side * 1.8f;
            var w = WorldHooks.World;
            if (w != null) p.y = w.TerrainHeight(p.x, p.z);
            try
            {
                if (passenger == null) passenger = PersonFactory.Create(PersonLook.Random(Job.index * 31 + 7), p, 0f, transform);
                if (passenger == null) { passengerMode = 0; return; }
                passenger.gameObject.SetActive(true);
                passenger.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.ProjectOnPlane(side, Vector3.up), Vector3.up));
                passenger.SetPose(PersonPose.Walk);
                passenger.speed = 1.3f;
                passengerFrom = p;
                passengerMode = 4;
                passengerT = 0f;
            }
            catch (System.Exception e) { Debug.LogException(e); passenger = null; passengerMode = 0; }
        }

        void TickPassenger()
        {
            if (passenger == null) return;
            float dt = Time.deltaTime;
            passengerT += dt;
            var car = PlayerState.Vehicle;
            if (passengerMode == 2)
            {
                if (car == null) { passenger.gameObject.SetActive(false); passengerMode = 3; return; }
                Vector3 door = car.transform.position + car.transform.right * 1.1f;
                float k = Mathf.Clamp01(passengerT / 1.3f);
                Vector3 p = Vector3.Lerp(passengerFrom, door, k);
                var w = WorldHooks.World;
                if (w != null) p.y = Mathf.Max(w.TerrainHeight(p.x, p.z), car.transform.position.y - 0.6f);
                Vector3 dir = door - passengerFrom; dir.y = 0f;
                passenger.transform.SetPositionAndRotation(p, dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : passenger.transform.rotation);
                passenger.speed = 1.4f;
                if (k >= 1f) { passenger.gameObject.SetActive(false); passengerMode = 3; }
            }
            else if (passengerMode == 4)
            {
                if (passengerT < 3.2f)
                {
                    passenger.transform.position += passenger.transform.forward * 1.3f * dt;
                    var w = WorldHooks.World;
                    if (w != null)
                    {
                        Vector3 p = passenger.transform.position;
                        p.y = w.TerrainHeight(p.x, p.z);
                        passenger.transform.position = p;
                    }
                }
                else if (passengerT < 3.3f) { passenger.SetPose(PersonPose.Wave); passenger.speed = 0f; }
                else if (passengerT > 6f) ClearPassenger();
            }
        }

        void ClearPassenger()
        {
            if (passenger != null) Destroy(passenger.gameObject);
            passenger = null;
            passengerMode = 0;
        }

        // ------------------------------------------------------------------ beacon
        void BuildBeacon()
        {
            if (beacon != null) return;
            var sh = MaterialLibrary.FindShader("BlossomRally/Additive");
            if (sh == null) return;
            beamMat = new Material(sh) { name = "BR_JobBeam" };
            beamMat.SetFloat("_Soft", 1.3f);
            ringMat = new Material(sh) { name = "BR_JobRing" };
            ringMat.SetFloat("_Soft", 0f);
            var root = new GameObject("BR_JobBeacon");
            root.transform.SetParent(transform, false);
            beacon = root.transform;
            AddPart(root.transform, BeamMesh(2.0f, 46f, 18), beamMat);
            AddPart(root.transform, RingMesh(3.6f, 5.0f, 32), ringMat);
            root.SetActive(false);
        }

        static void AddPart(Transform parent, Mesh m, Material mat)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Open cylinder, bright at the foot and fading out upwards.</summary>
        public static Mesh BeamMesh(float r, float h, int seg)
        {
            var v = new Vector3[(seg + 1) * 3];
            var n = new Vector3[v.Length];
            var c = new Color32[v.Length];
            var t = new int[seg * 12];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < 3; k++)
                {
                    float y = k == 0 ? 0f : k == 1 ? h * 0.25f : h;
                    int idx = i * 3 + k;
                    v[idx] = d * r * (k == 2 ? 0.7f : 1f) + Vector3.up * y;
                    n[idx] = d;
                    c[idx] = new Color32(255, 255, 255, (byte)(k == 0 ? 255 : k == 1 ? 120 : 0));
                }
            }
            int ti = 0;
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = i * 3 + k, b = (i + 1) * 3 + k;
                    t[ti++] = a; t[ti++] = a + 1; t[ti++] = b;
                    t[ti++] = b; t[ti++] = a + 1; t[ti++] = b + 1;
                }
            var m = new Mesh { name = "JobBeam", vertices = v, normals = n, colors32 = c, triangles = t };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat ring on the ground (soft inner and outer edge).</summary>
        public static Mesh RingMesh(float r0, float r1, int seg)
        {
            var v = new Vector3[(seg + 1) * 3];
            var n = new Vector3[v.Length];
            var c = new Color32[v.Length];
            var t = new int[seg * 12];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < 3; k++)
                {
                    float rr = k == 0 ? r0 : k == 1 ? (r0 + r1) * 0.5f : r1;
                    int idx = i * 3 + k;
                    v[idx] = d * rr + Vector3.up * 0.12f;
                    n[idx] = Vector3.up;
                    c[idx] = new Color32(255, 255, 255, (byte)(k == 1 ? 255 : 0));
                }
            }
            int ti = 0;
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = i * 3 + k, b = (i + 1) * 3 + k;
                    t[ti++] = a; t[ti++] = b; t[ti++] = a + 1;
                    t[ti++] = b; t[ti++] = b + 1; t[ti++] = a + 1;
                }
            var m = new Mesh { name = "JobRing", vertices = v, normals = n, colors32 = c, triangles = t };
            m.RecalculateBounds();
            return m;
        }

        void ShowBeacon(bool on)
        {
            if (beacon == null) BuildBeacon();
            if (beacon == null) return;
            if (beacon.gameObject.activeSelf != on) beacon.gameObject.SetActive(on);
            if (on && Job != null)
            {
                Color c = JobData.KindColours[(int)Job.kind];
                beamMat.SetColor(IdColor, c);
                ringMat.SetColor(IdColor, c);
                UpdateBeacon();
            }
        }

        void UpdateBeacon()
        {
            if (beacon == null || !beacon.gameObject.activeSelf) return;
            Vector3 p = target;
            var w = WorldHooks.World;
            if (w != null) p.y = w.TerrainHeight(p.x, p.z);
            float s = targetRadius / ArriveRadius;
            beacon.position = p;
            beacon.localScale = new Vector3(s, 1f, s);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
            // brighter from far away so it reads over the fog, softer up close
            var cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, p) : 100f;
            float far = Mathf.Lerp(0.55f, 1.6f, Mathf.InverseLerp(30f, 400f, d));
            beamMat.SetFloat(IdIntensity, pulse * far * (1f - 0.4f * Atmosphere.NightFactor));
            ringMat.SetFloat(IdIntensity, 0.9f * pulse);
        }

        // ------------------------------------------------------------------ HUD
        void OnGUI()
        {
            if (Job == null) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || GameManager.HideHud) return;
            if (EcoMenus.IsOpen || ValleyMap.IsOpen || PhotoAlbum.IsOpen) return;
            if (Event.current.type == EventType.Layout) return;
            EcoGui.Styles();
            if (CurrentStage == Stage.Done) { DrawResults(); return; }
            DrawMarker();
            DrawCard();
        }

        void DrawCard()
        {
            float s = EcoGui.S, W = Screen.width;
            float w = 380f * s, x = W - w - 22f * s, y = WalletHud.BottomEdge + 14f * s;
            bool meter = Job.kind == JobKind.Tea || Job.kind == JobKind.Taxi || Job.kind == JobKind.Snow;
            float h = (meter ? 150f : 124f) * s;
            Rect r = new Rect(x, y, w, h);
            MenuArt.Card(r, 1f, false);
            Color kc = JobData.KindColours[(int)Job.kind];
            MenuArt.Fill(new Rect(r.x, r.y, Mathf.Max(3f, 4f * s), r.height), kc);
            float ix = r.x + 16f * s, iw = w - 30f * s;
            EcoGui.Tag(new Rect(ix, r.y + 10f * s, EcoGui.Width(MenuArt.TinyC, JobData.KindTags[(int)Job.kind]) + 14f * s, 20f * s), JobData.KindTags[(int)Job.kind], kc, 1f);
            MenuArt.Text(new Rect(ix, r.y + 10f * s, iw, 20f * s), payText, MenuArt.MidR, UI.Gold);
            MenuArt.Text(new Rect(ix, r.y + 32f * s, iw, 30f * s), titleLine, MenuArt.MidL, UI.White);
            MenuArt.Text(new Rect(ix, r.y + 60f * s, iw, 24f * s), objective, MenuArt.Small, MenuArt.A(UI.White, 0.86f));
            // distance + timer
            float ty = r.y + 88f * s;
            if (hasTarget && CurrentStage != Stage.ToVehicle)
            {
                float d = Flat(PlayerState.Position, target);
                MenuArt.Text(new Rect(ix, ty, iw * 0.5f, 26f * s), dist.Get(d), MenuArt.MidL, UI.Pink);
            }
            if (limit > 0f && (CurrentStage == Stage.Deliver || CurrentStage == Stage.Photo))
            {
                bool late = timeLeft < 0f;
                string tt;
                if (late)
                {
                    int sec = Mathf.CeilToInt(-timeLeft);
                    if (lateText.Changed(sec)) lateText.Text = "late +" + (sec / 60) + ":" + (sec % 60).ToString("00");
                    tt = lateText.Text;
                }
                else tt = clock.Get(timeLeft);
                Color tc = late ? EcoGui.Muted : timeLeft < 15f ? (Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f ? EcoGui.Coral : UI.White) : UI.White;
                MenuArt.Text(new Rect(ix, ty, iw, 26f * s), tt, MenuArt.MidR, tc);
            }
            else if (CurrentStage == Stage.Pickup || CurrentStage == Stage.ToVehicle)
                MenuArt.Text(new Rect(ix, ty, iw, 26f * s), limit > 0f ? "timer starts at pickup" : "", MenuArt.SmallR, EcoGui.Muted);
            if (meter)
            {
                float my = r.y + 120f * s;
                string label; float v; Color c;
                if (Job.kind == JobKind.Tea)
                {
                    float warmth = limit > 0f ? (timeLeft < 0f ? 0.4f : 0.55f + 0.45f * Mathf.Clamp01(timeLeft / limit)) : 1f;
                    v = warmth * (1f - spill * 0.6f);
                    label = timeLeft < 0f ? "Cold" : spill > 0.02f ? "Warm · spilt" : "Warm";
                    c = Color.Lerp(new Color(0.55f, 0.75f, 1f), UI.Gold, Mathf.InverseLerp(0.4f, 1f, warmth));
                }
                else if (Job.kind == JobKind.Taxi)
                {
                    v = Mathf.Clamp01(1f - spill - 0.06f * nearMisses);
                    label = v > 0.9f ? "Comfy" : v > 0.6f ? "Bumpy" : "Shaken";
                    c = Color.Lerp(EcoGui.Coral, EcoGui.Jade, v);
                }
                else
                {
                    v = 1f - spill;
                    label = v > 0.95f ? "Cargo fine" : "Cargo rattled";
                    c = Color.Lerp(EcoGui.Coral, new Color(0.8f, 0.92f, 1f), v);
                }
                MenuArt.Text(new Rect(ix, my - 4f * s, 110f * s, 20f * s), label, MenuArt.Tiny, MenuArt.A(UI.White, 0.8f));
                EcoGui.Bar(new Rect(ix + 112f * s, my + 4f * s, iw - 112f * s, 7f * s), v, c, 1f);
            }
        }

        void DrawMarker()
        {
            if (!hasTarget || CurrentStage == Stage.ToVehicle) return;
            var cam = Camera.main;
            if (cam == null) return;
            float s = EcoGui.S, W = Screen.width, H = Screen.height;
            Vector3 sp = cam.WorldToScreenPoint(target + Vector3.up * 3f);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = W - sp.x; sp.y = H - sp.y; }
            Vector2 p = new Vector2(sp.x, H - sp.y);
            float m = 46f * s;
            bool clamped = behind || p.x < m || p.x > W - m || p.y < m || p.y > H - m;
            if (behind) p.y = H - m;
            p.x = Mathf.Clamp(p.x, m, W - m);
            p.y = Mathf.Clamp(p.y, m, H - m);
            float d = Flat(PlayerState.Position, target);
            if (!clamped && d < targetRadius * 0.9f) return;       // standing on it: the ring says enough
            Color kc = JobData.KindColours[(int)Job.kind];
            float r = (clamped ? 15f : 13f + 2f * Mathf.Sin(Time.unscaledTime * 4f)) * s;
            MenuArt.Tex(new Rect(p.x - r * 2.2f, p.y - r * 2.2f, r * 4.4f, r * 4.4f), MenuArt.Glow, MenuArt.A(kc, 0.45f));
            MenuArt.Tex(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), MenuArt.Dot, MenuArt.A(MenuArt.PlumDeep, 0.85f));
            MenuArt.Tex(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), MenuArt.Ring, kc);
            MenuArt.Tex(new Rect(p.x - r * 0.38f, p.y - r * 0.38f, r * 0.76f, r * 0.76f), MenuArt.Dot, kc);
            MenuArt.Text(new Rect(p.x - 60f * s, p.y + r + 2f * s, 120f * s, 22f * s), dist.Get(d), MenuArt.SmallC, UI.White);
        }

        void DrawResults()
        {
            float s = EcoGui.S, W = Screen.width;
            float a = Mathf.Clamp01(doneT * 2f) * MenuArt.EaseOut((5f - doneT) / 0.35f);
            float w = 460f * s, h = (128f + resultCount * 30f) * s;
            Rect r = new Rect((W - w) * 0.5f, Screen.height * 0.12f, w, h);
            MenuArt.Card(r, a);
            Color kc = Job != null ? JobData.KindColours[(int)Job.kind] : UI.Pink;
            MenuArt.Text(new Rect(r.x, r.y + 12f * s, w, 40f * s), resultTitle, MenuArt.Title, MenuArt.A(kc, a));
            MenuArt.Text(new Rect(r.x, r.y + 50f * s, w, 24f * s), titleLine, MenuArt.SmallC, MenuArt.A(UI.White, 0.8f * a));
            float y = r.y + 82f * s;
            for (int i = 0; i < resultCount; i++)
            {
                MenuArt.Text(new Rect(r.x + 30f * s, y, w * 0.6f, 26f * s), resultLines[i], MenuArt.Body, MenuArt.A(UI.White, 0.85f * a));
                bool neg = resultValues[i].Length > 0 && resultValues[i][0] == '−';
                MenuArt.Text(new Rect(r.x, y, w - 30f * s, 26f * s), resultValues[i], MenuArt.BodyR, MenuArt.A(neg ? EcoGui.Coral : UI.White, a));
                y += 30f * s;
            }
            MenuArt.Fill(new Rect(r.x + 30f * s, y + 4f * s, w - 60f * s, 1f), MenuArt.A(UI.White, 0.25f * a));
            MenuArt.Text(new Rect(r.x + 30f * s, y + 8f * s, w * 0.5f, 34f * s), "Total", MenuArt.MidL, MenuArt.A(UI.White, a));
            MenuArt.Text(new Rect(r.x, y + 8f * s, w - 30f * s, 34f * s), resultTotal, MenuArt.MidR, MenuArt.A(UI.Gold, a));
        }
    }
}

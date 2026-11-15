// People.cs - SHARED CONTRACT: build a person (player, pedestrians, shopkeepers, monks, bike riders) and pose it.
// Real implementation (on-foot module): stylised low-poly people in the game's pastel vertex-colour look.
//  * One skinned mesh per person (16 rigid bones, a few blended robe / hair vertices), shared between every
//    person with the same look (ref-counted cache), one shared material: ONE draw call per person.
//  * Procedural animation only: every pose is computed from code each tick (walk / run cycles driven by
//    speed with arm swing, bob and lean; idle breathing and look-around; sit, drive, ride, wave, work, pray,
//    jump) and cross-faded when the pose changes. Two-bone IK puts hands on wheels, bars, counters, brooms.
//  * Cheap: one central LateUpdate (PersonSystem) poses all people; far people animate at 1/2..1/16 rate,
//    people beyond the quality's cull distance are not drawn; no per-frame allocations.
// Public API (stable): PersonLook, PersonPose, PersonRig { look, pose, speed, head, handL, handR, hips, SetPose },
// PersonFactory.Create. Extras (optional): PersonRig.hero / airborne / seatHeight / workStyle / steer / lookYaw /
// lookAt / SetLook, PersonLook presets (Shopkeeper, Monk, Business, Winter, Hanfu, Rally).
//
// Pose conventions (root = this transform, on the ground at the feet, facing +Z):
//  * Sit / Drive / Ride: the root is the floor point under the hips; the seat surface is seatHeight above it
//    (defaults: Sit 0.45 m chair, Drive 0.30 m car seat above the car floor, Ride 0.78 m motorbike saddle).
//  * Work: workStyle 0 = at a counter (0.95 m high, hands wiping), 1 = sweeping with a broom (shown only then).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public enum PersonPose { Idle = 0, Walk = 1, Run = 2, Sit = 3, Drive = 4, Ride = 5, Wave = 6, Work = 7, Pray = 8 }

    /// <summary>Appearance. Use Random(seed) for crowds.</summary>
    public struct PersonLook
    {
        public Color skin, hair, top, bottom, shoes, accent;
        public int hairStyle, outfit;      // outfit: 0 casual, 1 rally suit, 2 shopkeeper/apron, 3 monk robe, 4 business, 5 winter coat, 6 hanfu
        public bool hat, female;
        public float height;               // metres, ~1.55 .. 1.9

        public static PersonLook Random(int seed)
        {
            var r = new System.Random(seed);
            System.Func<float> f = () => (float)r.NextDouble();
            var l = new PersonLook();
            l.skin = Color.Lerp(new Color(0.96f, 0.80f, 0.66f), new Color(0.62f, 0.44f, 0.32f), f());
            l.hair = Color.Lerp(new Color(0.08f, 0.07f, 0.07f), new Color(0.35f, 0.22f, 0.12f), f() * f());
            l.top = Color.HSVToRGB(f(), 0.35f + 0.4f * f(), 0.45f + 0.5f * f());
            l.bottom = Color.HSVToRGB(f(), 0.2f + 0.3f * f(), 0.2f + 0.4f * f());
            l.shoes = Color.Lerp(Color.black, Color.white, f() * f());
            l.accent = Color.HSVToRGB(f(), 0.7f, 0.9f);
            l.hairStyle = r.Next(6); l.outfit = 0; l.female = f() < 0.5f; l.hat = f() < 0.15f;
            l.height = 1.58f + 0.3f * f();
            return l;
        }

        /// <summary>The player's driver: rally suit.</summary>
        public static PersonLook Player
        {
            get
            {
                var l = Random(7);
                l.outfit = 1; l.top = new Color(0.95f, 0.95f, 0.93f); l.bottom = new Color(0.95f, 0.95f, 0.93f);
                l.accent = new Color(0.93f, 0.36f, 0.52f); l.height = 1.76f; l.hat = false;
                return l;
            }
        }

        // ---- presets (optional helpers for places / crowds)
        static PersonLook Base(int seed, int outfit)
        {
            var l = Random(seed);
            l.outfit = outfit;
            return l;
        }

        /// <summary>Shop / tea-house keeper: shirt, cream apron (accent), optional cloth cap.</summary>
        public static PersonLook Shopkeeper(int seed)
        {
            var l = Base(seed, 2);
            l.accent = Color.Lerp(new Color(0.97f, 0.94f, 0.86f), l.accent, 0.15f);
            l.bottom = Color.Lerp(l.bottom, new Color(0.18f, 0.17f, 0.2f), 0.5f);
            l.shoes = new Color(0.16f, 0.14f, 0.13f);
            return l;
        }

        /// <summary>Temple monk: saffron robe, shaved head, sandals.</summary>
        public static PersonLook Monk(int seed)
        {
            var l = Base(seed, 3);
            var r = new System.Random(seed * 31 + 5);
            l.top = Color.Lerp(new Color(0.93f, 0.55f, 0.16f), new Color(0.78f, 0.36f, 0.14f), (float)r.NextDouble());
            l.bottom = l.top;
            l.accent = new Color(0.72f, 0.24f, 0.16f);
            l.shoes = new Color(0.45f, 0.32f, 0.2f);
            l.hat = false; l.female = false;
            return l;
        }

        /// <summary>Office worker: dark suit, white shirt, coloured tie (accent).</summary>
        public static PersonLook Business(int seed)
        {
            var l = Base(seed, 4);
            var r = new System.Random(seed * 17 + 3);
            float v = 0.12f + 0.2f * (float)r.NextDouble();
            l.top = Color.Lerp(new Color(v, v, v * 1.15f), new Color(0.2f, 0.24f, 0.36f), (float)r.NextDouble() * 0.6f);
            l.bottom = l.top;
            l.shoes = new Color(0.08f, 0.07f, 0.07f);
            l.hat = false;
            return l;
        }

        /// <summary>Winter walker: puffy coat, scarf (accent), beanie when hat.</summary>
        public static PersonLook Winter(int seed)
        {
            var l = Base(seed, 5);
            l.shoes = Color.Lerp(new Color(0.3f, 0.22f, 0.16f), new Color(0.12f, 0.12f, 0.14f), 0.5f);
            l.hat = l.hat || (seed & 1) == 0;
            return l;
        }

        /// <summary>Traditional hanfu: wrap robe with contrasting collar and sash (accent).</summary>
        public static PersonLook Hanfu(int seed)
        {
            var l = Base(seed, 6);
            var r = new System.Random(seed * 13 + 1);
            l.top = Color.HSVToRGB((float)r.NextDouble(), 0.25f + 0.25f * (float)r.NextDouble(), 0.85f + 0.12f * (float)r.NextDouble());
            l.bottom = Color.Lerp(l.top, Color.white, 0.35f);
            l.shoes = new Color(0.12f, 0.1f, 0.1f);
            return l;
        }

        /// <summary>Rally driver / mechanic: one-piece suit, helmet when hat.</summary>
        public static PersonLook Rally(int seed)
        {
            var l = Base(seed, 1);
            l.bottom = l.top;
            l.shoes = new Color(0.1f, 0.1f, 0.12f);
            return l;
        }
    }

    /// <summary>A built person. Root sits on the ground at its feet, facing +Z.</summary>
    public class PersonRig : MonoBehaviour
    {
        public PersonLook look;
        public PersonPose pose = PersonPose.Idle;
        /// <summary>Ground speed in m/s (drives walk / run cycle speed). 0 while walking = measured from movement.</summary>
        public float speed;
        /// <summary>Named attachment points (bones; children follow the animation).</summary>
        public Transform head, handL, handR, hips;

        // ---- optional extras
        /// <summary>The player (or a close-up NPC): full-rate animation, never distance-culled, always casts shadows.</summary>
        public bool hero;
        /// <summary>Jumping / falling: legs tuck, arms out (blends over the current pose).</summary>
        public bool airborne;
        /// <summary>Seat surface above the root for Sit / Drive / Ride (metres); negative = pose default.</summary>
        public float seatHeight = -1f;
        /// <summary>Work pose style: 0 counter, 1 sweeping.</summary>
        public int workStyle;
        /// <summary>Drive / Ride: steering -1..1 (hands turn the wheel / bars).</summary>
        public float steer;
        /// <summary>Extra head yaw in degrees (+ = right).</summary>
        public float lookYaw;
        /// <summary>Optional: head turns towards this (within reach of the neck).</summary>
        public Transform lookAt;

        public SkinnedMeshRenderer Renderer { get { return smr; } }
        /// <summary>Height scale (look.height / 1.75).</summary>
        public float Scale { get { return scale; } }

        public virtual void SetPose(PersonPose p) { pose = p; }

        /// <summary>Change the appearance (rebuilds / re-shares the mesh). Not per frame.</summary>
        public void SetLook(PersonLook l)
        {
            look = l;
            if (!ready) return;
            PersonMesh.Release(meshKey);
            meshKey = null;
            ApplyLook();
        }

        // ================================================================== internals
        internal const int N = 16;
        internal const int BHips = 0, BChest = 1, BHead = 2, BUaL = 3, BFaL = 4, BHandL = 5, BUaR = 6, BFaR = 7, BHandR = 8,
                           BThL = 9, BShL = 10, BFtL = 11, BThR = 12, BShR = 13, BFtR = 14, BProp = 15;

        internal int sysIndex = -1;
        internal float pendingDt;
        internal Transform skeleton;
        SkinnedMeshRenderer smr;
        readonly Transform[] bones = new Transform[N];
        readonly Quaternion[] cur = new Quaternion[N], from = new Quaternion[N], tq = new Quaternion[N];
        Vector3[] rest;
        Vector3 curHips, fromHips, tHips;
        Vector3 propPos, curPropPos;
        Quaternion propRot = Quaternion.identity;
        float propShow, curPropShow;
        bool ready, hasPose;
        string meshKey;
        float scale = 1f;
        PersonPose lastPose;
        float blend = 1f, blendDur = 0.25f;
        float clock, phase, airW;
        float measured, lastSpeed, accelLean, bank, lastYaw;
        Vector3 lastPos;
        float lookCur, lookTarget, lookTimer, lookPitch;
        uint rng = 1;
        float seedT;

        static readonly Quaternion O = Quaternion.AngleAxis(-90f, Vector3.right);   // bone -Y -> +Z, bone +Z -> +Y

        internal void Init(PersonLook l, int layer)
        {
            look = l;
            gameObject.layer = layer;
            rest = PersonMesh.Rest(l.female);
            skeleton = new GameObject("Body").transform;
            skeleton.gameObject.layer = layer;
            skeleton.SetParent(transform, false);
            for (int i = 0; i < N; i++)
            {
                var b = new GameObject(PersonMesh.BoneNames[i]).transform;
                b.gameObject.layer = layer;
                int p = PersonMesh.Parent[i];
                b.SetParent(p < 0 ? skeleton : bones[p], false);
                b.localPosition = rest[i];
                bones[i] = b;
                cur[i] = from[i] = tq[i] = Quaternion.identity;
            }
            hips = bones[BHips]; head = bones[BHead]; handL = bones[BHandL]; handR = bones[BHandR];
            bones[BProp].localScale = new Vector3(0.0001f, 0.0001f, 0.0001f);
            smr = skeleton.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.bones = bones;
            smr.rootBone = bones[BHips];
            smr.quality = SkinQuality.Bone2;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, -0.05f, 0.15f), new Vector3(2.2f, 2.5f, 2.4f));
            smr.sharedMaterial = PersonMesh.Material;
            smr.lightProbeUsage = LightProbeUsage.Off;
            smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            smr.skinnedMotionVectors = false;
            curHips = fromHips = tHips = rest[BHips];
            uint s = unchecked((uint)(l.skin.GetHashCode() ^ (l.top.GetHashCode() * 31) ^ (l.hairStyle * 7919)) + (uint)GetHashCode());
            rng = s == 0 ? 1u : s;
            seedT = Rand01() * 100f;
            lookTimer = 1f + Rand01() * 3f;
            lastPose = pose;
            ready = true;
            ApplyLook();
            lastPos = transform.position;
            lastYaw = transform.eulerAngles.y;
            ApplyShadows();
            if (isActiveAndEnabled) PersonSystem.Register(this);
        }

        void ApplyLook()
        {
            scale = Mathf.Clamp(look.height > 0.5f ? look.height : 1.75f, 1.0f, 2.3f) / 1.75f;
            skeleton.localScale = new Vector3(scale, scale, scale);
            smr.sharedMesh = PersonMesh.Acquire(look, out meshKey);
        }

        internal void ApplyShadows()
        {
            if (smr == null) return;
            bool on = hero || GameSettings.Quality >= 1;
            smr.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
            smr.receiveShadows = true;
        }

        void OnEnable() { if (ready) PersonSystem.Register(this); }
        void OnDisable() { PersonSystem.Unregister(this); }
        void OnDestroy()
        {
            PersonSystem.Unregister(this);
            PersonMesh.Release(meshKey);
            meshKey = null;
        }

        float Rand01()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        // ------------------------------------------------------------------ tick (called by PersonSystem)
        internal void Tick(float dt)
        {
            if (!ready) return;
            if (dt > 0.5f) dt = 0.5f;
            clock += dt;
            // measured movement (fallback speed, lean from acceleration, bank into turns)
            Vector3 p = transform.position;
            float yaw = transform.eulerAngles.y;
            if (dt > 1e-5f)
            {
                Vector3 d = p - lastPos; d.y = 0f;
                float ms = d.magnitude / dt;
                if (ms > 20f) ms = 0f;                                   // teleport
                measured = Mathf.Lerp(measured, ms, 1f - Mathf.Exp(-dt * 8f));
                float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
                float sp0 = SpeedForGait();
                float acc = (sp0 - lastSpeed) / dt;
                lastSpeed = sp0;
                accelLean = Mathf.Lerp(accelLean, Mathf.Clamp(acc * 1.6f, -7f, 9f), 1f - Mathf.Exp(-dt * 5f));
                bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * sp0 * 0.012f, -12f, 12f), 1f - Mathf.Exp(-dt * 6f));
            }
            lastPos = p;
            lastYaw = yaw;

            if (pose != lastPose)
            {
                for (int i = 0; i < N; i++) from[i] = cur[i];
                fromHips = curHips;
                bool seat = IsSeated(pose) || IsSeated(lastPose);
                blendDur = seat ? 0.45f : 0.25f;
                blend = hasPose ? 0f : 1f;
                lastPose = pose;
            }
            airW = Mathf.MoveTowards(airW, airborne && !IsSeated(pose) ? 1f : 0f, dt * 7f);
            UpdateLook(dt);
            Evaluate(dt);

            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + dt / blendDur);
                float w = blend * blend * (3f - 2f * blend);
                for (int i = 0; i < N; i++) cur[i] = Quaternion.Slerp(from[i], tq[i], w);
                curHips = Vector3.Lerp(fromHips, tHips, w);
            }
            else
            {
                for (int i = 0; i < N; i++) cur[i] = tq[i];
                curHips = tHips;
            }
            curPropShow = Mathf.MoveTowards(curPropShow, propShow, dt * 4f);
            curPropPos = propPos;
            hasPose = true;

            bones[BHips].SetLocalPositionAndRotation(curHips, cur[BHips]);
            for (int i = 1; i < N - 1; i++) bones[i].localRotation = cur[i];
            var pr = bones[BProp];
            if (curPropShow > 0.001f)
            {
                pr.SetLocalPositionAndRotation(curPropPos, propRot);
                float k = curPropShow;
                pr.localScale = new Vector3(k, k, k);
            }
            else if (pr.localScale.x > 0.001f) pr.localScale = new Vector3(0.0001f, 0.0001f, 0.0001f);
        }

        static bool IsSeated(PersonPose p) { return p == PersonPose.Sit || p == PersonPose.Drive || p == PersonPose.Ride; }

        float SpeedForGait()
        {
            if (pose != PersonPose.Walk && pose != PersonPose.Run) return 0f;
            if (speed > 0.01f) return speed;
            if (measured > 0.05f) return measured;
            return pose == PersonPose.Run ? 3.8f : 1.3f;
        }

        void UpdateLook(float dt)
        {
            lookTimer -= dt;
            if (lookTimer <= 0f)
            {
                float r = Rand01();
                lookTarget = r < 0.35f ? 0f : (Rand01() - 0.5f) * 110f;
                lookPitch = (Rand01() - 0.6f) * 14f;
                lookTimer = 2.2f + Rand01() * 4.5f;
            }
            lookCur = Mathf.Lerp(lookCur, lookTarget, 1f - Mathf.Exp(-dt * 2.2f));
        }

        float HeadYawTarget(float bodyYaw)
        {
            float y = lookYaw;
            if (lookAt != null)
            {
                Vector3 d = transform.InverseTransformPoint(lookAt.position);
                if (d.z > -0.3f || Mathf.Abs(d.x) > 0.5f)
                {
                    float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(a) < 110f) return Mathf.Clamp(a - bodyYaw + y, -75f, 75f);
                }
            }
            return Mathf.Clamp(y + lookCur - bodyYaw, -75f, 75f);
        }

        // ------------------------------------------------------------------ pose helpers
        static Quaternion Ax(float deg, Vector3 axis) { return Quaternion.AngleAxis(deg, axis); }

        void Leg(int th, float side, float pitch, float rollOut, float knee, float foot)
        {
            tq[th] = Ax(rollOut * side, Vector3.forward) * Ax(-pitch, Vector3.right);
            tq[th + 1] = Ax(knee, Vector3.right);
            tq[th + 2] = Ax(foot, Vector3.right);
        }

        void Arm(int ua, float side, float pitch, float rollOut, float elbow)
        {
            tq[ua] = Ax(rollOut * side, Vector3.forward) * Ax(-pitch, Vector3.right);
            tq[ua + 1] = Ax(-elbow, Vector3.right);
            tq[ua + 2] = Ax(-elbow * 0.15f - 4f, Vector3.right);
        }

        /// <summary>Skeleton-space point -> chest space (uses tq hips / chest and tHips).</summary>
        Vector3 ToChest(Vector3 sk)
        {
            Quaternion hr = tq[BHips];
            Quaternion cr = hr * tq[BChest];
            Vector3 cp = tHips + hr * rest[BChest];
            return Quaternion.Inverse(cr) * (sk - cp);
        }

        /// <summary>Two-bone IK: wrist to a skeleton-space target (elbow bends down / back / out).</summary>
        void ArmIK(int ua, float side, Vector3 targetSk)
        {
            Vector3 S = rest[ua];
            Vector3 T = ToChest(targetSk);
            float a = PersonMesh.UpperArm, b = PersonMesh.Forearm;
            Vector3 dv = T - S;
            float L = dv.magnitude;
            float maxL = (a + b) * 0.995f, minL = Mathf.Abs(a - b) + 0.03f;
            if (L < 1e-4f) { dv = Vector3.forward * minL; L = minL; }
            Vector3 dn = dv / L;
            L = Mathf.Clamp(L, minL, maxL);
            float x = (a * a - b * b + L * L) / (2f * L);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 pole = new Vector3(side * 0.9f, -1.1f, -0.55f);
            Vector3 pd = Vector3.ProjectOnPlane(pole, dn);
            if (pd.sqrMagnitude < 1e-6f) pd = Vector3.ProjectOnPlane(Vector3.down, dn);
            pd.Normalize();
            Vector3 E = S + dn * x + pd * h;
            Vector3 H = S + dn * L;
            Vector3 ue = E - S, fe = H - E;
            Vector3 hintU = Vector3.ProjectOnPlane(fe, ue);
            if (hintU.sqrMagnitude < 1e-6f) hintU = Vector3.forward;
            Vector3 hintF = Vector3.ProjectOnPlane(S - E, fe);
            if (hintF.sqrMagnitude < 1e-6f) hintF = Vector3.up;
            Quaternion ru = Quaternion.LookRotation(ue, hintU) * O;
            Quaternion rf = Quaternion.LookRotation(fe, hintF) * O;
            tq[ua] = ru;
            tq[ua + 1] = Quaternion.Inverse(ru) * rf;
            tq[ua + 2] = Ax(-8f, Vector3.right);
        }

        float SeatSk(float def)
        {
            float s = seatHeight >= 0f ? seatHeight : def;
            return s / scale + 0.10f;
        }

        // ------------------------------------------------------------------ the poses
        void Evaluate(float dt)
        {
            float t = clock + seedT;
            float breath = Mathf.Sin(t * 1.7f);
            Vector3 hr = rest[BHips];
            propShow = 0f;
            switch (pose)
            {
                case PersonPose.Walk:
                case PersonPose.Run:
                    Gait(dt, t, breath);
                    break;
                case PersonPose.Sit:
                {
                    tHips = new Vector3(hr.x, SeatSk(0.45f), hr.z + 0.02f);
                    tq[BHips] = Quaternion.identity;
                    tq[BChest] = Ax(4f + breath * 1.2f, Vector3.right);
                    Leg(BThL, -1f, 86f, 6f, 84f, -2f);
                    Leg(BThR, 1f, 86f, 6f, 84f, -2f);
                    Arm(BUaL, -1f, 22f + breath * 0.6f, 9f, 52f);
                    Arm(BUaR, 1f, 22f + breath * 0.6f, 9f, 52f);
                    Head(0f, lookPitch * 0.5f + 2f, 4f);
                    break;
                }
                case PersonPose.Drive:
                {
                    tHips = new Vector3(hr.x, SeatSk(0.30f), hr.z);
                    tq[BHips] = Quaternion.identity;
                    tq[BChest] = Ax(-11f + breath * 0.8f, Vector3.right);
                    Leg(BThL, -1f, 74f, 9f, 52f, -16f);
                    Leg(BThR, 1f, 74f, 7f, 48f, -20f);
                    float st = Mathf.Clamp(steer, -1f, 1f) * 95f;
                    Vector3 wc = tHips + new Vector3(0f, 0.50f, 0.40f);
                    const float R = 0.17f;
                    float aL = (-90f + st) * Mathf.Deg2Rad, aR = (90f + st) * Mathf.Deg2Rad;
                    ArmIK(BUaL, -1f, wc + new Vector3(Mathf.Sin(aL) * R, Mathf.Cos(aL) * R, -0.02f));
                    ArmIK(BUaR, 1f, wc + new Vector3(Mathf.Sin(aR) * R, Mathf.Cos(aR) * R, -0.02f));
                    tq[BHead] = Ax(Mathf.Clamp(steer * 18f + lookYaw, -60f, 60f), Vector3.up) * Ax(-4f + 11f * 0.6f, Vector3.right);
                    break;
                }
                case PersonPose.Ride:
                {
                    tHips = new Vector3(hr.x, SeatSk(0.78f), hr.z);
                    tq[BHips] = Ax(Mathf.Clamp(steer, -1f, 1f) * -6f, Vector3.forward);
                    tq[BChest] = Ax(27f + breath * 0.8f, Vector3.right);
                    Leg(BThL, -1f, 50f, 15f, 98f, 34f);
                    Leg(BThR, 1f, 50f, 15f, 98f, 34f);
                    float sy = Mathf.Clamp(steer, -1f, 1f) * 12f;
                    Quaternion bars = Ax(sy, Vector3.up);
                    Vector3 bc = tHips + new Vector3(0f, 0.40f, 0.50f);
                    ArmIK(BUaL, -1f, bc + bars * new Vector3(-0.29f, 0f, 0f));
                    ArmIK(BUaR, 1f, bc + bars * new Vector3(0.29f, 0f, 0f));
                    tq[BHead] = Ax(Mathf.Clamp(steer * 14f + lookYaw, -50f, 50f), Vector3.up) * Ax(-24f, Vector3.right);
                    break;
                }
                case PersonPose.Wave:
                {
                    Stand(t, breath, 0.6f);
                    tq[BChest] = Ax(-4f, Vector3.forward) * tq[BChest];
                    tq[BUaR] = Ax(148f + Mathf.Sin(t * 2.1f) * 4f, Vector3.forward) * Ax(-14f, Vector3.right);
                    tq[BFaR] = Ax(-18f + Mathf.Sin(t * 9f) * 30f, Vector3.forward) * Ax(-12f, Vector3.right);
                    tq[BHandR] = Ax(Mathf.Sin(t * 9f - 0.6f) * 12f, Vector3.forward);
                    tq[BHead] = tq[BHead] * Ax(5f, Vector3.forward);
                    break;
                }
                case PersonPose.Work:
                    if (workStyle == 1) Sweep(t, breath); else Counter(t, breath);
                    break;
                case PersonPose.Pray:
                {
                    Stand(t, breath * 0.6f, 0f);
                    tq[BChest] = Ax(7f + breath * 0.8f, Vector3.right);
                    ArmIK(BUaL, -1f, new Vector3(-0.012f, 1.27f, 0.21f));
                    ArmIK(BUaR, 1f, new Vector3(0.012f, 1.27f, 0.21f));
                    tq[BHandL] = Ax(-60f, Vector3.right) * Ax(-20f, Vector3.up);
                    tq[BHandR] = Ax(-60f, Vector3.right) * Ax(20f, Vector3.up);
                    tq[BHead] = Ax(20f + Mathf.Sin(t * 0.6f) * 2f, Vector3.right);
                    break;
                }
                default:
                    Stand(t, breath, 1f);
                    break;
            }
            if (airW > 0.001f) AirOverlay(airW);
        }

        void Head(float bodyYaw, float pitch, float rollDeg)
        {
            tq[BHead] = Ax(HeadYawTarget(bodyYaw), Vector3.up) * Ax(pitch, Vector3.right) * Ax(rollDeg * Mathf.Sin(clock * 0.31f + seedT), Vector3.forward);
        }

        /// <summary>Idle stance: breathing, slow weight shift, look-around (lookK = how much the head roams).</summary>
        void Stand(float t, float breath, float lookK)
        {
            Vector3 hr = rest[BHips];
            float sway = Mathf.Sin(t * 0.37f);
            float roll = sway * 1.8f;
            tHips = new Vector3(hr.x + sway * 0.012f, hr.y - 0.004f + breath * 0.002f, hr.z);
            tq[BHips] = Ax(roll, Vector3.forward) * Ax(Mathf.Sin(t * 0.23f) * 3f, Vector3.up);
            tq[BChest] = Ax(-roll * 0.6f, Vector3.forward) * Ax(2f + breath * 1.3f, Vector3.right);
            Leg(BThL, -1f, 1f, 2.5f - roll, 3f + Mathf.Max(0f, sway) * 5f, -4f);
            Leg(BThR, 1f, 1f, 2.5f + roll, 3f + Mathf.Max(0f, -sway) * 5f, -4f);
            float sw = Mathf.Sin(t * 0.5f) * 2f;
            Arm(BUaL, -1f, sw, 7f + breath * 0.9f, 10f);
            Arm(BUaR, 1f, -sw, 7f + breath * 0.9f, 10f);
            float savedLook = lookCur;
            lookCur *= lookK;
            Head(0f, lookPitch * lookK - 1f, 3f);
            lookCur = savedLook;
        }

        void Gait(float dt, float t, float breath)
        {
            Vector3 hr = rest[BHips];
            float sp = SpeedForGait();
            float a = Mathf.Clamp01(sp / 0.7f);
            float g = pose == PersonPose.Run ? Mathf.Max(Mathf.InverseLerp(1.6f, 3.2f, sp), 0.35f) : Mathf.InverseLerp(2.3f, 3.6f, sp);
            float stride = Mathf.Lerp(1.35f, 2.5f, g) * scale;
            phase += sp / stride * dt * Mathf.PI * 2f;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
            float sn = Mathf.Sin(phase), cs = Mathf.Cos(phase);
            const float c5 = 0.87758f, s5 = 0.47943f;      // cos / sin 0.5
            float swL = Mathf.Max(0f, cs * c5 - sn * s5), swR = Mathf.Max(0f, -cs * c5 + sn * s5);

            float hAmp = Mathf.Lerp(25f, 40f, g) * a;
            float kBase = Mathf.Lerp(5f, 16f, g) * a, kAmp = Mathf.Lerp(50f, 105f, g) * a;
            float hL = hAmp * sn, hR = -hAmp * sn;
            float kL = kBase + kAmp * swL, kR = kBase + kAmp * swR;
            Leg(BThL, -1f, hL, 2f, kL, (hL - kL) * 0.7f);
            Leg(BThR, 1f, hR, 2f, kR, (hR - kR) * 0.7f);

            float bob = Mathf.Lerp(0.028f, 0.065f, g) * a;
            float c2 = cs * cs - sn * sn;     // cos(2 phase)
            tHips = new Vector3(hr.x, hr.y - 0.035f * g * a + bob * (0.5f * c2 - 0.5f), hr.z);
            float hipYaw = Mathf.Lerp(7f, 4f, g) * a * sn;
            tq[BHips] = Ax(hipYaw, Vector3.up) * Ax(bank * a, Vector3.forward) * Ax(1.5f * a * cs, Vector3.forward);
            float lean = Mathf.Lerp(3f, 13f, g) * a + accelLean * a;
            float chestYaw = -hipYaw * 1.7f;
            tq[BChest] = Ax(chestYaw, Vector3.up) * Ax(lean + breath * 0.6f * (1f - a), Vector3.right);

            float armAmp = Mathf.Lerp(22f, 50f, g) * a;
            float pL = -armAmp * sn + 6f * g, pR = armAmp * sn + 6f * g;
            float eBase = Mathf.Lerp(12f, 88f, g);
            Arm(BUaL, -1f, pL, Mathf.Lerp(7f, 13f, g), eBase + Mathf.Max(0f, pL) * 0.5f * (1f - g) + 12f * g * sn);
            Arm(BUaR, 1f, pR, Mathf.Lerp(7f, 13f, g), eBase + Mathf.Max(0f, pR) * 0.5f * (1f - g) - 12f * g * sn);

            float savedLook = lookCur;
            lookCur *= 0.35f;
            Head(hipYaw + chestYaw, -lean * 0.7f + bob * 40f * c2, 0f);
            lookCur = savedLook;
        }

        void Counter(float t, float breath)
        {
            Stand(t, breath, 0.25f);
            tq[BChest] = Ax(16f + breath * 0.8f, Vector3.right);
            float h = 0.95f / scale;
            float w = t * 3.1f;
            ArmIK(BUaL, -1f, new Vector3(-0.17f, h, 0.40f));
            ArmIK(BUaR, 1f, new Vector3(0.15f + Mathf.Cos(w) * 0.07f, h, 0.40f + Mathf.Sin(w) * 0.05f));
            tq[BHead] = Ax(Mathf.Sin(t * 0.4f) * 10f, Vector3.up) * Ax(18f, Vector3.right);
        }

        void Sweep(float t, float breath)
        {
            Stand(t, breath, 0f);
            float sw = Mathf.Sin(t * 2.4f);
            tq[BHips] = Ax(sw * 5f, Vector3.up);
            tq[BChest] = Ax(sw * 12f, Vector3.up) * Ax(16f + breath * 0.6f, Vector3.right);
            float inv = 1f / scale;
            Vector3 top = new Vector3(0.10f + 0.05f * sw, 1.05f, 0.24f) * inv;
            Vector3 bot = new Vector3(-0.10f + 0.42f * sw, 0.02f, 0.66f) * inv;
            Vector3 dir = bot - top;
            ArmIK(BUaL, -1f, top + dir * 0.06f + new Vector3(-0.03f, 0f, 0f));
            ArmIK(BUaR, 1f, top + dir * 0.40f + new Vector3(0.03f, 0f, 0f));
            // broom bone (child of the chest): handle top at 'top', pointing at 'bot'
            Quaternion hrq = tq[BHips];
            Quaternion cr = hrq * tq[BChest];
            Vector3 dc = Quaternion.Inverse(cr) * dir;
            propPos = ToChest(top) + rest[BProp];
            propRot = Quaternion.LookRotation(dc, Vector3.up) * O;
            propShow = 1f;
            tq[BHead] = Ax(sw * 8f, Vector3.up) * Ax(24f, Vector3.right);
        }

        void AirOverlay(float w)
        {
            float tuck = Mathf.Sin(clock * 3f) * 4f;
            Blend(BThL, Ax(-2f, Vector3.forward) * Ax(-38f - tuck, Vector3.right), w);
            Blend(BShL, Ax(72f, Vector3.right), w);
            Blend(BFtL, Ax(-20f, Vector3.right), w);
            Blend(BThR, Ax(2f, Vector3.forward) * Ax(12f, Vector3.right), w);
            Blend(BShR, Ax(40f, Vector3.right), w);
            Blend(BFtR, Ax(10f, Vector3.right), w);
            Blend(BUaL, Ax(-50f, Vector3.forward) * Ax(-15f, Vector3.right), w);
            Blend(BFaL, Ax(-30f, Vector3.right), w);
            Blend(BUaR, Ax(50f, Vector3.forward) * Ax(-15f, Vector3.right), w);
            Blend(BFaR, Ax(-30f, Vector3.right), w);
            Blend(BChest, Ax(6f, Vector3.right), w);
        }

        void Blend(int b, Quaternion q, float w) { tq[b] = Quaternion.Slerp(tq[b], q, w); }
    }

    public static class PersonFactory
    {
        /// <summary>Create a person at pos facing yaw (degrees). layer: render layer (default 0).</summary>
        public static PersonRig Create(PersonLook look, Vector3 pos, float yaw, Transform parent = null, int layer = 0)
        {
            var root = new GameObject("Person");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            root.layer = layer;
            var rig = root.AddComponent<PersonRig>();
            rig.Init(look, layer);
            return rig;
        }
    }

    // ====================================================================== central animator
    /// <summary>Poses every person once per frame in one place, with distance-based update rates and culling.</summary>
    [DefaultExecutionOrder(950)]
    internal sealed class PersonSystem : MonoBehaviour
    {
        static PersonSystem inst;
        static readonly List<PersonRig> rigs = new List<PersonRig>();
        int frame;
        int lastQuality = -1;
        Camera cam;
        float camTimer;

        public static int Count { get { return rigs.Count; } }

        public static void Register(PersonRig r)
        {
            if (r == null || r.sysIndex >= 0) return;
            if (inst == null && Application.isPlaying)
            {
                var go = new GameObject("PersonSystem");
                go.hideFlags = HideFlags.HideInHierarchy;
                inst = go.AddComponent<PersonSystem>();
            }
            r.sysIndex = rigs.Count;
            rigs.Add(r);
        }

        public static void Unregister(PersonRig r)
        {
            if (r == null) return;
            int i = r.sysIndex;
            if (i < 0 || i >= rigs.Count || rigs[i] != r) { r.sysIndex = -1; return; }
            int last = rigs.Count - 1;
            if (i != last) { rigs[i] = rigs[last]; rigs[i].sysIndex = i; }
            rigs.RemoveAt(last);
            r.sysIndex = -1;
        }

        static float CullDistance(int q)
        {
            switch (q) { case 0: return 75f; case 1: return 115f; case 2: return 160f; default: return 220f; }
        }

        void LateUpdate()
        {
            frame++;
            float dt = Time.deltaTime;
            camTimer -= Time.unscaledDeltaTime;
            if (cam == null || camTimer <= 0f) { cam = Camera.main; camTimer = 1f; }
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            int q = GameSettings.Quality;
            bool shadows = q != lastQuality;
            lastQuality = q;
            float cull = CullDistance(q), cull2 = cull * cull;
            for (int i = rigs.Count - 1; i >= 0; i--)
            {
                if (i >= rigs.Count) continue;
                var r = rigs[i];
                if (r == null) { rigs.RemoveAt(i); continue; }
                if (shadows) r.ApplyShadows();
                r.pendingDt += dt;
                var smr = r.Renderer;
                float d2 = (r.transform.position - eye).sqrMagnitude;
                int every;
                if (r.hero) every = 1;
                else
                {
                    if (((frame + i) & 7) == 0 && smr != null)
                    {
                        bool show = d2 < cull2;
                        if (smr.enabled != show) smr.enabled = show;
                    }
                    if (smr != null && !smr.enabled) every = 32;
                    else if (smr != null && !smr.isVisible) every = 16;
                    else every = d2 < 22f * 22f ? 1 : d2 < 45f * 45f ? 2 : d2 < 90f * 90f ? 4 : 8;
                    if (q == 0 && every < 4 && d2 > 14f * 14f) every *= 2;
                }
                if (every > 1 && ((frame + i) % every) != 0) continue;
                float pd = r.pendingDt;
                r.pendingDt = 0f;
                if (pd <= 0f && r.Renderer != null && r.Renderer.isVisible && frame > 2) continue;
                try { r.Tick(pd); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        void OnDestroy() { if (inst == this) inst = null; }
    }

    // ====================================================================== mesh building
    /// <summary>Skeleton layout and the shared, ref-counted skinned person meshes.</summary>
    internal static class PersonMesh
    {
        public const float UpperArm = 0.28f, Forearm = 0.25f;
        public static readonly string[] BoneNames =
        {
            "Hips", "Chest", "Head", "UpperArm_L", "Forearm_L", "Hand_L", "UpperArm_R", "Forearm_R", "Hand_R",
            "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R", "Prop"
        };
        public static readonly int[] Parent = { -1, 0, 1, 1, 3, 4, 1, 6, 7, 0, 9, 10, 0, 12, 13, 1 };

        static Vector3[] restM, restF;
        static Material mat;

        public static Material Material
        {
            get
            {
                if (mat == null)
                {
                    mat = new Material(MaterialLibrary.VertexColorWind) { name = "BR_Person" };
                    if (mat.HasProperty("_WindStrength")) mat.SetFloat("_WindStrength", 0f);
                    if (mat.HasProperty("_DetailPatterns")) mat.SetFloat("_DetailPatterns", 0f);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f);
                }
                return mat;
            }
        }

        static float ShoulderX(bool female) { return female ? 0.165f : 0.19f; }
        static float HipX(bool female) { return female ? 0.10f : 0.095f; }

        /// <summary>Bone rest positions, local to their parent (reference 1.75 m figure, metres).</summary>
        public static Vector3[] Rest(bool female)
        {
            if (female && restF != null) return restF;
            if (!female && restM != null) return restM;
            float sw = ShoulderX(female), hw = HipX(female);
            var r = new Vector3[PersonRig.N];
            r[PersonRig.BHips] = new Vector3(0f, 0.98f, 0f);
            r[PersonRig.BChest] = new Vector3(0f, 0.14f, 0f);
            r[PersonRig.BHead] = new Vector3(0f, 0.38f, 0f);
            r[PersonRig.BUaL] = new Vector3(-sw, 0.30f, 0f);
            r[PersonRig.BFaL] = new Vector3(0f, -UpperArm, 0f);
            r[PersonRig.BHandL] = new Vector3(0f, -Forearm, 0f);
            r[PersonRig.BUaR] = new Vector3(sw, 0.30f, 0f);
            r[PersonRig.BFaR] = new Vector3(0f, -UpperArm, 0f);
            r[PersonRig.BHandR] = new Vector3(0f, -Forearm, 0f);
            r[PersonRig.BThL] = new Vector3(-hw, -0.06f, 0f);
            r[PersonRig.BShL] = new Vector3(0f, -0.43f, 0f);
            r[PersonRig.BFtL] = new Vector3(0f, -0.41f, 0f);
            r[PersonRig.BThR] = new Vector3(hw, -0.06f, 0f);
            r[PersonRig.BShR] = new Vector3(0f, -0.43f, 0f);
            r[PersonRig.BFtR] = new Vector3(0f, -0.41f, 0f);
            r[PersonRig.BProp] = Vector3.zero;
            if (female) restF = r; else restM = r;
            return r;
        }

        static Vector3[] Model(bool female)
        {
            var r = Rest(female);
            var m = new Vector3[PersonRig.N];
            for (int i = 0; i < PersonRig.N; i++) m[i] = Parent[i] < 0 ? r[i] : m[Parent[i]] + r[i];
            return m;
        }

        // ---- ref-counted cache
        sealed class Entry { public Mesh mesh; public int refs; }
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();

        static int Q(float v) { return Mathf.Clamp(Mathf.RoundToInt(v * 31f), 0, 31); }
        static Color Qc(Color c) { return new Color(Q(c.r) / 31f, Q(c.g) / 31f, Q(c.b) / 31f, 1f); }
        static int Qi(Color c) { return Q(c.r) << 10 | Q(c.g) << 5 | Q(c.b); }

        public static Mesh Acquire(PersonLook l, out string key)
        {
            int outfit = Mathf.Clamp(l.outfit, 0, 6), hs = ((l.hairStyle % 6) + 6) % 6;
            key = outfit + "_" + hs + (l.hat ? "h" : "-") + (l.female ? "f" : "m") + "_" + Qi(l.skin) + "_" + Qi(l.hair) + "_" + Qi(l.top) + "_" +
                  Qi(l.bottom) + "_" + Qi(l.shoes) + "_" + Qi(l.accent);
            Entry e;
            if (!cache.TryGetValue(key, out e) || e.mesh == null)
            {
                l.outfit = outfit; l.hairStyle = hs;
                l.skin = Qc(l.skin); l.hair = Qc(l.hair); l.top = Qc(l.top); l.bottom = Qc(l.bottom); l.shoes = Qc(l.shoes); l.accent = Qc(l.accent);
                e = new Entry { mesh = Build(l, key) };
                cache[key] = e;
            }
            e.refs++;
            return e.mesh;
        }

        public static void Release(string key)
        {
            if (key == null) return;
            Entry e;
            if (!cache.TryGetValue(key, out e)) return;
            e.refs--;
            if (e.refs <= 0)
            {
                cache.Remove(key);
                if (e.mesh != null) Object.Destroy(e.mesh);
            }
        }

        // ---- builder: LifeGeo + one bone (or a 2-bone blend) per vertex
        sealed class Skin
        {
            public readonly LifeGeo g = new LifeGeo();
            public readonly List<BoneWeight> w = new List<BoneWeight>();
            public void Bind(int bone)
            {
                while (w.Count < g.v.Count) w.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            }
            public void Bind(System.Func<Vector3, BoneWeight> f)
            {
                while (w.Count < g.v.Count) w.Add(f(g.v[w.Count]));
            }
        }

        static BoneWeight Two(int a, int b, float wb)
        {
            wb = Mathf.Clamp01(wb);
            if (wb <= 0.001f) return new BoneWeight { boneIndex0 = a, weight0 = 1f };
            if (wb >= 0.5f) return new BoneWeight { boneIndex0 = b, weight0 = wb, boneIndex1 = a, weight1 = 1f - wb };
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - wb, boneIndex1 = b, weight1 = wb };
        }

        /// <summary>Robe / skirt / coat vertices: hips, pulled towards the thigh on the vertex side lower down.</summary>
        static System.Func<Vector3, BoneWeight> SkirtWeights(float top, float bottom, float reach)
        {
            return v =>
            {
                float f = Mathf.InverseLerp(top, bottom, v.y);
                float side = Mathf.Clamp01(Mathf.Abs(v.x) / 0.14f);
                float wt = reach * f * (0.4f + 0.6f * side);
                return Two(PersonRig.BHips, v.x < 0f ? PersonRig.BThL : PersonRig.BThR, wt);
            };
        }

        /// <summary>Long hair / ponytail: head at the top, chest lower down.</summary>
        static System.Func<Vector3, BoneWeight> HairWeights(float top, float bottom)
        {
            return v => Two(PersonRig.BHead, PersonRig.BChest, Mathf.InverseLerp(top, bottom, v.y) * 0.85f);
        }

        static Color32 C(Color c, float k = 1f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * k * 255f, 0f, 255f),
                               (byte)Mathf.Clamp(c.b * k * 255f, 0f, 255f), 0);
        }

        static Vector3 Ell(Vector3 c, Vector3 r, float th, float ph)
        {
            return c + new Vector3(Mathf.Cos(th) * Mathf.Cos(ph) * r.x, Mathf.Sin(th) * r.y, Mathf.Cos(th) * Mathf.Sin(ph) * r.z);
        }

        /// <summary>Ellipsoid shell with the face (front, below browY) and everything below minY left open.</summary>
        static void Cap(LifeGeo g, Vector3 ctr, Vector3 r, int lon, int lat, Color32 top, Color32 bottom, float frontZ, float browY, float minY, float minYBack)
        {
            for (int j = 0; j < lat; j++)
            {
                float th0 = -Mathf.PI * 0.5f + Mathf.PI * j / lat, th1 = -Mathf.PI * 0.5f + Mathf.PI * (j + 1) / lat;
                for (int i = 0; i < lon; i++)
                {
                    float ph0 = Mathf.PI * 2f * i / lon, ph1 = Mathf.PI * 2f * (i + 1) / lon;
                    Vector3 a = Ell(ctr, r, th0, ph0), b = Ell(ctr, r, th0, ph1), d = Ell(ctr, r, th1, ph1), e = Ell(ctr, r, th1, ph0);
                    Vector3 m = (a + b + d + e) * 0.25f;
                    if (m.z > frontZ && m.y < browY) continue;
                    if (m.y < (m.z < -0.04f ? minYBack : minY)) continue;
                    Color32 cl = Color32.Lerp(bottom, top, (j + 0.5f) / lat);
                    g.QuadOut(a, b, d, e, m - ctr, cl, cl, cl, cl);
                }
            }
        }

        static Mesh Build(PersonLook l, string key)
        {
            var M = Model(l.female);
            bool fem = l.female;
            int outfit = l.outfit;
            float sw = ShoulderX(fem), hw = HipX(fem);
            Color32 skin = C(l.skin), hair = C(l.hair), top = C(l.top), topDark = C(l.top, 0.82f), bottom = C(l.bottom), shoes = C(l.shoes);
            Color32 accent = C(l.accent), sole = C(l.shoes, 0.55f), white = LifeGeo.C(240, 238, 232), dark = LifeGeo.C(30, 30, 34);
            if (l.shoes.r + l.shoes.g + l.shoes.b > 2.4f) sole = LifeGeo.C(150, 150, 156);
            var s = new Skin();
            var g = s.g;

            // ---------------------------------------------------------------- outfit parameters
            Color32 torsoCol = top, pelvisCol = bottom, uaCol = top, faCol = top, handCol = skin, thighCol = bottom, shinCol = bottom;
            float uaSkinBelow = -1f;                  // short sleeves: upper arm below this y is skin
            float faWide = 0f;                        // wide sleeves (hanfu / monk)
            float bulk = 0f;                          // puffy coat
            bool skirt = false, longRobe = false, coat = false, apron = false, jacket = false, helmet = false, shaved = false;
            bool boots = false, gloves = false, scarf = false, tie = false, crossCollar = false, sash = false, suitStripe = false;
            float skirtBottom = 0.55f, skirtR = 0.22f;
            switch (outfit)
            {
                case 1:   // rally suit
                    pelvisCol = thighCol = shinCol = top; suitStripe = true; gloves = true; boots = true; helmet = l.hat;
                    break;
                case 2:   // shopkeeper
                    faCol = skin; apron = true;
                    break;
                case 3:   // monk robe
                    shaved = true; longRobe = true; faWide = 0.03f; pelvisCol = thighCol = top; shinCol = skin; sash = true; skirtBottom = 0.13f; skirtR = 0.27f;
                    break;
                case 4:   // business
                    jacket = true; tie = true; pelvisCol = thighCol = shinCol = bottom;
                    if (fem) { skirt = true; skirtBottom = 0.50f; skirtR = 0.165f; thighCol = bottom; shinCol = skin; }
                    break;
                case 5:   // winter coat
                    coat = true; bulk = 0.025f; scarf = true; boots = true;
                    break;
                case 6:   // hanfu
                    longRobe = true; crossCollar = true; sash = true; faWide = 0.065f; pelvisCol = thighCol = bottom; shinCol = bottom;
                    skirtBottom = 0.10f; skirtR = 0.29f;
                    break;
                default:  // casual
                    uaSkinBelow = 1.27f; faCol = skin;
                    if (fem && l.hairStyle >= 3) { skirt = true; skirtBottom = 0.52f; skirtR = 0.24f; thighCol = bottom; shinCol = skin; }
                    break;
            }
            if (gloves) handCol = dark;

            // ---------------------------------------------------------------- head (head bone)
            Vector3 hc = new Vector3(0f, 1.635f, 0.008f);
            Vector3 hrad = new Vector3(0.112f, 0.128f, 0.118f);
            g.Frustum(new Vector3(0f, 1.45f, -0.005f), new Vector3(0f, 1.56f, 0f), 0.05f, 0.048f, 0.046f, 0.044f, 6, 0f, skin, skin, false, false);
            g.Ellipsoid(hc, hrad, 8, 6, skin, LifeGeo.Shade(skin, 0.93f));
            // face hint: eyes, brows, cheeks, nose
            Color32 eye = LifeGeo.C(38, 32, 40);
            Color32 blush = Color32.Lerp(skin, LifeGeo.C(242, 128, 140), 0.45f);
            for (int k = -1; k <= 1; k += 2)
            {
                g.Disc(new Vector3(k * 0.041f, 1.638f, 0.1195f), 0.0165f, 6, 0f, new Vector3(k * 0.3f, 0f, 0.95f).normalized, eye, eye);
                g.Disc(new Vector3(k * 0.069f, 1.604f, 0.1035f), 0.0175f, 6, 0f, new Vector3(k * 0.55f, -0.1f, 0.83f).normalized, blush, blush);
                if (!shaved) g.Box(new Vector3(k * 0.042f, 1.673f, 0.1155f), new Vector3(0.022f, 0.0055f, 0.006f), hair);
            }
            g.Box(new Vector3(0f, 1.616f, 0.124f), new Vector3(0.011f, 0.016f, 0.011f), LifeGeo.Shade(skin, 0.94f));

            // hair
            if (!shaved && !helmet)
            {
                Color32 hairTop = LifeGeo.Shade(hair, 1.12f);
                switch (l.hairStyle)
                {
                    case 5:   // bob
                        Cap(g, new Vector3(0f, 1.642f, -0.006f), new Vector3(0.13f, 0.136f, 0.131f), 8, 6, hairTop, hair, 0.05f, 1.70f, 1.535f, 1.52f);
                        break;
                    default:
                        Cap(g, new Vector3(0f, 1.652f, -0.004f), new Vector3(0.121f, 0.125f, 0.126f), 8, 6, hairTop, hair, 0.045f, 1.695f, 1.585f, 1.545f);
                        break;
                }
                if (l.hairStyle == 1) g.Ellipsoid(new Vector3(0.02f, 1.748f, 0.062f), new Vector3(0.088f, 0.036f, 0.066f), 6, 4, hairTop, hair);
                if (l.hairStyle == 2) g.Ellipsoid(new Vector3(0f, 1.778f, -0.052f), new Vector3(0.058f, 0.055f, 0.058f), 6, 4, hairTop, hair);
                s.Bind(PersonRig.BHead);
                if (l.hairStyle == 3)
                {
                    g.Ellipsoid(new Vector3(0f, 1.69f, -0.118f), new Vector3(0.03f, 0.03f, 0.03f), 5, 3, accent, accent);
                    g.Frustum(new Vector3(0f, 1.69f, -0.13f), new Vector3(0f, 1.46f, -0.175f), 0.045f, 0.04f, 0.016f, 0.014f, 6, 0f, hair, hair, true, true);
                    s.Bind(HairWeights(1.66f, 1.44f));
                }
                if (l.hairStyle == 4)
                {
                    g.Frustum(new Vector3(0f, 1.64f, -0.035f), new Vector3(0f, 1.34f, -0.08f), 0.126f, 0.088f, 0.13f, 0.045f, 8, 0f, hair, LifeGeo.Shade(hair, 0.9f), false, true);
                    s.Bind(HairWeights(1.60f, 1.36f));
                }
            }
            // hats
            if (helmet)
            {
                Color32 hb = C(Color.Lerp(l.top, Color.white, 0.3f)), visor = LifeGeo.C(20, 24, 36);
                g.Paint = c =>
                {
                    if (c.z > 0.07f && c.y > 1.585f && c.y < 1.68f && Mathf.Abs(c.x) < 0.115f) return visor;
                    if (Mathf.Abs(c.x) < 0.032f && c.y > 1.68f) return accent;
                    return hb;
                };
                g.Ellipsoid(new Vector3(0f, 1.645f, 0.006f), new Vector3(0.143f, 0.153f, 0.151f), 10, 7, hb, hb);
                g.Paint = null;
            }
            else if (l.hat && !shaved)
            {
                switch (outfit)
                {
                    case 2:   // cloth cap
                        Cap(g, new Vector3(0f, 1.72f, -0.004f), new Vector3(0.129f, 0.075f, 0.133f), 8, 4, accent, LifeGeo.Shade(accent, 0.9f), 1f, 0f, 1.715f, 1.715f);
                        g.Box(new Vector3(0f, 1.718f, 0.125f), new Vector3(0.085f, 0.007f, 0.05f), LifeGeo.Shade(accent, 0.8f));
                        break;
                    case 4:
                        break;
                    case 5:   // beanie + pompom
                        Cap(g, new Vector3(0f, 1.675f, -0.004f), new Vector3(0.131f, 0.118f, 0.134f), 8, 5, accent, LifeGeo.Shade(accent, 0.88f), 1f, 0f, 1.655f, 1.655f);
                        g.Ellipsoid(new Vector3(0f, 1.80f, -0.01f), new Vector3(0.038f, 0.036f, 0.038f), 6, 4, white, white);
                        break;
                    case 6:
                        if (!fem)
                        {
                            g.Box(new Vector3(0f, 1.762f, -0.012f), new Vector3(0.074f, 0.048f, 0.085f), dark);
                            g.Box(new Vector3(0f, 1.79f, -0.095f), new Vector3(0.062f, 0.06f, 0.014f), dark);
                            break;
                        }
                        goto default;
                    default:  // conical straw hat
                    {
                        Color32 straw = LifeGeo.C(218, 188, 122), strawD = LifeGeo.C(178, 148, 92);
                        g.Frustum(new Vector3(0f, 1.712f, 0f), new Vector3(0f, 1.885f, 0f), 0.31f, 0.31f, 0.012f, 0.012f, 10, 0f, strawD, straw, true, false);
                        g.Frustum(new Vector3(0f, 1.70f, 0f), new Vector3(0f, 1.73f, 0f), 0.125f, 0.13f, 0.125f, 0.13f, 8, 0f, accent, accent, false, false);
                        break;
                    }
                }
            }
            s.Bind(PersonRig.BHead);

            // ---------------------------------------------------------------- torso (chest bone)
            float tb = fem ? 0.128f : 0.145f, tm = fem ? 0.152f : 0.172f, tt = fem ? 0.15f : 0.176f;
            float db = fem ? 0.094f : 0.10f, dm = fem ? 0.112f : 0.116f, dt = fem ? 0.098f : 0.104f;
            tb += bulk; tm += bulk; tt += bulk; db += bulk; dm += bulk; dt += bulk;
            Color32 tTop = top;
            System.Func<Vector3, Color32> paint = null;
            if (suitStripe) paint = c => (c.y > 1.29f && c.y < 1.345f) ? accent : top;
            else if (jacket)
                paint = c =>
                {
                    if (c.z > 0.05f && c.y > 1.16f)
                    {
                        if (Mathf.Abs(c.x) < 0.024f && tie) return accent;
                        if (Mathf.Abs(c.x) < (c.y - 1.16f) * 0.42f) return white;
                    }
                    return top;
                };
            else if (crossCollar)
                paint = c =>
                {
                    if (c.y > 0.985f && c.y < 1.075f) return accent;                  // sash
                    if (c.z > 0.03f && Mathf.Abs(c.x + (c.y - 1.30f) * 0.75f) < 0.03f && c.y > 1.08f) return accent;   // wrap collar
                    return top;
                };
            else if (sash) paint = c => Mathf.Abs(c.x * 0.85f - (c.y - 1.22f)) < 0.045f ? accent : top;
            g.Paint = paint;
            g.Frustum(new Vector3(0f, 1.0f, 0f), new Vector3(0f, 1.29f, 0f), tb, db, tm, dm, 12, Mathf.PI / 12f, top, top, true, false);
            g.Frustum(new Vector3(0f, 1.29f, 0f), new Vector3(0f, 1.465f, -0.005f), tm, dm, tt, dt, 12, Mathf.PI / 12f, tTop, tTop, false, true);
            g.Paint = null;
            if (apron)
            {
                g.Box(new Vector3(0f, 1.19f, dm + 0.008f), new Vector3(0.115f, 0.165f, 0.008f), accent);
                g.Box(new Vector3(0f, 1.425f, 0f), new Vector3(0.09f, 0.008f, dt + 0.006f), accent);  // neck strap
            }
            if (scarf)
            {
                g.Frustum(new Vector3(0f, 1.43f, 0f), new Vector3(0f, 1.50f, 0.005f), 0.085f, 0.08f, 0.07f, 0.066f, 8, 0f, accent, accent, true, true);
                g.Box(new Vector3(0.05f, 1.33f, dt + 0.02f), new Vector3(0.03f, 0.1f, 0.012f), LifeGeo.Shade(accent, 0.9f));
            }
            if (outfit == 1) g.Frustum(new Vector3(0f, 1.44f, 0f), new Vector3(0f, 1.50f, 0f), 0.06f, 0.056f, 0.055f, 0.052f, 6, 0f, dark, dark, false, true);
            s.Bind(PersonRig.BChest);

            // ---------------------------------------------------------------- pelvis (hips bone)
            float pb = fem ? 0.158f : 0.148f, pt = fem ? 0.138f : 0.145f;
            g.Frustum(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 1.03f, 0f), pb + bulk, 0.10f + bulk, pt + bulk, 0.098f + bulk, 10, 0f, pelvisCol, pelvisCol, true, true);
            if (sash || apron || outfit == 0 || outfit == 4)
            {
                Color32 belt = apron ? accent : sash ? accent : LifeGeo.Shade(pelvisCol, 0.6f);
                if (!crossCollar) g.Frustum(new Vector3(0f, 0.985f, 0f), new Vector3(0f, 1.02f, 0f), pt + 0.006f, 0.104f, pt + 0.004f, 0.103f, 10, 0f, belt, belt, false, false);
            }
            s.Bind(PersonRig.BHips);
            if (skirt || longRobe)
            {
                Color32 sc = longRobe ? (outfit == 3 ? top : bottom) : bottom;
                g.Frustum(new Vector3(0f, 1.0f, 0f), new Vector3(0f, skirtBottom, 0f), pb + 0.006f, 0.108f, skirtR, skirtR * 0.82f, 10, 0f, sc, LifeGeo.Shade(sc, 0.9f), false, false);
                s.Bind(SkirtWeights(1.0f, skirtBottom, longRobe ? 0.62f : 0.55f));
            }
            if (coat || jacket)
            {
                float cb = coat ? 0.58f : 0.80f, cr = coat ? 0.205f : 0.165f;
                g.Frustum(new Vector3(0f, 1.02f, 0f), new Vector3(0f, cb, 0f), pt + bulk + 0.01f, 0.11f + bulk, cr, cr * 0.75f, 10, 0f, top, topDark, false, false);
                s.Bind(SkirtWeights(1.0f, cb, coat ? 0.5f : 0.35f));
            }
            if (apron)
            {
                g.Box(new Vector3(-0.075f, 0.75f, 0.125f), new Vector3(0.075f, 0.24f, 0.008f), accent);
                g.Box(new Vector3(0.075f, 0.75f, 0.125f), new Vector3(0.075f, 0.24f, 0.008f), accent);
                s.Bind(v => Two(PersonRig.BHips, v.x < 0f ? PersonRig.BThL : PersonRig.BThR, Mathf.InverseLerp(0.99f, 0.51f, v.y) * 0.7f));
            }

            // ---------------------------------------------------------------- arms
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                int ua = k == 0 ? PersonRig.BUaL : PersonRig.BUaR;
                Vector3 sh = M[ua], el = M[ua + 1], wr = M[ua + 2];
                float rU = 0.052f + bulk * 0.6f, rE = 0.045f + bulk * 0.6f, rW = 0.036f + bulk * 0.4f;
                // shoulder ball + upper arm
                g.Ellipsoid(sh + new Vector3(-side * 0.015f, -0.005f, 0f), new Vector3(0.062f, 0.058f, 0.062f) * (fem ? 0.92f : 1f) + Vector3.one * bulk, 6, 3, uaCol, uaCol);
                if (uaSkinBelow > 0f)
                {
                    float y = Mathf.Lerp(sh.y, el.y, Mathf.InverseLerp(sh.y, el.y, uaSkinBelow));
                    Vector3 mid = new Vector3(sh.x, y, sh.z);
                    g.Frustum(sh, mid, rU + 0.006f, rU + 0.006f, rU + 0.004f, rU + 0.004f, 6, 0f, uaCol, uaCol, false, true);
                    g.Frustum(mid, el, rU - 0.006f, rU - 0.006f, rE, rE, 6, 0f, skin, skin, false, false);
                }
                else g.Frustum(sh, el, rU, rU, rE, rE, 6, 0f, uaCol, uaCol, false, false);
                s.Bind(ua);
                // forearm (+ wide sleeve)
                if (faWide > 0f)
                {
                    g.Frustum(el, wr + Vector3.down * 0.02f, rE + 0.004f, rE + 0.004f, rW + faWide, rW + faWide * 0.8f, 7, 0f, top, crossCollar ? accent : topDark, true, false);
                    g.Frustum(wr + Vector3.up * 0.04f, wr, rW, rW, rW - 0.004f, rW - 0.004f, 5, 0f, skin, skin, false, false);
                }
                else
                {
                    Color32 cuff = faCol.Equals(skin) ? skin : (outfit == 1 ? accent : faCol);
                    g.Frustum(el, wr, rE, rE, rW, rW, 6, 0f, faCol, cuff, true, false);
                    if (jacket) g.Frustum(wr + Vector3.up * 0.03f, wr + Vector3.down * 0.005f, rW + 0.002f, rW + 0.002f, rW - 0.002f, rW - 0.002f, 6, 0f, white, white, false, false);
                }
                s.Bind(ua + 1);
                // hand
                Vector3 hand = wr + new Vector3(0f, -0.052f, 0.006f);
                g.Ellipsoid(hand, new Vector3(0.031f, 0.056f, 0.042f), 5, 3, handCol, LifeGeo.Shade(handCol, 0.92f));
                g.Ellipsoid(hand + new Vector3(-side * 0.022f, 0.02f, 0.03f), new Vector3(0.012f, 0.026f, 0.012f), 4, 2, handCol, handCol);
                s.Bind(ua + 2);
            }

            // ---------------------------------------------------------------- legs
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                int th = k == 0 ? PersonRig.BThL : PersonRig.BThR;
                Vector3 hip = M[th], knee = M[th + 1], ank = M[th + 2];
                Color32 tc = skirt ? (longRobe ? thighCol : skin) : thighCol;
                if (skirt && skirtBottom < 0.6f) tc = skin;
                if (outfit == 4 && fem) tc = skin;
                Color32 stripe = suitStripe ? accent : tc;
                g.Paint = suitStripe ? (System.Func<Vector3, Color32>)(c => Mathf.Abs(c.x - side * (hw + 0.07f)) < 0.025f ? stripe : tc) : null;
                g.Frustum(hip + new Vector3(0f, 0.035f, 0f), knee, 0.078f + (fem ? 0.004f : 0f) + bulk * 0.3f, 0.078f, 0.058f, 0.056f, 6, 0f, tc, tc, true, false);
                g.Paint = null;
                s.Bind(th);
                Color32 shc = shinCol;
                if (boots)
                {
                    g.Frustum(knee, ank + Vector3.up * 0.16f, 0.057f, 0.055f, 0.048f, 0.046f, 6, 0f, shc, shc, false, false);
                    g.Frustum(ank + Vector3.up * 0.16f, ank, 0.056f, 0.056f, 0.052f, 0.052f, 6, 0f, shoes, shoes, true, false);
                }
                else g.Frustum(knee, ank, 0.056f, 0.054f, 0.043f, 0.042f, 6, 0f, shc, shc, false, false);
                s.Bind(th + 1);
                bool sandal = outfit == 3;
                Color32 shoeTop = sandal ? skin : shoes;
                g.Box(new Vector3(ank.x, 0.048f, 0.04f), new Vector3(0.049f, 0.04f, 0.108f), shoeTop);
                g.Box(new Vector3(ank.x, 0.008f, 0.042f), new Vector3(0.052f, 0.009f, 0.112f), sandal ? shoes : sole);
                s.Bind(th + 2);
            }

            // ---------------------------------------------------------------- broom (prop bone, child of the chest; shown only while sweeping)
            {
                Vector3 o = M[PersonRig.BProp];
                Color32 wood = LifeGeo.C(170, 120, 72), straw = LifeGeo.C(214, 182, 104);
                g.Frustum(o + Vector3.up * 0.06f, o + Vector3.down * 0.9f, 0.016f, 0.016f, 0.016f, 0.016f, 5, 0f, wood, wood, true, false);
                g.Frustum(o + Vector3.down * 0.86f, o + Vector3.down * 1.12f, 0.04f, 0.03f, 0.13f, 0.04f, 6, 0f, straw, LifeGeo.Shade(straw, 0.85f), true, true);
                s.Bind(PersonRig.BProp);
            }

            // ---------------------------------------------------------------- mesh
            var mesh = new Mesh { name = "Person_" + key };
            int n = g.v.Count;
            var cols = new Color32[n];
            for (int i = 0; i < n; i++) { var c = g.c[i]; c.a = 13; cols[i] = c; }   // alpha 0.05: no wind, no detail patterns
            mesh.SetVertices(g.v);
            mesh.SetNormals(g.n);
            mesh.colors32 = cols;
            mesh.SetTriangles(g.t, 0);
            mesh.boneWeights = s.w.ToArray();
            var bind = new Matrix4x4[PersonRig.N];
            for (int i = 0; i < PersonRig.N; i++) bind[i] = Matrix4x4.Translate(-M[i]);
            mesh.bindposes = bind;
            mesh.bounds = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1.4f, 2.0f, 1.4f));
            return mesh;
        }
    }
}

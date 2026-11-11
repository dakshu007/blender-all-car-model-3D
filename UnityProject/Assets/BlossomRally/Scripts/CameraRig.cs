// CameraRig.cs - Chase (drift-aware), Cockpit (driver view), Hood and Cinematic orbit cameras.
// Chase feel: the camera swings out to the outside of a slide and looks along the direction of
// travel, rolls slightly into the corner, widens its FOV with speed / drift, eases its follow
// distance with speed and buzzes subtly at high speed (Perlin).
// Reactions ("juice"), all layered on top of an un-shaken "base" pose so they never feed back into the
// follow smoothing:
//  * every bump: suspension compression of the wheels kicks a small vertical spring
//  * jump take-off (GameEvents.TakeOff): eases back / up and widens the FOV while airborne
//  * landing (GameEvents.Landed): spring dip + nod scaled by the impact speed, a short FOV squash
//  * impacts (GameEvents.Impact): directional jolt away from the hit plus a shake biased along it
//  * gravel / dirt (and a little grass): subtle high-frequency surface vibration
// ReducedMotion (JuiceSettings, PlayerPrefs) turns off shakes, rolls, vibration and jolts and softens dips.
// Set externalControl to hand the camera to replay / photo mode / the finish-line orbit.
using UnityEngine;

namespace BlossomRally
{
    public enum CamMode { Chase = 0, Cockpit = 1, Hood = 2, Cinematic = 3 }

    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public ArcadeCar target;
        public CamMode mode = CamMode.Chase;
        public bool showroom;              // slow orbit in the car-select screen
        /// <summary>When true the rig does nothing (replay / photo mode drive the camera).</summary>
        public bool externalControl;

        /// <summary>Accessibility: no shakes / rolls / vibration / jolts (stored in PlayerPrefs, same as JuiceSettings.ReducedMotion).</summary>
        public static bool ReducedMotion
        {
            get { return JuiceSettings.ReducedMotion; }
            set { JuiceSettings.ReducedMotion = value; }
        }

        Camera cam;
        Vector3 vel;
        Vector3 smoothDir = Vector3.forward;
        float orbit;
        float shake;

        // un-shaken pose the follow logic works on; offsets are added when writing the transform
        Vector3 basePos;
        Quaternion baseRot = Quaternion.identity;
        Vector3 lastOutPos;
        Quaternion lastOutRot = Quaternion.identity;
        bool hasBase;

        float followDist = 7f, distVel;
        float swing;                       // lateral offset towards the outside of a slide (m)
        float roll;                        // degrees, + = horizon banks with a left-hand corner
        float bumpPos, bumpVel;            // landing dip / suspension bump spring (m)
        Vector3 kickPos, kickVel;          // directional impact jolt spring (m, world)
        Vector3 shakeDir = Vector3.up;     // impact shake bias
        float fovKick;                     // landing FOV squash (deg, decays)
        bool airborne;                     // between TakeOff and Landed
        float airPull, groundedFor;        // 0..1 eased "pull back" while airborne
        float vibration;                   // smoothed surface vibration 0..1
        float pendingDip;                  // landing impact queued by the event (applied in LateUpdate)
        Vector3 lastVel;
        float[] prevSpring = new float[0];
        float noiseT;
        ArcadeCar lastTarget;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 1500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            ApplyCullDistances();
            cam.layerCullSpherical = true;
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplyCullDistances;
            GameEvents.TakeOff += OnTakeOff;
            GameEvents.Landed += OnLanded;
            GameEvents.Impact += OnImpact;
            ApplyCullDistances();
        }

        void OnDisable()
        {
            GameSettings.Changed -= ApplyCullDistances;
            GameEvents.TakeOff -= OnTakeOff;
            GameEvents.Landed -= OnLanded;
            GameEvents.Impact -= OnImpact;
        }

        // ---------------------------------------------------------------- gameplay reactions
        void OnTakeOff(Vector3 p)
        {
            if (target == null || (p - target.transform.position).sqrMagnitude > 100f) return;
            airborne = true;
            groundedFor = 0f;
        }

        void OnLanded(float air, float vy)
        {
            if (target == null) return;
            airborne = false;
            float impact = Mathf.Clamp01(Mathf.Abs(vy) / 8f);
            if (air < 0.12f) impact *= 0.3f;
            pendingDip = Mathf.Max(pendingDip, impact);
        }

        void OnImpact(float speed, Vector3 point)
        {
            if (target == null || ReducedMotion) return;
            float amount = Mathf.Clamp01(speed / 25f);
            Vector3 dir = target.transform.position - point;
            if (dir.sqrMagnitude < 0.09f)
            {
                // the event carries the car position: use the change of velocity (the shove) instead
                Rigidbody rb = target.Body;
                Vector3 dv = rb != null ? rb.GetVelocity() - lastVel : Vector3.zero;
                dir = dv.sqrMagnitude > 0.25f ? dv : -target.transform.forward;
            }
            dir.Normalize();
            shakeDir = dir;
            kickVel += dir * (0.6f + 2.2f * amount);
        }

        void ApplyCullDistances()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            var d = new float[32];
            d[WorldBuilder.LayerGrass] = GameSettings.GrassCullDistance;
            d[WorldBuilder.LayerTrees] = GameSettings.TreeCullDistance;
            cam.layerCullDistances = d;
        }

        public void Cycle()
        {
            mode = (CamMode)(((int)mode + 1) % 4);
        }

        public void Shake(float amount)
        {
            if (ReducedMotion) return;
            shake = Mathf.Max(shake, amount);
        }

        public void SnapBehind()
        {
            if (target == null) return;
            smoothDir = Flat(target.transform.forward);
            transform.position = target.transform.position - smoothDir * 7f + Vector3.up * 2.4f;
            transform.LookAt(target.transform.position + Vector3.up * 1.1f);
            vel = Vector3.zero;
            followDist = 7f;
            distVel = 0f;
            swing = 0f;
            roll = 0f;
            bumpPos = bumpVel = 0f;
            kickPos = kickVel = Vector3.zero;
            shake = fovKick = pendingDip = airPull = vibration = 0f;
            airborne = false;
            SyncSprings();
            basePos = lastOutPos = transform.position;
            baseRot = lastOutRot = transform.rotation;
            hasBase = true;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        static float Noise(float t, float seed)
        {
            return (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;
        }

        void LateUpdate()
        {
            if (externalControl) { hasBase = false; return; }
            if (target == null) return;
            if (cam == null) cam = GetComponent<Camera>();
            Transform t = target.transform;
            Rigidbody rb = target.Body;
            Vector3 v = rb != null ? rb.GetVelocity() : Vector3.zero;
            float speed = v.magnitude;
            float dt = Time.deltaTime;

            if (target != lastTarget)
            {
                lastTarget = target;
                airborne = false;
                bumpPos = bumpVel = 0f;
                kickPos = kickVel = Vector3.zero;
                SyncSprings();
            }
            // someone else moved the camera (replay / photo exit, teleports): continue from there
            if (!hasBase || (transform.position - lastOutPos).sqrMagnitude > 1e-6f || Quaternion.Angle(transform.rotation, lastOutRot) > 0.05f)
            {
                basePos = transform.position;
                baseRot = transform.rotation;
                hasBase = true;
            }

            bool reduced = ReducedMotion;
            UpdateReactions(v, dt, reduced);

            float offsetScale = 0f;        // how much shake / bump / buzz the current view gets
            float rollTarget = 0f;
            if (showroom)
            {
                orbit += dt * 14f;
                Quaternion q = Quaternion.Euler(0f, orbit, 0f);
                Vector3 pos = t.position + q * new Vector3(0f, 1.6f, -7.2f);
                basePos = Vector3.Lerp(basePos, pos, dt * 3f);
                baseRot = Quaternion.LookRotation(t.position + Vector3.up * 0.7f - basePos);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 42f, dt * 3f);
            }
            else
            {
                switch (mode)
                {
                    case CamMode.Chase:
                    {
                        float drift = target.DriftIntensity;
                        Vector3 flatV = new Vector3(v.x, 0f, v.z);
                        Vector3 fwd = Flat(t.forward);
                        Vector3 want = fwd;
                        bool moving = speed > 5f && target.ForwardSpeed > 0f;
                        if (moving)    // look where the car is going: the harder it slides, the more the velocity wins
                            want = Vector3.Slerp(fwd, Flat(flatV), 0.35f + 0.5f * drift).normalized;
                        smoothDir = Vector3.Slerp(smoothDir, want, 1f - Mathf.Exp(-dt * (4.5f - 1.6f * drift))).normalized;

                        // swing out to the outside of the slide (the side the velocity points to, relative to the nose)
                        float slipSide = moving ? Mathf.Clamp(target.SlipAngle / 30f, -1f, 1f) : 0f;
                        float swingTarget = slipSide * drift * 1.8f;
                        swing = Mathf.Lerp(swing, swingTarget, 1f - Mathf.Exp(-dt * 2.5f));
                        rollTarget = reduced ? 0f : slipSide * drift * 3.5f;

                        // smooth, speed-dependent follow distance
                        float distTarget = 6.4f + Mathf.Clamp01(speed / 55f) * 1.9f + drift * 0.5f + airPull * 0.9f;
                        followDist = Mathf.SmoothDamp(followDist, distTarget, ref distVel, 0.6f);

                        Vector3 right = Vector3.Cross(Vector3.up, smoothDir).normalized;
                        Vector3 desired = t.position - smoothDir * followDist + right * swing
                                          + Vector3.up * (2.2f + Mathf.Clamp01(speed / 60f) * 0.4f + airPull * 0.45f);
                        Vector3 look = t.position + Vector3.up * 1.05f + flatV * (0.06f + 0.04f * drift);
                        // keep the camera out of hills and buildings
                        RaycastHit hit;
                        Vector3 dir = desired - look;
                        if (Physics.SphereCast(look, 0.3f, dir.normalized, out hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                            && hit.rigidbody != rb)
                            desired = look + dir.normalized * Mathf.Max(1.5f, hit.distance - 0.2f);
                        basePos = Vector3.SmoothDamp(basePos, desired, ref vel, 0.12f);
                        Vector3 lookDir = look - basePos;
                        if (lookDir.sqrMagnitude > 1e-4f)
                            baseRot = Quaternion.Slerp(baseRot, Quaternion.LookRotation(lookDir), 1f - Mathf.Exp(-dt * 12f));
                        float fov = 60f + Mathf.Clamp01(target.SpeedKmh / 190f) * 14f + drift * 6f + airPull * 4f + fovKick;
                        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 3f));
                        offsetScale = 1f;
                        break;
                    }
                    case CamMode.Cockpit:
                    {
                        Vector3 seat = new Vector3(-0.36f, 1.06f, 0.05f);
                        basePos = t.TransformPoint(seat);
                        baseRot = Quaternion.LookRotation(t.forward, t.up) * Quaternion.Euler(4f, target.SteerAngle * 0.15f, 0f);
                        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 72f, dt * 4f);
                        offsetScale = 0.45f;
                        break;
                    }
                    case CamMode.Hood:
                    {
                        basePos = t.TransformPoint(new Vector3(0f, 1.25f, 1.1f));
                        baseRot = Quaternion.LookRotation(t.forward, t.up) * Quaternion.Euler(3f, 0f, 0f);
                        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 66f, dt * 4f);
                        offsetScale = 0.6f;
                        break;
                    }
                    case CamMode.Cinematic:
                    {
                        orbit += dt * 9f;
                        Quaternion q = Quaternion.Euler(0f, orbit, 0f);
                        Vector3 pos = t.position + q * new Vector3(0f, 1.3f, -9f);
                        basePos = Vector3.Lerp(basePos, pos, dt * 2f);
                        baseRot = Quaternion.LookRotation(t.position + Vector3.up * 0.9f - basePos);
                        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 38f, dt * 2f);
                        offsetScale = 0.3f;
                        break;
                    }
                }
            }
            if (showroom || mode != CamMode.Chase) swing = Mathf.Lerp(swing, 0f, 1f - Mathf.Exp(-dt * 3f));
            roll = Mathf.Lerp(roll, rollTarget, 1f - Mathf.Exp(-dt * 3f));

            // ---- layered offsets ----
            Vector3 outPos = basePos;
            Quaternion outRot = baseRot;
            noiseT += dt;
            if (!reduced && !showroom)
            {
                // subtle high-speed buzz (low amplitude, smooth noise)
                float buzz = Mathf.Clamp01((target.SpeedKmh - 110f) / 120f) * offsetScale;
                if (buzz > 0.001f)
                {
                    float nx = Noise(noiseT * 9f, 1.7f), ny = Noise(3.1f, noiseT * 9f);
                    outPos += outRot * new Vector3(nx, ny, 0f) * (0.028f * buzz);
                    outRot = outRot * Quaternion.Euler(ny * 0.22f * buzz, nx * 0.18f * buzz, 0f);
                }
                // surface vibration on gravel / dirt: fine, fast and tiny (it should be felt, not seen)
                float vib = vibration * Mathf.Max(offsetScale, 0.5f);
                if (vib > 0.001f)
                {
                    float f = noiseT * 31f;
                    float vx = Noise(f, 4.4f), vy = Noise(7.7f, f);
                    outPos += outRot * new Vector3(vx * 0.4f, vy, 0f) * (0.011f * vib);
                    outRot = outRot * Quaternion.Euler(vy * 0.14f * vib, 0f, vx * 0.1f * vib);
                }
                // impact shake (Shake(float)), biased along the direction of the last hit
                if (shake > 0.001f)
                {
                    float f = noiseT * 22f;
                    Vector3 n3 = new Vector3(Noise(f, 0.3f), Noise(5.2f, f), Noise(f, 9.4f));
                    outPos += (n3 * 0.6f + shakeDir * Noise(f, 2.6f) * 0.8f) * (shake * 0.12f);
                }
                // directional impact jolt (+ a matching tilt)
                if (kickPos.sqrMagnitude > 1e-6f)
                {
                    float s = Mathf.Max(offsetScale, 0.5f);
                    outPos += kickPos * s;
                    Vector3 lk = Quaternion.Inverse(outRot) * kickPos;
                    outRot = outRot * Quaternion.Euler(lk.y * 8f * s, 0f, -lk.x * 10f * s);
                }
            }
            if (shake > 0f) shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            // landing dip / bumps with a little nod
            if (Mathf.Abs(bumpPos) > 1e-4f && !showroom)
            {
                float s = Mathf.Max(offsetScale, 0.4f);
                outPos += Vector3.up * bumpPos * s;
                if (!reduced) outRot = outRot * Quaternion.Euler(-bumpPos * 10f * s, 0f, 0f);
            }
            // roll into the slide
            if (!reduced && Mathf.Abs(roll) > 1e-3f) outRot = outRot * Quaternion.Euler(0f, 0f, roll);

            transform.SetPositionAndRotation(outPos, outRot);
            lastOutPos = transform.position;
            lastOutRot = transform.rotation;
        }

        void SyncSprings()
        {
            if (target == null || target.wheels == null) { prevSpring = new float[0]; return; }
            if (prevSpring.Length != target.wheels.Length) prevSpring = new float[target.wheels.Length];
            for (int i = 0; i < prevSpring.Length; i++) prevSpring[i] = target.wheels[i] != null ? target.wheels[i].springLength : 0f;
        }

        void UpdateReactions(Vector3 v, float dt, bool reduced)
        {
            if (dt <= 0f) return;                       // paused: everything holds still
            var wheels = target.wheels;
            int n = wheels != null ? wheels.Length : 0;
            if (prevSpring.Length != n) SyncSprings();

            // ---- airborne pull-back (TakeOff -> Landed); safety net if a Landed event never arrives
            if (airborne)
            {
                groundedFor = target.Grounded ? groundedFor + dt : 0f;
                if (groundedFor > 0.5f) airborne = false;
            }
            airPull = Mathf.MoveTowards(airPull, airborne && !showroom ? 1f : 0f, dt * (airborne ? 2.2f : 1.5f));

            // ---- landing dip (queued by the event, which arrives from the physics step)
            if (pendingDip > 0f)
            {
                float impact = pendingDip;
                pendingDip = 0f;
                bumpVel -= impact * 3.2f * (reduced ? 0.35f : 1f);
                fovKick = Mathf.Min(fovKick, -3f * impact * (reduced ? 0f : 1f));
                if (impact > 0.3f && !reduced) { shakeDir = Vector3.up; Shake(impact * 0.25f); }
            }
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 6f);

            // ---- every bump: suspension compression -> small vertical kick; surface vibration
            float comp = 0f, rough = 0f;
            int grounded = 0;
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                if (w == null) continue;
                float d = prevSpring[i] - w.springLength;
                prevSpring[i] = w.springLength;
                if (!w.grounded) continue;
                grounded++;
                if (d > 0.004f) comp += d - 0.004f;
                switch (w.surface)
                {
                    case Surface.Gravel: rough += 1f; break;
                    case Surface.Dirt: rough += 0.8f; break;
                    case Surface.Grass: rough += 0.35f; break;
                    case Surface.Snow: rough += 0.3f; break;
                    case Surface.Sand: rough += 0.5f; break;
                    case Surface.Paving: rough += 0.15f; break;
                }
            }
            if (!reduced && !showroom && comp > 0f) bumpVel -= Mathf.Min(0.8f, comp * 9f);
            float vibTarget = !reduced && n > 0 ? rough / n * Mathf.Clamp01(v.magnitude / 25f) : 0f;
            vibration = Mathf.Lerp(vibration, vibTarget, 1f - Mathf.Exp(-dt * 4f));

            // ---- springs (sub-stepped for stability on slow frames)
            float remaining = Mathf.Min(dt, 0.1f);
            while (remaining > 0f)
            {
                float h = Mathf.Min(remaining, 0.02f);
                bumpVel += (-bumpPos * 160f - bumpVel * 16f) * h;
                bumpPos += bumpVel * h;
                kickVel += (-kickPos * 120f - kickVel * 13f) * h;
                kickPos += kickVel * h;
                remaining -= h;
            }
            bumpPos = Mathf.Clamp(bumpPos, -0.35f, 0.2f);
            if (kickPos.sqrMagnitude > 0.16f) kickPos = kickPos.normalized * 0.4f;
            if (reduced) kickPos = kickVel = Vector3.zero;
            lastVel = v;
        }
    }
}

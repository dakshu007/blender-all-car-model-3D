// CameraRig.cs - Chase (drift-aware), Cockpit (driver view), Hood and Cinematic orbit cameras.
// Chase feel: the camera swings out to the outside of a slide and looks along the direction of
// travel, rolls slightly into the corner, widens its FOV with speed / drift, eases its follow
// distance with speed, buzzes subtly at high speed (Perlin) and dips on hard landings.
// Shake / roll / landing offsets are layered on top of an un-shaken "base" pose so they never
// feed back into the follow smoothing. Set externalControl to hand the camera to replay / photo mode.
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
        float bumpPos, bumpVel;            // landing dip spring (m)
        float airTime, prevVy;
        bool wasGrounded = true;
        float noiseT;
        ArcadeCar lastTarget;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 1100f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            ApplyCullDistances();
            cam.layerCullSpherical = true;
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplyCullDistances;
            ApplyCullDistances();
        }

        void OnDisable()
        {
            GameSettings.Changed -= ApplyCullDistances;
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
                wasGrounded = true;
                airTime = 0f;
                bumpPos = bumpVel = 0f;
            }
            // someone else moved the camera (replay / photo exit, teleports): continue from there
            if (!hasBase || (transform.position - lastOutPos).sqrMagnitude > 1e-6f || Quaternion.Angle(transform.rotation, lastOutRot) > 0.05f)
            {
                basePos = transform.position;
                baseRot = transform.rotation;
                hasBase = true;
            }

            UpdateLanding(v, dt);

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
                        rollTarget = slipSide * drift * 3.5f;

                        // smooth, speed-dependent follow distance
                        float distTarget = 6.4f + Mathf.Clamp01(speed / 55f) * 1.9f + drift * 0.5f;
                        followDist = Mathf.SmoothDamp(followDist, distTarget, ref distVel, 0.6f);

                        Vector3 right = Vector3.Cross(Vector3.up, smoothDir).normalized;
                        Vector3 desired = t.position - smoothDir * followDist + right * swing
                                          + Vector3.up * (2.2f + Mathf.Clamp01(speed / 60f) * 0.4f);
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
                        float fov = 60f + Mathf.Clamp01(target.SpeedKmh / 190f) * 14f + drift * 6f;
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
            // subtle high-speed buzz (low amplitude, smooth noise)
            float buzz = Mathf.Clamp01((target.SpeedKmh - 110f) / 120f) * offsetScale;
            if (buzz > 0.001f)
            {
                float nx = Noise(noiseT * 9f, 1.7f), ny = Noise(3.1f, noiseT * 9f);
                outPos += outRot * new Vector3(nx, ny, 0f) * (0.028f * buzz);
                outRot = outRot * Quaternion.Euler(ny * 0.22f * buzz, nx * 0.18f * buzz, 0f);
            }
            // impact shake (Shake(float))
            if (shake > 0.001f)
            {
                float f = noiseT * 22f;
                outPos += new Vector3(Noise(f, 0.3f), Noise(5.2f, f), Noise(f, 9.4f)) * (shake * 0.12f);
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
            // landing dip with a little nod
            if (Mathf.Abs(bumpPos) > 1e-4f && !showroom)
            {
                float s = Mathf.Max(offsetScale, 0.4f);
                outPos += Vector3.up * bumpPos * s;
                outRot = outRot * Quaternion.Euler(-bumpPos * 10f * s, 0f, 0f);
            }
            // roll into the slide
            if (Mathf.Abs(roll) > 1e-3f) outRot = outRot * Quaternion.Euler(0f, 0f, roll);

            transform.SetPositionAndRotation(outPos, outRot);
            lastOutPos = transform.position;
            lastOutRot = transform.rotation;
        }

        void UpdateLanding(Vector3 v, float dt)
        {
            if (dt <= 0f) return;
            bool g = target.Grounded;
            if (!g)
            {
                airTime += dt;
                prevVy = v.y;
            }
            else
            {
                if (!wasGrounded && airTime > 0.12f)
                {
                    float impact = Mathf.Clamp01(-prevVy / 8f);
                    bumpVel -= impact * 3f;
                    if (impact > 0.3f) Shake(impact * 0.25f);
                }
                airTime = 0f;
            }
            wasGrounded = g;
            // damped spring back to rest (sub-stepped for stability on slow frames)
            float remaining = Mathf.Min(dt, 0.1f);
            while (remaining > 0f)
            {
                float h = Mathf.Min(remaining, 0.02f);
                bumpVel += (-bumpPos * 160f - bumpVel * 16f) * h;
                bumpPos += bumpVel * h;
                remaining -= h;
            }
        }
    }
}

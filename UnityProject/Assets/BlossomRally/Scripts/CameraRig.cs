// CameraRig.cs - Chase (drift-aware), Cockpit (driver view), Hood and Cinematic orbit cameras.
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

        Camera cam;
        Vector3 vel;
        Vector3 smoothDir = Vector3.forward;
        float orbit;
        float shake;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 1100f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var d = new float[32];
            d[WorldBuilder.LayerGrass] = 130f;
            d[WorldBuilder.LayerTrees] = 400f;
            cam.layerCullDistances = d;
            cam.layerCullSpherical = true;
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
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Transform t = target.transform;
            Rigidbody rb = target.Body;
            Vector3 v = rb != null ? rb.GetVelocity() : Vector3.zero;
            float speed = v.magnitude;
            float dt = Time.deltaTime;

            if (showroom)
            {
                orbit += dt * 14f;
                Quaternion q = Quaternion.Euler(0f, orbit, 0f);
                Vector3 pos = t.position + q * new Vector3(0f, 1.6f, -7.2f);
                transform.position = Vector3.Lerp(transform.position, pos, dt * 3f);
                transform.rotation = Quaternion.LookRotation(t.position + Vector3.up * 0.7f - transform.position);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 42f, dt * 3f);
                return;
            }

            switch (mode)
            {
                case CamMode.Chase:
                {
                    Vector3 want = Flat(t.forward);
                    if (speed > 5f && target.ForwardSpeed > 0f)
                        want = Vector3.Slerp(want, Flat(v), 0.35f + 0.25f * target.DriftIntensity).normalized;
                    smoothDir = Vector3.Slerp(smoothDir, want, 1f - Mathf.Exp(-dt * 4.5f));
                    float dist = 6.6f + Mathf.Clamp01(speed / 50f) * 1.6f;
                    Vector3 desired = t.position - smoothDir * dist + Vector3.up * (2.2f + Mathf.Clamp01(speed / 60f) * 0.4f);
                    Vector3 look = t.position + Vector3.up * 1.05f + v * 0.06f;
                    // keep the camera out of hills and buildings
                    RaycastHit hit;
                    Vector3 dir = desired - look;
                    if (Physics.SphereCast(look, 0.3f, dir.normalized, out hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                        && hit.rigidbody != rb)
                        desired = look + dir.normalized * Mathf.Max(1.5f, hit.distance - 0.2f);
                    transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, 0.12f);
                    Quaternion rot = Quaternion.LookRotation(look - transform.position);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-dt * 12f));
                    float fov = 60f + Mathf.Clamp01(target.SpeedKmh / 190f) * 14f + target.DriftIntensity * 4f;
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, dt * 3f);
                    break;
                }
                case CamMode.Cockpit:
                {
                    Vector3 seat = new Vector3(-0.36f, 1.06f, 0.05f);
                    transform.position = t.TransformPoint(seat);
                    transform.rotation = Quaternion.LookRotation(t.forward, t.up) * Quaternion.Euler(4f, target.SteerAngle * 0.15f, 0f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 72f, dt * 4f);
                    break;
                }
                case CamMode.Hood:
                {
                    transform.position = t.TransformPoint(new Vector3(0f, 1.25f, 1.1f));
                    transform.rotation = Quaternion.LookRotation(t.forward, t.up) * Quaternion.Euler(3f, 0f, 0f);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 66f, dt * 4f);
                    break;
                }
                case CamMode.Cinematic:
                {
                    orbit += dt * 9f;
                    Quaternion q = Quaternion.Euler(0f, orbit, 0f);
                    Vector3 pos = t.position + q * new Vector3(0f, 1.3f, -9f);
                    transform.position = Vector3.Lerp(transform.position, pos, dt * 2f);
                    transform.rotation = Quaternion.LookRotation(t.position + Vector3.up * 0.9f - transform.position);
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 38f, dt * 2f);
                    break;
                }
            }
            if (shake > 0.001f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.12f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
        }
    }
}

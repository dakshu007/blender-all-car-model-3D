// ResultsCamera.cs - cinematic orbit around the finished car while the event results are shown.
// Waits ~2.5 s after the results open (the finish-line slow-mo sweep plays first), then borrows the
// camera through CameraRig.externalControl, eases in from wherever the camera is, slowly circles the
// car with a gentle crane move and frames it in the left third of the screen (the results panel sits
// in the middle). Hands the camera back as soon as the results close. Pauses while photo mode or the
// replay own the camera. Nothing to wire up: it watches StageManager.Instance.ShowingResults.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(1100)]     // after the camera rig, garage and replay
    public class ResultsCamera : MonoBehaviour
    {
        public static ResultsCamera Instance { get; private set; }
        /// <summary>True while this script drives the camera.</summary>
        public static bool Active { get { return Instance != null && Instance.owning; } }

        const float StartDelay = 2.5f;      // after the results open (unscaled seconds)
        const float ForceAfter = 4.5f;      // take the camera even if someone forgot to release it

        bool showing, owning;
        float shownAt, ownedAt;
        float yaw, height, dist;
        Vector3 fromPos, smoothTarget;
        Quaternion fromRot;
        float fromFov;
        readonly RaycastHit[] hits = new RaycastHit[8];

        void Awake() { Instance = this; }

        void LateUpdate()
        {
            var sm = StageManager.Instance;
            var gm = GameManager.Instance;
            bool results = sm != null && sm.ShowingResults && gm != null && gm.Car != null && gm.Rig != null;
            float now = Time.unscaledTime;
            if (results && !showing) { showing = true; shownAt = now; }
            if (!results)
            {
                if (showing) Release(gm);
                showing = false;
                return;
            }
            bool otherOwner = (PhotoMode.Instance != null && PhotoMode.Instance.Active) || (ReplaySystem.Instance != null && ReplaySystem.Instance.Active);
            if (otherOwner)
            {
                owning = false;          // photo / replay set externalControl themselves; resume afterwards
                return;
            }
            var rig = gm.Rig;
            var cam = rig.GetComponent<Camera>();
            if (cam == null) return;
            if (!owning)
            {
                float since = now - shownAt;
                if (since < StartDelay) return;
                if (rig.externalControl && since < ForceAfter) return;     // the finish sweep still has it
                owning = true;
                ownedAt = now;
                rig.externalControl = true;
                fromPos = cam.transform.position;
                fromRot = cam.transform.rotation;
                fromFov = cam.fieldOfView;
                Vector3 car = gm.Car.transform.position;
                Vector3 d = fromPos - car;
                d.y = 0f;
                yaw = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : gm.Car.transform.eulerAngles.y + 160f;
                smoothTarget = car;
            }
            else if (!rig.externalControl) rig.externalControl = true;    // photo mode handed it back to the rig

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = now - ownedAt;
            Transform ct = gm.Car.transform;
            smoothTarget = Vector3.Lerp(smoothTarget, ct.position, MenuArt.Damp(dt, 4f));
            yaw += dt * 9f;
            dist = 6.8f + Mathf.Sin(t * 0.21f) * 0.8f;
            height = 1.3f + 0.7f * (0.5f + 0.5f * Mathf.Sin(t * 0.17f + 1f));
            Vector3 look = smoothTarget + Vector3.up * 0.75f;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 want = smoothTarget + dir * dist + Vector3.up * height;

            // keep out of hills, houses and trees
            Vector3 ray = want - look;
            float len = ray.magnitude;
            if (len > 0.01f)
            {
                int n = Physics.SphereCastNonAlloc(look, 0.35f, ray / len, hits, len, ~(1 << CarExtras.CarLayer), QueryTriggerInteraction.Ignore);
                float best = len;
                for (int i = 0; i < n; i++) if (hits[i].distance > 0.01f && hits[i].distance < best) best = hits[i].distance;
                if (best < len) want = look + ray / len * Mathf.Max(2.2f, best - 0.25f);
            }
            var w = gm.World;
            if (w != null) want.y = Mathf.Max(want.y, w.TerrainHeight(want.x, want.z) + 0.7f);

            // frame the car in the left third: turn the camera to the right of the look-at line
            float fov = 42f;
            float hfov = 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
            float shift = Mathf.Atan(0.6f * Mathf.Tan(hfov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.LookRotation(look - want, Vector3.up) * Quaternion.Euler(0f, shift, 0f);

            // ease in from the previous camera
            float k = MenuArt.EaseInOut(t / 1.8f);
            Vector3 pos = Vector3.Lerp(fromPos, want, k);
            if (k < 1f) pos += Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(fromRot, rot, k));
            cam.fieldOfView = Mathf.Lerp(fromFov, fov, k);
        }

        void Release(GameManager gm)
        {
            if (!owning) return;
            owning = false;
            if (gm != null && gm.Rig != null && !(PhotoMode.Instance != null && PhotoMode.Instance.Active)) gm.Rig.externalControl = false;
        }
    }
}

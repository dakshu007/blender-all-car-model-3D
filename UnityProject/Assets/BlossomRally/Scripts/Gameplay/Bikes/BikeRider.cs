// BikeRider.cs - the rider on the player's motorbike (added to every bike by CarFactory).
//  * built with PersonFactory (Ride pose, the player's look) plus a helmet in the rider's accent colour; the
//    on-foot module upgrades PersonFactory's internals, this only uses the public API
//  * sits on the seat anchor from the vehicle data and follows the tilted bike every frame (lean, wheelie,
//    wobble) plus its own extra lean (counter-lean at walking pace, hanging into fast corners) and a tuck
//    that depends on the bike class (sport bikes crouch, cruisers sit back)
//  * only exists while this bike is the player's vehicle and the player is riding it: it is removed when the
//    player gets off (PlayerState.OnFootChanged, re-checked twice a second) and on the garage turntable
// No per-frame allocations; everything is posed in LateUpdate after ArcadeCar has tilted the model.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ArcadeCar))]
    public class BikeRider : MonoBehaviour
    {
        ArcadeCar bike;
        CarExtras extras;
        PersonRig rider;
        Transform helmet;
        float height = 1.76f, tuck, check;
        bool placeholderHead;

        static Mesh helmetMesh;

        void Start()
        {
            bike = GetComponent<ArcadeCar>();
            extras = GetComponent<CarExtras>();
            tuck = TuckFor(bike.spec != null ? bike.spec.carClass : "");
            PlayerState.OnFootChanged += OnFootChanged;
            Refresh();
        }

        void OnDestroy()
        {
            PlayerState.OnFootChanged -= OnFootChanged;
            Remove();
        }

        void OnFootChanged(bool onFoot) { Refresh(); }

        static float TuckFor(string cls)
        {
            switch (cls)
            {
                case "Sport Bike": return 30f;
                case "Street Naked": return 12f;
                case "Enduro": return 10f;
                case "Adventure Tourer": return 5f;
                case "Cruiser": return -8f;
                default: return 2f;        // scooter: upright
            }
        }

        bool ShouldRide()
        {
            if (bike == null) return false;
            if (extras != null && extras.ShowroomMode) return false;
            return PlayerState.Vehicle == bike && !PlayerState.OnFoot;
        }

        void Refresh()
        {
            bool want = ShouldRide();
            if (want && rider == null) Build();
            else if (!want && rider != null) Remove();
        }

        void Build()
        {
            var look = PersonLook.Player;
            look.hat = false;                                  // the helmet replaces any hat
            height = look.height > 0.5f ? look.height : 1.76f;
            rider = PersonFactory.Create(look, transform.position, transform.eulerAngles.y, transform, CarExtras.CarLayer);
            rider.name = "BikeRider";
            rider.speed = 0f;
            rider.SetPose(PersonPose.Ride);
            placeholderHead = rider.head == null || rider.head == rider.hips;
            foreach (var r in rider.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var hg = new GameObject("Helmet");
            hg.transform.SetParent(rider.transform, false);
            hg.AddComponent<MeshFilter>().sharedMesh = HelmetMesh();
            var mr = hg.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                MaterialLibrary.Standard("BikeHelmet_" + ColorUtility.ToHtmlStringRGB(look.accent), look.accent, 0.75f, 0f),
                MaterialLibrary.Standard("BikeHelmetVisor", new Color(0.06f, 0.07f, 0.09f), 0.92f, 0.3f),
                MaterialLibrary.Standard("BikeHelmetStripe", new Color(0.96f, 0.95f, 0.92f), 0.6f, 0f)
            };
            helmet = hg.transform;
            float s = placeholderHead ? 1.75f : 1f;            // the placeholder capsule is wide: cover its top
            helmet.localScale = new Vector3(s, s, s);
            foreach (var t in rider.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CarExtras.CarLayer;
            Pose();
        }

        void Remove()
        {
            if (rider != null) Destroy(rider.gameObject);
            rider = null;
            helmet = null;
        }

        void LateUpdate()
        {
            check -= Time.unscaledDeltaTime;
            if (check <= 0f) { check = 0.5f; Refresh(); }
            if (rider != null) Pose();
        }

        void Pose()
        {
            // the rider's frame: the tilted bike, plus the rider's own lean about the seat and the class tuck
            Quaternion bikeQ = bike.VisualRotation;
            Vector3 seat = bike.VisualPoint(bike.RiderSeat);
            float speed = Mathf.Abs(bike.ForwardSpeed);
            float tuckNow = tuck * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed / 20f));
            Quaternion q = bikeQ * Quaternion.AngleAxis(-bike.RiderLean, Vector3.forward) * Quaternion.AngleAxis(tuckNow, Vector3.right);
            // PersonRig root is at the feet: hang it half a body below the hips so the figure straddles the seat
            Vector3 root = seat + q * new Vector3(0f, -height * 0.5f, 0f);
            rider.transform.localPosition = root;
            rider.transform.localRotation = q;
            rider.speed = speed;
            if (helmet != null)
            {
                if (!placeholderHead && rider.head != null)
                {
                    helmet.position = rider.head.position + rider.head.up * 0.09f;
                    helmet.rotation = rider.head.rotation;
                }
                else
                {
                    helmet.localPosition = new Vector3(0f, height * 0.93f, 0.01f);
                    helmet.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>Low-poly full-face helmet (shell, dark visor band, white centre stripe), ~0.3 m, origin at
        /// its centre, facing +Z. Built once and shared.</summary>
        static Mesh HelmetMesh()
        {
            if (helmetMesh != null) return helmetMesh;
            const int seg = 12, rings = 7;
            const float rx = 0.135f, ry = 0.15f, rz = 0.16f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var shell = new System.Collections.Generic.List<int>();
            var visor = new System.Collections.Generic.List<int>();
            var stripe = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float v = Mathf.Lerp(-0.42f, 1f, i / (float)rings) * Mathf.PI * 0.5f;   // open at the neck
                for (int j = 0; j < seg; j++)
                {
                    float u = j / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(u) * Mathf.Cos(v) * rx, Mathf.Sin(v) * ry, Mathf.Cos(u) * Mathf.Cos(v) * rz));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * seg + j, b = i * seg + (j + 1) % seg, c = a + seg, d = b + seg;
                    bool front = j >= seg - 2 || j <= 1;               // around +Z (u = 0)
                    var list = (i == 2 || i == 3) && front ? visor : (j == 0 || j == seg - 1) && i >= 4 ? stripe : shell;
                    list.Add(a); list.Add(b); list.Add(c);         // clockwise seen from outside
                    list.Add(b); list.Add(d); list.Add(c);
                }
            // flat-shaded look: unweld so every face gets its own normal
            var fv = new System.Collections.Generic.List<Vector3>();
            var subs = new[] { shell, visor, stripe };
            var outIdx = new System.Collections.Generic.List<int>[3];
            for (int s = 0; s < 3; s++)
            {
                outIdx[s] = new System.Collections.Generic.List<int>();
                foreach (int k in subs[s]) { outIdx[s].Add(fv.Count); fv.Add(verts[k]); }
            }
            helmetMesh = new Mesh { name = "BikeHelmet" };
            helmetMesh.SetVertices(fv);
            helmetMesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) helmetMesh.SetTriangles(outIdx[s], s);
            helmetMesh.RecalculateNormals();
            helmetMesh.RecalculateBounds();
            return helmetMesh;
        }
    }
}

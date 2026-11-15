// LifeExtras.cs - small life touches that are not traffic or pedestrians:
//   * parked cars: short rows of nose-in bays beside the road at car parks and plazas (valley gate, coast
//     harbour, ski lodge, oasis, caravanserai, cable car station, city stadium and Jade Plaza). Real models
//     with LOD proxies, placed lazily once the region is live (flat, clear of roads and colliders), parented to
//     the region's streamed root. They are resting dynamic bodies: bump one and it shoves, then settles.
//   * fishing boats: leave the Lotus Coast harbour, motor out past the headland, circle the fishing grounds,
//     come home and moor again (routes checked for depth when first built). Bob on the swell, lantern at night.
//     Only updated while the player is within ~1.1 km of the harbour.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class LifeExtras
    {
        readonly LifeDirector d;
        readonly System.Random rng = new System.Random(1717);

        // ------------------------------------------------------------------ parked cars
        struct Hub { public Vector2 c; public float r; public RegionId region; public bool done; }
        readonly Hub[] hubs =
        {
            new Hub { c = new Vector2(-88f, -345f), r = 55f, region = RegionId.Valley },
            new Hub { c = new Vector2(-1135f, 250f), r = 70f, region = RegionId.Coast },
            new Hub { c = new Vector2(948f, 1150f), r = 75f, region = RegionId.Snow },
            new Hub { c = new Vector2(-1227f, 1046f), r = 60f, region = RegionId.Canyon },
            new Hub { c = new Vector2(-994f, 1290f), r = 55f, region = RegionId.Canyon },
            new Hub { c = new Vector2(112f, 1162f), r = 50f, region = RegionId.Mountains },
            new Hub { c = new Vector2(982f, 312f), r = 90f, region = RegionId.City },
            new Hub { c = new Vector2(810f, -88f), r = 70f, region = RegionId.City },
        };
        readonly List<GameObject> parked = new List<GameObject>();
        readonly List<Vector2> parkedAt = new List<Vector2>();
        float hubT;

        // ------------------------------------------------------------------ boats
        sealed class Boat
        {
            public GameObject go;
            public Transform tr;
            public Renderer lantern;
            public Vector2[] route;      // home ... fishing ground centre
            public int leg, dir;         // dir +1 heading out, -1 heading home
            public int state;            // 0 moored, 1 sailing, 2 fishing
            public float t, dur, speed, heading, phase;
            public Vector2 pos, centre;
        }
        readonly List<Boat> boats = new List<Boat>();
        bool boatsBuilt;
        static readonly Vector2 Harbour = new Vector2(-1190f, 250f);
        static readonly float[] PierZ = { 186f, 250f, 314f };

        public LifeExtras(LifeDirector director) { d = director; }

        public void Dispose()
        {
            foreach (var go in parked) if (go != null) Object.Destroy(go);
            parked.Clear();
        }

        public void Tick(float dt)
        {
            if (!LifeDirector.Active) return;
            hubT -= dt;
            if (hubT <= 0f) { hubT = 1.5f; ParkTick(); }
            BoatTick(dt);
        }

        void ParkTick()
        {
            for (int h = 0; h < hubs.Length; h++)
            {
                if (hubs[h].done) continue;
                if (Vector2.Distance(LifeDirector.Player2, hubs[h].c) > 420f || !d.RegionLive(hubs[h].region)) continue;
                hubs[h].done = true;
                BuildHub(hubs[h]);
                return;                       // one hub per tick
            }
        }

        void BuildHub(Hub hub)
        {
            var f = d.Field;
            int want = LifeDirector.ParkedPerHub, placed = 0;
            int mask = LifeDirector.SolidMask | (1 << CarExtras.CarLayer);
            Transform parent = d.RegionRoot(hub.region);
            for (int t = 0; t < 50 && placed < want; t++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rng.NextDouble()) * hub.r;
                Vector2 p = hub.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                RoadSample s;
                float dist = f.NearestRoad(p, out s, 16f);
                if (dist == float.MaxValue || s.bridge) continue;
                if (s.road == LifeDirector.EventRoad) continue;
                Vector2 outward = (p - s.pos);
                if (outward.sqrMagnitude < 0.01f) continue;
                outward = Vector2.Dot(outward, s.Right) >= 0f ? s.Right : -s.Right;
                // a row of up to four bays along the road
                int row = 1 + rng.Next(Mathf.Min(4, want - placed));
                int ok = 0;
                for (int k = 0; k < row; k++)
                {
                    Vector2 c = s.pos + outward * (s.halfWidth + 3.9f) + s.tangent * (k * 2.8f);
                    float yaw = LifeDirector.YawOf(-outward);           // nose to the road
                    if (!Bay(c, yaw, mask)) break;
                    Park(c, yaw, parent);
                    ok++;
                    placed++;
                }
                if (ok == 0) continue;
            }
        }

        bool Bay(Vector2 c, float yaw, int mask)
        {
            var f = d.Field;
            foreach (var q in parkedAt) if ((q - c).sqrMagnitude < 2.6f * 2.6f) return false;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            float h = d.Ground(c.x, c.y);
            if (h < WorldField.WaterY + 0.4f) return false;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 o = rot * new Vector3(sx * 1.0f, 0f, sz * 2.3f);
                    Vector2 q = c + new Vector2(o.x, o.z);
                    if (Mathf.Abs(d.Ground(q.x, q.y) - h) > 0.35f) return false;
                    if (f.EdgeDistance(q) < 1.0f) return false;
                }
            if (f.EdgeDistance(c) < 2f) return false;
            if (Physics.CheckBox(new Vector3(c.x, h + 0.95f, c.y), new Vector3(1.05f, 0.75f, 2.35f), rot, mask, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        void Park(Vector2 c, float yaw, Transform parent)
        {
            string id = null;
            var cars = CarCatalog.Cars;
            for (int tries = 0; tries < 6 && id == null; tries++)
            {
                var s = cars[rng.Next(cars.Length)];
                if (s == null || s.id.StartsWith("BIKE-")) continue;
                id = s.id;
            }
            if (id == null) return;
            var rig = LifeVehicles.BuildCar(id, LifeVehicles.RandomPaint(rng), parent, false, false);
            if (rig == null) return;
            float h = Mathf.Max(d.Ground(c.x, c.y), Mathf.Max(d.Ground(c.x + 1f, c.y), d.Ground(c.x - 1f, c.y)));
            rig.tr.SetPositionAndRotation(new Vector3(c.x, h + 0.03f, c.y), Quaternion.Euler(0f, yaw, 0f));
            if (rig.col != null)
            {
                // rest on the box (no wheels): bottom near the ground
                Vector3 ce = rig.col.center, sz = rig.col.size;
                float top = ce.y + sz.y * 0.5f, bottom = 0.06f;
                rig.col.center = new Vector3(ce.x, (top + bottom) * 0.5f, ce.z);
                rig.col.size = new Vector3(sz.x, top - bottom, sz.z);
            }
            var rb = rig.go.AddComponent<Rigidbody>();
            rb.mass = 1300f;
            rb.SetDamping(0.6f, 1.2f);
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.Sleep();
            parked.Add(rig.go);
            parkedAt.Add(c);
        }

        // ------------------------------------------------------------------ boats
        void BoatTick(float dt)
        {
            float dh = Vector2.Distance(LifeDirector.Player2, Harbour);
            if (dh > 1100f) return;
            if (!boatsBuilt) { boatsBuilt = true; BuildBoats(); }
            bool lamp = LifeDirector.Night > 0.25f;
            float time = Time.time;
            int want = LifeDirector.BoatBudget;
            for (int i = 0; i < boats.Count; i++)
            {
                var b = boats[i];
                bool use = i < want;
                if (b.go.activeSelf != use) b.go.SetActive(use);
                if (!use) continue;
                StepBoat(b, dt);
                float bob = Mathf.Sin(time * 1.1f + b.phase) * 0.08f + Mathf.Sin(time * 0.53f + b.phase * 2f) * 0.05f;
                float pitch = Mathf.Sin(time * 0.9f + b.phase) * (b.state == 1 ? 2.2f : 1.2f);
                float roll = Mathf.Sin(time * 0.7f + b.phase * 1.3f) * 2.5f;
                b.tr.SetPositionAndRotation(new Vector3(b.pos.x, WorldField.WaterY - 0.25f + bob, b.pos.y), Quaternion.Euler(pitch, b.heading, roll));
                if (b.lantern != null && b.lantern.enabled != lamp) b.lantern.enabled = lamp;
            }
        }

        void StepBoat(Boat b, float dt)
        {
            b.t += dt;
            switch (b.state)
            {
                case 0:   // moored at home
                    if (b.t > b.dur) { b.state = 1; b.dir = 1; b.leg = 0; b.t = 0f; }
                    break;
                case 1:   // sailing along the route
                {
                    int target = b.leg + b.dir;
                    if (target < 0 || target >= b.route.Length)
                    {
                        if (b.dir > 0) { b.state = 2; b.t = 0f; b.dur = 40f + (float)rng.NextDouble() * 50f; b.centre = b.route[b.route.Length - 1]; }
                        else { b.state = 0; b.t = 0f; b.dur = 30f + (float)rng.NextDouble() * 40f; }
                        break;
                    }
                    Vector2 to = b.route[target] - b.pos;
                    float dist = to.magnitude;
                    float sp = b.speed * (target == 0 ? Mathf.Clamp01(dist / 25f) + 0.25f : 1f);
                    if (dist < 4f) { b.leg = target; break; }
                    Turn(b, to, dt);
                    b.pos += Fwd(b) * sp * dt;
                    break;
                }
                case 2:   // circling the fishing grounds
                {
                    float ang = b.t * 0.07f + b.phase;
                    Vector2 goal = b.centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 38f;
                    Turn(b, goal - b.pos, dt);
                    b.pos += Fwd(b) * b.speed * 0.45f * dt;
                    if (b.t > b.dur) { b.state = 1; b.dir = -1; b.leg = b.route.Length - 1; b.t = 0f; }
                    break;
                }
            }
        }

        static Vector2 Fwd(Boat b)
        {
            float a = b.heading * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        static void Turn(Boat b, Vector2 to, float dt)
        {
            if (to.sqrMagnitude < 0.01f) return;
            float want = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
            b.heading = Mathf.MoveTowardsAngle(b.heading, want, 22f * dt);
        }

        float ShoreX(float z)
        {
            for (float x = -1080f; x > -1450f; x -= 2f)
                if (d.Ground(x, z) < WorldField.WaterY - 0.6f) return x;
            return float.NaN;
        }

        bool Deep(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 8f));
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)n);
                if (d.Ground(p.x, p.y) > WorldField.WaterY - 1.3f) return false;
            }
            return true;
        }

        void BuildBoats()
        {
            Transform parent = d.RegionRoot(RegionId.Coast);
            for (int i = 0; i < 4; i++)
            {
                float pz = PierZ[i % PierZ.Length] + (i >= 3 ? -9f : 8f);
                float sx = ShoreX(pz);
                if (float.IsNaN(sx)) continue;
                Vector2 home = new Vector2(sx - 26f, pz);
                Vector2 mouth = new Vector2(sx - 90f, pz);
                Vector2 open = new Vector2(-1430f, 250f + (i - 1.5f) * 30f);
                Vector2 ground = new Vector2(-1660f + (float)rng.NextDouble() * 80f, 120f + i * 70f);
                if (!Deep(home, mouth) || !Deep(mouth, open) || !Deep(open, ground) || d.Ground(home.x, home.y) > WorldField.WaterY - 1.3f) continue;
                Renderer lantern;
                var go = LifeVehicles.BuildBoat(parent, i, out lantern);
                var b = new Boat
                {
                    go = go, tr = go.transform, lantern = lantern, route = new[] { home, mouth, open, ground }, pos = home,
                    heading = -90f, speed = 3.2f + (float)rng.NextDouble() * 1.4f, phase = i * 1.7f,
                    state = 0, dur = 8f + i * 25f
                };
                boats.Add(b);
            }
        }
    }
}

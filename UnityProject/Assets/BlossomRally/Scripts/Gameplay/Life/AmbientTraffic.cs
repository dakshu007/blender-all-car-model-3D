// AmbientTraffic.cs - AI cars, valley cyclists and the jade city bus driving the lane network (TrafficNet).
//
//   * Kinematic: each vehicle is a cursor on a lane (road, sample index u, direction) or on a Bezier curve
//     through a junction / U-turn; the body follows it with a kinematic Rigidbody + box collider. No physics
//     queries per vehicle: the car ahead comes from a spatial hash (exact along-lane gap on the same road,
//     a 4-point look-ahead path test for everything else, including the player on foot or in a car).
//   * Driving: per-road limits and curvature caps (gentle cornering), braking for the car ahead (1.3 s
//     headway), slowing for turns, giving way at T junctions and before left turns, U-turns at dead ends.
//   * Crashes: when the player's car closes in, the AI car turns into a velocity-servoed dynamic body so the
//     hit is a real shove; after contact it slides freely for a moment, then glides back onto its lane and
//     drives on. AI cars ignore each other physically (no pile-ups); the ones behind simply wait.
//   * Streaming: up to GameSettings budget (Low 12 / Medium 25 / High 32 / Ultra 40) within 350 m, spawned
//     out of sight, pooled forever; far cars tick at 1/2 or 1/4 rate. Rally events clear their road.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal struct LaneCursor
    {
        public int road, dir, mode;          // mode 0 lane, 1 curve
        public float u, lat;
        public Vector3 c0, c1, c2, c3;
        public float ct, clen;
        public LaneLink next;
        public bool hasPlan;
        public float planU;
        public LaneLink plan;
    }

    internal sealed class TrafficAgent
    {
        public int id, kind;                 // kind 0 car, 1 bicycle, 2 bus
        public bool active;
        public VehicleRig rig;
        public LaneCursor cur;
        public float speed, pref = 1f, wait, stuck, honkT, bumpT, accum, dist, blocked;
        public Vector3 pos, fwd = Vector3.forward;
        public Quaternion rot = Quaternion.identity;
        public int skipA = -1, skipB = -1;
        public bool planYield, braking, colliderOn = true;
        public int phys;                     // 0 kinematic, 1 armed (servo), 2 nudged, 3 recovering, 4 stranded
        public float physT;
        public Vector3 recPos, targetPos;
        public Quaternion recRot, targetRot;
        public int wp;
        public float dwell;
    }

    /// <summary>Forwards collisions of an AI vehicle to the traffic system.</summary>
    public sealed class LifeCarBody : MonoBehaviour
    {
        internal TrafficAgent agent;
        internal TrafficSystem system;

        void OnCollisionEnter(Collision c)
        {
            if (system != null && agent != null) system.OnHit(agent, c);
        }
    }

    internal sealed class TrafficSystem
    {
        readonly LifeDirector d;
        readonly TrafficNet net;
        readonly Transform root;
        readonly List<TrafficAgent> agents = new List<TrafficAgent>();
        readonly LifeGrid grid = new LifeGrid(24f, 256);
        readonly System.Random rng = new System.Random(4242);
        readonly Vector3[] path = new Vector3[5];
        int carsActive, bikesActive, busesActive, carsMade, bikesMade;
        int lastEvent = -1;
        float spawnT;
        bool madeThisFrame;
        readonly List<string> modelIds = new List<string>();
        readonly List<CarSpec> modelSpecs = new List<CarSpec>();
        readonly HashSet<string> badModels = new HashSet<string>();
        readonly float[] modelW = new float[64];

        /// <summary>Nearest moving AI vehicle within 35 m (engine hum) and how many are within 120 m (street hum).</summary>
        internal TrafficAgent Nearest;
        internal int NearCount;

        public TrafficSystem(LifeDirector director)
        {
            d = director;
            net = director.Net;
            root = new GameObject("Traffic").transform;
            root.SetParent(director.transform, false);
            foreach (var s in CarCatalog.Cars)
                if (s != null && !string.IsNullOrEmpty(s.id) && modelIds.Count < modelW.Length) { modelIds.Add(s.id); modelSpecs.Add(s); }
            InitBusRoute();
        }

        public void Dispose() { }

        // ================================================================== frame
        public void Tick(float dt)
        {
            madeThisFrame = false;
            LifeVehicles.UpdateNight(LifeDirector.Night);
            if (!LifeDirector.Active) return;
            if (LifeDirector.EventRoad != lastEvent)
            {
                lastEvent = LifeDirector.EventRoad;
                if (lastEvent >= 0) ClearRoad(lastEvent);
            }
            grid.Ensure(agents.Count + 1);
            grid.Clear();
            for (int i = 0; i < agents.Count; i++)
                if (agents[i].active) grid.Add(i, agents[i].pos.x, agents[i].pos.z);

            Nearest = null;
            NearCount = 0;
            float nearBest = 35f;
            bool lights = LifeDirector.Night > 0.2f || LifeDirector.Rain > 0.6f;
            int frame = LifeDirector.Frame;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (!a.active) continue;
                a.dist = LifeDirector.FlatDist(a.pos, LifeDirector.Player);
                float limit = a.kind == 2 ? 520f : LifeDirector.CarRadius + 30f;
                if (a.dist > limit || (LifeDirector.Teleported && a.dist > 250f)) { Despawn(a); continue; }
                if (a.dist < 120f) NearCount++;
                if (a.dist < nearBest && a.speed > 1.5f && a.kind != 1) { nearBest = a.dist; Nearest = a; }
                a.accum += dt;
                int rate = a.dist < 90f || a.phys != 0 ? 1 : a.dist < 200f ? 2 : 4;
                if (rate > 1 && ((frame + i) % rate) != 0) continue;
                float adt = Mathf.Min(a.accum, 0.25f);
                a.accum = 0f;
                Step(a, adt, lights);
            }
            SpawnTick(dt);
            BusTick();
        }

        public void FixedTick(float fdt)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (!a.active || a.phys != 1 || a.rig.rb == null || a.rig.rb.isKinematic) continue;
                var rb = a.rig.rb;
                Vector3 err = a.targetPos - rb.position;
                Vector3 v = a.fwd * a.speed + err * (0.5f / Mathf.Max(fdt, 0.005f));
                rb.SetVelocity(Vector3.ClampMagnitude(v, 45f));
                Quaternion dq = a.targetRot * Quaternion.Inverse(rb.rotation);
                float ang;
                Vector3 axis;
                dq.ToAngleAxis(out ang, out axis);
                if (ang > 180f) ang -= 360f;
                if (Mathf.Abs(ang) > 0.01f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
                    rb.angularVelocity = axis * (ang * Mathf.Deg2Rad * 0.5f / Mathf.Max(fdt, 0.005f));
                else rb.angularVelocity = Vector3.zero;
            }
        }

        // ================================================================== one vehicle
        void Step(TrafficAgent a, float dt, bool lights)
        {
            if (a.phys == 2 || a.phys == 4) { Loose(a, dt); return; }
            if (a.phys == 3) { Recover(a, dt); return; }

            if (a.kind == 2) BusPlan(a); else EnsurePlan(a);
            float target = Desired(a, dt);
            if (a.bumpT > 0f)
            {
                a.bumpT -= dt;
                target = 0f;
                if (a.bumpT <= 0f && a.rig.rider != null) a.rig.rider.SetPose(PersonPose.Ride);
            }
            float acc = a.kind == 1 ? 1.2f : a.kind == 2 ? 1.1f : 2.0f;
            if (target > a.speed) a.speed = Mathf.Min(target, a.speed + acc * dt);
            else a.speed = Mathf.Max(target, a.speed - (a.speed - target > 5f ? 7.5f : 3.6f) * dt);
            a.braking = target < a.speed - 0.4f || (a.speed < 0.3f && target < 0.3f);
            float ds = a.speed * dt;
            Advance(ref a.cur, ds, a.kind);

            Vector3 p, f;
            PoseOf(ref a.cur, out p, out f);
            a.pos = p;
            Vector3 flat = new Vector3(f.x, 0f, f.z);
            if (flat.sqrMagnitude > 1e-6f) a.fwd = flat.normalized;
            Quaternion want = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f, Vector3.up) : a.rot;
            a.rot = a.dist < 120f ? Quaternion.Slerp(a.rot, want, 1f - Mathf.Exp(-dt * 10f)) : want;
            if (a.phys == 1) { a.targetPos = p; a.targetRot = a.rot; }
            else a.rig.tr.SetPositionAndRotation(p, a.rot);

            if (a.dist < 40f && LifeDirector.Quality >= 1 && a.kind == 0) LifeVehicles.SpinWheels(a.rig, ds);
            if (a.rig.rider != null) a.rig.rider.speed = a.speed;
            LifeVehicles.SetLights(a.rig, lights && a.dist < 260f, a.braking && a.dist < 150f);

            bool wantCol = a.colliderOn ? a.dist < 150f : a.dist < 135f;
            if (wantCol != a.colliderOn && a.rig.col != null) { a.colliderOn = wantCol; a.rig.col.enabled = wantCol; }

            if (a.kind == 1) BikeBump(a);
            else Arm(a);

            if (a.speed < 0.2f) a.stuck += dt; else a.stuck = 0f;
            if (a.stuck > 16f && !LifeDirector.InView(a.pos, 0.1f)) { Despawn(a); return; }
            Honk(a, dt);
        }

        void Honk(TrafficAgent a, float dt)
        {
            a.honkT -= dt;
            if (a.blocked > 3f && a.honkT <= 0f && a.dist < 60f && d.Audio != null)
            {
                d.Audio.Horn(a.pos, a.kind == 2 ? 0.7f : Random.Range(0.92f, 1.12f));
                a.honkT = Random.Range(7f, 12f);
                a.blocked = 0f;
            }
        }

        float Desired(TrafficAgent a, float dt)
        {
            float v;
            if (a.cur.mode == 1) v = a.cur.next.vTurn;
            else
            {
                var L = net.roads[a.cur.road];
                v = TrafficNet.Cap(L, a.cur.u, a.cur.dir) * a.pref;
                if (a.kind == 1) v = Mathf.Min(v, BikeSpeed(a, L));
                else if (a.kind == 2) v = Mathf.Min(v, Mathf.Min(10.5f, BusStopCap(a, L, dt)));
                if (a.cur.hasPlan)
                {
                    float dist = Mathf.Max(0f, TrafficNet.Delta(L, a.cur.u, a.cur.planU) * a.cur.dir * L.seg);
                    float vt = a.cur.plan.vTurn;
                    v = Mathf.Min(v, Mathf.Sqrt(vt * vt + 2f * TrafficNet.Dec * dist));
                    if (a.planYield && dist < 24f && Conflict(a))
                    {
                        v = Mathf.Min(v, Mathf.Sqrt(2f * TrafficNet.Dec * Mathf.Max(0f, dist - 0.6f)));
                        if (dist < 4f) a.wait += dt;
                        if (a.wait > 6f) a.planYield = false;          // nobody waits for ever
                    }
                }
            }
            float gap = Gap(a, dt);
            if (gap < 80f) v = Mathf.Min(v, Mathf.Max(0f, (gap - 2.2f) / 1.3f));
            return v;
        }

        static float BikeSpeed(TrafficAgent a, LaneRoad L)
        {
            int i = Mathf.Clamp((int)a.cur.u, 0, L.n - 1);
            int j = Mathf.Clamp(i + a.cur.dir * 2, 0, L.n - 1);
            float grade = i == j ? 0f : (L.s[j].height - L.s[i].height) / (L.seg * 2f);
            float v = 5.6f * (1f - Mathf.Clamp(grade * 5f, -0.35f, 0.6f));
            if (L.def.surface != Surface.Asphalt && L.def.surface != Surface.Paving) v *= 0.82f;
            return v;
        }

        // ------------------------------------------------------------------ cursor motion
        float LatFor(LaneRoad L, int kind) { return kind == 1 ? L.bikeOff : L.laneOff; }

        void Advance(ref LaneCursor c, float ds, int kind)
        {
            for (int guard = 0; guard < 4 && ds > 1e-4f; guard++)
            {
                if (c.mode == 1)
                {
                    float step = ds / Mathf.Max(0.5f, c.clen);
                    if (c.ct + step < 1f) { c.ct += step; return; }
                    ds -= (1f - c.ct) * c.clen;
                    var L2 = net.roads[c.next.road];
                    c.mode = 0;
                    c.road = c.next.road;
                    c.dir = c.next.dir;
                    c.u = TrafficNet.Wrap(L2, c.next.idx);
                    c.lat = LatFor(L2, kind);
                    c.ct = 0f;
                    continue;
                }
                var L = net.roads[c.road];
                float du = ds / L.seg;
                if (c.hasPlan)
                {
                    float to = TrafficNet.Delta(L, c.u, c.planU) * c.dir;
                    if (to <= du)
                    {
                        c.u = c.planU;
                        ds -= Mathf.Max(0f, to) * L.seg;
                        StartCurve(ref c, kind);
                        continue;
                    }
                }
                c.u = TrafficNet.Wrap(L, c.u + du * c.dir);
                return;
            }
        }

        void StartCurve(ref LaneCursor c, int kind)
        {
            var L = net.roads[c.road];
            var link = c.plan;
            c.hasPlan = false;
            Vector3 p0, p3;
            Vector2 h0, h3;
            TrafficNet.Pose(L, c.u, c.dir, c.lat, out p0, out h0);
            var L2 = net.roads[link.road];
            TrafficNet.Pose(L2, link.idx, link.dir, LatFor(L2, kind), out p3, out h3);
            float span = Vector3.Distance(p0, p3);
            float k = link.uturn ? 3.5f : Mathf.Max(1.5f, span * 0.42f);
            c.c0 = p0;
            c.c3 = p3;
            c.c1 = p0 + new Vector3(h0.x, 0f, h0.y) * k;
            c.c2 = p3 - new Vector3(h3.x, 0f, h3.y) * k;
            c.c1.y = Mathf.Lerp(p0.y, p3.y, 0.33f);
            c.c2.y = Mathf.Lerp(p0.y, p3.y, 0.66f);
            float len = 0f;
            Vector3 prev = p0;
            for (int i = 1; i <= 8; i++)
            {
                Vector3 q = Bez(ref c, i / 8f);
                len += Vector3.Distance(prev, q);
                prev = q;
            }
            c.clen = Mathf.Max(0.5f, len);
            c.ct = 0f;
            c.mode = 1;
            c.next = link;
        }

        static Vector3 Bez(ref LaneCursor c, float t)
        {
            float u = 1f - t;
            return u * u * u * c.c0 + 3f * u * u * t * c.c1 + 3f * u * t * t * c.c2 + t * t * t * c.c3;
        }

        static Vector3 BezD(ref LaneCursor c, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (c.c1 - c.c0) + 6f * u * t * (c.c2 - c.c1) + 3f * t * t * (c.c3 - c.c2);
        }

        void PoseOf(ref LaneCursor c, out Vector3 p, out Vector3 f)
        {
            if (c.mode == 1)
            {
                p = Bez(ref c, c.ct);
                f = BezD(ref c, c.ct);
                if (f.sqrMagnitude < 1e-6f) f = c.c3 - c.c0;
                return;
            }
            var L = net.roads[c.road];
            Vector2 f2, g2;
            TrafficNet.Pose(L, c.u, c.dir, c.lat, out p, out f2);
            Vector3 p2;
            TrafficNet.Pose(L, c.u + c.dir * 0.8f, c.dir, c.lat, out p2, out g2);
            f = p2 - p;
            if (f.sqrMagnitude < 0.04f) f = new Vector3(f2.x, 0f, f2.y);
        }

        // ------------------------------------------------------------------ route choice
        bool Usable(int road, int kind)
        {
            if (road < 0 || road >= net.roads.Length || road == LifeDirector.EventRoad) return false;
            var L = net.roads[road];
            return L.drivable && (kind != 1 || L.bikes);
        }

        void SetPlan(TrafficAgent a, float u, LaneLink link)
        {
            var L = net.roads[a.cur.road];
            if (TrafficNet.Delta(L, a.cur.u, u) * a.cur.dir < 0f) u = a.cur.u;
            a.cur.hasPlan = true;
            a.cur.planU = u;
            a.cur.plan = link;
            a.planYield = link.yieldRoad >= 0;
            a.wait = 0f;
        }

        void EnsurePlan(TrafficAgent a)
        {
            if (a.cur.mode != 0 || a.cur.hasPlan) return;
            var L = net.roads[a.cur.road];
            float lookS = 36f / L.seg;
            var brs = L.branches;
            for (int b = 0; b < brs.Length; b++)
            {
                var br = brs[b];
                float ahead = TrafficNet.Delta(L, a.cur.u, br.at) * a.cur.dir;
                if (ahead < br.pre + 0.5f || ahead > lookS) continue;
                if (b == a.skipA || b == a.skipB) continue;
                bool ok = a.cur.dir > 0 ? br.okPos : br.okNeg;
                float chance = a.kind == 1 ? 0.25f : L.region == RegionId.City ? 0.4f : 0.3f;
                if (ok && Usable(br.link.road, a.kind) && rng.NextDouble() < chance)
                {
                    var link = br.link;
                    if (a.cur.dir > 0 ? br.leftPos : br.leftNeg) { link.yieldRoad = L.index; link.yieldIdx = br.at; link.oncoming = true; }
                    SetPlan(a, TrafficNet.Wrap(L, br.at - a.cur.dir * br.pre), link);
                }
                else { a.skipB = a.skipA; a.skipA = b; }
                return;
            }
            PlanEnd(a, L, lookS, Vector2.zero, false);
        }

        /// <summary>Near an open road's end: take an exit (random, or the one heading towards 'toward'), else U-turn.</summary>
        void PlanEnd(TrafficAgent a, LaneRoad L, float lookS, Vector2 toward, bool steer)
        {
            if (L.closed) return;
            bool fwd = a.cur.dir > 0;
            float toEnd = fwd ? (L.n - 1 - a.cur.u) : a.cur.u;
            float pre = fwd ? L.endPre1 : L.endPre0;
            if (toEnd > lookS + pre) return;
            var exits = fwd ? L.end1 : L.end0;
            float startU = fwd ? L.n - 1 - pre : pre;
            int count = 0, best = -1;
            float bestDot = -9f;
            for (int e = 0; e < exits.Length; e++)
            {
                if (!Usable(exits[e].road, a.kind)) continue;
                count++;
                if (steer)
                {
                    var L2 = net.roads[exits[e].road];
                    Vector2 h = L2.s[Mathf.Clamp((int)exits[e].idx, 0, L2.n - 1)].tangent * exits[e].dir;
                    float dd = Vector2.Dot(h, toward);
                    if (dd > bestDot) { bestDot = dd; best = e; }
                }
            }
            if (count > 0)
            {
                if (best < 0)
                {
                    int pick = rng.Next(count);
                    for (int e = 0; e < exits.Length; e++)
                        if (Usable(exits[e].road, a.kind) && pick-- == 0) { best = e; break; }
                }
                if (best >= 0) { SetPlan(a, startU, exits[best]); return; }
            }
            float uu = fwd ? Mathf.Max(0f, L.n - 1 - 4f) : Mathf.Min(L.n - 1, 4f);
            SetPlan(a, uu, new LaneLink { road = L.index, idx = uu, dir = -a.cur.dir, uturn = true, vTurn = 2.6f, yieldRoad = -1 });
            if (a.cur.planU != uu) a.cur.plan.idx = a.cur.planU;
        }

        /// <summary>Give way: traffic on the yield road heading into the junction, someone turning in it, or the player.</summary>
        bool Conflict(TrafficAgent a)
        {
            var link = a.cur.plan;
            if (link.yieldRoad < 0) return false;
            var Y = net.roads[link.yieldRoad];
            Vector2 J2 = Y.s[Mathf.Clamp((int)link.yieldIdx, 0, Y.n - 1)].pos;
            int cx = grid.CellOf(J2.x), cz = grid.CellOf(J2.y);
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                    for (int i = grid.First(cx + dx, cz + dz); i >= 0; i = grid.Next(i))
                    {
                        var o = agents[i];
                        if (o == a || !o.active) continue;
                        float jx = o.pos.x - J2.x, jz = o.pos.z - J2.y;
                        float d2 = jx * jx + jz * jz;
                        if (d2 > 34f * 34f) continue;
                        if (o.cur.mode == 1) { if (d2 < 12f * 12f) return true; continue; }
                        if (o.cur.road != link.yieldRoad) continue;
                        if (link.oncoming && o.cur.dir == a.cur.dir) continue;
                        float along = TrafficNet.Delta(Y, o.cur.u, link.yieldIdx) * o.cur.dir * Y.seg;
                        if (along > -4f && along < 6f) return true;
                        if (along > 0f && along < 32f && o.speed > 1.5f && along / o.speed < 3.6f) return true;
                    }
            if (LifeDirector.Active)
            {
                Vector2 pj = J2 - LifeDirector.Player2;
                float dist = pj.magnitude;
                if (dist < 7f) return true;
                if (dist < 30f && LifeDirector.PlayerSpeed > 2f)
                {
                    Vector2 v = new Vector2(LifeDirector.PlayerVel.x, LifeDirector.PlayerVel.z);
                    if (Vector2.Dot(v, pj) > 0f && dist / LifeDirector.PlayerSpeed < 3.6f) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ the car ahead
        float Gap(TrafficAgent a, float dt)
        {
            float look = Mathf.Min(60f, 9f + a.speed * 2.6f);
            float best = 1e9f;
            bool built = false, byPlayer = false;
            LaneRoad L = a.cur.mode == 0 ? net.roads[a.cur.road] : null;
            int span = Mathf.CeilToInt(look / 24f);
            int cx = grid.CellOf(a.pos.x), cz = grid.CellOf(a.pos.z);
            float reach2 = (look + 8f) * (look + 8f);
            for (int dx = -span; dx <= span; dx++)
                for (int dz = -span; dz <= span; dz++)
                    for (int i = grid.First(cx + dx, cz + dz); i >= 0; i = grid.Next(i))
                    {
                        var o = agents[i];
                        if (o == a || !o.active) continue;
                        float ox = o.pos.x - a.pos.x, oz = o.pos.z - a.pos.z;
                        if (ox * ox + oz * oz > reach2) continue;
                        if (L != null && o.phys == 0 && o.cur.mode == 0 && o.cur.road == a.cur.road)
                        {
                            if (o.cur.dir != a.cur.dir) continue;
                            if (o.kind != a.kind && Mathf.Abs(o.cur.lat - a.cur.lat) > a.rig.halfW + o.rig.halfW + 0.35f) continue;   // cyclist at the edge
                            float g = TrafficNet.Delta(L, a.cur.u, o.cur.u) * a.cur.dir * L.seg;
                            if (g <= 0f) continue;
                            g -= a.rig.halfL + o.rig.halfL;
                            if (g < best) best = g;
                            continue;
                        }
                        if (!built) { BuildPath(a, look); built = true; }
                        float along;
                        float lat = PathDist(o.pos, out along);
                        if (lat < a.rig.halfW + o.rig.halfW + 0.45f && along > 0.3f)
                        {
                            float g = along - a.rig.halfL - o.rig.halfL * 0.7f;
                            if (g < best) best = g;
                        }
                    }
            if (LifeDirector.Active && LifeDirector.FlatDist(a.pos, LifeDirector.Player) < look + 6f)
            {
                if (!built) { BuildPath(a, look); built = true; }
                float along;
                float lat = PathDist(LifeDirector.Player, out along);
                bool foot = LifeDirector.OnFoot;
                float pw = foot ? 0.4f : 1.0f, pl = foot ? 0.35f : 2.2f;
                if (lat < a.rig.halfW + pw + 0.5f && along > 0.2f)
                {
                    float g = along - a.rig.halfL - pl;
                    if (g < best) { best = g; byPlayer = LifeDirector.PlayerSpeed < 1f && g < 9f; }
                }
            }
            a.blocked = byPlayer ? a.blocked + dt : 0f;
            return Mathf.Max(0f, best);
        }

        void BuildPath(TrafficAgent a, float look)
        {
            path[0] = a.pos;
            var k = a.cur;
            float step = look * 0.25f;
            for (int j = 1; j < path.Length; j++)
            {
                Advance(ref k, step, a.kind);
                Vector3 f;
                PoseOf(ref k, out path[j], out f);
            }
        }

        float PathDist(Vector3 q, out float along)
        {
            float best = 1e9f, acc = 0f;
            along = -1f;
            for (int j = 0; j < path.Length - 1; j++)
            {
                Vector3 a = path[j], b = path[j + 1];
                float sx = b.x - a.x, sz = b.z - a.z;
                float len2 = sx * sx + sz * sz;
                float t = len2 > 1e-6f ? Mathf.Clamp01(((q.x - a.x) * sx + (q.z - a.z) * sz) / len2) : 0f;
                float px = a.x + sx * t - q.x, pz = a.z + sz * t - q.z;
                float dist = Mathf.Sqrt(px * px + pz * pz);
                float len = Mathf.Sqrt(len2);
                if (dist < best) { best = dist; along = acc + t * len; }
                acc += len;
            }
            return best;
        }

        // ------------------------------------------------------------------ crashes (short physics nudge, then recover)
        void Arm(TrafficAgent a)
        {
            var rb = a.rig.rb;
            if (rb == null || a.kind == 2) return;
            var pc = LifeDirector.PlayerCar;
            if (pc == null) { if (a.phys == 1) Disarm(a); return; }
            Vector3 rel = a.pos - pc.transform.position;
            float dist = rel.magnitude;
            float closing = dist > 0.01f ? Vector3.Dot(LifeDirector.PlayerVel - a.fwd * a.speed, rel / dist) : 0f;
            bool want = dist < 7f + Mathf.Max(0f, closing) * 0.6f;
            if (want && a.phys == 0)
            {
                a.phys = 1;
                a.targetPos = a.pos;
                a.targetRot = a.rot;
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.SetVelocity(a.fwd * a.speed);
            }
            else if (!want && a.phys == 1 && dist > 12f) Disarm(a);
        }

        void Disarm(TrafficAgent a)
        {
            var rb = a.rig.rb;
            a.phys = 0;
            if (rb == null) return;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            a.rig.tr.SetPositionAndRotation(a.targetPos, a.targetRot);
        }

        internal void OnHit(TrafficAgent a, Collision c)
        {
            if (!a.active || a.kind == 2 || a.rig.rb == null) return;
            if (a.phys == 2 || a.phys == 4) return;
            var pc = LifeDirector.PlayerCar;
            bool player = pc != null && c.rigidbody != null && c.rigidbody == pc.Body;
            if (!player && c.relativeVelocity.sqrMagnitude < 9f) return;
            var rb = a.rig.rb;
            a.phys = 2;
            a.physT = 0f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.SetDamping(0.55f, 1.6f);
            if (player && d.Audio != null && Random.value < 0.7f) d.Audio.Horn(a.pos, Random.Range(0.95f, 1.15f));
        }

        void Loose(TrafficAgent a, float dt)
        {
            a.physT += dt;
            a.pos = a.rig.tr.position;
            a.rot = a.rig.tr.rotation;
            if (a.phys == 4)
            {
                if (!LifeDirector.InView(a.pos, 0.1f) || a.physT > 25f) Despawn(a);
                return;
            }
            float sp = a.rig.rb != null ? a.rig.rb.GetVelocity().magnitude : 0f;
            if (a.physT < 1.1f || (sp > 0.8f && a.physT < 3.5f)) return;
            int road = a.cur.mode == 1 ? a.cur.next.road : a.cur.road;
            float u = a.rig.tr.up.y > 0.55f ? net.NearestU(road, new Vector2(a.pos.x, a.pos.z), 9f) : -1f;
            if (u < 0f) { a.phys = 4; a.physT = 0f; return; }
            var L = net.roads[road];
            Vector3 lp;
            Vector2 lf;
            TrafficNet.Pose(L, u, 1, 0f, out lp, out lf);
            Vector3 f = a.rig.tr.forward;
            int dir = Vector2.Dot(new Vector2(f.x, f.z), lf) >= 0f ? 1 : -1;
            a.cur = new LaneCursor { road = road, u = u, dir = dir, lat = LatFor(L, a.kind) };
            a.skipA = a.skipB = -1;
            a.phys = 3;
            a.physT = 0f;
            a.recPos = a.pos;
            a.recRot = a.rot;
            a.speed = 0f;
            var rb = a.rig.rb;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        void Recover(TrafficAgent a, float dt)
        {
            a.physT += dt;
            float k = Mathf.SmoothStep(0f, 1f, a.physT / 1.4f);
            Vector3 p, f;
            PoseOf(ref a.cur, out p, out f);
            Quaternion want = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f, Vector3.up) : a.recRot;
            a.pos = Vector3.Lerp(a.recPos, p, k);
            a.rot = Quaternion.Slerp(a.recRot, want, k);
            a.rig.tr.SetPositionAndRotation(a.pos, a.rot);
            if (k >= 1f) { a.phys = 0; a.speed = 0f; }
        }

        void BikeBump(TrafficAgent a)
        {
            var pc = LifeDirector.PlayerCar;
            if (pc == null || a.dist > 4f || a.bumpT > 0f || LifeDirector.PlayerSpeed < 1.5f) return;
            Vector3 rel = a.pos - pc.transform.position;
            Vector3 f = pc.transform.forward, r = pc.transform.right;
            if (Mathf.Abs(Vector3.Dot(rel, r)) < 1.3f && Mathf.Abs(Vector3.Dot(rel, f)) < 2.7f)
            {
                a.bumpT = 2.6f;
                a.speed = 0f;
                if (a.rig.rider != null) a.rig.rider.SetPose(PersonPose.Wave);
            }
        }

        // ================================================================== spawning / pooling
        void SpawnTick(float dt)
        {
            spawnT -= dt;
            if (spawnT > 0f && !LifeDirector.Teleported) return;
            spawnT = 0.1f;
            int carTarget = Mathf.RoundToInt(LifeDirector.CarBudget * LifeDirector.CarTimeFactor * LifeDirector.RegionTraffic(LifeDirector.PlayerRegion));
            float valley = LifeDirector.PlayerRegion == RegionId.Valley ? 1f : 0f;
            int bikeTarget = Mathf.RoundToInt(LifeDirector.BikeBudget * valley * (1f - 0.8f * LifeDirector.Night) * (1f - LifeDirector.Rain));
            bool burst = carsActive < carTarget / 3;
            if (carsActive < carTarget) TrySpawn(0, burst);
            else if (bikesActive < bikeTarget) TrySpawn(1, false);
            else if (carsActive > carTarget + 2) Thin(0);
            if (bikesActive > bikeTarget + 1) Thin(1);
        }

        void Thin(int kind)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a.active && a.kind == kind && a.phys == 0 && a.dist > 150f && !LifeDirector.InView(a.pos, 0.1f)) { Despawn(a); return; }
            }
        }

        void TrySpawn(int kind, bool burst)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = kind == 1 ? Random.Range(45f, 220f) : Random.Range(60f, LifeDirector.CarRadius - 15f);
                Vector2 p = LifeDirector.Player2 + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                int packed = net.SpawnSample(p);
                if (packed < 0) continue;
                int road = packed >> 16, idx = packed & 0xffff;
                if (!Usable(road, kind)) continue;
                var L = net.roads[road];
                int dir = Random.value < 0.5f ? 1 : -1;
                Vector3 wp;
                Vector2 wf;
                TrafficNet.Pose(L, idx, dir, LatFor(L, kind), out wp, out wf);
                float dist = LifeDirector.FlatDist(wp, LifeDirector.Player);
                if (dist < (kind == 1 ? 40f : 55f) || dist > LifeDirector.CarRadius) continue;
                if (LifeDirector.InView(wp, 0.05f) && dist < (burst || LifeDirector.Teleported ? 120f : 230f)) continue;
                if (AnyNear(wp, kind == 1 ? 14f : 22f)) continue;
                var a = Acquire(kind, L.region);
                if (a == null) return;
                a.cur = new LaneCursor { road = road, u = idx, dir = dir, lat = LatFor(L, kind) };
                a.skipA = a.skipB = -1;
                a.pref = kind == 1 ? Random.Range(0.8f, 1.05f) : Random.Range(0.86f, 1.06f);
                a.speed = Mathf.Min(TrafficNet.Cap(L, idx, dir) * a.pref * 0.8f, kind == 1 ? 4.5f : 30f);
                Activate(a, wp, new Vector3(wf.x, 0f, wf.y));
                return;
            }
        }

        bool AnyNear(Vector3 p, float r)
        {
            int cx = grid.CellOf(p.x), cz = grid.CellOf(p.z);
            float r2 = r * r;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    for (int i = grid.First(cx + dx, cz + dz); i >= 0; i = grid.Next(i))
                    {
                        var o = agents[i];
                        if (!o.active) continue;
                        float ox = o.pos.x - p.x, oz = o.pos.z - p.z;
                        if (ox * ox + oz * oz < r2) return true;
                    }
            return false;
        }

        void Activate(TrafficAgent a, Vector3 pos, Vector3 fwd)
        {
            a.active = true;
            a.phys = 0;
            a.wait = a.stuck = a.bumpT = a.accum = a.blocked = 0f;
            a.pos = pos;
            a.fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            a.rot = Quaternion.LookRotation(a.fwd, Vector3.up);
            a.dist = LifeDirector.FlatDist(pos, LifeDirector.Player);
            a.rig.go.SetActive(true);
            if (a.rig.rb != null) { a.rig.rb.isKinematic = true; a.rig.rb.interpolation = RigidbodyInterpolation.None; }
            a.rig.tr.SetPositionAndRotation(pos, a.rot);
            a.colliderOn = true;
            if (a.rig.col != null) a.rig.col.enabled = true;
            if (a.rig.rider != null) a.rig.rider.SetPose(PersonPose.Ride);
            grid.Add(a.id, pos.x, pos.z);
            if (a.kind == 0) carsActive++; else if (a.kind == 1) bikesActive++; else busesActive++;
        }

        void Despawn(TrafficAgent a)
        {
            if (!a.active) return;
            a.active = false;
            if (a.rig.rb != null && !a.rig.rb.isKinematic)
            {
                a.rig.rb.SetVelocity(Vector3.zero);
                a.rig.rb.angularVelocity = Vector3.zero;
                a.rig.rb.isKinematic = true;
            }
            a.phys = 0;
            a.rig.go.SetActive(false);
            if (a.kind == 0) carsActive--; else if (a.kind == 1) bikesActive--; else busesActive--;
        }

        void ClearRoad(int road)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (!a.active) continue;
                bool on = (a.cur.mode == 0 && a.cur.road == road) || (a.cur.mode == 1 && a.cur.next.road == road);
                if (on) { Despawn(a); continue; }
                if (a.cur.hasPlan && a.cur.plan.road == road) a.cur.hasPlan = false;
            }
        }

        TrafficAgent Acquire(int kind, RegionId region)
        {
            if (kind == 1)
            {
                for (int i = 0; i < agents.Count; i++) if (agents[i].kind == 1 && !agents[i].active) return agents[i];
                if (bikesMade >= LifeDirector.BikeBudget || madeThisFrame) return null;
                madeThisFrame = true;
                var rig = LifeVehicles.BuildBicycle(root, bikesMade * 7 + 3);
                bikesMade++;
                return Register(rig, 1);
            }
            string want = PickModel(region);
            TrafficAgent any = null;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                if (a.kind != 0 || a.active) continue;
                if (a.rig.id == want) return a;
                if (any == null) any = a;
            }
            if (carsMade < LifeDirector.CarBudget + 4 && !madeThisFrame && want != null)
            {
                madeThisFrame = true;
                var rig = LifeVehicles.BuildCar(want, LifeVehicles.RandomPaint(rng), root, true, true);
                if (rig == null) { badModels.Add(want); return any; }
                carsMade++;
                return Register(rig, 0);
            }
            return any;
        }

        TrafficAgent Register(VehicleRig rig, int kind)
        {
            var a = new TrafficAgent { id = agents.Count, kind = kind, rig = rig };
            if (rig.rb != null)
            {
                var body = rig.go.AddComponent<LifeCarBody>();
                body.agent = a;
                body.system = this;
                // AI vehicles never push each other around (no pile-ups)
                for (int i = 0; i < agents.Count; i++)
                    if (agents[i].rig.col != null && rig.col != null) Physics.IgnoreCollision(rig.col, agents[i].rig.col, true);
            }
            rig.go.SetActive(false);
            agents.Add(a);
            return a;
        }

        string PickModel(RegionId region)
        {
            float sum = 0f;
            int n = modelIds.Count;
            for (int i = 0; i < n; i++)
            {
                string id = modelIds[i];
                float w = badModels.Contains(id) ? 0f : ModelWeight(modelSpecs[i], region);
                modelW[i] = w;
                sum += w;
            }
            if (sum <= 0f) return null;
            float r = (float)rng.NextDouble() * sum;
            for (int i = 0; i < n; i++) { r -= modelW[i]; if (r <= 0f) return modelIds[i]; }
            return modelIds[n - 1];
        }

        static float ModelWeight(CarSpec s, RegionId region)
        {
            if (s == null) return 0f;
            if (s.id.StartsWith("BIKE-")) return 0.9f;          // motorbikes, when the bike module adds them
            bool rough = region == RegionId.Snow || region == RegionId.Canyon || region == RegionId.Mountains;
            switch (s.carClass)
            {
                case "Luxury SUV": case "Coupe SUV": case "Super Saloon": case "Electric GT": return region == RegionId.City ? 3f : 0.9f;
                case "Modern Rally": return 1.1f;
                case "Trail 4x4": case "Overland": case "Retro 4x4": return rough ? 2.6f : 0.8f;
                case "Snow Rally": return region == RegionId.Snow ? 3f : 0.25f;
                case "Rally Raid": case "Trophy Truck": case "Dune Buggy": return region == RegionId.Canyon ? 2.6f : region == RegionId.Coast ? 0.5f : 0.12f;
                case "Classic RWD": case "Group 4": return region == RegionId.Valley || region == RegionId.Coast ? 2.2f : 0.8f;
                case "Group B": return 0.45f;
                default: return 1f;
            }
        }

        // ================================================================== the jade city bus
        struct BusStopDef { public Vector2 p; public string road; public int dir; }

        // A loop through the plateau grid: Jade Avenue east -> Bank Street north -> Central Avenue east -> East Street
        // north -> North Avenue west -> West Street south -> back onto Jade Avenue. Corners steer the turns, stops dwell.
        static readonly Vector2[] BusWps =
        {
            new Vector2(930f, -130f), new Vector2(1040f, -130f), new Vector2(1040f, 50f), new Vector2(1130f, 50f), new Vector2(1415f, 50f),
            new Vector2(1415f, 230f), new Vector2(1100f, 230f), new Vector2(820f, 230f), new Vector2(740f, 230f), new Vector2(740f, 60f),
            new Vector2(740f, -130f)
        };
        static readonly int[] BusStopAt = { 0, -1, -1, 1, -1, -1, -1, 2, -1, 3, -1 };
        static readonly BusStopDef[] BusStops =
        {
            new BusStopDef { p = new Vector2(930f, -130f), road = "Road_City_JadeAvenue", dir = 0 },
            new BusStopDef { p = new Vector2(1130f, 50f), road = "Road_City_CentralAvenue", dir = 0 },
            new BusStopDef { p = new Vector2(820f, 230f), road = "Road_City_NorthAvenue", dir = 0 },
            new BusStopDef { p = new Vector2(740f, 60f), road = "Road_City_WestStreet", dir = 0 },
        };
        static readonly Vector2[] BusStopHeading = { new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, -1f) };
        int[] stopRoad;
        float[] stopU;
        int[] stopDir;
        bool busReady, signsBuilt;
        readonly List<TrafficAgent> buses = new List<TrafficAgent>();
        static readonly Vector2 CityCentre = new Vector2(1080f, 50f);

        void InitBusRoute()
        {
            int n = BusStops.Length;
            stopRoad = new int[n];
            stopU = new float[n];
            stopDir = new int[n];
            for (int i = 0; i < n; i++)
            {
                int r = d.Field.RoadIndex(BusStops[i].road);
                if (r < 0 || r >= net.roads.Length || !net.roads[r].drivable) return;
                float u = net.NearestU(r, BusStops[i].p, 20f);
                if (u < 0f) return;
                var L = net.roads[r];
                Vector2 t = L.s[Mathf.Clamp((int)u, 0, L.n - 1)].tangent;
                stopRoad[i] = r;
                stopU[i] = u;
                stopDir[i] = Vector2.Dot(t, BusStopHeading[i]) >= 0f ? 1 : -1;
            }
            busReady = true;
        }

        void BusTick()
        {
            if (!busReady) return;
            float dc = Vector2.Distance(LifeDirector.Player2, CityCentre);
            if (!signsBuilt && dc < 650f && d.RegionLive(RegionId.City)) BuildStopSigns();
            int want = dc < 640f ? LifeDirector.BusBudget : 0;
            if (busesActive >= want) return;
            // spawn at a stop nobody is looking at (40 m before it)
            int start = rng.Next(BusStops.Length);
            for (int k = 0; k < BusStops.Length; k++)
            {
                int s = (start + k) % BusStops.Length;
                if (stopRoad[s] == LifeDirector.EventRoad) continue;
                var L = net.roads[stopRoad[s]];
                float u = TrafficNet.Wrap(L, stopU[s] - stopDir[s] * (40f / L.seg));
                Vector3 p;
                Vector2 f;
                TrafficNet.Pose(L, u, stopDir[s], L.laneOff, out p, out f);
                float pd = LifeDirector.FlatDist(p, LifeDirector.Player);
                if (pd > 450f || (LifeDirector.InView(p, 0.1f) && pd < 260f)) continue;
                if (AnyNear(p, 16f)) continue;
                TrafficAgent bus = null;
                foreach (var b in buses) if (!b.active) { bus = b; break; }
                if (bus == null)
                {
                    if (buses.Count >= 2 || madeThisFrame) return;
                    madeThisFrame = true;
                    bus = Register(LifeVehicles.BuildBus(root), 2);
                    buses.Add(bus);
                }
                bus.cur = new LaneCursor { road = stopRoad[s], u = u, dir = stopDir[s], lat = L.laneOff };
                bus.skipA = bus.skipB = -1;
                bus.pref = 1f;
                bus.speed = 6f;
                bus.dwell = 0f;
                for (int w = 0; w < BusWps.Length; w++) if (BusStopAt[w] == s) bus.wp = w;
                Activate(bus, p, new Vector3(f.x, 0f, f.y));
                return;
            }
        }

        void BuildStopSigns()
        {
            signsBuilt = true;
            var g = new LifeGeo();
            for (int s = 0; s < BusStops.Length; s++)
            {
                var L = net.roads[stopRoad[s]];
                Vector3 p;
                Vector2 f;
                TrafficNet.Pose(L, stopU[s] + stopDir[s] * (6f / L.seg), stopDir[s], L.hw + 1.4f, out p, out f);
                Vector2 right = new Vector2(f.y, -f.x);
                float y = d.Ground(p.x, p.z);
                LifeVehicles.BusStop(g, new Vector3(p.x, Mathf.Max(y, p.y - 0.05f), p.z), LifeDirector.YawOf(-right));
            }
            LifeMesh.Make("LifeBusStops", g, LifeMesh.VertexColour, d.RegionRoot(RegionId.City), GameSettings.Quality >= 1);
        }

        float BusStopCap(TrafficAgent a, LaneRoad L, float dt)
        {
            int s = a.wp >= 0 && a.wp < BusStopAt.Length ? BusStopAt[a.wp] : -1;
            if (s < 0 || a.cur.road != stopRoad[s] || a.cur.dir != stopDir[s]) return 99f;
            float dist = TrafficNet.Delta(L, a.cur.u, stopU[s]) * a.cur.dir * L.seg;
            if (dist < -3f) { a.wp = (a.wp + 1) % BusWps.Length; return 99f; }
            if (a.dwell > 0f)
            {
                a.dwell -= dt;
                if (a.dwell <= 0f) a.wp = (a.wp + 1) % BusWps.Length;
                return 0f;
            }
            if (dist < 0.8f && a.speed < 0.4f)
            {
                a.dwell = 7f;
                if (d.Audio != null && a.dist < 70f) d.Audio.Hiss(a.pos);
                return 0f;
            }
            return Mathf.Sqrt(2f * 1.6f * Mathf.Max(0f, dist));
        }

        void BusPlan(TrafficAgent a)
        {
            if (a.cur.mode != 0) return;
            Vector2 target = BusWps[a.wp];
            if (BusStopAt[a.wp] < 0 && Vector2.Distance(new Vector2(a.pos.x, a.pos.z), target) < 16f)
            {
                a.wp = (a.wp + 1) % BusWps.Length;
                target = BusWps[a.wp];
            }
            if (a.cur.hasPlan) return;
            var L = net.roads[a.cur.road];
            float lookS = 36f / L.seg;
            var brs = L.branches;
            for (int b = 0; b < brs.Length; b++)
            {
                var br = brs[b];
                float ahead = TrafficNet.Delta(L, a.cur.u, br.at) * a.cur.dir;
                if (ahead < br.pre + 0.5f || ahead > lookS) continue;
                if (b == a.skipA || b == a.skipB) continue;
                bool ok = (a.cur.dir > 0 ? br.okPos : br.okNeg) && Usable(br.link.road, 2);
                Vector2 at = L.s[Mathf.Clamp((int)br.at, 0, L.n - 1)].pos;
                Vector2 to = Aim(a, at) - at;
                float tl = to.magnitude;
                if (ok && tl > 1f)
                {
                    to /= tl;
                    var S = net.roads[br.link.road];
                    Vector2 hs = S.s[Mathf.Clamp((int)br.link.idx, 0, S.n - 1)].tangent * br.link.dir;
                    Vector2 h = L.s[Mathf.Clamp((int)br.at, 0, L.n - 1)].tangent * a.cur.dir;
                    if (Vector2.Dot(hs, to) > Vector2.Dot(h, to) + 0.3f)
                    {
                        var link = br.link;
                        if (a.cur.dir > 0 ? br.leftPos : br.leftNeg) { link.yieldRoad = L.index; link.yieldIdx = br.at; link.oncoming = true; }
                        SetPlan(a, TrafficNet.Wrap(L, br.at - a.cur.dir * br.pre), link);
                        return;
                    }
                }
                a.skipB = a.skipA;
                a.skipA = b;
                return;
            }
            Vector2 here = new Vector2(a.pos.x, a.pos.z);
            Vector2 dir = Aim(a, here) - here;
            PlanEnd(a, L, lookS, dir.sqrMagnitude > 1f ? dir.normalized : Vector2.zero, true);
        }

        /// <summary>Where to steer at a junction near 'at': past a corner waypoint that is this junction, aim at the next one.</summary>
        static Vector2 Aim(TrafficAgent a, Vector2 at)
        {
            Vector2 target = BusWps[a.wp];
            if (BusStopAt[a.wp] < 0 && (target - at).sqrMagnitude < 45f * 45f) return BusWps[(a.wp + 1) % BusWps.Length];
            return target;
        }
    }
}

// Pedestrians.cs - people walking where they belong (LifeAreas), dressed for the place, pooled and streamed
// around PlayerState.Position.
//
//   * Look: PersonLook.Random + an outfit per place (business suits and casual in the city, hanfu in the valley,
//     aprons and sun hats at the harbour, bright beachwear, saffron robes at the monastery, winter coats on the
//     summit, earthy colours and sun hats at the oasis). Built with PersonFactory (code against the API only).
//   * Behaviour: walk the pavement (stopping at shop fronts, turning at the end), stroll between area points,
//     sit on benches, chat in pairs, couples walking side by side, monks praying, fishers working the quay.
//   * Cars: people step aside from an approaching car, jog away and shake a fist (playfully) when it comes
//     fast. Nobody is ever hurt: a car that touches someone makes them stumble, sit on the ground, get up
//     and wave a fist. The pedestrians have no colliders, so they never crash the car.
//   * Budget: Low 20 / Medium 40 / High 55 / Ultra 70, scaled by how much "town" is around, by night and rain.
//     Updates every frame within 40 m, every 2nd within 80 m, every 4th beyond; no allocation per frame;
//     physics queries only when someone picks a new destination (one linecast).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class Ped
    {
        public int style;
        public PersonRig rig;
        public Transform tr;
        public bool active;
        public int state;
        public float t, dur, speed, walkSpeed, accum, dist, chatT;
        public PedSite site;
        // pavement
        public PaveRoad pave;
        public float u, off;
        public int dir, side;
        public Vector3 disp;
        // area
        public LifeArea area;
        public Vector3 pos, from, to;
        public float pathT, pathLen, groundY;
        public BenchSeat seat;
        public Ped partner;
        public bool follower;
        // reactions
        public Vector3 moveDir, lookAt, tiltAxis;
        public float moveSpeed, tilt;
        public float yaw, curYaw;
        public PersonPose pose = PersonPose.Idle;
    }

    internal sealed class PedSystem
    {
        // states
        const int Walk = 0, Idle = 1, Sit = 2, Chat = 3, Activity = 4, Aside = 5, Flee = 6, Fist = 7, Fall = 8, Down = 9, GetUp = 10;
        const int Styles = 8;   // 0 business, 1 casual, 2 valley, 3 harbour, 4 beach, 5 monk, 6 winter, 7 desert

        readonly LifeDirector d;
        readonly Transform root;
        readonly List<PedSite> sites = new List<PedSite>();
        readonly List<PaveRoad> paves = new List<PaveRoad>();
        readonly List<LifeArea> areas;
        readonly List<Ped> peds = new List<Ped>();
        readonly List<int> near = new List<int>();
        readonly List<float> nearW = new List<float>();
        readonly System.Random rng = new System.Random(808);
        float nearT, totalW;
        int activeCount, created, seedCounter = 100;
        bool madeThisFrame;

        public int SiteCount { get { return sites.Count; } }

        public PedSystem(LifeDirector director)
        {
            d = director;
            root = new GameObject("Pedestrians").transform;
            root.SetParent(director.transform, false);
            LifeAreas.BuildPavements(d, paves, sites);
            areas = LifeAreas.Build();
            foreach (var a in areas)
                sites.Add(new PedSite
                {
                    kind = 1, area = a, c = a.c, r = Mathf.Max(a.half.x, a.half.y), style = a.style,
                    weight = a.density * Mathf.Clamp(a.half.x * a.half.y / 900f, 0.5f, 2.5f)
                });
        }

        float R() { return (float)rng.NextDouble(); }
        float R(float a, float b) { return a + (b - a) * (float)rng.NextDouble(); }

        // ================================================================== frame
        public void Tick(float dt)
        {
            madeThisFrame = false;
            if (!LifeDirector.Active) return;
            Vector3 player = LifeDirector.Player;
            float radius = LifeDirector.PedRadius;
            nearT -= dt;
            if (nearT <= 0f || LifeDirector.Teleported) RefreshNear(radius);

            int frame = LifeDirector.Frame;
            for (int i = 0; i < peds.Count; i++)
            {
                var p = peds[i];
                if (!p.active) continue;
                Vector3 at = p.tr.position;
                p.dist = LifeDirector.FlatDist(at, player);
                if (p.dist > radius + 25f || (LifeDirector.Teleported && p.dist > 120f)) { Release(p); continue; }
                p.accum += dt;
                int rate = p.dist < 40f ? 1 : p.dist < 80f ? 2 : 4;
                if (rate > 1 && ((frame + i) % rate) != 0) continue;
                float adt = Mathf.Min(p.accum, 0.25f);
                p.accum = 0f;
                if (p.dist < 45f) React(p);
                Step(p, adt);
            }

            int target = Mathf.RoundToInt(LifeDirector.PedBudget * LifeDirector.PedTimeFactor * Mathf.Clamp01(totalW / 6f));
            if (near.Count == 0) target = 0;
            bool burst = activeCount < target / 3 || LifeDirector.Teleported;
            for (int k = 0; k < 2 && activeCount < target; k++) TrySpawn(burst);
            if (activeCount > target + 3) Thin();
        }

        void RefreshNear(float radius)
        {
            nearT = 1f;
            near.Clear();
            nearW.Clear();
            totalW = 0f;
            Vector2 p = LifeDirector.Player2;
            bool built = false;
            for (int i = 0; i < sites.Count; i++)
            {
                var s = sites[i];
                float dd = Vector2.Distance(p, s.c) - s.r;
                if (s.kind == 1 && !s.area.built && dd < 300f && !built && d.RegionLive(s.area.region))
                {
                    LifeAreas.BuildArea(d, s.area, rng);       // one area per refresh keeps the cost tiny
                    built = true;
                }
                if (dd > radius) continue;
                if (s.kind == 1 && (!s.area.built || s.area.pts.Count == 0)) continue;
                near.Add(i);
                nearW.Add(s.weight);
                totalW += s.weight;
            }
        }

        void Thin()
        {
            for (int i = 0; i < peds.Count; i++)
            {
                var p = peds[i];
                if (p.active && p.dist > 45f && p.state <= Activity && !LifeDirector.InView(p.tr.position, 0.1f)) { Release(p); return; }
            }
        }

        // ================================================================== spawning
        void TrySpawn(bool burst)
        {
            if (near.Count == 0 || totalW <= 0f) return;
            float r = R() * totalW;
            int si = near[near.Count - 1];
            for (int k = 0; k < near.Count; k++) { r -= nearW[k]; if (r <= 0f) { si = near[k]; break; } }
            var s = sites[si];
            Vector3 pos;
            int dir = R() < 0.5f ? 1 : -1;
            float u = 0f;
            if (s.kind == 0)
            {
                var pr = paves[s.pave];
                int i = s.i0 + rng.Next(s.i1 - s.i0 + 1);
                if (!pr.Walk(s.side, i)) return;
                u = i;
                pos = PavePoint(pr, u, s.side, LifeAreas.PaveOffset);
            }
            else
            {
                var a = s.area;
                if (a.pts.Count == 0) return;
                pos = a.pts[rng.Next(a.pts.Count)];
            }
            float dist = LifeDirector.FlatDist(pos, LifeDirector.Player);
            if (dist < 10f || dist > LifeDirector.PedRadius) return;
            if (!burst && dist < 55f && LifeDirector.InView(pos, 0.05f)) return;
            bool pair = R() < (s.kind == 0 ? 0.2f : 0.28f) && activeCount + 2 <= LifeDirector.PedBudget;
            var p = Spawn(s, pos);
            if (p == null) return;
            if (s.kind == 0)
            {
                p.pave = paves[s.pave];
                p.u = u;
                p.dir = dir;
                p.side = s.side;
                p.off = LifeAreas.PaveOffset + R(-0.35f, 0.35f);
                StartWalk(p);
                if (pair)
                {
                    var q = Spawn(s, pos);
                    if (q != null)
                    {
                        q.pave = p.pave; q.u = u; q.dir = dir; q.side = s.side;
                        p.off = LifeAreas.PaveOffset - 0.32f;
                        q.off = LifeAreas.PaveOffset + 0.38f;
                        q.partner = p; q.follower = true;
                        q.walkSpeed = p.walkSpeed;
                        StartWalk(q);
                    }
                }
            }
            else
            {
                p.area = s.area;
                p.pos = pos;
                p.groundY = pos.y;
                if (pair)
                {
                    Vector3 f = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                    Vector3 qp = pos + f * 1.15f;
                    float gy = d.Ground(qp.x, qp.z);
                    if (Mathf.Abs(gy - pos.y) < 0.4f)
                    {
                        var q = Spawn(s, qp);
                        if (q != null)
                        {
                            q.area = s.area; q.pos = qp; q.groundY = Mathf.Max(gy, pos.y);
                            float dur = R(10f, 26f);
                            Begin(p, Chat, dur); Begin(q, Chat, dur);
                            p.partner = q; q.partner = p;
                            p.yaw = p.curYaw = LifeDirector.YawOf(f); q.yaw = q.curYaw = p.yaw + 180f;
                            p.chatT = R(0f, 3f); q.chatT = R(1.5f, 4f);
                            Place(p); Place(q);
                            return;
                        }
                    }
                }
                Begin(p, Idle, R(0.5f, 4f));
            }
            Place(p);
        }

        Ped Spawn(PedSite s, Vector3 pos)
        {
            int style = StyleFor(s.style);
            var p = Acquire(style, pos);
            if (p == null) return null;
            p.active = true;
            p.site = s;
            p.pave = null; p.area = null; p.partner = null; p.follower = false; p.seat = null;
            p.disp = Vector3.zero;
            p.tilt = 0f;
            p.accum = 0f;
            p.walkSpeed = R(1.0f, 1.55f) * (style == 5 ? 0.75f : 1f);
            p.speed = 0f;
            p.pos = pos;
            p.groundY = pos.y;
            p.yaw = p.curYaw = R(0f, 360f);
            p.rig.gameObject.SetActive(true);
            activeCount++;
            return p;
        }

        int StyleFor(SiteStyle s)
        {
            switch (s)
            {
                case SiteStyle.City: return R() < 0.5f ? 0 : 1;
                case SiteStyle.Valley: return 2;
                case SiteStyle.Harbour: return 3;
                case SiteStyle.Beach: return 4;
                case SiteStyle.Monastery: return R() < 0.7f ? 5 : 1;
                case SiteStyle.Snow: return 6;
                case SiteStyle.Desert: return 7;
                default: return 1;
            }
        }

        Ped Acquire(int style, Vector3 pos)
        {
            Ped other = null;
            for (int i = 0; i < peds.Count; i++)
            {
                var p = peds[i];
                if (p.active) continue;
                if (p.style == style) return p;
                if (other == null) other = p;
            }
            if (madeThisFrame) return null;
            int cap = LifeDirector.PedBudget + 8;
            if (created >= cap)
            {
                if (other == null) return null;
                // recycle a pooled person of another style (rare: only after travelling between regions)
                peds.Remove(other);
                Object.Destroy(other.rig.gameObject);
                created--;
            }
            madeThisFrame = true;
            var look = LookFor(style, seedCounter++);
            var rig = PersonFactory.Create(look, pos, 0f, root, 0);
            if (rig == null) return null;
            LifeVehicles.StripPhysics(rig.gameObject);
            if (LifeDirector.Quality == 0)
                foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rig.gameObject.SetActive(false);
            var np = new Ped { style = style, rig = rig, tr = rig.transform };
            peds.Add(np);
            created++;
            return np;
        }

        static Color Hsv(System.Random r, float h0, float h1, float s0, float s1, float v0, float v1)
        {
            return Color.HSVToRGB(Mathf.Repeat(h0 + (h1 - h0) * (float)r.NextDouble(), 1f), s0 + (s1 - s0) * (float)r.NextDouble(), v0 + (v1 - v0) * (float)r.NextDouble());
        }

        static PersonLook LookFor(int style, int seed)
        {
            var l = PersonLook.Random(seed * 7919 + style * 131);
            var r = new System.Random(seed * 31 + style);
            switch (style)
            {
                case 0:   // business
                    l.outfit = 4;
                    l.top = Hsv(r, 0.55f, 0.68f, 0.1f, 0.35f, 0.12f, 0.4f);
                    l.bottom = Color.Lerp(l.top, Color.black, 0.3f);
                    l.shoes = new Color(0.08f, 0.06f, 0.05f);
                    l.accent = Hsv(r, 0f, 1f, 0.5f, 0.8f, 0.6f, 0.9f);
                    l.hat = false;
                    break;
                case 2:   // valley villagers
                    if (r.NextDouble() < 0.3) { l.outfit = 6; l.top = Hsv(r, 0.9f, 1.1f, 0.2f, 0.45f, 0.85f, 1f); l.bottom = Hsv(r, 0.3f, 0.5f, 0.2f, 0.4f, 0.6f, 0.85f); }
                    else { l.top = Hsv(r, 0f, 1f, 0.15f, 0.4f, 0.7f, 0.95f); }
                    l.hat = r.NextDouble() < 0.2;
                    break;
                case 3:   // harbour folk
                    if (r.NextDouble() < 0.4) { l.outfit = 2; l.top = Hsv(r, 0.55f, 0.62f, 0.3f, 0.6f, 0.6f, 0.9f); l.accent = new Color(0.95f, 0.95f, 0.92f); }
                    l.hat = r.NextDouble() < 0.4;
                    break;
                case 4:   // beachgoers
                    l.top = Hsv(r, 0f, 1f, 0.55f, 0.9f, 0.85f, 1f);
                    l.bottom = r.NextDouble() < 0.5 ? new Color(0.93f, 0.88f, 0.76f) : Hsv(r, 0.45f, 0.55f, 0.4f, 0.7f, 0.7f, 0.95f);
                    l.shoes = Color.Lerp(l.skin, Color.white, 0.2f);
                    l.hat = r.NextDouble() < 0.5;
                    break;
                case 5:   // monks
                    l.outfit = 3;
                    l.top = r.NextDouble() < 0.7 ? Hsv(r, 0.07f, 0.1f, 0.75f, 0.9f, 0.8f, 0.95f) : new Color(0.55f, 0.52f, 0.5f);
                    l.bottom = l.top;
                    l.hairStyle = 0;
                    l.hat = false;
                    break;
                case 6:   // winter
                    l.outfit = 5;
                    l.top = Hsv(r, 0f, 1f, 0.5f, 0.85f, 0.55f, 0.95f);
                    l.bottom = Hsv(r, 0.6f, 0.65f, 0.1f, 0.3f, 0.15f, 0.35f);
                    l.hat = r.NextDouble() < 0.8;
                    break;
                case 7:   // oasis
                    if (r.NextDouble() < 0.3) l.outfit = 6;
                    l.top = Hsv(r, 0.05f, 0.13f, 0.25f, 0.55f, 0.6f, 0.95f);
                    l.bottom = Hsv(r, 0.07f, 0.11f, 0.2f, 0.4f, 0.4f, 0.7f);
                    l.hat = r.NextDouble() < 0.6;
                    break;
            }
            return l;
        }

        void Release(Ped p)
        {
            if (!p.active) return;
            p.active = false;
            if (p.seat != null) { p.seat.taken = false; p.seat = null; }
            if (p.partner != null && p.partner.partner == p) p.partner.partner = null;
            p.partner = null;
            p.rig.gameObject.SetActive(false);
            activeCount--;
        }

        // ================================================================== behaviour
        void Begin(Ped p, int state, float dur)
        {
            p.state = state;
            p.t = 0f;
            p.dur = dur;
        }

        void StartWalk(Ped p)
        {
            Begin(p, Walk, R(8f, 26f));
            if (p.pave != null) return;
            PickTarget(p);
        }

        void PickTarget(Ped p)
        {
            var a = p.area;
            if (p.seat != null) { p.seat.taken = false; p.seat = null; }
            if (a == null || a.pts.Count == 0) { Begin(p, Idle, R(2f, 5f)); return; }
            int mask = LifeDirector.SolidMask | (1 << CarExtras.CarLayer);
            for (int tries = 0; tries < 3; tries++)
            {
                BenchSeat seat = null;
                Vector3 to;
                if (a.seats.Count > 0 && R() < 0.25f)
                {
                    seat = a.seats[rng.Next(a.seats.Count)];
                    if (seat.taken) continue;
                    to = seat.pos;
                }
                else to = a.pts[rng.Next(a.pts.Count)];
                Vector3 from = new Vector3(p.pos.x, p.groundY, p.pos.z);
                float len = LifeDirector.FlatDist(from, to);
                if (len < 2f || len > 70f) continue;
                if (Physics.Linecast(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, mask, QueryTriggerInteraction.Ignore)) continue;
                p.from = from;
                p.to = to;
                p.pathLen = len;
                p.pathT = 0f;
                if (seat != null) { seat.taken = true; p.seat = seat; }
                Begin(p, Walk, 999f);
                return;
            }
            Begin(p, Idle, R(2f, 5f));
        }

        void Next(Ped p)
        {
            if (p.partner != null && p.state == Chat) { var q = p.partner; p.partner = null; if (q.partner == p) q.partner = null; }
            if (p.pave != null)
            {
                if (p.follower && p.partner != null) return;
                float r = R();
                if (p.state == Walk && r < 0.3f) { Begin(p, Idle, R(2.5f, 8f)); return; }
                if (r < 0.1f) p.dir = -p.dir;
                Begin(p, Walk, R(8f, 26f));
                return;
            }
            if (p.state == Walk)
            {
                if (p.seat != null) { Begin(p, Sit, R(15f, 45f)); p.yaw = p.seat.yaw; return; }
                float r = R();
                if (p.style == 5 && r < 0.3f) { Begin(p, Activity, R(6f, 14f)); return; }
                if (p.style == 3 && r < 0.18f) { Begin(p, Activity, R(8f, 18f)); return; }
                Begin(p, Idle, R(2f, 9f));
                return;
            }
            StartWalk(p);
        }

        void Step(Ped p, float dt)
        {
            p.t += dt;
            switch (p.state)
            {
                case Walk:
                    if (p.pave != null) PaveWalk(p, dt);
                    else AreaWalk(p, dt);
                    break;
                case Idle: case Sit: case Activity:
                    p.speed = 0f;
                    if (p.pave != null) p.disp = Vector3.MoveTowards(p.disp, Vector3.zero, 0.5f * dt);
                    if (p.t > p.dur) Next(p);
                    break;
                case Chat:
                    p.speed = 0f;
                    p.chatT -= dt;
                    if (p.chatT < -1.2f) p.chatT = R(1.5f, 4f);
                    if (p.t > p.dur) Next(p);
                    break;
                case Aside: case Flee:
                    Move(p, p.moveDir * p.moveSpeed * dt);
                    p.speed = p.moveSpeed;
                    p.yaw = LifeDirector.YawOf(p.moveDir);
                    if (p.t > p.dur)
                    {
                        if (p.state == Flee) Begin(p, Fist, R(1.4f, 2.2f));
                        else Resume(p);
                    }
                    break;
                case Fist:
                    p.speed = 0f;
                    FaceTo(p, p.lookAt);
                    if (p.t > p.dur) Resume(p);
                    break;
                case Fall:
                {
                    float k = Mathf.Clamp01(p.t / 0.45f);
                    Move(p, p.moveDir * p.moveSpeed * (1f - k) * dt);
                    p.tilt = 82f * Mathf.SmoothStep(0f, 1f, k);
                    p.speed = 0f;
                    if (p.t > 0.45f) Begin(p, Down, R(0.9f, 1.4f));
                    break;
                }
                case Down:
                    p.tilt = 82f;
                    if (p.t > p.dur) Begin(p, GetUp, 0.8f);
                    break;
                case GetUp:
                    p.tilt = 82f * (1f - Mathf.SmoothStep(0f, 1f, p.t / p.dur));
                    if (p.t > p.dur) { p.tilt = 0f; p.lookAt = LifeDirector.Player; Begin(p, Fist, R(1.6f, 2.4f)); }
                    break;
            }
            Place(p);
        }

        void Resume(Ped p)
        {
            if (p.pave != null) { Begin(p, Walk, R(6f, 18f)); return; }
            p.groundY = d.Ground(p.pos.x, p.pos.z);
            StartWalk(p);
        }

        void PaveWalk(Ped p, float dt)
        {
            if (p.follower && p.partner != null)
            {
                var q = p.partner;
                if (!q.active || q.pave != p.pave || (q.state != Walk && q.state != Idle)) { p.partner = null; p.follower = false; }
                else
                {
                    p.u = q.u; p.dir = q.dir; p.speed = q.speed;
                    p.disp = Vector3.MoveTowards(p.disp, Vector3.zero, 0.6f * dt);
                    if (q.state == Idle) { p.state = Idle; p.t = 0f; p.dur = q.dur - q.t + 0.2f; }
                    return;
                }
            }
            var pr = p.pave;
            int n = pr.wL.Length;
            p.speed = p.walkSpeed;
            float nu = p.u + p.dir * p.speed * dt / pr.seg;
            int next = p.dir > 0 ? Mathf.Min(n - 1, Mathf.CeilToInt(nu)) : Mathf.Max(0, Mathf.FloorToInt(nu));
            if (nu < 0f || nu > n - 1 || !pr.Walk(p.side, next))
            {
                p.dir = -p.dir;
                Begin(p, Idle, R(0.8f, 2.5f));
                return;
            }
            p.u = nu;
            p.disp = Vector3.MoveTowards(p.disp, Vector3.zero, 0.6f * dt);
            if (p.t > p.dur) Next(p);
        }

        void AreaWalk(Ped p, float dt)
        {
            p.speed = p.walkSpeed;
            p.pathT += p.speed * dt / Mathf.Max(0.5f, p.pathLen);
            if (p.pathT >= 1f)
            {
                p.pathT = 1f;
                p.pos = p.to;
                p.groundY = p.to.y;
                Next(p);
                return;
            }
            p.pos = Vector3.Lerp(p.from, p.to, p.pathT);
            p.groundY = p.pos.y;
            Vector3 dir = p.to - p.from;
            if (dir.sqrMagnitude > 1e-4f) p.yaw = LifeDirector.YawOf(dir);
        }

        void Move(Ped p, Vector3 delta)
        {
            delta.y = 0f;
            if (p.pave != null)
            {
                p.disp += delta;
                if (p.disp.sqrMagnitude > 16f) p.disp = p.disp.normalized * 4f;
            }
            else p.pos += delta;
        }

        void FaceTo(Ped p, Vector3 target)
        {
            Vector3 d3 = target - p.tr.position;
            d3.y = 0f;
            if (d3.sqrMagnitude > 0.01f) p.yaw = LifeDirector.YawOf(d3);
        }

        // ------------------------------------------------------------------ cars and the player
        void React(Ped p)
        {
            if (p.state == Fall || p.state == Down || p.state == GetUp) return;
            Vector3 at = p.tr.position;
            var car = LifeDirector.PlayerCar;
            if (car != null && LifeDirector.PlayerSpeed > 1.5f)
            {
                Transform ct = car.transform;
                Vector3 f = ct.forward, r = ct.right;
                f.y = 0f; r.y = 0f;
                Vector3 rel = at - ct.position;
                rel.y = 0f;
                float lx = Vector3.Dot(rel, r), lz = Vector3.Dot(rel, f);
                float vf = Vector3.Dot(LifeDirector.PlayerVel, f);
                if (vf < 0f) { lz = -lz; f = -f; }
                float sp = LifeDirector.PlayerSpeed;
                if (Mathf.Abs(lx) < 1.2f && Mathf.Abs(lz) < 2.5f + sp * 0.03f)
                {
                    // touched: a harmless stumble
                    if (p.seat != null) { p.seat.taken = false; p.seat = null; }
                    Vector3 push = LifeDirector.PlayerVel;
                    push.y = 0f;
                    push = push.normalized + r * Mathf.Sign(lx == 0f ? 1f : lx) * 0.6f;
                    p.moveDir = push.normalized;
                    p.moveSpeed = Mathf.Clamp(sp * 0.35f, 1.5f, 5f);
                    p.tiltAxis = Vector3.Cross(Vector3.up, p.moveDir);
                    p.lookAt = ct.position;
                    BreakPair(p);
                    Begin(p, Fall, 0.45f);
                    if (d.Audio != null) d.Audio.Oof(at);
                    return;
                }
                bool calm = p.state <= Chat || p.state == Activity || p.state == Aside;
                if (calm && sp > 4f && lz > 0f && lz < 3f + sp * 1.6f && Mathf.Abs(lx) < (p.state == Sit ? 2.2f : 3.2f))
                {
                    if (p.state == Aside && sp <= 12f) return;
                    Vector3 away = p.pave != null ? Outward(p) : r * (Mathf.Abs(lx) < 0.2f ? (R() < 0.5f ? -1f : 1f) : Mathf.Sign(lx));
                    p.moveDir = away.normalized;
                    p.lookAt = ct.position + f * 6f;
                    if (p.seat != null) { p.seat.taken = false; p.seat = null; }
                    BreakPair(p);
                    if (sp > 12f) { p.moveSpeed = R(3.2f, 4.2f); Begin(p, Flee, R(0.9f, 1.4f)); }
                    else { p.moveSpeed = 1.7f; Begin(p, Aside, 0.8f); }
                    return;
                }
            }
            if (LifeDirector.OnFoot && (p.state == Walk || p.state == Idle))
            {
                Vector3 rel = at - LifeDirector.Player;
                rel.y = 0f;
                if (rel.sqrMagnitude < 0.8f)
                {
                    p.moveDir = rel.sqrMagnitude > 1e-4f ? rel.normalized : Vector3.right;
                    p.moveSpeed = 1.1f;
                    BreakPair(p);
                    Begin(p, Aside, 0.6f);
                }
            }
        }

        void BreakPair(Ped p)
        {
            if (p.partner != null && p.partner.partner == p) p.partner.partner = null;
            p.partner = null;
            p.follower = false;
        }

        Vector3 Outward(Ped p)
        {
            var s = d.Field.roadSamples[p.pave.road];
            var rs = s[Mathf.Clamp((int)p.u, 0, s.Count - 1)];
            Vector2 o = rs.Right * p.side;
            return new Vector3(o.x, 0f, o.y);
        }

        // ------------------------------------------------------------------ placing the rig
        Vector3 PavePoint(PaveRoad pr, float u, int side, float off)
        {
            var s = d.Field.roadSamples[pr.road];
            int n = s.Count;
            u = Mathf.Clamp(u, 0f, n - 1f);
            int i = Mathf.Min((int)u, n - 2);
            float f = u - i;
            var a = s[i];
            var b = s[i + 1];
            Vector2 c = Vector2.Lerp(a.pos, b.pos, f);
            Vector2 t = Vector2.Lerp(a.tangent, b.tangent, f).normalized;
            Vector2 p = c + new Vector2(t.y, -t.x) * side * (pr.hw + off);
            return new Vector3(p.x, 0f, p.y);
        }

        void Place(Ped p)
        {
            Vector3 w;
            float y;
            if (p.pave != null)
            {
                w = PavePoint(p.pave, p.u, p.side, p.off) + p.disp;
                y = d.Ground(w.x, w.z);
                if (p.state == Walk || p.state == Idle)
                {
                    var s = d.Field.roadSamples[p.pave.road];
                    var rs = s[Mathf.Clamp((int)p.u, 0, s.Count - 1)];
                    Vector2 h = p.state == Walk ? rs.tangent * p.dir : rs.Right * p.side;
                    p.yaw = LifeDirector.YawOf(h);
                }
            }
            else
            {
                w = p.pos;
                float g = d.Ground(w.x, w.z);
                y = Mathf.Max(g, Mathf.Abs(p.groundY - g) < 0.9f ? p.groundY : g);
            }
            if (p.state == Chat && p.partner != null) FaceTo(p, p.partner.tr.position);
            w.y = y;
            float yaw = p.curYaw = Mathf.MoveTowardsAngle(p.curYaw, p.yaw, 400f * Mathf.Max(Time.deltaTime, 0.016f) * (p.dist < 40f ? 1f : 4f));
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            if (p.tilt > 0.5f) rot = Quaternion.AngleAxis(p.tilt, p.tiltAxis) * rot;
            p.tr.SetPositionAndRotation(w, rot);
            p.rig.speed = p.speed;
            SetPose(p, PoseFor(p));
        }

        PersonPose PoseFor(Ped p)
        {
            switch (p.state)
            {
                case Walk: return p.speed > 2.5f ? PersonPose.Run : (p.speed > 0.05f ? PersonPose.Walk : PersonPose.Idle);
                case Sit: return PersonPose.Sit;
                case Chat: return p.chatT < 0f ? PersonPose.Wave : PersonPose.Idle;
                case Activity: return p.style == 5 ? PersonPose.Pray : PersonPose.Work;
                case Aside: return PersonPose.Walk;
                case Flee: return PersonPose.Run;
                case Fist: return PersonPose.Wave;
                default: return PersonPose.Idle;
            }
        }

        static void SetPose(Ped p, PersonPose pose)
        {
            if (p.pose == pose && p.rig.pose == pose) return;
            p.pose = pose;
            p.rig.SetPose(pose);
        }
    }
}

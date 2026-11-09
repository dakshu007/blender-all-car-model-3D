// RallyCrowds.cs - spectator areas at scenic corners (the safe INSIDE of bends), crests ("jumps") and
// stage starts / finishes: dozens of low-poly spectators per area (one LifeBatch each), tape between
// them and the road, tents, food stalls, flags and parked cars (cars have colliders, people never do).
// Spectators idle and clap, cheer with arms up when the player passes fast, drifts or lands a jump
// nearby (GameEvents.DriftBanked / NearMiss / Landed + proximity), and hop back when a car comes
// within ~6 m at speed. Cheering raises GameEvents.Cheer and plays a procedural 3D crowd roar.
// Also: kids by village / meadow roads who fly kites and run a few metres alongside your car
// (on a path that is always >= 4 m from any road edge).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyCrowds : RallyModule
    {
        sealed class Fan
        {
            public int body, armL, armR;
            public Vector3 home;
            public Vector2 back;          // unit vector pointing away from the road
            public float yaw, baseYaw, scale, phase, cheer, delay;
            public Vector3 offset, fleeTarget;
            public float fleeT, calm;
            public bool fleeing, flag;
        }

        sealed class Area
        {
            public LifeBatch batch;
            public readonly List<Fan> fans = new List<Fan>();
            public Vector3 centre;
            public float radius, excite, cool, lastSeen;
            public int salt;
        }

        sealed class Kid
        {
            public int body, armL, armR, legL, legR;
            public float scale, phase, yaw, baseYaw, s, swing, wave;
            public int state;                 // 0 play, 1 run, 2 wave, 3 walk back
            public float dir, timer;
        }

        sealed class KidGroup
        {
            public Vector3[] path;            // validated points (>= 4 m off any road), home = middle
            public int home;
            public readonly List<Kid> kids = new List<Kid>();
            public int kite = -1, line = -1;
            public Vector3 kitePos;
            public Vector3 centre;
            public Vector2 roadTangent;
        }

        readonly List<Area> areas = new List<Area>();
        readonly List<KidGroup> groups = new List<KidGroup>();
        LifeBatch kidBatch;
        public bool Raising { get; private set; }
        public int SpectatorCount { get; private set; }
        public int AreaCount { get { return areas.Count; } }
        public int PropCount { get; private set; }
        public override string Summary { get { return areas.Count + " areas, " + SpectatorCount + " spectators, " + PropCount + " props, " + groups.Count + " kid groups"; } }

        struct Cand { public int road, idx, side; public float score; }

        public override void Build()
        {
            var T = L.Track;
            var cands = new List<Cand>();
            foreach (var st in T.stages)
            {
                var c = st.course;
                int i0 = c.IndexAt(c.startD - 16f);
                cands.Add(new Cand { road = st.def.road, idx = i0, side = 1, score = 500f });
                cands.Add(new Cand { road = st.def.road, idx = i0, side = -1, score = 300f });
                if (!st.closed)
                {
                    int i1 = c.IndexAt(c.endD + 4f);
                    cands.Add(new Cand { road = st.def.road, idx = i1, side = -1, score = 450f });
                    cands.Add(new Cand { road = st.def.road, idx = i1, side = 1, score = 280f });
                }
            }
            foreach (var c in T.corners)
            {
                if (c.peakTurn < 22f || !T.OnStage(c.road, c.apex)) continue;
                cands.Add(new Cand { road = c.road, idx = c.apex, side = -c.outside, score = Mathf.Abs(c.totalTurn) + (c.Hairpin ? 60f : 0f) });
            }
            foreach (var cr in T.crests)
            {
                if (!T.OnStage(cr.road, cr.idx)) continue;
                cands.Add(new Cand { road = cr.road, idx = cr.idx, side = Chance(0.5f) ? 1 : -1, score = 90f + cr.lift * 60f });
            }
            foreach (var lk in MapData.Lookouts)
            {
                RoadSample rs;
                if (W.NearestRoad(lk, out rs, 40f) > 40f || rs.road >= RallyTrack.StageRoads) continue;
                int best = 0; float bd = float.MaxValue;
                var s = T.S(rs.road);
                for (int i = 0; i < s.Count; i++) { float d = (s[i].pos - lk).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                int side = Vector2.Dot(lk - s[best].pos, RallyTrack.Right(s[best])) > 0f ? 1 : -1;
                cands.Add(new Cand { road = rs.road, idx = best, side = side, score = 120f });
            }
            cands.Sort((a, b) => b.score.CompareTo(a.score));
            int maxAreas = Mathf.Clamp(Mathf.RoundToInt(12 * L.Density), 5, 15);
            var picked = new List<Vector2>();
            foreach (var c in cands)
            {
                if (areas.Count >= maxAreas) break;
                Vector2 p = T.At(c.road, c.idx).pos;
                bool far = true;
                foreach (var q in picked) if ((q - p).sqrMagnitude < 95f * 95f) far = false;
                if (!far) continue;
                if (BuildArea(c.road, c.idx, c.side)) picked.Add(p);
            }
            BuildKids();
        }

        // ---------------------------------------------------------------- spectator areas
        bool BuildArea(int road, int idx, int side)
        {
            var T = L.Track;
            var spots = new List<Vector4>();
            var backs = new List<Vector2>();
            int want = Mathf.RoundToInt(Mathf.Lerp(12f, 30f, Mathf.Clamp01(L.Density - 0.25f)) * R(0.85f, 1.15f));
            float[] rows = { 4.7f, 5.6f, 6.5f, 7.4f };
            float roadY = T.At(road, idx).height;
            for (int row = 0; row < rows.Length && spots.Count < want; row++)
                for (int k = -9; k <= 9 && spots.Count < want; k++)
                {
                    int i = idx + k;
                    if (!T.Valid(road, i)) continue;
                    var s = T.At(road, i);
                    if (s.bridge) continue;
                    Vector2 n = RallyTrack.Right(s) * side;
                    for (int sub = 0; sub < 2 && spots.Count < want; sub++)
                    {
                        Vector2 p = s.pos + n * (s.halfWidth + rows[row] + R(-0.25f, 0.25f)) + s.tangent * (sub * 1.0f + R(-0.3f, 0.3f));
                        if (L.EdgeDist(p) < 4.2f) continue;
                        if (!PlacementRegistry.CanPlace(W, p, 0.34f, 4f)) continue;
                        float gy = L.Ground(p.x, p.y);
                        if (Mathf.Abs(gy - roadY) > 3.5f || !L.ClearOfColliders(p, 0.3f)) continue;
                        PlacementRegistry.Reserve(p, 0.34f);
                        Vector2 face = -n + s.tangent * R(-0.35f, 0.35f) + (T.At(road, idx).pos - s.pos) * 0.03f;
                        spots.Add(new Vector4(p.x, gy, p.y, RallyLife.Yaw(face)));
                        backs.Add(n);
                    }
                }
            if (spots.Count < 5) return false;

            var area = new Area { batch = new LifeBatch(), salt = areas.Count * 7 };
            // a small pool of looks -> templates shared by several fans
            int looks = Mathf.Min(10, spots.Count);
            var bodyT = new int[looks]; var armT = new int[looks]; var flagT = new int[looks];
            Color32[] flagCols = { LifeGeo.C(222, 30, 36), LifeGeo.C(250, 214, 40), LifeGeo.C(240, 130, 170), LifeGeo.C(60, 120, 210) };
            for (int k = 0; k < looks; k++)
            {
                var look = RallyShapes.RandomLook(Rng, RallyShapes.Standing);
                bodyT[k] = area.batch.AddTemplate(RallyShapes.Person(look));
                armT[k] = area.batch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
                flagT[k] = area.batch.AddTemplate(RallyShapes.FlagArm(look.top, look.skin, flagCols[k % flagCols.Length]));
            }
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < spots.Count; i++)
            {
                int lk = RI(looks);
                var f = new Fan();
                f.flag = Chance(0.18f);
                f.body = area.batch.AddPart(bodyT[lk]);
                f.armL = area.batch.AddPart(armT[lk]);
                f.armR = area.batch.AddPart(f.flag ? flagT[lk] : armT[lk]);
                f.home = new Vector3(spots[i].x, spots[i].y - 0.02f, spots[i].z);
                f.baseYaw = f.yaw = spots[i].w;
                f.back = backs[i];
                f.scale = Chance(0.15f) ? R(0.62f, 0.72f) : R(0.92f, 1.07f);
                f.phase = R(0f, 50f);
                f.delay = R(0f, 0.35f);
                area.fans.Add(f);
                sum += f.home;
            }
            area.centre = sum / area.fans.Count;
            foreach (var f in area.fans) area.radius = Mathf.Max(area.radius, (f.home - area.centre).magnitude);
            area.batch.Finish("Crowd_" + areas.Count, MaterialLibrary.VertexColorWind, Root, 0, true);
            areas.Add(area);
            SpectatorCount += area.fans.Count;
            for (int i = 0; i < area.fans.Count; i++) PoseFan(area, area.fans[i], 0f);
            area.batch.Upload();

            // tape between the road and the crowd
            var posts = new List<Vector3>();
            for (int k = -10; k <= 10; k += 2)
            {
                int i = idx + k;
                if (!T.Valid(road, i)) continue;
                var s = T.At(road, i);
                Vector2 p = s.pos + RallyTrack.Right(s) * (side * (s.halfWidth + 3.0f));
                bool ok = !s.bridge && L.EdgeDist(p) > 2.7f && PlacementRegistry.IsFree(p, 0.1f) && Mathf.Abs(L.Ground(p.x, p.y) - s.height) < 1.5f
                          && W.RiverDistance(p) > 15f && MapData.LakeK(p) > 1.3f;
                if (ok) { posts.Add(L.Ground(p)); PlacementRegistry.Reserve(p, 0.12f); }
                if ((!ok || k >= 9) && posts.Count > 0)
                {
                    if (posts.Count >= 3) RallyCorners.TapeLine(L.Static, posts);
                    posts.Clear();
                }
            }
            BuildAreaProps(road, idx, side);
            return true;
        }

        void BuildAreaProps(int road, int idx, int side)
        {
            var T = L.Track;
            var S = L.Static;
            Color32[] roofs = { LifeGeo.C(226, 52, 60), LifeGeo.C(246, 246, 240), LifeGeo.C(60, 120, 200), LifeGeo.C(240, 150, 180), LifeGeo.C(250, 200, 60) };
            Color32[] paints = { LifeGeo.C(200, 40, 44), LifeGeo.C(236, 236, 232), LifeGeo.C(40, 44, 54), LifeGeo.C(70, 120, 190), LifeGeo.C(150, 160, 168), LifeGeo.C(236, 190, 70) };
            int tents = 1 + RI(2), stalls = Chance(0.7f) ? 1 : 0, cars = 1 + RI(3), flags = 2 + RI(2);
            for (int attempt = 0; attempt < 60 && (tents + stalls + cars + flags) > 0; attempt++)
            {
                int i = idx + RI(25) - 12;
                if (!T.Valid(road, i)) continue;
                var s = T.At(road, i);
                Vector2 n = RallyTrack.Right(s) * side;
                int kind = tents > 0 ? 0 : stalls > 0 ? 1 : cars > 0 ? 2 : 3;
                float off = kind == 3 ? R(8.5f, 10f) : R(10.5f, 16f);
                Vector2 p = s.pos + n * (s.halfWidth + off) + s.tangent * R(-2f, 2f);
                float rad = kind == 0 ? 2.3f : kind == 1 ? 1.9f : kind == 2 ? 2.4f : 0.5f;
                if (!TryPlace(p, rad, kind == 3 ? 6f : 7f, kind == 3 ? 2f : 0.7f)) continue;
                Vector3 g = L.Ground(p);
                Quaternion faceRoad = Quaternion.LookRotation(new Vector3(-n.x, 0f, -n.y), Vector3.up);
                switch (kind)
                {
                    case 0:
                        S.Add(RallyShapes.Tent(roofs[RI(roofs.Length)], roofs[RI(roofs.Length)]), g - Vector3.up * 0.05f, faceRoad * Quaternion.Euler(0f, R(-10f, 10f), 0f));
                        tents--; break;
                    case 1:
                        S.Add(RallyShapes.FoodStall(LifeGeo.C(222, 44, 48), LifeGeo.C(248, 240, 226)), g - Vector3.up * 0.05f, faceRoad);
                        stalls--; break;
                    case 2:
                        Quaternion cr = Quaternion.LookRotation(new Vector3(s.tangent.x, 0f, s.tangent.y) * (Chance(0.5f) ? 1f : -1f), Vector3.up) * Quaternion.Euler(0f, R(-25f, 25f), 0f);
                        S.Add(RallyShapes.ParkedCar(paints[RI(paints.Length)]), g - Vector3.up * 0.02f, cr);
                        var go = new GameObject("ParkedCar");
                        go.transform.SetParent(Root, false);
                        go.transform.SetPositionAndRotation(g, cr);
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = new Vector3(0f, 0.78f, 0f);
                        bc.size = new Vector3(1.75f, 1.45f, 3.95f);
                        cars--; break;
                    default:
                        if (Chance(0.5f)) S.Add(RallyShapes.FeatherFlag(roofs[RI(roofs.Length)], roofs[RI(roofs.Length)], R(3.4f, 4.4f)), g, Quaternion.Euler(0f, R(0f, 360f), 0f));
                        else S.Add(RallyShapes.FlagPole(roofs[RI(roofs.Length)], roofs[RI(roofs.Length)], R(4.5f, 5.5f)), g, Quaternion.Euler(0f, R(0f, 360f), 0f));
                        flags--; break;
                }
                PropCount++;
            }
        }

        // ---------------------------------------------------------------- reactions
        /// <summary>Excite crowds within 'radius' of pos. raise = play the roar + GameEvents.Cheer.</summary>
        public void Excite(Vector3 pos, float radius, float loud, bool raise)
        {
            loud = Mathf.Clamp01(loud);
            for (int i = 0; i < areas.Count; i++)
            {
                var a = areas[i];
                float r = radius + a.radius;
                if ((a.centre - pos).sqrMagnitude > r * r) continue;
                a.excite = Mathf.Max(a.excite, loud);
                if (raise && a.cool <= 0f && loud > 0.15f)
                {
                    a.cool = 4.5f;
                    L.Audio.Play(RallyAudio.Cheer(i % 2), a.centre + Vector3.up * 1.5f, 0.35f + 0.6f * loud, R(0.93f, 1.07f), 12f, 260f);
                    Raising = true;
                    try { GameEvents.RaiseCheer(a.centre, loud); }
                    finally { Raising = false; }
                }
            }
        }

        public override void Tick(float dt)
        {
            bool hasCar = L.HasCar;
            Vector3 car = L.CarPos;
            float kmh = L.CarSpeed * 3.6f;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var a = areas[ai];
                if (a.cool > 0f) a.cool -= dt;
                a.excite = Mathf.Max(0f, a.excite - dt * 0.28f);
                if (hasCar)
                {
                    float d = (car - a.centre).magnitude - a.radius;
                    if (d < 30f)
                    {
                        float fast = Mathf.Clamp01((kmh - 45f) / 70f);
                        float drift = L.Car != null && L.Car.IsDrifting ? Mathf.Clamp01(0.4f + L.Car.DriftIntensity) : 0f;
                        float loud = Mathf.Max(fast, drift) * Mathf.Clamp01(1.2f - d / 35f);
                        if (loud > 0.2f) Excite(a.centre, 0f, loud, true);
                    }
                }
                if (!L.Due(a.centre, a.salt, 80f, 180f, 300f)) continue;
                bool near = hasCar && (car - a.centre).sqrMagnitude < (a.radius + 25f) * (a.radius + 25f);
                for (int i = 0; i < a.fans.Count; i++)
                {
                    var f = a.fans[i];
                    if (near) Flee(f, car, dt);
                    else if (f.fleeing || f.offset.sqrMagnitude > 0f) Return(f, dt);
                    float target = a.excite > f.delay * 0.5f ? a.excite : 0f;
                    f.cheer = Mathf.MoveTowards(f.cheer, target, dt * 3f);
                    float yawT = f.baseYaw;
                    if (hasCar)
                    {
                        Vector3 dc = car - f.home;
                        if (dc.sqrMagnitude < 70f * 70f) yawT = RallyLife.Yaw(dc);
                    }
                    f.yaw = Mathf.MoveTowardsAngle(f.yaw, yawT, 160f * dt);
                    PoseFan(a, f, dt);
                }
                a.batch.Upload();
            }
            UpdateKids(dt);
        }

        void Flee(Fan f, Vector3 car, float dt)
        {
            Vector3 pos = f.home + f.offset;
            Vector3 d = pos - car;
            d.y = 0f;
            float dist = d.magnitude;
            if (!f.fleeing && dist < 6.5f && L.CarSpeed > 4f)
            {
                Vector2 away = dist > 0.01f ? new Vector2(d.x, d.z) / dist : f.back;
                Vector2 dir = (away * 0.5f + f.back).normalized;      // never towards the road
                Vector3 t = f.home + new Vector3(dir.x, 0f, dir.y) * R(1.6f, 2.3f);
                f.fleeTarget = new Vector3(t.x, L.Ground(t.x, t.z) - f.home.y - 0.02f, t.z) - new Vector3(f.home.x, 0f, f.home.z);
                f.fleeing = true;
                f.fleeT = 0f;
                f.cheer = 0f;
            }
            if (f.fleeing)
            {
                f.fleeT += dt;
                f.offset = Vector3.MoveTowards(f.offset, f.fleeTarget, dt * 5.5f);
                if (dist > 10f) f.calm += dt; else f.calm = 0f;
                if (f.calm > 1.5f) f.fleeing = false;
            }
            else if (f.offset.sqrMagnitude > 0f) Return(f, dt);
        }

        static void Return(Fan f, float dt)
        {
            f.fleeing = false;
            f.offset = Vector3.MoveTowards(f.offset, Vector3.zero, dt * 0.8f);
        }

        void PoseFan(Area a, Fan f, float dt)
        {
            float t = L.Clock + f.phase;
            Vector3 p = f.home + f.offset;
            float panic = f.fleeing ? Mathf.Clamp01(1f - f.fleeT * 0.8f) : 0f;
            // hop while fleeing, bounce while cheering
            if (f.fleeing && f.fleeT < 0.45f) p.y += Mathf.Sin(f.fleeT / 0.45f * Mathf.PI) * 0.35f;
            p.y += Mathf.Max(0f, Mathf.Sin(t * 8.5f)) * 0.12f * f.cheer;
            float lean = f.fleeing ? -8f * panic : 0f;
            Quaternion rot = Quaternion.Euler(lean + Mathf.Sin(t * 1.3f) * 1.2f, f.yaw, Mathf.Sin(t * 0.8f) * 2f);
            float s = f.scale;
            Matrix4x4 m = Matrix4x4.TRS(p, rot, new Vector3(s, s, s));
            a.batch.Set(f.body, m);
            float idle = Mathf.Sin(t * 1.1f) * 4f;
            float clap = a.excite > 0.05f && f.cheer < 0.4f ? Mathf.Clamp01(a.excite * 3f) : 0f;
            float clapSwing = Mathf.Abs(Mathf.Sin(t * 12f)) * 18f * clap;
            float up = Mathf.Max(f.cheer, panic);
            float rRaise = Mathf.Lerp(7f + idle * 0.5f + clapSwing, 150f + Mathf.Sin(t * 9f) * (f.flag ? 35f : 20f), up);
            float lRaise = Mathf.Lerp(7f - idle * 0.5f + clapSwing, 145f + Mathf.Sin(t * 9f + 1.3f) * 20f, up);
            float fwd = Mathf.Lerp(-55f * clap, 10f, up);
            a.batch.Set(f.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(fwd + idle, 0f, rRaise), Vector3.one));
            a.batch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(fwd - idle, 0f, -lRaise), Vector3.one));
        }

        // ---------------------------------------------------------------- kids
        void BuildKids()
        {
            var T = L.Track;
            int want = Mathf.Clamp(Mathf.RoundToInt(5 * L.Density), 2, 6);
            var homes = new List<Vector2>();
            for (int attempt = 0; attempt < 400 && groups.Count < want; attempt++)
            {
                int road = attempt % 3 == 0 ? 4 : 0;
                if (road >= W.roadSamples.Count) continue;
                var s = T.S(road);
                int idx = RI(s.Count);
                var rs = s[idx];
                if (rs.bridge || MapData.InBambooForest(rs.pos) || MapData.InTeaTerraces(rs.pos)) continue;
                if (Mathf.Abs(L.Track.turn[road][idx]) > 10f) continue;
                bool far = true;
                foreach (var h in homes) if ((h - rs.pos).sqrMagnitude < 120f * 120f) far = false;
                foreach (var a in areas) if ((new Vector2(a.centre.x, a.centre.z) - rs.pos).sqrMagnitude < 50f * 50f) far = false;
                if (!far) continue;
                int side = Chance(0.5f) ? 1 : -1;
                // run path: +-9 samples (~18 m) along the road at 5.5 m beyond the edge
                var path = new List<Vector3>();
                bool ok = true;
                for (int k = -9; k <= 9 && ok; k++)
                {
                    if (!T.Valid(road, idx + k)) { ok = false; break; }
                    var q = T.At(road, idx + k);
                    Vector2 p = q.pos + RallyTrack.Right(q) * (side * (q.halfWidth + 5.5f));
                    if (L.EdgeDist(p) < 4.3f || !PlacementRegistry.CanPlace(W, p, 0.5f, 4.2f) || L.Bumpiness(p, 0.6f) > 0.5f
                        || !L.ClearOfColliders(p, 0.4f)) ok = false;
                    else path.Add(L.Ground(p));
                }
                if (!ok || path.Count < 15) continue;
                foreach (var p in path) PlacementRegistry.Reserve(new Vector2(p.x, p.z), 0.6f);
                var grp = new KidGroup { path = path.ToArray(), home = path.Count / 2, roadTangent = rs.tangent };
                grp.centre = grp.path[grp.home];
                groups.Add(grp);
                homes.Add(rs.pos);
            }
            if (groups.Count == 0) return;
            kidBatch = new LifeBatch();
            int kiteT0 = kidBatch.AddTemplate(LifeShapes.KiteDiamond(LifeGeo.C(226, 52, 44), LifeGeo.C(250, 204, 70)));
            int kiteT1 = kidBatch.AddTemplate(LifeShapes.KiteSwallow(LifeGeo.C(40, 70, 150), LifeGeo.C(246, 196, 64)));
            int lineT = kidBatch.AddTemplate(RallyShapes.StringUnit(LifeGeo.C(240, 236, 226)));
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];
                int n = 2 + RI(2);
                for (int k = 0; k < n; k++)
                {
                    var look = RallyShapes.RandomLook(Rng, RallyShapes.LegsSeparate);
                    if (look.hatStyle == RallyShapes.HatConical || look.hatStyle == RallyShapes.HatElder) look.hatStyle = RallyShapes.HatNone;
                    var kid = new Kid
                    {
                        body = kidBatch.AddPart(kidBatch.AddTemplate(RallyShapes.Person(look))),
                        scale = R(0.58f, 0.68f), phase = R(0f, 30f),
                        s = grp.home + (k - n * 0.5f) * 1.2f
                    };
                    int at = kidBatch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
                    int lt = kidBatch.AddTemplate(RallyShapes.Leg(look.bottom));
                    kid.armL = kidBatch.AddPart(at); kid.armR = kidBatch.AddPart(at);
                    kid.legL = kidBatch.AddPart(lt); kid.legR = kidBatch.AddPart(lt);
                    grp.kids.Add(kid);
                }
                // face the road: towards the nearest road sample
                RoadSample rs;
                W.NearestRoad(new Vector2(grp.centre.x, grp.centre.z), out rs, 40f);
                float faceYaw = RallyLife.Yaw(new Vector3(rs.pos.x - grp.centre.x, 0f, rs.pos.y - grp.centre.z));
                foreach (var kid in grp.kids) kid.baseYaw = kid.yaw = faceYaw + R(-35f, 35f);
                if (g % 2 == 0 || groups.Count == 1)
                {
                    grp.kite = kidBatch.AddPart(g % 4 == 0 ? kiteT0 : kiteT1);
                    grp.line = kidBatch.AddPart(lineT);
                    grp.kitePos = grp.centre + Vector3.up * 8f + ValleyLife.Wind * 6f;
                }
            }
            kidBatch.Finish("Kids", MaterialLibrary.VertexColorWind, Root, 0, true);
            for (int g = 0; g < groups.Count; g++) PoseGroup(groups[g], 0f, true);
            kidBatch.Upload();
        }

        Vector3 PathAt(KidGroup g, float s)
        {
            s = Mathf.Clamp(s, 0f, g.path.Length - 1.001f);
            int i = Mathf.FloorToInt(s);
            return Vector3.Lerp(g.path[i], g.path[i + 1], s - i);
        }

        void UpdateKids(float dt)
        {
            if (kidBatch == null) return;
            bool any = false;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                bool due = L.Due(g.centre, gi * 5, 80f, 160f, 260f);
                // react to the car even when not drawn this frame
                if (L.HasCar && L.CarSpeed > 7f && (L.CarPos - g.centre).sqrMagnitude < 26f * 26f)
                {
                    float along = Vector2.Dot(new Vector2(L.CarVel.x, L.CarVel.z), g.roadTangent);
                    // path index grows with the road sample index, so run in the car's direction
                    foreach (var k in g.kids)
                        if (k.state == 0 || k.state == 3) { k.state = 1; k.dir = along >= 0f ? 1f : -1f; k.timer = 0f; }
                }
                foreach (var k in g.kids)
                {
                    k.timer += dt;
                    switch (k.state)
                    {
                        case 1:   // run alongside (2 m per path step -> ~5 m/s)
                            k.s += k.dir * dt * 2.4f;
                            if (k.s <= 0.2f || k.s >= g.path.Length - 1.2f || k.timer > 2.6f) { k.state = 2; k.timer = 0f; }
                            break;
                        case 2:   // stop and wave after the car
                            if (k.timer > 2.2f) { k.state = 3; k.timer = 0f; }
                            break;
                        case 3:   // walk back home
                            float target = g.home + (g.kids.IndexOf(k) - g.kids.Count * 0.5f) * 1.2f;
                            k.s = Mathf.MoveTowards(k.s, target, dt * 0.6f);
                            if (Mathf.Abs(k.s - target) < 0.01f) k.state = 0;
                            break;
                    }
                }
                if (!due) continue;
                PoseGroup(g, dt, false);
                any = true;
            }
            if (any) kidBatch.Upload();
        }

        void PoseGroup(KidGroup g, float dt, bool force)
        {
            float clock = L.Clock;
            Vector3 hand = g.centre + Vector3.up * 0.9f;
            for (int i = 0; i < g.kids.Count; i++)
            {
                var k = g.kids[i];
                float t = clock + k.phase;
                Vector3 p = PathAt(g, k.s);
                float yawT = k.baseYaw;
                float run = 0f;
                if (k.state == 1 || k.state == 3)
                {
                    Vector3 ahead = PathAt(g, k.s + (k.state == 1 ? k.dir : Mathf.Sign(g.home - k.s + 0.001f)) * 0.5f) - p;
                    if (ahead.sqrMagnitude > 1e-5f) yawT = RallyLife.Yaw(ahead);
                    run = k.state == 1 ? 1f : 0.45f;
                }
                else if (k.state == 2 && L.HasCar) yawT = RallyLife.Yaw(L.CarPos - p);
                k.yaw = Mathf.MoveTowardsAngle(k.yaw, yawT, (force ? 999f : 360f) * Mathf.Max(dt, 0.016f));
                k.swing = Mathf.MoveTowards(k.swing, run, Mathf.Max(dt, 0.016f) * 4f);
                k.wave = Mathf.MoveTowards(k.wave, k.state == 2 ? 1f : 0f, Mathf.Max(dt, 0.016f) * 4f);
                float freq = k.state == 1 ? 11f : 6f;
                float ph = Mathf.Sin(t * freq);
                p.y += Mathf.Abs(Mathf.Cos(t * freq)) * 0.06f * k.swing + Mathf.Max(0f, Mathf.Sin(t * 3f) - 0.9f) * 1.5f * (1f - k.swing) * (k.state == 0 ? 1f : 0f);
                float sc = k.scale;
                Matrix4x4 m = Matrix4x4.TRS(p, Quaternion.Euler(8f * k.swing, k.yaw, 0f), new Vector3(sc, sc, sc));
                kidBatch.Set(k.body, m);
                float legA = ph * 38f * k.swing;
                kidBatch.Set(k.legR, m * Matrix4x4.TRS(RallyShapes.HipR, Quaternion.Euler(legA, 0f, 0f), Vector3.one));
                kidBatch.Set(k.legL, m * Matrix4x4.TRS(RallyShapes.HipL, Quaternion.Euler(-legA, 0f, 0f), Vector3.one));
                float armA = -ph * 45f * k.swing;
                bool kiteHolder = g.kite >= 0 && i == 0;
                float rz = Mathf.Lerp(8f, 150f + Mathf.Sin(t * 10f) * 25f, k.wave);
                float rx = armA;
                if (kiteHolder && k.state == 0) { rz = 60f; rx = -70f; }
                kidBatch.Set(k.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(rx, 0f, rz), Vector3.one));
                kidBatch.Set(k.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(-armA, 0f, -8f - 6f * k.swing), Vector3.one));
                if (kiteHolder) hand = (m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(rx, 0f, rz), Vector3.one)).MultiplyPoint3x4(new Vector3(0f, -0.5f, 0f));
            }
            if (g.kite < 0) return;
            float gust = Atmosphere.Gust;
            float tt = clock + g.centre.x;
            Vector3 side = new Vector3(-ValleyLife.Wind.z, 0f, ValleyLife.Wind.x);
            Vector3 want = hand + Vector3.up * (8f + Mathf.Sin(tt * 0.4f) * 0.8f + gust * 1.5f) + ValleyLife.Wind * 5.5f
                           + side * (Mathf.Sin(tt * 0.33f) * (1.5f + 2f * gust));
            g.kitePos = force ? want : Vector3.Lerp(g.kitePos, want, 1f - Mathf.Exp(-Mathf.Max(dt, 0.016f) * 1.5f));
            Vector3 toHand = hand - g.kitePos;
            Quaternion kr = Quaternion.LookRotation((toHand.normalized * 0.7f - ValleyLife.Wind * 0.3f).normalized, Vector3.up)
                            * Quaternion.Euler(0f, 0f, Mathf.Sin(tt * 1.1f) * 14f);
            kidBatch.Set(g.kite, Matrix4x4.TRS(g.kitePos, kr, new Vector3(0.75f, 0.75f, 0.75f)));
            Vector3 anchor = Matrix4x4.TRS(g.kitePos, kr, new Vector3(0.75f, 0.75f, 0.75f)).MultiplyPoint3x4(new Vector3(0f, 0.1f, 0.12f));
            Vector3 dl = hand - anchor;
            if (dl.sqrMagnitude > 1e-4f)
                kidBatch.Set(g.line, Matrix4x4.TRS(anchor, Quaternion.LookRotation(dl.normalized, Vector3.up), new Vector3(1f, 1f, dl.magnitude)));
        }
    }
}

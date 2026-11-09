// RallyCorners.cs - safety dressing along the stages, read from the road tangents (RallyTrack):
//  * hay bale walls / tyre walls on the OUTSIDE of tight corners, >= 1.5 m beyond the road edge. They
//    are solid (soft, low-bounce physics material); pieces near the player car turn into light
//    rigidbodies (limited number) so the car shoves them around, then settle and later reset.
//  * red / white barrier tape on posts along fast sections (crowd areas add their own),
//  * 100 / 50 m distance boards and chevron boards before sharp bends,
//  * marshals in orange vests waving yellow flags before hairpins and bridges.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyCorners : RallyModule
    {
        const int MaxActive = 12;

        sealed class Piece
        {
            public int part;
            public Rigidbody rb;
            public Transform tr;
            public Vector3 homePos;
            public Quaternion homeRot;
            public float wokeAt, restAt;
            public bool active, displaced;
            public Site site;
        }

        sealed class Site
        {
            public LifeBatch batch;
            public readonly List<Piece> pieces = new List<Piece>();
            public Vector3 centre;
            public float radius;
            public bool dirty;
        }

        sealed class Marshal
        {
            public int body, armL, armR;
            public Vector3 pos;
            public float baseYaw, yaw, wave, phase;
            public float lastDist = 999f;
        }

        readonly List<Site> sites = new List<Site>();
        readonly List<Piece> active = new List<Piece>();
        readonly List<Marshal> marshals = new List<Marshal>();
        LifeBatch marshalBatch;
        Transform colliderRoot;
#if UNITY_6000_0_OR_NEWER
        PhysicsMaterial hayMat, tyreMat;
#else
        PhysicMaterial hayMat, tyreMat;
#endif
        public int PieceCount { get; private set; }
        public int TapePosts { get; private set; }
        public int Boards { get; private set; }
        public override string Summary { get { return sites.Count + " walls / " + PieceCount + " pieces, " + TapePosts + " tape posts, " + Boards + " boards, " + marshals.Count + " marshals"; } }

        public override void Build()
        {
            colliderRoot = new GameObject("BarrierColliders").transform;
            colliderRoot.SetParent(Root, false);
#if UNITY_6000_0_OR_NEWER
            hayMat = new PhysicsMaterial("RallyHay") { dynamicFriction = 0.8f, staticFriction = 0.9f, bounciness = 0.02f,
                bounceCombine = PhysicsMaterialCombine.Minimum, frictionCombine = PhysicsMaterialCombine.Average };
            tyreMat = new PhysicsMaterial("RallyTyres") { dynamicFriction = 0.7f, staticFriction = 0.8f, bounciness = 0.3f,
                bounceCombine = PhysicsMaterialCombine.Average, frictionCombine = PhysicsMaterialCombine.Average };
#else
            hayMat = new PhysicMaterial("RallyHay") { dynamicFriction = 0.8f, staticFriction = 0.9f, bounciness = 0.02f,
                bounceCombine = PhysicMaterialCombine.Minimum, frictionCombine = PhysicMaterialCombine.Average };
            tyreMat = new PhysicMaterial("RallyTyres") { dynamicFriction = 0.7f, staticFriction = 0.8f, bounciness = 0.3f,
                bounceCombine = PhysicMaterialCombine.Average, frictionCombine = PhysicMaterialCombine.Average };
#endif
            var T = L.Track;
            int k = 0;
            foreach (var c in T.corners)
            {
                if (!c.Tight || T.NearBridge(c.road, c.apex, 8)) continue;
                bool tyres = c.Hairpin ? (k % 3 != 2) : (W.roads[c.road].surface == Surface.Asphalt ? k % 2 == 0 : k % 4 == 0);
                BuildWall(c, tyres);
                k++;
            }
            BuildSigns();
            BuildFastTape();
            BuildMarshals();
        }

        // ---------------------------------------------------------------- barrier walls
        void BuildWall(RallyCorner c, bool tyres)
        {
            var T = L.Track;
            int r = c.road;
            float spacing = tyres ? 0.7f : 1.16f, half = tyres ? 0.33f : 0.25f;
            // offset polyline along the outside, a little before and after the corner
            var pts = new List<Vector3>();
            var nrm = new List<Vector2>();
            int from = c.first - 3, to = c.last + 3;
            for (int i = from; i <= to; i++)
            {
                if (!T.Valid(r, i)) continue;
                var s = T.At(r, i);
                Vector2 n = RallyTrack.Right(s) * c.outside;
                Vector2 p = s.pos + n * (s.halfWidth + 1.75f + half);
                pts.Add(new Vector3(p.x, s.height, p.y));
                nrm.Add(n);
            }
            if (pts.Count < 3) return;
            var site = new Site { batch = new LifeBatch() };
            var templates = new int[3];
            if (tyres)
            {
                Color32[] bands = { LifeGeo.C(236, 236, 230), LifeGeo.C(214, 40, 44), LifeGeo.C(60, 110, 200) };
                for (int t = 0; t < 3; t++) templates[t] = site.batch.AddTemplate(RallyShapes.TyreStack(bands[t]));
            }
            else
                for (int t = 0; t < 3; t++) templates[t] = site.batch.AddTemplate(RallyShapes.HayPiece(t));
            float acc = spacing * 0.5f;
            int count = 0, maxPieces = Mathf.RoundToInt(Mathf.Lerp(18f, 40f, Mathf.Clamp01(L.Density)));
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pts.Count - 1 && count < maxPieces; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 ab = b - a;
                ab.y = 0f;
                float len = ab.magnitude;
                if (len < 1e-3f) continue;
                Vector3 dir = ab / len;
                while (acc <= len && count < maxPieces)
                {
                    Vector3 p = a + dir * acc;
                    acc += spacing;
                    Vector2 p2 = new Vector2(p.x, p.z);
                    float roadY = p.y;
                    if (!PlacementRegistry.CanPlace(W, p2, 0.36f, 1.5f)) continue;
                    float gy = L.Ground(p.x, p.z);
                    if (Mathf.Abs(gy - roadY) > 1.1f || L.Bumpiness(p2, 0.55f) > 0.45f) continue;
                    if (!L.ClearOfColliders(p2, 0.4f)) continue;
                    PlacementRegistry.Reserve(p2, 0.4f);
                    var pos = new Vector3(p.x, gy - 0.03f, p.z);
                    var rot = Quaternion.LookRotation(new Vector3(-dir.z, 0f, dir.x), Vector3.up) * Quaternion.Euler(0f, R(-3f, 3f), 0f);
                    if (tyres) rot = Quaternion.Euler(0f, R(0f, 360f), 0f);
                    var pc = new Piece { part = site.batch.AddPart(templates[count % 3]), homePos = pos, homeRot = rot };
                    MakeCollider(pc, tyres, pos, rot);
                    site.pieces.Add(pc);
                    sum += pos;
                    count++;
                    // a second row of tyres on hairpins for a proper tyre wall
                    if (tyres && c.Hairpin)
                    {
                        Vector3 back = p + new Vector3(nrm[i].x, 0f, nrm[i].y) * 0.66f;
                        Vector2 b2 = new Vector2(back.x, back.z);
                        if (PlacementRegistry.CanPlace(W, b2, 0.36f, 1.5f) && L.ClearOfColliders(b2, 0.4f))
                        {
                            float by = L.Ground(back.x, back.z);
                            if (Mathf.Abs(by - roadY) < 1.1f)
                            {
                                PlacementRegistry.Reserve(b2, 0.4f);
                                var bp = new Vector3(back.x, by - 0.03f, back.z);
                                var br = Quaternion.Euler(0f, R(0f, 360f), 0f);
                                var pb = new Piece { part = site.batch.AddPart(templates[(count + 1) % 3]), homePos = bp, homeRot = br };
                                MakeCollider(pb, true, bp, br);
                                site.pieces.Add(pb);
                                sum += bp;
                            }
                        }
                    }
                }
                acc -= len;
            }
            if (site.pieces.Count == 0) return;
            foreach (var pc in site.pieces) pc.site = site;
            site.batch.Finish(tyres ? "TyreWall" : "HayWall", MaterialLibrary.VertexColorWind, Root, 0, true);
            foreach (var pc in site.pieces) site.batch.Set(pc.part, Matrix4x4.TRS(pc.homePos, pc.homeRot, Vector3.one));
            site.batch.Upload();
            site.centre = sum / site.pieces.Count;
            float rad = 0f;
            foreach (var pc in site.pieces) rad = Mathf.Max(rad, (pc.homePos - site.centre).magnitude);
            site.radius = rad + 2f;
            sites.Add(site);
            PieceCount += site.pieces.Count;
        }

        void MakeCollider(Piece pc, bool tyre, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(tyre ? "TyreStack" : "HayBales");
            go.transform.SetParent(colliderRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var bc = go.AddComponent<BoxCollider>();
            if (tyre) { bc.size = new Vector3(0.64f, 0.62f, 0.64f); bc.center = new Vector3(0f, 0.33f, 0f); bc.sharedMaterial = tyreMat; }
            else { bc.size = new Vector3(1.1f, 0.9f, 0.5f); bc.center = new Vector3(0f, 0.47f, 0f); bc.sharedMaterial = hayMat; }
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = tyre ? 28f : 42f;
            rb.SetDamping(tyre ? 0.35f : 1.2f, tyre ? 0.8f : 2.5f);
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            pc.rb = rb;
            pc.tr = go.transform;
        }

        // ---------------------------------------------------------------- signs
        void BuildSigns()
        {
            var T = L.Track;
            var S = L.Static;
            var chevR = RallyShapes.Chevron(1, false);
            var chevL = RallyShapes.Chevron(-1, false);
            var chevRY = RallyShapes.Chevron(1, true);
            var chevLY = RallyShapes.Chevron(-1, true);
            var b100 = RallyShapes.DistanceBoard("100");
            var b50 = RallyShapes.DistanceBoard("50");
            foreach (var c in T.corners)
            {
                if (c.peakTurn < 30f || !T.OnStage(c.road, c.apex)) continue;
                int r = c.road;
                bool sharp = c.Hairpin || c.peakTurn >= 55f;
                // distance boards before the corner (stage direction = increasing distance)
                if (sharp)
                {
                    PlaceBoard(c, -100f, b100);
                    PlaceBoard(c, -50f, b50);
                }
                // chevrons on the outside, behind the walls, facing the approach
                int n = sharp ? 3 : 2;
                int dir = c.totalTurn > 0f ? -1 : 1;
                bool yellow = W.roads[r].surface != Surface.Asphalt;
                var tpl = dir > 0 ? (yellow ? chevRY : chevR) : (yellow ? chevLY : chevL);
                Vector2 entry = T.At(r, T.Along(r, c.first, -12f)).pos;
                for (int k = 0; k < n; k++)
                {
                    int idx = Mathf.RoundToInt(Mathf.Lerp(c.first, c.last, n == 1 ? 0.5f : (k + 0.5f) / n));
                    if (!T.Valid(r, idx)) continue;
                    var s = T.At(r, idx);
                    Vector2 p = s.pos + RallyTrack.Right(s) * (c.outside * (s.halfWidth + 3.9f));
                    if (!PlacementRegistry.CanPlace(W, p, 0.9f, 2.6f)) continue;
                    float gy = L.Ground(p.x, p.y);
                    if (Mathf.Abs(gy - s.height) > 1.6f) continue;
                    PlacementRegistry.Reserve(p, 0.9f);
                    Vector2 away = (p - entry).normalized;
                    S.Add(tpl, new Vector3(p.x, gy, p.y), Quaternion.LookRotation(new Vector3(away.x, 0f, away.y), Vector3.up));
                    Boards++;
                }
            }
        }

        void PlaceBoard(RallyCorner c, float metres, LifeGeo tpl)
        {
            var T = L.Track;
            int r = c.road;
            int idx = T.Along(r, c.first, metres);
            if (!T.OnStage(r, idx) || T.NearBridge(r, idx, 3)) return;
            if (!T.Closed(r) && (c.first + metres / 2f < 0)) return;
            foreach (var o in T.corners)
                if (o != c && o.road == r && InSpan(r, idx, o.first - 4, o.last + 4)) return;
            var s = T.At(r, idx);
            for (int side = 1; side >= -1; side -= 2)
            {
                Vector2 p = s.pos + RallyTrack.Right(s) * (side * (s.halfWidth + 2.3f));
                if (!PlacementRegistry.CanPlace(W, p, 0.5f, 1.6f)) continue;
                float gy = L.Ground(p.x, p.y);
                if (Mathf.Abs(gy - s.height) > 1.5f) continue;
                PlacementRegistry.Reserve(p, 0.6f);
                L.Static.Add(tpl, new Vector3(p.x, gy, p.y), Quaternion.LookRotation(new Vector3(s.tangent.x, 0f, s.tangent.y), Vector3.up));
                Boards++;
                return;
            }
        }

        bool InSpan(int r, int i, int a, int b)
        {
            var T = L.Track;
            int n = T.S(r).Count;
            if (!T.Closed(r)) return i >= a && i <= b;
            int span = ((b - a) % n + n) % n, off = ((i - a) % n + n) % n;
            return off <= span;
        }

        // ---------------------------------------------------------------- tape
        void BuildFastTape()
        {
            var T = L.Track;
            int want = Mathf.RoundToInt(6 * L.Density), done = 0;
            int side = 1;
            foreach (var st in T.fast)
            {
                if (done >= want) break;
                if (!T.OnStage(st.road, (st.first + st.last) / 2)) continue;
                int len = st.last - st.first;
                int runLen = Mathf.Min(len - 6, 30);       // up to ~60 m of tape
                if (runLen < 10) continue;
                int start = st.first + (len - runLen) / 2;
                var posts = new List<Vector3>();
                for (int i = start; i <= start + runLen; i += 2)
                {
                    var s = T.At(st.road, i);
                    Vector2 p = s.pos + RallyTrack.Right(s) * (side * (s.halfWidth + 2.1f));
                    bool ok = PlacementRegistry.CanPlace(W, p, 0.12f, 1.7f) && Mathf.Abs(L.Ground(p.x, p.y) - s.height) < 1.2f
                              && L.ClearOfColliders(p, 0.15f);
                    if (ok) { posts.Add(L.Ground(p)); PlacementRegistry.Reserve(p, 0.15f); }
                    if ((!ok || i + 2 > start + runLen) && posts.Count > 0)
                    {
                        if (posts.Count >= 3) { TapeLine(L.Static, posts); TapePosts += posts.Count; }
                        posts.Clear();
                    }
                }
                side = -side;
                done++;
            }
        }

        static LifeGeo postTpl;

        /// <summary>Red / white barrier tape sagging between posts (wind weight on the ribbon).</summary>
        public static void TapeLine(StaticChunks S, List<Vector3> posts)
        {
            if (postTpl == null) postTpl = RallyShapes.TapePost();
            Color32 red = LifeGeo.C(214, 36, 40), white = LifeGeo.C(246, 246, 242);
            for (int i = 0; i < posts.Count; i++) S.Add(postTpl, posts[i], Quaternion.identity);
            for (int i = 0; i < posts.Count - 1; i++)
            {
                Vector3 a = posts[i] + Vector3.up * 1.0f, b = posts[i + 1] + Vector3.up * 1.0f;
                float len = (b - a).magnitude;
                if (len > 9f) continue;
                var g = S.At(a);
                float sag = len * 0.035f;
                int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.6f));
                for (int k = 0; k < n; k++)
                {
                    float t0 = k / (float)n, t1 = (k + 1) / (float)n;
                    Vector3 p0 = Vector3.Lerp(a, b, t0) + Vector3.down * (sag * 4f * t0 * (1f - t0));
                    Vector3 p1 = Vector3.Lerp(a, b, t1) + Vector3.down * (sag * 4f * t1 * (1f - t1));
                    Color32 c0 = k % 2 == 0 ? red : white, c1 = c0;
                    c0.a = (byte)(Mathf.Sin(Mathf.PI * t0) * 150f);
                    c1.a = (byte)(Mathf.Sin(Mathf.PI * t1) * 150f);
                    Vector3 d = Vector3.down * 0.075f;
                    g.Tri2(p0, p1, p1 + d, c0, c1, c1);
                    g.Tri2(p0, p1 + d, p0 + d, c0, c1, c0);
                }
            }
        }

        // ---------------------------------------------------------------- marshals
        void BuildMarshals()
        {
            var T = L.Track;
            var spots = new List<Vector4>();   // x, y, z, yaw
            int want = Mathf.RoundToInt(Mathf.Lerp(6f, 12f, Mathf.Clamp01(L.Density)));
            foreach (var c in T.corners)
            {
                if (spots.Count >= want) break;
                if (!c.Hairpin || !T.OnStage(c.road, c.apex)) continue;
                TryMarshal(spots, c.road, T.Along(c.road, c.first, -28f), c.outside);
            }
            for (int r = 0; r < RallyTrack.StageRoads && r < W.roadSamples.Count && spots.Count < want; r++)
            {
                var s = T.S(r);
                for (int i = 1; i < s.Count && spots.Count < want; i++)
                {
                    if (!s[i].bridge || s[i - 1].bridge) continue;      // first sample of a bridge
                    if (!T.OnStage(r, i)) continue;
                    TryMarshal(spots, r, T.Along(r, i, -30f), Chance(0.5f) ? 1 : -1);
                }
            }
            if (spots.Count == 0) return;
            marshalBatch = new LifeBatch();
            Color32 vest = LifeGeo.C(248, 120, 30), flagY = LifeGeo.C(250, 214, 40);
            for (int i = 0; i < spots.Count; i++)
            {
                var look = RallyShapes.RandomLook(Rng, RallyShapes.Standing);
                look.vestOn = true;
                look.vest = vest;
                look.hatStyle = i % 2 == 0 ? RallyShapes.HatCap : RallyShapes.HatNone;
                look.hat = LifeGeo.C(248, 120, 30);
                look.top = i % 3 == 0 ? LifeGeo.C(238, 238, 232) : LifeGeo.C(56, 62, 92);
                int bt = marshalBatch.AddTemplate(RallyShapes.Person(look));
                int at = marshalBatch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
                int ft = marshalBatch.AddTemplate(RallyShapes.FlagArm(look.top, look.skin, flagY));
                var m = new Marshal { body = marshalBatch.AddPart(bt), armL = marshalBatch.AddPart(at), armR = marshalBatch.AddPart(ft) };
                Vector4 sp = spots[i];
                m.pos = new Vector3(sp.x, sp.y - 0.02f, sp.z);
                m.baseYaw = m.yaw = sp.w;
                m.phase = R(0f, 10f);
                marshals.Add(m);
            }
            marshalBatch.Finish("Marshals", MaterialLibrary.VertexColorWind, Root, 0, true);
            for (int i = 0; i < marshals.Count; i++) PoseMarshal(marshals[i], 0f);
            marshalBatch.Upload();
        }

        void TryMarshal(List<Vector4> spots, int r, int idx, int pref)
        {
            var T = L.Track;
            if (!T.Valid(r, idx) || T.NearBridge(r, idx, 2)) return;
            var s = T.At(r, idx);
            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? pref : -pref;
                for (float off = 4.6f; off <= 7f; off += 1.2f)
                {
                    Vector2 p = s.pos + RallyTrack.Right(s) * (side * (s.halfWidth + off));
                    if (L.EdgeDist(p) < 4.2f) continue;
                    if (!TryPlace(p, 0.6f, 4f, 0.8f)) continue;
                    // face the approaching traffic, slightly towards the road
                    Vector2 look = T.At(r, T.Along(r, idx, -25f)).pos - p;
                    spots.Add(new Vector4(p.x, L.Ground(p.x, p.y), p.y, RallyLife.Yaw(look)));
                    return;
                }
            }
        }

        void PoseMarshal(Marshal m, float dt)
        {
            float t = L.Clock + m.phase;
            Quaternion rot = Quaternion.Euler(0f, m.yaw, Mathf.Sin(t * 0.9f) * 1.5f);
            Matrix4x4 b = Matrix4x4.TRS(m.pos, rot, Vector3.one);
            marshalBatch.Set(m.body, b);
            float idle = Mathf.Sin(t * 1.2f) * 4f;
            // flag arm: down by the side when calm, raised and waving when a car comes
            float raise = Mathf.Lerp(12f + idle, 140f + Mathf.Sin(t * 13f) * 32f, m.wave);
            Quaternion rArm = Quaternion.Euler(Mathf.Lerp(idle, -10f, m.wave), 0f, raise);
            Quaternion lArm = Quaternion.Euler(-idle, 0f, -(8f + m.wave * 20f));
            marshalBatch.Set(m.armR, b * Matrix4x4.TRS(RallyShapes.ShoulderR, rArm, Vector3.one));
            marshalBatch.Set(m.armL, b * Matrix4x4.TRS(RallyShapes.ShoulderL, lArm, Vector3.one));
        }

        // ---------------------------------------------------------------- update
        public override void Tick(float dt)
        {
            Vector3 car = L.CarPos;
            bool hasCar = L.HasCar;
            Vector3 ahead = car + L.CarVel * 0.25f;
            for (int si = 0; si < sites.Count; si++)
            {
                var site = sites[si];
                // wake pieces the car is about to touch
                if (hasCar && L.CarSpeed > 1.5f)
                {
                    float dc = (car - site.centre).magnitude;
                    if (dc < site.radius + 16f)
                    {
                        for (int i = 0; i < site.pieces.Count; i++)
                        {
                            var pc = site.pieces[i];
                            if (pc.active) continue;
                            Vector3 pp = pc.tr.position;
                            float d1 = (pp - car).sqrMagnitude, d2 = (pp - ahead).sqrMagnitude;
                            if (d1 < 36f || d2 < 36f) Wake(pc);
                        }
                    }
                }
                // restore displaced pieces once nobody is looking
                if (site.dirty && (L.Eye - site.centre).sqrMagnitude > 130f * 130f && (!hasCar || (car - site.centre).sqrMagnitude > 130f * 130f))
                {
                    site.dirty = false;
                    for (int i = 0; i < site.pieces.Count; i++)
                    {
                        var pc = site.pieces[i];
                        if (pc.active || !pc.displaced) continue;
                        pc.tr.SetPositionAndRotation(pc.homePos, pc.homeRot);
                        pc.displaced = false;
                        site.batch.Set(pc.part, Matrix4x4.TRS(pc.homePos, pc.homeRot, Vector3.one));
                    }
                    site.batch.Upload();
                }
            }
            // follow the awake rigidbodies, put them back to sleep when they have settled
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var pc = active[i];
                var site = SiteOf(pc);
                site.batch.Set(pc.part, Matrix4x4.TRS(pc.tr.position, pc.tr.rotation, Vector3.one));
                site.dirty = true;
                bool still = pc.rb.IsSleeping() || pc.rb.GetVelocity().sqrMagnitude < 0.01f;
                if (still) pc.restAt += dt; else pc.restAt = 0f;
                float age = L.Clock - pc.wokeAt;
                bool carNear = hasCar && (car - pc.tr.position).sqrMagnitude < 64f;
                if ((pc.restAt > 1.2f && age > 2f && !carNear) || age > 25f || pc.tr.position.y < pc.homePos.y - 6f)
                {
                    if (pc.tr.position.y < pc.homePos.y - 6f) pc.tr.SetPositionAndRotation(pc.homePos, pc.homeRot);
                    pc.rb.isKinematic = true;
                    pc.rb.interpolation = RigidbodyInterpolation.None;
                    pc.active = false;
                    pc.displaced = (pc.tr.position - pc.homePos).sqrMagnitude > 0.0025f || Quaternion.Angle(pc.tr.rotation, pc.homeRot) > 2f;
                    site.batch.Set(pc.part, Matrix4x4.TRS(pc.tr.position, pc.tr.rotation, Vector3.one));
                    active.RemoveAt(i);
                }
            }
            for (int si = 0; si < sites.Count; si++) sites[si].batch.Upload();
            UpdateMarshals(dt);
        }

        static Site SiteOf(Piece pc) { return pc.site; }

        void Wake(Piece pc)
        {
            if (active.Count >= MaxActive)
            {
                // freeze the oldest one where it is
                var old = active[0];
                old.rb.isKinematic = true;
                old.rb.interpolation = RigidbodyInterpolation.None;
                old.active = false;
                old.displaced = true;
                active.RemoveAt(0);
            }
            pc.rb.isKinematic = false;
            pc.rb.interpolation = RigidbodyInterpolation.Interpolate;
            pc.rb.WakeUp();
            pc.active = true;
            pc.wokeAt = L.Clock;
            pc.restAt = 0f;
            active.Add(pc);
        }

        void UpdateMarshals(float dt)
        {
            if (marshalBatch == null) return;
            bool any = false;
            for (int i = 0; i < marshals.Count; i++)
            {
                var m = marshals[i];
                if (!L.Due(m.pos, i)) continue;
                float target = 0f, yawT = m.baseYaw;
                if (L.HasCar)
                {
                    Vector3 d = L.CarPos - m.pos;
                    d.y = 0f;
                    float dist = d.magnitude;
                    bool approaching = dist < m.lastDist - 0.01f;
                    if (dist < 110f && (approaching || dist < 25f) && L.CarSpeed > 3f) target = 1f;
                    if (dist < 70f) yawT = RallyLife.Yaw(d);
                    m.lastDist = dist;
                }
                m.wave = Mathf.MoveTowards(m.wave, target, dt * 3f);
                m.yaw = Mathf.MoveTowardsAngle(m.yaw, yawT, 120f * dt);
                PoseMarshal(m, dt);
                any = true;
            }
            if (any) marshalBatch.Upload();
        }
    }
}

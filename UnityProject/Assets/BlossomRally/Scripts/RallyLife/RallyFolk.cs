// RallyFolk.cs - valley people who are not here for the rally: farmers in conical hats picking tea in
// the terraces and hoeing the meadows, monks in saffron robes walking up the path to the pagoda and
// round it, old men playing Chinese chess (xiangqi) on a stone table under a blossom tree in the
// village, and a tai chi group in an open village square in the morning (Morning / Day only).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyFolk : RallyModule
    {
        sealed class Figure
        {
            public int body, armL, armR, tool = -1;
            public Vector3 pos;
            public float yaw, phase, kind;
        }

        // farmers
        LifeBatch farmBatch;
        readonly List<Figure> farmers = new List<Figure>();
        // monks
        LifeBatch monkBatch;
        readonly List<Figure> monks = new List<Figure>();
        Vector3[] monkPath;
        float[] monkLen;
        float monkS, monkDir = 1f, monkPause;
        // chess
        LifeBatch chessBatch;
        readonly List<Figure> chess = new List<Figure>();
        Vector3 chessPos;
        // tai chi
        LifeBatch taiBatch;
        readonly List<Figure> tai = new List<Figure>();
        Vector3 taiPos;
        bool taiShown = true;

        public int FarmerCount { get { return farmers.Count; } }
        public override string Summary { get { return farmers.Count + " farmers, " + monks.Count + " monks, " + chess.Count + " at chess, " + tai.Count + " tai chi"; } }

        public override void Build()
        {
            BuildFarmers();
            BuildMonks();
            BuildChess();
            BuildTaiChi();
        }

        static RallyShapes.Look Folk(System.Random rng, int body, int hat)
        {
            var k = RallyShapes.RandomLook(rng, body);
            Color32[] cloth = { LifeGeo.C(70, 96, 130), LifeGeo.C(110, 120, 96), LifeGeo.C(150, 120, 90), LifeGeo.C(60, 70, 80), LifeGeo.C(236, 232, 220) };
            k.top = cloth[rng.Next(cloth.Length)];
            k.bottom = cloth[rng.Next(cloth.Length)];
            k.hatStyle = hat;
            if (hat == RallyShapes.HatConical) k.hat = LifeGeo.C(214, 184, 118);
            return k;
        }

        Figure Add(LifeBatch b, RallyShapes.Look look, List<Figure> list, int toolT = -1)
        {
            var f = new Figure();
            f.body = b.AddPart(b.AddTemplate(RallyShapes.Person(look)));
            int at = b.AddTemplate(RallyShapes.Arm(look.top, look.skin));
            f.armL = b.AddPart(at);
            f.armR = b.AddPart(at);
            if (toolT >= 0) f.tool = b.AddPart(toolT);
            f.phase = R(0f, 30f);
            list.Add(f);
            return f;
        }

        // ---------------------------------------------------------------- farmers
        void BuildFarmers()
        {
            int want = Mathf.Clamp(Mathf.RoundToInt(8 * L.Density), 3, 10);
            var spots = new List<Vector4>();   // x y z kind (0 tea picker, 1 hoe)
            Vector2 ta = MapData.P(1190, 340), tb = MapData.P(1466, 1310);
            for (int tries = 0; tries < 500 && spots.Count < want / 2 + 1; tries++)
            {
                Vector2 p = new Vector2(R(ta.x, tb.x), R(tb.y, ta.y));
                if (!MapData.InTeaTerraces(p) || L.EdgeDist(p) < 7f || !Spaced(spots, p, 22f)) continue;
                if (!TryPlace(p, 0.8f, 6f, 1.4f, false)) continue;
                spots.Add(new Vector4(p.x, L.Ground(p.x, p.y), p.y, 0f));
            }
            for (int tries = 0; tries < 600 && spots.Count < want; tries++)
            {
                Vector2 p = new Vector2(R(-420f, 420f), R(-420f, 420f));
                if (MapData.InTeaTerraces(p) || MapData.InBambooForest(p) || MapData.InVillage(p)) continue;
                if (Vector2.Distance(p, MapData.MountainCenter) < 150f || L.EdgeDist(p) < 12f || !Spaced(spots, p, 45f)) continue;
                if (L.Bumpiness(p, 3f) > 0.5f) continue;
                if (!TryPlace(p, 2.2f, 10f, 0.5f)) continue;
                spots.Add(new Vector4(p.x, L.Ground(p.x, p.y), p.y, 1f));
            }
            if (spots.Count == 0) return;
            farmBatch = new LifeBatch();
            int hoeT = farmBatch.AddTemplate(RallyShapes.Hoe());
            var basket = RallyShapes.FieldBasket();
            foreach (var s in spots)
            {
                bool hoe = s.w > 0.5f;
                var look = Folk(Rng, RallyShapes.Standing, RallyShapes.HatConical);
                look.basket = !hoe;
                var f = Add(farmBatch, look, farmers, hoe ? hoeT : -1);
                f.pos = new Vector3(s.x, s.y - 0.03f, s.z);
                f.yaw = R(0f, 360f);
                f.kind = s.w;
                if (hoe)
                {
                    Vector3 bp = f.pos + Quaternion.Euler(0f, f.yaw + 120f, 0f) * Vector3.forward * 1.3f;
                    L.Static.Add(basket, new Vector3(bp.x, L.Ground(bp.x, bp.z) - 0.02f, bp.z), Quaternion.Euler(0f, R(0f, 360f), 0f));
                }
            }
            farmBatch.Finish("Farmers", MaterialLibrary.VertexColorWind, Root, 0, true);
            PoseFarmers(true);
        }

        static bool Spaced(List<Vector4> spots, Vector2 p, float d)
        {
            foreach (var s in spots) if ((new Vector2(s.x, s.z) - p).sqrMagnitude < d * d) return false;
            return true;
        }

        void PoseFarmers(bool force)
        {
            if (farmBatch == null) return;
            bool any = false;
            for (int i = 0; i < farmers.Count; i++)
            {
                var f = farmers[i];
                if (!force && !L.Due(f.pos, i, 60f, 140f, 220f)) continue;
                float t = L.Clock + f.phase;
                Matrix4x4 m;
                if (f.kind < 0.5f)
                {
                    // tea picking: bent over, hands plucking at the bushes, slowly turning
                    float yaw = f.yaw + Mathf.Sin(t * 0.08f) * 50f;
                    float bend = 38f + Mathf.Sin(t * 0.5f) * 8f;
                    m = Matrix4x4.TRS(f.pos, Quaternion.Euler(bend, yaw, 0f), Vector3.one);
                    float pr = Mathf.Sin(t * 3.1f), pl = Mathf.Sin(t * 3.1f + 1.7f);
                    farmBatch.Set(f.body, m);
                    farmBatch.Set(f.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(-45f + pr * 18f, 0f, 10f), Vector3.one));
                    farmBatch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(-45f + pl * 18f, 0f, -10f), Vector3.one));
                }
                else
                {
                    // hoeing: lift the hoe overhead, strike, pull back
                    float cyc = Mathf.Repeat(t / 2.4f, 1f);
                    float swing = cyc < 0.45f ? Mathf.SmoothStep(0f, 1f, cyc / 0.45f) : cyc < 0.6f ? 1f - (cyc - 0.45f) / 0.15f : 0f;
                    float bend = 12f + (1f - swing) * 18f;
                    m = Matrix4x4.TRS(f.pos, Quaternion.Euler(bend, f.yaw, 0f), Vector3.one);
                    farmBatch.Set(f.body, m);
                    float armPitch = Mathf.Lerp(-40f, -150f, swing);
                    Matrix4x4 ar = m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(armPitch, 0f, -12f), Vector3.one);
                    farmBatch.Set(f.armR, ar);
                    farmBatch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(armPitch + 8f, 0f, 12f), Vector3.one));
                    Vector3 hand = ar.MultiplyPoint3x4(new Vector3(-0.1f, -0.46f, 0f));
                    Quaternion hr = Quaternion.Euler(0f, f.yaw, 0f) * Quaternion.Euler(Mathf.Lerp(28f, -70f, swing) + bend * 0.5f, 0f, 0f);
                    farmBatch.Set(f.tool, Matrix4x4.TRS(hand, hr, Vector3.one));
                }
                any = true;
            }
            if (any) farmBatch.Upload();
        }

        // ---------------------------------------------------------------- monks
        void BuildMonks()
        {
            if (W.roadSamples.Count < 4) return;
            Vector2 pg = MapData.PagodaTop;
            var pts = new List<Vector3>();
            var rd = W.roadSamples[3];
            // roadside footpath along the last ~70 m of the hairpin road, the side that is clear
            for (int side = -1; side <= 1 && pts.Count < 10; side += 2)
            {
                pts.Clear();
                for (int i = Mathf.Max(0, rd.Count - 36); i < rd.Count; i++)
                {
                    var s = rd[i];
                    Vector2 p = s.pos + RallyTrack.Right(s) * (side * (s.halfWidth + 4.6f));
                    if (L.EdgeDist(p) < 4.2f || W.RiverDistance(p) < 15f || !L.ClearOfColliders(p, 0.4f)) { pts.Clear(); continue; }
                    if (Vector2.Distance(p, pg) < 12f) break;
                    pts.Add(L.Ground(p));
                }
            }
            if (pts.Count < 6) pts.Clear();
            // then once round the pagoda (clockwise seen from above)
            Vector2 start = pts.Count > 0 ? new Vector2(pts[pts.Count - 1].x, pts[pts.Count - 1].z) : pg + Vector2.up * 11f;
            float a0 = Mathf.Atan2(start.y - pg.y, start.x - pg.x);
            for (int k = 0; k <= 24; k++)
            {
                float a = a0 - k * Mathf.PI * 2f / 24f;
                Vector2 p = pg + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11f;
                if (L.EdgeDist(p) < 3.5f || !L.ClearOfColliders(p, 0.3f)) { if (k > 0) break; else continue; }
                pts.Add(L.Ground(p));
            }
            if (pts.Count < 6) return;
            monkPath = pts.ToArray();
            monkLen = new float[monkPath.Length];
            for (int i = 1; i < monkPath.Length; i++) monkLen[i] = monkLen[i - 1] + Vector3.Distance(monkPath[i], monkPath[i - 1]);
            foreach (var p in monkPath) PlacementRegistry.Reserve(new Vector2(p.x, p.z), 0.6f);
            monkBatch = new LifeBatch();
            Color32[] robes = { LifeGeo.C(226, 140, 40), LifeGeo.C(150, 40, 36), LifeGeo.C(232, 150, 50) };
            int n = L.Density >= 1f ? 4 : 3;
            for (int i = 0; i < n; i++)
            {
                var look = new RallyShapes.Look
                {
                    top = robes[i % robes.Length], bottom = robes[i % robes.Length], skin = RallyShapes.Skins[RI(RallyShapes.Skins.Length)],
                    hair = RallyShapes.Hairs[0], body = RallyShapes.Robe, hatStyle = RallyShapes.HatBald
                };
                Add(monkBatch, look, monks);
            }
            monkBatch.Finish("Monks", MaterialLibrary.VertexColorWind, Root, 0, true);
            monkS = R(0f, monkLen[monkLen.Length - 1]);
            PoseMonks(0f, true);
        }

        Vector3 MonkAt(float s, out Vector3 dir)
        {
            float total = monkLen[monkLen.Length - 1];
            s = Mathf.Clamp(s, 0f, total);
            int i = 1;
            while (i < monkLen.Length - 1 && monkLen[i] < s) i++;
            float seg = monkLen[i] - monkLen[i - 1];
            float u = seg > 1e-4f ? (s - monkLen[i - 1]) / seg : 0f;
            dir = monkPath[i] - monkPath[i - 1];
            return Vector3.Lerp(monkPath[i - 1], monkPath[i], u);
        }

        void PoseMonks(float dt, bool force)
        {
            if (monkBatch == null) return;
            float total = monkLen[monkLen.Length - 1];
            bool walking = monkPause <= 0f;
            if (!force)
            {
                if (walking)
                {
                    monkS += monkDir * 0.65f * dt;
                    float lastOff = (monks.Count - 1) * 1.7f;
                    if (monkS > total) { monkS = total; monkDir = -1f; monkPause = 8f; }
                    else if (monkS < lastOff) { monkS = lastOff; monkDir = 1f; monkPause = 8f; }
                }
                else monkPause -= dt;
            }
            Vector3 c = monkPath[monkPath.Length / 2];
            if (!force && !L.Due(c, 9, 70f, 150f, 230f)) return;
            for (int i = 0; i < monks.Count; i++)
            {
                var f = monks[i];
                Vector3 dir;
                Vector3 p = MonkAt(monkS - monkDir * i * 1.7f, out dir);
                if (monkDir < 0f) dir = -dir;
                float t = L.Clock + f.phase;
                if (walking) p.y += Mathf.Abs(Mathf.Sin(t * 4.2f)) * 0.03f;
                if (dir.sqrMagnitude > 1e-4f) f.yaw = Mathf.MoveTowardsAngle(f.yaw, RallyLife.Yaw(dir), force ? 999f : 200f * Mathf.Max(dt, 0.02f));
                Matrix4x4 m = Matrix4x4.TRS(p, Quaternion.Euler(walking ? 3f : 6f, f.yaw, Mathf.Sin(t * 2.1f) * (walking ? 2f : 0.5f)), Vector3.one);
                monkBatch.Set(f.body, m);
                // palms together in front of the chest
                monkBatch.Set(f.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(-62f, -28f, 18f), Vector3.one));
                monkBatch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(-62f, 28f, -18f), Vector3.one));
            }
            monkBatch.Upload();
        }

        // ---------------------------------------------------------------- chess under a tree
        void BuildChess()
        {
            var root = GameObject.Find("World_Static");
            if (root == null) return;
            var trees = new List<Vector3>();
            Transform rt = root.transform;
            for (int i = 0; i < rt.childCount; i++)
            {
                Transform ch = rt.GetChild(i);
                if (!ch.name.StartsWith("ENV_Cherry")) continue;
                Vector2 p = new Vector2(ch.position.x, ch.position.z);
                if (MapData.InVillage(p) || Vector2.Distance(p, MapData.Gate) < 90f) trees.Add(ch.position);
            }
            trees.Sort((a, b) => Vector2.Distance(new Vector2(a.x, a.z), MapData.Gate).CompareTo(Vector2.Distance(new Vector2(b.x, b.z), MapData.Gate)));
            Vector2 spot = Vector2.zero;
            bool found = false;
            foreach (var tr in trees)
            {
                for (int k = 0; k < 8 && !found; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Vector2 p = new Vector2(tr.x, tr.z) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f;
                    if (L.EdgeDist(p) < 6f) continue;
                    if (!TryPlace(p, 1.9f, 5.5f, 0.4f)) continue;
                    spot = p;
                    found = true;
                }
                if (found) break;
            }
            if (!found) return;
            chessPos = L.Ground(spot);
            float yaw = R(0f, 360f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            L.Static.Add(RallyShapes.ChessTable(), chessPos - Vector3.up * 0.03f, q);
            var stool = RallyShapes.Stool();
            chessBatch = new LifeBatch();
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 sp = chessPos + q * new Vector3(0f, 0f, 0.82f * s);
                sp.y = L.Ground(sp.x, sp.z);
                L.Static.Add(stool, sp - Vector3.up * 0.02f, q);
                var look = Folk(Rng, RallyShapes.Sitting, RallyShapes.HatElder);
                var f = Add(chessBatch, look, chess);
                // sitting figure: hips over the stool, feet under the table
                f.yaw = yaw + (s > 0 ? 180f : 0f);
                f.pos = sp;
                f.kind = 0f;
            }
            for (int k = 0; k < 2; k++)
            {
                var look = Folk(Rng, RallyShapes.Standing, k == 0 ? RallyShapes.HatElder : RallyShapes.HatNone);
                var f = Add(chessBatch, look, chess);
                Vector3 op = chessPos + q * new Vector3(k == 0 ? 1.0f : -1.05f, 0f, k == 0 ? 0.3f : -0.4f);
                op.y = L.Ground(op.x, op.z) - 0.02f;
                f.pos = op;
                f.yaw = RallyLife.Yaw(chessPos - op);
                f.kind = 1f;
            }
            chessBatch.Finish("ChessPlayers", MaterialLibrary.VertexColorWind, Root, 0, true);
            PoseChess(true);
        }

        void PoseChess(bool force)
        {
            if (chessBatch == null || (!force && !L.Due(chessPos, 5, 45f, 100f, 150f))) return;
            float clock = L.Clock;
            int mover = Mathf.FloorToInt(clock / 7f) % 2;
            float u = Mathf.Repeat(clock / 7f, 1f);
            float reach = u > 0.7f && u < 0.9f ? Mathf.Sin((u - 0.7f) / 0.2f * Mathf.PI) : 0f;
            for (int i = 0; i < chess.Count; i++)
            {
                var f = chess[i];
                float t = clock + f.phase;
                if (f.kind < 0.5f)
                {
                    bool moving = i == mover;
                    Matrix4x4 m = Matrix4x4.TRS(f.pos, Quaternion.Euler(10f + (moving ? reach * 12f : Mathf.Sin(t * 0.4f) * 3f), f.yaw, 0f), Vector3.one);
                    chessBatch.Set(f.body, m);
                    float sh = 1.33f - RallyShapes.SitDrop;
                    float r = moving ? reach : 0f;
                    chessBatch.Set(f.armR, m * Matrix4x4.TRS(new Vector3(0.2f, sh, 0f), Quaternion.Euler(-35f - r * 45f, 0f, 6f), Vector3.one));
                    chessBatch.Set(f.armL, m * Matrix4x4.TRS(new Vector3(-0.2f, sh, 0f), Quaternion.Euler(-30f + Mathf.Sin(t * 0.3f) * 6f, 0f, -6f), Vector3.one));
                }
                else
                {
                    // onlookers: hands behind the back, leaning in now and then
                    float lean = 8f + Mathf.Max(0f, Mathf.Sin(t * 0.35f)) * 10f;
                    Matrix4x4 m = Matrix4x4.TRS(f.pos, Quaternion.Euler(lean, f.yaw + Mathf.Sin(t * 0.2f) * 10f, 0f), Vector3.one);
                    chessBatch.Set(f.body, m);
                    chessBatch.Set(f.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(30f, 0f, -12f), Vector3.one));
                    chessBatch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(30f, 0f, 12f), Vector3.one));
                }
            }
            chessBatch.Upload();
        }

        // ---------------------------------------------------------------- tai chi
        void BuildTaiChi()
        {
            Vector2 a = MapData.P(MapData.VillagePx.xMin, MapData.VillagePx.yMin), b = MapData.P(MapData.VillagePx.xMax, MapData.VillagePx.yMax);
            Vector2 spot = Vector2.zero;
            bool found = false;
            for (int tries = 0; tries < 400 && !found; tries++)
            {
                Vector2 p = new Vector2(R(a.x, b.x), R(b.y, a.y));
                if (!MapData.InVillage(p) || L.EdgeDist(p) < 6f || (p - Garage.Spot).sqrMagnitude < 30f * 30f) continue;
                if (L.Bumpiness(p, 4f) > 0.45f) continue;
                if (!L.AreaClear(p, 5.5f)) continue;
                if (!TryPlace(p, 5.5f, 5f, 0.45f)) continue;
                spot = p;
                found = true;
            }
            if (!found) return;
            taiPos = L.Ground(spot);
            RoadSample rs;
            W.NearestRoad(spot, out rs, 40f);
            float yaw = RallyLife.Yaw(new Vector3(rs.pos.x - spot.x, 0f, rs.pos.y - spot.y)) + 180f;   // backs to the street
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            taiBatch = new LifeBatch();
            Color32 white = LifeGeo.C(242, 240, 232), red = LifeGeo.C(196, 40, 44);
            int n = Mathf.Clamp(Mathf.RoundToInt(8 * L.Density), 4, 9);
            for (int i = 0; i < n; i++)
            {
                var look = Folk(Rng, RallyShapes.Standing, i % 3 == 0 ? RallyShapes.HatElder : RallyShapes.HatNone);
                look.top = i == 0 ? red : white;
                look.bottom = i == 0 ? red : white;
                var f = Add(taiBatch, look, tai);
                Vector3 local = i == 0 ? new Vector3(0f, 0f, 2.6f) : new Vector3(((i - 1) % 4 - 1.5f) * 1.6f, 0f, -((i - 1) / 4) * 1.7f);
                Vector3 p = taiPos + q * local;
                p.y = L.Ground(p.x, p.z) - 0.02f;
                f.pos = p;
                f.yaw = yaw + (i == 0 ? 180f : 0f);
                f.phase = i == 0 ? 0f : R(0f, 0.25f);
            }
            taiBatch.Finish("TaiChi", MaterialLibrary.VertexColorWind, Root, 0, true);
            PoseTaiChi(true);
        }

        void PoseTaiChi(bool force)
        {
            if (taiBatch == null) return;
            var atm = Atmosphere.Instance;
            bool show = atm == null || atm.TimeIndex == 0 || atm.TimeIndex == 1;
            if (show != taiShown) { taiShown = show; taiBatch.Renderer.enabled = show; }
            if (!show || (!force && !L.Due(taiPos, 13, 60f, 130f, 200f))) return;
            for (int i = 0; i < tai.Count; i++)
            {
                var f = tai[i];
                float t = L.Clock * 0.75f - f.phase;          // the group follows the leader a touch late
                float cyc = t / 9f * Mathf.PI * 2f;
                float raise = 0.5f - 0.5f * Mathf.Cos(cyc);
                float push = Mathf.Max(0f, Mathf.Sin(cyc * 2f));
                float turn = Mathf.Sin(cyc * 0.5f) * 30f;
                float shift = Mathf.Sin(cyc) * 0.12f;
                Quaternion rot = Quaternion.Euler(0f, f.yaw + turn, 0f);
                Vector3 p = f.pos + rot * new Vector3(shift, -0.04f * raise, 0f);
                Matrix4x4 m = Matrix4x4.TRS(p, rot * Quaternion.Euler(0f, 0f, -shift * 20f), Vector3.one);
                taiBatch.Set(f.body, m);
                float pitch = Mathf.Lerp(-10f, -85f, raise) - push * 10f;
                float side = Mathf.Lerp(8f, 30f, push) - raise * 6f;
                taiBatch.Set(f.armR, m * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(pitch, 0f, side), Vector3.one));
                taiBatch.Set(f.armL, m * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(pitch + Mathf.Sin(cyc * 2f) * 15f, 0f, -side), Vector3.one));
            }
            taiBatch.Upload();
        }

        public override void Tick(float dt)
        {
            PoseFarmers(false);
            if (monkBatch != null) PoseMonks(dt, false);
            PoseChess(false);
            PoseTaiChi(false);
        }
    }
}

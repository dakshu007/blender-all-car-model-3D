// SnowRegion.Build.cs - Frost Summit content: the frozen lake's ice sheet, roadside snow poles / banks / walls / fences,
// the gateway arch, the ski-lodge village with its ice-sculpture plaza, the cable car to the Ski Peak, the summit
// observatory, wind turbines, cliff-rim viewpoints, crags along the escarpment and the Frost Crown, crevasses, a frozen
// waterfall, a crashed snowplough and the snowy forests. Animation + weather effects live in SnowFX.Build.cs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class SnowRegion
    {
        RegionContext ctx;
        float buildT;
        readonly List<Vector3> chimneys = new List<Vector3>();

        // crisp, cold blue-white with warm sun
        public override Grade Grade
        {
            get
            {
                var g = Grade.Identity;
                g.tint = new Vector3(0.985f, 1.0f, 1.045f);
                g.sat = 0.96f;
                g.con = 1.05f;
                g.shadow = new Vector3(0.84f, 0.92f, 1.16f);
                g.shadowAmt = 0.1f;
                g.lift = new Vector3(0.9f, 0.95f, 1.06f);
                g.liftAmt = 0.03f;
                return g;
            }
        }

        static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        static float YawOf(Vector2 forward) { return Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg; }
        static Vector2 Dir(float yawDeg) { float a = yawDeg * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(a), Mathf.Cos(a)); }
        float Rand(float a, float b) { return ctx.Rand(a, b); }
        float G(Vector2 p) { return ctx.Ground(p.x, p.y); }

        bool Breather()
        {
            if (Time.realtimeSinceStartup - buildT < 0.012f) return false;
            buildT = Time.realtimeSinceStartup;
            return true;
        }

        List<RoadSample> Samples(string road)
        {
            int i = ctx.Field.RoadIndex(road);
            return i >= 0 ? ctx.Field.roadSamples[i] : new List<RoadSample>();
        }

        /// <summary>World transform helper: model point -> world (pos, yaw degrees).</summary>
        static Vector3 L2W(Vector3 pos, float yaw, Vector3 local) { return pos + Quaternion.Euler(0f, yaw, 0f) * local; }

        void BoxAt(Vector3 pos, float yaw, Vector3 localCentre, Vector3 size) { ctx.Box(L2W(pos, yaw, localCentre), size, yaw); }

        public override IEnumerator Build(RegionContext c)
        {
            ctx = c;
            buildT = Time.realtimeSinceStartup;
            ctx.Progress = 0.02f;
            foreach (var s in BuildIce()) if (s) yield return null;
            ctx.Progress = 0.1f;
            foreach (var s in BuildRoadside()) if (s) yield return null;
            ctx.Progress = 0.3f;
            BuildGate();
            BuildVillage();
            yield return null;
            ctx.Progress = 0.4f;
            BuildCableCar();
            BuildObservatory();
            BuildTurbines();
            yield return null;
            ctx.Progress = 0.5f;
            BuildViewpoints();
            BuildCrags();
            BuildLandmarkDecor();
            yield return null;
            BuildLife();
            yield return null;
            ctx.Progress = 0.6f;
            foreach (var s in BuildForest()) if (s) yield return null;
            ctx.Progress = 0.9f;
            BuildFX();
            ctx.Progress = 1f;
        }

        // ================================================================== frozen lake
        IEnumerable<bool> BuildIce()
        {
            const int RINGS = 14, SEG = 96;
            var verts = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float x0 = LakeC.x - LakeR.x * 1.2f, z0 = LakeC.y - LakeR.y * 1.2f, sx = LakeR.x * 2.4f, sz = LakeR.y * 2.4f;
            for (int j = 0; j <= RINGS; j++)
            {
                float f = j / (float)RINGS;
                for (int k = 0; k < SEG; k++)
                {
                    float a = k * Mathf.PI * 2f / SEG;
                    float wob = 1f + 0.06f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 5f + 2.1f);
                    float x = LakeC.x + Mathf.Cos(a) * wob * f * LakeR.x, z = LakeC.y + Mathf.Sin(a) * wob * f * LakeR.y;
                    float y = LakeLevel + (j == RINGS ? 0.0f : 0.06f);
                    verts.Add(new Vector3(x, y, z));
                    nrm.Add(Vector3.up);
                    uvs.Add(new Vector2((x - x0) / sx, (z - z0) / sz));
                    if (j == 0) break;
                }
            }
            // centre fan + rings
            for (int k = 0; k < SEG; k++) { tris.Add(0); tris.Add(1 + (k + 1) % SEG); tris.Add(1 + k); }
            for (int j = 1; j < RINGS; j++)
            {
                int r0 = 1 + (j - 1) * SEG, r1 = 1 + j * SEG;
                for (int k = 0; k < SEG; k++)
                {
                    int k1 = (k + 1) % SEG;
                    tris.Add(r0 + k); tris.Add(r0 + k1); tris.Add(r1 + k1);
                    tris.Add(r0 + k); tris.Add(r1 + k1); tris.Add(r1 + k);
                }
            }
            var mesh = new Mesh { name = "Snow_IceSheet" };
            mesh.SetVertices(verts);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var tex = new Texture2D(IceTex, IceTex, TextureFormat.RGBA32, true);
            var px = new Color32[IceTex * IceTex];
            for (int y = 0; y < IceTex; y += 32)
            {
                IceRows(px, y, Mathf.Min(IceTex, y + 32));
                yield return Breather();
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;
            var mat = MaterialLibrary.Standard("SnowIceSheet", new Color(0.86f, 0.94f, 1f), 0.86f, 0.06f, tex);
            var go = ctx.AddMesh("Snow_FrozenLake", mesh, mat, 0, false);
            go.GetComponent<MeshRenderer>().reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            // ice-fishing huts and a few snow drifts on the ice, away from the circuit
            Color32[] hutCols = { LifeGeo.C(214, 60, 52), LifeGeo.C(46, 110, 190), LifeGeo.C(240, 186, 40), LifeGeo.C(56, 150, 96) };
            Vector2[] huts = { new Vector2(1032f, 912f), new Vector2(1046f, 890f), new Vector2(1160f, 905f), new Vector2(1186f, 884f), new Vector2(1104f, 940f), new Vector2(1210f, 930f) };
            for (int i = 0; i < huts.Length; i++)
            {
                Vector2 p = huts[i];
                if (ctx.EdgeDist(p) < 8f) continue;
                float yaw = Rand(0f, 360f);
                ctx.Stamp(SnowShapes.FishingHut(hutCols[i % hutCols.Length]), V3(p, LakeLevel + 0.06f), yaw);
                ctx.Box(V3(p, LakeLevel + 1.3f), new Vector3(2.1f, 2.6f, 2.5f), yaw);
                ctx.Reserve(p, 3f);
            }
        }

        const int IceTex = 512;

        static void IceRows(Color32[] px, int y0, int y1)
        {
            const int S = IceTex;
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < S; x++)
                {
                    float wx = x * 0.9f, wz = y * 0.6f;
                    float n = WorldNoise.Fbm(wx, wz, 60f, 3, 7001);
                    float cr = Mathf.Abs(WorldNoise.Fbm(wx + 300f, wz, 38f, 2, 7011));
                    float cr2 = Mathf.Abs(WorldNoise.Fbm(wx, wz + 500f, 14f, 2, 7021));
                    float clear = Mathf.Clamp01(0.5f + n * 1.6f);
                    Color c = Color.Lerp(new Color(0.46f, 0.70f, 0.86f), new Color(0.74f, 0.88f, 0.97f), clear);
                    c = Color.Lerp(c, new Color(0.94f, 0.97f, 1f), WorldNoise.Smooth(0.03f, 0.008f, cr) * 0.85f);
                    c = Color.Lerp(c, new Color(0.86f, 0.93f, 1f), WorldNoise.Smooth(0.025f, 0.006f, cr2) * 0.4f * clear);
                    float h = WorldNoise.Hash01(x, y, 7031);
                    if (h > 0.996f) c = Color.Lerp(c, Color.white, 0.6f);
                    // frost toward the edges of the texture (= the shore)
                    float ex = (x / (float)S - 0.5f) * 2.4f, ey = (y / (float)S - 0.5f) * 2.4f;
                    float e = Mathf.Sqrt(ex * ex + ey * ey);
                    c = Color.Lerp(c, new Color(0.92f, 0.96f, 1f), WorldNoise.Smooth(0.82f, 1.0f, e) * 0.7f);
                    px[y * S + x] = c;
                }
        }

        // ================================================================== roadside
        static readonly string[] SnowRoads = { "Road_Snow_FrostRun", "Road_Snow_GlacierLink", "Road_Snow_LodgeLane", "Road_Snow_LakeWest", "Road_Snow_LakeNorth" };

        /// <summary>True when q (beside road r) is not covered by another road / junction / pad.</summary>
        bool SideClear(Vector2 q, int road, float minEdge)
        {
            RoadSample rs;
            float d = ctx.Field.NearestRoad(q, out rs, 30f);
            if (d < 30f && rs.road != road && d < rs.halfWidth + minEdge) return false;
            if (ctx.Field.OnPad(q)) return false;
            if (!ctx.InRegion(q, 6f)) return false;
            return true;
        }

        IEnumerable<bool> BuildRoadside()
        {
            var pole = SnowShapes.SnowPole();
            var chevL = SnowShapes.Chevron(true);
            var chevR = SnowShapes.Chevron(false);
            int poles = 0;
            var roads = new List<string>(SnowRoads) { "Road_Snow_FrostPass", "Road_Snow_IceCircuit" };
            foreach (var name in roads)
            {
                int ri = ctx.Field.RoadIndex(name);
                if (ri < 0) continue;
                var list = ctx.Field.roadSamples[ri];
                bool ice = name == "Road_Snow_IceCircuit";
                bool asphalt = name == "Road_Snow_FrostPass";
                int n = list.Count;
                // ---- snow poles (every ~26 m, staggered sides)
                float poleStep = ice ? 30f : 26f;
                float next = 6f;
                int side = 1;
                for (int i = 0; i < n; i++)
                {
                    var s = list[i];
                    if (s.distance < next) continue;
                    next = s.distance + poleStep * 0.5f;
                    side = -side;
                    Vector2 r = new Vector2(s.tangent.y, -s.tangent.x) * side;
                    Vector2 q = s.pos + r * (s.halfWidth + (ice ? 2.6f : 1.3f));
                    if (!SideClear(q, ri, 2f)) continue;
                    Vector3 p = V3(q, G(q) - 0.05f);
                    ctx.Stamp(pole, p, Rand(0f, 360f));
                    ctx.Capsule(p, 0.07f, 2.4f);
                    poles++;
                }
                // ---- banks (low drifts) or walls (snow corridor) along both sides
                if (!asphalt) yield return Breather();
                for (int sd = -1; sd <= 1; sd += 2)
                    BuildBanks(list, ri, sd, ice, asphalt);
                // ---- chevrons on the outside of tight bends
                if (!ice) BuildChevrons(list, ri, chevL, chevR);
                yield return Breather();
            }
            BuildFences();
        }

        bool WallAt(string road, float d)
        {
            if (road != "Road_Snow_FrostRun") return false;
            return (d > 1380f && d < 1560f) || (d > 150f && d < 250f);
        }

        void BuildBanks(List<RoadSample> list, int ri, int side, bool ice, bool asphalt)
        {
            string name = ctx.Field.roads[ri].name;
            var pts = new List<Vector3>();
            var rights = new List<Vector3>();
            bool curWall = false;
            Vector3 origin = Vector3.zero;
            int n = list.Count;
            bool closed = ctx.Field.roads[ri].closed;
            int step = 2;
            System.Action flush = null;
            flush = () =>
            {
                if (pts.Count >= 3)
                {
                    var m = new PropModel();
                    var g = m[PK.Props];
                    for (int i = 0; i < pts.Count; i++) pts[i] -= origin;
                    float h = curWall ? 2.6f : (ice ? 0.9f : (asphalt ? 0.45f : 0.75f));
                    float w = curWall ? 2.2f : (ice ? 2.6f : 2.4f);
                    SnowShapes.BankStrip(g, pts, rights, h, w, origin.x * 0.1f + side, curWall);
                    ctx.Stamp(m, origin, 0f);
                    // colliders: walls and the ice-circuit banks keep cars in
                    if (curWall || ice)
                    {
                        for (int i = 0; i + 4 < pts.Count; i += 4)
                        {
                            Vector3 a = pts[i] + origin, b = pts[i + 4] + origin;
                            Vector3 mid = (a + b) * 0.5f + rights[i + 2] * (w * 0.4f);
                            Vector3 d = b - a; d.y = 0f;
                            if (d.sqrMagnitude < 0.5f) continue;
                            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                            ctx.Box(mid + Vector3.up * (h * 0.4f), new Vector3(w * 0.8f, h, d.magnitude + 0.4f), yaw);
                        }
                    }
                }
                pts.Clear();
                rights.Clear();
            };
            int count = closed ? n + step : n;
            for (int ii = 0; ii < count; ii += step)
            {
                var s = list[ii % n];
                Vector2 r2 = new Vector2(s.tangent.y, -s.tangent.x) * side;
                bool wall = WallAt(name, s.distance);
                float off = s.halfWidth + (wall ? 0.6f : (ice ? 1.2f : 0.9f));
                Vector2 q = s.pos + r2 * off;
                bool ok = SideClear(q, ri, 3f) && SideClear(q + r2 * 2.2f, ri, 1f) && (ice || LakeE(q.x, q.y) > 1.04f);
                if (!ice && !wall && !asphalt)
                {
                    // gaps: banks come and go so the verge breathes
                    float gap = Mathf.Sin(s.distance * 0.045f + side * 1.7f + ri) + 0.4f * Mathf.Sin(s.distance * 0.13f);
                    if (gap < -0.55f) ok = false;
                }
                if (asphalt && s.distance < 30f) ok = false;
                if (!ok || wall != curWall || pts.Count > 36)
                {
                    Vector3 lastP = pts.Count > 0 ? pts[pts.Count - 1] : Vector3.zero;
                    Vector3 lastR = rights.Count > 0 ? rights[rights.Count - 1] : Vector3.zero;
                    bool cont = ok && wall == curWall && pts.Count > 36;
                    flush();
                    curWall = wall;
                    if (cont) { origin = lastP; pts.Add(lastP); rights.Add(lastR); }
                    if (!ok) continue;
                }
                if (pts.Count == 0) origin = V3(q, G(q));
                float y = Mathf.Min(G(q), s.height + 0.3f) - 0.05f;
                pts.Add(V3(q, y));
                rights.Add(new Vector3(r2.x, 0f, r2.y));
            }
            flush();
        }

        void BuildChevrons(List<RoadSample> list, int ri, PropModel chevL, PropModel chevR)
        {
            int n = list.Count;
            float lastD = -999f;
            for (int i = 8; i < n - 8; i += 3)
            {
                Vector2 a = list[i - 8].pos, b = list[i].pos, c = list[i + 8].pos;
                float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                float area2 = Mathf.Abs(cross);
                if (area2 < 1e-3f) continue;
                float rad = ab * bc * ca / (2f * area2);
                if (rad > 55f || list[i].distance - lastD < 14f) continue;
                lastD = list[i].distance;
                var s = list[i];
                // outside of the bend: turning left (cross > 0) -> outside is the right
                float side = cross > 0f ? 1f : -1f;
                Vector2 r = new Vector2(s.tangent.y, -s.tangent.x) * side;
                Vector2 q = s.pos + r * (s.halfWidth + 3.4f);
                if (!SideClear(q, ri, 3f)) continue;
                // the sign faces the oncoming car (its -Z towards -tangent)
                float yaw = YawOf(s.tangent);
                ctx.Stamp(cross > 0f ? chevR : chevL, V3(q, G(q) - 0.05f), yaw);
                ctx.Capsule(V3(q, G(q)), 0.08f, 1.9f);
            }
        }

        void BuildFences()
        {
            // slatted snow fences on the windward (north / west) side of the open stretches
            var run = Samples("Road_Snow_FrostRun");
            int ri = ctx.Field.RoadIndex("Road_Snow_FrostRun");
            var fenceA = SnowShapes.SnowFence(12f, ctx.Rng, false);
            var fenceB = SnowShapes.SnowFence(12f, ctx.Rng, true);
            float next = 0f;
            foreach (var s in run)
            {
                bool rim = s.distance > 640f && s.distance < 1060f;
                bool east = s.distance > 1100f && s.distance < 1340f;
                if (!rim && !east) continue;
                if (s.distance < next) continue;
                next = s.distance + 16f;
                if (Mathf.Sin(s.distance * 0.02f) < -0.6f) continue;
                Vector2 r = new Vector2(s.tangent.y, -s.tangent.x);
                // north of the rim road / west of the east road = left side when driving the run
                Vector2 q = s.pos - r * (s.halfWidth + 9f);
                if (!SideClear(q, ri, 6f) || !SideClear(q + s.tangent * 6f, ri, 6f) || !SideClear(q - s.tangent * 6f, ri, 6f)) continue;
                float yaw = YawOf(new Vector2(-s.tangent.y, s.tangent.x)) + 180f;  // model X along the road, -Z (windward drift) away from the road
                yaw = Mathf.Atan2(r.x, r.y) * Mathf.Rad2Deg;                        // +Z towards the road
                Vector3 p = V3(q, G(q));
                ctx.Stamp(rim ? fenceA : fenceB, p, yaw);
                BoxAt(p, yaw, new Vector3(0f, 0.8f, 0.05f), new Vector3(12f, 1.6f, 0.3f));
            }
        }

        // ================================================================== gateway arch
        void BuildGate()
        {
            var list = Samples("Road_Snow_FrostPass");
            if (list.Count < 20) return;
            var s = list[12];
            float yaw = YawOf(new Vector2(s.tangent.y, -s.tangent.x));   // model X across the road
            yaw = YawOf(s.tangent) + 90f;
            Vector3 p = V3(s.pos, s.height - 0.05f);
            float span = s.halfWidth * 2f + 4.5f;
            var arch = SnowShapes.GateArch(span);
            ctx.Stamp(arch, p, YawOf(s.tangent));
            Vector2 r = new Vector2(s.tangent.y, -s.tangent.x);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector2 q = s.pos + r * sd * span * 0.5f;
                ctx.Box(V3(q, G(q) + 2f), new Vector3(1.5f, 4f, 1.5f), YawOf(s.tangent));
                ctx.Reserve(q, 2f);
            }
            // prayer flags from the arch to poles on both sides
            var flags = new PropModel();
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector2 q = s.pos + r * sd * (span * 0.5f + 12f) - s.tangent * 6f;
                Vector3 top = V3(q, G(q)) - p;
                Vector3 local = Quaternion.Euler(0f, -YawOf(s.tangent), 0f) * top;
                SnowShapes.FlagPole(flags[PK.Props], local, 5.5f);
                SnowShapes.FlagLine(flags, new Vector3(sd * span * 0.5f, 5.9f, 0f), local + Vector3.up * 5.4f, ctx.Rng, 0.6f);
                ctx.Capsule(V3(q, G(q)), 0.1f, 5.5f);
            }
            ctx.Stamp(flags, p, YawOf(s.tangent));
            // welcome cairns
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector2 q = s.pos + r * sd * (span * 0.5f + 3.2f) + s.tangent * 3f;
                ctx.Stamp(SnowShapes.Cairn(ctx.Rng), V3(q, G(q) - 0.1f), Rand(0f, 360f));
            }
        }

        // ================================================================== village
        void BuildVillage()
        {
            int lane = ctx.Field.RoadIndex("Road_Snow_LodgeLane");
            // chalets around the plaza and up the lane, each facing the nearest road
            Vector2[] spots =
            {
                new Vector2(910f, 1188f), new Vector2(944f, 1196f), new Vector2(980f, 1176f), new Vector2(915f, 1112f), new Vector2(970f, 1120f),
                new Vector2(1030f, 1140f), new Vector2(1032f, 1180f), new Vector2(962f, 1232f), new Vector2(1036f, 1222f), new Vector2(880f, 1150f),
                new Vector2(1060f, 1106f), new Vector2(925f, 1240f)
            };
            int v = 0;
            foreach (var sp in spots)
            {
                Vector2 p = sp;
                if (ctx.EdgeDist(p) < 9f || !ctx.Free(p, 7f)) continue;
                RoadSample rs;
                ctx.Field.NearestRoad(p, out rs, 120f);
                Vector2 toRoad = (rs.pos - p).normalized;
                Vector2 toPlaza = (Plaza - p).normalized;
                Vector2 face = Vector2.Distance(p, Plaza) < 45f ? toPlaza : toRoad;
                float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;     // model -Z = front
                float hx = Rand(3.6f, 5.2f), hz = Rand(2.8f, 3.6f), wh = Rand(2.6f, 3.4f);
                bool lodge = v == 2;
                if (lodge) { hx = 8f; hz = 5f; wh = 5.2f; }
                Vector3 chim;
                var m = SnowShapes.Chalet(hx, hz, wh, v, ctx.Rng, out chim);
                float y = MinGround(p, hx + 1f, hz + 1f, yaw) + 0.05f;
                Vector3 pos = V3(p, y);
                ctx.Stamp(m, pos, yaw);
                BoxAt(pos, yaw, new Vector3(0f, wh * 0.5f + 0.6f, 0f), new Vector3(hx * 2f + 0.4f, wh + 1.2f, hz * 2f + 0.4f));
                chimneys.Add(L2W(pos, yaw, chim));
                ctx.Reserve(p, Mathf.Max(hx, hz) + 3f);
                // snowmobiles parked by the door
                if (v % 2 == 0)
                {
                    Color32[] cols = { LifeGeo.C(220, 40, 40), LifeGeo.C(240, 190, 30), LifeGeo.C(40, 110, 200), LifeGeo.C(30, 30, 34) };
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 lp = new Vector3(-hx * 0.5f + k * 1.6f + 2.2f, 0f, -hz - 3.2f);
                        Vector3 wp = L2W(pos, yaw, lp);
                        wp.y = ctx.Ground(wp.x, wp.z) - 0.02f;
                        float yy = yaw + 90f + Rand(-12f, 12f);
                        ctx.Stamp(SnowShapes.Snowmobile(cols[(v + k) % cols.Length]), wp, yy);
                        ctx.Box(wp + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 3f), yy);
                    }
                }
                // a lamp at the corner
                Vector3 lampP = L2W(pos, yaw, new Vector3(hx + 1.5f, 0f, -hz - 1.6f));
                lampP.y = ctx.Ground(lampP.x, lampP.z);
                ctx.Stamp(SnowShapes.Lamp(), lampP, yaw + 180f);
                ctx.Capsule(lampP, 0.1f, 3.6f);
                v++;
            }
            // ice-sculpture plaza
            float py = G(Plaza);
            Vector2[] sculpt = { new Vector2(-12f, 4f), new Vector2(0f, -4f), new Vector2(12f, 5f), new Vector2(-20f, -8f), new Vector2(18f, -8f) };
            int[] kinds = { 0, 1, 2, 3, 4 };
            for (int i = 0; i < sculpt.Length; i++)
            {
                Vector2 o = Rot(sculpt[i], -12f);
                Vector2 p = Plaza + o;
                float yaw = YawOf(Plaza - p) + 180f + Rand(-20f, 20f);
                ctx.Stamp(SnowShapes.IceSculpture(kinds[i], ctx.Rng), V3(p, G(p) - 0.02f), yaw);
                ctx.Box(V3(p, G(p) + 1.5f), new Vector3(3f, 3f, 3f), yaw);
                ctx.Reserve(p, 3f);
            }
            // lamps and flag lines around the plaza
            var deco = new PropModel();
            Vector3 pc = V3(Plaza, py);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.3f;
                Vector2 p = Plaza + new Vector2(Mathf.Cos(a) * 25f, Mathf.Sin(a) * 18f);
                Vector3 lp = V3(p, G(p));
                ctx.Stamp(SnowShapes.Lamp(), lp, Rand(0f, 360f));
                ctx.Capsule(lp, 0.1f, 3.6f);
                SnowShapes.FlagPole(deco[PK.Props], lp - pc, 5.5f);
                float a2 = (k + 1) * Mathf.PI / 3f + 0.3f;
                Vector2 p2 = Plaza + new Vector2(Mathf.Cos(a2) * 25f, Mathf.Sin(a2) * 18f);
                SnowShapes.FlagLine(deco, lp - pc + Vector3.up * 5.4f, V3(p2, G(p2)) - pc + Vector3.up * 5.4f, ctx.Rng, 0.9f);
            }
            ctx.Stamp(deco, pc, 0f);
        }

        static Vector2 Rot(Vector2 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        float MinGround(Vector2 p, float hx, float hz, float yaw)
        {
            float mn = G(p);
            for (int sx = -1; sx <= 1; sx++)
                for (int sz = -1; sz <= 1; sz++)
                {
                    Vector3 w = Quaternion.Euler(0f, yaw, 0f) * new Vector3(sx * hx, 0f, sz * hz);
                    mn = Mathf.Min(mn, ctx.Ground(p.x + w.x, p.y + w.z));
                }
            return mn;
        }

        // ================================================================== cable car
        readonly List<Vector3> cableSup = new List<Vector3>();   // support points along the line centre (bottom -> top)
        Vector3 cableSide;                                        // unit vector to the 'up' cable
        float cableLen;
        readonly List<float> cableCum = new List<float>();

        void BuildCableCar()
        {
            Vector2 b2 = CableBottom + new Vector2(0f, 12f), t2 = CableTop;
            Vector2 dir = (t2 - b2).normalized;
            float yaw = YawOf(dir);
            Vector3 wheelB, wheelT;
            var stB = SnowShapes.CableStation(false, out wheelB);
            var stT = SnowShapes.CableStation(true, out wheelT);
            Vector3 pb = V3(b2, G(b2) - 0.1f), pt = V3(t2, G(t2) - 0.1f);
            ctx.StampSkyline(stB, pb, yaw);
            ctx.StampSkyline(stT, pt, yaw + 180f);
            BoxAt(pb, yaw, new Vector3(0f, 4f, -2.4f), new Vector3(14f, 8f, 7.2f));
            BoxAt(pt, yaw + 180f, new Vector3(0f, 4f, -2.4f), new Vector3(14f, 8f, 7.2f));
            ctx.Reserve(b2, 11f);
            ctx.Reserve(t2, 11f);
            Vector3 wb = L2W(pb, yaw, wheelB), wt = L2W(pt, yaw + 180f, wheelT);
            cableSide = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            // supports: stations + pylons where the straight line would come within 10 m of the ground
            cableSup.Clear();
            cableSup.Add(wb);
            cableSup.Add(wt);
            for (int iter = 0; iter < 5; iter++)
            {
                float worst = 0f; int wi = -1; Vector3 wp = Vector3.zero;
                for (int sgi = 0; sgi < cableSup.Count - 1; sgi++)
                {
                    Vector3 a = cableSup[sgi], b = cableSup[sgi + 1];
                    float len = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    for (float d = 12f; d < len - 12f; d += 4f)
                    {
                        Vector3 p = Vector3.Lerp(a, b, d / len);
                        float need = ctx.Ground(p.x, p.z) + 10f - p.y;
                        if (need > worst) { worst = need; wi = sgi; wp = p; }
                    }
                }
                if (wi < 0) break;
                wp.y = ctx.Ground(wp.x, wp.z) + 13f;
                cableSup.Insert(wi + 1, wp);
            }
            if (cableSup.Count == 2)
            {
                Vector3 mid = Vector3.Lerp(wb, wt, 0.5f);
                mid.y = Mathf.Max(mid.y, ctx.Ground(mid.x, mid.z) + 10f);
                cableSup.Insert(1, mid);
            }
            // pylons
            for (int i = 1; i < cableSup.Count - 1; i++)
            {
                Vector3 s = cableSup[i];
                float gy = ctx.Ground(s.x, s.z);
                float H = s.y - gy - 0.3f;
                ctx.StampSkyline(SnowShapes.Pylon(H, 3f), new Vector3(s.x, gy, s.z), yaw + 90f);
                ctx.Box(new Vector3(s.x, gy + 3f, s.z), new Vector3(4f, 6f, 4f), yaw);
                AddBlinker(s + Vector3.up * 1.2f, 1);
            }
            // the two cables (with a little sag), as thin beams
            var cab = new PropModel();
            var cg = cab[PK.Landmark];
            Vector3 o = cableSup[0];
            for (int i = 0; i < cableSup.Count - 1; i++)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Vector3 prev = CablePoint(i, 0f, sd) - o;
                    for (int k = 1; k <= 8; k++)
                    {
                        Vector3 p = CablePoint(i, k / 8f, sd) - o;
                        PG.Rod(cg, prev, p, 0.05f, LifeGeo.C(40, 40, 44), 3);
                        prev = p;
                    }
                }
            ctx.StampSkyline(cab, o, 0f);
            cableCum.Clear();
            cableCum.Add(0f);
            for (int i = 0; i < cableSup.Count - 1; i++) cableCum.Add(cableCum[i] + Vector3.Distance(cableSup[i], cableSup[i + 1]));
            cableLen = cableCum[cableCum.Count - 1];
            // queueing skiers' gear at the base: skis racks + flags
            var flags = new PropModel();
            Vector3 fb = L2W(pb, yaw, new Vector3(-12f, 0f, -6f));
            fb.y = ctx.Ground(fb.x, fb.z);
            Vector3 fe = L2W(pb, yaw, new Vector3(12f, 0f, -6f));
            fe.y = ctx.Ground(fe.x, fe.z);
            SnowShapes.FlagPole(flags[PK.Props], Vector3.zero, 6f);
            SnowShapes.FlagPole(flags[PK.Props], fe - fb, 6f);
            SnowShapes.FlagLine(flags, Vector3.up * 5.9f, fe - fb + Vector3.up * 5.9f, ctx.Rng, 1.2f);
            ctx.Stamp(flags, fb, 0f);
        }

        /// <summary>Point on span i of the cable (t 0..1) on side sd (+1 up cable, -1 down cable), with sag.</summary>
        Vector3 CablePoint(int i, float t, int sd)
        {
            Vector3 a = cableSup[i], b = cableSup[i + 1];
            float span = Vector3.Distance(a, b);
            Vector3 p = Vector3.Lerp(a, b, t) + cableSide * (3f * sd);
            p.y -= span * 0.012f * 4f * t * (1f - t);
            return p;
        }

        // ================================================================== observatory
        void BuildObservatory()
        {
            Vector2 c = Obs + new Vector2(-12f, 30f);
            float y = MinGround(c, 9f, 9f, 0f);
            Vector3 beacon;
            var m = SnowShapes.Observatory(out beacon);
            Vector3 pos = V3(c, y);
            ctx.StampSkyline(m, pos, 0f);
            ctx.Capsule(pos + Vector3.down, 8.2f, 9f);
            ctx.Box(pos + new Vector3(9.5f, 2.2f, 4f), new Vector3(8f, 4.4f, 6f), 0f);
            ctx.Capsule(pos + new Vector3(-10f, -1f, 6f), 3.1f, 5f);
            ctx.Capsule(pos + new Vector3(13f, -1f, 9f), 1f, 22f);
            ctx.Reserve(c, 18f);
            AddBlinker(pos + beacon, 0);
            AddBlinker(pos + new Vector3(0f, 14.6f, 0f), 2);
            // prayer flags fanning from the mast and cairns around the summit
            var fl = new PropModel();
            Vector3 mt = new Vector3(13f, 20f, 9f);
            for (int k = 0; k < 4; k++)
            {
                float a = -0.4f + k * 0.55f;
                Vector2 q = c + new Vector2(13f, 9f) + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 26f;
                Vector3 lq = V3(q, ctx.Ground(q.x, q.y)) - pos;
                SnowShapes.FlagPole(fl[PK.Props], lq, 2.2f);
                SnowShapes.FlagLine(fl, mt, lq + Vector3.up * 2.1f, ctx.Rng, 1.6f);
            }
            ctx.StampSkyline(fl, pos, 0f);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.25f + 0.4f;
                Vector2 q = Obs + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(20f, 28f);
                if (ctx.EdgeDist(q) < 3f || !ctx.Free(q, 1.5f)) continue;
                ctx.Stamp(SnowShapes.Cairn(ctx.Rng), V3(q, G(q) - 0.1f), Rand(0f, 360f));
                ctx.Reserve(q, 1.5f);
            }
            // a lamp pair at the road end
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector2 q = Obs + new Vector2(sd * 9f, 6f);
                Vector3 lp = V3(q, G(q));
                ctx.Stamp(SnowShapes.Lamp(), lp, 180f);
                ctx.Capsule(lp, 0.1f, 3.6f);
            }
        }

        // ================================================================== turbines
        struct Spinner { public int part; public Vector3 hub; public float yaw, angle, speed; }
        readonly List<Spinner> rotors = new List<Spinner>();

        void BuildTurbines()
        {
            var tower = SnowShapes.TurbineTower();
            Vector3 wind = new Vector3(1f, 0f, 0.35f).normalized;
            float yaw = Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg;   // nacelle -Z faces upwind
            foreach (var t in Turbines)
            {
                Vector3 p = V3(t, G(t) - 0.2f);
                ctx.StampSkyline(tower, p, yaw);
                ctx.Capsule(p, 2.1f, 20f);
                Vector3 hub = L2W(p, yaw, new Vector3(0f, SnowShapes.TurbineHub, -2.65f));
                rotors.Add(new Spinner { hub = hub, yaw = yaw, angle = Rand(0f, 360f), speed = Rand(26f, 34f) });
                AddBlinker(L2W(p, yaw, new Vector3(0f, SnowShapes.TurbineHub + 1.6f, 2.8f)), 0);
                ctx.Reserve(t, 6f);
            }
        }

        // ================================================================== viewpoints over the city
        void BuildViewpoints()
        {
            int vi = 0;
            foreach (var v in Viewpoints)
            {
                Vector2 c = new Vector2(v.x, v.y);
                float y = G(c);
                Vector3 pc = V3(c, y);
                float hw = v.z, hd = v.w;
                // railing along the brink (south edge of the pad), short returns at the sides
                Vector2 rc = c + new Vector2(0f, -hd + 1f);
                var rail = SnowShapes.Railing(hw * 2f - 2f);
                ctx.Stamp(rail, V3(rc, G(rc)), 0f);
                ctx.Box(V3(rc, G(rc) + 0.6f), new Vector3(hw * 2f - 2f, 1.2f, 0.3f), 0f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Vector2 sc = c + new Vector2(sd * (hw - 1f), -hd * 0.5f + 1f);
                    ctx.Stamp(SnowShapes.Railing(hd - 1f), V3(sc, G(sc)), 90f);
                    ctx.Box(V3(sc, G(sc) + 0.6f), new Vector3(0.3f, 1.2f, hd - 1f), 0f);
                }
                // binoculars, benches, an info board
                for (int k = 0; k < 3; k++)
                {
                    Vector2 q = c + new Vector2(-hw * 0.6f + k * hw * 0.6f, -hd + 2.4f);
                    if (k == 1) { ctx.Stamp(SnowShapes.Bench(), V3(q, G(q)), 0f); ctx.Box(V3(q, G(q) + 0.5f), new Vector3(2f, 1f, 0.6f), 0f); }
                    else { ctx.Stamp(SnowShapes.Binoculars(), V3(q, G(q)), 0f); ctx.Capsule(V3(q, G(q)), 0.15f, 1.5f); }
                }
                // pavilion on the biggest lookout, prayer flags on every one
                var fl = new PropModel();
                if (vi == 1)
                {
                    Vector2 q = c + new Vector2(hw * 0.55f, hd * 0.1f);
                    ctx.Stamp(SnowShapes.Pavilion(3.2f), V3(q, G(q)), 0f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI / 3f;
                        Vector2 pp = q + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f;
                        ctx.Capsule(V3(pp, G(pp) - 0.3f), 0.18f, 3.2f);
                    }
                }
                Vector2 f0 = c + new Vector2(-hw - 1.5f, -hd + 0.5f), f1 = c + new Vector2(hw + 1.5f, -hd + 0.5f);
                Vector3 l0 = V3(f0, G(f0)) - pc, l1 = V3(f1, G(f1)) - pc;
                SnowShapes.FlagPole(fl[PK.Props], l0, 4.5f);
                SnowShapes.FlagPole(fl[PK.Props], l1, 4.5f);
                SnowShapes.FlagLine(fl, l0 + Vector3.up * 4.4f, l1 + Vector3.up * 4.4f, ctx.Rng, 1.1f);
                ctx.Stamp(fl, pc, 0f);
                ctx.Capsule(V3(f0, G(f0)), 0.1f, 4.5f);
                ctx.Capsule(V3(f1, G(f1)), 0.1f, 4.5f);
                // a lamp at each pad's road side
                Vector2 lq = c + new Vector2(-hw + 1.5f, hd - 2f);
                ctx.Stamp(SnowShapes.Lamp(), V3(lq, G(lq)), 0f);
                ctx.Capsule(V3(lq, G(lq)), 0.1f, 3.6f);
                ctx.Reserve(c, Mathf.Max(hw, hd));
                vi++;
            }
        }

        // ================================================================== crags (skyline)
        void BuildCrags()
        {
            var rng = ctx.Rng;
            var g = new LifeGeo();
            // the escarpment: a continuous rock skin on the cliff face below the brink (strata, snowy ledges), with
            // buttresses and the odd spire standing proud - the wall the city looks up at
            var brink = new List<Vector2>();
            for (float x = 560f; x <= 1456f; x += 5f)
            {
                float zb = -1f;
                for (float z = 596f; z > 430f; z -= 2f)
                    if (ctx.Ground(x, z) - ctx.Ground(x, z - 4f) > 2.6f) { zb = z + 1f; break; }
                brink.Add(new Vector2(x, zb));
            }
            for (int i = 1; i < brink.Count - 1; i++)
                if (brink[i].y < 0f && brink[i - 1].y > 0f && brink[i + 1].y > 0f) brink[i] = new Vector2(brink[i].x, 0.5f * (brink[i - 1].y + brink[i + 1].y));
            // median-of-5 then a light blur so the skin never jumps between columns
            var zs = new float[brink.Count];
            for (int i = 0; i < brink.Count; i++)
            {
                var win = new List<float>();
                for (int k = -2; k <= 2; k++) { int j = i + k; if (j >= 0 && j < brink.Count && brink[j].y > 0f) win.Add(brink[j].y); }
                win.Sort();
                zs[i] = brink[i].y < 0f || win.Count == 0 ? -1f : win[win.Count / 2];
            }
            for (int i = 1; i < brink.Count - 1; i++)
                if (zs[i] > 0f && zs[i - 1] > 0f && zs[i + 1] > 0f) brink[i] = new Vector2(brink[i].x, 0.25f * zs[i - 1] + 0.5f * zs[i] + 0.25f * zs[i + 1]);
                else brink[i] = new Vector2(brink[i].x, zs[i]);
            const int ROWS = 14;
            Color32[] rockCols = { LifeGeo.C(118, 112, 116), LifeGeo.C(96, 94, 104), LifeGeo.C(138, 130, 128), LifeGeo.C(84, 86, 98), LifeGeo.C(126, 120, 122) };
            var prevRow = new Vector3[ROWS];
            bool havePrev = false;
            for (int i = 0; i < brink.Count; i++)
            {
                Vector2 bp = brink[i];
                if (bp.y < 0f) { havePrev = false; continue; }
                float x = bp.x;
                float depth = 50f + 12f * Mathf.Sin(x * 0.021f) + 7f * Mathf.Sin(x * 0.067f + 1f);
                var row = new Vector3[ROWS];
                for (int k = 0; k < ROWS; k++)
                {
                    float t = k / (float)(ROWS - 1);
                    float z = bp.y + 1.5f - t * depth;
                    float gy = ctx.Ground(x, z);
                    float jag = WorldNoise.Fbm(x, t * 40f, 14f, 2, 8101);
                    float block = WorldNoise.Fbm(x, k * 11f, 26f, 2, 8121);
                    float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f)), 0.6f);
                    float outv = k == 0 ? 0f : taper * (1.6f + 3.8f * Mathf.Abs(block) + 1.2f * jag);
                    row[k] = new Vector3(x + jag * 1.5f, gy + 0.3f + outv * 0.4f - (k == ROWS - 1 ? 2f : 0f), z - outv * 0.9f);
                }
                if (havePrev)
                    for (int k = 0; k < ROWS - 1; k++)
                    {
                        Vector3 a0 = prevRow[k], a1 = row[k], b1 = row[k + 1], b0 = prevRow[k + 1];
                        Vector3 nrm = Vector3.Cross(b0 - a0, a1 - a0).normalized;
                        if (nrm.z > 0f) nrm = -nrm;
                        int hsh = (int)(WorldNoise.Hash01(i / 4, k / 2, 8111) * 5f) % 5;
                        Color32 col = rockCols[hsh];
                        if (k == 0 || nrm.y > 0.7f) col = SnowShapes.Snow;
                        else if (nrm.y > 0.55f) col = SnowShapes.SnowShade;
                        g.QuadOut(a0, a1, b1, b0, nrm, col, col, col, col);
                    }
                System.Array.Copy(row, prevRow, ROWS);
                havePrev = true;
            }
            for (float x = 640f; x < 1440f; x += Rand(110f, 170f))
            {
                float hp = ctx.Ground(x, 600f);
                float zb = -1f;
                for (float z = 590f; z > 430f; z -= 2f)
                    if (ctx.Ground(x, z) < hp - 4f) { zb = z; break; }
                if (zb < 0f) continue;
                bool view = false;
                foreach (var v in Viewpoints) if (Mathf.Abs(x - v.x) < v.z + 6f) view = true;
                float zBase = zb - Rand(14f, 24f);
                float gy = ctx.Ground(x, zBase);
                float top = ctx.Ground(x, zb + 4f) + (view ? -4f : Rand(-2f, 5f));
                float H = top - gy + 10f;
                if (H < 12f) continue;
                SnowShapes.Crag(g, new Vector3(x, gy - 10f, zBase), H, Rand(9f, 13f), rng, 1, Rand(-8f, 8f));
                // the odd spire standing proud on the brink
                if (!view && rng.NextDouble() < 0.45)
                {
                    Vector2 p = new Vector2(x + Rand(-14f, 14f), zb + Rand(-2f, 3f));
                    if (ctx.EdgeDist(p) > 14f && ctx.Free(p, 6f))
                    {
                        float py = ctx.Ground(p.x, p.y);
                        SnowShapes.Crag(g, new Vector3(p.x, py - 5f, p.y), Rand(14f, 24f), Rand(4f, 6f), rng, 0);
                        ctx.Reserve(p, 6f);
                        ctx.Capsule(new Vector3(p.x, py - 2f, p.y), 3.5f, 14f);
                    }
                }
            }
            // the Frost Crown on the north-east rim + sentinels around the summit and the Ski Peak
            Vector2[] crown =
            {
                new Vector2(1468f, 1400f), new Vector2(1436f, 1462f), new Vector2(1480f, 1300f), new Vector2(1352f, 1472f), new Vector2(1476f, 1196f),
                new Vector2(1160f, 1468f), new Vector2(1098f, 1352f), new Vector2(860f, 1470f), new Vector2(1478f, 560f), new Vector2(1472f, 1080f),
                new Vector2(1060f, 1470f), new Vector2(700f, 1460f)
            };
            foreach (var cp in crown)
            {
                if (ctx.EdgeDist(cp) < 16f || !ctx.Free(cp, 10f)) continue;
                float gy = ctx.Ground(cp.x, cp.y);
                float H = Rand(34f, 58f), Rb = Rand(11f, 16f);
                SnowShapes.Crag(g, new Vector3(cp.x, gy - 10f, cp.y), H, Rb, rng, 0);
                for (int k = 0; k < 2; k++)
                {
                    float a = Rand(0f, Mathf.PI * 2f);
                    Vector2 q = cp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rb * Rand(1.1f, 1.6f);
                    SnowShapes.Crag(g, new Vector3(q.x, ctx.Ground(q.x, q.y) - 6f, q.y), H * Rand(0.4f, 0.65f), Rb * Rand(0.45f, 0.7f), rng, 0);
                }
                ctx.Reserve(cp, Rb * 1.6f);
            }
            var rockM = new PropModel();
            rockM.geo[(int)PK.Landmark] = g;
            ctx.StampSkyline(rockM, Vector3.zero, 0f);
        }

        // ================================================================== landmark decor
        void BuildLandmarkDecor()
        {
            var rng = ctx.Rng;
            // crevasse field on the glacier below the summit face
            int made = 0;
            for (int tries = 0; tries < 60 && made < 11; tries++)
            {
                Vector2 p = new Vector2(Rand(1100f, 1265f), Rand(1110f, 1300f));
                if (ctx.EdgeDist(p) < 16f || !ctx.Free(p, 9f) || ctx.Field.OnPad(p)) continue;
                float L = Rand(14f, 26f), W = Rand(1.6f, 3f);
                float yaw = Rand(-20f, 20f) + 90f;
                ctx.Stamp(SnowShapes.Crevasse(L, W, rng), V3(p, G(p) - 0.05f), yaw);
                ctx.Reserve(p, L * 0.5f);
                made++;
            }
            // frozen waterfall against the Ski Peak's west flank
            {
                Vector2 p = new Vector2(906f, 1352f);
                float yaw = YawOf(new Vector2(-1f, -0.6f).normalized) + 180f;
                ctx.Stamp(SnowShapes.FrozenFall(16f, 10f, rng), V3(p, G(p) - 0.4f), yaw);
                ctx.Box(V3(p, G(p) + 6f), new Vector3(12f, 14f, 6f), yaw);
                ctx.Reserve(p, 12f);
            }
            // crashed snowplough nosed into a drift on the east run
            var run = Samples("Road_Snow_FrostRun");
            foreach (var s in run)
            {
                if (s.distance < 1235f) continue;
                Vector2 r = new Vector2(s.tangent.y, -s.tangent.x);
                Vector2 q = s.pos + r * (s.halfWidth + 6.5f);
                float yaw = YawOf(s.tangent) + 28f;
                float gy = G(q);
                var mat = Matrix4x4.TRS(V3(q, gy - 0.5f), Quaternion.Euler(-6f, yaw, 11f), Vector3.one);
                ctx.Stamp(SnowShapes.Snowplough(), mat);
                var dm = new PropModel();
                int st = dm[PK.Props].v.Count;
                dm[PK.Props].Ellipsoid(new Vector3(0f, 0f, 4.8f), new Vector3(3.2f, 1.6f, 2.4f), 9, 4, SnowShapes.Snow, SnowShapes.SnowShade);
                PG.Lumpy(dm[PK.Props], st, new Vector3(0f, 0f, 4.8f), 0.12f, 3f);
                ctx.Stamp(dm, V3(q, gy - 0.3f), yaw);
                ctx.Box(V3(q, gy + 1.5f), new Vector3(2.8f, 3f, 7f), yaw);
                AddBlinker(L2W(V3(q, gy - 0.5f), yaw, new Vector3(0f, 3.2f, 2.2f)), 3);
                ctx.Reserve(q, 6f);
                break;
            }
            // cairns + flags at hairpins and the lake shore
            Vector2[] cairns = { new Vector2(1160f, 1240f), new Vector2(1428f, 1340f), new Vector2(880f, 960f), new Vector2(1320f, 1010f), new Vector2(760f, 1080f) };
            foreach (var p in cairns)
            {
                if (ctx.EdgeDist(p) < 4f || !ctx.Free(p, 2f)) continue;
                ctx.Stamp(SnowShapes.Cairn(rng), V3(p, G(p) - 0.1f), Rand(0f, 360f));
                ctx.Capsule(V3(p, G(p)), 0.8f, 2f);
                ctx.Reserve(p, 2f);
            }
            // flag lines strung over the switchbacks' hairpins
            var fl = new PropModel();
            Vector3 o = V3(new Vector2(1172f, 1238f), G(new Vector2(1172f, 1238f)));
            Vector2 a0 = new Vector2(1150f, 1220f), a1 = new Vector2(1158f, 1262f);
            Vector3 la = V3(a0, G(a0)) - o, lb = V3(a1, G(a1)) - o;
            SnowShapes.FlagPole(fl[PK.Props], la, 5f);
            SnowShapes.FlagPole(fl[PK.Props], lb, 5f);
            SnowShapes.FlagLine(fl, la + Vector3.up * 4.9f, lb + Vector3.up * 4.9f, rng, 1.2f);
            ctx.Stamp(fl, o, 0f);
        }

        // ================================================================== spectators and life
        PropModel[] people;

        PropModel PersonModel(int pose)
        {
            if (people == null)
            {
                people = new PropModel[18];
                for (int i = 0; i < people.Length; i++) people[i] = SnowShapes.Person(ctx.Rng, i % 6 == 1 ? 1 : (i % 6 == 4 ? 2 : 0));
            }
            int k = ctx.Rng.Next(people.Length / 3) * 3;
            return people[Mathf.Clamp(k + (pose == 1 ? 1 : (pose == 2 ? 2 : 0)), 0, people.Length - 1)];
        }

        /// <summary>A loose crowd of n people around c, all facing 'look'.</summary>
        void Crowd(Vector2 c, Vector2 look, int n, float spread, float y0 = float.NaN)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 p = c + new Vector2(Rand(-spread, spread), Rand(-spread, spread) * 0.6f);
                if (!ctx.Free(p, 0.45f) || ctx.EdgeDist(p) < 1.2f) continue;
                Vector2 d = (look - p).normalized;
                float yaw = Mathf.Atan2(-d.x, -d.y) * Mathf.Rad2Deg + Rand(-25f, 25f);
                float roll = (float)ctx.Rng.NextDouble();
                int pose = roll < 0.25f ? 1 : (roll < 0.35f ? 2 : 0);
                float y = float.IsNaN(y0) ? G(p) : y0;
                ctx.Stamp(PersonModel(pose), V3(p, y - 0.03f), yaw);
                ctx.Reserve(p, 0.45f);
            }
        }

        void BuildLife()
        {
            // ice circuit: crowds on the outside of the corners, a race-control tent on the shore
            var ice = Samples("Road_Snow_IceCircuit");
            int n = ice.Count;
            float lastD = -999f;
            for (int i = 0; i < n; i += 4)
            {
                Vector2 a = ice[(i - 10 + n) % n].pos, b = ice[i].pos, c = ice[(i + 10) % n].pos;
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
                float rad = ab * bc * ca / (2f * Mathf.Max(1e-3f, Mathf.Abs(cross)));
                if (rad > 70f || ice[i].distance - lastD < 70f) continue;
                lastD = ice[i].distance;
                var s = ice[i];
                float side = cross > 0f ? 1f : -1f;
                Vector2 r = new Vector2(s.tangent.y, -s.tangent.x) * side;
                Vector2 q = s.pos + r * (s.halfWidth + 6.5f);
                if (LakeE(q.x, q.y) > 0.97f) continue;
                Crowd(q, s.pos, 6, 3.5f, LakeLevel + 0.06f);
            }
            {
                Vector2 t = new Vector2(905f, 918f);
                if (ctx.Free(t, 4f) && ctx.EdgeDist(t) > 4f)
                {
                    ctx.Stamp(SnowShapes.Tent(LifeGeo.C(206, 44, 40)), V3(t, G(t)), 200f);
                    ctx.Box(V3(t, G(t) + 1.2f), new Vector3(5.4f, 2.4f, 4.2f), 200f);
                    ctx.Reserve(t, 4f);
                    Crowd(t + new Vector2(6f, 4f), new Vector2(980f, 900f), 5, 3f);
                }
            }
            // spectator spots along the Frost Run (outside of corners, with a tent and flags)
            var run = Samples("Road_Snow_FrostRun");
            float[] spots = { 300f, 560f, 950f, 1180f, 1650f, 1880f, 2090f };
            foreach (float d in spots)
            {
                int i = 0;
                while (i < run.Count - 1 && run[i].distance < d) i++;
                if (i < 8 || i > run.Count - 9) continue;
                Vector2 a = run[i - 8].pos, b = run[i].pos, c = run[i + 8].pos;
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                float side = cross > 0f ? -1f : 1f;               // inside of the bend: safe, good view
                var s = run[i];
                Vector2 r = new Vector2(s.tangent.y, -s.tangent.x) * side;
                Vector2 q = s.pos + r * (s.halfWidth + 9f);
                if (!SideClear(q, ctx.Field.RoadIndex("Road_Snow_FrostRun"), 6f) || LakeE(q.x, q.y) < 1.1f) continue;
                Crowd(q, s.pos, 7, 4f);
                Vector2 tq = q + r * 6f + s.tangent * 5f;
                if (ctx.Free(tq, 3.5f) && ctx.EdgeDist(tq) > 8f)
                {
                    float yaw = Mathf.Atan2(r.x, r.y) * Mathf.Rad2Deg;
                    ctx.Stamp(SnowShapes.Tent(d < 1000f ? LifeGeo.C(40, 104, 196) : LifeGeo.C(206, 44, 40)), V3(tq, G(tq)), yaw);
                    ctx.Box(V3(tq, G(tq) + 1.2f), new Vector3(5.4f, 2.4f, 4.2f), yaw);
                    ctx.Reserve(tq, 4f);
                }
                var fl = new PropModel();
                Vector3 o = V3(q, G(q));
                Vector2 f0 = q - s.tangent * 9f, f1 = q + s.tangent * 9f;
                Vector3 l0 = V3(f0, G(f0)) - o, l1 = V3(f1, G(f1)) - o;
                SnowShapes.FlagPole(fl[PK.Props], l0, 4f);
                SnowShapes.FlagPole(fl[PK.Props], l1, 4f);
                SnowShapes.FlagLine(fl, l0 + Vector3.up * 3.9f, l1 + Vector3.up * 3.9f, ctx.Rng, 0.8f);
                ctx.Stamp(fl, o, 0f);
            }
            // viewpoints: people at the railing looking at the city
            foreach (var v in Viewpoints)
            {
                Vector2 c = new Vector2(v.x, v.y - v.w + 3.5f);
                Crowd(c, c + new Vector2(0f, -60f), 4, v.z * 0.6f);
            }
            // the plaza and the observatory
            Crowd(Plaza + new Vector2(0f, 9f), Plaza, 9, 14f);
            Crowd(Obs + new Vector2(-6f, 16f), Obs + new Vector2(-12f, 30f), 4, 5f);
            Crowd(CableBottom + new Vector2(-9f, -6f), CableBottom + new Vector2(0f, 12f), 5, 4f);
        }

        // ================================================================== forests, dead trees, rocks
        IEnumerable<bool> BuildForest()
        {
            var rng = ctx.Rng;
            var pines = new PropModel[10];
            for (int i = 0; i < pines.Length; i++) pines[i] = SnowShapes.Pine(rng, i / (float)(pines.Length - 1));
            var dead = new PropModel[5];
            for (int i = 0; i < dead.Length; i++) dead[i] = SnowShapes.DeadTree(rng);
            var drifts = new PropModel[6];
            for (int i = 0; i < drifts.Length; i++) drifts[i] = SnowShapes.Drift(rng);
            var rocks = new PropModel[8];
            for (int i = 0; i < rocks.Length; i++) rocks[i] = SnowShapes.SnowRock(rng, i < 4 ? Rand(0.8f, 1.5f) : Rand(1.8f, 3.2f), i >= 4);
            yield return true;
            Rect r = Rect;
            const float step = 8.5f;
            int trees = 0, deads = 0, stones = 0, cols = 0, row = 0;
            for (float z = r.yMin + 6f; z < r.yMax - 6f; z += step, row++)
            {
                for (float x = r.xMin + 6f; x < r.xMax - 6f; x += step)
                {
                    Vector2 p = new Vector2(x + Rand(-3.6f, 3.6f), z + Rand(-3.6f, 3.6f));
                    float h = G(p);
                    float forest = Forest(p, h);
                    float roll = (float)rng.NextDouble();
                    float e = LakeE(p.x, p.y);
                    if (e < 1.12f) continue;
                    float edge = ctx.EdgeDist(p);
                    if (edge < 3.5f || ctx.Field.OnPad(p)) continue;
                    float slope = Slope(p);
                    bool tree = roll < forest && slope < 0.75f;
                    bool deadTree = !tree && roll < 0.012f + 0.03f * WorldNoise.Smooth(170f, 210f, h) && slope < 0.7f;
                    bool rock = !tree && !deadTree && roll > 0.985f - 0.03f * Mathf.Clamp01(slope * 1.5f);
                    if (!tree && !deadTree && !rock)
                    {
                        if (roll > 0.93f && roll < 0.952f && slope < 0.3f && edge > 7f && ctx.Free(p, 4f))
                        {
                            Vector2 wd = new Vector2(1f, 0.35f);
                            ctx.Stamp(drifts[rng.Next(drifts.Length)], V3(p, h), YawOf(wd) + 90f + Rand(-15f, 15f), Rand(0.8f, 1.4f));
                            ctx.Reserve(p, 3f);
                        }
                        continue;
                    }
                    if (edge < 6f && !rock) continue;
                    float rad = tree ? 2.4f : 1.6f;
                    if (!ctx.Free(p, rad)) continue;
                    Vector3 pos = V3(p, h - 0.15f);
                    if (tree)
                    {
                        int vi = Mathf.Clamp(Mathf.FloorToInt(((float)rng.NextDouble() * 0.5f + WorldNoise.Smooth(130f, 175f, h) * 0.5f) * pines.Length), 0, pines.Length - 1);
                        float sc = Rand(0.75f, 1.3f) * (edge < 12f ? 0.85f : 1f);
                        ctx.Stamp(pines[vi], pos, Rand(0f, 360f), sc);
                        trees++;
                        if (edge < 40f && cols < 1100) { ctx.Capsule(pos, 0.3f * sc, 4f); cols++; }
                    }
                    else if (deadTree)
                    {
                        ctx.Stamp(dead[rng.Next(dead.Length)], pos, Rand(0f, 360f), Rand(0.8f, 1.2f));
                        deads++;
                        if (edge < 40f && cols < 1100) { ctx.Capsule(pos, 0.3f, 3f); cols++; }
                    }
                    else
                    {
                        int ri = rng.Next(rocks.Length);
                        if (edge < 8f) ri = rng.Next(4);
                        float sc = Rand(0.8f, 1.4f);
                        ctx.Stamp(rocks[ri], V3(p, h - 0.25f), Rand(0f, 360f), sc);
                        stones++;
                        if (edge < 40f && cols < 1100) { ctx.Capsule(V3(p, h - 1f), (ri < 4 ? 1.1f : 2.4f) * sc, 2.5f * sc); cols++; }
                    }
                    ctx.Reserve(p, rad);
                }
                if ((row & 3) == 0) yield return Breather();
            }
            Debug.Log("[BlossomRally] Frost Summit: " + trees + " pines, " + deads + " dead trees, " + stones + " rocks");
        }

        float Slope(Vector2 p)
        {
            float h0 = G(p);
            float dx = G(p + new Vector2(3f, 0f)) - h0, dz = G(p + new Vector2(0f, 3f)) - h0;
            return Mathf.Sqrt(dx * dx + dz * dz) / 3f;
        }

        /// <summary>Tree probability: dense spruce stands in the west and around the village, thinning with altitude.</summary>
        static float Forest(Vector2 p, float h)
        {
            float n = WorldNoise.Fbm(p.x, p.y, 110f, 3, 6101);
            float k = WorldNoise.Smooth(-0.05f, 0.35f, n);
            float west = WorldNoise.Smooth(1180f, 900f, p.x);
            float alt = WorldNoise.Smooth(205f, 165f, h);
            float gate = WorldNoise.Smooth(70f, 130f, Vector2.Distance(p, WorldLayout.GateSnowWest));
            float rim = WorldNoise.Smooth(560f, 640f, p.y);
            return (0.03f + 0.62f * k * (0.4f + 0.6f * west)) * alt * gate * rim;
        }
    }
}

// TrafficNet.cs - the lane network ambient traffic drives on, built once from the world's road samples.
//
//   * Lanes: every drivable road (not the sand drift loops or the ice circuit) has one lane per direction,
//     offset to the right of the centreline (right-hand traffic). A car's place on a road is a float sample
//     index u (samples are ~2 m apart, so one sample = LaneRoad.seg metres) plus a direction (+1 / -1).
//   * Speed: a per-road limit (city 45 km/h, paving 23, country asphalt 58, highways and passes 75, gravel 41,
//     snow 34) capped by the smoothed curvature (gentle 2.4 m/s2 lateral) and back-propagated so cars brake
//     before bends instead of in them (capF for dir +1, capB for dir -1).
//   * Junctions (WorldField.junctions): a T junction gives the through road a Branch into the side road and
//     the side road's end two exits onto the through road (with a give-way rule); end-to-end joints (gateways,
//     connectors) give each road end one exit into the other. Ends without exits make cars U-turn.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal struct LaneLink
    {
        public int road, dir;
        public float idx;
        public float vTurn;
        public bool uturn;
        /// <summary>Give way to traffic on this road at this sample before taking the link (-1 = no rule).</summary>
        public int yieldRoad;
        public float yieldIdx;
        /// <summary>Only oncoming traffic matters (left turn across the through road).</summary>
        public bool oncoming;
    }

    internal sealed class Branch
    {
        public float at, pre;
        public LaneLink link;
        public bool okPos, okNeg, leftPos, leftNeg;
    }

    internal sealed class LaneRoad
    {
        public int index;
        public RoadDef def;
        public List<RoadSample> s;
        public int n;
        public bool closed, drivable, bikes;
        public float seg, limit, laneOff, bikeOff, hw;
        public RegionId region;
        public float[] capF, capB;
        public Branch[] branches = new Branch[0];
        public LaneLink[] end0 = new LaneLink[0], end1 = new LaneLink[0];
        public float endPre0 = 2f, endPre1 = 2f;
    }

    internal sealed class TrafficNet
    {
        public const float LatAcc = 2.4f;      // comfortable cornering (m/s2)
        public const float Dec = 2.6f;         // planned braking (m/s2)
        const float SpawnCell = 48f;

        public LaneRoad[] roads = new LaneRoad[0];
        public int DrivableRoads { get; private set; }
        public int LinkCount { get; private set; }
        WorldField field;
        readonly Dictionary<long, List<int>> spawnCells = new Dictionary<long, List<int>>();

        public void Build(WorldField f)
        {
            field = f;
            int R = f.roads.Count;
            roads = new LaneRoad[R];
            for (int r = 0; r < R; r++)
            {
                var def = f.roads[r];
                var s = f.roadSamples[r];
                var L = new LaneRoad { index = r, def = def, s = s, n = s != null ? s.Count : 0, closed = def.closed };
                roads[r] = L;
                if (L.n < 2) continue;
                float total = s[L.n - 1].distance + (L.closed ? Vector2.Distance(s[L.n - 1].pos, s[0].pos) : 0f);
                L.seg = Mathf.Max(0.5f, total / (L.closed ? L.n : L.n - 1));
                L.hw = def.width * 0.5f;
                L.region = WorldLayout.At(s[L.n / 2].pos);
                L.drivable = L.n >= 8 && def.surface != Surface.Sand && def.surface != Surface.Ice;
                L.laneOff = Mathf.Clamp(L.hw * 0.42f, 1.25f, 2.7f);
                L.bikeOff = Mathf.Max(L.laneOff + 0.4f, L.hw - 0.85f);
                L.bikes = L.drivable && L.region == RegionId.Valley && !def.connector;
                L.limit = Limit(def, L.region);
                if (!L.drivable) continue;
                DrivableRoads++;
                Caps(L);
            }
            BuildJunctions();
            for (int r = 0; r < R; r++)
            {
                var L = roads[r];
                if (!L.drivable) continue;
                for (int i = 2; i < L.n - 2; i += 3)
                {
                    long key = WorldField.Key(Mathf.FloorToInt(L.s[i].pos.x / SpawnCell), Mathf.FloorToInt(L.s[i].pos.y / SpawnCell));
                    List<int> l;
                    if (!spawnCells.TryGetValue(key, out l)) { l = new List<int>(); spawnCells[key] = l; }
                    if (i < 65536) l.Add((r << 16) | i);
                }
            }
        }

        static float Limit(RoadDef def, RegionId region)
        {
            if (def.surface == Surface.Paving) return 6.5f;
            if (def.surface == Surface.Snow) return 9.5f;
            if (def.surface != Surface.Asphalt) return 11.5f;
            if (region == RegionId.City && !def.connector) return 12.5f;
            if (def.connector || def.name.Contains("Highway") || def.name.Contains("CoastRoad") || def.name.Contains("FrostPass")) return 21f;
            return 16f;
        }

        void Caps(LaneRoad L)
        {
            int n = L.n;
            var k = WorldField.Curvature(L.s, L.closed);
            var b = new float[n];
            for (int i = 0; i < n; i++)
                b[i] = Mathf.Min(L.limit, Mathf.Sqrt(LatAcc / Mathf.Max(Mathf.Abs(k[i]), 1e-4f)));
            L.capF = (float[])b.Clone();
            L.capB = (float[])b.Clone();
            float step = 2f * Dec * L.seg;
            if (L.closed)
            {
                for (int t = 2 * n - 1; t >= 0; t--) { int i = t % n, j = (i + 1) % n; L.capF[i] = Mathf.Min(L.capF[i], Mathf.Sqrt(L.capF[j] * L.capF[j] + step)); }
                for (int t = 0; t < 2 * n; t++) { int i = t % n, j = (i - 1 + n) % n; L.capB[i] = Mathf.Min(L.capB[i], Mathf.Sqrt(L.capB[j] * L.capB[j] + step)); }
            }
            else
            {
                for (int i = n - 2; i >= 0; i--) L.capF[i] = Mathf.Min(L.capF[i], Mathf.Sqrt(L.capF[i + 1] * L.capF[i + 1] + step));
                for (int i = 1; i < n; i++) L.capB[i] = Mathf.Min(L.capB[i], Mathf.Sqrt(L.capB[i - 1] * L.capB[i - 1] + step));
            }
        }

        static float TurnSpeed(Vector2 h0, Vector2 h1)
        {
            float d = Vector2.Dot(h0, h1);
            if (d < -0.5f) return 3f;
            return Mathf.Lerp(5f, 13f, Mathf.Clamp01((d + 0.15f) / 1.15f));
        }

        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        void BuildJunctions()
        {
            var br = new List<Branch>[roads.Length];
            var e0 = new List<LaneLink>[roads.Length];
            var e1 = new List<LaneLink>[roads.Length];
            for (int r = 0; r < roads.Length; r++) { br[r] = new List<Branch>(); e0[r] = new List<LaneLink>(); e1[r] = new List<LaneLink>(); }
            foreach (var j in field.junctions)
            {
                if (j.slave < 0 || j.master < 0 || j.slave >= roads.Length || j.master >= roads.Length) continue;
                var S = roads[j.slave];
                var M = roads[j.master];
                if (!S.drivable || !M.drivable) continue;
                int sEnd = j.slaveEnd == 0 ? 0 : S.n - 1;
                int sOut = j.slaveEnd == 0 ? 1 : -1;
                Vector2 hin = S.s[sEnd].tangent * -sOut;
                var sEnds = j.slaveEnd == 0 ? e0[S.index] : e1[S.index];
                if (j.endToEnd)
                {
                    int mEnd = j.masterEnd == 0 ? 0 : M.n - 1;
                    int mOut = j.masterEnd == 0 ? 1 : -1;
                    Vector2 hm = M.s[mEnd].tangent * mOut;
                    sEnds.Add(new LaneLink { road = M.index, idx = mEnd + mOut * 2, dir = mOut, vTurn = TurnSpeed(hin, hm), yieldRoad = -1 });
                    (j.masterEnd == 0 ? e0[M.index] : e1[M.index]).Add(new LaneLink
                    {
                        road = S.index, idx = sEnd + sOut * 2, dir = sOut, vTurn = TurnSpeed(-hm, -hin), yieldRoad = -1
                    });
                    LinkCount += 2;
                    continue;
                }
                int mi = Nearest(M, j.point);
                float clearS = Mathf.Min((M.hw + 2.5f) / S.seg, S.n * 0.3f);
                float clearM = (S.hw + 2.5f) / M.seg;
                if (j.slaveEnd == 0) S.endPre0 = Mathf.Max(S.endPre0, clearS); else S.endPre1 = Mathf.Max(S.endPre1, clearS);
                // through road -> side road
                float sIdx = sEnd + sOut * clearS;
                Vector2 hs = S.s[Mathf.Clamp(Mathf.RoundToInt(sIdx), 0, S.n - 1)].tangent * sOut;
                Vector2 hmPos = M.s[mi].tangent;
                var b = new Branch
                {
                    at = mi, pre = (S.hw + 1.5f) / M.seg,
                    link = new LaneLink { road = S.index, idx = sIdx, dir = sOut, vTurn = TurnSpeed(hmPos, hs), yieldRoad = -1 },
                    okPos = Vector2.Dot(hmPos, hs) > -0.25f, okNeg = Vector2.Dot(-hmPos, hs) > -0.25f,
                    leftPos = Cross(hmPos, hs) > 0f, leftNeg = Cross(-hmPos, hs) > 0f
                };
                b.link.vTurn = Mathf.Min(TurnSpeed(hmPos, hs), TurnSpeed(-hmPos, hs)) + 1f;
                br[M.index].Add(b);
                LinkCount++;
                // side road end -> through road, both ways (give way to the through road)
                for (int d = -1; d <= 1; d += 2)
                {
                    float idx = mi + d * clearM;
                    if (M.closed) idx = Wrap(M, idx);
                    else if (idx < 1f || idx > M.n - 2f) continue;
                    Vector2 hm = M.s[mi].tangent * d;
                    if (Vector2.Dot(hin, hm) < -0.25f) continue;
                    sEnds.Add(new LaneLink { road = M.index, idx = idx, dir = d, vTurn = TurnSpeed(hin, hm), yieldRoad = M.index, yieldIdx = mi });
                    LinkCount++;
                }
            }
            for (int r = 0; r < roads.Length; r++)
            {
                br[r].Sort((x, y) => x.at.CompareTo(y.at));
                roads[r].branches = br[r].ToArray();
                roads[r].end0 = e0[r].ToArray();
                roads[r].end1 = e1[r].ToArray();
            }
        }

        static int Nearest(LaneRoad L, Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < L.n; i++)
            {
                float d = (L.s[i].pos - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ lane geometry
        public static float Wrap(LaneRoad L, float u)
        {
            if (!L.closed) return Mathf.Clamp(u, 0f, L.n - 1f);
            float n = L.n;
            u %= n;
            if (u < 0f) u += n;
            return u;
        }

        /// <summary>Signed sample count from 'from' to 'to' (shortest way round on closed roads).</summary>
        public static float Delta(LaneRoad L, float from, float to)
        {
            float d = to - from;
            if (L.closed)
            {
                float n = L.n;
                if (d > n * 0.5f) d -= n;
                else if (d < -n * 0.5f) d += n;
            }
            return d;
        }

        /// <summary>World position on a lane: lat metres to the right of the driving direction (dir).</summary>
        public static void Pose(LaneRoad L, float u, int dir, float lat, out Vector3 pos, out Vector2 fwd)
        {
            u = Wrap(L, u);
            int i = Mathf.Min((int)u, L.n - 1);
            float f = u - i;
            int j = L.closed ? (i + 1) % L.n : Mathf.Min(i + 1, L.n - 1);
            var a = L.s[i];
            var b = L.s[j];
            Vector2 tan = Vector2.Lerp(a.tangent, b.tangent, f);
            float m = tan.magnitude;
            tan = m > 1e-5f ? tan / m : a.tangent;
            Vector2 right = new Vector2(tan.y, -tan.x);
            float sl = lat * dir;
            Vector2 c = Vector2.Lerp(a.pos, b.pos, f) + right * sl;
            float h = Mathf.Lerp(a.SurfaceAt(sl), b.SurfaceAt(sl), f);
            pos = new Vector3(c.x, h, c.y);
            fwd = tan * dir;
        }

        public static float Cap(LaneRoad L, float u, int dir)
        {
            if (L.capF == null) return L.limit;
            int i = Mathf.Clamp((int)Wrap(L, u), 0, L.n - 1);
            return dir > 0 ? L.capF[i] : L.capB[i];
        }

        /// <summary>A random spawn sample near p (packed road << 16 | index), or -1.</summary>
        public int SpawnSample(Vector2 p)
        {
            List<int> l;
            if (!spawnCells.TryGetValue(WorldField.Key(Mathf.FloorToInt(p.x / SpawnCell), Mathf.FloorToInt(p.y / SpawnCell)), out l) || l.Count == 0) return -1;
            return l[Random.Range(0, l.Count)];
        }

        /// <summary>Nearest sample index on one road to p (within maxDist), or -1.</summary>
        public float NearestU(int road, Vector2 p, float maxDist)
        {
            if (road < 0 || road >= roads.Length) return -1f;
            var L = roads[road];
            RoadSample rs;
            if (field.NearestOnRoad(road, p, out rs, maxDist) == float.MaxValue) return -1f;
            // samples are evenly spaced: the distance along the road maps straight to the index
            return Wrap(L, rs.distance / L.seg);
        }
    }
}

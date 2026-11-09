// RallyTrack.cs - reads the road samples like a co-driver: how sharply every sample turns (from the
// tangents), where the corners, hairpins, fast straights, crests ("jumps") and bridges are, which
// side is the outside of each corner, and where the stage starts / finishes are.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyCorner
    {
        public int road, apex, first, last;     // sample indices (first..last may wrap on closed roads)
        public float peakTurn;                  // deg over +-10 m at the apex (absolute)
        public float totalTurn;                 // deg over the whole corner (signed: + = left)
        public int outside;                     // +1 = outside is the road's right (+normal), -1 = left
        public bool Hairpin { get { return Mathf.Abs(totalTurn) >= 100f; } }
        public bool Tight { get { return peakTurn >= 38f; } }
    }

    internal sealed class RallyStretch { public int road, first, last; }

    internal sealed class RallyCrest { public int road, idx; public float lift; }

    internal sealed class RallyStageEnds
    {
        public StageDef def;
        public int defIndex;
        public StageCourse course;
        public bool closed;
        public StageGate start, finish;
        public readonly List<int> sharedWith = new List<int>();   // other events using the same course
    }

    internal sealed class RallyTrack
    {
        public readonly WorldBuilder world;
        public readonly List<float[]> turn = new List<float[]>();     // signed deg over +-10 m (+ = left)
        public readonly List<RallyCorner> corners = new List<RallyCorner>();
        public readonly List<RallyStretch> fast = new List<RallyStretch>();
        public readonly List<RallyCrest> crests = new List<RallyCrest>();
        public readonly List<RallyStageEnds> stages = new List<RallyStageEnds>();
        /// <summary>Roads used by rally stages (0..3); Lantern Street (4) is a village street.</summary>
        public const int StageRoads = 4;

        public RallyTrack(WorldBuilder w)
        {
            world = w;
            for (int r = 0; r < w.roadSamples.Count; r++) turn.Add(ComputeTurn(r));
            for (int r = 0; r < Mathf.Min(StageRoads, w.roadSamples.Count); r++)
            {
                FindCorners(r);
                FindFast(r);
                FindCrests(r);
            }
            for (int i = 0; i < StageDef.All.Length; i++)
            {
                var def = StageDef.All[i];
                StageCourse c = null;
                try { c = StageCourse.Build(def, w, false); } catch (System.Exception e) { Debug.LogException(e); }
                if (c == null || c.gates.Count < 2) continue;
                RallyStageEnds same = null;
                foreach (var s in stages)
                    if (s.def.road == def.road && Mathf.Abs(s.course.startD - c.startD) < 1f && Mathf.Abs(s.course.endD - c.endD) < 1f) same = s;
                if (same != null) { same.sharedWith.Add(i); continue; }
                stages.Add(new RallyStageEnds
                {
                    def = def, defIndex = i, course = c, closed = c.closed, start = c.gates[0], finish = c.gates[c.gates.Count - 1]
                });
            }
        }

        public List<RoadSample> S(int road) { return world.roadSamples[road]; }
        public bool Closed(int road) { return world.roads[road].closed; }

        public int Wrap(int road, int i)
        {
            int n = world.roadSamples[road].Count;
            return Closed(road) ? (i % n + n) % n : Mathf.Clamp(i, 0, n - 1);
        }

        public bool Valid(int road, int i)
        {
            return Closed(road) || (i >= 0 && i < world.roadSamples[road].Count);
        }

        public RoadSample At(int road, int i) { return world.roadSamples[road][Wrap(road, i)]; }

        public static Vector2 Right(RoadSample s) { return new Vector2(s.tangent.y, -s.tangent.x); }

        /// <summary>World point beside the road: lateral metres from the centre line (+ = right).</summary>
        public Vector2 Side(int road, int i, float lateral)
        {
            var s = At(road, i);
            return s.pos + Right(s) * lateral;
        }

        float[] ComputeTurn(int r)
        {
            var s = world.roadSamples[r];
            int n = s.Count;
            var t = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = At(r, i - 5).tangent, b = At(r, i + 5).tangent;
                float cross = a.x * b.y - a.y * b.x, dot = a.x * b.x + a.y * b.y;
                t[i] = Mathf.Atan2(cross, dot) * Mathf.Rad2Deg;
            }
            return t;
        }

        void FindCorners(int r)
        {
            var t = turn[r];
            int n = t.Length;
            bool closed = Closed(r);
            var used = new bool[n];
            // strongest peaks first; each claims its corner span (|turn| above 40% of the peak, same sign)
            var order = new List<int>();
            for (int i = 0; i < n; i++) if (Mathf.Abs(t[i]) >= 16f) order.Add(i);
            order.Sort((a, b) => Mathf.Abs(t[b]).CompareTo(Mathf.Abs(t[a])));
            foreach (int apex in order)
            {
                if (used[apex]) continue;
                float sign = Mathf.Sign(t[apex]), peak = Mathf.Abs(t[apex]);
                int first = apex, last = apex;
                for (int k = 1; k < 60; k++)
                {
                    int j = apex - k;
                    if (!closed && j < 0) break;
                    j = Wrap(r, j);
                    if (t[j] * sign < Mathf.Max(6f, peak * 0.3f) || used[j]) break;
                    first = apex - k;
                }
                for (int k = 1; k < 60; k++)
                {
                    int j = apex + k;
                    if (!closed && j >= n) break;
                    j = Wrap(r, j);
                    if (t[j] * sign < Mathf.Max(6f, peak * 0.3f) || used[j]) break;
                    last = apex + k;
                }
                float total = 0f;
                for (int i = first; i <= last; i++)
                {
                    used[Wrap(r, i)] = true;
                    Vector2 a = At(r, i).tangent, b = At(r, i + 1).tangent;
                    total += Mathf.Atan2(a.x * b.y - a.y * b.x, a.x * b.x + a.y * b.y) * Mathf.Rad2Deg;
                }
                // merge into a neighbouring corner of the same sign (hairpins show up as two kinks)
                RallyCorner merged = null;
                foreach (var c in corners)
                    if (c.road == r && Mathf.Sign(c.totalTurn) == sign && (Gap(r, c.last, first) < 12 || Gap(r, last, c.first) < 12)) merged = c;
                if (merged != null)
                {
                    if (Gap(r, merged.last, first) < 12) merged.last = last; else merged.first = first;
                    merged.totalTurn += total;
                    if (peak > merged.peakTurn) { merged.peakTurn = peak; }
                    merged.apex = Wrap(r, (merged.first + merged.last) / 2);
                    continue;
                }
                corners.Add(new RallyCorner
                {
                    road = r, apex = apex, first = first, last = last, peakTurn = peak, totalTurn = total,
                    outside = sign > 0f ? 1 : -1
                });
            }
        }

        int Gap(int r, int a, int b)
        {
            int n = S(r).Count;
            int g = b - a;
            if (Closed(r)) g = ((g % n) + n) % n;
            return g < 0 ? 9999 : g;
        }

        void FindFast(int r)
        {
            var t = turn[r];
            int n = t.Length;
            int run = 0, start = 0;
            for (int i = 0; i <= n; i++)
            {
                bool ok = i < n && Mathf.Abs(t[i]) < 7f && !S(r)[i].bridge;
                if (ok) { if (run == 0) start = i; run++; }
                else
                {
                    if (run >= 45) fast.Add(new RallyStretch { road = r, first = start, last = i - 1 });   // >= 90 m
                    run = 0;
                }
            }
        }

        void FindCrests(int r)
        {
            var s = S(r);
            int n = s.Count;
            bool closed = Closed(r);
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i < 10 || i >= n - 10)) continue;
                if (s[i].bridge || At(r, i - 10).bridge || At(r, i + 10).bridge) continue;
                float lift = s[i].height - 0.5f * (At(r, i - 8).height + At(r, i + 8).height);
                if (lift < 0.3f) continue;
                bool best = true;
                for (int k = -12; k <= 12 && best; k++)
                {
                    if (k == 0) continue;
                    int j = i + k;
                    if (!closed && (j < 8 || j >= n - 8)) continue;
                    float l2 = At(r, j).height - 0.5f * (At(r, j - 8).height + At(r, j + 8).height);
                    if (l2 > lift) best = false;
                }
                if (best && Mathf.Abs(turn[r][i]) < 12f) crests.Add(new RallyCrest { road = r, idx = i, lift = lift });
            }
        }

        /// <summary>Sample index a distance (m) along the road from i (negative = before).</summary>
        public int Along(int road, int i, float metres)
        {
            var s = S(road);
            int steps = Mathf.RoundToInt(metres / 2f);
            int j = i + steps;
            if (!Closed(road)) return Mathf.Clamp(j, 0, s.Count - 1);
            return Wrap(road, j);
        }

        /// <summary>True when any sample in [i-range, i+range] is on a bridge.</summary>
        public bool NearBridge(int road, int i, int range)
        {
            for (int k = -range; k <= range; k++)
            {
                if (!Valid(road, i + k)) continue;
                if (At(road, i + k).bridge) return true;
            }
            return false;
        }

        /// <summary>Is sample i part of a rally stage (between some start and finish)?</summary>
        public bool OnStage(int road, int i)
        {
            float d = At(road, i).distance;
            foreach (var st in stages)
            {
                if (st.def.road != road) continue;
                if (st.closed) return true;
                if (d >= st.course.startD - 5f && d <= st.course.endD + 5f) return true;
            }
            return false;
        }
    }
}

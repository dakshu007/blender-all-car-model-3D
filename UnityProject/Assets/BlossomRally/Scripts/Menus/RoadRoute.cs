// RoadRoute.cs - GPS for the world map and the minimap (pure C#: no engine objects, so the offline harness can test it).
//
//   RoadGraph  every road's samples (WorldField.roadSamples, one every 2 m) thinned to a node every 6 m and linked along
//              each road; roads are joined where a road END lies within 15 m of another road (T-junctions, connector
//              gateways) and where samples of two roads cross within 2.5 m at the same height (crossroads; a bridge
//              over a road is not a junction). Built once per world and cached.
//   Gps        the waypoint. Setting it runs Dijkstra outward from the waypoint's road node over the whole graph
//              (A* with a zero heuristic, run once), which leaves a shortest-path tree: from ANY node the route is
//              just "follow Next[]". So the route re-plans instantly every frame wherever the player drives - no
//              re-routing pass, no allocation. Track(player) picks the best node near the car (close by, low
//              remaining distance) and rebuilds the polyline only when that node changes.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed class RoadGraph
    {
        public const int Stride = 3;                 // one node every 3 samples (6 m)
        public const float EndJoin = 15f;            // a road end joins another road within this distance
        public const float CrossJoin = 2.5f;         // two roads' samples this close (and level) form a crossroads
        const float Cell = 32f;                      // node grid
        const float SCell = 8f;                      // sample grid (build only)

        public int Count { get; private set; }
        public int EdgeCount { get; private set; }
        public Vector2[] Pos;
        public int[] Road;
        int[] adjStart, adjTo;
        float[] adjCost;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        // shortest-path tree towards Target (Dijkstra outward from it)
        public float[] Dist;
        public int[] Next;
        public int Target { get; private set; }

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public static RoadGraph Build(List<List<RoadSample>> samples, List<RoadDef> roads)
        {
            var g = new RoadGraph();
            g.Target = -1;
            int R = samples.Count;
            var pos = new List<Vector2>();
            var hgt = new List<float>();
            var road = new List<int>();
            var nodeBase = new int[R];
            var nodeSamples = new int[R][];
            for (int r = 0; r < R; r++)
            {
                var list = samples[r];
                int n = list != null ? list.Count : 0;
                nodeBase[r] = pos.Count;
                if (n == 0) { nodeSamples[r] = new int[0]; continue; }
                int cnt = (n - 1) / Stride + 1 + ((n - 1) % Stride != 0 ? 1 : 0);
                var arr = new int[cnt];
                for (int k = 0; k < cnt; k++)
                {
                    arr[k] = Mathf.Min(k * Stride, n - 1);
                    pos.Add(list[arr[k]].pos);
                    hgt.Add(list[arr[k]].height);
                    road.Add(r);
                }
                nodeSamples[r] = arr;
            }
            g.Count = pos.Count;
            g.Pos = pos.ToArray();
            g.Road = road.ToArray();

            var ea = new List<int>();
            var eb = new List<int>();
            var ew = new List<float>();
            var seen = new HashSet<long>();
            System.Action<int, int, float> add = (a, b, w) =>
            {
                if (a == b || a < 0 || b < 0) return;
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!seen.Add(key)) return;
                ea.Add(a); eb.Add(b); ew.Add(Mathf.Max(0.1f, w));
            };
            System.Func<int, int, int> nodeOf = (r, i) =>
            {
                var arr = nodeSamples[r];
                if (arr.Length == 0) return -1;
                int k = Mathf.Clamp(Mathf.RoundToInt(i / (float)Stride), 0, arr.Length - 1);
                int last = arr.Length - 1;
                if (k != last && Mathf.Abs(arr[last] - i) < Mathf.Abs(arr[k] - i)) k = last;
                return nodeBase[r] + k;
            };

            // along each road
            for (int r = 0; r < R; r++)
            {
                var arr = nodeSamples[r];
                var list = samples[r];
                for (int k = 0; k + 1 < arr.Length; k++)
                {
                    float w = list[arr[k + 1]].distance - list[arr[k]].distance;
                    if (w <= 0f) w = Vector2.Distance(list[arr[k]].pos, list[arr[k + 1]].pos);
                    add(nodeBase[r] + k, nodeBase[r] + k + 1, w);
                }
                bool closed = roads != null && r < roads.Count && roads[r].closed;
                if (closed && arr.Length > 2)
                    add(nodeBase[r] + arr.Length - 1, nodeBase[r], Vector2.Distance(list[arr[arr.Length - 1]].pos, list[arr[0]].pos));
            }

            // sample grid (every sample, packed road << 16 | index)
            var sg = new Dictionary<long, List<int>>();
            for (int r = 0; r < R; r++)
            {
                var list = samples[r];
                if (list == null) continue;
                for (int i = 0; i < list.Count && i < 65536; i++)
                {
                    long key = Key(Mathf.FloorToInt(list[i].pos.x / SCell), Mathf.FloorToInt(list[i].pos.y / SCell));
                    List<int> l;
                    if (!sg.TryGetValue(key, out l)) { l = new List<int>(); sg[key] = l; }
                    l.Add((r << 16) | i);
                }
            }

            // crossroads: samples of two different roads within CrossJoin at about the same height
            for (int r = 0; r < R; r++)
            {
                var list = samples[r];
                if (list == null) continue;
                for (int i = 0; i < list.Count && i < 65536; i++)
                {
                    Vector2 p = list[i].pos;
                    int cx = Mathf.FloorToInt(p.x / SCell), cz = Mathf.FloorToInt(p.y / SCell);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            List<int> l;
                            if (!sg.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                            for (int q = 0; q < l.Count; q++)
                            {
                                int o = l[q] >> 16, j = l[q] & 0xffff;
                                if (o <= r) continue;                       // each pair of roads once
                                var so = samples[o][j];
                                if ((so.pos - p).sqrMagnitude > CrossJoin * CrossJoin) continue;
                                if (Mathf.Abs(so.height - list[i].height) > 2.5f) continue;   // bridge / underpass
                                int a = nodeOf(r, i), b = nodeOf(o, j);
                                add(a, b, Vector2.Distance(g.Pos[a], g.Pos[b]));
                            }
                        }
                }
            }

            // road ends: join the nearest sample of another road (or a far part of the same road) within EndJoin
            int reach = Mathf.CeilToInt(EndJoin / SCell);
            for (int r = 0; r < R; r++)
            {
                var list = samples[r];
                if (list == null || list.Count < 2) continue;
                if (roads != null && r < roads.Count && roads[r].closed) continue;
                for (int end = 0; end < 2; end++)
                {
                    int ei = end == 0 ? 0 : list.Count - 1;
                    Vector2 ep = list[ei].pos;
                    int cx = Mathf.FloorToInt(ep.x / SCell), cz = Mathf.FloorToInt(ep.y / SCell);
                    float best = EndJoin * EndJoin;
                    int bo = -1, bj = -1;
                    for (int dx = -reach; dx <= reach; dx++)
                        for (int dz = -reach; dz <= reach; dz++)
                        {
                            List<int> l;
                            if (!sg.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                            for (int q = 0; q < l.Count; q++)
                            {
                                int o = l[q] >> 16, j = l[q] & 0xffff;
                                if (o == r && Mathf.Abs(list[j].distance - list[ei].distance) < 60f) continue;
                                var so = samples[o][j];
                                float d = (so.pos - ep).sqrMagnitude;
                                if (d >= best || Mathf.Abs(so.height - list[ei].height) > 6f) continue;
                                best = d; bo = o; bj = j;
                            }
                        }
                    if (bo < 0) continue;
                    int a = nodeOf(r, ei), b = nodeOf(bo, bj);
                    add(a, b, Mathf.Max(Mathf.Sqrt(best), Vector2.Distance(g.Pos[a], g.Pos[b])));
                }
            }

            // compressed adjacency
            int N = g.Count, E = ea.Count;
            g.EdgeCount = E;
            g.adjStart = new int[N + 1];
            for (int e = 0; e < E; e++) { g.adjStart[ea[e] + 1]++; g.adjStart[eb[e] + 1]++; }
            for (int i = 0; i < N; i++) g.adjStart[i + 1] += g.adjStart[i];
            g.adjTo = new int[E * 2];
            g.adjCost = new float[E * 2];
            var fill = new int[N];
            for (int e = 0; e < E; e++)
            {
                int a = ea[e], b = eb[e];
                int ia = g.adjStart[a] + fill[a]++, ib = g.adjStart[b] + fill[b]++;
                g.adjTo[ia] = b; g.adjCost[ia] = ew[e];
                g.adjTo[ib] = a; g.adjCost[ib] = ew[e];
            }

            // node grid
            for (int i = 0; i < N; i++)
            {
                long key = Key(Mathf.FloorToInt(g.Pos[i].x / Cell), Mathf.FloorToInt(g.Pos[i].y / Cell));
                List<int> l;
                if (!g.grid.TryGetValue(key, out l)) { l = new List<int>(); g.grid[key] = l; }
                l.Add(i);
            }
            g.Dist = new float[N];
            g.Next = new int[N];
            for (int i = 0; i < N; i++) { g.Dist[i] = float.MaxValue; g.Next[i] = -1; }
            return g;
        }

        // ------------------------------------------------------------------ queries
        /// <summary>Nearest node to p within maxDist (optionally only nodes that reach the current Target). -1 if none.</summary>
        public int Nearest(Vector2 p, float maxDist, bool reachableOnly = false)
        {
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.y / Cell);
            float best = maxDist * maxDist;
            int bi = -1;
            int maxRing = Mathf.CeilToInt(maxDist / Cell) + 1;
            for (int ring = 0; ring <= maxRing; ring++)
            {
                if (bi >= 0)
                {
                    float edge = (ring - 1) * Cell;
                    if (edge > 0f && edge * edge > best) break;
                }
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        List<int> l;
                        if (!grid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                        for (int q = 0; q < l.Count; q++)
                        {
                            int i = l[q];
                            if (reachableOnly && Dist[i] == float.MaxValue) continue;
                            float d = (Pos[i] - p).sqrMagnitude;
                            if (d < best) { best = d; bi = i; }
                        }
                    }
            }
            return bi;
        }

        /// <summary>Where the car should join the route: a nearby node with little distance left (prefers nodes ahead).</summary>
        public int BestStart(Vector2 p)
        {
            if (Target < 0) return -1;
            const float R = 45f;
            int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.y / Cell);
            int bi = -1;
            float bs = float.MaxValue;
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    List<int> l;
                    if (!grid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    for (int q = 0; q < l.Count; q++)
                    {
                        int i = l[q];
                        if (Dist[i] == float.MaxValue) continue;
                        float d2 = (Pos[i] - p).sqrMagnitude;
                        if (d2 > R * R) continue;
                        float sc = 2.5f * Mathf.Sqrt(d2) + Dist[i];
                        if (sc < bs) { bs = sc; bi = i; }
                    }
                }
            return bi >= 0 ? bi : Nearest(p, 4000f, true);
        }

        // ------------------------------------------------------------------ shortest-path tree
        int[] heapN;
        float[] heapK;
        int heapSize;

        /// <summary>Dijkstra outward from target over the whole graph: afterwards Dist[i] = road metres to target, Next[i] = next node.</summary>
        public void SolveTo(int target)
        {
            if (target < 0 || target >= Count) return;
            Target = target;
            for (int i = 0; i < Count; i++) { Dist[i] = float.MaxValue; Next[i] = -1; }
            if (heapN == null) { heapN = new int[Count + EdgeCount * 2 + 4]; heapK = new float[heapN.Length]; }
            heapSize = 0;
            Dist[target] = 0f;
            Push(target, 0f);
            while (heapSize > 0)
            {
                float k;
                int u = Pop(out k);
                if (k > Dist[u]) continue;
                for (int e = adjStart[u]; e < adjStart[u + 1]; e++)
                {
                    int v = adjTo[e];
                    float nd = k + adjCost[e];
                    if (nd < Dist[v]) { Dist[v] = nd; Next[v] = u; Push(v, nd); }
                }
            }
        }

        void Push(int n, float k)
        {
            if (heapSize >= heapN.Length) return;      // cannot happen (each edge relaxes at most twice)
            int i = heapSize++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heapK[p] <= k) break;
                heapN[i] = heapN[p]; heapK[i] = heapK[p];
                i = p;
            }
            heapN[i] = n; heapK[i] = k;
        }

        int Pop(out float k)
        {
            int top = heapN[0];
            k = heapK[0];
            heapSize--;
            if (heapSize > 0)
            {
                int ln = heapN[heapSize];
                float lk = heapK[heapSize];
                int i = 0;
                while (true)
                {
                    int c = i * 2 + 1;
                    if (c >= heapSize) break;
                    if (c + 1 < heapSize && heapK[c + 1] < heapK[c]) c++;
                    if (heapK[c] >= lk) break;
                    heapN[i] = heapN[c]; heapK[i] = heapK[c];
                    i = c;
                }
                heapN[i] = ln; heapK[i] = lk;
            }
            return top;
        }

        /// <summary>Number of connected pieces (diagnostics).</summary>
        public int Components()
        {
            var comp = new int[Count];
            for (int i = 0; i < Count; i++) comp[i] = -1;
            int c = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < Count; s++)
            {
                if (comp[s] >= 0) continue;
                comp[s] = c;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int u = stack.Pop();
                    for (int e = adjStart[u]; e < adjStart[u + 1]; e++)
                        if (comp[adjTo[e]] < 0) { comp[adjTo[e]] = c; stack.Push(adjTo[e]); }
                }
                c++;
            }
            return c;
        }
    }

    /// <summary>The player's waypoint and the road route to it (shared by the world map and the minimap).</summary>
    public static class Gps
    {
        public static bool Active { get; private set; }
        public static Vector2 Waypoint { get; private set; }
        public static string WaypointName { get; private set; }
        /// <summary>Route polyline from the node where the car joins the roads to the waypoint (the car itself is not included).</summary>
        public static readonly List<Vector2> Path = new List<Vector2>();
        /// <summary>Metres left by road (including the bits off the road at both ends).</summary>
        public static float Remaining { get; private set; }
        public static bool HasRoute { get { return Active && Path.Count > 0; } }
        /// <summary>Raised when the car reaches the waypoint (the waypoint is cleared first).</summary>
        public static event System.Action Arrived;
        public const float ArriveDistance = 28f;

        static RoadGraph graph;
        static WorldField graphField;
        static int startNode = -1, targetNode = -1;
        static Vector2 lastPlayer;

        public static RoadGraph Graph
        {
            get
            {
                var f = WorldField.Active;
                if (f == null || !f.RoadsReady) return null;
                if (graph == null || graphField != f)
                {
                    graph = RoadGraph.Build(f.roadSamples, f.roads);
                    graphField = f;
                    targetNode = -1;
                }
                return graph;
            }
        }

        public static void Set(Vector2 p, string name)
        {
            Active = true;
            Waypoint = p;
            WaypointName = string.IsNullOrEmpty(name) ? "Waypoint" : name;
            startNode = -1;
            targetNode = -1;
            Path.Clear();
            var g = Graph;
            if (g == null) return;
            targetNode = g.Nearest(p, 600f);
            if (targetNode >= 0) g.SolveTo(targetNode);
        }

        public static void Clear()
        {
            Active = false;
            Path.Clear();
            startNode = -1;
            targetNode = -1;
            Remaining = 0f;
        }

        /// <summary>Call every frame with the car's position (x, z). Cheap: re-plans only when the car's join node changes.</summary>
        public static void Track(Vector2 player)
        {
            if (!Active) return;
            lastPlayer = player;
            if ((player - Waypoint).sqrMagnitude < ArriveDistance * ArriveDistance)
            {
                Clear();
                if (Arrived != null) Arrived();
                return;
            }
            var g = Graph;
            if (g == null) { Remaining = Vector2.Distance(player, Waypoint); return; }
            if (targetNode < 0 || g.Target != targetNode)
            {
                targetNode = g.Nearest(Waypoint, 600f);
                if (targetNode < 0) { Path.Clear(); Remaining = Vector2.Distance(player, Waypoint); return; }
                g.SolveTo(targetNode);
                startNode = -1;
            }
            int s = g.BestStart(player);
            if (s < 0) { Path.Clear(); Remaining = Vector2.Distance(player, Waypoint); return; }
            if (s != startNode)
            {
                startNode = s;
                Path.Clear();
                int guard = g.Count;
                for (int i = s; i >= 0 && guard-- > 0; i = g.Next[i]) Path.Add(g.Pos[i]);
                if ((g.Pos[targetNode] - Waypoint).sqrMagnitude > 64f) Path.Add(Waypoint);
            }
            Remaining = Vector2.Distance(player, g.Pos[s]) + g.Dist[s] + Vector2.Distance(g.Pos[targetNode], Waypoint);
        }

        /// <summary>Last position given to Track.</summary>
        public static Vector2 LastPlayer { get { return lastPlayer; } }

    }

    /// <summary>"350 m" / "1.4 km" label that only allocates a new string when the shown value changes.</summary>
    public sealed class DistText
    {
        int key = -1;
        string text = "";

        public string Get(float m)
        {
            int k = m < 1000f ? Mathf.RoundToInt(m / 10f) * 10 : 100000 + Mathf.RoundToInt(m / 100f);
            if (k == key) return text;
            key = k;
            text = m < 1000f ? k + " m" : (Mathf.RoundToInt(m / 100f) / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km";
            return text;
        }
    }
}

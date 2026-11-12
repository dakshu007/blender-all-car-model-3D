// RoadDetails.cs - generic roadside detail, added after the regions are built (so nothing is doubled up):
//   * guard rails (posts + W-beam) where the ground falls away beside the road (high fills, drop-offs),
//   * low dry-stone retaining walls at the foot of deep cuts,
//   * red / white kerbs on the inside of fast asphalt bends,
//   * gravel shoulders beside asphalt roads.
// Only on roads with RoadDef.autoDetails, never on bridges or across junction mouths, and only where no other
// collider, region prop or placement reservation lies within ~1.5 m. Everything is merged per 256 m chunk into a
// few vertex-coloured meshes; rails and walls get one merged MeshCollider per chunk (the only parts a car can hit).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public partial class WorldBuilder
    {
        sealed class DetailChunk
        {
            public readonly MeshAcc solid = new MeshAcc();      // rails, posts, walls (cast shadows)
            public readonly MeshAcc flat = new MeshAcc();       // kerbs and shoulders (no shadows)
            public readonly List<Vector3> cv = new List<Vector3>();
            public readonly List<int> ct = new List<int>();
        }

        readonly Dictionary<long, DetailChunk> detailChunks = new Dictionary<long, DetailChunk>();
        readonly Collider[] overlapBuf = new Collider[16];
        int railSegs, wallSegs, kerbSegs, shoulderSegs, skipped;

        DetailChunk ChunkAt(Vector2 p)
        {
            long k = WorldField.Key(Mathf.FloorToInt(p.x / 256f), Mathf.FloorToInt(p.y / 256f));
            DetailChunk c;
            if (!detailChunks.TryGetValue(k, out c)) { c = new DetailChunk(); detailChunks[k] = c; }
            return c;
        }

        float G(Vector2 p) { return TerrainHeight(p.x, p.y); }

        /// <summary>Nothing of the regions / valley within r of p (colliders other than terrain and roads, region props).</summary>
        bool SpotFree(Vector2 p, float y, float r)
        {
            if (!PlacementRegistry.IsFree(p, r * 0.6f)) return false;
            var reg = field.Dominant(p);
            if (reg != null && reg.Context != null && !reg.Context.Free(p, r * 0.6f)) return false;
            int n = Physics.OverlapSphereNonAlloc(new Vector3(p.x, y + 0.7f, p.y), r, overlapBuf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlapBuf[i];
                if (c == null || c.GetComponent<TerrainSurface>() != null) continue;
                if (roadsRoot != null && c.transform.IsChildOf(roadsRoot)) continue;
                Vector3 size = c.bounds.size;
                if (size.x > 60f || size.z > 60f) continue;
                return false;
            }
            return true;
        }

        /// <summary>True near a junction of this road (mouths stay open) or where another road is close.</summary>
        bool NearOtherRoad(int r, RoadSample s, Vector2 p)
        {
            foreach (var j in field.junctions)
            {
                if (j.slave != r && j.master != r) continue;
                float reach = roads[j.slave].width * 0.5f + roads[j.master].width * 0.5f + 8f;
                if ((p - j.point).sqrMagnitude < reach * reach) return true;
            }
            RoadSample o;
            float d = field.NearestRoad(p, out o, 20f);
            if (d < 20f && (o.road != r || Mathf.Abs(o.distance - s.distance) > 12f) && d < o.halfWidth + 2.5f) return true;
            return false;
        }

        IEnumerator BuildRoadDetails()
        {
            Physics.SyncTransforms();
            float t0 = Time.realtimeSinceStartup;
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                if (!def.autoDetails || def.fixedHeight) continue;
                var L = roadSamples[r];
                int n = L.Count;
                if (n < 12) continue;
                var k = WorldField.Curvature(L, def.closed);
                bool asphalt = def.surface == Surface.Asphalt;
                for (int side = -1; side <= 1; side += 2)
                {
                    var rail = new List<int>();
                    var wall = new List<int>();
                    var kerb = new List<int>();
                    for (int i = 0; i <= n; i++)
                    {
                        bool wantRail = false, wantWall = false, wantKerb = false;
                        if (i < n)
                        {
                            var s = L[i];
                            Vector2 rt = s.Right * side;
                            Vector2 edge = s.pos + rt * s.halfWidth;
                            bool ok = !s.bridge && (def.closed || (i > 3 && i < n - 4)) && !NearOtherRoad(r, s, edge + rt * 1.5f);
                            if (ok)
                            {
                                float surf = s.SurfaceAt(side * s.halfWidth);
                                float drop = surf - G(edge + rt * 5.5f), drop2 = surf - G(edge + rt * 9f);
                                bool going = rail.Count > 0;
                                wantRail = drop > (going ? 1.6f : 2.3f) || drop2 > (going ? 2.6f : 3.6f);
                                float rise = G(edge + rt * (WorldField.Shoulder + 3f)) - surf, rise2 = G(edge + rt * (WorldField.Shoulder + 1.2f)) - surf;
                                wantWall = !wantRail && rise > (wall.Count > 0 ? 1.4f : 2.2f) && rise2 > 0.5f;
                                float ak = Mathf.Abs(k[i]);
                                wantKerb = asphalt && !wantRail && ak > 1f / 90f && ak < 1f / 11f && side == (k[i] > 0f ? -1 : 1);
                                // shoulders: every asphalt road where nothing else is
                                if (asphalt && i < n - 1 && !wantKerb && !L[i + 1].bridge) Shoulder(s, L[i + 1], side);
                            }
                        }
                        Flush(rail, wantRail, 5, i, L, side, RailRun);
                        Flush(wall, wantWall, 4, i, L, side, WallRun);
                        Flush(kerb, wantKerb, 4, i, L, side, KerbRun);
                        if (i < n) { if (wantRail) rail.Add(i); if (wantWall) wall.Add(i); if (wantKerb) kerb.Add(i); }
                    }
                }
                if (Time.realtimeSinceStartup - t0 > 0.03f) { t0 = Time.realtimeSinceStartup; yield return null; }
            }
            FinishDetails();
            Debug.Log("[BlossomRally] road details: " + railSegs + " rail, " + wallSegs + " wall, " + kerbSegs + " kerb, " + shoulderSegs
                      + " shoulder segments (" + skipped + " spots left to the regions)");
        }

        delegate void RunBuilder(List<RoadSample> L, int i0, int i1, int side);

        static void Flush(List<int> run, bool want, int minLen, int i, List<RoadSample> L, int side, RunBuilder build)
        {
            if (want || run.Count == 0) return;
            if (run.Count >= minLen) build(L, run[0], run[run.Count - 1], side);
            run.Clear();
        }

        // ------------------------------------------------------------------ guard rail
        void RailRun(List<RoadSample> L, int i0, int i1, int side)
        {
            int n = L.Count;
            i0 = Mathf.Max(0, i0 - 2); i1 = Mathf.Min(n - 1, i1 + 2);
            Color32 steel = new Color32(176, 182, 188, 0), steelDark = new Color32(120, 126, 132, 0), post = new Color32(92, 96, 100, 0);
            Color32 red = new Color32(200, 40, 36, 0);
            Vector3 prev = Vector3.zero;
            bool have = false;
            for (int i = i0; i <= i1; i++)
            {
                var s = L[i];
                Vector2 rt = s.Right * side;
                Vector2 p = s.pos + rt * (s.halfWidth + 0.9f);
                float top = s.SurfaceAt(side * s.halfWidth) + 0.78f;
                Vector3 cur = new Vector3(p.x, top, p.y);
                if (!SpotFree(p, top - 0.8f, 1.5f)) { skipped++; have = false; continue; }
                var ch = ChunkAt(p);
                if ((i - i0) % 2 == 0)
                {
                    float gy = G(p) - 0.25f;
                    var fwd = new Vector3(s.tangent.x, 0f, s.tangent.y);
                    ch.solid.Box(new Vector3(p.x, gy, p.y), fwd, new Vector3(0.14f, top - gy - 0.02f, 0.12f), post, post);
                    if ((i - i0) % 8 == 0) ch.solid.Box(new Vector3(p.x, top - 0.16f, p.y) - new Vector3(rt.x, 0f, rt.y) * 0.08f, fwd, new Vector3(0.06f, 0.08f, 0.1f), red, red);
                    PlacementRegistry.Reserve(p, 0.8f);
                }
                if (have)
                {
                    Vector3 a = prev, b = cur;
                    Vector3 dir = b - a; dir.y = 0f;
                    float len = dir.magnitude;
                    if (len > 0.05f && len < 4f)
                    {
                        Vector3 o = Vector3.Cross(Vector3.up, dir / len) * (side * -0.07f);
                        // W-beam: a front face, a top lip and a shaded lower band
                        ch.solid.Quad(a + o + Vector3.down * 0.32f, a + o, b + o, b + o + Vector3.down * 0.32f, o, steelDark, steel);
                        ch.solid.Quad(a + o, a - o, b - o, b + o, Vector3.up, steel, steel);
                        ch.solid.Quad(a - o + Vector3.down * 0.32f, a - o, b - o, b - o + Vector3.down * 0.32f, -o, steelDark, steelDark);
                        AddColliderBox(ch, (a + b) * 0.5f + Vector3.down * 0.3f, dir / len, new Vector3(0.3f, 1.2f, len + 0.1f));
                        railSegs++;
                    }
                }
                prev = cur; have = true;
            }
        }

        // ------------------------------------------------------------------ retaining wall
        void WallRun(List<RoadSample> L, int i0, int i1, int side)
        {
            int n = L.Count;
            i0 = Mathf.Max(0, i0 - 1); i1 = Mathf.Min(n - 1, i1 + 1);
            Color32 cope = Stone(1.12f, 0.5f);
            for (int i = i0; i < i1; i++)
            {
                var s0 = L[i]; var s1 = L[i + 1];
                float off = WorldField.Shoulder - 0.2f;
                Vector2 p0 = s0.pos + s0.Right * side * (s0.halfWidth + off), p1 = s1.pos + s1.Right * side * (s1.halfWidth + off);
                Vector2 mid = (p0 + p1) * 0.5f;
                float g0 = G(p0), g1 = G(p1);
                // wall height: up to where the slope is 1.5 m behind it, 0.9 .. 1.8 m
                Vector2 back = s0.Right * side * 1.6f;
                float h0 = Mathf.Clamp(G(p0 + back) - g0, 0.9f, 1.8f), h1 = Mathf.Clamp(G(p1 + back) - g1, 0.9f, 1.8f);
                if (!SpotFree(mid, (g0 + g1) * 0.5f, 1.2f)) { skipped++; continue; }
                var ch = ChunkAt(mid);
                Vector3 a = new Vector3(p0.x, g0 - 0.3f, p0.y), b = new Vector3(p1.x, g1 - 0.3f, p1.y);
                Vector3 ta = new Vector3(p0.x, g0 + h0, p0.y), tb = new Vector3(p1.x, g1 + h1, p1.y);
                Vector3 outw = new Vector3(-s0.Right.x * side, 0f, -s0.Right.y * side);          // faces the road
                Vector3 th = -outw * 0.55f;
                Color32 c0 = Stone(0.92f, WorldBuilderHash(i, side)), c1 = Stone(1.02f, WorldBuilderHash(i + 7, side));
                // slightly battered face, coping slab on top
                ch.solid.Quad(a, ta + th * 0.15f, tb + th * 0.15f, b, outw, c0, c1);
                ch.solid.Quad(ta + th * 0.15f - outw * 0.06f, ta + th, tb + th, tb + th * 0.15f - outw * 0.06f, Vector3.up, cope, cope);
                // vertical joints every segment so it reads as dressed stone
                if (i % 2 == 0)
                    ch.solid.Box(new Vector3(p0.x, g0 + h0 * 0.48f, p0.y) - th * 0.02f, new Vector3(s0.tangent.x, 0f, s0.tangent.y), new Vector3(0.6f, 0.05f, 0.06f), Stone(0.7f, 0.3f), Stone(0.75f, 0.3f));
                Vector3 dir = b - a; dir.y = 0f;
                float len = dir.magnitude;
                if (len > 0.05f)
                    AddColliderBox(ch, new Vector3(mid.x, (g0 + g1) * 0.5f + 0.5f, mid.y) + th * 0.5f, dir / len, new Vector3(0.6f, Mathf.Max(h0, h1) + 1f, len + 0.1f));
                PlacementRegistry.Reserve(mid, 0.9f);
                wallSegs++;
            }
        }

        static float WorldBuilderHash(int a, int b) { return Hash(a, b); }

        // ------------------------------------------------------------------ kerbs
        void KerbRun(List<RoadSample> L, int i0, int i1, int side)
        {
            Color32 red = new Color32(206, 44, 40, 0), white = new Color32(240, 238, 232, 0), redS = new Color32(150, 30, 28, 0), whiteS = new Color32(185, 183, 178, 0);
            for (int i = i0; i < i1; i++)
            {
                var s0 = L[i]; var s1 = L[i + 1];
                Vector2 r0 = s0.Right * side, r1 = s1.Right * side;
                Vector2 in0 = s0.pos + r0 * (s0.halfWidth - 0.55f), in1 = s1.pos + r1 * (s1.halfWidth - 0.55f);
                Vector2 out0 = s0.pos + r0 * (s0.halfWidth + 0.45f), out1 = s1.pos + r1 * (s1.halfWidth + 0.45f);
                if (!SpotFree((out0 + out1) * 0.5f, s0.height, 1.0f)) { skipped++; continue; }
                float yi0 = s0.SurfaceAt(side * (s0.halfWidth - 0.55f)) + 0.025f, yi1 = s1.SurfaceAt(side * (s1.halfWidth - 0.55f)) + 0.025f;
                float yo0 = s0.SurfaceAt(side * s0.halfWidth) + 0.06f, yo1 = s1.SurfaceAt(side * s1.halfWidth) + 0.06f;
                bool r = ((int)(s0.distance / 2f)) % 2 == 0;
                Color32 c = r ? red : white, cs = r ? redS : whiteS;
                var ch = ChunkAt(out0);
                Vector3 A = new Vector3(in0.x, yi0, in0.y), B = new Vector3(in1.x, yi1, in1.y), C = new Vector3(out1.x, yo1, out1.y), D = new Vector3(out0.x, yo0, out0.y);
                ch.flat.Quad(A, B, C, D, Vector3.up, c, c);
                // outer face down into the ground
                float g0 = G(out0) - 0.1f, g1 = G(out1) - 0.1f;
                ch.flat.Quad(new Vector3(out0.x, Mathf.Min(g0, yo0 - 0.15f), out0.y), D, C, new Vector3(out1.x, Mathf.Min(g1, yo1 - 0.15f), out1.y),
                             new Vector3(r0.x, 0f, r0.y), cs, cs);
                kerbSegs++;
            }
        }

        // ------------------------------------------------------------------ gravel shoulder
        void Shoulder(RoadSample s0, RoadSample s1, int side)
        {
            Vector2 r0 = s0.Right * side, r1 = s1.Right * side;
            Vector2 a0 = s0.pos + r0 * (s0.halfWidth + 0.2f), a1 = s1.pos + r1 * (s1.halfWidth + 0.2f);
            Vector2 b0 = s0.pos + r0 * (s0.halfWidth + 1.25f), b1 = s1.pos + r1 * (s1.halfWidth + 1.25f);
            if (Vector2.Dot(a1 - a0, s0.tangent) < 0.2f || Vector2.Dot(b1 - b0, s0.tangent) < 0.2f) return;   // tight inside of a bend
            var ch = ChunkAt(a0);
            Color32 g = GravelColor(a0, s0.distance);
            Color32 gEdge = GravelColor(b0, s0.distance + 1f);
            gEdge.r = (byte)(gEdge.r * 0.92f); gEdge.g = (byte)(gEdge.g * 0.92f); gEdge.b = (byte)(gEdge.b * 0.92f);
            ch.flat.Quad(new Vector3(b0.x, G(b0) + 0.05f, b0.y), new Vector3(a0.x, G(a0) + 0.05f, a0.y), new Vector3(a1.x, G(a1) + 0.05f, a1.y), new Vector3(b1.x, G(b1) + 0.05f, b1.y),
                         Vector3.up, gEdge, g);
            shoulderSegs++;
        }

        Color32 GravelColor(Vector2 p, float d)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt((p.x - terrX0) / TerrainCell), 0, terrNx - 1), j = Mathf.Clamp(Mathf.RoundToInt((p.y - terrZ0) / TerrainCell), 0, terrNz - 1);
            Color32 ground = terrCol != null ? terrCol[j * terrNx + i] : new Color32(140, 150, 110, 0);
            float n = 0.92f + 0.12f * Hash(Mathf.FloorToInt(d), 3);
            Color gravel = new Color(0.62f, 0.58f, 0.52f) * n;
            Color c = Color.Lerp(gravel, (Color)ground, 0.35f);
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 0);
        }

        // ------------------------------------------------------------------ assembly
        static void AddColliderBox(DetailChunk ch, Vector3 centre, Vector3 fwd, Vector3 size)
        {
            Vector3 f = fwd * size.z * 0.5f, r = Vector3.Cross(Vector3.up, fwd) * size.x * 0.5f, u = Vector3.up * size.y * 0.5f;
            int b = ch.cv.Count;
            for (int k = 0; k < 8; k++)
                ch.cv.Add(centre + ((k & 1) != 0 ? r : -r) + ((k & 2) != 0 ? u : -u) + ((k & 4) != 0 ? f : -f));
            int[] q = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
            for (int i = 0; i < 24; i += 4)
            {
                ch.ct.Add(b + q[i]); ch.ct.Add(b + q[i + 1]); ch.ct.Add(b + q[i + 2]);
                ch.ct.Add(b + q[i]); ch.ct.Add(b + q[i + 2]); ch.ct.Add(b + q[i + 3]);
            }
        }

        void FinishDetails()
        {
            var root = new GameObject("World_RoadDetails").transform;
            root.SetParent(staticRoot, false);
            foreach (var kv in detailChunks)
            {
                var ch = kv.Value;
                MakeDetailMesh(root, "RoadSolid", ch.solid, true);
                MakeDetailMesh(root, "RoadFlat", ch.flat, false);
                if (ch.cv.Count > 0)
                {
                    var m = new Mesh { name = "RoadDetailCol" };
                    if (ch.cv.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                    m.SetVertices(ch.cv);
                    m.SetTriangles(ch.ct, 0);
                    m.RecalculateBounds();
                    var go = new GameObject("RoadDetailColliders");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshCollider>().sharedMesh = m;
                }
            }
            detailChunks.Clear();
        }

        static void MakeDetailMesh(Transform root, string name, MeshAcc acc, bool shadows)
        {
            if (acc.t.Count == 0) return;
            var m = new Mesh { name = name };
            if (acc.v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(acc.v);
            m.SetColors(acc.c);
            m.SetTriangles(acc.t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Terrain;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }
    }
}

// WorldProps.Nature.cs - nature placement: water wheels on the river, terraced rice paddies with seedlings,
// brooks with stepping stones and mini waterfalls, willows by the lake and river, pines on the mountain and
// rim hills, plum / peach blossom trees among the cherries, rapeseed fields, karst pillars beyond the rim,
// light shafts in the bamboo forest and the spots where butterflies / dragonflies / fireflies appear.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public partial class WorldProps
    {
        class WheelInfo { public Vector3 hub; public float yaw, spin, angle; public int part; }
        readonly List<WheelInfo> wheels = new List<WheelInfo>();
        const float WheelR = 3f;

        void Tree(PropModel m, Vector2 p, float trunkR, float trunkH, string key)
        {
            Vector3 pos = V3(p, Ground(p) - 0.05f);
            meshes.Stamp(m, pos, Rand(0f, 360f));
            CapCol(pos, trunkR, trunkH);
            Count(key);
        }

        static bool OpenLand(Vector2 p)
        {
            return !MapData.InBambooForest(p) && !MapData.InTeaTerraces(p) && !MapData.InVillage(p);
        }

        // ------------------------------------------------------------------ water wheels
        void BuildWaterWheels()
        {
            int last = -99;
            for (int i = 3; i < river.Count - 3 && wheels.Count < 3; i++)
            {
                if (i - last < 12) continue;
                Vector2 c = river[i];
                if (c.y > 250f || c.y < -205f) continue;
                Vector2 t = (river[i + 1] - river[i - 1]).normalized;
                Vector2 n = new Vector2(t.y, -t.x);
                if (n.x < 0f) n = -n;                                   // towards the paddies on the east bank
                Vector2 hub2 = c + n * 9.5f, land = c + n * 17.5f;
                if (EdgeDist(hub2) < 14f || EdgeDist(land) < 8f || MapData.LakeK(land) < 1.6f) continue;
                if (!PlacementRegistry.IsFree(land, 3f)) continue;
                bool nearBridge = false;
                foreach (var bm in world.BridgeMarks) if ((new Vector2(bm.x, bm.z) - c).sqrMagnitude < 45f * 45f) nearBridge = true;
                if (nearBridge) continue;
                float hubY = WorldBuilder.WaterY + WheelR - 0.55f;
                float down = Mathf.Clamp(hubY - Ground(hub2) + 0.3f, 2.5f, 6.5f);
                float drop = Mathf.Clamp(hubY + WheelR + 0.25f - (Ground(land) + 1.6f), 0.3f, 1.8f);
                float yaw = Mathf.Atan2(-n.y, n.x) * Mathf.Rad2Deg;
                Vector3 hub = V3(hub2, hubY);
                meshes.Stamp(PropShapes.WheelFrame(WheelR, down, 8f, drop), hub, yaw);
                Vector2 localZ = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                float spin = (Vector2.Dot(t, localZ) > 0f ? -1f : 1f) * Rand(24f, 32f);
                wheels.Add(new WheelInfo { hub = hub, yaw = yaw, spin = spin, angle = Rand(0f, 360f) });
                PlacementRegistry.Reserve(land, 3f);
                fx.water.Add(V3(c + n * 11f, WorldBuilder.WaterY + 0.1f));
                Count("water wheels");
                last = i;
            }
        }

        // ------------------------------------------------------------------ rice paddies
        void AddBlade(PropBucket b, Vector3 root, float h, float w, Color32 cRoot, Color32 cTip)
        {
            float yaw = Rand(0f, Mathf.PI * 2f);
            Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (w * 0.5f);
            Vector3 lean = new Vector3(Rand(-0.3f, 0.3f), 0f, Rand(-0.3f, 0.3f)) * h;
            Vector3 nrm = (Vector3.up + lean * 0.4f).normalized;
            Vector4 uv = new Vector4(root.x, root.y, root.z, (float)rng.NextDouble());
            cRoot.a = 0; cTip.a = 255;
            int i0 = b.Vert(root - side, nrm, cRoot, uv);
            b.Vert(root + lean + Vector3.up * h, nrm, cTip, uv);
            b.Vert(root + side, nrm, cRoot, uv);
            b.Tri(i0, i0 + 1, i0 + 2);
        }

        void BuildPaddies()
        {
            Vector2[] targets = { new Vector2(122f, 68f), new Vector2(128f, -92f), new Vector2(72f, 186f), new Vector2(150f, 160f) };
            const float hx = 7f, hz = 4.5f, sx = 15f, sz = 10f;
            int clusters = 0, plots = 0;
            foreach (var t in targets)
            {
                if (clusters >= 3) break;
                float yaw = Rand(-28f, 28f);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                var ok = new List<Vector4>();   // x, z, min, max
                for (int ix = -2; ix <= 1; ix++)
                    for (int iz = -1; iz <= 1; iz++)
                    {
                        Vector3 l = q * new Vector3((ix + 0.5f) * sx, 0f, iz * sz);
                        Vector2 p = t + new Vector2(l.x, l.z);
                        if (!OpenLand(p) || !PlacementRegistry.CanPlace(world, p, 8.6f, 3f)) continue;
                        float mn = float.MaxValue, mx = float.MinValue;
                        for (float x = -hx - 0.5f; x <= hx + 0.51f; x += 1.5f)
                            for (float z = -hz - 0.5f; z <= hz + 0.51f; z += 1.25f)
                            {
                                Vector3 w = q * new Vector3(x, 0f, z);
                                float h = Ground(p.x + w.x, p.y + w.z);
                                if (h < mn) mn = h;
                                if (h > mx) mx = h;
                            }
                        if (mx - mn > 1.3f) continue;
                        ok.Add(new Vector4(p.x, p.y, mn, mx));
                    }
                if (ok.Count < 4) continue;
                clusters++;
                for (int i = 0; i < ok.Count; i++)
                {
                    Paddy(new Vector2(ok[i].x, ok[i].y), yaw, ok[i].z, ok[i].w, hx, hz, plots++);
                    PlacementRegistry.Reserve(new Vector2(ok[i].x, ok[i].y), 8.6f);
                }
                // a scarecrow on a bund and a farm cart at the edge of the fields
                Vector3 sc = V3(new Vector2(ok[0].x, ok[0].y), ok[0].w + 0.19f) + q * new Vector3(hx + 0.25f, 0f, hz + 0.25f);
                meshes.Stamp(PropShapes.Scarecrow(rng), sc, Rand(0f, 360f));
                Count("scarecrows");
                PlaceModel(PropShapes.Cart(clusters % 2, rng), t + new Vector2(Rand(-8f, 8f), -22f), 30f, 2.2f, 2f, Rand(0f, 360f),
                           new Vector3(1.8f, 1.5f, 3.8f), new Vector3(0f, 0.75f, 0.75f), "carts");
            }
        }

        void Paddy(Vector2 c, float yaw, float mn, float mx, float hx, float hz, int index)
        {
            float L = mx + 0.05f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pos = V3(c, L);
            var m = new PropModel();
            var g = m[PK.Props];
            float bot = mn - L - 0.3f, top = 0.14f, cy = (top + bot) * 0.5f, hy = (top - bot) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, new Vector3(0f, cy, s * (hz + 0.25f)), new Vector3(hx + 0.5f, hy, 0.25f), Pal.Earth, Pal.EarthGrass);
                PG.Box2(g, new Vector3(s * (hx + 0.25f), cy, 0f), new Vector3(0.25f, hy, hz), Pal.Earth, Pal.EarthGrass);
            }
            meshes.Stamp(m, pos, yaw);
            var wb = meshes.Get(PK.Water, pos);
            Vector3[] cs = { new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz) };
            int v0 = -1;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = pos + q * cs[i];
                int vi = wb.Vert(p, Vector3.up, new Color32(255, 255, 255, 255), new Vector4(p.x * 0.05f, p.z * 0.05f, 0f, 0f));
                if (i == 0) v0 = vi;
            }
            wb.Tri(v0, v0 + 1, v0 + 2);
            wb.Tri(v0, v0 + 2, v0 + 3);
            if (index % 4 != 3)
            {
                // neat rows of seedlings (BR_Grass vertex format: they sway and fade with distance)
                float rowStep = new[] { 0.85f, 0.65f, 0.5f, 0.45f }[GameSettings.Quality];
                bool tall = index % 3 == 1;
                Color32 cr = LifeGeo.C(64, 106, 50), ct = tall ? LifeGeo.C(170, 206, 84) : LifeGeo.C(150, 208, 96);
                var bb = meshes.Get(PK.Blades, pos);
                for (float z = -hz + 0.4f; z <= hz - 0.4f; z += rowStep)
                    for (float x = -hx + 0.4f; x <= hx - 0.4f; x += 0.36f)
                    {
                        Vector3 root = pos + q * new Vector3(x + Rand(-0.05f, 0.05f), -0.06f, z + Rand(-0.04f, 0.04f));
                        float h = tall ? Rand(0.4f, 0.6f) : Rand(0.24f, 0.4f);
                        AddBlade(bb, root, h, 0.05f, cr, ct);
                        AddBlade(bb, root, h * 0.85f, 0.05f, cr, ct);
                    }
            }
            if (index % 3 == 0) fx.water.Add(pos + Vector3.up * 0.05f);
            if (index % 2 == 0) fx.meadow.Add(pos + Vector3.up * 0.1f);
            Count("rice paddies");
        }

        // ------------------------------------------------------------------ brooks, stepping stones, mini waterfalls
        Vector2 NearestRiver(Vector2 p)
        {
            Vector2 best = river[0];
            float bd = float.MaxValue;
            for (int i = 0; i < river.Count; i++)
            {
                float d = (river[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = river[i]; }
            }
            return best;
        }

        List<Vector2> TraceStream(Vector2 start)
        {
            var pts = new List<Vector2>();
            Vector2 p = start, dir = (NearestRiver(p) - p).normalized;
            int mask = ~((1 << WorldBuilder.LayerGrass) | (1 << CarExtras.CarLayer));
            for (int step = 0; step < 240; step++)
            {
                if (world.RiverDistance(p) < 12.5f || Ground(p) < WorldBuilder.WaterY + 0.12f) { pts.Add(p); return pts; }
                if (EdgeDist(p) < 4f || MapData.LakeK(p) < 1.35f || MapData.InVillage(p) || Mathf.Abs(p.x) > 468f || Mathf.Abs(p.y) > 468f) return null;
                if ((step & 1) == 0 && Physics.CheckSphere(V3(p, Ground(p) + 1.4f), 0.9f, mask, QueryTriggerInteraction.Ignore)) return null;
                pts.Add(p);
                Vector2 g = new Vector2(Ground(p.x + 1.5f, p.y) - Ground(p.x - 1.5f, p.y), Ground(p.x, p.y + 1.5f) - Ground(p.x, p.y - 1.5f)) / 3f;
                float gm = g.magnitude;
                Vector2 down = gm > 1e-4f ? -g / gm : dir;
                Vector2 toRiver = (NearestRiver(p) - p).normalized;
                float w = Mathf.Clamp(gm * 12f, 0.25f, 0.85f);
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 nd = (down * w + toRiver * (1f - w) + perp * (Mathf.Sin(step * 0.35f) * 0.25f)).normalized;
                dir = (dir * 0.5f + nd * 0.5f).normalized;
                p += dir * 1.6f;
            }
            return null;
        }

        void BuildStreams()
        {
            Physics.SyncTransforms();
            Vector2[] starts = { new Vector2(58f, 442f), new Vector2(-112f, 438f), new Vector2(150f, 246f), new Vector2(28f, 410f), new Vector2(-150f, 240f), new Vector2(120f, -30f) };
            int built = 0;
            foreach (var s in starts)
            {
                if (built >= 3) break;
                var path = TraceStream(s);
                if (path == null || path.Count < 22) continue;
                Stream(path);
                built++;
            }
        }

        void Stream(List<Vector2> path)
        {
            int n = path.Count;
            var sb = meshes.Get(PK.Stream, V3(path[0], 0f));
            var rocks = new PropModel();
            var rg = rocks[PK.Props];
            var ys = new float[n];
            var nr = new Vector2[n];
            float dist = 0f;
            int prev = -1;
            Color32 edge = new Color32(255, 255, 255, 80), mid = new Color32(255, 255, 255, 215);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = path[i];
                Vector2 t = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector2 nrm = new Vector2(t.y, -t.x);
                nr[i] = nrm;
                float hw = 0.6f + 0.2f * Mathf.Sin(i * 0.4f) + 0.4f * i / n;
                Vector2 l = p - nrm * hw, r = p + nrm * hw;
                float y = Mathf.Max(Ground(l), Mathf.Max(Ground(p), Ground(r))) + 0.09f;
                ys[i] = y;
                if (i > 0) dist += Vector2.Distance(path[i], path[i - 1]);
                float v = dist * 0.5f;
                int a = sb.Vert(V3(l, y), Vector3.up, edge, new Vector4(0f, v, 0f, 0f));
                sb.Vert(V3(p, y), Vector3.up, mid, new Vector4(0.5f, v, 0f, 0f));
                sb.Vert(V3(r, y), Vector3.up, edge, new Vector4(1f, v, 0f, 0f));
                if (prev >= 0)
                {
                    sb.Tri(prev, a, a + 1); sb.Tri(prev, a + 1, prev + 1);
                    sb.Tri(prev + 1, a + 1, a + 2); sb.Tri(prev + 1, a + 2, prev + 2);
                }
                prev = a;
                if (i % 3 == 0) PlacementRegistry.Reserve(p, 1.6f);
                if (i % 12 == 6) fx.water.Add(V3(p, y + 0.1f));
                if (i % 5 == 2)   // pebbles along the banks
                {
                    Vector2 bp = p + nrm * (hw + 0.25f) * ((i & 1) == 0 ? 1f : -1f);
                    PG.Boulder(rg, V3(bp, Ground(bp) + 0.08f), new Vector3(Rand(0.2f, 0.4f), Rand(0.14f, 0.25f), Rand(0.2f, 0.36f)), Pal.StoneLight, Pal.StoneDark, i);
                }
            }
            // mini waterfalls where the brook drops quickly: white water + a rocky lip
            int falls = 0, lastFall = -99;
            for (int i = 2; i < n - 2 && falls < 3; i++)
            {
                if (i - lastFall < 8 || ys[i - 2] - ys[i] < 0.5f) continue;
                Color32 white = new Color32(255, 255, 255, 245);
                int pv = -1;
                for (int k = i - 2; k <= i; k++)
                {
                    Vector2 p = path[k];
                    float y = ys[k] + 0.07f, v = k * 1.7f;
                    int a = sb.Vert(V3(p - nr[k] * 0.55f, y), Vector3.up, white, new Vector4(0.1f, v, 0f, 0f));
                    sb.Vert(V3(p + nr[k] * 0.55f, y), Vector3.up, white, new Vector4(0.9f, v, 0f, 0f));
                    if (pv >= 0) { sb.Tri(pv, a, a + 1); sb.Tri(pv, a + 1, pv + 1); }
                    pv = a;
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 bp = path[i - 1] + nr[i - 1] * s * 1.25f;
                    PG.Boulder(rg, V3(bp, Ground(bp) + 0.2f), new Vector3(Rand(0.5f, 0.8f), Rand(0.4f, 0.6f), Rand(0.5f, 0.7f)), Pal.Stone, Pal.StoneDark, i * 3f + s);
                    Vector2 bp2 = path[i - 2] + nr[i - 2] * s * 1.0f;
                    PG.Boulder(rg, V3(bp2, Ground(bp2) + 0.15f), new Vector3(Rand(0.35f, 0.55f), Rand(0.3f, 0.45f), Rand(0.35f, 0.5f)), Pal.StoneLight, Pal.StoneDark, i * 5f + s);
                }
                falls++; lastFall = i;
                Count("mini waterfalls");
            }
            // stepping stones on the gentle stretches
            foreach (float f in new[] { 0.3f, 0.68f })
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(n * f), 3, n - 4);
                if (Mathf.Abs(ys[i + 2] - ys[i - 2]) > 0.4f) continue;
                for (int k = -2; k <= 2; k++)
                {
                    Vector2 sp = path[i] + nr[i] * (k * 0.58f) + new Vector2(Rand(-0.06f, 0.06f), Rand(-0.06f, 0.06f));
                    PG.Boulder(rg, V3(sp, ys[i] + 0.02f), new Vector3(Rand(0.24f, 0.3f), 0.1f, Rand(0.2f, 0.26f)), Pal.StoneLight, Pal.Stone, k + i);
                }
                Count("stepping-stone crossings");
            }
            meshes.Stamp(rocks, Matrix4x4.identity, V3(path[0], 0f));
            Count("brooks");
        }

        // ------------------------------------------------------------------ willows
        void BuildWillows()
        {
            Vector2 lc = MapData.LakeCenter, lr = MapData.LakeRadii;
            int n = 0;
            for (int i = 0; i < 18 && n < 11; i++)
            {
                float a = i * Mathf.PI * 2f / 18f + Rand(-0.1f, 0.1f), k = Rand(1.36f, 1.6f);
                Vector2 p = lc + new Vector2(Mathf.Cos(a) * lr.x * k, Mathf.Sin(a) * lr.y * k);
                if (!LifeShapes.ClearOfPavilion(p, 5f) || !PlacementRegistry.TryReserve(world, p, 3f, 2.5f)) continue;
                Tree(NatureShapes.Willow(rng), p, 0.42f, 4.5f, "willow trees");
                n++;
            }
            int m = 0;
            for (int i = 5; i < river.Count - 5 && m < 8; i += 8)
            {
                Vector2 t = (river[i + 1] - river[i - 1]).normalized;
                Vector2 nrm = new Vector2(t.y, -t.x) * ((i & 8) == 0 ? 1f : -1f);
                Vector2 p = river[i] + nrm * Rand(18.5f, 21f);
                if (!PlacementRegistry.TryReserve(world, p, 3f, 3f)) continue;
                Tree(NatureShapes.Willow(rng), p, 0.42f, 4.5f, "willow trees");
                m++;
            }
        }

        // ------------------------------------------------------------------ pines on the mountain and the rim hills
        void BuildPines()
        {
            Vector2 mc = MapData.MountainCenter;
            int n = 0;
            for (int tries = 0; tries < 1200 && n < 26; tries++)
            {
                float a = Rand(0f, Mathf.PI * 2f), r = Rand(30f, 180f);
                Vector2 p = mc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (p.y < 300f || !OpenLand(p) || Ground(p) < 5f) continue;
                if (!PlacementRegistry.TryReserve(world, p, 2.8f, 3f)) continue;
                Tree(NatureShapes.Pine(rng, Rand(0.85f, 1.25f)), p, 0.35f, 5f, "pine trees");
                n++;
            }
            int e = 0;
            for (int tries = 0; tries < 1200 && e < 14; tries++)
            {
                float band = Rand(430f, 468f) * (rng.NextDouble() < 0.5 ? 1f : -1f), other = Rand(-460f, 460f);
                Vector2 p = rng.NextDouble() < 0.5 ? new Vector2(band, other) : new Vector2(other, band);
                if (!OpenLand(p)) continue;
                if (!PlacementRegistry.TryReserve(world, p, 2.8f, 3f)) continue;
                Tree(NatureShapes.Pine(rng, Rand(0.9f, 1.3f)), p, 0.35f, 5f, "pine trees");
                e++;
            }
        }

        // ------------------------------------------------------------------ plum & peach among the cherries
        void BuildBlossomTrees()
        {
            var cherries = new List<Vector2>();
            var root = GameObject.Find("World_Static");
            if (root != null)
            {
                Transform rt = root.transform;
                for (int i = 0; i < rt.childCount; i++)
                {
                    Transform ch = rt.GetChild(i);
                    if (ch.name.StartsWith("ENV_Cherry")) cherries.Add(new Vector2(ch.position.x, ch.position.z));
                }
            }
            int n = 0;
            for (int tries = 0; tries < 2500 && n < 36; tries++)
            {
                Vector2 p;
                if (cherries.Count > 0 && rng.NextDouble() < 0.65)
                {
                    float a = Rand(0f, Mathf.PI * 2f);
                    p = cherries[rng.Next(cherries.Count)] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(9f, 18f);
                }
                else p = new Vector2(Rand(-450f, 450f), Rand(-440f, 440f));
                if (MapData.InBambooForest(p) || MapData.InTeaTerraces(p)) continue;
                if (!PlacementRegistry.TryReserve(world, p, 4f, 3.5f)) continue;
                bool peach = (n & 1) == 0;
                Tree(NatureShapes.BlossomTree(peach, rng), p, 0.3f, 4f, peach ? "peach trees" : "plum trees");
                if (n % 2 == 0) fx.flowers.Add(V3(p, Ground(p) + 1.5f));
                n++;
            }
        }

        // ------------------------------------------------------------------ rapeseed fields
        void BuildFlowerFields()
        {
            int n = 0;
            for (int tries = 0; tries < 900 && n < 6; tries++)
            {
                Vector2 p = new Vector2(Rand(-430f, 430f), Rand(-420f, 430f));
                if (!OpenLand(p) || MapData.LakeK(p) < 1.7f) continue;
                float ed = EdgeDist(p);
                if (ed < 7f || ed > 46f) continue;
                float R = Rand(9f, 15f), mn, mx;
                Spread(p, R, out mn, out mx);
                if (mx - mn > 3.5f) continue;
                if (!PlacementRegistry.TryReserve(world, p, R * 0.75f, 2f)) continue;
                Rapeseed(p, R);
                n++;
            }
        }

        void Rapeseed(Vector2 c, float R)
        {
            float s1 = Rand(0f, 6f), s2 = Rand(0f, 6f);
            System.Func<Vector2, bool> inside = q =>
            {
                Vector2 d = q - c;
                float a = Mathf.Atan2(d.y, d.x);
                return d.magnitude < R * (1f + 0.2f * Mathf.Sin(3f * a + s1) + 0.1f * Mathf.Sin(7f * a + s2));
            };
            Vector3 at = V3(c, 0f);
            var pb = meshes.Get(PK.Props, at);
            const float step = 1.5f;
            int cells = Mathf.CeilToInt(R * 1.35f / step);
            Color32[] ycol = { LifeGeo.C(246, 214, 56), LifeGeo.C(236, 200, 44), LifeGeo.C(250, 226, 84), LifeGeo.C(206, 200, 70) };
            for (int ix = -cells; ix < cells; ix++)
                for (int iz = -cells; iz < cells; iz++)
                {
                    Vector2 a = c + new Vector2(ix, iz) * step, b = a + new Vector2(step, 0f), d = a + new Vector2(step, step), e = a + new Vector2(0f, step);
                    if (!inside(a) || !inside(b) || !inside(d) || !inside(e)) continue;
                    Vector2 mid = (a + d) * 0.5f;
                    if (EdgeDist(mid) < 1.6f || world.RiverDistance(mid) < 14f) continue;
                    Color32 col = ycol[((ix * 7 + iz * 13) & 0x7fffffff) % ycol.Length];
                    int v0 = pb.Vert(V3(a, Ground(a) + 0.06f), Vector3.up, col, Vector4.zero);
                    pb.Vert(V3(e, Ground(e) + 0.06f), Vector3.up, col, Vector4.zero);
                    pb.Vert(V3(d, Ground(d) + 0.06f), Vector3.up, col, Vector4.zero);
                    pb.Vert(V3(b, Ground(b) + 0.06f), Vector3.up, col, Vector4.zero);
                    pb.Tri(v0, v0 + 1, v0 + 2);
                    pb.Tri(v0, v0 + 2, v0 + 3);
                }
            var bb = meshes.Get(PK.Blades, at);
            float density = new[] { 1.2f, 1.8f, 2.6f, 3.2f }[GameSettings.Quality];
            int tufts = Mathf.RoundToInt(Mathf.PI * R * R * density);
            Color32 cr = LifeGeo.C(140, 168, 60), ct = LifeGeo.C(250, 216, 56), ct2 = LifeGeo.C(255, 238, 112);
            for (int i = 0; i < tufts; i++)
            {
                Vector2 p = c + new Vector2(Rand(-R, R), Rand(-R, R)) * 1.25f;
                if (!inside(p) || EdgeDist(p) < 1.8f) continue;
                Vector3 root = V3(p, Ground(p) - 0.02f);
                AddBlade(bb, root, Rand(0.5f, 0.95f), Rand(0.09f, 0.14f), cr, (i & 3) == 0 ? ct2 : ct);
                AddBlade(bb, root, Rand(0.4f, 0.7f), Rand(0.08f, 0.12f), cr, ct);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = c + new Vector2(Rand(-R, R), Rand(-R, R)) * 0.6f;
                fx.flowers.Add(V3(p, Ground(p) + 0.6f));
            }
            fx.meadow.Add(V3(c, Ground(c) + 0.5f));
            Count("rapeseed fields");
        }

        // ------------------------------------------------------------------ karst pillars on the far hills
        void BuildKarsts()
        {
            var g = new LifeGeo();
            int n = 0;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f + Rand(-0.15f, 0.15f);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 c = d / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) * Rand(545f, 605f);
                int k = 1 + rng.Next(3);
                for (int j = 0; j < k; j++)
                {
                    Vector2 p = c + new Vector2(Rand(-40f, 40f), Rand(-40f, 40f));
                    p.x = Mathf.Clamp(p.x, -638f, 638f);
                    p.y = Mathf.Clamp(p.y, -638f, 638f);
                    if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) < 520f) continue;
                    NatureShapes.Karst(g, V3(p, Ground(p) - 4f), Rand(38f, 85f), Rand(10f, 20f), rng);
                    n++;
                }
            }
            meshes.Get(PK.Karst, Vector3.zero).Add(g, Matrix4x4.identity);
            Count("karst pillars", n);
        }

        // ------------------------------------------------------------------ bamboo forest light shafts
        void BuildBambooLight()
        {
            if (world.roadSamples.Count < 3) return;
            var road = world.roadSamples[2];
            var sb = meshes.Get(PK.Shafts, Vector3.zero);
            Vector3 up = -new Vector3(0.38f, -1f, 0.26f).normalized;       // against the light
            Vector3 s1 = Vector3.Cross(up, Vector3.forward).normalized, s2 = Vector3.Cross(up, s1).normalized;
            int n = 0;
            for (int i = 6; i < road.Count - 6 && n < 30; i += 8)
            {
                var s = road[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x) * ((n & 1) == 0 ? 1f : -1f);
                Vector2 p = s.pos + nrm * Rand(-1f, 14f) + s.tangent * Rand(-5f, 5f);
                if (!MapData.InBambooForest(p)) continue;
                Vector3 g = V3(p, Ground(p) - 0.3f);
                float len = Rand(15f, 22f), w = Rand(1.2f, 2.6f);
                int alpha = rng.Next(90, 165);
                ShaftQuad(sb, g, up, s1, len, w, alpha);
                ShaftQuad(sb, g, up, s2, len, w * 0.8f, alpha);
                if (n % 4 == 0) fx.meadow.Add(g + Vector3.up * 1.2f);
                n++;
            }
            Count("light shafts", n);
        }

        static void ShaftQuad(PropBucket b, Vector3 g, Vector3 up, Vector3 side, float len, float w, int alpha)
        {
            Vector3 nrm = Vector3.Cross(up, side).normalized;
            float[] t = { 0f, 0.25f, 1f };
            float[] al = { 0.55f, 1f, 0f };
            int prev = -1;
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = g + up * (len * t[i]);
                float hw = w * Mathf.Lerp(1.15f, 0.8f, t[i]) * 0.5f;
                Color32 col = new Color32(255, 238, 196, (byte)(alpha * al[i]));
                int a = b.Vert(c - side * hw, nrm, col, new Vector4(0f, 0.5f, 0f, 0f));
                b.Vert(c + side * hw, nrm, col, new Vector4(1f, 0.5f, 0f, 0f));
                if (prev >= 0) { b.Tri(prev, a, a + 1); b.Tri(prev, a + 1, prev + 1); }
                prev = a;
            }
        }

        // ------------------------------------------------------------------ where insects appear
        void BuildBugSpots()
        {
            Vector2 lc = MapData.LakeCenter, lr = MapData.LakeRadii;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                fx.water.Add(new Vector3(lc.x + Mathf.Cos(a) * lr.x * 1.02f, WorldBuilder.WaterY + 0.15f, lc.y + Mathf.Sin(a) * lr.y * 1.02f));
            }
            for (int i = 4; i < river.Count - 4; i += 7)
            {
                Vector2 t = (river[i + 1] - river[i - 1]).normalized;
                Vector2 nrm = new Vector2(t.y, -t.x) * ((i & 1) == 0 ? 1f : -1f);
                Vector2 p = river[i] + nrm * 10.5f;
                if (Mathf.Abs(p.x) > 470f || Mathf.Abs(p.y) > 470f) continue;
                fx.water.Add(V3(p, WorldBuilder.WaterY + 0.15f));
            }
            Count("butterfly spots", fx.flowers.Count);
            Count("dragonfly spots", fx.water.Count);
            Count("firefly spots", fx.water.Count + fx.meadow.Count + fx.flowers.Count);
            Count("steam / incense sources", fx.steam.Count + fx.incense.Count);
        }
    }
}

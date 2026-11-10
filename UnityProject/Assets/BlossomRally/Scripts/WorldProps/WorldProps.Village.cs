// WorldProps.Village.cs - village & culture placement: Lantern Street market and lantern spans, guardian
// lions (gate, bridges, pagoda), tea house, temple with drum / bell towers and incense, courtyard houses,
// moon-gate gardens, washing lines and parked vehicles.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public partial class WorldProps
    {
        readonly List<Vector3> stallSpots = new List<Vector3>();
        Vector2 teaSite, templeSite;
        bool hasTea, hasTemple;

        // ------------------------------------------------------------------ Lantern Street (built synchronously)
        void BuildStreet()
        {
            streetBuilt = true;
            if (world.roadSamples.Count > 4) { BuildStalls(); BuildLanternSpans(); }
            BuildGateLions();
            BuildBridgeLions();
            Physics.SyncTransforms();
        }

        List<Vector2> StreetPosts()
        {
            var posts = new List<Vector2>();
            var ls = world.roadSamples[4];
            for (int i = 4; i < ls.Count - 2; i += 7)
            {
                var s = ls[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                posts.Add(s.pos + nrm * (s.halfWidth + 1.3f));
                posts.Add(s.pos - nrm * (s.halfWidth + 1.3f));
            }
            return posts;
        }

        static bool NearAny(List<Vector2> pts, Vector2 p, float r)
        {
            float r2 = r * r;
            for (int i = 0; i < pts.Count; i++) if ((pts[i] - p).sqrMagnitude < r2) return true;
            return false;
        }

        bool Straight(List<RoadSample> list, int i, int k, float minDot)
        {
            int a = Mathf.Max(0, i - k), b = Mathf.Min(list.Count - 1, i + k);
            return Vector2.Dot(list[a].tangent, list[b].tangent) > minDot;
        }

        void BuildStalls()
        {
            var street = world.roadSamples[4];
            var posts = StreetPosts();
            int[] last = { -99, -99 };
            int type = 0, placed = 0;
            for (int i = 6; i < street.Count - 6 && placed < 10; i++)
                for (int k = 0; k < 2; k++)
                {
                    if (i - last[k] < 6) continue;
                    float side = k == 0 ? -1f : 1f;
                    var s = street[i];
                    if (!Straight(street, i, 3, 0.97f)) continue;
                    Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x) * side;
                    Vector2 origin = s.pos + nrm * (s.halfWidth + 2.4f);
                    Vector2 centre = s.pos + nrm * (s.halfWidth + 3.2f);
                    if (Vector2.Distance(centre, MapData.Gate) < 13f || Vector2.Distance(centre, MapData.Garage) < 20f || Vector2.Distance(centre, Garage.Spot) < 12f) continue;
                    if (!PlacementRegistry.IsFree(centre, 1.9f)) continue;
                    if (EdgeDist(s.pos + nrm * (s.halfWidth + 1.65f)) < 1.45f) continue;   // awning edge >= 1.5 m off every road
                    if (NearAny(posts, centre, 2.7f)) continue;
                    float yaw = YawTo(-nrm);
                    float y = Ground(origin);
                    if (!PhysFree(V3(centre, y + 1.45f), new Vector3(1.3f, 0.75f, 1.0f), yaw)) continue;
                    var steam = new List<Vector3>();
                    var m = PropShapes.Stall(type % 5, rng, steam);
                    Vector3 pos = V3(origin, y - 0.02f);
                    meshes.Stamp(m, pos, yaw);
                    foreach (var sp in steam) fx.steam.Add(L2W(pos, yaw, sp));
                    BoxCol(L2W(pos, yaw, new Vector3(0f, 1.2f, -0.8f)), new Vector3(2.7f, 2.4f, 1.9f), yaw);
                    PlacementRegistry.Reserve(centre, 1.9f);
                    stallSpots.Add(V3(centre, y));
                    fx.flowers.Add(V3(centre, y));
                    Count("market stalls");
                    type++; placed++;
                    last[k] = i;
                }
        }

        void BuildLanternSpans()
        {
            var street = world.roadSamples[4];
            var posts = StreetPosts();
            int last = -99;
            for (int i = 8; i < street.Count - 6; i++)
            {
                if (i - last < 11) continue;
                var s = street[i];
                if (!Straight(street, i, 4, 0.985f)) continue;
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                float off = s.halfWidth + 1.8f;
                Vector2 a = s.pos + nrm * off, b = s.pos - nrm * off;
                if (Vector2.Distance(s.pos, MapData.Gate) < 12f || Vector2.Distance(s.pos, MapData.Garage) < 16f) continue;
                if (NearAny(posts, a, 1.6f) || NearAny(posts, b, 1.6f)) continue;
                if (EdgeDist(a) < 1.6f || EdgeDist(b) < 1.6f) continue;
                if (!PlacementRegistry.IsFree(a, 0.4f) || !PlacementRegistry.IsFree(b, 0.4f)) continue;
                float ya = Ground(a), yb = Ground(b);
                if (!PhysFree(V3(a, ya + 1.6f), new Vector3(0.35f, 0.9f, 0.35f), 0f) || !PhysFree(V3(b, yb + 1.6f), new Vector3(0.35f, 0.9f, 0.35f), 0f)) continue;
                float top = Mathf.Max(Mathf.Max(ya, yb), s.height) + 7.4f;    // lantern bottoms stay > 5.5 m above the paving
                var m = PropShapes.LanternSpan(V3(a, ya), V3(b, yb), rng, top);
                meshes.Stamp(m, Matrix4x4.identity, V3(s.pos, s.height));
                foreach (var p in new[] { a, b })
                {
                    BoxCol(V3(p, Ground(p) + 1.5f), new Vector3(0.3f, 3f, 0.3f), 0f);
                    PlacementRegistry.Reserve(p, 0.6f);
                }
                Count("lantern & firecracker spans");
                last = i;
            }
        }

        void Lion(Vector2 p, float yaw, int variant, string key)
        {
            float y = Ground(p);
            Vector3 pos = V3(p, y - 0.05f);
            meshes.Stamp(PropShapes.StoneLion(variant), pos, yaw);
            BoxCol(pos + Vector3.up * 0.95f, new Vector3(1.05f, 1.9f, 1.4f), yaw);
            PlacementRegistry.Reserve(p, 0.9f);
            Count(key);
        }

        void BuildGateLions()
        {
            Vector2 g = MapData.Gate;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 p = g + new Vector2(s * 8.3f, -2.9f);
                if (EdgeDist(p) < 2.1f) p += new Vector2(s * 1.2f, 0f);
                if (EdgeDist(p) < 2.1f) continue;
                Lion(p, 180f, s > 0 ? 0 : 1, "stone lions");
            }
        }

        void BuildBridgeLions()
        {
            foreach (var bm in world.BridgeMarks)
            {
                Vector2 c = new Vector2(bm.x, bm.z);
                RoadSample rs;
                if (world.NearestRoad(c, out rs, 12f) >= 12f) continue;
                var list = world.roadSamples[rs.road];
                bool closed = world.roads[rs.road].closed;
                int n = list.Count, j = 0;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = (list[i].pos - c).sqrMagnitude;
                    if (d < best && list[i].bridge) { best = d; j = i; }
                }
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    int e = j;
                    for (int g = 0; g < n; g++)
                    {
                        int nx = Wrap(e + dir, n, closed);
                        if (nx < 0 || !list[nx].bridge) break;
                        e = nx;
                    }
                    for (int side = -1; side <= 1; side += 2)
                        for (int k = 4; k <= 18; k++)
                        {
                            int idx = Wrap(e + dir * k, n, closed);
                            if (idx < 0) break;
                            var s = list[idx];
                            Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                            Vector2 p = s.pos + nrm * side * (s.halfWidth + 2.3f);
                            if (world.RiverDistance(p) < 13.5f || Ground(p) < WorldBuilder.WaterY + 0.5f) continue;
                            if (EdgeDist(p) < 2.0f || !PlacementRegistry.IsFree(p, 0.8f)) continue;
                            if (!PhysFree(V3(p, Ground(p) + 1.1f), new Vector3(0.5f, 0.6f, 0.6f), 0f)) continue;
                            Lion(p, YawTo(s.tangent * dir), side > 0 ? 0 : 1, "stone lions");
                            break;
                        }
                }
            }
        }

        static int Wrap(int i, int n, bool closed)
        {
            if (closed) return (i % n + n) % n;
            return (i < 0 || i >= n) ? -1 : i;
        }

        // ------------------------------------------------------------------ tea house by the lake
        void BuildTeaHouse()
        {
            Vector2 lc = MapData.LakeCenter;
            Vector2 site;
            System.Func<Vector2, bool> ok = p => MapData.LakeK(p) > 1.4f && LifeShapes.ClearOfPavilion(p, 6f);
            if (!FindSite(lc + new Vector2(0f, MapData.LakeRadii.y + 19f), 50f, 10.4f, 3f, 2.2f, out site, ok)) return;
            float yaw = YawTo(lc - site);
            float y0 = Ground(site), mn, mx;
            Spread(site, 10f, out mn, out mx);
            float dT = mx - y0 + 0.35f, dB = mn - y0 - 0.5f;
            var steam = new List<Vector3>();
            var m = PropShapes.TeaHouse(dT, dB, rng, steam);
            Vector3 pos = V3(site, y0);
            meshes.Stamp(m, pos, yaw);
            foreach (var s in steam) fx.steam.Add(L2W(pos, yaw, s));
            BoxCol(L2W(pos, yaw, new Vector3(0f, dT + 2.1f, -4.6f)), new Vector3(11.4f, 4.2f, 5.6f), yaw);
            BoxCol(L2W(pos, yaw, new Vector3(0f, (dT + dB) * 0.5f, 0f)), new Vector3(13.2f, dT - dB, 15.2f), yaw);
            PlacementRegistry.Reserve(site, 10.4f);
            TeaSign(pos, yaw, dT);
            hasTea = true;
            teaSite = site;
            Count("tea house");
            Count("tea tables", 5);
            fx.flowers.Add(L2W(pos, yaw, new Vector3(2f, dT, 7f)));
            Vector3 side = L2W(pos, yaw, new Vector3(10.5f, 0f, 2f));
            PlaceModel(NatureShapes.WisteriaArch(rng, 3f), new Vector2(side.x, side.z), 12f, 3.6f, 2f, yaw, Vector3.zero, Vector3.zero, "wisteria arches", 1.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 r = L2W(pos, yaw, new Vector3(s * 7.6f, 0f, 8.5f));
                PlaceModel(PropShapes.ScholarRock(rng, Rand(1.4f, 2.2f)), new Vector2(r.x, r.z), 5f, 0.8f, 2f, Rand(0f, 360f), Vector3.zero, Vector3.zero, "scholar rocks");
            }
            Vector3 bk = L2W(pos, yaw, new Vector3(-4.5f, 0f, 10f));
            PlaceBikes(new Vector2(bk.x, bk.z), 2, 8f);
            PlaceModel(PropShapes.Rickshaw(0), new Vector2(bk.x, bk.z) + new Vector2(3f, 0f), 10f, 1.7f, 2f, float.NaN, new Vector3(1.45f, 1.8f, 2.9f), new Vector3(0f, 0.9f, 0.55f), "rickshaws");
        }

        void TeaSign(Vector3 pos, float yaw, float dT)
        {
            var tex = Resources.Load<Texture2D>("Env/ENV_Sign_Teahouse");
            if (tex == null) return;
            var frame = new PropModel();
            frame[PK.Landmark].Box(new Vector3(-2.75f, dT + 1.75f, -1.5f), new Vector3(0.33f, 0.9f, 0.03f), Pal.WoodDark);
            meshes.Stamp(frame, pos, yaw);
            var mesh = new Mesh { name = "Props_TeaSign" };
            float w = 0.28f, h = 0.84f;
            mesh.vertices = new[] { new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f) };
            mesh.uv = new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            var go = new GameObject("Props_TeaSign");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(L2W(pos, yaw, new Vector3(-2.75f, dT + 1.75f, -1.465f)), Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Standard("PropTeaSign", Color.white, 0.25f, 0f, tex);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ temple, towers, incense, lotus pond
        Vector3 bellTowerPos, drumTowerPos;
        Vector3 bellHangW, logPivotW;
        float towerYaw;
        bool hasBell, hasDrum;

        void BuildTemple()
        {
            // the pagoda's own incense burner and guardian lions on the summit approach
            if (world.roadSamples.Count > 3 && world.roadSamples[3].Count > 0)
            {
                var hp = world.roadSamples[3];
                Vector2 end = hp[hp.Count - 1].pos, pt = MapData.PagodaTop;
                Vector2 dir = (end - pt).normalized, perp = new Vector2(-dir.y, dir.x);
                for (float d = 10.5f; d < 16f; d += 0.75f)
                {
                    Vector2 p = pt + dir * d;
                    if (EdgeDist(p) < 2.6f) continue;
                    if (!PhysFree(V3(p, Ground(p) + 1.2f), new Vector3(0.9f, 0.7f, 0.9f), 0f)) continue;
                    Burner(p, YawTo(-dir));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector2 lp = p + dir * 1.5f + perp * s * 3.4f;
                        if (EdgeDist(lp) > 2.1f && PhysFree(V3(lp, Ground(lp) + 1.1f), new Vector3(0.5f, 0.6f, 0.6f), 0f))
                            Lion(lp, YawTo(dir), s > 0 ? 0 : 1, "stone lions");
                    }
                    break;
                }
            }
            // temple at the foot of Pagoda hill, by the main loop
            Vector2 site;
            System.Func<Vector2, bool> ok = p => !MapData.InBambooForest(p) && !MapData.InTeaTerraces(p);
            if (!FindSite(new Vector2(42f, 345f), 70f, 8.2f, 3f, 3.4f, out site, ok)) return;
            float yaw = FaceRoadYaw(site);
            float y0 = Ground(site), mn, mx;
            Spread(site, 7.8f, out mn, out mx);
            float bt = mx - y0 + 0.45f, bb = mn - y0 - 0.5f;
            Vector3 pos = V3(site, y0);
            meshes.Stamp(PropShapes.TempleHall(bt, bb), pos, yaw);
            BoxCol(L2W(pos, yaw, new Vector3(0f, (bt + bb) * 0.5f, 0f)), new Vector3(12.4f, bt - bb, 9.4f), yaw);
            BoxCol(L2W(pos, yaw, new Vector3(0f, bt + 2.2f, -0.7f)), new Vector3(9.2f, 4.4f, 5f), yaw);
            PlacementRegistry.Reserve(site, 8.2f);
            hasTemple = true;
            templeSite = site;
            towerYaw = yaw;
            Count("temple");
            int steps = bt - bb > 0.15f ? Mathf.Clamp(Mathf.CeilToInt((bt - bb) / 0.22f), 1, 14) : 0;
            Vector3 bw = L2W(pos, yaw, new Vector3(0f, 0f, 4.7f + steps * 0.32f + 2.4f));
            Vector2 bp = new Vector2(bw.x, bw.z);
            if (PlacementRegistry.CanPlace(world, bp, 1.2f, 2.5f)) { Burner(bp, yaw + 180f); PlacementRegistry.Reserve(bp, 1.2f); }
            // drum tower (west) and bell tower (east) flanking the temple
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 tw = L2W(pos, yaw, new Vector3(s * 14f, 0f, 3f));
                Vector2 ts;
                if (!FindSite(new Vector2(tw.x, tw.z), 30f, 5.6f, 3f, 3.2f, out ts, ok)) continue;
                float ty0 = Ground(ts), tmn, tmx;
                Spread(ts, 5.2f, out tmn, out tmx);
                float tbt = tmx - ty0 + 0.3f, tbb = tmn - ty0 - 0.4f;
                bool bell = s > 0;
                Vector3 hang, pivot;
                var tm = PropShapes.Tower(bell, tbt, tbb, out hang, out pivot);
                Vector3 tpos = V3(ts, ty0);
                meshes.Stamp(tm, tpos, yaw);
                float top = tbt + 3.8f;
                BoxCol(L2W(tpos, yaw, new Vector3(0f, (top + tbb) * 0.5f, 0f)), new Vector3(7.4f, top - tbb, 7.4f), yaw);
                PlacementRegistry.Reserve(ts, 5.6f);
                if (bell) { hasBell = true; bellTowerPos = tpos; bellHangW = L2W(tpos, yaw, hang); logPivotW = L2W(tpos, yaw, pivot); Count("bell tower"); }
                else { hasDrum = true; drumTowerPos = L2W(tpos, yaw, new Vector3(0f, top + 1.7f, 0f)); Count("drum tower"); }
            }
            // lotus pond on the approach
            Vector3 pw = L2W(pos, yaw, new Vector3(-6f, 0f, 17f));
            Vector2 ps;
            if (FindSite(new Vector2(pw.x, pw.z), 40f, 6.4f, 3f, 2.4f, out ps, ok))
            {
                LotusPond(ps, 5.6f);
                PlacementRegistry.Reserve(ps, 6.4f);
                Vector3 aw = L2W(V3(ps, 0f), yaw, new Vector3(8.5f, 0f, 0f));
                PlaceModel(NatureShapes.WisteriaArch(rng, 2f), new Vector2(aw.x, aw.z), 14f, 2.8f, 2f, yaw + 90f, Vector3.zero, Vector3.zero, "wisteria arches", 1.4f);
            }
            PlaceBikes(new Vector2(bw.x, bw.z) + new Vector2(4f, 0f), 1, 10f);
            PlaceModel(PropShapes.Rickshaw(1), new Vector2(bw.x, bw.z) - new Vector2(5f, 0f), 12f, 1.7f, 2f, float.NaN, new Vector3(1.45f, 1.8f, 2.9f), new Vector3(0f, 0.9f, 0.55f), "rickshaws");
        }

        void Burner(Vector2 p, float yaw)
        {
            float y = Ground(p);
            Vector3 pos = V3(p, y - 0.05f);
            meshes.Stamp(PropShapes.IncenseBurner(rng), pos, yaw);
            BoxCol(pos + Vector3.up * 0.75f, new Vector3(1.7f, 1.5f, 1.7f), yaw);
            fx.incense.Add(pos + Vector3.up * 2.0f);
            fx.incense.Add(pos + new Vector3(0.2f, 1.95f, -0.15f));
            Count("incense burners");
        }

        void LotusPond(Vector2 c, float R)
        {
            float mn, mx;
            Spread(c, R + 0.6f, out mn, out mx);
            float L = mx + 0.06f;
            var m = new PropModel();
            var g = m[PK.Props];
            const int N = 18;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N, am = (a0 + a1) * 0.5f;
                Vector3 mid = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * (R + 0.3f);
                float bottom = mn - L - 0.4f;
                PG.OBox(g, mid + Vector3.up * ((0.22f + bottom) * 0.5f), new Vector3(0.36f, (0.22f - bottom) * 0.5f, R * Mathf.PI / N + 0.05f),
                        Quaternion.Euler(0f, -am * Mathf.Rad2Deg, 0f), k % 3 == 0 ? Pal.StoneLight : Pal.Stone);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), r = Rand(0.8f, R - 0.6f);
                NatureShapes.LilyPad(g, new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r), Rand(0.35f, 0.62f), Rand(0f, 6.28f));
            }
            for (int i = 0; i < 9; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), r = Rand(0.6f, R - 0.9f);
                NatureShapes.LotusFlower(g, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Rand(1.0f, 1.35f), Rand(0f, 6.28f), i >= 6);
            }
            Vector3 pos = V3(c, L);
            meshes.Stamp(m, pos, 0f);
            var wb = meshes.Get(PK.Water, pos);
            int ci = wb.Vert(pos, Vector3.up, new Color32(255, 255, 255, 255), new Vector4(pos.x * 0.05f, pos.z * 0.05f, 0f, 0f));
            int first = -1, prev = -1;
            for (int k = 0; k <= N; k++)
            {
                float a = (k % N) * Mathf.PI * 2f / N;
                Vector3 p = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R + 0.1f);
                int vi = k < N ? wb.Vert(p, Vector3.up, new Color32(255, 255, 255, 255), new Vector4(p.x * 0.05f, p.z * 0.05f, 0f, 0f)) : first;
                if (k == 0) first = vi;
                if (prev >= 0) wb.Tri(ci, vi, prev);
                prev = vi;
            }
            fx.water.Add(pos);
            fx.flowers.Add(pos);
            for (int s = 0; s < 2; s++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                Vector2 rp = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R + 1.6f);
                PlaceModel(PropShapes.ScholarRock(rng, Rand(1.2f, 2.0f)), rp, 4f, 0.8f, 2f, Rand(0f, 360f), Vector3.zero, Vector3.zero, "scholar rocks");
            }
            Count("lotus pond");
        }

        // ------------------------------------------------------------------ courtyard houses
        void BuildCourtyards()
        {
            Vector2[] targets = { new Vector2(-165f, -372f), new Vector2(-40f, -398f), new Vector2(-262f, -392f), new Vector2(-150f, -462f), new Vector2(15f, -452f), new Vector2(-300f, -440f) };
            int placed = 0;
            System.Func<Vector2, bool> ok = p => !MapData.InBambooForest(p) && Vector2.Distance(p, Garage.Spot) > 20f && Vector2.Distance(p, MapData.Garage) > 18f;
            foreach (var t in targets)
            {
                if (placed >= 4) break;
                Vector2 site;
                if (!FindSite(t, 45f, 10.8f, 2.0f, 2.2f, out site, ok)) continue;
                float yaw = FaceRoadYaw(site);
                float y0 = Ground(site), mn, mx;
                Spread(site, 10.4f, out mn, out mx);
                float bt = mx - y0 + 0.15f, bb = mn - y0 - 0.45f;
                Vector3 pos = V3(site, y0);
                meshes.Stamp(PropShapes.CourtyardHouse(bt, bb, placed % 3, rng), pos, yaw);
                BoxCol(L2W(pos, yaw, new Vector3(0f, (bb + bt + 2.8f) * 0.5f, 0.4f)), new Vector3(15.6f, bt + 2.8f - bb, 14.4f), yaw);
                PlacementRegistry.Reserve(site, 10.8f);
                Count("courtyard houses");
                placed++;
                // washing line beside the compound, a bicycle at the gate
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 wl = L2W(pos, yaw, new Vector3(s * 12f, 0f, -2f));
                    if (PlaceModel(PropShapes.WashingLine(rng, Rand(4.2f, 5.6f)), new Vector2(wl.x, wl.z), 6f, 3.0f, 2f, yaw + 90f, Vector3.zero, Vector3.zero, "washing lines", 1.2f))
                        break;
                }
                Vector3 bk = L2W(pos, yaw, new Vector3(2.6f, 0f, 9.4f));
                PlaceBikes(new Vector2(bk.x, bk.z), 1, 5f);
            }
            // a few more washing lines between the street houses
            var street = world.roadSamples.Count > 4 ? world.roadSamples[4] : null;
            if (street != null)
                for (int i = 20, n = 0; i < street.Count - 10 && n < 4; i += 23)
                {
                    var s = street[i];
                    Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x) * ((i & 1) == 0 ? 1f : -1f);
                    Vector2 t = s.pos + nrm * (s.halfWidth + 23f);
                    if (PlaceModel(PropShapes.WashingLine(rng, Rand(4f, 5.5f)), t, 8f, 3.0f, 3f, YawTo(new Vector2(-nrm.y, nrm.x)), Vector3.zero, Vector3.zero, "washing lines", 1.2f)) n++;
                }
        }

        // ------------------------------------------------------------------ moon-gate scholar gardens
        void BuildGardens()
        {
            Vector2[] targets = { new Vector2(-30f, -362f), MapData.LakeCenter + new Vector2(78f, 12f) };
            foreach (var t in targets)
            {
                const float hx = 7.5f, hz = 6.0f;
                Vector2 site;
                if (!FindSite(t, 70f, 10.2f, 2.5f, 1.6f, out site, p => !MapData.InBambooForest(p) && MapData.LakeK(p) > 1.6f)) continue;
                float yaw = FaceRoadYaw(site);
                float y0 = Ground(site), mn, mx;
                Spread(site, 9.6f, out mn, out mx);
                float baseY = mn - y0 - 0.3f, wallH = mx - y0 + 2.8f;
                Vector3 pos = V3(site, y0);
                meshes.Stamp(PropShapes.GardenWalls(hx, hz, baseY, wallH), pos, yaw);
                float cy = (baseY + wallH) * 0.5f, h = wallH - baseY;
                BoxCol(L2W(pos, yaw, new Vector3(0f, cy, -hz)), new Vector3(2f * hx + 0.4f, h, 0.45f), yaw);
                BoxCol(L2W(pos, yaw, new Vector3(hx, cy, 0f)), new Vector3(0.45f, h, 2f * hz), yaw);
                BoxCol(L2W(pos, yaw, new Vector3(-hx, cy, 0f)), new Vector3(0.45f, h, 2f * hz), yaw);
                for (int s = -1; s <= 1; s += 2)
                    BoxCol(L2W(pos, yaw, new Vector3(s * (1.4f + (hx - 1.4f) * 0.5f), cy, hz)), new Vector3(hx - 1.4f, h, 0.5f), yaw);
                PlacementRegistry.Reserve(site, 10.2f);
                Count("moon-gate gardens");
                // inside: scholar rocks, a plum tree, a stone table, a short wisteria arch
                var inner = new PropModel();
                for (int i = 0; i < 3; i++)
                {
                    Vector3 lp = new Vector3(-4.8f + i * 2.2f, 0f, -3.6f + (i & 1) * 1.2f);
                    inner.Append(PropShapes.ScholarRock(rng, Rand(1.3f, 2.4f)), Matrix4x4.TRS(lp + Vector3.up * (Ground(L2W(pos, yaw, lp).x, L2W(pos, yaw, lp).z) - y0), Quaternion.Euler(0f, Rand(0f, 360f), 0f), Vector3.one));
                    Count("scholar rocks");
                }
                Vector3 tl = new Vector3(3.6f, 0f, -2.2f);
                inner.Append(PropShapes.StoneTableSet(), Matrix4x4.TRS(tl + Vector3.up * (Ground(L2W(pos, yaw, tl).x, L2W(pos, yaw, tl).z) - y0), Quaternion.identity, Vector3.one));
                Vector3 pl = new Vector3(-4.0f, 0f, 2.0f);
                float ply = Ground(L2W(pos, yaw, pl).x, L2W(pos, yaw, pl).z) - y0;
                inner.Append(NatureShapes.BlossomTree(false, rng), Matrix4x4.TRS(pl + Vector3.up * ply, Quaternion.Euler(0f, Rand(0f, 360f), 0f), Vector3.one));
                Vector3 al = new Vector3(3.2f, 0f, 2.4f);
                inner.Append(NatureShapes.WisteriaArch(rng, 2f), Matrix4x4.TRS(al + Vector3.up * (Ground(L2W(pos, yaw, al).x, L2W(pos, yaw, al).z) - y0), Quaternion.Euler(0f, 90f, 0f), Vector3.one));
                meshes.Stamp(inner, pos, yaw);
                CapCol(L2W(pos, yaw, pl + Vector3.up * ply), 0.3f, 4f);
                Count("plum trees");
                Count("wisteria arches");
                fx.flowers.Add(L2W(pos, yaw, al));
                fx.flowers.Add(L2W(pos, yaw, pl));
            }
        }

        // ------------------------------------------------------------------ vehicles
        void BuildVehicles()
        {
            Vector3 truckCol = new Vector3(2.4f, 2.4f, 6.9f), truckC = new Vector3(0f, 1.2f, 0.05f);
            PlaceModel(PropShapes.Truck(0, rng), new Vector2(-190f, -404f), 45f, 3.9f, 2.0f, float.NaN, truckCol, truckC, "old trucks", 0.8f,
                       p => !MapData.InBambooForest(p));
            PlaceModel(PropShapes.Truck(1, rng), new Vector2(268f, 22f), 50f, 3.9f, 2.0f, float.NaN, truckCol, truckC, "old trucks", 0.8f,
                       p => !MapData.InTeaTerraces(p));
            Vector3 cartCol = new Vector3(1.8f, 1.5f, 3.8f), cartC = new Vector3(0f, 0.75f, 0.75f);
            PlaceModel(PropShapes.Cart(2, rng), new Vector2(285f, 60f), 40f, 2.2f, 2.0f, float.NaN, cartCol, cartC, "carts", 0.8f, p => !MapData.InTeaTerraces(p));
            PlaceModel(PropShapes.Cart(3, rng), new Vector2(-120f, -445f), 40f, 2.2f, 2.0f, float.NaN, cartCol, cartC, "carts");
            PlaceModel(PropShapes.Rickshaw(rng.Next(2)), MapData.Gate + new Vector2(-13f, -9f), 14f, 1.7f, 2f, float.NaN, new Vector3(1.45f, 1.8f, 2.9f), new Vector3(0f, 0.9f, 0.55f), "rickshaws");
            for (int i = 0; i < stallSpots.Count && i < 9; i += 3)
            {
                Vector3 s = stallSpots[i];
                PlaceBikes(new Vector2(s.x, s.z), 1, 6f);
            }
        }
    }
}

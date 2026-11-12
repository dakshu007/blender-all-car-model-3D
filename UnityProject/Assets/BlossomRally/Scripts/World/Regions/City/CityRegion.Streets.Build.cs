// CityRegion.Streets.Build.cs - street detail along every city road: raised kerbs (red-white on the race
// routes), street lamps, trees in planters, signals and zebra crossings at the junctions, parked cars and
// scooters, bus shelters, billboards, and the concrete street-circuit barriers with red-white stripes and catch
// fences along Harbour Street (start / finish gantries), plus neon gantries over the Neon Circus.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CityRegion
    {
        struct CityRoad { public int idx; public RoadDef def; public List<RoadSample> s; }
        readonly List<CityRoad> myRoads = new List<CityRoad>();
        readonly Dictionary<long, List<int>> sampleGrid = new Dictionary<long, List<int>>();   // packed road << 16 | sample

        void IndexRoads(RegionContext ctx)
        {
            var f = ctx.Field;
            for (int r = 0; r < f.roads.Count; r++)
            {
                var def = f.roads[r];
                if (def.region != RegionId.City && !(def.connector && def.name == "Road_Link_ValleyCity")) continue;
                if (def.region == RegionId.City) myRoads.Add(new CityRoad { idx = r, def = def, s = f.roadSamples[r] });
                var list = f.roadSamples[r];
                for (int i = 0; i < list.Count; i++)
                {
                    long k = WorldField.Key(Mathf.FloorToInt(list[i].pos.x / 16f), Mathf.FloorToInt(list[i].pos.y / 16f));
                    List<int> l;
                    if (!sampleGrid.TryGetValue(k, out l)) { l = new List<int>(); sampleGrid[k] = l; }
                    l.Add((r << 16) | i);
                }
            }
        }

        /// <summary>Distance from p to the edge of the nearest road other than 'self' (99 if none within ~30 m).</summary>
        float OtherEdge(RegionContext ctx, Vector2 p, int self)
        {
            int cx = Mathf.FloorToInt(p.x / 16f), cz = Mathf.FloorToInt(p.y / 16f);
            float best = 99f;
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    List<int> l;
                    if (!sampleGrid.TryGetValue(WorldField.Key(cx + dx, cz + dz), out l)) continue;
                    foreach (int packed in l)
                    {
                        int r = packed >> 16;
                        if (r == self) continue;
                        var s = ctx.Field.roadSamples[r][packed & 0xFFFF];
                        float d = Vector2.Distance(s.pos, p) - s.halfWidth;
                        if (d < best) best = d;
                    }
                }
            return best;
        }

        static Vector2 Nrm(RoadSample s) { return new Vector2(s.tangent.y, -s.tangent.x); }

        bool Blocked(Vector2 p)
        {
            int zi = CityPlan.ZoneAt(p.x, p.y);
            if (zi >= 0)
            {
                var k = CityPlan.Zones[zi].kind;
                if (k == CityZoneKind.Lot || k == CityZoneKind.Stadium || k == CityZoneKind.Temple) return true;
            }
            return CityPlan.ShoreSD(p.x, p.y) > -4f;
        }

        // ------------------------------------------------------------------ kerbs
        void BuildKerbs(RegionContext ctx)
        {
            foreach (var rd in myRoads)
            {
                bool race = rd.def.name == RoadHarbour || rd.def.name == RoadCircus;
                bool paving = rd.def.surface == Surface.Paving;
                var s = rd.s;
                int n = s.Count + (rd.def.closed ? 1 : 0);
                float w = race ? 0.8f : 0.32f;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        var a = s[i % s.Count];
                        var b = s[(i + 1) % s.Count];
                        if (a.bridge || b.bridge) continue;
                        Vector2 na = Nrm(a) * side, nb = Nrm(b) * side;
                        Vector2 pa = a.pos + na * a.halfWidth, pb = b.pos + nb * b.halfWidth;
                        if (OtherEdge(ctx, pa, rd.idx) < 1.2f || OtherEdge(ctx, pb, rd.idx) < 1.2f) continue;
                        if (CityPlan.ShoreSD(pa.x, pa.y) > -1f) continue;
                        float ya = a.height + 0.05f, yb = b.height + 0.05f;
                        float hk = paving ? 0.08f : 0.15f;
                        Color32 col = race ? (((int)(a.distance / 4f)) % 2 == 0 ? CityKit.BarrierRed : CityKit.BarrierWhite) : CityKit.Kerb;
                        Vector3 ia = new Vector3(pa.x, ya + hk, pa.y), ib = new Vector3(pb.x, yb + hk, pb.y);
                        Vector3 oa = new Vector3(pa.x + na.x * w, ya + hk, pa.y + na.y * w), ob = new Vector3(pb.x + nb.x * w, yb + hk, pb.y + nb.y * w);
                        geo.Quad(PK.Props, ia, ib, ob, oa, Vector3.up, col);
                        Vector3 down = new Vector3(0f, hk + 0.12f, 0f);
                        geo.Quad(PK.Props, ia - down, ib - down, ib, ia, new Vector3(-na.x, 0f, -na.y), Sh(col, 0.85f));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ lamps, trees, cars
        void BuildStreetFurniture(RegionContext ctx)
        {
            var r = new System.Random(6161);
            var lampAve = CityKit.Lamp(true, false);
            var lampSt = CityKit.Lamp(false, false);
            var trees = new PropModel[4];
            for (int i = 0; i < 4; i++) trees[i] = CityKit.StreetTree(new System.Random(300 + i), i, true);
            var cars = new List<PropModel>();
            for (int i = 0; i < 10; i++) cars.Add(CityKit.Car(CityKit.CarPaints[i % CityKit.CarPaints.Length], i % 3));
            var bench = CityKit.Bench();
            int carCount = 0;
            foreach (var rd in myRoads)
            {
                string nm = rd.def.name;
                bool avenue = nm == RoadJade || nm == "Road_City_CentralAvenue" || nm == "Road_City_NorthAvenue";
                bool race = nm == RoadHarbour;
                bool alley = rd.def.surface == Surface.Paving;
                bool circus = nm == RoadCircus;
                bool parking = !avenue && !race && !alley && !circus && rd.def.fixedHeight && !nm.Contains("Spur");
                var s = rd.s;
                float total = s[s.Count - 1].distance;
                float lampStep = avenue ? 30f : 34f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = (avenue || side < 0) ? 0f : lampStep * 0.5f;
                    float nextLamp = 8f + phase, nextTree = 18f + phase, nextCar = 12f + (side > 0 ? 9f : 0f);
                    for (int i = 0; i < s.Count; i++)
                    {
                        var a = s[i];
                        if (a.bridge) continue;
                        Vector2 nn = Nrm(a) * side;
                        float d = a.distance;
                        float gy = a.height;
                        // lamps
                        if (!alley && d >= nextLamp)
                        {
                            nextLamp = d + lampStep;
                            Vector2 p = a.pos + nn * (a.halfWidth + (race ? 2.6f : 1.3f));
                            if (OtherEdge(ctx, p, rd.idx) > 4f && !Blocked(p))
                            {
                                Vector2 to = -nn;
                                float yaw = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
                                ctx.Stamp(avenue || circus ? lampAve : lampSt, new Vector3(p.x, ctx.Ground(p) - 0.05f, p.y), yaw);
                                ctx.Capsule(new Vector3(p.x, ctx.Ground(p), p.y), 0.2f, 8f);
                            }
                        }
                        // trees in planters on the avenues and the hillside boulevards
                        if ((avenue || nm == "Road_City_TerraceRise") && d >= nextTree)
                        {
                            nextTree = d + 30f;
                            Vector2 p = a.pos + nn * (a.halfWidth + 4.2f);
                            if (OtherEdge(ctx, p, rd.idx) > 5f && !Blocked(p))
                            {
                                ctx.Stamp(trees[r.Next(4)], new Vector3(p.x, ctx.Ground(p) - 0.05f, p.y), (float)r.NextDouble() * 360f);
                                ctx.Capsule(new Vector3(p.x, ctx.Ground(p), p.y), 1.35f, 1.3f);
                                if (r.NextDouble() < 0.4)
                                {
                                    Vector2 bp = p + a.tangent * 4f;
                                    ctx.Stamp(bench, new Vector3(bp.x, ctx.Ground(bp), bp.y), Mathf.Atan2(nn.x, nn.y) * Mathf.Rad2Deg);
                                }
                            }
                        }
                        // parked cars in kerbside bays
                        if (parking && d >= nextCar && d > 20f && d < total - 20f && carCount < 90)
                        {
                            Vector2 p = a.pos + nn * (a.halfWidth + 1.5f);
                            if (OtherEdge(ctx, p, rd.idx) > 6f && !Blocked(p + nn * 1.2f) && r.NextDouble() < 0.7)
                            {
                                float yaw = Mathf.Atan2(a.tangent.x, a.tangent.y) * Mathf.Rad2Deg + (side > 0 ? 0f : 180f);
                                ctx.Stamp(cars[r.Next(cars.Count)], new Vector3(p.x, gy, p.y), yaw);
                                ctx.Box(new Vector3(p.x, gy + 0.8f, p.y), new Vector3(1.9f, 1.6f, 4.6f), yaw);
                                carCount++;
                                nextCar = d + 6.2f;
                            }
                            else nextCar = d + 6.2f * (1 + r.Next(3));
                        }
                    }
                }
            }
            // scooters lined up in front of the seafront shophouses and in the Old Town
            var scooters = new List<PropModel>();
            for (int i = 0; i < 5; i++) scooters.Add(CityKit.Scooter(CityKit.CarPaints[(i * 3 + 2) % CityKit.CarPaints.Length]));
            foreach (var lot in CityPlan.Lots)
            {
                if (lot.style != CityStyle.Shophouse || r.NextDouble() < 0.35) continue;
                int k = 2 + r.Next(4);
                for (int j = 0; j < k; j++)
                {
                    Vector3 p = new Vector3(lot.c.x - lot.half.x + 1f + j * 0.9f, CityPlan.Low, lot.c.y - lot.half.y - 1.6f);
                    ctx.Stamp(scooters[r.Next(scooters.Count)], p, 165f + (float)r.NextDouble() * 30f);
                }
            }
            for (int j = 0; j < 14; j++)
            {
                float x = 756f + j * 6f + (float)r.NextDouble() * 2f;
                ctx.Stamp(scooters[r.Next(scooters.Count)], new Vector3(x, CityPlan.Plat, 145.6f), 80f + (float)r.NextDouble() * 20f);
            }
            // bus shelters
            var shelter = CityKit.BusShelter(r);
            Vector3[] shel = { new Vector3(930f, 0f, -124.5f - 3f), new Vector3(1130f, 0f, 50f - 5.5f - 3f), new Vector3(820f, 0f, 230f + 5f + 3f), new Vector3(960f, 0f, 50f + 5.5f + 3f) };
            float[] shYaw = { 0f, 0f, 180f, 180f };
            for (int i = 0; i < shel.Length; i++)
            {
                Vector3 p = new Vector3(shel[i].x, CityPlan.Plat, shel[i].z);
                ctx.Stamp(shelter, p, shYaw[i]);
                ctx.Box(p + Up * 1.4f, new Vector3(6.4f, 2.8f, 2.2f), shYaw[i]);
            }
        }

        // ------------------------------------------------------------------ junctions: signals and zebra crossings
        void BuildJunctions(RegionContext ctx)
        {
            var f = ctx.Field;
            var lightA = CityKit.TrafficLight(5.2f, 0, false);
            var lightB = CityKit.TrafficLight(5.2f, 2, true);
            int signals = 0;
            foreach (var rd in myRoads)
            {
                if (rd.def.closed) continue;
                var s = rd.s;
                for (int end = 0; end < 2; end++)
                {
                    var e = s[end == 0 ? 0 : s.Count - 1];
                    // the through road at this end
                    RoadSample other = default(RoadSample);
                    float best = 99f;
                    foreach (var o in myRoads)
                    {
                        if (o.idx == rd.idx) continue;
                        foreach (var so in o.s)
                        {
                            float dd = Vector2.Distance(so.pos, e.pos);
                            if (dd < best) { best = dd; other = so; }
                        }
                    }
                    if (best > 4f) continue;
                    // direction into the side road
                    var inner = s[end == 0 ? Mathf.Min(s.Count - 1, 8) : Mathf.Max(0, s.Count - 9)];
                    Vector2 dIn = (inner.pos - e.pos).normalized;
                    Vector2 T = other.tangent;
                    float hwT = other.halfWidth, hwS = e.halfWidth;
                    // zebra crossing across the side road just outside the through road
                    Vector2 zc = e.pos + dIn * (hwT + 3.2f);
                    Zebra(zc, dIn, hwS, e.height);
                    // zebra across the through road on one side of the junction
                    Vector2 zt = other.pos + T * (hwS + 3.4f) * (end == 0 ? 1f : -1f);
                    Zebra(zt, T, hwT, other.height);
                    if (rd.def.surface == Surface.Paving) continue;
                    // two signal poles at the corners of the side road, arms reaching over it
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Vector2 corner = e.pos + dIn * (hwT + 1.6f) + T * sgn * (hwS + 1.3f);
                        if (Blocked(corner)) continue;
                        Vector2 arm = -T * sgn;
                        Vector3 A = new Vector3(arm.x, 0f, arm.y);
                        Vector3 right = Vector3.Cross(Vector3.up, A);
                        Vector3 F = new Vector3(dIn.x, 0f, dIn.y);
                        bool plusX = Vector3.Dot(-right, F) < 0f;
                        float yaw = Mathf.Atan2(arm.x, arm.y) * Mathf.Rad2Deg;
                        ctx.Stamp(plusX ? lightB : lightA, new Vector3(corner.x, e.height, corner.y), yaw);
                        ctx.Capsule(new Vector3(corner.x, e.height, corner.y), 0.18f, 6f);
                        signals++;
                    }
                }
            }
        }

        void Zebra(Vector2 c, Vector2 along, float hw, float roadY)
        {
            Vector2 n = new Vector2(along.y, -along.x);
            Color32 white = C(244, 244, 238);
            for (float o = -hw + 0.7f; o <= hw - 0.6f; o += 1.1f)
            {
                Vector2 a0 = c + n * o - along * 1.6f, a1 = c + n * (o + 0.55f) - along * 1.6f;
                Vector2 b0 = c + n * o + along * 1.6f, b1 = c + n * (o + 0.55f) + along * 1.6f;
                float y0 = roadY + 0.05f + 0.04f * (1f - Mathf.Abs(o) / hw) + 0.025f, y1 = roadY + 0.05f + 0.04f * (1f - Mathf.Abs(o + 0.55f) / hw) + 0.025f;
                geo.Quad(PK.Props, new Vector3(a0.x, y0, a0.y), new Vector3(a1.x, y1, a1.y), new Vector3(b1.x, y1, b1.y), new Vector3(b0.x, y0, b0.y), Vector3.up, white);
            }
        }

        // ------------------------------------------------------------------ race barriers
        void BuildBarriers(RegionContext ctx)
        {
            int ri = ctx.Field.RoadIndex(RoadHarbour);
            if (ri < 0) return;
            var s = ctx.Field.roadSamples[ri];
            Color32 fence = C(70, 76, 84);
            for (int side = -1; side <= 1; side += 2)
            {
                int runStart = -1;
                float runLen = 0f;
                for (int i = 0; i < s.Count - 1; i++)
                {
                    var a = s[i];
                    var b = s[i + 1];
                    Vector2 na = Nrm(a) * side, nb = Nrm(b) * side;
                    float off = a.halfWidth + 1.25f;
                    Vector2 pa = a.pos + na * off, pb = b.pos + nb * off;
                    bool gap = OtherEdge(ctx, pa, ri) < 5f || OtherEdge(ctx, pb, ri) < 5f || a.distance < 6f || b.distance > s[s.Count - 1].distance - 6f;
                    if (!gap)
                    {
                        float ya = ctx.Ground(pa) - 0.02f, yb = ctx.Ground(pb) - 0.02f;
                        bool red = ((int)(a.distance / 3f)) % 2 == 0;
                        Color32 col = red ? CityKit.BarrierRed : CityKit.BarrierWhite;
                        Vector2 ia0 = pa - na * 0.32f, ib0 = pb - nb * 0.32f, ia1 = pa - na * 0.14f, ib1 = pb - nb * 0.14f;
                        Vector2 oa0 = pa + na * 0.32f, ob0 = pb + nb * 0.32f, oa1 = pa + na * 0.14f, ob1 = pb + nb * 0.14f;
                        Vector3 inward = new Vector3(-na.x, 0.4f, -na.y), outward = new Vector3(na.x, 0.4f, na.y);
                        geo.Quad(PK.Props, new Vector3(ia0.x, ya, ia0.y), new Vector3(ib0.x, yb, ib0.y), new Vector3(ib1.x, yb + 0.9f, ib1.y), new Vector3(ia1.x, ya + 0.9f, ia1.y), inward, col);
                        geo.Quad(PK.Props, new Vector3(oa0.x, ya, oa0.y), new Vector3(ob0.x, yb, ob0.y), new Vector3(ob1.x, yb + 0.9f, ob1.y), new Vector3(oa1.x, ya + 0.9f, oa1.y), outward, Sh(col, 0.9f));
                        geo.Quad(PK.Props, new Vector3(ia1.x, ya + 0.9f, ia1.y), new Vector3(ib1.x, yb + 0.9f, ib1.y), new Vector3(ob1.x, yb + 0.9f, ob1.y), new Vector3(oa1.x, ya + 0.9f, oa1.y), Vector3.up, Sh(col, 1.05f));
                        // catch fence posts and rails above the barrier
                        if (i % 4 == 0) geo.Beam(PK.Props, new Vector3(pa.x, ya + 0.9f, pa.y), new Vector3(pa.x, ya + 2.9f, pa.y), 0.05f, 0.05f, fence);
                        geo.Beam(PK.Props, new Vector3(pa.x, ya + 2.85f, pa.y), new Vector3(pb.x, yb + 2.85f, pb.y), 0.025f, 0.025f, fence);
                        geo.Beam(PK.Props, new Vector3(pa.x, ya + 1.9f, pa.y), new Vector3(pb.x, yb + 1.9f, pb.y), 0.02f, 0.02f, fence);
                        if (runStart < 0) { runStart = i; runLen = 0f; }
                        runLen += Vector2.Distance(pa, pb);
                    }
                    bool close = runStart >= 0 && (gap || runLen >= 10f || i == s.Count - 2);
                    if (close)
                    {
                        int endI = gap ? i : i + 1;
                        Vector2 p0 = s[runStart].pos + Nrm(s[runStart]) * side * off;
                        Vector2 p1 = s[endI].pos + Nrm(s[endI]) * side * off;
                        Vector2 mid = (p0 + p1) * 0.5f;
                        float len = Vector2.Distance(p0, p1);
                        if (len > 0.5f)
                        {
                            Vector2 d = (p1 - p0) / len;
                            float y = (ctx.Ground(p0) + ctx.Ground(p1)) * 0.5f;
                            ctx.Box(new Vector3(mid.x, y + 0.7f, mid.y), new Vector3(0.7f, 1.4f + Mathf.Abs(ctx.Ground(p0) - ctx.Ground(p1)), len + 0.6f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
                        }
                        runStart = gap ? -1 : i + 1;
                        runLen = 0f;
                    }
                }
            }
            // start and finish gantries
            float startD = 22f, endD = s[s.Count - 1].distance - 24f;
            for (int k = 0; k < 2; k++)
            {
                float target = k == 0 ? startD : endD;
                int idx = 0;
                while (idx < s.Count - 1 && s[idx].distance < target) idx++;
                Gantry(ctx, s[idx], k == 0, new System.Random(40 + k));
            }
        }

        /// <summary>Overhead gantry across a road sample: two posts, a truss, a neon board facing both ways.</summary>
        void Gantry(RegionContext ctx, RoadSample a, bool start, System.Random r)
        {
            Vector2 n = Nrm(a);
            float hw = a.halfWidth + 2.2f, H = 7.5f;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Vector3 right = new Vector3(n.x, 0f, n.y), fwd = new Vector3(a.tangent.x, 0f, a.tangent.y);
            Color32 truss = start ? C(232, 236, 238) : C(40, 44, 50);
            for (int s = -1; s <= 1; s += 2)
            {
                g.Frustum(right * hw * s, right * hw * s + Up * (H + 1.6f), 0.35f, 0.35f, 0.3f, 0.3f, 6, 0f, truss, truss, false, true);
                ctx.Capsule(new Vector3(a.pos.x + n.x * hw * s, a.height, a.pos.y + n.y * hw * s), 0.45f, H + 1.6f);
            }
            PG.Beam(g, right * -hw + Up * H, right * hw + Up * H, 0.35f, 0.35f, truss);
            PG.Beam(g, right * -hw + Up * (H + 1.5f), right * hw + Up * (H + 1.5f), 0.25f, 0.25f, truss);
            for (float t = -hw; t < hw; t += 1.5f) PG.Beam(g, right * t + Up * H, right * (t + 1.5f) + Up * (H + 1.5f), 0.06f, 0.06f, truss);
            PG.OBox(g, Up * (H - 1.2f), new Vector3(hw - 1.5f, 0.9f, 0.15f), Quaternion.LookRotation(fwd), C(24, 26, 34));
            if (start)
            {
                // start lights: five red lamps over the road
                for (int k = -2; k <= 2; k++) gl.Ellipsoid(right * k * 1.2f + Up * (H + 0.7f) - fwd * 0.4f, new Vector3(0.35f, 0.35f, 0.35f), 8, 4, C(255, 50, 40), C(220, 30, 30));
            }
            ctx.Stamp(m, new Vector3(a.pos.x, a.height, a.pos.y), 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 nn = fwd * side;
                Vector3 c = new Vector3(a.pos.x, a.height + H - 1.2f, a.pos.y) + nn * 0.17f;
                Vector3 rr = Vector3.Cross(Vector3.up, nn);
                CityNeon.Outline(neon.Group(start ? 1 : 0), c, rr, Vector3.up, nn, 2f * (hw - 1.5f) - 0.2f, 1.6f, 0.1f, start ? C(80, 255, 170) : C(255, 90, 170));
                for (int k = 0; k < 4; k++)
                    CityKit.Glyph(neon.Group(start ? 3 : 2), c + rr * (-4.5f + k * 3f), rr, Vector3.up, nn, 1.3f, 0.14f, C(255, 236, 200), r);
            }
        }

        void BuildCircusDecor(RegionContext ctx)
        {
            int ri = ctx.Field.RoadIndex(RoadCircus);
            if (ri < 0) return;
            var s = ctx.Field.roadSamples[ri];
            // neon gantries at the west and east ends, tyre walls on the outside of the bends
            int[] at = { s.Count / 4, (s.Count * 3) / 4 };
            var r = new System.Random(99);
            foreach (int i in at) Gantry(ctx, s[i], false, r);
            var tyres = new PropModel();
            var tg = tyres[PK.Props];
            for (int k = 0; k < 6; k++)
                for (int l = 0; l < 3; l++)
                    tg.Frustum(new Vector3(k * 0.75f - 1.9f, l * 0.28f, 0f), new Vector3(k * 0.75f - 1.9f, l * 0.28f + 0.26f, 0f), 0.36f, 0.36f, 0.36f, 0.36f, 8, 0f, C(30, 30, 34), (k + l) % 2 == 0 ? C(220, 60, 50) : C(240, 240, 236), false, true);
            for (int i = 0; i < s.Count; i += 9)
            {
                var a = s[i];
                Vector2 c = CityPlan.CircusC;
                Vector2 outN = (a.pos - c).normalized;
                Vector2 p = a.pos + outN * (a.halfWidth + 4.5f);
                if (OtherEdge(ctx, p, ri) < 4f || Mathf.Abs(a.pos.x - c.x) < 60f) continue;
                float yaw = Mathf.Atan2(a.tangent.x, a.tangent.y) * Mathf.Rad2Deg + 90f;
                ctx.Stamp(tyres, new Vector3(p.x, a.height, p.y), yaw);
                ctx.Box(new Vector3(p.x, a.height + 0.4f, p.y), new Vector3(4.6f, 0.8f, 0.8f), yaw);
            }
        }

        // ------------------------------------------------------------------ billboards along the hillside
        void BuildBillboards(RegionContext ctx)
        {
            var r = new System.Random(808);
            Vector2[] spots = { new Vector2(840f, -230f), new Vector2(760f, -350f), new Vector2(1060f, -300f), new Vector2(930f, -380f), new Vector2(1370f, -230f) };
            float[] yaws = { 120f, 60f, 270f, 200f, 90f };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = spots[i];
                float gy = ctx.Ground(p);
                float yaw = yaws[i];
                var q = Quaternion.Euler(0f, yaw, 0f);
                var m = new PropModel();
                var g = m[PK.Landmark];
                var gl = m[PK.Glow];
                for (int s = -1; s <= 1; s += 2)
                {
                    g.Frustum(new Vector3(s * 4f, 0f, 0f), new Vector3(s * 4f, 7f, 0f), 0.3f, 0.3f, 0.25f, 0.25f, 6, 0f, CityKit.Steel, CityKit.SteelLight, false, true);
                    ctx.Capsule(new Vector3(p.x, gy, p.y) + q * new Vector3(s * 4f, 0f, 0f), 0.35f, 7f);
                }
                PG.Box2(g, Up * 9.5f, new Vector3(7.5f, 3f, 0.3f), C(30, 34, 40), C(30, 34, 40));
                Color32 c0 = CityKit.Neon[r.Next(CityKit.Neon.Length)], c1 = C(250, 246, 236);
                // a painted panel: gradient sky, a big glyph and a jade wave band
                gl.QuadOut(new Vector3(-7.2f, 6.8f, -0.32f), new Vector3(7.2f, 6.8f, -0.32f), new Vector3(7.2f, 12.2f, -0.32f), new Vector3(-7.2f, 12.2f, -0.32f), Vector3.back, Sh(c0, 0.5f), Sh(c0, 0.5f), Sh(c0, 0.9f), Sh(c0, 0.9f));
                gl.QuadOut(new Vector3(-7.2f, 6.8f, -0.34f), new Vector3(7.2f, 6.8f, -0.34f), new Vector3(7.2f, 8f, -0.34f), new Vector3(-7.2f, 8.6f, -0.34f), Vector3.back, CityKit.Jade, CityKit.Jade, CityKit.JadeLight, CityKit.JadeLight);
                CityKit.Glyph(gl, new Vector3(-3.5f, 10f, -0.36f), Vector3.right, Up, Vector3.back, 3.4f, 0.4f, c1, r);
                CityKit.Glyph(gl, new Vector3(0.5f, 10f, -0.36f), Vector3.right, Up, Vector3.back, 3.4f, 0.4f, c1, r);
                CityKit.Glyph(gl, new Vector3(4.5f, 10f, -0.36f), Vector3.right, Up, Vector3.back, 3.0f, 0.36f, CityKit.Gold, r);
                ctx.Stamp(m, new Vector3(p.x, gy, p.y), yaw);
            }
        }
    }
}

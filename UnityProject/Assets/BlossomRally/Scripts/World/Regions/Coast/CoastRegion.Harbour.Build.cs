// CoastRegion.Harbour.Build.cs - the granite headland (lighthouse in the skyline, keeper's house, boulders,
// tide pools) and Lotus Harbour: the granite sea-wall promenade with lamps, piers on piles, stilt houses,
// moored junks and sampans (posed in Tick), drying nets and fish racks on the quay, the seafood market
// street with arcaded qilou shophouses, stalls and lantern spans, a paifang gate and the Mazu temple.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        Vector3 lanternPos;
        readonly List<Vector4> boatSpots = new List<Vector4>();       // x, z, yaw, type (0 junk, 1 sampan)

        // ------------------------------------------------------------------ headland
        void BuildHeadland()
        {
            float g = Gnd(Lighthouse);
            cx.StampSkyline(CoastShapes.Lighthouse(), V3(Lighthouse, g - 0.4f), -90f);
            Trunk(Lighthouse, g - 0.5f, 3.4f, 24f);
            cx.Reserve(Lighthouse, 6f);
            lanternPos = V3(Lighthouse, g - 0.4f + CoastShapes.LightTowerH + 2.2f);
            Vector2 kp = KeeperPos;
            StampGround(CoastShapes.KeeperHouse(), kp, 180f, 1f, 0.3f);
            BoxCol(kp, Gnd(kp) - 0.3f, new Vector3(9.2f, 5f, 7f), 180f);
            cx.Reserve(kp, 7f);
            houses += 2;
            // benches and a viewing railing at the plaza
            var bench = Bench();
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = Lighthouse + new Vector2(10f + i * 6f, -14f);
                if (Edge(p) < 1.5f) continue;
                StampGround(bench, p, 180f);
            }

            // granite boulders: the headland rim, the south cape and the north point
            var rk = new PropModel();
            var rg = rk[PK.Landmark];
            for (int i = 0; i < 90; i++)
            {
                float a = i * Mathf.PI * 2f / 90f + Rn(-0.02f, 0.02f);
                float k = Rn(0.82f, 1.12f);
                Vector2 p = new Vector2(HeadC.x + Mathf.Cos(a) * HeadR.x * k, HeadC.y + Mathf.Sin(a) * HeadR.y * k);
                if (p.x > -1185f) continue;
                Boulder(rg, p, Rn(1.2f, 3.4f));
            }
            for (int i = 0; i < 70; i++)
            {
                float z = i < 35 ? Rn(-468f, -398f) : Rn(398f, 468f);
                Vector2 p = new Vector2(ShoreX(z) + Rn(-6f, 26f), z);
                Boulder(rg, p, Rn(1.4f, 4f));
            }
            // boulders on the headland's meadow
            for (int i = 0; i < 40; i++)
            {
                Vector2 p = new Vector2(HeadC.x + Rn(-0.85f, 0.75f) * HeadR.x, HeadC.y + Rn(-0.75f, 0.75f) * HeadR.y);
                if (Edge(p) < 4f || (p - Lighthouse).sqrMagnitude < 400f || (p - KeeperPos).sqrMagnitude < 300f) continue;
                Boulder(rg, p, Rn(0.6f, 1.8f));
            }
            // tide pools on the low rock shelves
            var water = rk[PK.Water];
            int pools = 0;
            for (int i = 0; i < 400 && pools < 12; i++)
            {
                Vector2 p = new Vector2(Rn(-1430f, -1150f), Rn(-140f, 130f));
                if (i > 250) p = new Vector2(Rn(-1300f, -1180f), i % 2 == 0 ? Rn(-460f, -390f) : Rn(390f, 460f));
                float h = Gnd(p);
                if (h < WorldField.WaterY + 0.25f || h > WorldField.WaterY + 1.8f || Slope(p) > 0.2f) continue;
                if (!Claim(p, 14f)) continue;
                CoastShapes.TidePool(rg, water, V3(p, h), Rn(1.6f, 2.6f), rnd, h + 0.12f);
                pools++;
            }
            cx.Stamp(rk, Matrix4x4.identity);
        }


        void Boulder(LifeGeo g, Vector2 p, float size)
        {
            float h = Gnd(p);
            if (h < WorldField.WaterY - 2.5f || Edge(p) < size + 1.5f) return;
            CoastShapes.GraniteBoulder(g, V3(p, h - size * 0.25f), new Vector3(size * Rn(0.9f, 1.4f), size * Rn(0.55f, 0.85f), size * Rn(0.8f, 1.2f)), Rn(0f, 90f));
            if (size > 2f && Edge(p) < 25f) BoxCol(p, h - 0.5f, new Vector3(size * 1.6f, size * 1.2f, size * 1.6f), Rn(0f, 90f));
            rocks++;
        }

        PropModel benchModel;
        PropModel Bench()
        {
            if (benchModel != null) return benchModel;
            benchModel = new PropModel();
            var g = benchModel[PK.Props];
            for (int s = -1; s <= 1; s += 2) PG.Box2(g, new Vector3(s * 0.75f, 0.22f, 0f), new Vector3(0.12f, 0.24f, 0.22f), Pal.Stone, Pal.StoneLight);
            PG.Box2(g, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.05f, 0.24f), Pal.Wood, Pal.WoodLight);
            PG.Box2(g, new Vector3(0f, 0.85f, -0.22f), new Vector3(0.95f, 0.16f, 0.03f), Pal.Wood, Pal.WoodLight);
            return benchModel;
        }

        // ------------------------------------------------------------------ harbour
        static float WallX(float z)
        {
            float s = 0f;
            for (int k = -2; k <= 2; k++) s += ShoreX(z + k * 4f);
            return s / 5f - 2.2f;
        }

        const float WallZ0 = 146f, WallZ1 = 358f;
        static readonly float[] PierZ = { 186f, 250f, 314f };

        void BuildHarbour()
        {
            BuildSeaWall();
            var hr = new System.Random(4411);
            // piers with stilt houses along them, junks off the ends, sampans alongside
            foreach (float pz in PierZ)
            {
                Vector2 root = new Vector2(WallX(pz), pz);
                float len = Rn(30f, 40f);
                Stamp(CoastShapes.Pier(len, QuayY - 0.25f, 6f, hr), root, 0f, -90f);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float s = 9f + i * 9.5f;
                        if (s > len - 2f) break;
                        Vector2 p = root + new Vector2(-s, side * 8.6f);
                        if (Gnd(p) > WorldField.WaterY - 1f) continue;
                        // front (+Z) faces the pier
                        Stamp(CoastShapes.StiltHouse(hr, QuayY - 0.4f, 6f), p, 0f, side > 0 ? 180f : 0f);
                        cx.Reserve(p, 6f);
                        houses++;
                    }
                    boatSpots.Add(new Vector4(root.x - len - 9f, pz + side * 9f, side > 0 ? -80f : -100f, 0f));
                    for (int i = 0; i < 2; i++)
                        boatSpots.Add(new Vector4(root.x - len + 2f + i * 6f, pz + side * 3.4f, -90f + Rn(-6f, 6f), 1f));
                }
            }
            // stilt houses off the wall between the piers, with gangways
            var gang = new PropModel();
            var gg = gang[PK.Landmark];
            for (int i = 0; i < PierZ.Length - 1; i++)
                for (int k = 0; k < 2; k++)
                {
                    float z = Mathf.Lerp(PierZ[i], PierZ[i + 1], 0.33f + k * 0.34f);
                    Vector2 w = new Vector2(WallX(z), z), p = w + new Vector2(-11f, 0f);
                    if (Gnd(p) > WorldField.WaterY - 1f) continue;
                    Stamp(CoastShapes.StiltHouse(hr, QuayY - 0.4f, 6f), p, 0f, 90f);
                    cx.Reserve(p, 6f);
                    Boardwalk(gg, w + new Vector2(0.5f, 0f), p + new Vector2(5.2f, 0f), QuayY - 0.35f);
                    houses++;
                    boatSpots.Add(new Vector4(p.x - 1f, z + (k == 0 ? -8f : 8f), Rn(-20f, 20f) + (k == 0 ? 180f : 0f), 1f));
                }
            cx.Stamp(gang, Matrix4x4.identity);
            // a few sampans out on the harbour and junks at anchor
            for (int i = 0; i < 5; i++) boatSpots.Add(new Vector4(Rn(-1330f, -1260f), Rn(150f, 340f), Rn(0f, 360f), i < 3 ? 1f : 0f));
            for (int i = 0; i < 3; i++) boatSpots.Add(new Vector4(Rn(-1420f, -1330f), Rn(-240f, 120f) + (i == 0 ? 200f : 0f), Rn(0f, 360f), 0f));
            // keep only the spots in deep enough water
            for (int i = boatSpots.Count - 1; i >= 0; i--)
            {
                var b = boatSpots[i];
                float need = b.w < 0.5f ? WorldField.WaterY - 2.2f : WorldField.WaterY - 1.0f;
                if (Gnd(b.x, b.y) > need) boatSpots.RemoveAt(i);
            }

            BuildMarketStreet(hr);
            BuildQuayProps(hr);
        }

        void BuildSeaWall()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float top = QuayY + 0.12f;
            Color32 face = CoastShapes.GraniteB, faceD = CoastShapes.GraniteC, pave = LifeGeo.C(206, 196, 184), paveB = LifeGeo.C(190, 180, 170), rail = LifeGeo.C(220, 212, 200);
            var lampM = CoastShapes.PromenadeLamp();
            var bench = Bench();
            int idx = 0;
            float lastLamp = -99f, lastCol = WallZ0;
            Vector2 colStart = new Vector2(WallX(WallZ0), WallZ0);
            for (float z = WallZ0; z < WallZ1; z += 2.5f, idx++)
            {
                Vector2 a = new Vector2(WallX(z), z), b = new Vector2(WallX(z + 2.5f), z + 2.5f);
                Vector3 a0 = V3(a, -4.5f), b0 = V3(b, -4.5f), a1 = V3(a, top), b1 = V3(b, top);
                Color32 c = (idx & 1) == 0 ? face : Color32.Lerp(face, faceD, 0.4f);
                g.QuadOut(a0, b0, b1, a1, Vector3.left, faceD, faceD, c, c);
                // coping and paved promenade top
                Vector2 inl = new Vector2(4.2f, 0f);
                g.QuadOut(a1 + Vector3.left * 0.25f, b1 + Vector3.left * 0.25f, V3(b + inl, top), V3(a + inl, top), Vector3.up, (idx & 1) == 0 ? pave : paveB, pave, pave, pave);
                g.QuadOut(a1 + Vector3.left * 0.25f - Vector3.up * 0.2f, b1 + Vector3.left * 0.25f - Vector3.up * 0.2f, b1 + Vector3.left * 0.25f, a1 + Vector3.left * 0.25f, Vector3.left, rail, rail, rail, rail);
                // a dark wet band at the waterline
                g.QuadOut(V3(a, WorldField.WaterY - 0.1f) + Vector3.left * 0.02f, V3(b, WorldField.WaterY - 0.1f) + Vector3.left * 0.02f, V3(b, WorldField.WaterY + 0.45f) + Vector3.left * 0.02f, V3(a, WorldField.WaterY + 0.45f) + Vector3.left * 0.02f,
                          Vector3.left, LifeGeo.C(88, 96, 84), LifeGeo.C(88, 96, 84), LifeGeo.C(120, 120, 110), LifeGeo.C(120, 120, 110));
                bool atPier = false;
                foreach (float pz in PierZ) if (Mathf.Abs(z - pz) < 2.6f) atPier = true;
                if (!atPier)
                {
                    Vector3 pa = V3(a + new Vector2(0.25f, 0f), top), pb = V3(b + new Vector2(0.25f, 0f), top);
                    PG.Box2(g, pa + Vector3.up * 0.48f, new Vector3(0.13f, 0.48f, 0.13f), rail, rail);
                    PG.Beam(g, pa + Vector3.up * 0.95f, pb + Vector3.up * 0.95f, 0.08f, 0.06f, rail);
                    PG.Beam(g, pa + Vector3.up * 0.45f, pb + Vector3.up * 0.45f, 0.04f, 0.04f, rail);
                }
                if (z - lastLamp > 16f && !atPier)
                {
                    Stamp(lampM, a + new Vector2(2.6f, 0f), top, 0f);
                    Trunk(a + new Vector2(2.6f, 0f), top, 0.15f, 4f);
                    lastLamp = z;
                    if (((int)z / 16) % 2 == 0) Stamp(bench, a + new Vector2(2.4f, 6f), top, -90f);
                }
                if (z - lastCol >= 10f || z + 2.5f >= WallZ1)
                {
                    // railing collider (keeps cars out of the harbour)
                    Vector2 mid = (colStart + b) * 0.5f, d = b - colStart;
                    cx.Box(new Vector3(mid.x + 0.2f, top + 0.6f, mid.y), new Vector3(0.5f, 1.4f, d.magnitude + 0.3f), YawOf(d));
                    colStart = b;
                    lastCol = z;
                }
            }
            // end caps
            foreach (float z in new[] { WallZ0, WallZ1 })
            {
                Vector2 a = new Vector2(WallX(z), z);
                PG.Box2(g, V3(a + new Vector2(1.5f, 0f), top - 2.6f), new Vector3(2f, 2.75f, 0.6f), face, pave);
            }
            cx.Stamp(m, Matrix4x4.identity);
        }

        void BuildMarketStreet(System.Random hr)
        {
            var st = Road(RStreet);
            if (st == null) return;
            int n = st.Count;
            float total = st[n - 1].distance;
            var stall = new PropModel[3];
            for (int i = 0; i < 3; i++) stall[i] = CoastShapes.SeafoodStall(new System.Random(700 + i));
            int colour = 0;
            float nextHouse = 14f, nextStall = 12f, nextSpan = 10f, nextSea = 16f;
            for (int i = 0; i < n; i++)
            {
                var s = st[i];
                Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x);
                float side = ShoreD(s.pos + nr * 10f) > ShoreD(s.pos - nr * 10f) ? 1f : -1f;    // +1: right side is land
                Vector2 land = nr * side;
                float hw = s.halfWidth;
                // qilou row on the land side
                if (s.distance >= nextHouse && s.distance < total - 14f)
                {
                    float W = Rn(5.4f, 7.6f);
                    int idx = Mathf.Min(n - 1, i + Mathf.RoundToInt(W * 0.25f));
                    Vector2 c = st[idx].pos + land * (hw + 1.2f);
                    Vector2 tw = -land;
                    Vector2 centre = c + land * 5.5f;
                    if (cx.Free(centre, 4f) && Edge(centre + land * 6f) > 2f)
                    {
                        float yaw = YawOf(tw);
                        Stamp(CoastShapes.Qilou(hr, W, Ch(0.35f) ? 3 : 2, colour++), c, QuayY - 0.05f, yaw);
                        BoxCol(centre, QuayY - 0.3f, new Vector3(W, 10f, 11f), yaw);
                        cx.Reserve(centre, W * 0.5f);
                        houses++;
                    }
                    nextHouse = s.distance + W + 0.25f;
                }
                // lower shophouses on the sea side, set back behind the stall row
                if (s.distance >= nextSea && s.distance < total - 16f)
                {
                    float W = Rn(5.2f, 7f);
                    int idx = Mathf.Min(n - 1, i + Mathf.RoundToInt(W * 0.25f));
                    Vector2 c = st[idx].pos - land * (hw + 3.4f);
                    Vector2 centre = c - land * 3.5f;
                    if (cx.Free(centre, 3f) && Edge(centre - land * 4f) > 2f && ShoreD(centre - land * 4f) > 10f)
                    {
                        float yaw = YawOf(land);
                        Stamp(CoastShapes.Qilou(hr, W, Ch(0.5f) ? 2 : 1, colour + 3, 7f), c, QuayY - 0.05f, yaw);
                        BoxCol(centre, QuayY - 0.3f, new Vector3(W, 7f, 7f), yaw);
                        cx.Reserve(centre, W * 0.5f);
                        houses++;
                    }
                    nextSea = s.distance + W + 0.3f;
                }
                // seafood stalls on the sea side (in front of the sea-side houses)
                if (s.distance >= nextStall && s.distance < total - 10f)
                {
                    Vector2 p = s.pos - land * (hw + 1.5f);
                    if (cx.Free(p, 1.4f))
                    {
                        cx.Reserve(p, 1.4f);
                        Stamp(stall[rnd.Next(3)], p, QuayY, YawOf(land));
                        BoxCol(p, QuayY, new Vector3(3f, 2.4f, 1.6f), YawOf(land));
                    }
                    nextStall = s.distance + Rn(5.5f, 8f);
                }
                // lantern spans over the street
                if (s.distance >= nextSpan && s.distance < total - 8f)
                {
                    Vector3 a = V3(s.pos + land * (hw + 0.5f), Gnd(s.pos + land * (hw + 0.5f))), b = V3(s.pos - land * (hw + 0.5f), Gnd(s.pos - land * (hw + 0.5f)));
                    cx.Stamp(PropShapes.LanternSpan(a, b, hr, Mathf.Max(a.y, b.y) + 5.6f), Matrix4x4.identity);
                    Trunk(new Vector2(a.x, a.z), a.y, 0.15f, 5f);
                    Trunk(new Vector2(b.x, b.z), b.y, 0.15f, 5f);
                    nextSpan = s.distance + 22f;
                }
            }
            // paifang gate over the south entrance of the street, Mazu temple at the north end
            int gi = Mathf.Min(n - 1, 8);
            var gs = st[gi];
            Stamp(Paifang(gs.halfWidth + 1f), gs.pos, gs.height - 0.05f, YawOf(gs.tangent));
            Vector2 gn = new Vector2(gs.tangent.y, -gs.tangent.x) * (gs.halfWidth + 1f);
            Trunk(gs.pos + gn, gs.height - 0.3f, 0.35f, 6f);
            Trunk(gs.pos - gn, gs.height - 0.3f, 0.35f, 6f);
            Vector2 tp = TemplePos;
            Stamp(PropShapes.TempleHall(0.9f, 0f), tp, QuayY - 0.05f, 180f);
            BoxCol(tp, QuayY, new Vector3(12.6f, 7f, 9.6f), 180f);
            cx.Reserve(tp, 9f);
            Stamp(PropShapes.IncenseBurner(hr), tp + new Vector2(0f, -10f), QuayY, 0f);
            for (int s = -1; s <= 1; s += 2) Stamp(PropShapes.StoneLion(s > 0 ? 0 : 1), tp + new Vector2(s * 3.2f, -7.4f), QuayY, 180f);
            houses++;
        }


        PropModel Paifang(float half)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            float H = 5.4f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, new Vector3(s * half, 0.35f, 0f), new Vector3(0.55f, 0.45f, 0.55f), Pal.StoneDark, Pal.Stone);
                PG.Column(g, new Vector3(s * half, 0f, 0f), new Vector3(s * half, H, 0f), 0.3f, Pal.Red, 8);
                PG.Lantern(gl, new Vector3(s * (half - 0.9f), H - 1.0f, 0.35f), 0.3f, 0.5f, Pal.RedBright, 90);
            }
            PG.Beam(g, new Vector3(-half - 0.6f, H - 0.6f, 0f), new Vector3(half + 0.6f, H - 0.6f, 0f), 0.22f, 0.25f, LifeGeo.C(56, 118, 122));
            PG.Beam(g, new Vector3(-half - 0.4f, H - 1.5f, 0f), new Vector3(half + 0.4f, H - 1.5f, 0f), 0.18f, 0.18f, Pal.RedDeep);
            PG.Box2(g, new Vector3(0f, H - 1.05f, 0.12f), new Vector3(1.6f, 0.36f, 0.08f), LifeGeo.C(40, 60, 110), LifeGeo.C(40, 60, 110));
            for (int i = 0; i < 4; i++) g.Box(new Vector3(-1.05f + i * 0.7f, H - 1.05f, 0.21f), new Vector3(0.22f, 0.24f, 0.01f), Pal.Gold);
            PG.Roof(g, new Vector3(0f, H - 0.35f, 0f), half + 0.6f, 0.7f, 1.0f, 0.8f, 0.45f, Pal.GreenTile, Pal.GreenTile2, Pal.TileUnder, LifeGeo.C(52, 100, 84), true);
            return m;
        }

        void BuildQuayProps(System.Random hr)
        {
            var nets = new PropModel[3];
            var racks = new PropModel[2];
            for (int i = 0; i < 3; i++) nets[i] = CoastShapes.NetFrame(new System.Random(810 + i));
            for (int i = 0; i < 2; i++) racks[i] = CoastShapes.FishRack(new System.Random(820 + i));
            var misc = new PropModel();
            var mg = misc[PK.Props];
            for (float z = WallZ0 + 6f; z < WallZ1 - 4f; z += Rn(7f, 11f))
            {
                Vector2 p = new Vector2(WallX(z) + Rn(8.5f, 13f), z);
                if (Edge(p) < 3f || !Claim(p, 3.4f)) continue;
                float yaw = Rn(-8f, 8f);
                if (Ch(0.6f)) Stamp(nets[rnd.Next(3)], p, QuayY, yaw);
                else Stamp(racks[rnd.Next(2)], p, QuayY, yaw);
                // baskets, crates, buoys and a coil of rope around it
                for (int k = 0; k < 3; k++)
                {
                    Vector2 q = p + new Vector2(Rn(-2.5f, 2.5f), Rn(1.6f, 2.6f) * (Ch(0.5f) ? 1f : -1f));
                    float y = QuayY;
                    switch (rnd.Next(4))
                    {
                        case 0: mg.Frustum(V3(q, y), V3(q, y + 0.42f), 0.26f, 0.26f, 0.32f, 0.32f, 7, 0f, CoastShapes.BambooDark, CoastShapes.Bamboo, true, false); break;
                        case 1: PG.Box2(mg, V3(q, y + 0.28f), new Vector3(0.42f, 0.28f, 0.32f), LifeGeo.C(70, 120, 170), LifeGeo.C(90, 140, 190)); break;
                        case 2: mg.Ellipsoid(V3(q, y + 0.2f), Vector3.one * 0.2f, 6, 4, LifeGeo.C(246, 140, 50), LifeGeo.C(200, 100, 40)); break;
                        default: mg.Frustum(V3(q, y), V3(q, y + 0.16f), 0.45f, 0.45f, 0.4f, 0.4f, 8, 0f, LifeGeo.C(196, 170, 120), LifeGeo.C(176, 150, 104), false, true); break;
                    }
                }
            }
            // people on the quay and the street
            var folk = new PropModel[6];
            for (int i = 0; i < folk.Length; i++) folk[i] = CoastShapes.Person(new System.Random(300 + i), i == 2);
            for (int i = 0; i < 40; i++)
            {
                float z = Rn(WallZ0 + 4f, WallZ1 - 6f);
                Vector2 p = new Vector2(WallX(z) + Rn(1.5f, 40f), z);
                if (Edge(p) < 0.8f || !Claim(p, 0.8f)) continue;
                Stamp(folk[rnd.Next(folk.Length)], p, Gnd(p) - 0.02f, Rn(0f, 360f));
                people++;
            }
            cx.Stamp(misc, Matrix4x4.identity);
        }
    }
}

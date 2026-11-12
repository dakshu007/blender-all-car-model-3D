// CoastRegion.Shore.Build.cs - Sunset Beach (umbrellas, loungers, snack huts, the lifeguard tower, the
// volleyball court, beached sampans, kite fliers, beachgoers, dune shrubs), the roadside (granite guard walls
// on the clifftops, chevrons, kilometre stones, beach-road lamps, junction signs, spectators, the pine-ridge
// pagoda) and the vegetation (coconut palms, coastal pine woods, mangroves, flowering shrubs).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        readonly List<Vector4> kiteSpots = new List<Vector4>();       // x, ground y, z, yaw

        // ------------------------------------------------------------------ beach
        void BuildBeach()
        {
            var br = new System.Random(6121);
            var sets = new PropModel[8];
            for (int i = 0; i < sets.Length; i++) sets[i] = CoastShapes.BeachSet(new System.Random(600 + i));
            // volleyball court in the infield of the sand loop
            float vz = -268f;
            Vector2 vc = new Vector2(ShoreX(vz) + 37f, vz);
            if (Edge(vc) > 4f) { StampGround(CoastShapes.VolleyCourt(), vc, Rn(-4f, 4f), 1f, 0.02f); cx.Reserve(vc, 9f); }
            // lifeguard tower near the water, facing the sea
            float lz = -300f;
            Vector2 lt = new Vector2(ShoreX(lz) + 2f, lz);
            StampGround(CoastShapes.LifeguardTower(), lt, -90f, 1f, 0.1f);
            BoxCol(lt, Gnd(lt), new Vector3(2.6f, 3.4f, 2.6f), 0f);
            cx.Reserve(lt, 3.5f);
            // snack huts on the dunes, facing the sea
            float[] hutZ = { -214f, -290f, -338f };
            foreach (float hz in hutZ)
            {
                Vector2 p = new Vector2(ShoreX(hz) + 72f, hz);
                for (int k = 0; k < 6 && Edge(p) < 3.5f; k++) p.x += k % 2 == 0 ? 2f : -4f;
                if (Edge(p) < 3.5f || !Claim(p, 4.5f)) continue;
                StampGround(CoastShapes.BeachHut(br), p, -90f, 1f, 0.1f);
                BoxCol(p, Gnd(p), new Vector3(4.4f, 3f, 3.4f), -90f);
                houses++;
            }
            // umbrellas: in the infield and on the dry sand towards the water
            int placed = 0;
            for (int i = 0; i < 400 && placed < 46; i++)
            {
                float z = Rn(-372f, -150f);
                float d = Ch(0.55f) ? Rn(25f, 50f) : Rn(-6f, 10f);
                Vector2 p = new Vector2(ShoreX(z) + d, z);
                float h = Gnd(p);
                if (h < WorldField.WaterY + 0.45f || Edge(p) < 2.6f || !Claim(p, 3.2f)) continue;
                StampGround(sets[rnd.Next(sets.Length)], p, Rn(0f, 360f), 1f, 0.02f);
                placed++;
                if (Ch(0.5f))
                {
                    Vector2 q = p + new Vector2(Rn(-2.5f, 2.5f), Rn(-2.5f, 2.5f));
                    if (Edge(q) > 1f && cx.Free(q, 0.6f)) { StampGround(Folk(rnd.Next(6)), q, Rn(0f, 360f), 1f, 0.02f); people++; }
                }
            }
            // beached sampans at the north end of the bay
            for (int i = 0; i < 4; i++)
            {
                float z = -150f + i * 7f;
                Vector2 p = new Vector2(ShoreX(z) - 2f + i * 1.5f, z);
                if (Gnd(p) < WorldField.WaterY + 0.2f || Edge(p) < 3f || !Claim(p, 3f)) continue;
                var sm = new PropModel();
                PG.Append(sm[PK.Props], CoastShapes.Sampan(new System.Random(40 + i)), Matrix4x4.identity);
                cx.Stamp(sm, Matrix4x4.TRS(V3(p, Gnd(p) + 0.25f), Quaternion.Euler(0f, -70f + i * 15f, 4f), Vector3.one));
            }
            // kite fliers on the dunes and in the infield
            for (int i = 0; i < 400 && kiteSpots.Count < 6; i++)
            {
                float z = Rn(-360f, -160f);
                Vector2 p = new Vector2(ShoreX(z) + Rn(26f, 70f), z);
                if (Edge(p) < 2f || !Claim(p, 12f)) continue;
                float yaw = Rn(60f, 120f);                 // back to the onshore breeze, kite over the dunes
                StampGround(Folk(rnd.Next(6)), p, yaw, 1f, 0.02f);
                people++;
                kiteSpots.Add(new Vector4(p.x, Gnd(p), p.y, yaw));
            }
            // dune shrubs (pandanus-like spiky clumps and beach morning glory)
            var shrubs = new PropModel[3];
            for (int i = 0; i < 3; i++) shrubs[i] = DuneShrub(new System.Random(510 + i));
            for (int i = 0; i < 260; i++)
            {
                float z = Rn(-400f, -130f);
                Vector2 p = new Vector2(ShoreX(z) + Rn(40f, 130f), z);
                if (Edge(p) < 2.2f || Gnd(p) < LagoonY + 0.4f || !Claim(p, 1.8f)) continue;
                StampGround(shrubs[rnd.Next(3)], p, Rn(0f, 360f), Rn(0.8f, 1.3f), 0.1f);
            }
        }

        PropModel[] folkModels;
        PropModel Folk(int i)
        {
            if (folkModels == null)
            {
                folkModels = new PropModel[6];
                for (int k = 0; k < 6; k++) folkModels[k] = CoastShapes.Person(new System.Random(320 + k), k % 3 == 0);
            }
            return folkModels[i % 6];
        }

        static PropModel DuneShrub(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 lA = LifeGeo.C(110, 146, 76, 60), lB = LifeGeo.C(150, 170, 92, 120), lD = LifeGeo.C(80, 112, 60, 30);
            int n = 14 + rng.Next(8);
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.4f + (float)rng.NextDouble();
                float el = 0.5f + (float)rng.NextDouble() * 0.7f;
                float L = 0.8f + (float)rng.NextDouble() * 0.7f;
                Vector3 d = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
                Vector3 side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                PG.Petal(g, Vector3.up * 0.1f, d * L + Vector3.down * 0.15f * L, side, 0.07f, lD, i % 3 == 0 ? lB : lA);
            }
            if (rng.NextDouble() < 0.5)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.3f;
                    g.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.6f, 0.12f, Mathf.Sin(a) * 0.6f), new Vector3(0.09f, 0.05f, 0.09f), 5, 2, LifeGeo.C(216, 120, 200), LifeGeo.C(180, 90, 170));
                }
            return m;
        }

        // ------------------------------------------------------------------ roadside
        void BuildRoadside()
        {
            var walls = new PropModel();
            var wg = walls[PK.Landmark];
            GuardWalls(wg, RCoast);
            GuardWalls(wg, RLight);
            GuardWalls(wg, RRidge);
            cx.Stamp(walls, Matrix4x4.identity);

            var coast = Road(RCoast);
            if (coast == null) return;
            var chevron = CoastShapes.Chevron();
            var stone = KmStone();
            var lamp = CoastShapes.PromenadeLamp();
            int n = coast.Count;
            float lastChev = -99f, lastKm = 0f, lastLamp = -99f;
            for (int i = 6; i < n - 6; i++)
            {
                var s = coast[i];
                // curvature from the neighbours 12 m apart
                Vector2 a = coast[i - 6].pos, b = coast[i + 6].pos;
                Vector2 ta = (s.pos - a).normalized, tb = (b - s.pos).normalized;
                float turn = ta.x * tb.y - ta.y * tb.x;                   // + = left turn
                float ang = Mathf.Acos(Mathf.Clamp(Vector2.Dot(ta, tb), -1f, 1f));
                Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x);       // right
                if (ang > 0.36f && s.distance - lastChev > 11f)
                {
                    Vector2 outer = nr * (turn > 0f ? 1f : -1f);
                    Vector2 p = s.pos + outer * (s.halfWidth + 2.6f);
                    if (cx.Free(p, 1.2f) && Gnd(p) > s.height - 2.5f)
                    {
                        StampGround(chevron, p, YawOf(-outer), 1f, 0.1f);
                        Trunk(p, Gnd(p), 0.12f, 1.8f);
                        cx.Reserve(p, 1.2f);
                        lastChev = s.distance;
                    }
                }
                if (s.distance - lastKm > 250f)
                {
                    Vector2 p = s.pos + nr * (s.halfWidth + 1.6f);
                    if (cx.Free(p, 1f)) { StampGround(stone, p, YawOf(-nr), 1f, 0.05f); lastKm = s.distance; }
                }
                // lamps along the beach road (between the bay and the headland)
                if (s.pos.y > -385f && s.pos.y < -140f && s.distance - lastLamp > 34f)
                {
                    Vector2 sea = ShoreD(s.pos + nr * 5f) < ShoreD(s.pos - nr * 5f) ? nr : -nr;
                    Vector2 p = s.pos + sea * (s.halfWidth + 1.5f);
                    if (cx.Free(p, 1f) && Edge(p) > 0.8f)
                    {
                        StampGround(lamp, p, YawOf(s.tangent), 1f, 0.05f);
                        Trunk(p, Gnd(p), 0.15f, 4f);
                        cx.Reserve(p, 1f);
                        lastLamp = s.distance;
                    }
                }
            }
            // spectators at the best corners of the time attack
            float[] spots = { 180f, 470f, 760f, 980f, 1140f, 1320f, 1480f };
            int si = 0;
            for (int i = 0; i < n && si < spots.Length; i++)
            {
                if (coast[i].distance < spots[si]) continue;
                si++;
                var s = coast[i];
                Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 c = s.pos + nr * side * (s.halfWidth + 6.5f);
                    if (Mathf.Abs(Gnd(c) - s.height) > 2f || Edge(c) < 4f) continue;
                    int k = 3 + rnd.Next(4);
                    for (int j = 0; j < k; j++)
                    {
                        Vector2 p = c + s.tangent * (j * 1.1f - k * 0.5f) + nr * side * Rn(0f, 1.5f);
                        if (!Claim(p, 0.5f)) continue;
                        StampGround(Folk(rnd.Next(6)), p, YawOf(-nr * side) + Rn(-25f, 25f), 1f, 0.02f);
                        people++;
                    }
                    break;
                }
            }
            // junction signs
            JunctionSign(RLight, 1);
            JunctionSign(RStreet, 2);
            JunctionSign(RBeachLink, 1);
            JunctionSign(RRidge, 1);
            JunctionSign(RHills, 2);
            // the pine-ridge pagoda (a skyline landmark) and a hilltop pavilion above the south bay
            float pg = Gnd(PineHill);
            cx.StampSkyline(CoastShapes.Pagoda(5), V3(PineHill, pg - 0.3f), 200f);
            BoxCol(PineHill, pg, new Vector3(9f, 8f, 9f), 200f);
            cx.Reserve(PineHill, 9f);
            Vector2 sh = SouthHill + new Vector2(-10f, 6f);
            if (Edge(sh) > 5f)
            {
                StampGround(CoastShapes.Pavilion(2.4f, 0.6f, Pal.GreenTile, Pal.GreenTile2), sh, 250f, 1f, 0.2f);
                BoxCol(sh, Gnd(sh), new Vector3(6f, 4f, 6f), 250f);
                cx.Reserve(sh, 5f);
            }
            houses += 2;
        }

        void GuardWalls(LifeGeo g, string road)
        {
            var rs = Road(road);
            if (rs == null) return;
            for (int side = -1; side <= 1; side += 2)
            {
                int run = 0;
                Vector3 last = Vector3.zero;
                Vector2 colA = Vector2.zero;
                float colY = 0f;
                int colN = 0;
                for (int i = 2; i < rs.Count - 2; i += 2)
                {
                    var s = rs[i];
                    Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x) * side;
                    Vector2 e = s.pos + nr * (s.halfWidth + 1.1f);
                    bool drop = Gnd(s.pos + nr * (s.halfWidth + 7f)) < s.height - 2.4f && Edge(s.pos + nr * (s.halfWidth + 7f)) > 2f;
                    if (!drop || Edge(e) < 0.6f) { run = 0; colN = 0; continue; }
                    Vector3 p = V3(e, Mathf.Max(Gnd(e), s.height - 0.25f));
                    if (run > 0 && (i / 2) % 4 != 3) CoastShapes.GuardWall(g, last, p, 0.7f, 0.5f, i);
                    if (colN == 0) { colA = e; colY = p.y; }
                    colN++;
                    if (colN >= 4)
                    {
                        Vector2 mid = (colA + e) * 0.5f, d = e - colA;
                        cx.Box(new Vector3(mid.x, colY + 0.35f, mid.y), new Vector3(0.55f, 1.4f, d.magnitude + 0.4f), YawOf(d));
                        colN = 0;
                    }
                    last = p;
                    run++;
                }
            }
        }

        void JunctionSign(string road, int lines)
        {
            var rs = Road(road);
            if (rs == null || rs.Count < 12) return;
            var s = rs[10];
            Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x);
            Vector2 p = s.pos + nr * (s.halfWidth + 2.2f);
            if (!cx.Free(p, 1.5f)) p = s.pos - nr * (s.halfWidth + 2.2f);
            if (!Claim(p, 1.5f)) return;
            StampGround(CoastShapes.RoadSign(lines), p, YawOf(-s.tangent), 1f, 0.1f);
            Trunk(p, Gnd(p), 0.2f, 3f);
        }

        PropModel KmStone()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, 0.3f, 0f), new Vector3(0.2f, 0.55f, 0.1f), LifeGeo.C(240, 238, 232), LifeGeo.C(214, 64, 52));
            g.Box(new Vector3(0f, 0.55f, 0.105f), new Vector3(0.12f, 0.08f, 0.005f), Pal.Ink);
            return m;
        }

        // ------------------------------------------------------------------ vegetation
        void BuildVegetation()
        {
            var palmM = new PropModel[6];
            for (int i = 0; i < palmM.Length; i++) palmM[i] = CoastShapes.Palm(new System.Random(100 + i), 7.5f + i * 0.9f);
            var pineM = new PropModel[5];
            for (int i = 0; i < pineM.Length; i++) pineM[i] = CoastShapes.CoastPine(new System.Random(200 + i), 9f + i * 1.2f);
            var mangM = new PropModel[3];
            for (int i = 0; i < 3; i++) mangM[i] = CoastShapes.Mangrove(new System.Random(230 + i), 1f + i * 0.15f);
            var bushM = new PropModel[4];
            for (int i = 0; i < 4; i++) bushM[i] = Bush(new System.Random(240 + i), i);

            // palms lining the coast road along the bay and through the village, and on the beach backs
            var coast = Road(RCoast);
            if (coast != null)
                for (int i = 0; i < coast.Count; i += 6)
                {
                    var s = coast[i];
                    bool bay = s.pos.y > -392f && s.pos.y < -120f, village = s.pos.y > 140f && s.pos.y < 330f;
                    if (!bay && !village) continue;
                    Vector2 nr = new Vector2(s.tangent.y, -s.tangent.x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!Ch(0.55f)) continue;
                        Vector2 p = s.pos + nr * side * (s.halfWidth + Rn(3f, 6f));
                        TryPalm(palmM, p);
                    }
                }
            for (int i = 0; i < 260; i++)
            {
                float z = Rn(-440f, 130f);
                Vector2 p = new Vector2(ShoreX(z) + Rn(4f, 200f), z);
                float d = ShoreD(p);
                if (z > -130f && z < 110f && d < 60f) continue;              // headland is pine country
                TryPalm(palmM, p);
            }
            for (int i = 0; i < 90; i++)
            {
                float z = Rn(150f, 360f);
                Vector2 p = new Vector2(WallX(z) + Rn(4f, 90f), z);
                TryPalm(palmM, p);
            }

            // coastal pine woods on the hills, pines dotting the headland meadow and the north point
            for (int i = 0; i < 3000 && pines < 430; i++)
            {
                Vector2 p = new Vector2(Rn(-1440f, -560f), Rn(-470f, 470f));
                float h = Gnd(p);
                if (h < 3.5f) continue;
                float hk = Mathf.Sqrt(Mathf.Pow((p.x - HeadC.x) / HeadR.x, 2f) + Mathf.Pow((p.y - HeadC.y) / HeadR.y, 2f));
                bool headland = hk < 0.85f;
                float wood = WoodMask(p.x, p.y);
                if (headland) { if (!Ch(0.35f)) continue; }
                else if (ShoreD(p) < 100f && Mathf.Abs(p.y) > 130f && p.y < 380f) continue;
                else if (wood < 0f && !Ch(0.04f)) continue;
                if (Slope(p) > 0.4f || Edge(p) < 3.5f || !InTile(p, 8f) || WorldLayout.EdgeDistance(p) < 12f) continue;
                if (!Claim(p, wood > 0.2f ? 2.6f : 3.4f)) continue;
                int k = rnd.Next(pineM.Length);
                StampGround(pineM[k], p, Rn(0f, 360f), Rn(0.8f, 1.25f), 0.3f);
                Trunk(p, h - 0.3f, 0.35f, 4f);
                pines++;
            }

            // mangroves in the shallows of the north cove
            int mg = 0;
            for (int i = 0; i < 400 && mg < 26; i++)
            {
                float z = Rn(346f, 412f);
                Vector2 p = new Vector2(ShoreX(z) + Rn(-30f, 2f), z);
                float h = Gnd(p);
                if (h > WorldField.WaterY - 0.3f || h < WorldField.WaterY - 2.6f || !Claim(p, 3.5f)) continue;
                Stamp(mangM[rnd.Next(3)], p, WorldField.WaterY, Rn(0f, 360f), Rn(0.85f, 1.3f));
                mg++;
            }

            // flowering shrubs: the village, the lagoon banks, road verges near the bay
            for (int i = 0; i < 300; i++)
            {
                Vector2 p;
                int k = i % 3;
                if (k == 0) { float z = Rn(150f, 360f); p = new Vector2(WallX(z) + Rn(4f, 100f), z); }
                else if (k == 1) { var L = Lagoons[rnd.Next(Lagoons.Length)]; float a = Rn(0f, 6.28f); p = new Vector2(L.x + Mathf.Cos(a) * L.z * 1.25f, L.y + Mathf.Sin(a) * L.w * 1.25f); }
                else p = new Vector2(Rn(-1150f, -700f), Rn(-450f, 460f));
                if (Edge(p) < 2f || Gnd(p) < 1.2f || Slope(p) > 0.35f || !Claim(p, 1.6f)) continue;
                StampGround(bushM[rnd.Next(bushM.Length)], p, Rn(0f, 360f), Rn(0.8f, 1.3f), 0.15f);
            }
        }

        void TryPalm(PropModel[] palmM, Vector2 p)
        {
            float h = Gnd(p);
            if (h < WorldField.WaterY + 0.6f || h > 16f || Edge(p) < 2.5f || Slope(p) > 0.35f || !InTile(p, 6f)) return;
            if (h < LagoonY + 0.35f && p.x > -1100f && p.y < -120f) return;
            if (!Claim(p, 2.6f)) return;
            StampGround(palmM[rnd.Next(palmM.Length)], p, Rn(0f, 360f), Rn(0.85f, 1.2f), 0.2f);
            Trunk(p, h - 0.2f, 0.28f, 3.5f);
            palms++;
        }

        static PropModel Bush(System.Random rng, int kind)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 lt = LifeGeo.C(86, 138, 70, 40), dk = LifeGeo.C(52, 96, 52, 25);
            Color32 fl = kind == 0 ? LifeGeo.C(224, 64, 150, 60) : kind == 1 ? LifeGeo.C(240, 96, 70, 60) : kind == 2 ? LifeGeo.C(250, 236, 220, 60) : LifeGeo.C(252, 196, 70, 60);
            int lumps = 3 + rng.Next(2);
            for (int i = 0; i < lumps; i++)
            {
                float a = i * 2.1f + (float)rng.NextDouble();
                Vector3 c = new Vector3(Mathf.Cos(a) * 0.5f, 0.55f + (float)rng.NextDouble() * 0.3f, Mathf.Sin(a) * 0.5f);
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(0.75f, 0.6f, 0.75f), 6, 4, lt, dk);
                PG.Lumpy(g, s, c, 0.2f, i * 3.3f + kind);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = (float)rng.NextDouble() * 6.28f, el = 0.2f + (float)rng.NextDouble() * 1.1f;
                Vector3 p = new Vector3(Mathf.Cos(a) * Mathf.Cos(el) * 1.0f, 0.75f + Mathf.Sin(el) * 0.6f, Mathf.Sin(a) * Mathf.Cos(el) * 1.0f);
                g.Ellipsoid(p, new Vector3(0.11f, 0.08f, 0.11f), 4, 2, fl, LifeGeo.Shade(fl, 0.8f));
            }
            return m;
        }
    }
}

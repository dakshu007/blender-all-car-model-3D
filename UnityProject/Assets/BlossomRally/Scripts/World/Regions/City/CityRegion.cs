// CityRegion.cs - Jade Harbour City (港城), the south-east tile (x 500..1500, z -500..500). Pure half:
// terrain (plateau, ramps, harbour basin, open sea), ground colours, surfaces, grass, roads, flat pads,
// spawn and the two street events. The content half lives in the *.Build.cs files of this folder.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CityRegion : Region
    {
        public override RegionId Id { get { return RegionId.City; } }
        public override string Name { get { return "Jade Harbour City"; } }
        public override string Hanzi { get { return "港城"; } }
        public override string Tagline { get { return "Neon towers, wide boulevards and the harbour bridge"; } }
        public override Color Accent { get { return new Color(0.40f, 0.85f, 0.95f); } }

        // ------------------------------------------------------------------ terrain
        public override float Height(float x, float z)
        {
            float h = CityPlan.Level(x, z);
            // the escarpment under Frost Summit rises behind the north streets
            if (z > 360f)
            {
                float k = WorldNoise.Smooth(372f, 470f, z);
                h += k * (14f + 9f * WorldNoise.Fbm(x, z, 140f, 3, 571));
            }
            // soft hummocks in the west greenbelt (Gateway Park), away from the streets
            if (x < 728f && z > -110f && z < 400f)
                h += 2.2f * WorldNoise.Smooth(728f, 690f, x) * (0.5f + 0.5f * WorldNoise.Fbm(x, z, 70f, 2, 577));
            float sd = CityPlan.ShoreSD(x, z);
            if (sd > -7.5f)
            {
                float sea = CityPlan.SeaSD(x, z);
                float floor = CityPlan.BasinFloor - 4.5f * Mathf.Clamp01((sea - 8f) / 160f);
                h = Mathf.Lerp(h, floor, WorldNoise.Smooth(-7f, -1.5f, sd));
            }
            return h;
        }

        /// <summary>Removes the outer rim where the open sea lies south of the city (headlands remain at both ends).</summary>
        public override float Shape(float x, float z, float h, float weight)
        {
            if (z > -430f || x < 540f) return h;
            float taper = WorldNoise.Smooth(560f, 700f, x) * (1f - WorldNoise.Smooth(1440f, 1530f, x));
            float k = taper * WorldNoise.Smooth(-438f, -470f, z) * weight;
            if (k <= 0f) return h;
            return h - WorldLayout.RimHeight(new Vector2(x, z)) * k;
        }

        // ------------------------------------------------------------------ ground
        static readonly Color Pave = new Color(0.66f, 0.66f, 0.64f);
        static readonly Color PaveWarm = new Color(0.74f, 0.71f, 0.66f);
        static readonly Color PlazaStone = new Color(0.80f, 0.76f, 0.70f);
        static readonly Color Granite = new Color(0.78f, 0.66f, 0.63f);
        static readonly Color QuayConcrete = new Color(0.73f, 0.72f, 0.69f);
        static readonly Color YardAsphalt = new Color(0.36f, 0.37f, 0.40f);
        static readonly Color Footprint = new Color(0.46f, 0.49f, 0.52f);
        static readonly Color Cobble = new Color(0.64f, 0.58f, 0.51f);
        static readonly Color GrassA = new Color(0.44f, 0.67f, 0.33f), GrassB = new Color(0.58f, 0.77f, 0.39f);
        static readonly Color Rock = new Color(0.56f, 0.55f, 0.52f);
        static readonly Color SeaBed = new Color(0.46f, 0.55f, 0.52f);

        static float Pattern(float x, float z, float cell)
        {
            return WorldNoise.Hash01(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(z / cell), 433);
        }

        static Color GrassAt(float x, float z)
        {
            float n = 0.5f + 0.5f * WorldNoise.Fbm(x, z, 46f, 2, 581);
            Color c = Color.Lerp(GrassA, GrassB, n);
            // azalea / camellia speckles in the parks
            float sp = Pattern(x, z, 3f);
            if (sp > 0.93f) c = Color.Lerp(c, sp > 0.97f ? new Color(0.95f, 0.62f, 0.72f) : new Color(0.98f, 0.86f, 0.90f), 0.55f);
            return c;
        }

        /// <summary>0 on the plateau / waterfront, 1 on the ramp band between them.</summary>
        static float Hillside(float x, float z)
        {
            float r = x < 1225f ? CityPlan.RampW(z) : CityPlan.RampE(z);
            return Mathf.Clamp01(Mathf.Min(r, 1f - r) * 14f);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            Vector2 p = new Vector2(x, z);
            float sd = CityPlan.ShoreSD(x, z);
            if (h < -0.4f || sd > 0.5f)
            {
                float d = Mathf.Clamp01((-h - 0.4f) / 5f);
                return Color.Lerp(new Color(0.70f, 0.68f, 0.60f), SeaBed, d);
            }
            float pat = Pattern(x, z, 4f);
            float big = WorldNoise.Fbm(x, z, 90f, 2, 583);
            Color c = Pave * (0.97f + 0.05f * pat + 0.04f * big);
            c.a = 1f;
            int zi = CityPlan.ZoneAt(x, z);
            bool green = false;
            if (zi >= 0)
            {
                var zn = CityPlan.Zones[zi];
                Vector2 d = p - zn.c;
                switch (zn.kind)
                {
                    case CityZoneKind.Lot: c = Footprint * (0.95f + 0.06f * pat); break;
                    case CityZoneKind.Plaza:
                        {
                            float rr = Mathf.Sqrt(d.x * d.x / (zn.half.x * zn.half.x) + d.y * d.y / (zn.half.y * zn.half.y));
                            float ring = Mathf.Repeat(rr * 9f, 1f);
                            c = PlazaStone * (0.96f + 0.04f * pat);
                            if (ring < 0.16f) c = Color.Lerp(c, new Color(0.56f, 0.66f, 0.64f), 0.45f);
                            break;
                        }
                    case CityZoneKind.Promenade:
                        c = Granite * (0.95f + 0.05f * pat);
                        if (Mathf.Repeat(x / 9f, 1f) < 0.18f) c = Color.Lerp(c, new Color(0.88f, 0.86f, 0.82f), 0.6f);
                        break;
                    case CityZoneKind.Quay: c = QuayConcrete * (0.96f + 0.05f * pat); break;
                    case CityZoneKind.Yard:
                        c = YardAsphalt * (0.94f + 0.08f * pat);
                        if (Mathf.Repeat(x / 15f, 1f) < 0.04f || Mathf.Repeat(z / 20f, 1f) < 0.03f) c = new Color(0.86f, 0.78f, 0.36f);
                        break;
                    case CityZoneKind.OldTown: c = Cobble * (0.94f + 0.08f * pat); break;
                    case CityZoneKind.Temple: c = new Color(0.76f, 0.68f, 0.56f) * (0.96f + 0.05f * pat); break;
                    case CityZoneKind.Stadium: c = Color.Lerp(PaveWarm, new Color(0.84f, 0.84f, 0.82f), 0.5f); break;
                    case CityZoneKind.Park:
                    case CityZoneKind.Garden:
                        green = true; break;
                }
            }
            // greenbelt west of the city and hillside parks between the plateau and the waterfront
            if (zi < 0 && x < 728f) green = true;
            float hill = Hillside(x, z);
            if (zi < 0 && hill > 0f) { c = Color.Lerp(c, GrassAt(x, z), hill); }
            if (green) c = GrassAt(x, z);
            // escarpment behind the north streets: grass, then rock
            if (z > 372f && zi < 0)
            {
                float k = WorldNoise.Smooth(372f, 400f, z);
                c = Color.Lerp(c, GrassAt(x, z) * 0.92f, k);
            }
            float steep = Mathf.Clamp01((0.84f - normal.y) * 5f);
            if (steep > 0f) c = Color.Lerp(c, Rock * (0.92f + 0.12f * big), steep * 0.85f);
            // quays: concrete apron and a wet dark band at the water line
            if (sd > -16f && h < CityPlan.Plat + 1f) c = Color.Lerp(c, QuayConcrete, WorldNoise.Smooth(-16f, -9f, sd) * 0.7f);
            float o = WorldLayout.OutsideDistance(p);
            if (o > 0f) c = Color.Lerp(c, new Color(0.58f, 0.70f, 0.62f), WorldNoise.Smooth(0f, 160f, o) * 0.5f);
            c.a = 0f;
            return c;
        }

        public override Color VergeColor(float x, float z) { return new Color(0.78f, 0.77f, 0.74f); }

        public override Surface GroundSurface(float x, float z)
        {
            int zi = CityPlan.ZoneAt(x, z);
            if (zi >= 0)
            {
                var k = CityPlan.Zones[zi].kind;
                if (k == CityZoneKind.Park || k == CityZoneKind.Garden) return Surface.Grass;
                if (k == CityZoneKind.Yard) return Surface.Asphalt;
                return Surface.Paving;
            }
            if (Hillside(x, z) > 0.5f || z > 380f || x < 728f) return Surface.Grass;
            return Surface.Paving;
        }

        public override float GrassDensity(float x, float z, float edge)
        {
            if (edge < 2.5f) return 0f;
            int zi = CityPlan.ZoneAt(x, z);
            float d = 0f;
            if (zi >= 0)
            {
                var k = CityPlan.Zones[zi].kind;
                if (k == CityZoneKind.Park || k == CityZoneKind.Garden) d = 0.5f;
            }
            else
            {
                d = Hillside(x, z) * 0.42f;
                if (x < 728f) d = Mathf.Max(d, 0.4f * WorldNoise.Smooth(728f, 715f, x));
                if (z > 380f) d = Mathf.Max(d, 0.22f * WorldNoise.Smooth(380f, 410f, z) * (1f - WorldNoise.Smooth(470f, 520f, z)));
            }
            if (d <= 0f) return 0f;
            if (CityPlan.ShoreSD(x, z) > -12f) return 0f;
            return d * Mathf.Clamp01((edge - 2.5f) / 3f);
        }

        public override void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            root = new Color(0.18f, 0.32f, 0.16f);
            tip = r01 < 0.5f ? new Color(0.52f, 0.76f, 0.36f) : r01 < 0.85f ? new Color(0.62f, 0.80f, 0.40f) : new Color(0.74f, 0.82f, 0.46f);
        }

        // ------------------------------------------------------------------ roads
        public const string RoadJade = "Road_City_JadeAvenue", RoadHarbour = "Road_City_HarbourStreet", RoadCircus = "Road_City_NeonCircus";

        static RoadDef Flat(string n, Surface s, float w, Vector2[] p, bool closed = false)
        {
            return new RoadDef(n, s, w, closed, p) { fixedHeight = true, heightOffset = CityPlan.Plat, cutWidth = 7f };
        }

        public override void AddRoads(List<RoadDef> roads)
        {
            var jade = Flat(RoadJade, Surface.Asphalt, 12f, CityPlan.JadeAvenue);
            float d0 = CityPlan.BridgeX0 - CityPlan.Gate.x, d1 = CityPlan.BridgeX1 - CityPlan.Gate.x;
            jade.bridgeSpans = new List<Vector2> { new Vector2(d0, d1) };
            roads.Add(jade);
            roads.Add(Flat("Road_City_CentralAvenue", Surface.Asphalt, 11f, CityPlan.CentralAvenue));
            roads.Add(Flat("Road_City_NorthAvenue", Surface.Asphalt, 10f, CityPlan.NorthAvenue));
            roads.Add(Flat("Road_City_WestStreet", Surface.Asphalt, 9f, CityPlan.WestStreet));
            roads.Add(Flat("Road_City_MidStreetA", Surface.Asphalt, 9f, CityPlan.MidStreetA));
            roads.Add(Flat("Road_City_MidStreetB", Surface.Asphalt, 9f, CityPlan.MidStreetB));
            roads.Add(Flat("Road_City_BankStreetA", Surface.Asphalt, 9f, CityPlan.BankStreetA));
            roads.Add(Flat("Road_City_BankStreetB", Surface.Asphalt, 9f, CityPlan.BankStreetB));
            roads.Add(Flat("Road_City_EastStreet", Surface.Asphalt, 9f, CityPlan.EastStreet));
            roads.Add(Flat("Road_City_LanternAlley", Surface.Paving, 7f, CityPlan.LanternAlley));
            var circus = Flat(RoadCircus, Surface.Asphalt, 11f, CityPlan.NeonCircus(), true);
            circus.tint = new Color(0.86f, 0.88f, 0.96f);
            roads.Add(circus);
            roads.Add(Flat("Road_City_CircusSpurS", Surface.Asphalt, 9f, CityPlan.CircusSpurS));
            roads.Add(Flat("Road_City_CircusSpurN", Surface.Asphalt, 9f, CityPlan.CircusSpurN));
            roads.Add(new RoadDef(RoadHarbour, Surface.Asphalt, 10f, false, CityPlan.HarbourStreet) { cutWidth = 8f, maxGrade = 0.10f });
            roads.Add(new RoadDef("Road_City_TerraceRise", Surface.Asphalt, 9f, false, CityPlan.TerraceRise) { cutWidth = 8f });
            roads.Add(new RoadDef("Road_City_TerminalRoad", Surface.Asphalt, 10f, false, CityPlan.TerminalRoad) { cutWidth = 9f });
        }

        public override void Plan()
        {
            for (int i = 0; i < CityPlan.FlatPads.Count; i++)
            {
                Vector4 r = CityPlan.FlatPads[i];
                Field.AddFlatRect(new Vector2(r.x, r.y), new Vector2(r.z, r.w), 0f, CityPlan.FlatPadH[i * 2], CityPlan.FlatPadH[i * 2 + 1]);
            }
            // terminal road turnaround
            Field.AddPad(CityPlan.TerminalEnd, 16f, CityPlan.Low);
        }

        // ------------------------------------------------------------------ gameplay
        public override Vector3 SpawnPoint { get { return new Vector3(726f, CityPlan.Plat, -130f); } }
        public override float SpawnYaw { get { return 90f; } }

        public override void AddStages(List<StageDef> stages)
        {
            stages.Add(new StageDef
            {
                key = "CityHarbourSprint", name = "Harbour Street Sprint",
                blurb = "Time attack - barrier-lined street circuit down to the seafront and back up the hill",
                roadName = RoadHarbour, kind = StageKind.TimeAttack, startOffset = 22f, endOffset = 24f, goldSpeed = 19f
            });
            stages.Add(new StageDef
            {
                key = "CityNeonDrift", name = "Neon Night Drift",
                blurb = "Drift challenge - one lap of the Neon Circus around the lantern tower",
                roadName = RoadCircus, kind = StageKind.Drift, startOffset = 0f, limitSpeed = 9.5f,
                bronzePts = 2200, silverPts = 5000, goldPts = 8500
            });
        }
    }
}

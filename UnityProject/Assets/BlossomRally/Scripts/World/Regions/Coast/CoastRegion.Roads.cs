// CoastRegion.Roads.cs - Lotus Coast road network, flat pads, stages and spawn.
//   Coast Road (asphalt)      east gateway -> pine hills -> south clifftops -> behind the bay dunes -> over the
//                             headland neck -> harbour back street -> up to the hill road (time attack).
//   Pine Hill Road (gravel)   north gateway -> south through the coastal pine hills -> joins the coast road.
//   Sunset Beach (sand loop)  on the beach and dunes of the south bay (drift event), with a short access track.
//   Lighthouse Lane (gravel)  along the granite headland to the lighthouse.
//   Fisherman's Street (paving) the seafood market street of the stilt harbour, both ends on the coast road.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        public const string RCoast = "Road_Coast_CoastRoad", RHills = "Road_Coast_PineHillRoad", RBeach = "Road_Coast_SunsetBeach",
                            RBeachLink = "Road_Coast_BeachAccess", RLight = "Road_Coast_LighthouseLane", RStreet = "Road_Coast_FishermansStreet",
                            RRidge = "Road_Coast_PineRidge", RCove = "Road_Coast_MangroveCove", RCape = "Road_Coast_SouthCape";

        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        public static readonly Vector2[] CoastRoadPts =
        {
            WorldLayout.GateCoastEast, V(-735f, 4f), V(-756f, -60f), V(-762f, -130f), V(-772f, -200f), V(-792f, -268f), V(-832f, -328f),
            V(-890f, -370f), V(-960f, -396f), V(-1030f, -404f), V(-1084f, -390f), V(-1104f, -350f), V(-1082f, -296f), V(-1080f, -236f),
            V(-1096f, -184f), V(-1118f, -140f), V(-1142f, -94f), V(-1160f, -44f), V(-1168f, 10f), V(-1160f, 66f), V(-1146f, 114f),
            V(-1126f, 160f), V(-1118f, 200f), V(-1124f, 240f), V(-1130f, 280f), V(-1124f, 318f), V(-1090f, 340f), V(-1044f, 330f),
            V(-1000f, 297f),
        };

        public static readonly Vector2[] HillRoadPts =
        {
            WorldLayout.GateCoastNorth, V(-985f, 260f), V(-962f, 200f), V(-930f, 140f), V(-912f, 70f), V(-918f, 0f), V(-940f, -70f),
            V(-952f, -150f), V(-938f, -230f), V(-908f, -300f), V(-890f, -370f),
        };

        public static readonly Vector2[] BeachLoopPts =
        {
            V(-1176f, -352f), V(-1155f, -320f), V(-1147f, -280f), V(-1152f, -240f), V(-1170f, -202f), V(-1186f, -176f), V(-1180f, -154f),
            V(-1158f, -154f), V(-1146f, -176f), V(-1130f, -204f), V(-1112f, -240f), V(-1106f, -280f), V(-1114f, -320f), V(-1134f, -352f),
            V(-1150f, -374f), V(-1168f, -372f),
        };
        public static readonly Vector2[] BeachLinkPts = { V(-1081f, -250f), V(-1096f, -254f), V(-1109f, -258f) };
        public static readonly Vector2[] LighthousePts =
        {
            V(-1168f, 8f), V(-1196f, 22f), V(-1232f, 34f), V(-1270f, 36f), V(-1300f, 24f), V(-1312f, 2f), V(-1326f, -12f), V(-1344f, -10f),
        };
        public static readonly Vector2[] CoveTrackPts = { V(-1123f, 315f), V(-1146f, 344f), V(-1178f, 360f), V(-1212f, 370f), V(-1238f, 382f) };
        public static readonly Vector2[] CapeLanePts = { V(-1022f, -405f), V(-1058f, -424f), V(-1100f, -434f), V(-1140f, -437f), V(-1180f, -433f), V(-1214f, -423f) };
        public static readonly Vector2[] StreetPts =
        {
            V(-1124f, 174f), V(-1148f, 180f), V(-1164f, 204f), V(-1160f, 240f), V(-1168f, 272f), V(-1160f, 298f), V(-1133f, 305f),
        };
        public static readonly Vector2[] RidgeLoopPts =
        {
            V(-913f, 60f), V(-872f, 84f), V(-826f, 112f), V(-796f, 168f), V(-802f, 236f), V(-842f, 290f), V(-900f, 306f), V(-950f, 272f),
            V(-968f, 236f),
        };

        public override void AddRoads(List<RoadDef> roads)
        {
            roads.Add(new RoadDef(RCoast, Surface.Asphalt, 9f, false, CoastRoadPts) { cutWidth = 12f });
            roads.Add(new RoadDef(RHills, Surface.Gravel, 8f, false, HillRoadPts) { cutWidth = 12f });
            roads.Add(new RoadDef(RBeach, Surface.Sand, 10f, true, BeachLoopPts) { cutWidth = 8f, lines = false, maxGrade = 0.08f });
            roads.Add(new RoadDef(RBeachLink, Surface.Sand, 8f, false, BeachLinkPts) { cutWidth = 8f, lines = false });
            roads.Add(new RoadDef(RLight, Surface.Gravel, 7f, false, LighthousePts) { cutWidth = 10f, maxGrade = 0.11f });
            roads.Add(new RoadDef(RStreet, Surface.Paving, 8f, false, StreetPts) { cutWidth = 6f, lines = false });
            roads.Add(new RoadDef(RRidge, Surface.Dirt, 8f, false, RidgeLoopPts) { cutWidth = 10f });
            roads.Add(new RoadDef(RCove, Surface.Dirt, 7f, false, CoveTrackPts) { cutWidth = 8f });
            roads.Add(new RoadDef(RCape, Surface.Gravel, 7f, false, CapeLanePts) { cutWidth = 10f, maxGrade = 0.11f });
        }

        public override void Plan()
        {
            var f = Field;
            int li = f.RoadIndex(RLight);
            if (li >= 0)
            {
                var l = f.roadSamples[li];
                float h = l[l.Count - 1].height;
                f.AddPad(new Vector2(-1352f, -5f), 15f, h);
                f.AddPad(KeeperPos, 8.5f, h);
            }
            f.AddPad(PineHill, 9f, f.BaseHeight(PineHill.x, PineHill.y));
        }

        public override Vector3 SpawnPoint { get { return new Vector3(-1081f, 0f, -265f); } }
        public override float SpawnYaw { get { return 0f; } }

        public override void AddStages(List<StageDef> stages)
        {
            stages.Add(new StageDef
            {
                key = "LotusCoastRoad", name = "Lotus Coast Road", roadName = RCoast, kind = StageKind.TimeAttack,
                blurb = "Time attack - clifftops, the sandy bay and over the lighthouse headland on coastal asphalt",
                startOffset = 40f, endOffset = 30f, goldSpeed = 24f
            });
            stages.Add(new StageDef
            {
                key = "SunsetBeachDrift", name = "Sunset Beach Drift", roadName = RBeach, kind = StageKind.Drift,
                blurb = "Drift challenge - slide the sand loop between the surf and the dunes before the sun goes down",
                startOffset = 20f, limitSpeed = 11f, bronzePts = 2000, silverPts = 5000, goldPts = 8500
            });
        }
    }
}

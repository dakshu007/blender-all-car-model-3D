// Place.cs - one enterable place (tea house, temple, showroom...) and the table of all places in the world.
// Existing buildings are found by their collider signature (PlaceSites.FindBox); new buildings (car showroom,
// bike shop) get a free site next to a road. Positions / sizes are read from the region and WorldProps code
// (comments name the source) - the region files themselves are never referenced, so isolated region compiles work.
using UnityEngine;

namespace BlossomRally
{
    public enum PlaceKind { TeaHouse, Temple, Showroom, BikeShop, SkiLodge, OasisTea, Restaurant }

    public sealed class Place
    {
        // ---- definition
        public string id, name, hanzi, hint;
        public char glyph;
        public PlaceKind kind;
        public RegionId region;
        public MapIcon icon;
        public string jobBoard;           // GameplayHooks.OpenJobBoard id (null = none)
        public string showroom;           // GameplayHooks.OpenShowroom id (null = none)
        public int variant;               // look variant of the interior

        // how to find an existing building: collider size (negative = any axis), tolerance, near point, search radius
        internal bool existing;
        internal bool inWorldProps;       // valley buildings (WorldProps colliders) instead of a region root
        internal Vector3 colSize;
        internal float colTol = 0.08f, searchR = 40f;
        internal Vector2 near;
        /// <summary>Door in the found collider's local frame (x, z; y ignored), outward direction = sign of localOut.</summary>
        internal Vector3 doorLocal;
        internal Vector3 outLocal = Vector3.forward;
        internal bool doorOnColliderBase;  // floor = collider bottom + doorLift (raised platforms), else terrain
        internal float doorLift;
        // front steps of WorldProps platforms: the door goes past the last step (count from the platform height)
        internal float stepFrom, stepRise, stepDepth = 0.32f;
        internal int stepMax;
        internal bool stepFromSize;        // step count from the collider height (temple) instead of top - ground (tea house)

        // new building: preferred point, search radius, footprint half sizes (front width / depth)
        internal Vector2 prefer;
        internal float prefR = 80f, hx = 10f, hz = 8f;

        // ---- resolved at load
        public bool Ready { get; internal set; }
        /// <summary>Door point on the ground outside (world) and the yaw facing OUT of the building.</summary>
        public Vector3 Door { get; internal set; }
        public float OutYaw { get; internal set; }
        /// <summary>Suggested parking spot (world) and its heading.</summary>
        public Vector3 Parking { get; internal set; }
        public float ParkingYaw { get; internal set; }
        public bool HasViewpoint { get; internal set; }
        public Vector3 Viewpoint { get; internal set; }
        public float ViewpointYaw { get; internal set; }
        /// <summary>
        /// Y of the pocket interiors (same x / z as the building). Far ABOVE the world rather than below it: the on-foot
        /// controller treats anything under the water level as "too deep" and below -60 m as fallen out of the world.
        /// </summary>
        public const float PocketY = 900f;

        internal Transform exterior;
        internal InteriorSpace interior;
        internal MapPoi poi;
        internal Vector3 buildingPos;      // new buildings: floor centre
        internal float buildingYaw;

        public Vector3 Forward { get { return Quaternion.Euler(0f, OutYaw, 0f) * Vector3.forward; } }
    }

    internal static class PlaceTable
    {
        public static Place[] Build()
        {
            return new[]
            {
                // ---- Blossom Valley: WorldProps.BuildTeaHouse - main platform collider 13.2 x (dT-dB) x 15.2, front (+Z)
                // faces the lake, steps beyond z = 7.6
                new Place
                {
                    id = "valley_tea", name = "Lakeside Tea House", hanzi = "湖畔茶馆", glyph = '茶', kind = PlaceKind.TeaHouse, region = RegionId.Valley,
                    icon = MapIcon.Town, hint = "Tea, cakes and a quiet table by the lotus lake", jobBoard = "teahouse_valley",
                    existing = true, inWorldProps = true, colSize = new Vector3(13.2f, -1f, 15.2f), near = MapData.LakeCenter, searchR = 140f,
                    doorLocal = new Vector3(0f, 0f, 7.6f), outLocal = Vector3.forward, variant = 0,
                    stepFrom = 7.6f, stepRise = 0.2f, stepMax = 6
                },
                // ---- Blossom Valley: WorldProps.BuildTemple - platform collider 12.4 x (bt-bb) x 9.4, front (+Z) faces the road,
                // steps from z = 4.7 (door offset computed from the step count)
                new Place
                {
                    id = "valley_temple", name = "Blossom Temple", hanzi = "花谷寺", glyph = '寺', kind = PlaceKind.Temple, region = RegionId.Valley,
                    icon = MapIcon.Temple, hint = "Ring the bell, light incense and make a wish",
                    existing = true, inWorldProps = true, colSize = new Vector3(12.4f, -1f, 9.4f), near = new Vector2(42f, 345f), searchR = 120f,
                    doorLocal = new Vector3(0f, 0f, 4.7f), outLocal = Vector3.forward, variant = 0,
                    stepFrom = 4.7f, stepRise = 0.22f, stepMax = 14, stepFromSize = true
                },
                // ---- Jade Harbour City: Old Town shophouse on Lantern Alley (CityRegion.BuildOldTown) - south row, collider
                // 2hw x 7 x 9.2 centred at z = 129.1, front faces the alley (+Z), pavement at z ~ 135.4
                new Place
                {
                    id = "city_tea", name = "Jade Leaf Tea Shop", hanzi = "玉叶茶庄", glyph = '茶', kind = PlaceKind.TeaHouse, region = RegionId.City,
                    icon = MapIcon.Town, hint = "Old Town tea shop on Lantern Alley", jobBoard = "teahouse_city",
                    existing = true, colSize = new Vector3(-1f, 7f, 9.2f), colTol = 0.05f, near = new Vector2(796f, 129.1f), searchR = 30f,
                    doorLocal = new Vector3(0f, 0f, 6.25f), outLocal = Vector3.forward, variant = 1
                },
                // ---- Lotus Coast: qilou on Fisherman's Street (CoastRegion.BuildMarketStreet, land side) - collider W x 10 x 11,
                // front (+Z) faces the street
                new Place
                {
                    id = "coast_seafood", name = "Lotus Seafood House", hanzi = "莲岸海鲜", glyph = '鱼', kind = PlaceKind.Restaurant, region = RegionId.Coast,
                    icon = MapIcon.Harbour, hint = "Steamed fish, crab and a sea view", jobBoard = "restaurant_coast",
                    existing = true, colSize = new Vector3(-1f, 10f, 11f), colTol = 0.05f, near = new Vector2(-1166f, 236f), searchR = 60f,
                    doorLocal = new Vector3(0f, 0f, 6.1f), outLocal = Vector3.forward, variant = 0
                },
                // ---- Cloud Peak: golden main hall of the monastery (MountainsLandmarks: hall at MonaLocal(0.8, 38) on T2, collider
                // 23 x 7 x 17 at yaw 12; the hall faces yaw 192 = collider -Z, steps to z = 10.8)
                new Place
                {
                    id = "mountain_monastery", name = "Golden Monastery", hanzi = "金顶禅寺", glyph = '禅', kind = PlaceKind.Temple, region = RegionId.Mountains,
                    icon = MapIcon.Temple, hint = "Main hall of the summit monastery",
                    existing = true, colSize = new Vector3(23f, 7f, 17f), near = new Vector2(222f, 1423f), searchR = 30f,
                    doorLocal = new Vector3(0f, 0f, -10.3f), outLocal = Vector3.back, variant = 1
                },
                // ---- Jade Harbour City: new glass showroom on free ground by a street (west greenbelt preferred)
                new Place
                {
                    id = "city_showroom", name = "Jade Motors", hanzi = "玉龙车行", glyph = '车', kind = PlaceKind.Showroom, region = RegionId.City,
                    icon = MapIcon.Garage, hint = "Car showroom - buy a new car", showroom = "city_cars",
                    existing = false, prefer = new Vector2(694f, 30f), prefR = 170f, hx = 12f, hz = 9f
                },
                // ---- Lotus Coast: new scooter / bike shop by the coast road
                new Place
                {
                    id = "coast_bikes", name = "Sea Breeze Scooters", hanzi = "海风车行", glyph = '车', kind = PlaceKind.BikeShop, region = RegionId.Coast,
                    icon = MapIcon.Garage, hint = "Motorbikes and scooters", showroom = "coast_bikes",
                    existing = false, prefer = new Vector2(-1098f, 140f), prefR = 150f, hx = 6.5f, hz = 5.5f
                },
                // ---- Frost Summit: the big lodge of the ski village (SnowRegion.BuildVillage: chalet v == 2, collider
                // 16.4 x 6.4 x 10.4, model -Z = front)
                new Place
                {
                    id = "snow_lodge", name = "Frost Summit Ski Lodge", hanzi = "雪顶山屋", glyph = '雪', kind = PlaceKind.SkiLodge, region = RegionId.Snow,
                    icon = MapIcon.Town, hint = "Fireplace, hot chocolate and work up the mountain", jobBoard = "ski_lodge",
                    existing = true, colSize = new Vector3(16.4f, 6.4f, 10.4f), near = new Vector2(948f, 1170f), searchR = 120f,
                    doorLocal = new Vector3(0f, 0f, -6.4f), outLocal = Vector3.back, variant = 0
                },
                // ---- Red Canyon: Crescent Spring tea house (CanyonRegion.BuildOasis: hall collider 9.8 x 3.6 x 5.2 at yaw -90,
                // terrace towards the building's +Z; platform top 0.25 above the base)
                new Place
                {
                    id = "canyon_oasis", name = "Crescent Spring Tea House", hanzi = "月泉茶馆", glyph = '茶', kind = PlaceKind.OasisTea, region = RegionId.Canyon,
                    icon = MapIcon.Town, hint = "Silk Road tea in the shade of the oasis", jobBoard = "oasis_tea",
                    existing = true, colSize = new Vector3(9.8f, 3.6f, 5.2f), near = new Vector2(-1222.5f, 1046f), searchR = 25f,
                    doorLocal = new Vector3(0f, 0f, 3.6f), outLocal = Vector3.forward, doorOnColliderBase = true, doorLift = 0.25f, variant = 0
                },
            };
        }
    }
}

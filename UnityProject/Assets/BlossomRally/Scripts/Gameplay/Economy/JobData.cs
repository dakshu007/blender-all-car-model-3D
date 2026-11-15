// JobData.cs - the places jobs start and end at (named spots in all six regions, snapped to the nearest road
// when the world is built) and the job catalogue: tea deliveries, taxi fares, courier runs between regions,
// photo commissions, Frost Summit supply runs and Red Canyon expeditions. Cosy, non-violent work only.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum JobKind { Tea = 0, Taxi = 1, Courier = 2, Photo = 3, Snow = 4, Desert = 5 }

    public sealed class JobPlace
    {
        public string name;
        public RegionId region;
        public Vector2 raw;           // design position (landmark)
        public Vector3 pos;           // roadside point (after Resolve)
        public Vector3 roadDir;       // road tangent there
        public bool resolved, onRoad;
    }

    public sealed class JobDef
    {
        public int index;
        public JobKind kind;
        public RegionId region;
        public string title, client, flavour, cargo;
        public JobPlace from, to;
        public JobPlace[] via;        // extra stops before 'to' (expeditions)
        public float lastDone = -9999f;

        // ---- derived (computed per accept from the actual start point)
        public float RouteLength(Vector3 start)
        {
            float d = 0f;
            Vector3 p = start;
            if (via != null) foreach (var v in via) { d += Flat(p, v.pos); p = v.pos; }
            d += Flat(p, to.pos);
            return d * 1.3f;          // straight line -> roughly by road
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }

    public static class JobData
    {
        public static readonly List<JobPlace> Places = new List<JobPlace>();
        public static readonly List<JobDef> Jobs = new List<JobDef>();
        static readonly Dictionary<string, JobPlace> byName = new Dictionary<string, JobPlace>();
        static bool built;
        static WorldBuilder resolvedFor;

        public static readonly string[] KindNames = { "Tea delivery", "Taxi fare", "Courier", "Photo commission", "Supply run", "Expedition" };
        public static readonly string[] KindTags = { "TEA", "TAXI", "COURIER", "PHOTO", "SUPPLY", "EXPEDITION" };
        public static readonly Color[] KindColours =
        {
            new Color(0.62f, 0.90f, 0.55f, 1f),   // tea green
            new Color(1f, 0.82f, 0.30f, 1f),      // taxi yellow
            new Color(0.55f, 0.78f, 1f, 1f),      // courier blue
            new Color(1f, 0.66f, 0.84f, 1f),      // photo pink
            new Color(0.80f, 0.92f, 1f, 1f),      // snow ice
            new Color(1f, 0.62f, 0.38f, 1f),      // desert sand
        };
        /// <summary>Pay per kind: base + per km (by road), before the region factor.</summary>
        static readonly float[] BasePay = { 120f, 150f, 300f, 260f, 300f, 420f };
        static readonly float[] PerKm = { 260f, 300f, 280f, 120f, 380f, 360f };
        /// <summary>Average speed (m/s) the time limit is built on.</summary>
        static readonly float[] LimitSpeed = { 13f, 14f, 16f, 0f, 10.5f, 11.5f };

        public static float RegionFactor(RegionId r)
        {
            switch (r)
            {
                case RegionId.Snow: case RegionId.Canyon: return 1.3f;
                case RegionId.Mountains: return 1.2f;
                case RegionId.City: return 1.05f;
                case RegionId.Valley: return 0.95f;
                default: return 1f;
            }
        }

        public static int Pay(JobDef j, float route)
        {
            int k = (int)j.kind;
            float p = (BasePay[k] + PerKm[k] * route * 0.001f) * RegionFactor(j.region);
            return Mathf.Max(50, Mathf.RoundToInt(p / 5f) * 5);
        }

        /// <summary>Seconds allowed (0 = no limit).</summary>
        public static float TimeLimit(JobDef j, float route)
        {
            float v = LimitSpeed[(int)j.kind];
            if (v <= 0f) return 0f;
            return Mathf.Round(35f + route / v);
        }

        public static JobPlace Place(string name) { JobPlace p; return byName.TryGetValue(name, out p) ? p : null; }

        // ------------------------------------------------------------------ places
        static void P(RegionId r, string name, Vector2 p)
        {
            var pl = new JobPlace { name = name, region = r, raw = p, pos = new Vector3(p.x, 0f, p.y), roadDir = Vector3.forward };
            Places.Add(pl);
            byName[name] = pl;
        }

        static void BuildPlaces()
        {
            // Blossom Valley (blueprint positions from MapData)
            P(RegionId.Valley, "Lantern Street Tea House", MapData.P(600f, 1420f));
            P(RegionId.Valley, "Paifang Gate", MapData.Gate);
            P(RegionId.Valley, "Village Garage", MapData.Garage);
            P(RegionId.Valley, "Lotus Lake Pavilion", MapData.LakeCenter);
            P(RegionId.Valley, "Mountain Pagoda", MapData.PagodaTop);
            P(RegionId.Valley, "Tea Terraces", MapData.P(1330f, 900f));
            P(RegionId.Valley, "Bamboo Grove", MapData.P(200f, 600f));
            P(RegionId.Valley, "East Lookout", MapData.Lookouts[2]);
            P(RegionId.Valley, "Willow Lookout", MapData.Lookouts[1]);
            // Jade Harbour City
            P(RegionId.City, "Jade Plaza Taxi Rank", new Vector2(810f, -88f));
            P(RegionId.City, "Moon Gate Tea House", new Vector2(858f, 140f));
            P(RegionId.City, "Old Town Temple", new Vector2(812f, 192f));
            P(RegionId.City, "Neon Circus", new Vector2(1227f, 140f));
            P(RegionId.City, "Ferry Pier", new Vector2(1000f, -445f));
            P(RegionId.City, "Dragon Stadium", new Vector2(982f, 312f));
            P(RegionId.City, "Gateway Arch", new Vector2(712f, -130f));
            P(RegionId.City, "Container Terminal", new Vector2(1388f, -398f));
            P(RegionId.City, "Harbour Bridge", new Vector2(1225f, -130f));
            P(RegionId.City, "Harbour Lighthouse", new Vector2(1262f, -575f));
            // Lotus Coast
            P(RegionId.Coast, "Lotus Harbour", new Vector2(-1178f, 248f));
            P(RegionId.Coast, "Sea Breeze Tea House", new Vector2(-1186f, 300f));
            P(RegionId.Coast, "Mazu Temple", new Vector2(-1192f, 334f));
            P(RegionId.Coast, "Headland Lighthouse", new Vector2(-1362f, -4f));
            P(RegionId.Coast, "Pine Hill Pagoda", new Vector2(-850f, 196f));
            P(RegionId.Coast, "Sunset Bay", new Vector2(-1140f, -320f));
            P(RegionId.Coast, "Lotus Lagoons", new Vector2(-1030f, -240f));
            // Cloud Peak Mountains
            P(RegionId.Mountains, "Cable Car Tea Stall", new Vector2(112f, 1162f));
            P(RegionId.Mountains, "Golden Monastery", new Vector2(214f, 1386f));
            P(RegionId.Mountains, "Jade Lake", new Vector2(176f, 1106f));
            P(RegionId.Mountains, "Valley Falls", new Vector2(-40f, 604f));
            P(RegionId.Mountains, "Gorge Lookout", new Vector2(-48f, 868f));
            P(RegionId.Mountains, "Cloud-Sea Ridge", new Vector2(330f, 788f));
            P(RegionId.Mountains, "Central Col", new Vector2(-50f, 985f));
            // Frost Summit
            P(RegionId.Snow, "Ski Lodge", new Vector2(948f, 1150f));
            P(RegionId.Snow, "Summit Observatory", new Vector2(1246f, 1392f));
            P(RegionId.Snow, "Frozen Lake", new Vector2(1100f, 900f));
            P(RegionId.Snow, "Wind Turbine Ridge", new Vector2(1450f, 790f));
            P(RegionId.Snow, "Cliff Rim Viewpoint", new Vector2(1242f, 529f));
            // Red Canyon
            P(RegionId.Canyon, "Oasis Tea House", new Vector2(-1227f, 1046f));
            P(RegionId.Canyon, "Caravanserai", new Vector2(-994f, 1290f));
            P(RegionId.Canyon, "Watchtower", new Vector2(-1062f, 1394f));
            P(RegionId.Canyon, "Red Citadel", new Vector2(-842f, 800f));
            P(RegionId.Canyon, "Salt Playa", new Vector2(-1330f, 730f));
            P(RegionId.Canyon, "Rainbow Hills", new Vector2(-786f, 1338f));
            P(RegionId.Canyon, "The Slot", new Vector2(-832f, 933f));
            P(RegionId.Canyon, "Gorge Bridge", new Vector2(-929.5f, 1093f));
        }

        /// <summary>Snap every place to the roadside of its nearest road (call once the world is built).</summary>
        public static void Resolve(WorldBuilder w)
        {
            Ensure();
            if (w == null || resolvedFor == w) return;
            resolvedFor = w;
            foreach (var pl in Places)
            {
                RoadSample s;
                float d = w.NearestRoad(pl.raw, out s, 260f);
                if (d < float.MaxValue && !s.bridge)
                {
                    Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
                    float side = Vector2.Dot(pl.raw - s.pos, right) >= 0f ? 1f : -1f;
                    Vector2 q = s.pos + right * side * (s.halfWidth + 2.5f);
                    float h = Mathf.Max(s.height, w.TerrainHeight(q.x, q.y));
                    pl.pos = new Vector3(q.x, h, q.y);
                    pl.roadDir = new Vector3(s.tangent.x, 0f, s.tangent.y);
                    pl.onRoad = true;
                }
                else
                {
                    pl.pos = new Vector3(pl.raw.x, w.TerrainHeight(pl.raw.x, pl.raw.y), pl.raw.y);
                    pl.onRoad = false;
                }
                pl.resolved = true;
            }
        }

        // ------------------------------------------------------------------ jobs
        static void J(JobKind k, RegionId r, string from, string to, string title, string client, string cargo, string text, params string[] via)
        {
            var a = Place(from);
            var b = Place(to);
            if (a == null || b == null) { Debug.LogWarning("[BlossomRally] job '" + title + "': unknown place"); return; }
            JobPlace[] v = null;
            if (via != null && via.Length > 0)
            {
                v = new JobPlace[via.Length];
                for (int i = 0; i < via.Length; i++) { v[i] = Place(via[i]); if (v[i] == null) return; }
            }
            Jobs.Add(new JobDef { index = Jobs.Count, kind = k, region = r, from = a, to = b, via = v, title = title, client = client, cargo = cargo, flavour = text });
        }

        public static void Ensure()
        {
            if (built) return;
            built = true;
            BuildPlaces();
            const JobKind T = JobKind.Tea, X = JobKind.Taxi, C = JobKind.Courier, F = JobKind.Photo, S = JobKind.Snow, D = JobKind.Desert;
            const RegionId V = RegionId.Valley, CI = RegionId.City, CO = RegionId.Coast, M = RegionId.Mountains, SN = RegionId.Snow, CA = RegionId.Canyon;

            // ---------------- tea deliveries (hot and steady: hard knocks spill the cup)
            J(T, V, "Lantern Street Tea House", "Lotus Lake Pavilion", "Lakeside Oolong", "Grandma Lin", "a pot of oolong",
              "Grandma Lin is feeding the koi and would love a pot of oolong. Keep it hot, and keep it in the pot.");
            J(T, V, "Lantern Street Tea House", "Mountain Pagoda", "Pagoda Pu'er", "the pagoda caretaker", "a flask of aged pu'er",
              "Aged pu'er for the old caretaker at the top of the hairpins. He says a sloshy cup ruins the view.");
            J(T, V, "Lantern Street Tea House", "Tea Terraces", "Pickers' Break", "the terrace pickers", "jasmine tea for twelve",
              "Jasmine tea for the pickers' afternoon break. They grew the leaves - the least we can do is bring it warm.");
            J(T, V, "Lantern Street Tea House", "Bamboo Grove", "Bamboo Picnic", "the Zhou family", "a basket of green tea and buns",
              "A family picnic in the bamboo has run out of tea. The little one insists on 'the steamy kind'.");
            J(T, CI, "Moon Gate Tea House", "Neon Circus", "Night-Shift Jasmine", "the TV tower crew", "a tray of jasmine tea",
              "The lantern tower crew are rewiring the lights tonight. Hot jasmine keeps their hands steady.");
            J(T, CI, "Moon Gate Tea House", "Ferry Pier", "Captain's Cup", "Captain Ho", "a thermos of Iron Goddess",
              "Captain Ho will not cast off without her Iron Goddess oolong. The ferry waits for nobody else.");
            J(T, CI, "Moon Gate Tea House", "Dragon Stadium", "Half-Time Chrysanthemum", "the stadium groundskeepers", "chrysanthemum tea",
              "Chrysanthemum tea for the groundskeepers before the evening match. Smooth roads, smooth brew.");
            J(T, CO, "Sea Breeze Tea House", "Headland Lighthouse", "Keeper's Brew", "the lighthouse keeper", "a pot of smoky lapsang",
              "The keeper likes his tea smoky and his deliveries early. The headland wind will cool it fast.");
            J(T, CO, "Sea Breeze Tea House", "Sunset Bay", "Beach Picnic Tea", "a group of painters", "iced lychee tea",
              "Painters on the beach have asked for lychee tea. It's iced, but it still mustn't spill on the canvases.");
            J(T, CO, "Sea Breeze Tea House", "Pine Hill Pagoda", "Pine Ridge Pot", "Auntie Mei's tai chi class", "a big pot of white tea",
              "Auntie Mei's tai chi class finishes at the pagoda. Calm movements, calm driving, warm white tea.");
            J(T, M, "Cable Car Tea Stall", "Golden Monastery", "Monks' Morning Tea", "the monastery kitchen", "butter tea in clay jars",
              "The monks ran out of butter tea before dawn prayers. Clay jars - gentle on the hairpins, please.");
            J(T, M, "Cable Car Tea Stall", "Jade Lake", "Painter by the Lake", "Master Gao", "a pot of Dragon Well",
              "Master Gao is painting the lake and forgot his tea. Dragon Well, still steaming, if you can.");
            J(T, M, "Cable Car Tea Stall", "Gorge Lookout", "Hikers' Warm-Up", "a hiking club", "ginger tea in flasks",
              "A hiking club reached the gorge pavilion cold and happy. Ginger tea will make them happier.");
            J(T, SN, "Ski Lodge", "Summit Observatory", "Stargazers' Ginger Tea", "the night astronomers", "ginger-honey tea",
              "The astronomers are up all night. Ginger-honey tea, hot enough to fog the telescope eyepiece.");
            J(T, SN, "Ski Lodge", "Frozen Lake", "Rink-Side Warmers", "the skating club", "hot red-date tea",
              "The skating club is out on the ice. Red-date tea - it cools fast out there, so don't dawdle.");
            J(T, SN, "Ski Lodge", "Wind Turbine Ridge", "Turbine Crew Thermos", "the turbine engineers", "two big thermoses",
              "Engineers on the ridge want something warm. Steady on the snow - nobody likes a thermos dent.");
            J(T, CA, "Oasis Tea House", "Caravanserai", "Caravan Mint Tea", "the caravanserai innkeeper", "mint tea and honey cakes",
              "The innkeeper's guests arrived early. Mint tea and honey cakes, before the sand gets in the cups.");
            J(T, CA, "Oasis Tea House", "Watchtower", "Desert Rose Brew", "a history student", "rose-petal tea",
              "A student sketching the old watchtower asked for rose tea. Keep it hot - the desert wind is fickle.");
            J(T, CA, "Oasis Tea House", "Salt Playa", "Salt Flat Survey Tea", "the survey team", "jasmine pearls",
              "Surveyors on the salt playa have earned their jasmine pearls. Flat ground, no excuses for spills.");

            // ---------------- taxi fares (comfort earns the tip)
            J(X, CI, "Jade Plaza Taxi Rank", "Old Town Temple", "Exam Blessings", "Mrs Wu", "Mrs Wu",
              "Mrs Wu is lighting incense for her grandson's exams. She would like to arrive calm, not rattled.");
            J(X, CI, "Jade Plaza Taxi Rank", "Ferry Pier", "Catch the Ferry", "Mr Chen", "Mr Chen and three suitcases",
              "A tourist with three suitcases and a ferry to catch. He keeps checking his watch, very politely.");
            J(X, CI, "Jade Plaza Taxi Rank", "Dragon Stadium", "Match Day", "two football fans", "the Liu brothers",
              "Two brothers in matching scarves, singing already. Get them there for kick-off.");
            J(X, CI, "Jade Plaza Taxi Rank", "Neon Circus", "Neon Date Night", "a young couple", "Xiao and Ren",
              "A first date under the lantern tower. They'd like the scenic way, but not too scenic.");
            J(X, CO, "Lotus Harbour", "Headland Lighthouse", "Wedding Photos", "the newlyweds", "the newlyweds",
              "Newlyweds want photos at the lighthouse while the light is golden. Mind the dress.");
            J(X, CO, "Lotus Harbour", "Sunset Bay", "Surf's Up", "a surfer", "Bo and his longboard",
              "Bo heard the waves are good at Sunset Bay. His board sticks out of the window - drive gently.");
            J(X, CO, "Lotus Harbour", "Lotus Lagoons", "Lotus Watercolours", "Granny Pan", "Granny Pan and her easel",
              "Granny Pan paints the lotus pools every week. She tips in apricot sweets, and they're excellent.");

            // ---------------- courier runs between regions (time bonus)
            J(C, V, "Village Garage", "Jade Plaza Taxi Rank", "Spare Parts to Town", "Uncle Bao's garage", "a crate of spark plugs",
              "Uncle Bao promised a city mechanic some spare parts by this afternoon. Over the pass and into town.");
            J(C, V, "Lantern Street Tea House", "Lotus Harbour", "Lantern Order", "the lantern makers", "forty paper lanterns",
              "The harbour festival needs forty paper lanterns. Light as air, delicate as a promise.");
            J(C, V, "Paifang Gate", "Golden Monastery", "Temple Scrolls", "the village scribe", "hand-copied sutra scrolls",
              "The scribe finished the monastery's new scrolls. Up through the mountains, nice and quick.");
            J(C, CI, "Container Terminal", "Ski Lodge", "Ski Wax Express", "the port office", "boxes of ski wax",
              "A shipment of ski wax just landed. The lodge's ski school opens tomorrow morning.");
            J(C, CI, "Ferry Pier", "Lantern Street Tea House", "Mooncake Rush", "the harbour bakery", "a tower of mooncakes",
              "Freshly baked mooncakes for the valley tea house. They're best on the day, so make haste.");
            J(C, CI, "Neon Circus", "Caravanserai", "Neon Sign Order", "a sign maker", "a hand-bent neon sign",
              "The caravanserai ordered a neon camel for its gate. The sign maker is very proud of the hump.");
            J(C, CO, "Lotus Harbour", "Neon Circus", "Fresh Catch", "the fishing co-op", "iced boxes of prawns",
              "Prawns caught at dawn for a Neon Circus noodle bar. On ice, but the clock is ticking.");
            J(C, CO, "Mazu Temple", "Oasis Tea House", "Shells for the Oasis", "the temple gift shop", "a sack of painted shells",
              "The oasis tea house wants seashells to decorate its pool. From the sea to the sand.");
            J(C, CO, "Sunset Bay", "Lantern Street Tea House", "Kite Delivery", "the kite flyers", "a giant dragon kite",
              "A ten-metre dragon kite for the valley's spring festival. Don't let it catch the wind.");
            J(C, M, "Golden Monastery", "Moon Gate Tea House", "Calligraphy Commission", "Abbot Shen", "a framed calligraphy",
              "The abbot's calligraphy, 'Tea and Patience', is going to hang in the Moon Gate tea house.");
            J(C, M, "Cable Car Tea Stall", "Ski Lodge", "Cable Car Spares", "the cable car mechanic", "a box of pulley wheels",
              "The Frost Summit cable car needs spare pulley wheels. Heavy box, short deadline.");
            J(C, M, "Jade Lake", "Lotus Harbour", "Mountain Honey", "the lakeside beekeepers", "jars of wildflower honey",
              "Wildflower honey for the harbour market. Sticky if dropped, golden if delivered.");
            J(C, SN, "Summit Observatory", "Jade Plaza Taxi Rank", "Star Charts", "the observatory", "rolled star charts",
              "New star charts for the city planetarium. Down from the clouds, into the bright lights.");
            J(C, SN, "Ski Lodge", "Golden Monastery", "Woollen Blankets", "the lodge knitting circle", "a bale of yak-wool blankets",
              "The knitting circle made blankets for the monastery. Warm hearts travel fast.");
            J(C, SN, "Frozen Lake", "Lantern Street Tea House", "Ice for the Festival", "the ice carvers", "a block of lake ice",
              "A block of lake ice for an ice sculpture in the valley. It melts a little every minute!");
            J(C, CA, "Caravanserai", "Lantern Street Tea House", "Silk Road Spices", "the spice merchant", "sacks of star anise",
              "Star anise and cinnamon from the caravanserai for the valley kitchens. Smells wonderful.");
            J(C, CA, "Oasis Tea House", "Ski Lodge", "Dates for the Lodge", "the oasis orchard", "crates of fresh dates",
              "Fresh dates from the oasis orchard for the ski lodge's hot puddings. From sand to snow.");
            J(C, CA, "Watchtower", "Ferry Pier", "Museum Pottery", "the archaeology team", "a padded crate of pottery",
              "Ancient pottery for the harbour museum. Padded well, but treat every bump as a heartbeat.");

            // ---------------- photo commissions (reach the spot, open photo mode, take the shot)
            J(F, V, "Lantern Street Tea House", "Mountain Pagoda", "Pagoda Postcard", "Blossom Weekly", "camera",
              "Blossom Weekly wants the five-tier pagoda for next week's cover. Frame it with blossom if you can.");
            J(F, V, "Lantern Street Tea House", "Lotus Lake Pavilion", "Koi Pond Portrait", "the postcard printer", "camera",
              "The postcard printer needs a fresh picture of Lotus Lake. Koi optional but encouraged.");
            J(F, V, "Lantern Street Tea House", "East Lookout", "Terrace Panorama", "the tea growers' guild", "camera",
              "A panorama from the top of the tea terraces for the guild's new tins.");
            J(F, CI, "Moon Gate Tea House", "Harbour Bridge", "Bridge at Dusk", "Jade Harbour Tourism", "camera",
              "The tourism board wants the harbour bridge with the city behind it. Night lights earn a bonus.");
            J(F, CI, "Moon Gate Tea House", "Neon Circus", "Neon Circus Nights", "a fashion magazine", "camera",
              "A fashion magazine needs the lantern tower as a backdrop. Bring your best angle.");
            J(F, CI, "Ferry Pier", "Harbour Lighthouse", "Mole Lighthouse", "the ferry company", "camera",
              "The ferry company's new timetable needs a lighthouse on the front. Out on the harbour mole.");
            J(F, CO, "Lotus Harbour", "Headland Lighthouse", "Headland Light", "the coastal trust", "camera",
              "The coastal trust is making a calendar. March is the headland lighthouse - and it's your shot.");
            J(F, CO, "Lotus Harbour", "Pine Hill Pagoda", "Pine Ridge View", "a travel blogger", "camera",
              "A travel blogger needs the view from the Pine Hill pagoda. Islets, sea and sky.");
            J(F, CO, "Lotus Harbour", "Sunset Bay", "Golden Sands", "the beach café", "camera",
              "The beach café wants a picture of Sunset Bay for its menu. Sunset is the obvious choice.");
            J(F, M, "Cable Car Tea Stall", "Valley Falls", "Waterfall Mist", "Cloud Peak Rangers", "camera",
              "The rangers' new trail map needs the waterfall. Get close enough to feel the mist.");
            J(F, M, "Cable Car Tea Stall", "Cloud-Sea Ridge", "Sea of Clouds", "a poetry society", "camera",
              "A poetry society wants a sea of clouds to inspire this year's anthology.");
            J(F, M, "Cable Car Tea Stall", "Gorge Lookout", "Gorge Pavilion", "the mountain inn", "camera",
              "The mountain inn wants the gorge pavilion on its sign. Dramatic, but cosy.");
            J(F, SN, "Ski Lodge", "Cliff Rim Viewpoint", "City from the Clouds", "the city council", "camera",
              "The council wants Jade Harbour City from the Frost Summit cliffs for the town hall wall.");
            J(F, SN, "Ski Lodge", "Summit Observatory", "Observatory Dome", "the science museum", "camera",
              "The science museum's new exhibition needs the observatory dome against the sky.");
            J(F, SN, "Ski Lodge", "Frozen Lake", "Glass Lake", "a winter magazine", "camera",
              "The frozen lake like a mirror, for a winter magazine's centre spread.");
            J(F, CA, "Oasis Tea House", "Rainbow Hills", "Rainbow Stripes", "a geology journal", "camera",
              "A geology journal wants the striped Danxia hills. Every colour counts.");
            J(F, CA, "Oasis Tea House", "Red Citadel", "The Red Citadel", "a film location scout", "camera",
              "A film scout is looking for a mesa that 'looks like a castle'. This might be it.");
            J(F, CA, "Caravanserai", "The Slot", "Slot Canyon Light", "an art gallery", "camera",
              "An art gallery wants light falling into the slot canyon. Be patient, be still.");

            // ---------------- Frost Summit supply runs (heavy, fragile, slippery)
            J(S, SN, "Ski Lodge", "Summit Observatory", "Observatory Supplies", "the observatory cook", "firewood and dumpling soup",
              "Firewood and a vat of dumpling soup for the observatory. The road is icy - slow is smooth, smooth is fast.");
            J(S, SN, "Ski Lodge", "Wind Turbine Ridge", "Ridge Rations", "the turbine crew", "a week of rations",
              "A week of rations for the turbine crew. Noodles, tea and far too many pickles.");
            J(S, SN, "Ski Lodge", "Cliff Rim Viewpoint", "Rescue Hut Restock", "the mountain rescue", "blankets and lamp oil",
              "Restock the rescue hut by the cliff rim. Blankets, lamp oil and a tin of almond biscuits.");
            J(S, SN, "Ski Lodge", "Frozen Lake", "Ice-Fishing Huts", "the ice-fishing club", "stoves and spare rods",
              "Little stoves for the ice-fishing huts, by way of the turbine track. Fragile - and slippery.", "Wind Turbine Ridge");

            // ---------------- Red Canyon expeditions (multi-stop desert treks)
            J(D, CA, "Caravanserai", "Oasis Tea House", "The Old Caravan Route", "the caravan guild", "water skins and maps",
              "Retrace the old caravan route past the Red Citadel and arrive at the oasis by tea time.", "Red Citadel");
            J(D, CA, "Oasis Tea House", "Rainbow Hills", "Geology Field Trip", "the university", "a field kit and students' lunches",
              "Take the field kit across the salt playa to the rainbow hills. The students are already waiting.", "Salt Playa");
            J(D, CA, "Caravanserai", "The Slot", "Lost Camel Bells", "a camel herder", "a string of brass bells",
              "A herder's bells were found at the watchtower. Collect them and bring them to the slot canyon camp.", "Watchtower");
            J(D, CA, "Oasis Tea House", "Red Citadel", "Sunrise Expedition", "a photography club", "tents and tripods",
              "Tents and tripods for a sunrise shoot, by way of the gorge bridge. Rough tracks, big skies.", "Gorge Bridge");
        }

        /// <summary>Board type → which job kinds it offers. Unknown ids offer everything.</summary>
        public static bool BoardOffers(string boardId, JobKind k)
        {
            string id = boardId ?? "";
            System.StringComparison o = System.StringComparison.OrdinalIgnoreCase;
            if (id.IndexOf("phone", o) >= 0 || id.IndexOf("any", o) >= 0) return true;
            bool courierPhoto = k == JobKind.Courier || k == JobKind.Photo;
            if (id.IndexOf("tea", o) >= 0) return k == JobKind.Tea || courierPhoto;
            if (id.IndexOf("taxi", o) >= 0) return k == JobKind.Taxi || courierPhoto;
            if (id.IndexOf("harbour", o) >= 0 || id.IndexOf("harbor", o) >= 0 || id.IndexOf("pier", o) >= 0) return k == JobKind.Taxi || k == JobKind.Tea || courierPhoto;
            if (id.IndexOf("lodge", o) >= 0 || id.IndexOf("snow", o) >= 0 || id.IndexOf("frost", o) >= 0) return k == JobKind.Snow || k == JobKind.Tea || courierPhoto;
            if (id.IndexOf("oasis", o) >= 0 || id.IndexOf("caravan", o) >= 0 || id.IndexOf("desert", o) >= 0 || id.IndexOf("canyon", o) >= 0)
                return k == JobKind.Desert || k == JobKind.Tea || courierPhoto;
            if (id.IndexOf("courier", o) >= 0 || id.IndexOf("post", o) >= 0) return k == JobKind.Courier;
            if (id.IndexOf("photo", o) >= 0 || id.IndexOf("camera", o) >= 0) return k == JobKind.Photo;
            return true;
        }

        /// <summary>Board title for an id.</summary>
        public static string BoardTitle(string boardId, RegionId region)
        {
            string id = boardId ?? "";
            System.StringComparison o = System.StringComparison.OrdinalIgnoreCase;
            if (id.IndexOf("phone", o) >= 0) return "Blossom Jobs";
            if (id.IndexOf("tea", o) >= 0) return "Tea House Orders";
            if (id.IndexOf("taxi", o) >= 0) return "Taxi Rank";
            if (id.IndexOf("harbour", o) >= 0 || id.IndexOf("pier", o) >= 0) return "Harbour Notice Board";
            if (id.IndexOf("lodge", o) >= 0) return "Lodge Supply Board";
            if (id.IndexOf("oasis", o) >= 0 || id.IndexOf("caravan", o) >= 0) return "Caravan Board";
            return "Job Board";
        }
    }
}

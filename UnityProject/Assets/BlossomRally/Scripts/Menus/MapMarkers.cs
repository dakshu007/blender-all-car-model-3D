// MapMarkers.cs - everything the world map and the minimap mark, and how they draw it:
//   * MapPoi / MapMarkers: one list of points of interest - rally stage starts (icon by kind, medal ring), the
//     garage, fast-travel points (each region's Region.SpawnPoint), the six connector passes and a static table of
//     landmarks per region (positions read from the region code: lighthouses, TV tower, monastery, observatory,
//     oasis, harbour bridge, frozen lake ...). Built once the world exists, rebuilt when the event list changes.
//   * Icons: small white glyphs rasterised once from signed-distance primitives (flag, drift line, house, lantern,
//     pass, pagoda, temple, waves, bridge, lighthouse, eye, houses, peaks, dome, anchor, paifang, tree, star, pin),
//     drawn as ink-ringed badges so they read on the painted paper map at any size.
//   * MapGL: immediate-mode GL triangles in GUI coordinates (y down), either on screen inside OnGUI Repaint or into
//     a RenderTexture (the minimap), with a textured quad for the painted map.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum MapIcon
    {
        You, Waypoint, TimeAttack, Drift, Garage, FastTravel, Pass, Pagoda, Temple, Water, Bridge, Lighthouse, Viewpoint,
        Town, Peak, Observatory, Harbour, Gate, Nature, Star, Count
    }

    public sealed class MapPoi
    {
        public string name;
        public string hint;              // short line for the info card
        public Vector2 pos;
        public MapIcon icon;
        public RegionId region;
        public int eventIndex = -1;      // rally stage start
        public bool travel;              // fast-travel point
        public RegionId travelTo;
        public bool landmark;
        public float labelW = -1f, labelH;    // cached label size (MenuArt.Tiny) for the current resolution
        public int labelStyleH;
        string kindLine;
        /// <summary>"Temple  ·  Lotus Coast" (cached).</summary>
        public string KindLine
        {
            get
            {
                if (kindLine == null) kindLine = (eventIndex >= 0 ? (icon == MapIcon.Drift ? "Drift challenge" : "Rally stage · time attack") : MapMarkers.IconName(icon))
                                                 + "  ·  " + MapMarkers.RegionName(region);
                return kindLine;
            }
        }
    }

    public static class MapMarkers
    {
        struct Mark
        {
            public string name, hint; public float x, z; public MapIcon icon; public RegionId region;
            public Mark(RegionId r, string n, float x, float z, MapIcon i, string h) { region = r; name = n; this.x = x; this.z = z; icon = i; hint = h; }
        }

        // Landmarks, positions taken from the region code (CityPlan, CoastRegion, MountainsMap, SnowRegion, CanyonLayout, MapData).
        static readonly Mark[] Landmarks =
        {
            // Jade Harbour City
            new Mark(RegionId.City, "Neon Circus TV Tower", 1227f, 140f, MapIcon.Pagoda, "Lantern tower in the middle of the Neon Circus"),
            new Mark(RegionId.City, "Harbour Bridge", 1225f, -130f, MapIcon.Bridge, "Cable-stayed bridge over the harbour basin"),
            new Mark(RegionId.City, "Dragon Stadium", 982f, 312f, MapIcon.Star, "Stadium under the north cliffs"),
            new Mark(RegionId.City, "Old Town Temple", 812f, 192f, MapIcon.Temple, "Temple courtyard in the old town"),
            new Mark(RegionId.City, "Moon Gate", 858f, 140f, MapIcon.Gate, "Round gate at the end of Lantern Alley"),
            new Mark(RegionId.City, "Jade Plaza", 810f, -88f, MapIcon.Star, "Fountain plaza and metro entrance"),
            new Mark(RegionId.City, "Gateway Arch", 712f, -130f, MapIcon.Gate, "City gate on the road from the valley"),
            new Mark(RegionId.City, "Ferry Pier", 1000f, -445f, MapIcon.Harbour, "Seafront promenade and ferry quay"),
            new Mark(RegionId.City, "Container Terminal", 1388f, -398f, MapIcon.Harbour, "Cranes and stacked containers"),
            new Mark(RegionId.City, "Harbour Lighthouse", 1262f, -575f, MapIcon.Lighthouse, "Out on the harbour mole"),
            // Lotus Coast
            new Mark(RegionId.Coast, "Headland Lighthouse", -1362f, -4f, MapIcon.Lighthouse, "On the granite headland's high point"),
            new Mark(RegionId.Coast, "Mazu Temple", -1192f, 334f, MapIcon.Temple, "Sea goddess temple above the harbour street"),
            new Mark(RegionId.Coast, "Lotus Harbour", -1178f, 248f, MapIcon.Harbour, "Stilt houses, piers and the seafood street"),
            new Mark(RegionId.Coast, "Lotus Lagoons", -1030f, -240f, MapIcon.Water, "Lotus pools behind the dunes"),
            new Mark(RegionId.Coast, "Pine Hill Pagoda", -850f, 196f, MapIcon.Viewpoint, "Viewpoint on the coastal pine ridge"),
            new Mark(RegionId.Coast, "Sunset Bay", -1140f, -320f, MapIcon.Water, "Crescent sand beach on the south bay"),
            // Cloud Peak Mountains
            new Mark(RegionId.Mountains, "Golden Monastery", 214f, 1386f, MapIcon.Temple, "Summit monastery at the end of the hillclimb"),
            new Mark(RegionId.Mountains, "Jade Lake", 176f, 1106f, MapIcon.Water, "Mountain lake under the hairpins"),
            new Mark(RegionId.Mountains, "Valley Falls", -40f, 604f, MapIcon.Water, "Waterfall viewpoint above the plunge pool"),
            new Mark(RegionId.Mountains, "Gorge Lookout", -48f, 868f, MapIcon.Viewpoint, "Pavilion over the gorge"),
            new Mark(RegionId.Mountains, "Cloud-Sea Ridge", 330f, 788f, MapIcon.Viewpoint, "Spur road to the sea of cloud"),
            new Mark(RegionId.Mountains, "Cable Car", 112f, 1162f, MapIcon.Star, "Lower station of the monastery cable car"),
            new Mark(RegionId.Mountains, "Central Col", -50f, 985f, MapIcon.Peak, "Crossroads of the mountain passes"),
            // Frost Summit
            new Mark(RegionId.Snow, "Summit Observatory", 1246f, 1392f, MapIcon.Observatory, "Above the clouds at the end of the Frost Run"),
            new Mark(RegionId.Snow, "Frozen Lake", 1100f, 900f, MapIcon.Water, "Glassy ice circuit on the lake"),
            new Mark(RegionId.Snow, "Ski Lodge Village", 948f, 1150f, MapIcon.Town, "Ice-sculpture plaza and lodges"),
            new Mark(RegionId.Snow, "Ski Peak", 1000f, 1428f, MapIcon.Peak, "Top station of the cable car"),
            new Mark(RegionId.Snow, "Wind Turbine Ridge", 1450f, 790f, MapIcon.Star, "Turbines along the east ridge"),
            new Mark(RegionId.Snow, "Cliff Rim Viewpoint", 1242f, 529f, MapIcon.Viewpoint, "Looking down on Jade Harbour City"),
            // Red Canyon
            new Mark(RegionId.Canyon, "Red Citadel", -842f, 800f, MapIcon.Peak, "Striped mesa in the middle of the loop"),
            new Mark(RegionId.Canyon, "Crescent Spring Oasis", -1290f, 1050f, MapIcon.Water, "Crescent lake in the dunes, tea house and well"),
            new Mark(RegionId.Canyon, "Caravanserai", -994f, 1290f, MapIcon.Town, "Walled desert inn"),
            new Mark(RegionId.Canyon, "Watchtower", -1062f, 1394f, MapIcon.Pagoda, "Old tower on its knoll"),
            new Mark(RegionId.Canyon, "Gorge Bridge", -929.5f, 1093f, MapIcon.Bridge, "The main track leaps the gorge"),
            new Mark(RegionId.Canyon, "The Slot", -832f, 933f, MapIcon.Star, "The track squeezes between sheer walls"),
            new Mark(RegionId.Canyon, "Dune Sea", -1352f, 1338f, MapIcon.Nature, "Rolling sand in the north-west"),
            new Mark(RegionId.Canyon, "Salt Playa", -1330f, 730f, MapIcon.Star, "Dry lake bed and an old truck"),
            new Mark(RegionId.Canyon, "Rainbow Hills", -786f, 1338f, MapIcon.Peak, "Striped Danxia hills"),
        };

        static readonly string[] PassNames = { "Harbour Pass", "Coast Pass", "Cloud Pass", "Frost Pass", "Canyon Pass", "Desert Pass" };

        static readonly List<MapPoi> all = new List<MapPoi>();
        static WorldBuilder builtFor;
        static int builtEvents = -1;

        /// <summary>Every marker (landmarks always; events, garage, fast travel and passes once the world is built).</summary>
        public static List<MapPoi> All { get { Ensure(); return all; } }

        public static void Ensure()
        {
            var w = WorldHooks.World;
            int ev = StageDef.All.Length;
            if (all.Count > 0 && w == builtFor && ev == builtEvents) return;
            builtFor = w;
            builtEvents = ev;
            all.Clear();
            // Blossom Valley: blueprint positions straight from MapData
            AddValley("Mountain Pagoda", MapData.PagodaTop, MapIcon.Pagoda, "Five-tier pagoda at the top of the hairpins");
            AddValley("Lotus Lake", MapData.LakeCenter, MapIcon.Water, "Koi, lotus pads and a lakeside lookout");
            AddValley("Paifang Gate", MapData.Gate, MapIcon.Gate, "Stone gate at the head of Lantern Street");
            AddValley("Blossom Village", MapData.P(625f, 1443f), MapIcon.Town, "Lantern Street, tea houses and the garage");
            AddValley("Tea Terraces", MapData.P(1330f, 900f), MapIcon.Nature, "Gravel switchbacks through the tea rows");
            AddValley("Bamboo Forest", MapData.P(200f, 600f), MapIcon.Nature, "Fast dirt run through the bamboo");
            AddValley("East Lookout", MapData.Lookouts[2], MapIcon.Viewpoint, "Top of the tea terraces");
            foreach (var m in Landmarks)
                all.Add(new MapPoi { name = m.name, hint = m.hint, pos = new Vector2(m.x, m.z), icon = m.icon, region = m.region, landmark = true });
            all.Add(new MapPoi { name = "Garage", hint = "Change car and livery, wash and repair", pos = MapData.Garage, icon = MapIcon.Garage, region = RegionId.Valley });
            for (int i = 0; i < WorldLayout.Connectors.Length; i++)
            {
                var c = WorldLayout.Connectors[i];
                Vector2 mid = c.points[c.points.Length / 2];
                all.Add(new MapPoi
                {
                    name = i < PassNames.Length ? PassNames[i] : "Pass", hint = RegionName(c.a) + "  ↔  " + RegionName(c.b),
                    pos = mid, icon = MapIcon.Pass, region = WorldLayout.At(mid)
                });
            }
            if (w == null) return;
            // fast travel
            var field = w.Field;
            foreach (var id in WorldLayout.All)
            {
                var reg = field != null ? field.RegionOf(id) : null;
                if (reg == null) continue;
                Vector2 p;
                if (id == RegionId.Valley) p = new Vector2(w.SpawnPosition.x, w.SpawnPosition.z);
                else { Vector3 sp = reg.SpawnPoint; p = new Vector2(sp.x, sp.z); }
                all.Add(new MapPoi { name = reg.Name, hint = "Fast travel point", pos = p, icon = MapIcon.FastTravel, region = id, travel = true, travelTo = id });
            }
            // rally stage starts
            var defs = StageDef.All;
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                if (d.road < 0 || d.road >= w.roadSamples.Count) continue;
                var s = w.roadSamples[d.road];
                if (s == null || s.Count < 2) continue;
                int k = 0;
                while (k < s.Count - 1 && s[k].distance < d.startOffset) k++;
                Vector2 p = s[k].pos;
                // two events on one start (time attack + drift on the same road): nudge the second badge aside
                foreach (var o in all) if (o.eventIndex >= 0 && (o.pos - p).sqrMagnitude < 64f) { p += new Vector2(s[k].tangent.y, -s[k].tangent.x) * 14f; break; }
                all.Add(new MapPoi
                {
                    name = d.name, hint = d.blurb, pos = p, icon = d.kind == StageKind.Drift ? MapIcon.Drift : MapIcon.TimeAttack,
                    region = WorldLayout.At(p), eventIndex = i
                });
            }
        }

        static void AddValley(string n, Vector2 p, MapIcon i, string hint)
        {
            all.Add(new MapPoi { name = n, hint = hint, pos = p, icon = i, region = RegionId.Valley, landmark = true });
        }

        /// <summary>Stage course polyline (start to finish, ~8 m steps) for an event, cached.</summary>
        public static List<Vector2> Course(int eventIndex)
        {
            if (eventIndex == courseEvent && courseWorld == WorldHooks.World) return course;
            courseEvent = eventIndex;
            courseWorld = WorldHooks.World;
            course.Clear();
            var w = courseWorld;
            if (w == null || eventIndex < 0 || eventIndex >= StageDef.All.Length) return course;
            var d = StageDef.All[eventIndex];
            if (d.road < 0 || d.road >= w.roadSamples.Count) return course;
            var s = w.roadSamples[d.road];
            if (s == null || s.Count < 2) return course;
            bool closed = d.road < w.roads.Count && w.roads[d.road].closed;
            float total = s[s.Count - 1].distance;
            float endD = closed ? total : total - d.endOffset;
            int n = s.Count;
            int i0 = 0;
            while (i0 < n - 1 && s[i0].distance < d.startOffset) i0++;
            if (closed)
            {
                for (int k = 0; k <= n; k += 4) course.Add(s[(i0 + k) % n].pos);
                course.Add(s[i0].pos);
            }
            else
                for (int k = i0; k < n && s[k].distance <= endD; k += 4) course.Add(s[k].pos);
            return course;
        }
        static readonly List<Vector2> course = new List<Vector2>();
        static int courseEvent = -2;
        static WorldBuilder courseWorld;

        // ------------------------------------------------------------------ regions
        static readonly string[] regionNames = new string[6], regionUpper = new string[6], regionHanzi = new string[6];

        public static Region RegionObj(RegionId id)
        {
            var w = WorldHooks.World;
            return w != null && w.Field != null ? w.Field.RegionOf(id) : null;
        }

        public static string RegionName(RegionId id)
        {
            int i = (int)id;
            if (regionNames[i] == null)
            {
                var r = RegionObj(id);
                if (r == null) return id.ToString();
                regionNames[i] = r.Name;
            }
            return regionNames[i];
        }

        public static string RegionUpper(RegionId id)
        {
            int i = (int)id;
            if (regionUpper[i] == null) { var n = RegionName(id); if (RegionObj(id) == null) return n.ToUpperInvariant(); regionUpper[i] = n.ToUpperInvariant(); }
            return regionUpper[i];
        }

        public static string RegionHanzi(RegionId id)
        {
            int i = (int)id;
            if (regionHanzi[i] == null) { var r = RegionObj(id); if (r == null) return ""; regionHanzi[i] = r.Hanzi ?? ""; }
            return regionHanzi[i];
        }

        public static Color RegionAccent(RegionId id)
        {
            var r = RegionObj(id);
            return r != null ? r.Accent : new Color(0.9f, 0.5f, 0.6f);
        }

        // ------------------------------------------------------------------ look
        public static readonly Color RouteCol = new Color(0.98f, 0.26f, 0.56f, 1f);      // GPS route + waypoint
        public static readonly Color TimeCol = new Color(0.86f, 0.24f, 0.20f, 1f);
        public static readonly Color DriftCol = new Color(0.56f, 0.38f, 0.90f, 1f);
        public static readonly Color GarageCol = new Color(0.16f, 0.60f, 0.52f, 1f);
        public static readonly Color TravelCol = new Color(0.96f, 0.64f, 0.16f, 1f);
        public static readonly Color PassCol = new Color(0.52f, 0.38f, 0.26f, 1f);
        public static readonly Color LandCol = new Color(0.22f, 0.26f, 0.40f, 1f);

        public static Color IconColor(MapIcon i)
        {
            switch (i)
            {
                case MapIcon.Waypoint: return RouteCol;
                case MapIcon.TimeAttack: return TimeCol;
                case MapIcon.Drift: return DriftCol;
                case MapIcon.Garage: return GarageCol;
                case MapIcon.FastTravel: return TravelCol;
                case MapIcon.Pass: return PassCol;
                case MapIcon.You: return UI.White;
                default: return LandCol;
            }
        }

        public static string IconName(MapIcon i)
        {
            switch (i)
            {
                case MapIcon.You: return "You";
                case MapIcon.Waypoint: return "Waypoint";
                case MapIcon.TimeAttack: return "Time attack";
                case MapIcon.Drift: return "Drift challenge";
                case MapIcon.Garage: return "Garage";
                case MapIcon.FastTravel: return "Fast travel";
                case MapIcon.Pass: return "Mountain pass";
                case MapIcon.Pagoda: return "Tower / pagoda";
                case MapIcon.Temple: return "Temple";
                case MapIcon.Water: return "Lake / water";
                case MapIcon.Bridge: return "Bridge";
                case MapIcon.Lighthouse: return "Lighthouse";
                case MapIcon.Viewpoint: return "Viewpoint";
                case MapIcon.Town: return "Village / town";
                case MapIcon.Peak: return "Peak / mesa";
                case MapIcon.Observatory: return "Observatory";
                case MapIcon.Harbour: return "Harbour";
                case MapIcon.Gate: return "Gate";
                case MapIcon.Nature: return "Forest / fields";
                default: return "Sight";
            }
        }

        /// <summary>Ink-ringed badge: soft shadow, ink disc, coloured disc, paper glyph. ring = medal colour (a = 0 for none).</summary>
        public static void Badge(Vector2 c, float size, MapIcon icon, Color fill, Color ring, float alpha)
        {
            if (alpha <= 0.01f) return;
            float h = size * 0.5f;
            MenuArt.Tex(new Rect(c.x - h * 1.5f, c.y - h * 1.3f, size * 1.5f, size * 1.5f), MenuArt.Glow, new Color(0f, 0f, 0f, 0.35f * alpha));
            if (ring.a > 0f)
            {
                MenuArt.Tex(new Rect(c.x - h - 3f, c.y - h - 3f, size + 6f, size + 6f), MenuArt.Dot, MenuArt.A(MenuArt.Ink, alpha));
                MenuArt.Tex(new Rect(c.x - h - 1.5f, c.y - h - 1.5f, size + 3f, size + 3f), MenuArt.Dot, MenuArt.A(ring, alpha));
            }
            MenuArt.Tex(new Rect(c.x - h, c.y - h, size, size), MenuArt.Dot, MenuArt.A(MenuArt.Ink, alpha));
            float f = size * 0.84f;
            MenuArt.Tex(new Rect(c.x - f * 0.5f, c.y - f * 0.5f, f, f), MenuArt.Dot, new Color(fill.r, fill.g, fill.b, alpha));
            float g = size * 0.62f;
            MenuArt.Tex(new Rect(c.x - g * 0.5f, c.y - g * 0.5f, g, g), Icon(icon), MenuArt.A(MenuArt.PaperCol, alpha));
        }

        /// <summary>Waypoint pin standing on c (the tip touches c).</summary>
        public static void Pin(Vector2 c, float size, float alpha)
        {
            var t = Icon(MapIcon.Waypoint);
            float o = size * 1.18f;
            MenuArt.Tex(new Rect(c.x - size * 0.6f, c.y - size * 0.25f, size * 1.2f, size * 0.5f), MenuArt.Glow, new Color(0f, 0f, 0f, 0.4f * alpha));
            MenuArt.Tex(new Rect(c.x - o * 0.5f, c.y - o * 0.97f, o, o), t, MenuArt.A(MenuArt.Ink, alpha));
            MenuArt.Tex(new Rect(c.x - size * 0.5f, c.y - size * 0.98f, size, size), t, MenuArt.A(RouteCol, alpha));
        }

        // ------------------------------------------------------------------ icon glyphs (signed distance rasteriser)
        static Texture2D[] icons;

        public static Texture2D Icon(MapIcon i)
        {
            if (icons == null) icons = new Texture2D[(int)MapIcon.Count];
            int k = Mathf.Clamp((int)i, 0, icons.Length - 1);
            if (icons[k] == null) icons[k] = Raster(i);
            return icons[k];
        }

        enum PK { Capsule, Disc, Box, Tri, Arc, Ellipse }

        struct Prim
        {
            public PK k; public bool hole; public Vector2 a, b, c; public float r, w;
            public float Dist(Vector2 p)
            {
                switch (k)
                {
                    case PK.Capsule: { float t; return Spline.DistToSegment(p, a, b, out t) - r; }
                    case PK.Disc: return (p - a).magnitude - r;
                    case PK.Box:
                        {
                            Vector2 q = new Vector2(Mathf.Abs(p.x - a.x) - b.x, Mathf.Abs(p.y - a.y) - b.y);
                            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                        }
                    case PK.Tri: return TriDist(p, a, b, c);
                    case PK.Arc:
                        {
                            Vector2 d = p - a;
                            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                            if (ang < 0f) ang += 360f;
                            if (ang >= b.x && ang <= b.y) return Mathf.Abs(d.magnitude - r) - w * 0.5f;
                            Vector2 e0 = a + new Vector2(Mathf.Cos(b.x * Mathf.Deg2Rad), Mathf.Sin(b.x * Mathf.Deg2Rad)) * r;
                            Vector2 e1 = a + new Vector2(Mathf.Cos(b.y * Mathf.Deg2Rad), Mathf.Sin(b.y * Mathf.Deg2Rad)) * r;
                            return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - w * 0.5f;
                        }
                    default:
                        {
                            Vector2 d = new Vector2((p.x - a.x) / b.x, (p.y - a.y) / b.y);
                            return (d.magnitude - 1f) * Mathf.Min(b.x, b.y);
                        }
                }
            }
        }

        static float TriDist(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Mathf.Min(pq0.sqrMagnitude, pq1.sqrMagnitude), pq2.sqrMagnitude);
            float dy = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)), s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }

        static List<Prim> P;
        static void Cap(float ax, float ay, float bx, float by, float r, bool hole = false) { P.Add(new Prim { k = PK.Capsule, a = new Vector2(ax, ay), b = new Vector2(bx, by), r = r, hole = hole }); }
        static void Disc(float x, float y, float r, bool hole = false) { P.Add(new Prim { k = PK.Disc, a = new Vector2(x, y), r = r, hole = hole }); }
        static void Box(float x, float y, float hx, float hy, bool hole = false, float round = 0f) { P.Add(new Prim { k = PK.Box, a = new Vector2(x, y), b = new Vector2(hx, hy), r = round, hole = hole }); }
        static void Tri(float ax, float ay, float bx, float by, float cx, float cy, bool hole = false) { P.Add(new Prim { k = PK.Tri, a = new Vector2(ax, ay), b = new Vector2(bx, by), c = new Vector2(cx, cy), hole = hole }); }
        static void Arc(float x, float y, float r, float w, float a0, float a1, bool hole = false) { P.Add(new Prim { k = PK.Arc, a = new Vector2(x, y), b = new Vector2(a0, a1), r = r, w = w, hole = hole }); }
        static void Ell(float x, float y, float rx, float ry, bool hole = false) { P.Add(new Prim { k = PK.Ellipse, a = new Vector2(x, y), b = new Vector2(rx, ry), hole = hole }); }
        static void Wave(float y, float amp, float freq, float r, float x0 = -0.75f, float x1 = 0.75f)
        {
            float px = x0, py = y + amp * Mathf.Sin(x0 * freq);
            for (int i = 1; i <= 12; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / 12f), yy = y + amp * Mathf.Sin(x * freq);
                Cap(px, py, x, yy, r);
                px = x; py = yy;
            }
        }

        static void Glyph(MapIcon i)
        {
            switch (i)
            {
                case MapIcon.TimeAttack:
                    Cap(-0.58f, -0.82f, -0.58f, 0.8f, 0.09f);
                    Box(0.07f, 0.42f, 0.55f, 0.33f);
                    Box(-0.345f, 0.585f, 0.1375f, 0.165f, true); Box(-0.07f, 0.255f, 0.1375f, 0.165f, true);
                    Box(0.205f, 0.585f, 0.1375f, 0.165f, true); Box(0.48f, 0.255f, 0.1375f, 0.165f, true);
                    break;
                case MapIcon.Drift:
                    Wave(0.05f, 0.42f, 2.6f, 0.13f, -0.72f, 0.72f);
                    Disc(-0.55f, -0.62f, 0.1f); Disc(-0.25f, -0.72f, 0.07f); Disc(0.55f, 0.72f, 0.07f);
                    break;
                case MapIcon.Garage:
                    Tri(-0.88f, 0.08f, 0f, 0.8f, 0.88f, 0.08f);
                    Box(0f, -0.36f, 0.62f, 0.46f);
                    Box(0f, -0.22f, 0.38f, 0.06f, true); Box(0f, -0.46f, 0.38f, 0.06f, true); Box(0f, -0.7f, 0.38f, 0.06f, true);
                    break;
                case MapIcon.FastTravel:
                    Ell(0f, -0.02f, 0.56f, 0.5f);
                    Box(0f, 0.56f, 0.3f, 0.08f, false, 0.02f); Box(0f, -0.6f, 0.3f, 0.08f, false, 0.02f);
                    Cap(0f, 0.66f, 0f, 0.88f, 0.06f); Cap(0f, -0.68f, 0f, -0.92f, 0.06f);
                    Ell(0f, -0.02f, 0.2f, 0.42f, true); Ell(0f, -0.02f, 0.11f, 0.36f);
                    break;
                case MapIcon.Pass:
                    Cap(-0.85f, -0.5f, -0.42f, 0.48f, 0.11f); Cap(-0.42f, 0.48f, 0f, -0.02f, 0.11f);
                    Cap(0f, -0.02f, 0.42f, 0.48f, 0.11f); Cap(0.42f, 0.48f, 0.85f, -0.5f, 0.11f);
                    Cap(-0.8f, -0.76f, 0.8f, -0.76f, 0.08f);
                    break;
                case MapIcon.Pagoda:
                    Cap(0f, 0.5f, 0f, 0.92f, 0.05f);
                    Box(0f, -0.22f, 0.21f, 0.62f);
                    Box(0f, 0.42f, 0.34f, 0.06f); Box(0f, 0.06f, 0.52f, 0.07f); Box(0f, -0.34f, 0.7f, 0.08f);
                    Cap(0.52f, 0.06f, 0.64f, 0.18f, 0.05f); Cap(-0.52f, 0.06f, -0.64f, 0.18f, 0.05f);
                    Cap(0.7f, -0.34f, 0.84f, -0.2f, 0.06f); Cap(-0.7f, -0.34f, -0.84f, -0.2f, 0.06f);
                    Box(0f, -0.82f, 0.46f, 0.06f);
                    break;
                case MapIcon.Temple:
                    Box(0f, 0.6f, 0.46f, 0.05f);
                    Tri(-0.86f, 0.26f, 0f, 0.62f, 0.86f, 0.26f);
                    Box(0f, 0.25f, 0.88f, 0.07f);
                    Cap(0.88f, 0.25f, 0.98f, 0.42f, 0.06f); Cap(-0.88f, 0.25f, -0.98f, 0.42f, 0.06f);
                    Box(0f, -0.24f, 0.6f, 0.42f);
                    Box(0f, -0.38f, 0.17f, 0.3f, true);
                    Box(-0.38f, -0.12f, 0.08f, 0.12f, true); Box(0.38f, -0.12f, 0.08f, 0.12f, true);
                    Box(0f, -0.75f, 0.82f, 0.07f);
                    break;
                case MapIcon.Water:
                    Wave(0.42f, 0.11f, 7f, 0.085f); Wave(0f, 0.11f, 7f, 0.085f); Wave(-0.42f, 0.11f, 7f, 0.085f);
                    break;
                case MapIcon.Bridge:
                    Box(0f, 0.2f, 0.92f, 0.07f);
                    Arc(0f, -0.62f, 0.66f, 0.14f, 0f, 180f);
                    Box(-0.76f, -0.32f, 0.07f, 0.5f); Box(0.76f, -0.32f, 0.07f, 0.5f);
                    Cap(-0.36f, 0.14f, -0.36f, -0.08f, 0.04f); Cap(0.36f, 0.14f, 0.36f, -0.08f, 0.04f); Cap(0f, 0.14f, 0f, 0.04f, 0.04f);
                    break;
                case MapIcon.Lighthouse:
                    Tri(-0.3f, -0.82f, 0.3f, -0.82f, 0.18f, 0.3f); Tri(-0.3f, -0.82f, 0.18f, 0.3f, -0.18f, 0.3f);
                    Box(0f, -0.38f, 0.4f, 0.07f, true); Box(0f, -0.02f, 0.4f, 0.06f, true);
                    Box(0f, 0.44f, 0.19f, 0.11f);
                    Tri(-0.27f, 0.56f, 0.27f, 0.56f, 0f, 0.82f);
                    Cap(0.3f, 0.5f, 0.88f, 0.7f, 0.045f); Cap(0.3f, 0.4f, 0.88f, 0.22f, 0.045f);
                    Cap(-0.3f, 0.5f, -0.88f, 0.7f, 0.045f); Cap(-0.3f, 0.4f, -0.88f, 0.22f, 0.045f);
                    Box(0f, -0.86f, 0.42f, 0.05f);
                    break;
                case MapIcon.Viewpoint:
                    Ell(0f, 0f, 0.88f, 0.48f);
                    Ell(0f, 0f, 0.7f, 0.32f, true);
                    Disc(0f, 0f, 0.24f);
                    break;
                case MapIcon.Town:
                    Tri(-0.84f, 0.02f, -0.36f, 0.48f, 0.12f, 0.02f); Box(-0.36f, -0.36f, 0.36f, 0.38f);
                    Tri(0.02f, 0.3f, 0.44f, 0.74f, 0.86f, 0.3f); Box(0.44f, -0.26f, 0.32f, 0.56f);
                    Box(-0.36f, -0.26f, 0.1f, 0.12f, true); Box(0.44f, 0.02f, 0.1f, 0.1f, true); Box(0.44f, -0.5f, 0.1f, 0.24f, true);
                    break;
                case MapIcon.Peak:
                    Tri(-0.92f, -0.7f, -0.18f, 0.76f, 0.58f, -0.7f);
                    Tri(0.02f, -0.7f, 0.5f, 0.28f, 0.96f, -0.7f);
                    Cap(-0.44f, 0.22f, -0.3f, 0.12f, 0.05f, true); Cap(-0.3f, 0.12f, -0.16f, 0.24f, 0.05f, true); Cap(-0.16f, 0.24f, 0.02f, 0.1f, 0.05f, true);
                    break;
                case MapIcon.Observatory:
                    Disc(0f, -0.12f, 0.64f);
                    Box(0f, -0.72f, 1f, 0.58f, true);
                    Box(0f, -0.42f, 0.78f, 0.3f);
                    Box(0.14f, 0.22f, 0.09f, 0.32f, true);
                    Cap(0.12f, 0.28f, 0.6f, 0.76f, 0.09f);
                    Box(0f, -0.56f, 0.12f, 0.16f, true);
                    break;
                case MapIcon.Harbour:
                    Arc(0f, 0.62f, 0.17f, 0.1f, 0f, 360f);
                    Cap(0f, 0.44f, 0f, -0.76f, 0.08f);
                    Cap(-0.36f, 0.24f, 0.36f, 0.24f, 0.07f);
                    Arc(0f, -0.2f, 0.6f, 0.14f, 200f, 340f);
                    Tri(-0.68f, -0.28f, -0.42f, -0.1f, -0.5f, -0.46f); Tri(0.68f, -0.28f, 0.42f, -0.1f, 0.5f, -0.46f);
                    break;
                case MapIcon.Gate:
                    Cap(-0.56f, -0.86f, -0.56f, 0.46f, 0.09f); Cap(0.56f, -0.86f, 0.56f, 0.46f, 0.09f);
                    Box(0f, 0.58f, 0.86f, 0.09f);
                    Cap(0.86f, 0.58f, 0.96f, 0.74f, 0.06f); Cap(-0.86f, 0.58f, -0.96f, 0.74f, 0.06f);
                    Box(0f, 0.22f, 0.7f, 0.06f);
                    Box(0f, 0.4f, 0.13f, 0.12f);
                    break;
                case MapIcon.Nature:
                    Disc(0f, 0.24f, 0.5f); Disc(-0.4f, -0.04f, 0.34f); Disc(0.4f, -0.04f, 0.34f);
                    Box(0f, -0.58f, 0.1f, 0.32f);
                    break;
                case MapIcon.Waypoint:
                    Disc(0f, 0.28f, 0.6f);
                    Tri(-0.52f, -0.02f, 0.52f, -0.02f, 0f, -0.96f);
                    Disc(0f, 0.28f, 0.24f, true);
                    break;
                case MapIcon.You:
                    Tri(0f, 0.9f, 0.62f, -0.72f, 0f, -0.36f); Tri(0f, 0.9f, 0f, -0.36f, -0.62f, -0.72f);
                    break;
                default:   // star
                    for (int k = 0; k < 5; k++)
                    {
                        float a0 = Mathf.PI * 0.5f + k * Mathf.PI * 0.4f, a1 = a0 + Mathf.PI * 0.2f, a2 = a0 - Mathf.PI * 0.2f;
                        Tri(0f, 0f, Mathf.Cos(a0) * 0.92f, Mathf.Sin(a0) * 0.92f, Mathf.Cos(a1) * 0.38f, Mathf.Sin(a1) * 0.38f);
                        Tri(0f, 0f, Mathf.Cos(a2) * 0.38f, Mathf.Sin(a2) * 0.38f, Mathf.Cos(a0) * 0.92f, Mathf.Sin(a0) * 0.92f);
                    }
                    break;
            }
        }

        static Texture2D Raster(MapIcon id)
        {
            const int N = 64;
            P = new List<Prim>();
            Glyph(id);
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "BR_MapIcon_" + id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[N * N];
            float pix = 2f / N;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                    float a = 0f;
                    for (int k = 0; k < P.Count; k++)
                    {
                        float cov = Mathf.Clamp01(0.5f - P[k].Dist(p) / pix);
                        a = P[k].hole ? a * (1f - cov) : Mathf.Max(a, cov);
                    }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f + 0.5f));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            P = null;
            return t;
        }
    }

    /// <summary>Immediate-mode GL triangles in GUI coordinates (y down): on screen (OnGUI Repaint) or into a RenderTexture.</summary>
    public static class MapGL
    {
        static Material col, colPre, tex;
        static bool tried, linear, batching, premul;
        static float H;
        static Rect clip;
        static RenderTexture prevRT;

        public static bool Ready
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    Shader s = Shader.Find("Hidden/Internal-Colored");
                    if (s != null)
                    {
                        col = new Material(s) { name = "BR_MapGL", hideFlags = HideFlags.HideAndDontSave };
                        col.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        col.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        col.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                        col.SetInt("_ZWrite", 0);
                        col.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                        // render-texture pass: premultiplied colours keep the target's alpha at 1 under translucent lines
                        colPre = new Material(col) { name = "BR_MapGLPremul", hideFlags = HideFlags.HideAndDontSave };
                        colPre.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    }
                    Shader ts = Shader.Find("Sprites/Default");
                    if (ts == null) ts = Shader.Find("UI/Default");
                    if (ts == null) ts = Shader.Find("Unlit/Texture");
                    if (ts != null) tex = new Material(ts) { name = "BR_MapGLTex", hideFlags = HideFlags.HideAndDontSave };
                }
                return col != null;
            }
        }

        public static bool CanTexture { get { return Ready && tex != null; } }

        // ---- on screen (OnGUI Repaint only) ----
        public static bool BeginScreen(Rect clipRect)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || !Ready) return false;
            clip = clipRect;
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            premul = false;
            H = Screen.height;
            col.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            batching = true;
            return true;
        }

        public static void EndScreen()
        {
            if (batching) GL.End();
            batching = false;
            GL.PopMatrix();
        }

        // ---- into a render texture ----
        public static bool BeginTarget(RenderTexture rt, Color clear)
        {
            if (rt == null || !Ready) return false;
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            premul = true;
            prevRT = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, rt.width, 0f, rt.height);
            GL.Viewport(new Rect(0f, 0f, rt.width, rt.height));
            GL.Clear(false, true, Lin(clear));
            H = rt.height;
            clip = new Rect(0f, 0f, rt.width, rt.height);
            batching = false;
            return true;
        }

        public static void EndTarget()
        {
            if (batching) GL.End();
            batching = false;
            GL.PopMatrix();
            RenderTexture.active = prevRT;
            prevRT = null;
            premul = false;
        }

        /// <summary>Start a run of coloured primitives (inside BeginTarget).</summary>
        public static void Colored()
        {
            if (batching) return;
            (premul ? colPre : col).SetPass(0);
            GL.Begin(GL.TRIANGLES);
            batching = true;
        }

        /// <summary>Textured quad (corners in GUI coords, clockwise from top-left) with uvs (inside BeginTarget).</summary>
        public static void TexQuad(Texture t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3, Color c)
        {
            if (tex == null || t == null) return;
            if (batching) { GL.End(); batching = false; }
            tex.mainTexture = t;
            tex.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Lin(c));         // Sprites/Default premultiplies in its shader
            GL.TexCoord2(u0.x, u0.y); V(p0);
            GL.TexCoord2(u1.x, u1.y); V(p1);
            GL.TexCoord2(u2.x, u2.y); V(p2);
            GL.TexCoord2(u3.x, u3.y); V(p3);
            GL.End();
        }

        static Color Lin(Color c) { return linear ? new Color(c.linear.r, c.linear.g, c.linear.b, c.a) : c; }
        static void C(Color c)
        {
            Color l = Lin(c);
            if (premul) l = new Color(l.r * l.a, l.g * l.a, l.b * l.a, l.a);
            GL.Color(l);
        }
        static void V(Vector2 p) { GL.Vertex3(p.x, H - p.y, 0f); }

        public static void Seg(Vector2 a, Vector2 b, float width, Color c)
        {
            if (!ClipSeg(ref a, ref b, width)) return;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            Vector2 e = d / len * (width * 0.3f);
            C(c);
            Vector2 a0 = a - e + n, a1 = b + e + n, a2 = b + e - n, a3 = a - e - n;
            V(a0); V(a1); V(a2);
            V(a0); V(a2); V(a3);
        }

        public static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            C(col);
            V(a); V(b); V(c);
        }

        public static void Disc(Vector2 c, float r, Color col, int sides = 16)
        {
            if (c.x < clip.xMin - r || c.x > clip.xMax + r || c.y < clip.yMin - r || c.y > clip.yMax + r) return;
            C(col);
            float step = Mathf.PI * 2f / sides;
            Vector2 prev = c + new Vector2(r, 0f);
            for (int i = 1; i <= sides; i++)
            {
                float a = i * step;
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                V(c); V(prev); V(p);
                prev = p;
            }
        }

        public static void Ring(Vector2 c, float r, float w, Color col, int sides = 28)
        {
            C(col);
            float step = Mathf.PI * 2f / sides, ri = r - w * 0.5f, ro = r + w * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * step, a1 = a0 + step;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector2 p0 = c + d0 * ri, p1 = c + d0 * ro, p2 = c + d1 * ro, p3 = c + d1 * ri;
                V(p0); V(p1); V(p2);
                V(p0); V(p2); V(p3);
            }
        }

        /// <summary>Arrow (player marker) at c pointing along dir (GUI coords, y down).</summary>
        public static void Arrow(Vector2 c, Vector2 dir, float size, Color fill, Color ink)
        {
            Vector2 f = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(0f, -1f);
            Vector2 r = new Vector2(-f.y, f.x);
            float o = size * 0.22f;
            Vector2 tip = c + f * size, l = c - f * size * 0.75f - r * size * 0.68f, rr = c - f * size * 0.75f + r * size * 0.68f, back = c - f * size * 0.32f;
            Vector2 tipO = c + f * (size + o * 1.6f), lO = c - f * (size * 0.75f + o) - r * (size * 0.68f + o * 1.3f);
            Vector2 rO = c - f * (size * 0.75f + o) + r * (size * 0.68f + o * 1.3f), backO = c - f * (size * 0.32f - o * 0.2f);
            Tri(tipO, rO, backO, ink); Tri(tipO, backO, lO, ink);
            Tri(tip, rr, back, fill); Tri(tip, back, l, fill);
            Color shade = new Color(fill.r * 0.78f, fill.g * 0.78f, fill.b * 0.82f, fill.a);
            Tri(tip, back, l, shade);
        }

        static bool ClipSeg(ref Vector2 a, ref Vector2 b, float pad)
        {
            Rect r = new Rect(clip.x - pad, clip.y - pad, clip.width + pad * 2f, clip.height + pad * 2f);
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Clip1(-d.x, a.x - r.xMin, ref t0, ref t1) || !Clip1(d.x, r.xMax - a.x, ref t0, ref t1) ||
                !Clip1(-d.y, a.y - r.yMin, ref t0, ref t1) || !Clip1(d.y, r.yMax - a.y, ref t0, ref t1)) return false;
            Vector2 a2 = a + d * t0, b2 = a + d * t1;
            a = a2; b = b2;
            return true;
        }

        static bool Clip1(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
    }
}

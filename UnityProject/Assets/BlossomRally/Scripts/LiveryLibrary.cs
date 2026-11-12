// LiveryLibrary.cs - paint colour and pattern choices for every car (picked in the garage, saved per
// car in GameSettings) and per-car runtime state for the paint shader (car matrix, dirt).
using UnityEngine;

namespace BlossomRally
{
    public static class LiveryLibrary
    {
        public static readonly string[] ColourNames =
            { "Factory", "Cherry Red", "Jade", "Imperial Gold", "Ink Black", "Porcelain", "Sky Blue", "Plum", "Peach Blossom", "Tea Green" };
        static readonly Color[] Colours =
        {
            Color.white, new Color(0.74f, 0.10f, 0.14f), new Color(0.16f, 0.52f, 0.40f), new Color(0.92f, 0.66f, 0.14f),
            new Color(0.05f, 0.05f, 0.06f), new Color(0.93f, 0.92f, 0.88f), new Color(0.36f, 0.60f, 0.86f),
            new Color(0.44f, 0.16f, 0.36f), new Color(0.98f, 0.68f, 0.74f), new Color(0.56f, 0.70f, 0.40f)
        };
        public static readonly string[] PatternNames = { "Plain", "Racing Stripes", "Two-Tone", "Roundels", "Blossom Sprigs" };

        /// <summary>Dominant paint colour of each car's livery texture (measured from the Blender exports).</summary>
        public static Color PaintKey(string carId)
        {
            switch (carId)
            {
                case "CAR-002": return Srgb(216, 152, 8);
                case "CAR-004": return Srgb(216, 104, 24);
                case "CAR-006": return Srgb(40, 56, 88);
                case "CAR-008": return Srgb(40, 88, 56);
                case "CAR-009": return Srgb(232, 232, 216);
                case "CAR-016": return Srgb(120, 168, 200);
                case "CAR-017": return Srgb(168, 24, 40);
                case "CAR-018": return Srgb(136, 184, 136);
                case "CAR-019": return Srgb(40, 88, 168);
                case "CAR-020": return Srgb(232, 118, 43);
                case "CAR-021": return Srgb(31, 59, 54);
                case "CAR-022": return Srgb(60, 63, 68);
                case "CAR-023": return Srgb(216, 195, 154);
                case "CAR-024": return Srgb(244, 242, 236);
                case "CAR-025": return Srgb(31, 163, 160);
                case "CAR-026": return Srgb(244, 244, 240);
                case "CAR-027": return Srgb(127, 138, 99);
                case "CAR-028": return Srgb(244, 244, 242);
                case "CAR-029": return Srgb(29, 35, 51);
                case "CAR-030": return Srgb(42, 31, 77);
                case "CAR-031": return Srgb(122, 37, 49);
                default: return Color.white;
            }
        }

        static Color Srgb(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f); }

        public static Color Colour(int i) { return Colours[((i % Colours.Length) + Colours.Length) % Colours.Length]; }

        /// <summary>A pattern colour that contrasts with the chosen body colour.</summary>
        public static Color PatternColour(string carId, int colour)
        {
            Color body = colour == 0 ? PaintKey(carId) : Colour(colour);
            float l = body.r * 0.3f + body.g * 0.55f + body.b * 0.15f;
            if (colour == 0 || colour == 5) return l > 0.6f ? new Color(0.70f, 0.08f, 0.12f) : new Color(0.96f, 0.95f, 0.92f);
            return l > 0.55f ? new Color(0.08f, 0.08f, 0.10f) : new Color(0.96f, 0.95f, 0.92f);
        }

        /// <summary>Apply the saved (or given) livery to a car's paint material.</summary>
        public static void Apply(string carId, int colour, int pattern)
        {
            var m = MaterialLibrary.CarPaint(carId);
            if (m == null) return;
            colour = ((colour % Colours.Length) + Colours.Length) % Colours.Length;
            pattern = ((pattern % PatternNames.Length) + PatternNames.Length) % PatternNames.Length;
            m.SetColor("_PaintColor", Colour(colour));
            m.SetFloat("_PaintMix", colour == 0 ? 0f : 1f);
            m.SetFloat("_Pattern", pattern);
            Color pc = PatternColour(carId, colour);
            if (pattern == 4) pc = new Color(1f, 0.70f, 0.80f);
            m.SetColor("_PatternColor", pc);
        }

        public static void ApplySaved(string carId)
        {
            Apply(carId, GameSettings.LiveryColour(carId), GameSettings.LiveryPattern(carId));
        }

        public static void SetDirt(string carId, float dirt, Color dirtColour)
        {
            var m = MaterialLibrary.TryGet("Paint_" + carId);
            if (m == null) return;
            m.SetFloat("_Dirt", dirt);
            m.SetColor("_DirtColor", dirtColour);
        }

        public static void SetCarFrame(string carId, Transform car, Vector3 extents)
        {
            var m = MaterialLibrary.TryGet("Paint_" + carId);
            if (m == null) return;
            m.SetMatrix("_CarW2L", car.worldToLocalMatrix);
            m.SetVector("_CarExt", extents);
        }
    }
}

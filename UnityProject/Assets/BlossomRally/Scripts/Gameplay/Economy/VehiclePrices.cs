// VehiclePrices.cs - what every vehicle in CarCatalog costs, and which showroom sells it.
// Prices come from the spec (class, power, top speed, weight), so cars and bikes added later by other modules
// (CarCatalog entries with "BIKE-..." ids) get a sensible price without a table. CarCatalog is read at runtime.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class VehiclePrices
    {
        static readonly Dictionary<string, int> cache = new Dictionary<string, int>();
        static readonly Dictionary<string, string> priceText = new Dictionary<string, string>();

        public static bool IsBike(CarSpec s)
        {
            if (s == null || s.id == null) return false;
            if (s.twoWheeler) return true;
            if (s.id.StartsWith("BIKE", System.StringComparison.OrdinalIgnoreCase)) return true;
            string c = s.carClass ?? "";
            return c.IndexOf("bike", System.StringComparison.OrdinalIgnoreCase) >= 0
                || c.IndexOf("scooter", System.StringComparison.OrdinalIgnoreCase) >= 0
                || c.IndexOf("moto", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Price in yuan (rounded to ¥500 for cars, ¥250 for bikes).</summary>
        public static int Price(CarSpec s)
        {
            if (s == null || s.id == null) return 0;
            int p;
            if (cache.TryGetValue(s.id, out p)) return p;
            p = Compute(s);
            cache[s.id] = p;
            return p;
        }

        static int Compute(CarSpec s)
        {
            if (IsBike(s))
            {
                float b = 1200f + s.power * 0.55f + Mathf.Max(0f, s.maxSpeedKmh - 90f) * 55f + ClassBonus(s.carClass) * 2500f;
                return Mathf.Clamp(Mathf.RoundToInt(b / 250f) * 250, 1500, 30000);
            }
            float v = 2000f + s.power * 1.2f + Mathf.Max(0f, s.maxSpeedKmh - 150f) * 150f;
            v *= ClassFactor(s.carClass);
            return Mathf.Clamp(Mathf.RoundToInt(v / 500f) * 500, 4000, 90000);
        }

        static float ClassFactor(string cls)
        {
            switch (cls)
            {
                case "Group B": return 1.25f;
                case "Group 4": return 1.05f;
                case "Classic RWD": return 0.9f;
                case "Luxury SUV": return 1.35f;
                case "Electric GT": return 1.25f;
                case "Modern Rally": return 1.2f;
                case "Super Saloon": return 1.15f;
                case "Trophy Truck": return 1.15f;
                case "Coupe SUV": return 1.1f;
                case "Retro 4x4": return 0.9f;
                case "Dune Buggy": return 0.75f;
                default: return 1f;
            }
        }

        static float ClassBonus(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return 0f;
            if (cls.IndexOf("sport", System.StringComparison.OrdinalIgnoreCase) >= 0) return 1f;
            if (cls.IndexOf("tour", System.StringComparison.OrdinalIgnoreCase) >= 0) return 0.8f;
            if (cls.IndexOf("adventure", System.StringComparison.OrdinalIgnoreCase) >= 0) return 0.7f;
            if (cls.IndexOf("scooter", System.StringComparison.OrdinalIgnoreCase) >= 0) return -0.3f;
            return 0.3f;
        }

        /// <summary>"¥31,500", cached per vehicle.</summary>
        public static string PriceText(CarSpec s)
        {
            if (s == null || s.id == null) return "";
            string t;
            if (priceText.TryGetValue(s.id, out t)) return t;
            t = Wallet.Format(Price(s));
            priceText[s.id] = t;
            return t;
        }

        public static bool Owned(CarSpec s) { return s != null && Wallet.Owns(s.id); }

        /// <summary>Index of a vehicle id in CarCatalog.Cars, or -1.</summary>
        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            var cars = CarCatalog.Cars;
            for (int i = 0; i < cars.Length; i++) if (cars[i] != null && cars[i].id == id) return i;
            return -1;
        }

        /// <summary>Fills 'into' with the catalogue entries a showroom sells: "city_cars" (or any id with "car"),
        /// "coast_bikes" (any id with "bike" / "moto"), anything else = everything. Sorted by price.</summary>
        public static void Stock(string showroomId, List<CarSpec> into)
        {
            into.Clear();
            string id = showroomId ?? "";
            bool bikes = id.IndexOf("bike", System.StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("moto", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool cars = !bikes && id.IndexOf("car", System.StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var s in CarCatalog.Cars)
            {
                if (s == null) continue;
                bool b = IsBike(s);
                if (bikes && !b) continue;
                if (cars && b) continue;
                into.Add(s);
            }
            into.Sort((a, b) =>
            {
                // vehicles for sale first (cheapest first), then the ones already owned
                bool oa = Owned(a), ob = Owned(b);
                if (oa != ob) return oa ? 1 : -1;
                return Price(a).CompareTo(Price(b));
            });
        }

        /// <summary>Showroom display name for an id.</summary>
        public static string ShowroomName(string showroomId)
        {
            string id = showroomId ?? "";
            if (id.IndexOf("bike", System.StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("moto", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lotus Coast Motorbikes";
            return "Jade Harbour Showroom";
        }

        // ---- display stats shared by the showroom and garage (0..1)
        public static float StatPower(CarSpec s) { return Mathf.InverseLerp(4.5f, 9.5f, s.power / Mathf.Max(100f, s.mass)); }
        public static float StatSpeed(CarSpec s) { return Mathf.InverseLerp(150f, 255f, s.maxSpeedKmh); }
        public static float StatGrip(CarSpec s) { return Mathf.InverseLerp(0.95f, 1.15f, s.grip); }
        public static float StatDrift(CarSpec s) { return Mathf.InverseLerp(0.75f, 0.52f, s.driftGrip); }
        public static float StatAgility(CarSpec s) { return Mathf.InverseLerp(2400f, 300f, s.mass); }
        public static float StatOffroad(CarSpec s) { return Mathf.InverseLerp(0.9f, 1.15f, 0.5f * (s.gripLoose + s.gripSand)) * 0.6f + Mathf.InverseLerp(0.15f, 0.36f, s.susTravel) * 0.4f; }
    }
}

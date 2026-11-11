// ValleyRegion.cs - the original Blossom Valley as region (0,0). Its terrain and ground colours are exactly the
// original valley's (minus the old ring of rim mountains, which the border ridges and world rim replace).
// Its content is still built by WorldBuilder / WorldProps / RallyLife / ValleyLife as before.
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class ValleyRegion : Region
    {
        public override RegionId Id { get { return RegionId.Valley; } }
        public override string Name { get { return "Blossom Valley"; } }
        public override string Hanzi { get { return "花谷"; } }
        public override string Tagline { get { return "Cherry blossom lanes, tea terraces and the pagoda hairpins"; } }
        public override Color Accent { get { return new Color(0.98f, 0.62f, 0.74f); } }

        public override float Height(float x, float z)
        {
            float h = 0.9f * Mathf.Sin(x * 0.013f) * Mathf.Sin(z * 0.011f) + 0.6f * Mathf.Sin(x * 0.031f + z * 0.017f + 1.3f)
                      + 0.3f * Mathf.Sin(x * 0.07f - z * 0.05f);
            Vector2 mc = MapData.MountainCenter;
            float dm2 = (x - mc.x) * (x - mc.x) + (z - mc.y) * (z - mc.y);
            h += MapData.MountainHeight * Mathf.Exp(-dm2 / (2f * MapData.MountainSigma * MapData.MountainSigma));
            if (x > 270f)
            {
                float band = Spline.SmoothStep(-380f, -310f, z) * (1f - Spline.SmoothStep(370f, 430f, z));
                h += Mathf.Min(x - 270f, 260f) * 0.09f * band;
            }
            if (MapData.InBambooForest(new Vector2(x, z))) h += 1.6f * Mathf.Sin(x * 0.05f) * Mathf.Sin(z * 0.045f);
            return h;
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            Vector2 p = new Vector2(x, z);
            float n1 = 0.5f + 0.5f * Mathf.Sin(x * 0.043f + Mathf.Sin(z * 0.031f) * 2f) * Mathf.Sin(z * 0.037f);
            float n2 = 0.5f + 0.5f * Mathf.Sin(x * 0.17f + z * 0.13f) * Mathf.Sin(x * 0.11f - z * 0.19f);
            Color c = Color.Lerp(new Color(0.47f, 0.70f, 0.31f), new Color(0.60f, 0.79f, 0.39f), n1);
            c = Color.Lerp(c, new Color(0.67f, 0.83f, 0.46f), Mathf.Max(0f, n2 - 0.7f) * 1.5f);
            if (MapData.InBambooForest(p)) c = Color.Lerp(c, new Color(0.45f, 0.58f, 0.27f), 0.6f);
            if (MapData.InTeaTerraces(p)) c = Color.Lerp(c, new Color(0.38f, 0.60f, 0.27f), 0.5f);
            if (MapData.InVillage(p)) c = Color.Lerp(c, new Color(0.84f, 0.77f, 0.62f), 0.85f);
            if (h > 26f) c = Color.Lerp(c, new Color(0.60f, 0.62f, 0.55f), Mathf.Clamp01((h - 26f) / 22f) * 0.65f);
            // steep ridge flanks show rock
            float steep = Mathf.Clamp01((0.86f - normal.y) * 5f);
            if (steep > 0f) c = Color.Lerp(c, new Color(0.58f, 0.57f, 0.52f), steep * 0.7f);
            var f = Field;
            if (f != null)
            {
                float dr = f.RiverDistance(p);
                float k = MapData.LakeK(p);
                if (dr < 17f || k < 1.4f)
                {
                    float bank = Mathf.Max(1f - Spline.SmoothStep(9f, 17f, dr), 1f - Spline.SmoothStep(1.0f, 1.4f, k));
                    c = Color.Lerp(c, new Color(0.84f, 0.78f, 0.62f), bank * 0.9f);
                    if (h < -1.0f) c = Color.Lerp(c, new Color(0.45f, 0.52f, 0.46f), 0.6f);
                }
                if (Mathf.Abs(x) < 500f && Mathf.Abs(z) < 500f)
                    foreach (var bs in f.blossomSpots)
                    {
                        float dx = x - bs.x, dz = z - bs.y;
                        if (dx * dx + dz * dz >= bs.z * bs.z) continue;
                        float db = Mathf.Sqrt(dx * dx + dz * dz);
                        float speck = 0.5f + 0.5f * Mathf.Sin(x * 3.7f + z * 2.9f) * Mathf.Sin(x * 1.3f - z * 4.1f);
                        Color pk = speck > 0.65f ? new Color(0.99f, 0.88f, 0.92f) : new Color(0.95f, 0.74f, 0.81f);
                        c = Color.Lerp(c, pk, Mathf.Pow(1f - db / bs.z, 0.7f) * 0.8f);
                    }
            }
            float o = WorldLayout.OutsideDistance(p);
            if (o > 0f) c = Color.Lerp(c, new Color(0.62f, 0.72f, 0.66f), Spline.SmoothStep(0f, 160f, o) * 0.5f);
            return c;
        }

        public override Vector3 SpawnPoint
        {
            get { Vector2 g = MapData.Garage; return new Vector3(g.x + 14f, 0f, g.y); }
        }
        public override float SpawnYaw { get { return 90f; } }
    }
}

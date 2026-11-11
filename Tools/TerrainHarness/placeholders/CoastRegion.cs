// CoastRegion.cs - Lotus Coast (PLACEHOLDER: replaced by the region build-out).
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion : Region
    {
        public override RegionId Id { get { return RegionId.Coast; } }
        public override string Name { get { return "Lotus Coast"; } }
        public override string Hanzi { get { return "莲岸"; } }
        public override string Tagline { get { return "Sandy bays, fishing villages and the lighthouse cliffs"; } }
        public override Color Accent { get { return new Color(0.35f, 0.75f, 0.85f); } }

        public override float Height(float x, float z)
        {
            return 8f + 6f * WorldNoise.Fbm(x, z, 220f, 4, 255);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.86f, 0.80f, 0.62f);
        }
    }
}

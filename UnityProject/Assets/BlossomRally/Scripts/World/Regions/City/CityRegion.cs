// CityRegion.cs - Jade Harbour City (PLACEHOLDER: replaced by the region build-out).
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

        public override float Height(float x, float z)
        {
            return 18f + 6f * WorldNoise.Fbm(x, z, 220f, 4, 563);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.62f, 0.62f, 0.60f);
        }
    }
}

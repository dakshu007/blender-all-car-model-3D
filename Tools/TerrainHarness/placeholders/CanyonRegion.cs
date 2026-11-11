// CanyonRegion.cs - Red Canyon (PLACEHOLDER: replaced by the region build-out).
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CanyonRegion : Region
    {
        public override RegionId Id { get { return RegionId.Canyon; } }
        public override string Name { get { return "Red Canyon"; } }
        public override string Hanzi { get { return "丹霞"; } }
        public override string Tagline { get { return "Rainbow rock canyons, mesas and dusty desert tracks"; } }
        public override Color Accent { get { return new Color(0.95f, 0.55f, 0.30f); } }

        public override float Height(float x, float z)
        {
            return 38f + 6f * WorldNoise.Fbm(x, z, 220f, 4, 603);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.80f, 0.52f, 0.36f);
        }
    }
}

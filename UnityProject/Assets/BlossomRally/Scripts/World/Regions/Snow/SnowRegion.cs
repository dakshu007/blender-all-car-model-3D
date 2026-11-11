// SnowRegion.cs - Frost Summit (PLACEHOLDER: replaced by the region build-out).
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class SnowRegion : Region
    {
        public override RegionId Id { get { return RegionId.Snow; } }
        public override string Name { get { return "Frost Summit"; } }
        public override string Hanzi { get { return "雪顶"; } }
        public override string Tagline { get { return "Snowfields, frozen lake and the summit pass"; } }
        public override Color Accent { get { return new Color(0.75f, 0.88f, 1.0f); } }

        public override float Height(float x, float z)
        {
            return 130f + 6f * WorldNoise.Fbm(x, z, 220f, 4, 543);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.92f, 0.95f, 1.0f);
        }
    }
}

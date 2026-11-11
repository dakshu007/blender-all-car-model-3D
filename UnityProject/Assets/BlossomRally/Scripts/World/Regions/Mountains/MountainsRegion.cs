// MountainsRegion.cs - Cloud Peak Mountains (PLACEHOLDER: replaced by the region build-out).
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class MountainsRegion : Region
    {
        public override RegionId Id { get { return RegionId.Mountains; } }
        public override string Name { get { return "Cloud Peak Mountains"; } }
        public override string Hanzi { get { return "云峰"; } }
        public override string Tagline { get { return "Pine forests, cliff roads and the high monastery"; } }
        public override Color Accent { get { return new Color(0.45f, 0.75f, 0.45f); } }

        public override float Height(float x, float z)
        {
            return 60f + 6f * WorldNoise.Fbm(x, z, 220f, 4, 449);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.42f, 0.56f, 0.34f);
        }
    }
}

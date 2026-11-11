// WorldRegions.cs - creates the six regions of the world (index = (int)RegionId).
using System.Collections.Generic;

namespace BlossomRally
{
    public static class WorldRegions
    {
        public static List<Region> Create()
        {
            return new List<Region>
            {
                new ValleyRegion(), new CityRegion(), new CoastRegion(), new MountainsRegion(), new SnowRegion(), new CanyonRegion()
            };
        }
    }
}

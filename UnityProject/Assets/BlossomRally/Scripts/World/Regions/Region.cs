// Region.cs - base class of one 1 km world region (pure data part: terrain, ground colour, surfaces, roads).
// The content part (buildings, trees, effects) lives in Region.Build.cs; the split keeps everything in this
// file free of engine objects so the offline terrain harness can compile and evaluate it.
//
// Coordinates are world metres: x = east, z = north, y = up. A region's Height() is evaluated over its tile
// plus WorldLayout.Blend (110 m) on every side, and on outer world edges out to WorldLayout.TerrainPad (240 m)
// beyond the playable area. WorldField blends the six regions, adds the border ridges and rim, then cuts
// the roads in.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public abstract partial class Region
    {
        public abstract RegionId Id { get; }
        /// <summary>Display name, e.g. "Jade Harbour City".</summary>
        public abstract string Name { get; }
        /// <summary>Two or three Chinese characters shown with the name on the region banner.</summary>
        public virtual string Hanzi { get { return ""; } }
        /// <summary>One line for the map / banner.</summary>
        public virtual string Tagline { get { return ""; } }

        public Rect Rect { get { return WorldLayout.RectOf(Id); } }
        public Vector2 Center { get { return WorldLayout.CenterOf(Id); } }

        /// <summary>Set by WorldField before anything else is called (roads, pads and river are queryable after roads are sampled).</summary>
        public WorldField Field { get; internal set; }

        // ------------------------------------------------------------------ terrain
        /// <summary>Natural ground height (m) before ridges, rim and road cuts. Must be smooth and cheap (called ~1M times).</summary>
        public abstract float Height(float x, float z);

        /// <summary>
        /// Optional shaping after blending (flatten plazas, carve a lake, terrace a slope). h = blended height so far;
        /// weight = this region's blend weight at (x,z) (0..1). Return the new height. Called before roads are cut in.
        /// </summary>
        public virtual float Shape(float x, float z, float h, float weight) { return h; }

        /// <summary>Ground vertex colour (linear-ish sRGB as used by the valley). normal = terrain normal; h = final height.</summary>
        public virtual Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            return new Color(0.5f, 0.7f, 0.33f);
        }

        /// <summary>When true (default) WorldField tints the ground beside roads with VergeColor.</summary>
        public virtual bool AutoVerge { get { return true; } }

        /// <summary>Tint of the strip of ground right beside roads (worn verge).</summary>
        public virtual Color VergeColor(float x, float z) { return new Color(0.73f, 0.66f, 0.52f); }

        /// <summary>Driving surface of the bare terrain at (x,z) (grip, dust, sounds).</summary>
        public virtual Surface GroundSurface(float x, float z) { return Surface.Grass; }

        /// <summary>
        /// Grass blade density 0..1 at (x,z) (0 = none). edge = distance to the nearest road edge (99 if none within 26 m).
        /// About 0.35 tufts per m2 at density 1 - keep large areas low, verges and meadows higher.
        /// </summary>
        public virtual float GrassDensity(float x, float z, float edge) { return 0f; }

        /// <summary>Root and tip colour of one grass blade (r01 = random 0..1 per blade).</summary>
        public virtual void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            root = new Color(0.20f, 0.32f, 0.12f);
            tip = new Color(0.58f, 0.76f, 0.32f);
        }

        // ------------------------------------------------------------------ roads
        /// <summary>Add this region's roads. Called once, before sampling. Use unique names "Road_<Region>_<Name>".</summary>
        public virtual void AddRoads(List<RoadDef> roads) { }

        /// <summary>
        /// Called after all roads are sampled and before the terrain is built: reserve flat pads
        /// (Field.AddPad / Field.AddFlatRect) for buildings, plazas, car parks.
        /// </summary>
        public virtual void Plan() { }

        // ------------------------------------------------------------------ gameplay
        /// <summary>Fast-travel / respawn point (on a road) and heading in degrees (0 = +z).</summary>
        public virtual Vector3 SpawnPoint { get { return new Vector3(Center.x, 0f, Center.y); } }
        public virtual float SpawnYaw { get { return 0f; } }

        /// <summary>Rally events in this region (road given by name; resolved to an index at load).</summary>
        public virtual void AddStages(List<StageDef> stages) { }

        /// <summary>Map colour of this region (ValleyMap legend / banner accent).</summary>
        public virtual Color Accent { get { return new Color(0.9f, 0.5f, 0.6f); } }
    }
}

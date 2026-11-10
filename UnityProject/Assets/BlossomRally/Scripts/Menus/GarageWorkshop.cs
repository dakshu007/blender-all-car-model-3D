// GarageWorkshop.cs - the open-front village workshop around the garage turntable (procedural,
// low-poly, vertex coloured, merged into a few meshes): stone-tiled floor with a gold ring around
// the turntable, cream plaster back wall with dark timber posts and red-lacquer beams, lattice
// windows, a lean-to tiled roof with upturned eaves, red lacquer pillars, a gold-lettered plaque,
// swaying red paper lanterns, warm pendant work lights, a tool bench with pegboard and vice, a red
// rolling toolbox, tyre stacks, oil drums, a two-post hydraulic lift, a trolley jack, a rally poster
// and a calendar. Three mechanics (one CPU-posed LifeBatch mesh) work in it: one at the bench with
// a wrench, one polishing the car, one bent over checking tyres. When the car changes they walk
// back in and the bench mechanic comes over to give a thumbs up; in the showroom they step aside.
//
// Local frame: origin = turntable centre on the floor, +X east, +Z north = the open front (camera side).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class GarageWorkshop
    {
        public Transform Root { get; private set; }
        public Vector3 Origin { get; private set; }
        /// <summary>Camera limits inside the workshop (local): x, z ranges and max height.</summary>
        public const float CamMinX = -7.0f, CamMaxX = 7.0f, CamMinZ = -6.0f, CamMaxY = 4.3f, CamMinY = 0.35f;
        /// <summary>Front limit: shortened at build time if the village garage building is close behind the camera.</summary>
        public float CamMaxZ = 8.2f;

        LifeBatch people;
        Mechanic[] crew;
        float clock;

        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }

        static readonly Color32 Stone = C(184, 174, 158), StoneDark = C(132, 124, 114), Lacq = C(168, 32, 38), LacqDark = C(120, 22, 28);
        static readonly Color32 Timber = C(72, 48, 40), Plaster = C(234, 222, 198), Gold = C(214, 166, 74), Tile = C(76, 80, 94), TileDark = C(58, 62, 74);
        static readonly Color32 Wood = C(156, 112, 74), WoodDark = C(116, 80, 54), Steel = C(128, 132, 140), SteelDark = C(70, 74, 80);
        static readonly Color32 Rubber = C(40, 38, 42), Hub = C(92, 94, 100), LiftYellow = C(228, 178, 52), Paper = C(240, 232, 214);

        // ================================================================== build
        public void Build(WorldBuilder w, Vector3 origin, Transform parent)
        {
            Origin = origin;
            var root = new GameObject("Garage_Workshop");
            root.transform.SetParent(parent, true);
            root.transform.position = origin;
            Root = root.transform;

            var g = new LifeGeo();
            var glow = new LifeGeo();
            Floor(g);
            Walls(g, glow);
            Roof(g);
            Bench(g, glow);
            Toolbox(g);
            Tyres(g);
            Drums(g);
            Lift(g);
            Jack(g);
            Pendants(g, glow);

            var mesh = g.ToMesh("GarageWorkshop");
            mesh.RecalculateBounds();
            Mesh(root.transform, "Structure", mesh, MaterialLibrary.Terrain, true);
            if (glow.v.Count > 0) Mesh(root.transform, "Lamps", glow.ToMesh("GarageLamps"), MaterialLibrary.Glow, false);

            // textured boards: poster, calendar, plaque
            Board(root.transform, "Poster", new Vector3(-1.5f, 2.55f, -6.385f), new Vector2(1.25f, 1.7f), 0f, PosterTexture());
            Board(root.transform, "Calendar", new Vector3(2.15f, 2.6f, -6.385f), new Vector2(0.72f, 1.04f), 0f, CalendarTexture());
            Board(root.transform, "Plaque", new Vector3(0f, 4.45f, 5.615f), new Vector2(2.5f, 0.56f), 0f, PlaqueTexture());

            // swaying red paper lanterns under the front eave
            Lantern(root.transform, new Vector3(-4.6f, 4.22f, 5.55f), true);
            Lantern(root.transform, new Vector3(4.6f, 4.22f, 5.55f), true);
            Lantern(root.transform, new Vector3(-7.0f, 3.6f, -6.0f), false);
            Lantern(root.transform, new Vector3(7.0f, 3.6f, -6.0f), false);

            Colliders(root.transform);
            BuildCrew(root.transform);
            ProbeFront();
        }

        /// <summary>Keep the camera out of the garage building / anything solid in front of the workshop.</summary>
        void ProbeFront()
        {
            try
            {
                Physics.SyncTransforms();
                foreach (float x in new[] { -3.5f, 0f, 3.5f })
                    foreach (float y in new[] { 1.2f, 2.6f })
                    {
                        Vector3 o = Origin + new Vector3(x, y, 6.6f);
                        RaycastHit hit;
                        if (Physics.SphereCast(o, 0.3f, Vector3.forward, out hit, 4f, ~(1 << CarExtras.CarLayer), QueryTriggerInteraction.Ignore))
                            CamMaxZ = Mathf.Min(CamMaxZ, 6.6f + hit.distance - 0.6f);
                    }
                CamMaxZ = Mathf.Max(CamMaxZ, 6.2f);
            }
            catch (System.Exception) { }
        }

        static void Mesh(Transform parent, string name, Mesh m, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        static void Floor(LifeGeo g)
        {
            // stone tiles (1 m) with colour variation and a few oil stains
            for (int ix = -8; ix < 8; ix++)
                for (int iz = -7; iz < 6; iz++)
                {
                    float x0 = Mathf.Max(ix, -7.6f), x1 = Mathf.Min(ix + 1, 7.6f), z0 = Mathf.Max(iz, -6.8f), z1 = Mathf.Min(iz + 1, 5.7f);
                    if (x1 <= x0 || z1 <= z0) continue;
                    float h = Noise.Hash01(ix, iz, 4);
                    Color32 c = LifeGeo.Shade(Stone, 0.9f + h * 0.16f);
                    if (Noise.Hash01(ix, iz, 9) < 0.07f) c = LifeGeo.Shade(StoneDark, 0.85f);
                    const float gap = 0.025f;
                    Vector3 a = new Vector3(x0 + gap, 0f, z0 + gap), b = new Vector3(x0 + gap, 0f, z1 - gap), d = new Vector3(x1 - gap, 0f, z1 - gap), e = new Vector3(x1 - gap, 0f, z0 + gap);
                    g.QuadOut(a, b, d, e, Vector3.up, c, c, c, c);
                }
            // grout / slab under the tiles, and a deep plinth that hides uneven ground
            g.Box(new Vector3(0f, -0.012f, -0.55f), new Vector3(7.6f, 0.0f + 0.004f, 6.25f), C(110, 104, 96));
            g.Box(new Vector3(0f, -1.3f, -0.55f), new Vector3(7.65f, 1.28f, 6.3f), StoneDark);
            g.Box(new Vector3(0f, -0.62f, 6.05f), new Vector3(7.65f, 0.55f, 0.35f), StoneDark);         // front step
            g.Box(new Vector3(0f, -0.06f, 5.85f), new Vector3(7.7f, 0.06f, 0.16f), Lacq);              // lacquered sill
            // gold ring around the turntable
            const int seg = 64;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                g.QuadOut(p0 * 3.55f + Vector3.up * 0.006f, p1 * 3.55f + Vector3.up * 0.006f, p1 * 3.68f + Vector3.up * 0.006f, p0 * 3.68f + Vector3.up * 0.006f, Vector3.up, Gold, Gold, Gold, Gold);
            }
        }

        static void Walls(LifeGeo g, LifeGeo glow)
        {
            // back wall
            g.Box(new Vector3(0f, 2.78f, -6.6f), new Vector3(7.7f, 2.78f, 0.2f), Plaster);
            g.Box(new Vector3(0f, 0.36f, -6.37f), new Vector3(7.6f, 0.36f, 0.04f), LacqDark);               // skirting
            g.Box(new Vector3(0f, 4.55f, -6.36f), new Vector3(7.7f, 0.2f, 0.06f), Lacq);                    // upper beam
            g.Box(new Vector3(0f, 4.31f, -6.33f), new Vector3(7.7f, 0.035f, 0.05f), Gold);
            g.Box(new Vector3(0f, 1.25f, -6.36f), new Vector3(7.6f, 0.07f, 0.05f), Timber);                 // chair rail
            foreach (float x in new[] { -7.45f, -3.75f, 0f, 3.75f, 7.45f })
                g.Box(new Vector3(x, 2.78f, -6.36f), new Vector3(0.15f, 2.78f, 0.07f), Timber);
            // lattice windows high on the back wall
            foreach (float cx in new[] { -5.6f, 5.6f })
            {
                Vector3 c = new Vector3(cx, 3.45f, -6.36f);
                g.Box(c, new Vector3(0.95f, 0.5f, 0.03f), C(248, 236, 206));
                g.Box(c + new Vector3(0f, 0.5f, 0.02f), new Vector3(1.0f, 0.06f, 0.05f), Timber);
                g.Box(c + new Vector3(0f, -0.5f, 0.02f), new Vector3(1.0f, 0.06f, 0.05f), Timber);
                g.Box(c + new Vector3(-0.98f, 0f, 0.02f), new Vector3(0.06f, 0.5f, 0.05f), Timber);
                g.Box(c + new Vector3(0.98f, 0f, 0.02f), new Vector3(0.06f, 0.5f, 0.05f), Timber);
                for (int i = -3; i <= 3; i++) g.Box(c + new Vector3(i * 0.26f, 0f, 0.03f), new Vector3(0.018f, 0.46f, 0.02f), Timber);
                for (int j = -1; j <= 1; j++) g.Box(c + new Vector3(0f, j * 0.24f, 0.03f), new Vector3(0.94f, 0.018f, 0.02f), Timber);
            }
            // west wall (closed half) with posts
            g.Box(new Vector3(-7.62f, 2.78f, -3.4f), new Vector3(0.2f, 2.78f, 3.4f), Plaster);
            g.Box(new Vector3(-7.39f, 0.36f, -3.4f), new Vector3(0.04f, 0.36f, 3.3f), LacqDark);
            g.Box(new Vector3(-7.39f, 1.25f, -3.4f), new Vector3(0.05f, 0.07f, 3.3f), Timber);
            foreach (float z in new[] { -3.4f, 0f })
                g.Box(new Vector3(-7.38f, 2.78f, z), new Vector3(0.07f, 2.78f, 0.15f), Timber);
            // pillars: red lacquer columns on stone bases
            foreach (var p in new[] { new Vector2(-7.5f, 5.3f), new Vector2(7.5f, 5.3f), new Vector2(7.5f, -0.6f), new Vector2(7.5f, -6.4f), new Vector2(-7.5f, 0.15f) })
            {
                Vector3 b = new Vector3(p.x, 0f, p.y);
                g.Frustum(b, b + Vector3.up * 0.26f, 0.34f, 0.34f, 0.3f, 0.3f, 8, Mathf.PI / 8f, StoneDark, Stone, false, true);
                g.Frustum(b + Vector3.up * 0.26f, b + Vector3.up * 4.4f, 0.2f, 0.2f, 0.19f, 0.19f, 8, Mathf.PI / 8f, Lacq, LacqDark, false, false);
                g.Frustum(b + Vector3.up * 4.0f, b + Vector3.up * 4.12f, 0.215f, 0.215f, 0.215f, 0.215f, 8, Mathf.PI / 8f, Gold, Gold, false, false);
            }
            // top beams around the opening
            g.Box(new Vector3(0f, 4.48f, 5.35f), new Vector3(7.9f, 0.24f, 0.2f), Lacq);
            g.Box(new Vector3(0f, 4.2f, 5.55f), new Vector3(7.9f, 0.035f, 0.02f), Gold);
            g.Box(new Vector3(0f, 4.76f, 5.55f), new Vector3(7.9f, 0.035f, 0.02f), Gold);
            g.Box(new Vector3(7.5f, 4.48f, -0.55f), new Vector3(0.2f, 0.24f, 6.05f), Lacq);
            g.Box(new Vector3(-7.5f, 4.48f, 2.7f), new Vector3(0.2f, 0.24f, 2.75f), Lacq);
            // plaque backing + gold frame (texture board sits in front)
            g.Box(new Vector3(0f, 4.45f, 5.56f), new Vector3(1.36f, 0.36f, 0.04f), Gold);
            // spare wheel hanging on the west wall + coiled hose
            Wheel(g, new Vector3(-7.32f, 2.7f, -1.6f), Vector3.right, 0.34f, 0.2f);
            for (int k = 0; k < 4; k++)
                g.Frustum(new Vector3(-7.36f, 2.2f + k * 0.035f, -4.6f), new Vector3(-7.25f, 2.2f + k * 0.035f, -4.6f), 0.3f - k * 0.01f, 0.3f - k * 0.01f, 0.3f - k * 0.01f, 0.3f - k * 0.01f, 10, 0f, C(60, 120, 90), C(60, 120, 90), false, false);
        }

        static float RoofY(float z) { return 5.62f + (z + 7.05f) / 13.5f * (4.72f - 5.62f); }

        static void Roof(LifeGeo g)
        {
            const float x0 = -8.3f, x1 = 8.3f, zb = -7.05f, zf = 6.45f;
            float yb = RoofY(zb), yf = RoofY(zf);
            // tile strips with ridges (alternating shades), thickness 0.12
            int strips = 40;
            for (int i = 0; i < strips; i++)
            {
                float xa = Mathf.Lerp(x0, x1, i / (float)strips), xb = Mathf.Lerp(x0, x1, (i + 1) / (float)strips), xm = (xa + xb) * 0.5f;
                Color32 c = (i & 1) == 0 ? Tile : TileDark;
                Vector3 up = new Vector3(0f, 0.05f, 0f);
                g.QuadOut(new Vector3(xa, yb + 0.12f, zb), new Vector3(xm, yb + 0.12f, zb) + up, new Vector3(xm, yf + 0.12f, zf) + up, new Vector3(xa, yf + 0.12f, zf), Vector3.up, c, c, c, c);
                g.QuadOut(new Vector3(xm, yb + 0.12f, zb) + up, new Vector3(xb, yb + 0.12f, zb), new Vector3(xb, yf + 0.12f, zf), new Vector3(xm, yf + 0.12f, zf) + up, Vector3.up, c, c, c, c);
            }
            // underside (wooden ceiling) and edges
            Color32 ceil = C(132, 96, 70);
            g.QuadOut(new Vector3(x0, yb, zb), new Vector3(x1, yb, zb), new Vector3(x1, yf, zf), new Vector3(x0, yf, zf), Vector3.down, ceil, ceil, ceil, ceil);
            g.QuadOut(new Vector3(x0, yf, zf), new Vector3(x1, yf, zf), new Vector3(x1, yf + 0.14f, zf), new Vector3(x0, yf + 0.14f, zf), Vector3.forward, LacqDark, LacqDark, LacqDark, LacqDark);
            g.QuadOut(new Vector3(x0, yb, zb), new Vector3(x0, yf, zf), new Vector3(x0, yf + 0.14f, zf), new Vector3(x0, yb + 0.14f, zb), Vector3.left, TileDark, TileDark, TileDark, TileDark);
            g.QuadOut(new Vector3(x1, yb, zb), new Vector3(x1, yf, zf), new Vector3(x1, yf + 0.14f, zf), new Vector3(x1, yb + 0.14f, zb), Vector3.right, TileDark, TileDark, TileDark, TileDark);
            // ridge cap along the back with gold end ornaments
            g.Box(new Vector3(0f, yb + 0.24f, zb + 0.1f), new Vector3(8.45f, 0.12f, 0.18f), C(46, 48, 58));
            for (int s = -1; s <= 1; s += 2)
            {
                g.Ellipsoid(new Vector3(s * 8.45f, yb + 0.42f, zb + 0.1f), new Vector3(0.14f, 0.2f, 0.14f), 6, 4, Gold, Gold);
                // upturned front eave corners
                Vector3 a = new Vector3(s * 8.3f, yf + 0.12f, zf), b = new Vector3(s * 7.7f, yf + 0.12f, zf), tip = new Vector3(s * 8.75f, yf + 0.62f, zf + 0.45f), b2 = new Vector3(s * 8.3f, yf + 0.12f, zf - 0.7f);
                g.Tri2(a, b, tip, Tile, Tile, TileDark);
                g.Tri2(a, b2, tip, Tile, Tile, TileDark);
                g.Ellipsoid(tip, new Vector3(0.07f, 0.07f, 0.07f), 5, 3, Gold, Gold);
            }
            // ceiling beams
            foreach (float x in new[] { -5.2f, -1.8f, 1.8f, 5.2f })
            {
                Vector3 pb = new Vector3(x, RoofY(-6.4f) - 0.14f, -6.4f), pf = new Vector3(x, RoofY(5.2f) - 0.14f, 5.2f);
                g.Frustum(pb, pf, 0.11f, 0.11f, 0.11f, 0.11f, 4, Mathf.PI / 4f, Timber, Timber, false, false);
            }
        }

        static void Bench(LifeGeo g, LifeGeo glow)
        {
            Vector3 c = new Vector3(-5.35f, 0f, -5.95f);
            g.Box(c + new Vector3(0f, 0.94f, 0f), new Vector3(1.75f, 0.05f, 0.42f), Wood);
            g.Box(c + new Vector3(0f, 0.86f, 0.4f), new Vector3(1.75f, 0.04f, 0.03f), WoodDark);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Box(c + new Vector3(sx * 1.62f, 0.45f, sz * 0.33f), new Vector3(0.05f, 0.45f, 0.05f), WoodDark);
            g.Box(c + new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.03f, 0.36f), WoodDark);                       // lower shelf
            g.Box(c + new Vector3(0.95f, 0.68f, 0.18f), new Vector3(0.6f, 0.18f, 0.22f), Lacq);                     // drawers
            g.Box(c + new Vector3(0.95f, 0.68f, 0.405f), new Vector3(0.12f, 0.02f, 0.01f), Gold);
            // vice
            g.Box(c + new Vector3(-1.35f, 1.06f, 0.25f), new Vector3(0.12f, 0.08f, 0.14f), SteelDark);
            g.Box(c + new Vector3(-1.35f, 1.14f, 0.38f), new Vector3(0.1f, 0.06f, 0.03f), Steel);
            g.Frustum(c + new Vector3(-1.35f, 1.1f, 0.42f), c + new Vector3(-1.35f, 1.1f, 0.62f), 0.015f, 0.015f, 0.015f, 0.015f, 4, 0f, Steel, Steel, true, true);
            // clutter: tins, a gearbox casing, small boxes
            g.Frustum(c + new Vector3(-0.6f, 0.99f, 0.1f), c + new Vector3(-0.6f, 1.17f, 0.1f), 0.07f, 0.07f, 0.07f, 0.07f, 8, 0f, C(196, 60, 52), C(220, 90, 70), false, true);
            g.Frustum(c + new Vector3(-0.42f, 0.99f, 0.05f), c + new Vector3(-0.42f, 1.12f, 0.05f), 0.06f, 0.06f, 0.06f, 0.06f, 8, 0f, C(60, 110, 160), C(80, 140, 190), false, true);
            g.Box(c + new Vector3(0.2f, 1.08f, -0.05f), new Vector3(0.26f, 0.13f, 0.18f), C(96, 98, 104));
            g.Box(c + new Vector3(0.75f, 1.02f, 0.15f), new Vector3(0.18f, 0.07f, 0.1f), C(210, 160, 60));
            // desk lamp
            g.Frustum(c + new Vector3(1.4f, 0.99f, -0.15f), c + new Vector3(1.4f, 1.02f, -0.15f), 0.09f, 0.09f, 0.08f, 0.08f, 8, 0f, SteelDark, SteelDark, false, true);
            g.Frustum(c + new Vector3(1.4f, 1.02f, -0.15f), c + new Vector3(1.25f, 1.45f, 0.0f), 0.015f, 0.015f, 0.015f, 0.015f, 4, 0f, SteelDark, SteelDark, false, false);
            g.Frustum(c + new Vector3(1.25f, 1.45f, 0.0f), c + new Vector3(1.12f, 1.33f, 0.1f), 0.04f, 0.04f, 0.12f, 0.12f, 8, 0f, C(40, 90, 70), C(50, 110, 86), false, false);
            glow.Ellipsoid(c + new Vector3(1.1f, 1.31f, 0.11f), new Vector3(0.05f, 0.05f, 0.05f), 6, 3, C(255, 230, 170, 255), C(255, 230, 170, 255));
            // pegboard with hanging tools
            Vector3 pb = new Vector3(c.x, 2.2f, -6.37f);
            g.Box(pb, new Vector3(1.7f, 0.68f, 0.02f), C(196, 160, 116));
            var rng = new System.Random(5);
            for (int i = 0; i < 11; i++)
            {
                float x = -1.5f + i * 0.3f;
                float len = 0.18f + (float)rng.NextDouble() * 0.28f;
                Color32 tc = i % 4 == 1 ? C(196, 48, 44) : Steel;
                g.Box(pb + new Vector3(x, 0.32f - len, 0.04f), new Vector3(0.022f, len, 0.015f), tc);
                if (i % 3 == 0) g.Box(pb + new Vector3(x, 0.32f - len * 2f, 0.04f), new Vector3(0.05f, 0.03f, 0.016f), tc);     // spanner jaw
                if (i % 4 == 2) g.Box(pb + new Vector3(x, 0.3f, 0.05f), new Vector3(0.08f, 0.035f, 0.025f), WoodDark);        // hammer head
            }
            g.Box(pb + new Vector3(0f, -0.48f, 0.03f), new Vector3(1.55f, 0.04f, 0.06f), WoodDark);   // small shelf
            for (int i = 0; i < 6; i++)
                g.Frustum(pb + new Vector3(-1.3f + i * 0.5f, -0.44f, 0.05f), pb + new Vector3(-1.3f + i * 0.5f, -0.3f, 0.05f), 0.045f, 0.045f, 0.045f, 0.045f, 6, 0f,
                          i % 2 == 0 ? C(220, 180, 60) : C(70, 140, 110), C(240, 220, 200), false, true);
        }

        static void Toolbox(LifeGeo g)
        {
            Vector3 c = new Vector3(-2.75f, 0f, -5.85f);
            Color32 red = C(196, 40, 40);
            g.Box(c + new Vector3(0f, 0.58f, 0f), new Vector3(0.56f, 0.46f, 0.32f), red);
            g.Box(c + new Vector3(0f, 1.1f, 0f), new Vector3(0.58f, 0.06f, 0.34f), C(150, 28, 30));
            g.Box(c + new Vector3(0f, 1.28f, -0.02f), new Vector3(0.5f, 0.13f, 0.27f), red);                 // top chest
            for (int i = 0; i < 5; i++)
            {
                float y = 0.24f + i * 0.17f;
                g.Box(c + new Vector3(0f, y + 0.07f, 0.322f), new Vector3(0.54f, 0.006f, 0.004f), C(110, 20, 22));
                g.Box(c + new Vector3(0f, y, 0.335f), new Vector3(0.18f, 0.012f, 0.012f), C(220, 220, 226));
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Frustum(c + new Vector3(sx * 0.46f - 0.03f, 0.06f, sz * 0.24f), c + new Vector3(sx * 0.46f + 0.03f, 0.06f, sz * 0.24f), 0.06f, 0.06f, 0.06f, 0.06f, 8, 0f, Rubber, Rubber, true, true);
            // a wrench and a screwdriver lying on top
            g.Box(c + new Vector3(-0.15f, 1.43f, 0.02f), new Vector3(0.16f, 0.012f, 0.025f), Steel);
            g.Box(c + new Vector3(0.2f, 1.43f, -0.05f), new Vector3(0.12f, 0.015f, 0.015f), C(230, 190, 40));
        }

        static void Wheel(LifeGeo g, Vector3 c, Vector3 axis, float r, float w)
        {
            Vector3 h = axis.normalized * (w * 0.5f);
            g.Frustum(c - h, c + h, r, r, r, r, 14, 0f, Rubber, Rubber, true, true);
            g.Disc(c + h + axis.normalized * 0.005f, r * 0.6f, 10, 0f, axis.normalized, Hub, LifeGeo.Shade(Hub, 0.8f));
        }

        static void Tyres(LifeGeo g)
        {
            Stack(g, new Vector2(-6.55f, 3.9f), 4);
            Stack(g, new Vector2(-6.6f, -1.1f), 3);
            Stack(g, new Vector2(-5.95f, -1.75f), 2);
            Stack(g, new Vector2(6.7f, 4.3f), 2);
            // one tyre leaning against the back wall
            Wheel(g, new Vector3(-0.35f, 0.33f, -6.2f), new Vector3(0f, 0.25f, 1f), 0.33f, 0.22f);
        }

        static void Stack(LifeGeo g, Vector2 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float jitter = (Noise.Hash01((int)(p.x * 10f), i, 3) - 0.5f) * 0.06f;
                Vector3 b = new Vector3(p.x + jitter, i * 0.23f, p.y - jitter);
                g.Frustum(b, b + Vector3.up * 0.22f, 0.34f, 0.34f, 0.34f, 0.34f, 14, i * 0.2f, Rubber, LifeGeo.Shade(Rubber, 1.2f), true, true);
                g.Disc(b + Vector3.up * 0.226f, 0.19f, 10, 0f, Vector3.up, C(24, 22, 26), C(24, 22, 26));
            }
        }

        static void Drums(LifeGeo g)
        {
            Color32[] cols = { C(52, 92, 150), C(176, 52, 44), C(70, 120, 84) };
            Vector2[] ps = { new Vector2(6.75f, -1.75f), new Vector2(6.8f, -1.05f), new Vector2(6.15f, -1.45f) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 b = new Vector3(ps[i].x, 0f, ps[i].y);
                Color32 c = cols[i];
                g.Frustum(b, b + Vector3.up * 0.9f, 0.29f, 0.29f, 0.29f, 0.29f, 12, 0f, LifeGeo.Shade(c, 0.85f), c, false, false);
                g.Disc(b + Vector3.up * 0.9f, 0.29f, 12, 0f, Vector3.up, LifeGeo.Shade(c, 1.1f), LifeGeo.Shade(c, 0.95f));
                foreach (float y in new[] { 0.3f, 0.6f })
                    g.Frustum(b + Vector3.up * (y - 0.02f), b + Vector3.up * (y + 0.02f), 0.305f, 0.305f, 0.305f, 0.305f, 12, 0f, LifeGeo.Shade(c, 0.7f), LifeGeo.Shade(c, 0.7f), false, false);
                g.Frustum(b + new Vector3(0.12f, 0.9f, 0.05f), b + new Vector3(0.12f, 0.94f, 0.05f), 0.04f, 0.04f, 0.04f, 0.04f, 6, 0f, Steel, Steel, false, true);
            }
        }

        static void Lift(LifeGeo g)
        {
            foreach (float x in new[] { 3.9f, 7.0f })
            {
                Vector3 b = new Vector3(x, 0f, -4.7f);
                g.Box(b + new Vector3(0f, 0.03f, 0f), new Vector3(0.32f, 0.03f, 0.32f), SteelDark);
                g.Box(b + new Vector3(0f, 1.95f, 0f), new Vector3(0.16f, 1.95f, 0.16f), LiftYellow);
                g.Box(b + new Vector3(0f, 1.9f, 0.17f), new Vector3(0.05f, 1.8f, 0.02f), LifeGeo.Shade(LiftYellow, 0.8f));
                float dir = x < 5f ? 1f : -1f;
                // carriage + two swing arms with rubber pads
                g.Box(b + new Vector3(dir * 0.2f, 0.5f, 0f), new Vector3(0.08f, 0.16f, 0.2f), SteelDark);
                foreach (float dz in new[] { -0.55f, 0.55f })
                {
                    Vector3 a0 = b + new Vector3(dir * 0.25f, 0.45f, dz * 0.3f), a1 = b + new Vector3(dir * 1.15f, 0.45f, dz);
                    g.Frustum(a0, a1, 0.05f, 0.04f, 0.05f, 0.04f, 4, Mathf.PI / 4f, Steel, Steel, true, true);
                    g.Frustum(a1, a1 + Vector3.up * 0.1f, 0.08f, 0.08f, 0.08f, 0.08f, 8, 0f, Rubber, Rubber, false, true);
                }
                g.Box(b + new Vector3(dir * -0.2f, 1.3f, 0.1f), new Vector3(0.05f, 0.12f, 0.08f), C(40, 40, 46));     // control box
            }
            g.Box(new Vector3(5.45f, 3.95f, -4.7f), new Vector3(1.72f, 0.1f, 0.12f), LiftYellow);                // overhead bar
            g.Frustum(new Vector3(3.95f, 3.85f, -4.6f), new Vector3(6.95f, 3.85f, -4.6f), 0.02f, 0.02f, 0.02f, 0.02f, 4, 0f, Rubber, Rubber, false, false);
            g.Box(new Vector3(5.45f, 0.012f, -4.7f), new Vector3(1.4f, 0.008f, 0.05f), C(230, 200, 60));             // floor line
        }

        static void Jack(LifeGeo g)
        {
            Vector3 c = new Vector3(2.9f, 0f, 3.75f);
            Color32 red = C(200, 44, 40);
            g.Box(c + new Vector3(0f, 0.12f, 0f), new Vector3(0.16f, 0.07f, 0.38f), red);
            g.Frustum(c + new Vector3(0f, 0.2f, 0.25f), c + new Vector3(0f, 0.32f, 0.38f), 0.07f, 0.07f, 0.07f, 0.07f, 8, 0f, Steel, Steel, true, true);
            g.Frustum(c + new Vector3(0f, 0.18f, -0.35f), c + new Vector3(0f, 0.95f, -0.85f), 0.025f, 0.025f, 0.025f, 0.025f, 4, 0f, SteelDark, SteelDark, false, true);
            for (int s = -1; s <= 1; s += 2)
                g.Frustum(c + new Vector3(s * 0.14f, 0.06f, 0.3f), c + new Vector3(s * 0.2f, 0.06f, 0.3f), 0.05f, 0.05f, 0.05f, 0.05f, 8, 0f, Rubber, Rubber, true, true);
        }

        static void Pendants(LifeGeo g, LifeGeo glow)
        {
            foreach (var p in new[] { new Vector2(-2.3f, 0.3f), new Vector2(2.3f, 0.3f), new Vector2(0f, -3.4f) })
            {
                float top = RoofY(p.y) - 0.02f;
                Vector3 b = new Vector3(p.x, 3.72f, p.y);
                g.Frustum(new Vector3(p.x, top, p.y), b + Vector3.up * 0.04f, 0.012f, 0.012f, 0.012f, 0.012f, 4, 0f, C(30, 28, 30), C(30, 28, 30), false, false);
                g.Frustum(b + Vector3.up * 0.05f, b - Vector3.up * 0.26f, 0.07f, 0.07f, 0.42f, 0.42f, 12, 0f, C(36, 76, 62), C(56, 104, 86), true, false);
                g.Frustum(b - Vector3.up * 0.26f, b - Vector3.up * 0.28f, 0.42f, 0.42f, 0.43f, 0.43f, 12, 0f, Gold, Gold, false, false);
                glow.Ellipsoid(b - Vector3.up * 0.24f, new Vector3(0.13f, 0.11f, 0.13f), 8, 4, C(255, 236, 190, 255), C(255, 214, 150, 255));
            }
        }

        static void Lantern(Transform parent, Vector3 pos, bool swing)
        {
            var pivot = new GameObject("Lantern");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pos;
            var caps = new LifeGeo();
            var body = new LifeGeo();
            Color32 dark = C(40, 30, 30);
            caps.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.2f, 0f), 0.012f, 0.012f, 0.012f, 0.012f, 4, 0f, dark, dark, false, false);
            caps.Frustum(new Vector3(0f, -0.2f, 0f), new Vector3(0f, -0.27f, 0f), 0.13f, 0.13f, 0.15f, 0.15f, 10, 0f, Gold, LifeGeo.Shade(Gold, 0.8f), true, true);
            caps.Frustum(new Vector3(0f, -0.83f, 0f), new Vector3(0f, -0.9f, 0f), 0.15f, 0.15f, 0.12f, 0.12f, 10, 0f, LifeGeo.Shade(Gold, 0.8f), Gold, true, true);
            caps.Frustum(new Vector3(0f, -0.9f, 0f), new Vector3(0f, -1.15f, 0f), 0.03f, 0.03f, 0.07f, 0.07f, 6, 0f, C(220, 40, 40), C(240, 60, 50), false, true);
            body.Ellipsoid(new Vector3(0f, -0.55f, 0f), new Vector3(0.3f, 0.32f, 0.3f), 12, 7, C(236, 66, 50, 255), C(196, 40, 36, 255));
            for (int k = 0; k < 6; k++)    // ribs
            {
                float a = k * Mathf.PI / 3f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                caps.Frustum(d * 0.13f + Vector3.up * -0.26f, d * 0.305f + Vector3.up * -0.55f, 0.012f, 0.012f, 0.012f, 0.012f, 3, 0f, LacqDark, LacqDark, false, false);
                caps.Frustum(d * 0.305f + Vector3.up * -0.55f, d * 0.15f + Vector3.up * -0.84f, 0.012f, 0.012f, 0.012f, 0.012f, 3, 0f, LacqDark, LacqDark, false, false);
            }
            Mesh(pivot.transform, "Frame", caps.ToMesh("LanternFrame"), MaterialLibrary.Terrain, false);
            Mesh(pivot.transform, "Paper", body.ToMesh("LanternPaper"), MaterialLibrary.Glow, false);
            if (swing) pivot.AddComponent<LanternSwing>().amplitude = 4f;
        }

        static void Board(Transform parent, string name, Vector3 pos, Vector2 size, float yaw, Texture2D tex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // a Quad's visible side faces -Z: turn it round so it faces +Z (into the workshop / towards the camera)
            go.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.Standard("Garage" + name, Color.white, 0.15f, 0f, tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Colliders(Transform root)
        {
            var go = new GameObject("Colliders");
            go.transform.SetParent(root, false);
            System.Action<Vector3, Vector3> box = (c, s) =>
            {
                var b = go.AddComponent<BoxCollider>();
                b.center = c;
                b.size = s;
            };
            box(new Vector3(0f, -0.26f, -0.55f), new Vector3(15.3f, 0.5f, 12.6f));       // floor (top just under the tiles)
            box(new Vector3(0f, 2.78f, -6.6f), new Vector3(15.4f, 5.56f, 0.4f));         // back wall
            box(new Vector3(-7.62f, 2.78f, -3.4f), new Vector3(0.4f, 5.56f, 6.8f));      // west wall
            foreach (var p in new[] { new Vector2(-7.5f, 5.3f), new Vector2(7.5f, 5.3f), new Vector2(7.5f, -0.6f), new Vector2(7.5f, -6.4f), new Vector2(-7.5f, 0.15f) })
                box(new Vector3(p.x, 2.2f, p.y), new Vector3(0.6f, 4.4f, 0.6f));
            box(new Vector3(-5.35f, 0.5f, -5.95f), new Vector3(3.5f, 1f, 0.85f));        // bench
            box(new Vector3(-2.75f, 0.7f, -5.85f), new Vector3(1.15f, 1.4f, 0.68f));     // toolbox
            box(new Vector3(3.9f, 1.95f, -4.7f), new Vector3(0.34f, 3.9f, 0.34f));       // lift posts
            box(new Vector3(7.0f, 1.95f, -4.7f), new Vector3(0.34f, 3.9f, 0.34f));
            box(new Vector3(6.55f, 0.45f, -1.4f), new Vector3(1.2f, 0.9f, 1.3f));        // drums
            box(new Vector3(-6.55f, 0.46f, 3.9f), new Vector3(0.7f, 0.92f, 0.7f));       // tyre stacks
            box(new Vector3(-6.3f, 0.35f, -1.4f), new Vector3(1.3f, 0.7f, 1.3f));
            box(new Vector3(6.7f, 0.23f, 4.3f), new Vector3(0.7f, 0.46f, 0.7f));
        }

        // ================================================================== textures
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { 'A', new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" } },
            { 'B', new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " } },
            { 'E', new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" } },
            { 'G', new[] { " ####", "#    ", "#    ", "#  ##", "#   #", "#   #", " ### " } },
            { 'I', new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " } },
            { 'L', new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" } },
            { 'M', new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" } },
            { 'O', new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " } },
            { 'P', new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " } },
            { 'R', new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" } },
            { 'S', new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " } },
            { 'Y', new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " } },
        };

        /// <summary>Pixel-font text into a top-down Color32 buffer (centred on cx, top at y).</summary>
        static void Write(Color32[] px, int w, int h, string text, int cx, int y, int scale, Color32 c)
        {
            int width = text.Length * 6 * scale - scale;
            int x = cx - width / 2;
            foreach (char ch in text)
            {
                string[] g;
                if (Font.TryGetValue(ch, out g))
                    for (int r = 0; r < 7; r++)
                        for (int k = 0; k < 5; k++)
                            if (g[r][k] == '#')
                                for (int sy = 0; sy < scale; sy++)
                                    for (int sx = 0; sx < scale; sx++)
                                    {
                                        int px0 = x + k * scale + sx, py0 = y + r * scale + sy;
                                        if (px0 >= 0 && px0 < w && py0 >= 0 && py0 < h) px[py0 * w + px0] = c;
                                    }
                x += 6 * scale;
            }
        }

        static Texture2D Upload(string name, Color32[] topDown, int w, int h)
        {
            var flipped = new Color32[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(topDown, y * w, flipped, (h - 1 - y) * w, w);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(flipped);
            t.Apply(true, true);
            return t;
        }

        static Color32 L(Color32 a, Color32 b, float t) { return Color32.Lerp(a, b, t); }

        static Texture2D PosterTexture()
        {
            const int w = 256, h = 348;
            var px = new Color32[w * h];
            Color32 sky0 = C(252, 214, 214, 255), sky1 = C(244, 160, 180, 255), ink = C(36, 28, 34, 255), sun = C(232, 70, 56, 255);
            Color32 border = C(250, 244, 230, 255), road = C(70, 64, 72, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)h;
                    Color32 c = L(sky0, sky1, Mathf.Clamp01(v * 1.4f));
                    float sx = x - 176, sy = y - 104;
                    if (sx * sx + sy * sy < 34 * 34) c = sun;
                    float m1 = 190 - 60 * Mathf.Exp(-Mathf.Pow((x - 70) / 40f, 2f)) - 35 * Mathf.Exp(-Mathf.Pow((x - 190) / 50f, 2f));
                    float m2 = 222 - 40 * Mathf.Exp(-Mathf.Pow((x - 140) / 60f, 2f));
                    if (y > m1) c = L(c, C(120, 100, 130, 255), 0.75f);
                    if (y > m2) c = L(c, C(70, 60, 80, 255), 0.85f);
                    if (y > 250) c = road;
                    if (y > 254 && y < 258 && (x / 16) % 2 == 0) c = C(240, 220, 160, 255);
                    // stylised rally car: body, cabin, wheels
                    float bx = x - 128, by = y - 236;
                    if (Mathf.Abs(bx) < 62 && by > -14 && by < 8) c = C(214, 42, 52, 255);
                    if (Mathf.Abs(bx + 6) < 34 - (by + 30) * 0.3f && by > -32 && by <= -14) c = C(230, 230, 238, 255);
                    if (Mathf.Abs(bx + 6) < 26 - (by + 30) * 0.3f && by > -28 && by <= -16) c = C(90, 120, 160, 255);
                    if (Mathf.Abs(bx) < 62 && by > -4 && by < 0) c = C(255, 255, 255, 255);
                    float w1 = (bx + 36) * (bx + 36) + (by - 8) * (by - 8), w2 = (bx - 36) * (bx - 36) + (by - 8) * (by - 8);
                    if (w1 < 14 * 14 || w2 < 14 * 14) c = ink;
                    if (w1 < 6 * 6 || w2 < 6 * 6) c = C(190, 190, 196, 255);
                    // petals
                    if (Noise.Hash01(x / 5, y / 5, 7) > 0.975f && y < 250) c = C(255, 246, 248, 255);
                    // title band
                    if (y > 268) c = C(40, 22, 36, 255);
                    if (x < 8 || x >= w - 8 || y < 8 || y >= h - 8) c = border;
                    px[y * w + x] = c;
                }
            Write(px, w, h, "BLOSSOM", w / 2, 280, 4, C(255, 188, 206, 255));
            Write(px, w, h, "RALLY", w / 2, 314, 3, C(240, 196, 90, 255));
            return Upload("BR_GaragePoster", px, w, h);
        }

        static Texture2D CalendarTexture()
        {
            const int w = 144, h = 208;
            var px = new Color32[w * h];
            Color32 paper = C(250, 246, 236, 255), red = C(196, 40, 44, 255), grid = C(214, 206, 196, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = paper;
                    if (y < 28) c = red;
                    // picture: blossom branch on pink
                    if (y >= 34 && y < 104 && x >= 8 && x < w - 8)
                    {
                        c = C(250, 226, 230, 255);
                        float br = Mathf.Abs((y - 40) - (x - 8) * 0.45f);
                        if (br < 2.2f) c = C(70, 50, 44, 255);
                        if (Noise.Hash01(x / 6, y / 6, 3) > 0.82f && br < 18f) c = C(246, 150, 180, 255);
                    }
                    // day grid 7 x 5
                    if (y >= 118 && y < 198 && x >= 8 && x < w - 8)
                    {
                        int cx = (x - 8) / 18, cy = (y - 118) / 16;
                        bool line = (x - 8) % 18 == 0 || (y - 118) % 16 == 0;
                        c = line ? grid : paper;
                        if (!line && cx == 6) c = C(252, 228, 228, 255);
                        if (!line && cx == 3 && cy == 2) c = C(250, 200, 120, 255);   // circled day
                        if (!line && ((x - 8) % 18 > 5 && (x - 8) % 18 < 11) && ((y - 118) % 16 > 5 && (y - 118) % 16 < 11) && cy * 7 + cx < 30)
                            c = cx == 6 ? red : C(90, 80, 84, 255);
                    }
                    px[y * w + x] = c;
                }
            Write(px, w, h, "APRIL", w / 2, 7, 2, C(255, 240, 220, 255));
            return Upload("BR_GarageCalendar", px, w, h);
        }

        static Texture2D PlaqueTexture()
        {
            const int w = 320, h = 72;
            var px = new Color32[w * h];
            Color32 bg = C(30, 22, 26, 255), gold = C(232, 186, 84, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool frame = x < 5 || x >= w - 5 || y < 5 || y >= h - 5;
                    bool inner = (x == 9 || x == w - 10 || y == 9 || y == h - 10) && x >= 9 && x <= w - 10 && y >= 9 && y <= h - 10;
                    px[y * w + x] = frame ? C(160, 30, 34, 255) : inner ? gold : bg;
                }
            Write(px, w, h, "BLOSSOM GARAGE", w / 2, 22, 3, gold);
            // tiny blossoms at both ends
            foreach (int cx in new[] { 24, w - 25 })
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 0.4f;
                    int ox = cx + Mathf.RoundToInt(Mathf.Cos(a) * 6f), oy = 36 + Mathf.RoundToInt(Mathf.Sin(a) * 6f);
                    for (int dy = -3; dy <= 3; dy++)
                        for (int dx = -3; dx <= 3; dx++)
                            if (dx * dx + dy * dy <= 9) px[(oy + dy) * w + ox + dx] = C(246, 150, 180, 255);
                }
            return Upload("BR_GaragePlaque", px, w, h);
        }

        // ================================================================== lights
        public void AddLights(List<Light> list)
        {
            var go = new GameObject("WorkLights");
            go.transform.SetParent(Root, false);
            Vector3[] lp = { new Vector3(-2.3f, 3.25f, 0.3f), new Vector3(2.3f, 3.25f, 0.3f), new Vector3(0f, 3.0f, 4.8f) };
            Color[] lc = { new Color(1f, 0.80f, 0.58f), new Color(1f, 0.78f, 0.62f), new Color(1f, 0.70f, 0.80f) };
            for (int i = 0; i < 3; i++)
            {
                var lg = new GameObject("GarageLight" + i);
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = lp[i];
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = i == 2 ? 10f : 9f;
                l.color = lc[i];
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                list.Add(l);
            }
        }

        // ================================================================== mechanics
        enum Mode { Work, Walk, Thumbs, Idle }

        sealed class Mechanic
        {
            public int kind;                     // 0 wrench at the bench, 1 polishing, 2 tyres
            public int legL, legR, torso, armL, armR, prop, thumb;
            public Vector3 pos, target;
            public float yaw, targetYaw, scale = 1f;
            public Mode mode = Mode.Work, after = Mode.Work;
            public float timer, delay, phase, walkPhase;
            public Vector3 home, aside, entry, thumbSpot;
            public float homeYaw, asideYaw;
            // current (smoothed) pose
            public float lean, sway, legSwing, bob;
            public Vector3 armRE, armLE;
            public bool showThumb;
        }

        static readonly Vector3 ShoulderR = new Vector3(0.205f, 0.78f, 0f), ShoulderL = new Vector3(-0.205f, 0.78f, 0f);
        const float HipY = 0.55f;

        static float FaceYaw(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        void BuildCrew(Transform root)
        {
            people = new LifeBatch();
            Color32[] suits = { C(46, 86, 150), C(196, 70, 60), C(56, 120, 104) };
            Color32[] shirts = { C(236, 230, 214), C(250, 214, 120), C(240, 236, 226) };
            Color32[] caps = { C(196, 40, 44), C(40, 44, 70), C(236, 196, 84) };
            crew = new Mechanic[3];
            for (int i = 0; i < 3; i++)
            {
                var m = new Mechanic { kind = i, phase = i * 2.3f, scale = i == 1 ? 0.96f : (i == 2 ? 1.03f : 1f) };
                int leg = people.AddTemplate(LegGeo(suits[i]));
                int torso = people.AddTemplate(TorsoGeo(suits[i], shirts[i], caps[i], i));
                int arm = people.AddTemplate(ArmGeo(suits[i]));
                int prop = people.AddTemplate(i == 0 ? WrenchGeo() : i == 1 ? RagGeo() : GaugeGeo());
                int thumb = people.AddTemplate(ThumbGeo());
                m.legL = people.AddPart(leg); m.legR = people.AddPart(leg);
                m.torso = people.AddPart(torso);
                m.armL = people.AddPart(arm); m.armR = people.AddPart(arm);
                m.prop = people.AddPart(prop);
                m.thumb = people.AddPart(thumb);
                crew[i] = m;
            }
            // spots (local)
            var a = crew[0];
            a.home = new Vector3(-5.2f, 0f, -5.05f); a.homeYaw = 180f; a.aside = a.home; a.asideYaw = 180f;
            a.entry = a.home; a.thumbSpot = new Vector3(-3.5f, 0f, 3.1f);
            var b = crew[1];
            b.home = new Vector3(-3.1f, 0f, 1.75f); b.homeYaw = FaceYaw(b.home, Vector3.zero);
            b.aside = new Vector3(-6.0f, 0f, 2.6f); b.asideYaw = FaceYaw(b.aside, Vector3.zero) + 10f;
            b.entry = new Vector3(-9.6f, 0f, 3.4f);
            var c = crew[2];
            c.home = new Vector3(2.85f, 0f, -2.2f); c.homeYaw = FaceYaw(c.home, Vector3.zero);
            c.aside = new Vector3(5.6f, 0f, -2.6f); c.asideYaw = FaceYaw(c.aside, Vector3.zero);
            c.entry = new Vector3(9.6f, 0f, -0.9f);
            foreach (var m in crew) { m.pos = m.target = m.home; m.yaw = m.targetYaw = m.homeYaw; m.armRE = m.armLE = Vector3.zero; }
            people.Finish("Garage_Mechanics", MaterialLibrary.VertexColorWind, root, 0, true);
            Tick(0f, true);
        }

        static LifeGeo LegGeo(Color32 suit)
        {
            var g = new LifeGeo();
            Color32 boot = C(44, 34, 30);
            g.Frustum(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.46f, 0f), 0.085f, 0.09f, 0.072f, 0.076f, 6, 0f, suit, LifeGeo.Shade(suit, 0.9f), true, false);
            g.Frustum(new Vector3(0f, -0.46f, 0.015f), new Vector3(0f, -0.55f, 0.03f), 0.075f, 0.1f, 0.08f, 0.12f, 6, 0f, boot, boot, false, true);
            return g;
        }

        static LifeGeo TorsoGeo(Color32 suit, Color32 shirt, Color32 cap, int style)
        {
            var g = new LifeGeo();
            Color32 skin = C(240, 202, 168), hair = C(36, 30, 30);
            const float oct = Mathf.PI / 8f;
            g.Frustum(new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.5f, 0f), 0.2f, 0.15f, 0.21f, 0.14f, 8, oct, suit, suit, true, false);
            g.Frustum(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.8f, 0f), 0.21f, 0.14f, 0.15f, 0.11f, 8, oct, shirt, shirt, false, true);
            // overall bib + straps + belt + chest pocket
            g.Box(new Vector3(0f, 0.6f, 0.13f), new Vector3(0.13f, 0.12f, 0.02f), suit);
            for (int s = -1; s <= 1; s += 2) g.Box(new Vector3(s * 0.1f, 0.72f, 0.07f), new Vector3(0.025f, 0.08f, 0.08f), suit);
            g.Frustum(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.13f, 0f), 0.205f, 0.152f, 0.205f, 0.152f, 8, oct, C(40, 34, 30), C(40, 34, 30), false, false);
            g.Box(new Vector3(0.06f, 0.58f, 0.155f), new Vector3(0.04f, 0.04f, 0.006f), LifeGeo.Shade(suit, 0.8f));
            // rag in the back pocket (red)
            g.Box(new Vector3(-0.1f, 0.0f, -0.15f), new Vector3(0.05f, 0.08f, 0.01f), C(210, 50, 50));
            // neck + head
            g.Frustum(new Vector3(0f, 0.8f, 0f), new Vector3(0f, 0.9f, 0f), 0.05f, 0.05f, 0.05f, 0.05f, 6, 0f, skin, skin, false, false);
            g.Ellipsoid(new Vector3(0f, 1.0f, 0.005f), new Vector3(0.115f, 0.13f, 0.12f), 8, 5, skin, skin);
            Color32 cheek = C(240, 150, 140);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 e = new Vector3(0.042f * s, 1.015f, 0.124f), k = new Vector3(0.062f * s, 0.968f, 0.118f);
                g.QuadOut(e + new Vector3(-0.011f, -0.015f, 0f), e + new Vector3(0.011f, -0.015f, 0f), e + new Vector3(0.011f, 0.015f, 0f), e + new Vector3(-0.011f, 0.015f, 0f), Vector3.forward, hair, hair, hair, hair);
                g.QuadOut(k + new Vector3(-0.018f, -0.01f, 0f), k + new Vector3(0.018f, -0.01f, 0f), k + new Vector3(0.018f, 0.01f, 0f), k + new Vector3(-0.018f, 0.01f, 0f), Vector3.forward, cheek, cheek, cheek, cheek);
            }
            g.Ellipsoid(new Vector3(0f, 1.04f, -0.014f), new Vector3(0.123f, 0.105f, 0.122f), 8, 4, hair, hair);
            if (style == 2)
            {
                // headband instead of a cap
                g.Frustum(new Vector3(0f, 1.06f, 0f), new Vector3(0f, 1.1f, 0f), 0.125f, 0.13f, 0.125f, 0.13f, 8, 0f, cap, cap, false, false);
            }
            else
            {
                g.Ellipsoid(new Vector3(0f, 1.07f, -0.005f), new Vector3(0.128f, 0.085f, 0.13f), 8, 3, cap, cap);
                g.Frustum(new Vector3(0f, 1.07f, 0.06f), new Vector3(0f, 1.075f, 0.22f), 0.11f, 0.012f, 0.09f, 0.008f, 6, 0f, cap, cap, true, true);
            }
            return g;
        }

        static LifeGeo ArmGeo(Color32 suit)
        {
            var g = new LifeGeo();
            Color32 glove = C(60, 60, 66);
            g.Frustum(new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.4f, 0f), 0.062f, 0.062f, 0.07f, 0.07f, 6, 0f, suit, suit, true, true);
            g.Ellipsoid(new Vector3(0f, -0.46f, 0f), new Vector3(0.048f, 0.058f, 0.048f), 6, 4, glove, glove);
            return g;
        }

        static LifeGeo WrenchGeo()
        {
            var g = new LifeGeo();
            Color32 st = C(176, 180, 188);
            g.Box(new Vector3(0f, -0.6f, 0.03f), new Vector3(0.016f, 0.13f, 0.008f), st);
            g.Box(new Vector3(0f, -0.74f, 0.03f), new Vector3(0.042f, 0.025f, 0.01f), st);
            return g;
        }

        static LifeGeo RagGeo()
        {
            var g = new LifeGeo();
            g.Ellipsoid(new Vector3(0f, -0.5f, 0.04f), new Vector3(0.08f, 0.05f, 0.07f), 6, 3, C(250, 196, 210), C(236, 160, 182));
            return g;
        }

        static LifeGeo GaugeGeo()
        {
            var g = new LifeGeo();
            g.Box(new Vector3(0f, -0.56f, 0.03f), new Vector3(0.012f, 0.07f, 0.012f), C(30, 30, 34));
            g.Frustum(new Vector3(0f, -0.5f, 0.05f), new Vector3(0f, -0.5f, 0.08f), 0.035f, 0.035f, 0.035f, 0.035f, 8, 0f, C(200, 40, 40), C(240, 240, 240), false, true);
            return g;
        }

        static LifeGeo ThumbGeo()
        {
            var g = new LifeGeo();
            Color32 glove = C(60, 60, 66);
            g.Box(new Vector3(0f, -0.47f, 0.085f), new Vector3(0.02f, 0.02f, 0.05f), glove);
            return g;
        }

        /// <summary>Car changed on the turntable: helpers walk back in, the bench mechanic gives a thumbs up.</summary>
        public void React()
        {
            if (crew == null) return;
            var b = crew[1]; var c = crew[2]; var a = crew[0];
            b.pos = b.entry; WalkTo(b, b.home, b.homeYaw, Mode.Work, 0.15f);
            c.pos = c.entry; WalkTo(c, c.home, c.homeYaw, Mode.Work, 0.4f);
            if (a.mode != Mode.Thumbs) WalkTo(a, a.thumbSpot, 0f, Mode.Thumbs, 0.7f);
        }

        /// <summary>Showroom: helpers step aside (true) or come back to work (false).</summary>
        public void StepAside(bool aside)
        {
            if (crew == null) return;
            for (int i = 1; i < 3; i++)
            {
                var m = crew[i];
                if (aside) WalkTo(m, m.aside, m.asideYaw, Mode.Idle, i * 0.25f);
                else WalkTo(m, m.home, m.homeYaw, Mode.Work, i * 0.3f);
            }
        }

        /// <summary>Snap everyone back to their work spots.</summary>
        public void ResetCrew()
        {
            if (crew == null) return;
            foreach (var m in crew) { m.pos = m.target = m.home; m.yaw = m.targetYaw = m.homeYaw; m.mode = Mode.Work; m.delay = 0f; }
        }

        static void WalkTo(Mechanic m, Vector3 target, float yaw, Mode after, float delay)
        {
            m.target = target;
            m.targetYaw = yaw;
            m.after = after;
            m.delay = delay;
            m.mode = Mode.Walk;
            m.timer = 0f;
        }

        /// <summary>Animate the crew. 'force' poses even when far away.</summary>
        public void Tick(float dt, bool force)
        {
            if (crew == null) return;
            clock += dt;
            for (int i = 0; i < crew.Length; i++) UpdateMechanic(crew[i], dt);
            people.Upload();
        }

        void UpdateMechanic(Mechanic m, float dt)
        {
            float t = clock + m.phase;
            m.timer += dt;
            // ---- behaviour ----
            float tLean = 4f, tSway = 0f, tLeg = 0f, tBob = 0f;
            Vector3 tR = new Vector3(-6f, 0f, 8f), tL = new Vector3(-6f, 0f, -8f);
            bool thumb = false;
            if (m.mode == Mode.Walk)
            {
                if (m.delay > 0f) { m.delay -= dt; m.timer = 0f; }
                else
                {
                    Vector3 d = m.target - m.pos;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < 0.04f)
                    {
                        m.pos = m.target;
                        m.mode = m.after;
                        m.timer = 0f;
                    }
                    else
                    {
                        float step = Mathf.Min(dist, 1.45f * dt);
                        m.pos += d / dist * step;
                        float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        m.yaw = Mathf.MoveTowardsAngle(m.yaw, want, 360f * dt);
                        m.walkPhase += dt * 8.5f;
                        float sw = Mathf.Sin(m.walkPhase);
                        tLeg = sw * 26f;
                        tR = new Vector3(-sw * 22f, 0f, 6f);
                        tL = new Vector3(sw * 22f, 0f, -6f);
                        tBob = Mathf.Abs(Mathf.Cos(m.walkPhase)) * 0.035f;
                        tLean = 6f;
                    }
                }
            }
            if (m.mode != Mode.Walk || m.delay > 0f) m.yaw = Mathf.MoveTowardsAngle(m.yaw, m.targetYaw, 220f * dt);
            switch (m.mode)
            {
                case Mode.Work:
                    if (m.kind == 0)
                    {
                        tLean = 12f;
                        tR = new Vector3(-72f + Mathf.Sin(t * 7f) * 16f, 0f, 10f + Mathf.Sin(t * 7f) * 4f);
                        tL = new Vector3(-48f, 0f, -14f);
                        tSway = Mathf.Sin(t * 1.3f) * 2f;
                        // now and then straighten up and stretch
                        if (Mathf.Repeat(t, 11f) > 9.4f) { tLean = -2f; tR = new Vector3(-8f, 0f, 12f); tL = new Vector3(-8f, 0f, -12f); }
                    }
                    else if (m.kind == 1)
                    {
                        tLean = 16f;
                        tR = new Vector3(-62f + Mathf.Sin(t * 4.2f) * 13f, 0f, 14f + Mathf.Cos(t * 4.2f) * 16f);
                        tL = new Vector3(-20f, 0f, -18f);
                        tSway = Mathf.Sin(t * 2.1f) * 4f;
                        tBob = Mathf.Abs(Mathf.Sin(t * 2.1f)) * 0.012f;
                    }
                    else
                    {
                        // bent over the tyre, pressing the gauge; stands up every few seconds
                        float cyc = Mathf.Repeat(t, 8f);
                        bool up = cyc > 6.2f;
                        tLean = up ? 2f : 58f + Mathf.Sin(t * 1.7f) * 3f;
                        tR = up ? new Vector3(-30f, 0f, 10f) : new Vector3(-22f + Mathf.Sin(t * 5f) * 6f, 0f, 8f);
                        tL = up ? new Vector3(-6f, 0f, -12f) : new Vector3(-12f, 0f, -22f);
                        tLeg = up ? 0f : 6f;
                    }
                    break;
                case Mode.Thumbs:
                    thumb = true;
                    tLean = -4f;
                    tR = new Vector3(-96f + Mathf.Sin(m.timer * 9f) * 5f, 0f, 6f);
                    tL = new Vector3(-6f, 0f, -10f);
                    tBob = Mathf.Max(0f, Mathf.Sin(m.timer * 7f)) * 0.03f;
                    if (m.timer > 2.4f) WalkTo(m, m.home, m.homeYaw, Mode.Work, 0f);
                    break;
                case Mode.Idle:
                    tLean = 0f;
                    tSway = Mathf.Sin(t * 0.8f) * 3f;
                    tR = new Vector3(-10f, 0f, 9f + Mathf.Sin(t) * 2f);
                    tL = new Vector3(-10f, 0f, -9f);
                    m.yaw = Mathf.MoveTowardsAngle(m.yaw, m.targetYaw + Mathf.Sin(t * 0.35f) * 20f, 60f * dt);
                    break;
            }
            // ---- smooth pose ----
            float k = dt > 0f ? 1f - Mathf.Exp(-dt * 9f) : 1f;
            m.lean = Mathf.Lerp(m.lean, tLean, k);
            m.sway = Mathf.Lerp(m.sway, tSway, k);
            m.legSwing = m.mode == Mode.Walk ? tLeg : Mathf.Lerp(m.legSwing, tLeg, k);
            m.bob = Mathf.Lerp(m.bob, tBob, k * 2f > 1f ? 1f : k * 2f);
            m.armRE = Vector3.Lerp(m.armRE, tR, m.mode == Mode.Walk ? 1f : k);
            m.armLE = Vector3.Lerp(m.armLE, tL, m.mode == Mode.Walk ? 1f : k);
            m.showThumb = thumb;

            // ---- matrices ----
            Matrix4x4 root = Matrix4x4.TRS(Origin + m.pos + Vector3.up * m.bob, Quaternion.Euler(0f, m.yaw, 0f), Vector3.one * m.scale);
            Matrix4x4 hip = root * Matrix4x4.TRS(new Vector3(0f, HipY, 0f), Quaternion.Euler(m.lean, 0f, m.sway), Vector3.one);
            people.Set(m.torso, hip);
            people.Set(m.legL, root * Matrix4x4.TRS(new Vector3(-0.09f, HipY, 0f), Quaternion.Euler(m.legSwing, 0f, 0f), Vector3.one));
            people.Set(m.legR, root * Matrix4x4.TRS(new Vector3(0.09f, HipY, 0f), Quaternion.Euler(-m.legSwing, 0f, 0f), Vector3.one));
            Matrix4x4 armR = hip * Matrix4x4.TRS(ShoulderR, Quaternion.Euler(m.armRE.x, m.armRE.y, m.armRE.z), Vector3.one);
            people.Set(m.armR, armR);
            people.Set(m.armL, hip * Matrix4x4.TRS(ShoulderL, Quaternion.Euler(m.armLE.x, m.armLE.y, m.armLE.z), Vector3.one));
            people.Set(m.prop, m.showThumb && m.kind == 0 ? Matrix4x4.zero : armR);
            people.Set(m.thumb, m.showThumb ? armR : Matrix4x4.zero);
        }
    }
}

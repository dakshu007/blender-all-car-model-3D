// PlaceKit.cs - geometry toolkit of the Places module (interiors and entrance dressing), built on LifeGeo / PG / Pal:
//  * PGeo       - one building's geometry in buckets (baked shell, glow, glass) plus its colliders; Finish() turns it
//                 into a handful of meshes under a root (one draw call per material).
//  * PlaceMats  - the few shared materials (baked interior shell, lantern glow, glass, exterior lit / glow).
//  * PlaceKit   - rooms (floor / wainscot / plaster / posts / beams / ceiling), painted window views, lanterns,
//                 shelves, counters, plants, scrolls, rugs, cushions, job boards and glowing hanzi glyphs.
//  * Bake       - cheap "lightmap" in the vertex colours: warm light pools + floor contact shading. Interiors use an
//                 emissive vertex-colour material, so they look lit on Low (no realtime lights) and real point
//                 lights only add a little shading on Medium and up.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    internal struct BakeLight
    {
        public Vector3 pos; public Color col; public float range, intensity;
        public BakeLight(Vector3 p, Color c, float r, float i) { pos = p; col = c; range = r; intensity = i; }
    }

    internal struct ColBox { public Vector3 c, size; public float yaw; }

    /// <summary>Geometry of one interior / exterior in its own local space.</summary>
    internal sealed class PGeo
    {
        public readonly LifeGeo shell = new LifeGeo();   // baked, emissive vertex colour (interiors) / lit (exteriors)
        public readonly LifeGeo glow = new LifeGeo();    // lanterns, candles, painted views, signs
        public readonly LifeGeo glass = new LifeGeo();
        public readonly List<BakeLight> lights = new List<BakeLight>();
        public readonly List<ColBox> cols = new List<ColBox>();
        /// <summary>Collider index -> pitch (degrees, down along +Z) for ramp colliders.</summary>
        public readonly Dictionary<int, float> rampPitch = new Dictionary<int, float>();
        public Color ambient = new Color(0.62f, 0.56f, 0.5f);
        public float floorY;                              // contact shading reference (local)

        public void Col(Vector3 c, Vector3 size, float yaw = 0f) { cols.Add(new ColBox { c = c, size = size, yaw = yaw }); }

        /// <summary>Append a PropModel (Glow kind -> glow bucket, everything else -> shell).</summary>
        public void Add(PropModel m, Vector3 pos, float yaw, float scale = 1f)
        {
            Matrix4x4 mat = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            for (int i = 0; i < PropModel.Kinds; i++)
            {
                var g = m.geo[i];
                if (g == null || g.v.Count == 0) continue;
                PG.Append(i == (int)PK.Glow ? glow : shell, g, mat);
            }
        }

        public void Add(LifeGeo src, Vector3 pos, float yaw, bool toGlow = false)
        {
            PG.Append(toGlow ? glow : shell, src, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
        }

        public void Light(Vector3 p, Color c, float range, float intensity) { lights.Add(new BakeLight(p, c, range, intensity)); }

        /// <summary>Meshes + colliders under root (local space). interior: baked emissive shell; else lit exterior.</summary>
        public void Finish(Transform root, bool interior, string name)
        {
            if (interior) PlaceKit.Bake(shell, lights, ambient, floorY);
            MakeMesh(root, name + "_Shell", shell, interior ? PlaceMats.Shell : PlaceMats.Ext, interior ? 0 : 0, !interior);
            MakeMesh(root, name + "_Glow", glow, interior ? PlaceMats.Glow : PlaceMats.ExtGlow, 0, false);
            MakeMesh(root, name + "_Glass", glass, PlaceMats.Glass, 0, false);
            if (cols.Count > 0)
            {
                var cr = new GameObject(name + "_Colliders").transform;
                cr.SetParent(root, false);
                for (int i = 0; i < cols.Count; i++)
                {
                    var b = cols[i];
                    float pitch;
                    rampPitch.TryGetValue(i, out pitch);
                    var go = new GameObject("Col");
                    go.transform.SetParent(cr, false);
                    go.transform.localPosition = b.c;
                    go.transform.localRotation = Quaternion.Euler(pitch, b.yaw, 0f);
                    go.AddComponent<BoxCollider>().size = b.size;
                }
            }
        }

        static void MakeMesh(Transform root, string name, LifeGeo g, Material mat, int layer, bool shadows)
        {
            if (g.v.Count == 0) return;
            var mesh = g.ToMesh(name);
            mesh.UploadMeshData(true);
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = shadows;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    /// <summary>Shared materials of the Places module.</summary>
    internal static class PlaceMats
    {
        static Material shell, glow, glass, extGlow;

        /// <summary>Interior shell: emissive vertex colour (baked light) - brighter on Low where no realtime lights exist.</summary>
        public static Material Shell
        {
            get
            {
                if (shell == null)
                {
                    shell = new Material(MaterialLibrary.FindShader("BlossomRally/VertexColorGlow")) { name = "BR_PlaceShell" };
                    ApplyQuality();
                }
                return shell;
            }
        }

        public static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    glow = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_PlaceGlow" };
                    glow.SetFloat("_Glow", 1.9f);
                    glow.SetFloat("_WindStrength", 0f);
                }
                return glow;
            }
        }

        public static Material Glass
        {
            get
            {
                if (glass == null)
                {
                    glass = new Material(MaterialLibrary.Glass) { name = "BR_PlaceGlass" };
                    glass.SetColor("_Color", new Color(0.30f, 0.42f, 0.46f, 0.32f));
                    glass.SetFloat("_Glossiness", 0.96f);
                }
                return glass;
            }
        }

        public static Material Ext { get { return MaterialLibrary.VertexColorWind; } }

        /// <summary>Exterior lanterns / signs: glow follows the night (PlacesManager updates it).</summary>
        public static Material ExtGlow
        {
            get
            {
                if (extGlow == null)
                {
                    extGlow = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_PlaceExtGlow" };
                    extGlow.SetFloat("_WindStrength", 0.12f);
                    UpdateNight();
                }
                return extGlow;
            }
        }

        public static void UpdateNight()
        {
            if (extGlow != null) extGlow.SetFloat("_Glow", 0.55f + 2.4f * Atmosphere.NightFactor + 0.25f * Atmosphere.CloudCover);
        }

        public static void ApplyQuality()
        {
            if (shell != null) shell.SetFloat("_Glow", GameSettings.Quality == 0 ? 0.86f : 0.66f);
        }
    }

    internal static class PlaceKit
    {
        static readonly Vector3 Up = Vector3.up;
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }

        // ================================================================== bake
        /// <summary>Multiply vertex colours by ambient + warm light pools (+ soft contact darkening near the floor).</summary>
        public static void Bake(LifeGeo g, List<BakeLight> lights, Color ambient, float floorY)
        {
            for (int i = 0; i < g.v.Count; i++)
            {
                Vector3 p = g.v[i], n = g.n[i];
                float r = ambient.r, gg = ambient.g, b = ambient.b;
                for (int k = 0; k < lights.Count; k++)
                {
                    var L = lights[k];
                    Vector3 d = L.pos - p;
                    float dist = d.magnitude;
                    if (dist >= L.range) continue;
                    float att = 1f - dist / L.range;
                    att *= att;
                    float ndl = dist > 1e-3f ? Vector3.Dot(n, d / dist) : 1f;
                    ndl = Mathf.Clamp01(ndl) * 0.7f + 0.3f;
                    float e = L.intensity * att * ndl;
                    r += L.col.r * e; gg += L.col.g * e; b += L.col.b * e;
                }
                float h = p.y - floorY;
                float ao = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(h / 0.55f));
                Color32 c = g.c[i];
                g.c[i] = new Color32((byte)Mathf.Clamp(c.r * r * ao, 0f, 255f), (byte)Mathf.Clamp(c.g * gg * ao, 0f, 255f), (byte)Mathf.Clamp(c.b * b * ao, 0f, 255f), 0);
            }
        }

        // ================================================================== grids
        /// <summary>nu x nv quads spanning origin + u * i + v * j, facing 'normal', coloured per cell.</summary>
        public static void Grid(LifeGeo g, Vector3 origin, Vector3 u, Vector3 v, int nu, int nv, Vector3 normal, System.Func<int, int, Color32> col)
        {
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    Vector3 a = origin + u * i + v * j;
                    Color32 c = col(i, j);
                    g.QuadOut(a, a + u, a + u + v, a + v, normal, c, c, c, c);
                }
        }

        static float Hash(int a, int b, int s)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(s * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        public static Color32 Vary(Color32 c, float t, float amt) { return Sh(c, 1f - amt + 2f * amt * t); }

        // ================================================================== room shell
        public enum Floor { Planks, Tiles, Stone, Polished, Carpet, Concrete }

        public sealed class Style
        {
            public Floor floor = Floor.Planks;
            public Color32 floorA = C(176, 128, 82), floorB = C(150, 104, 66);
            public Color32 wallLow = C(110, 70, 46), wallHigh = C(236, 226, 206), trim = C(96, 60, 40);
            public Color32 post = C(150, 46, 38), ceiling = C(170, 128, 88), beam = C(104, 66, 44);
            public float wainscot = 0.95f;
            public bool posts = true, beams = true, door = true;
            public float postStep = 3.2f, beamStep = 2f;
        }

        /// <summary>
        /// Room box centred on x, floor at y = 0, from z = -D/2 (entrance wall) to +D/2. Inward-facing surfaces,
        /// colliders for floor, walls and ceiling. A decorative double door is drawn on the entrance wall.
        /// </summary>
        public static void Room(PGeo pg, float W, float D, float H, Style st, int seed)
        {
            var g = pg.shell;
            float hx = W * 0.5f, hz = D * 0.5f;
            // ---- floor
            switch (st.floor)
            {
                case Floor.Planks:
                    {
                        float pw = 0.32f; int n = Mathf.CeilToInt(W / pw);
                        float seg = 1.25f; int m = Mathf.CeilToInt(D / seg) + 1;
                        for (int i = 0; i < n; i++)
                        {
                            float x0 = -hx + i * pw, x1 = Mathf.Min(hx, x0 + pw - 0.012f);
                            float off = (i % 3) * 0.42f;
                            for (int j = 0; j < m; j++)
                            {
                                float z0 = -hz + j * seg - off, z1 = z0 + seg - 0.01f;
                                z0 = Mathf.Max(-hz, z0); z1 = Mathf.Min(hz, z1);
                                if (z1 <= z0) continue;
                                Color32 c = Vary((i + j) % 2 == 0 ? st.floorA : st.floorB, Hash(i, j, seed), 0.07f);
                                g.QuadOut(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), Up, c, c, c, c);
                            }
                        }
                        // dark gaps under the planks
                        g.QuadOut(new Vector3(-hx, -0.004f, -hz), new Vector3(hx, -0.004f, -hz), new Vector3(hx, -0.004f, hz), new Vector3(-hx, -0.004f, hz), Up, Sh(st.floorB, 0.5f), Sh(st.floorB, 0.5f), Sh(st.floorB, 0.5f), Sh(st.floorB, 0.5f));
                        break;
                    }
                default:
                    {
                        float ts = st.floor == Floor.Tiles ? 0.6f : st.floor == Floor.Polished ? 1.2f : st.floor == Floor.Stone ? 1.0f : st.floor == Floor.Carpet ? 1.0f : 1.5f;
                        int nx = Mathf.CeilToInt(W / ts), nz = Mathf.CeilToInt(D / ts);
                        float gap = st.floor == Floor.Carpet ? 0f : (st.floor == Floor.Polished ? 0.008f : 0.025f);
                        for (int i = 0; i < nx; i++)
                            for (int j = 0; j < nz; j++)
                            {
                                float x0 = -hx + i * ts + gap, x1 = Mathf.Min(hx, -hx + (i + 1) * ts) - gap;
                                float z0 = -hz + j * ts + gap, z1 = Mathf.Min(hz, -hz + (j + 1) * ts) - gap;
                                Color32 c = ((i + j) & 1) == 0 ? st.floorA : st.floorB;
                                c = Vary(c, Hash(i, j, seed), st.floor == Floor.Polished ? 0.02f : 0.06f);
                                g.QuadOut(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), Up, c, c, c, c);
                            }
                        if (gap > 0f)
                        {
                            Color32 jc = Sh(st.floorB, 0.62f);
                            g.QuadOut(new Vector3(-hx, -0.003f, -hz), new Vector3(hx, -0.003f, -hz), new Vector3(hx, -0.003f, hz), new Vector3(-hx, -0.003f, hz), Up, jc, jc, jc, jc);
                        }
                        break;
                    }
            }
            pg.Col(new Vector3(0f, -0.5f, 0f), new Vector3(W + 1f, 1f, D + 1f));
            // ---- walls (4 sides, inward)
            for (int side = 0; side < 4; side++)
            {
                // side 0: -Z (entrance), 1: +X, 2: +Z, 3: -X
                Vector3 inward, a0, along; float len;
                switch (side)
                {
                    case 0: inward = Vector3.forward; a0 = new Vector3(-hx, 0f, -hz); along = Vector3.right; len = W; break;
                    case 1: inward = Vector3.left; a0 = new Vector3(hx, 0f, -hz); along = Vector3.forward; len = D; break;
                    case 2: inward = Vector3.back; a0 = new Vector3(hx, 0f, hz); along = Vector3.left; len = W; break;
                    default: inward = Vector3.right; a0 = new Vector3(-hx, 0f, hz); along = Vector3.back; len = D; break;
                }
                int nu = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
                Vector3 u = along * (len / nu);
                float ws = st.wainscot;
                Grid(g, a0, u, Up * ws, nu, 1, inward, (i, j) => Vary(st.wallLow, Hash(i, side, seed + 3), 0.04f));
                int nv = Mathf.Max(1, Mathf.CeilToInt((H - ws) / 1.1f));
                Grid(g, a0 + Up * ws, u, Up * ((H - ws) / nv), nu, nv, inward, (i, j) => Vary(st.wallHigh, Hash(i, j + side * 7, seed + 5), 0.025f));
                // chair rail + skirting
                Vector3 mid = a0 + along * (len * 0.5f);
                PG.OBox(g, mid + Up * ws + inward * 0.03f, new Vector3(len * 0.5f, 0.04f, 0.03f), Quaternion.LookRotation(inward), st.trim);
                PG.OBox(g, mid + Up * 0.07f + inward * 0.02f, new Vector3(len * 0.5f, 0.07f, 0.02f), Quaternion.LookRotation(inward), Sh(st.trim, 0.8f));
                PG.OBox(g, mid + Up * (H - 0.08f) + inward * 0.04f, new Vector3(len * 0.5f, 0.08f, 0.04f), Quaternion.LookRotation(inward), st.trim);
                // collider (0.5 m thick, behind the surface)
                Vector3 cc = mid - inward * 0.25f + Up * (H * 0.5f);
                Vector3 size = side % 2 == 0 ? new Vector3(len + 1f, H + 1f, 0.5f) : new Vector3(0.5f, H + 1f, len + 1f);
                pg.Col(cc, size);
            }
            // ---- posts in the corners and along the long walls
            if (st.posts)
            {
                float pr = 0.13f;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        PG.Column(g, new Vector3(sx * (hx - pr), 0f, sz * (hz - pr)), new Vector3(sx * (hx - pr), H, sz * (hz - pr)), pr, st.post, 8);
                int np = Mathf.Max(1, Mathf.RoundToInt(D / st.postStep));
                for (int k = 1; k < np; k++)
                {
                    float z = -hz + k * D / np;
                    for (int sx = -1; sx <= 1; sx += 2)
                        PG.Column(g, new Vector3(sx * (hx - pr), 0f, z), new Vector3(sx * (hx - pr), H, z), pr, st.post, 8);
                }
            }
            // ---- ceiling boards + beams
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt(W / 1.4f)), nz = Mathf.Max(1, Mathf.CeilToInt(D / 1.4f));
                Grid(g, new Vector3(-hx, H, -hz), Vector3.right * (W / nx), Vector3.forward * (D / nz), nx, nz, Vector3.down,
                     (i, j) => Vary(st.ceiling, Hash(i, j, seed + 11), 0.05f));
                pg.Col(new Vector3(0f, H + 0.5f, 0f), new Vector3(W + 1f, 1f, D + 1f));
                if (st.beams)
                {
                    int nb = Mathf.Max(1, Mathf.RoundToInt(D / st.beamStep));
                    for (int k = 0; k <= nb; k++)
                    {
                        float z = Mathf.Lerp(-hz + 0.1f, hz - 0.1f, k / (float)nb);
                        PG.Box2(g, new Vector3(0f, H - 0.13f, z), new Vector3(hx, 0.13f, 0.1f), st.beam, st.beam);
                    }
                    PG.Box2(g, new Vector3(0f, H - 0.32f, 0f), new Vector3(0.12f, 0.08f, hz), Sh(st.beam, 0.85f), st.beam);
                }
            }
            // ---- entrance double door (decorative; the exit is an interactable in front of it)
            if (st.door) Door(pg, new Vector3(0f, 0f, -hz), Vector3.forward, 1.9f, 2.45f, st.trim, st.post);
        }

        /// <summary>Wooden double door with frame on a wall at 'basePt' (floor), facing 'inward'.</summary>
        public static void Door(PGeo pg, Vector3 basePt, Vector3 inward, float w, float h, Color32 frame, Color32 leaf)
        {
            var g = pg.shell;
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            Vector3 c = basePt + inward * 0.03f;
            for (int s = -1; s <= 1; s += 2)
                PG.OBox(g, c + right * (s * (w * 0.5f + 0.08f)) + Up * (h * 0.5f + 0.05f), new Vector3(0.09f, h * 0.5f + 0.05f, 0.06f), q, frame);
            PG.OBox(g, c + Up * (h + 0.14f), new Vector3(w * 0.5f + 0.2f, 0.1f, 0.07f), q, frame);
            Color32 lc = Sh(leaf, 0.9f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lcen = c + right * (s * w * 0.25f) + Up * (h * 0.5f);
                PG.OBox(g, lcen, new Vector3(w * 0.25f - 0.015f, h * 0.5f, 0.03f), q, lc);
                // panels + studs
                for (int k = 0; k < 2; k++)
                    PG.OBox(g, lcen + Up * (k == 0 ? -h * 0.22f : h * 0.18f) + inward * 0.03f, new Vector3(w * 0.17f, h * 0.17f, 0.012f), q, Sh(leaf, 0.75f));
                g.Ellipsoid(c + right * (s * 0.12f) + Up * (h * 0.48f) + inward * 0.07f, new Vector3(0.05f, 0.05f, 0.03f), 6, 3, Pal.Gold, Pal.GoldDark);
            }
        }

        // ================================================================== painted window views
        public enum View { Garden, Sea, Snow, Desert, City, Peaks, Lake }

        /// <summary>
        /// Window on a wall: painted, self-lit view (glow) inset in a lattice frame. c = centre on the wall
        /// surface, inward = wall normal into the room.
        /// </summary>
        public static void Window(PGeo pg, Vector3 c, Vector3 inward, float w, float h, View view, Color32 frame, bool lattice = true, bool round = false)
        {
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            Paint(pg.glow, c + inward * 0.012f, right, Up, w, h, view, round);
            var g = pg.shell;
            float fz = 0.05f;
            if (round)
            {
                int N = 20;
                float R = Mathf.Min(w, h) * 0.5f;
                for (int k = 0; k < N; k++)
                {
                    float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                    Vector3 p0 = c + right * (Mathf.Cos(a0) * (R + 0.06f)) + Up * (Mathf.Sin(a0) * (R + 0.06f)) + inward * fz;
                    Vector3 p1 = c + right * (Mathf.Cos(a1) * (R + 0.06f)) + Up * (Mathf.Sin(a1) * (R + 0.06f)) + inward * fz;
                    PG.Beam(g, p0, p1, 0.07f, 0.05f, frame);
                }
                return;
            }
            PG.OBox(g, c + Up * (h * 0.5f + 0.05f) + inward * fz, new Vector3(w * 0.5f + 0.1f, 0.06f, 0.06f), q, frame);
            PG.OBox(g, c - Up * (h * 0.5f + 0.05f) + inward * (fz + 0.05f), new Vector3(w * 0.5f + 0.14f, 0.05f, 0.11f), q, frame);
            for (int s = -1; s <= 1; s += 2)
                PG.OBox(g, c + right * (s * (w * 0.5f + 0.05f)) + inward * fz, new Vector3(0.06f, h * 0.5f + 0.1f, 0.06f), q, frame);
            if (!lattice) return;
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 0.45f)), ny = Mathf.Max(2, Mathf.RoundToInt(h / 0.45f));
            Color32 lt = Sh(frame, 0.85f);
            for (int i = 1; i < nx; i++)
                PG.OBox(g, c + right * (-w * 0.5f + w * i / nx) + inward * fz, new Vector3(0.018f, h * 0.5f, 0.018f), q, lt);
            for (int j = 1; j < ny; j++)
                PG.OBox(g, c + Up * (-h * 0.5f + h * j / ny) + inward * fz, new Vector3(w * 0.5f, 0.018f, 0.018f), q, lt);
        }

        /// <summary>Simple flat "painting" of a view in the glow bucket (rows of bands and silhouettes).</summary>
        public static void Paint(LifeGeo g, Vector3 c, Vector3 right, Vector3 up, float w, float h, View view, bool round)
        {
            Vector3 n = Vector3.Cross(right, up);    // towards the viewer
            float hw = w * 0.5f, hh = h * 0.5f;
            if (round) { hw = hh = Mathf.Min(hw, hh); }
            System.Func<float, float, Vector3> P = (u, v) => c + right * (u * hw) + up * (v * hh);
            Color32 skyT, skyB, far, mid, near;
            switch (view)
            {
                case View.Sea: skyT = C(120, 170, 230); skyB = C(255, 196, 150); far = C(70, 130, 170); mid = C(46, 110, 160); near = C(230, 214, 170); break;
                case View.Snow: skyT = C(130, 170, 225); skyB = C(220, 230, 245); far = C(200, 210, 230); mid = C(240, 244, 250); near = C(70, 110, 90); break;
                case View.Desert: skyT = C(110, 160, 230); skyB = C(250, 210, 160); far = C(214, 120, 80); mid = C(236, 186, 120); near = C(90, 150, 90); break;
                case View.City: skyT = C(40, 46, 96); skyB = C(150, 90, 140); far = C(70, 70, 110); mid = C(40, 44, 70); near = C(60, 64, 80); break;
                case View.Peaks: skyT = C(120, 170, 225); skyB = C(240, 236, 230); far = C(150, 170, 190); mid = C(250, 250, 252); near = C(60, 100, 80); break;
                case View.Lake: skyT = C(140, 190, 235); skyB = C(240, 230, 220); far = C(120, 160, 150); mid = C(110, 170, 190); near = C(96, 140, 80); break;
                default: skyT = C(150, 200, 240); skyB = C(246, 236, 226); far = C(140, 180, 160); mid = C(110, 160, 110); near = C(80, 130, 80); break;
            }
            // sky gradient (2 bands)
            if (round)
            {
                int N = 20;
                for (int k = 0; k < N; k++)
                {
                    float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                    Vector3 p0 = P(Mathf.Cos(a0), Mathf.Sin(a0)), p1 = P(Mathf.Cos(a1), Mathf.Sin(a1));
                    Color32 c0 = Color32.Lerp(skyB, skyT, Mathf.Sin(a0) * 0.5f + 0.5f), c1 = Color32.Lerp(skyB, skyT, Mathf.Sin(a1) * 0.5f + 0.5f);
                    g.TriOut(c, p0, p1, n, Color32.Lerp(skyB, skyT, 0.5f), c0, c1);
                }
            }
            else
            {
                Color32 mc = Color32.Lerp(skyB, skyT, 0.5f);
                g.QuadOut(P(-1f, 0f), P(1f, 0f), P(1f, 1f), P(-1f, 1f), n, mc, mc, skyT, skyT);
                g.QuadOut(P(-1f, -1f), P(1f, -1f), P(1f, 0f), P(-1f, 0f), n, skyB, skyB, mc, mc);
            }
            float lim = round ? 0.92f : 1f;
            Vector3 o1 = n * 0.004f, o2 = n * 0.008f, o3 = n * 0.012f;
            // far hills / peaks: a zigzag band
            int K = 9;
            for (int k = 0; k < K; k++)
            {
                float u0 = -lim + 2f * lim * k / K, u1 = -lim + 2f * lim * (k + 1) / K;
                float t0 = Mathf.Sin(k * 1.7f + (int)view) * 0.5f + 0.5f, t1 = Mathf.Sin((k + 1) * 1.7f + (int)view) * 0.5f + 0.5f;
                float base0 = view == View.Sea ? -0.05f : -0.25f;
                float top0 = base0 + (view == View.Peaks || view == View.Snow ? 0.55f : 0.25f) * (0.4f + 0.6f * t0);
                float top1 = base0 + (view == View.Peaks || view == View.Snow ? 0.55f : 0.25f) * (0.4f + 0.6f * t1);
                if (round) { float cl0 = Mathf.Sqrt(Mathf.Max(0f, 1f - u0 * u0)), cl1 = Mathf.Sqrt(Mathf.Max(0f, 1f - u1 * u1)); top0 = Mathf.Min(top0, cl0 * 0.9f); top1 = Mathf.Min(top1, cl1 * 0.9f); }
                g.QuadOut(P(u0, base0) + o1, P(u1, base0) + o1, P(u1, top1) + o1, P(u0, top0) + o1, n, far, far, Sh(far, 1.1f), Sh(far, 1.1f));
            }
            // middle band (water / snowfield / dunes / garden)
            float mb = -0.25f, mt = view == View.Sea ? -0.05f : -0.25f;
            if (view == View.Sea || view == View.Lake)
            {
                mt = view == View.Sea ? -0.05f : -0.3f;
                mb = -0.75f;
                Color32 sun = view == View.Sea ? C(255, 220, 170) : C(230, 240, 250);
                g.QuadOut(P(-lim, mb) + o2, P(lim, mb) + o2, P(lim, mt) + o2, P(-lim, mt) + o2, n, Sh(mid, 0.8f), Sh(mid, 0.8f), mid, mid);
                // sun glitter streak
                g.QuadOut(P(0.15f, mb + 0.2f) + o3, P(0.32f, mb + 0.2f) + o3, P(0.27f, mt) + o3, P(0.2f, mt) + o3, n, sun, sun, sun, sun);
                if (view == View.Sea)
                {
                    // sun disc + a junk sail
                    int N = 10;
                    Vector3 sc = P(0.24f, 0.12f) + o2;
                    for (int k = 0; k < N; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                        g.TriOut(sc, sc + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * hh * 0.12f, sc + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * hh * 0.12f, n, C(255, 236, 200), C(255, 214, 160), C(255, 214, 160));
                    }
                    Vector3 sb = P(-0.45f, -0.18f) + o3;
                    g.TriOut(sb, sb + up * hh * 0.32f, sb + right * hw * 0.16f, n, C(190, 70, 50), C(220, 110, 70), C(190, 70, 50));
                    g.QuadOut(sb - right * hw * 0.08f - up * hh * 0.05f, sb + right * hw * 0.2f - up * hh * 0.05f, sb + right * hw * 0.16f, sb - right * hw * 0.04f, n, C(80, 50, 40), C(80, 50, 40), C(80, 50, 40), C(80, 50, 40));
                }
            }
            else
            {
                mb = -1f;
                g.QuadOut(P(-lim, -0.75f) + o2, P(lim, -0.75f) + o2, P(lim, -0.22f) + o2, P(-lim, -0.22f) + o2, n, Sh(mid, 0.9f), Sh(mid, 0.9f), mid, mid);
            }
            // near foreground
            float nt = -0.62f;
            g.QuadOut(P(-lim, -1f) + o3, P(lim, -1f) + o3, P(lim, nt) + o3, P(-lim, nt) + o3, n, Sh(near, 0.85f), Sh(near, 0.85f), near, near);
            // silhouettes: bamboo / pines / poplars / towers
            Color32 sil = view == View.City ? C(30, 30, 50) : view == View.Desert ? C(70, 120, 70) : view == View.Snow ? C(50, 90, 70) : C(60, 110, 60);
            if (view == View.City)
            {
                for (int k = 0; k < 9; k++)
                {
                    float u = -0.9f + k * 0.22f, tw = 0.07f + 0.04f * Mathf.Abs(Mathf.Sin(k * 2.3f));
                    float top = -0.2f + 0.9f * Mathf.Abs(Mathf.Sin(k * 1.3f + 0.4f));
                    g.QuadOut(P(u - tw, -0.75f) + o3, P(u + tw, -0.75f) + o3, P(u + tw, top) + o3, P(u - tw, top) + o3, n, sil, sil, Sh(sil, 1.3f), Sh(sil, 1.3f));
                    for (float y = -0.6f; y < top - 0.08f; y += 0.12f)
                        for (int wx = 0; wx < 2; wx++)
                        {
                            if (Hash(k, Mathf.RoundToInt(y * 50f), wx) < 0.45f) continue;
                            float x0 = u - tw * 0.7f + wx * tw * 0.8f;
                            Color32 wc = Hash(k, wx, Mathf.RoundToInt(y * 10f)) < 0.3f ? C(255, 120, 190) : C(255, 220, 150);
                            g.QuadOut(P(x0, y) + o3 * 1.5f, P(x0 + tw * 0.5f, y) + o3 * 1.5f, P(x0 + tw * 0.5f, y + 0.05f) + o3 * 1.5f, P(x0, y + 0.05f) + o3 * 1.5f, n, wc, wc, wc, wc);
                        }
                }
                return;
            }
            int stalks = view == View.Garden ? 7 : 4;
            for (int k = 0; k < stalks; k++)
            {
                float u = Mathf.Lerp(-0.85f * lim, 0.85f * lim, (k + 0.5f) / stalks) + 0.08f * Mathf.Sin(k * 3.1f);
                if (view == View.Sea && Mathf.Abs(u) < 0.6f) continue;
                float top = 0.15f + 0.6f * Mathf.Abs(Mathf.Sin(k * 2.1f + 0.3f));
                if (round) top = Mathf.Min(top, Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * 0.85f);
                float sw = 0.025f;
                g.QuadOut(P(u - sw, -1f) + o3 * 2f, P(u + sw, -1f) + o3 * 2f, P(u + sw, top) + o3 * 2f, P(u - sw, top) + o3 * 2f, n, Sh(sil, 0.8f), Sh(sil, 0.8f), sil, sil);
                Color32 leaf = view == View.Garden && k % 3 == 1 ? C(250, 170, 196) : Sh(sil, 1.25f);
                Vector3 tp = P(u, top) + o3 * 2.5f;
                for (int l = 0; l < 3; l++)
                {
                    float a = l * 2.1f + k;
                    Vector3 d = (right * Mathf.Cos(a) * hw * 0.09f + up * (Mathf.Sin(a) * 0.5f + 0.3f) * hh * 0.12f);
                    g.TriOut(tp, tp + d + up * hh * 0.04f, tp + d - up * hh * 0.04f, n, leaf, leaf, Sh(leaf, 0.85f));
                }
            }
        }

        // ================================================================== props
        /// <summary>Paper lantern hanging from the ceiling at height ceilY; returns the lantern centre.</summary>
        public static Vector3 Lantern(PGeo pg, Vector3 p, float ceilY, float r, float h, Color32 col, bool round = true)
        {
            PG.Rod(pg.shell, new Vector3(p.x, ceilY, p.z), new Vector3(p.x, p.y, p.z), 0.012f, Pal.Ink, 3);
            PG.Lantern(pg.glow, p, r, h, col, 0, round);
            return p - Vector3.up * (0.07f + h * 0.5f);
        }

        /// <summary>Standing floor lantern (wooden frame, paper shade).</summary>
        public static void FloorLantern(PGeo pg, Vector3 p, float h, Color32 shade)
        {
            var g = pg.shell;
            PG.Box2(g, p + Up * 0.04f, new Vector3(0.2f, 0.04f, 0.2f), Pal.WoodDark, Pal.Wood);
            PG.Rod(g, p, p + Up * (h - 0.4f), 0.03f, Pal.WoodDark, 4);
            Vector3 c = p + Up * (h - 0.22f);
            pg.glow.Box(c, new Vector3(0.16f, 0.22f, 0.16f), shade);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.Rod(g, c + new Vector3(sx * 0.17f, -0.24f, sz * 0.17f), c + new Vector3(sx * 0.17f, 0.24f, sz * 0.17f), 0.014f, Pal.WoodDark, 4);
            PG.Box2(g, c + Up * 0.26f, new Vector3(0.2f, 0.025f, 0.2f), Pal.WoodDark, Pal.WoodDark);
        }

        /// <summary>Hanging scroll with a big ink glyph, on a wall (c = centre, inward = wall normal).</summary>
        public static void Scroll(PGeo pg, Vector3 c, Vector3 inward, float w, float h, char glyph, Color32 paper, Color32 ink)
        {
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            var g = pg.shell;
            PG.OBox(g, c + inward * 0.012f, new Vector3(w * 0.5f, h * 0.5f, 0.006f), q, paper);
            PG.OBox(g, c + inward * 0.01f, new Vector3(w * 0.5f + 0.06f, h * 0.5f + 0.12f, 0.004f), q, C(70, 96, 120));
            for (int s = -1; s <= 1; s += 2)
                PG.OBox(g, c + Up * (s * (h * 0.5f + 0.13f)) + inward * 0.03f, new Vector3(w * 0.5f + 0.12f, 0.025f, 0.025f), q, Pal.WoodDark);
            Glyph(g, c + inward * 0.022f, right, Up, Mathf.Min(w, h) * 0.75f, 0.035f * Mathf.Min(w, h) / 0.5f, glyph, ink);
        }

        /// <summary>Glowing sign panel: dark frame, coloured board and a big glowing glyph (exterior / interior).</summary>
        public static void SignPanel(PGeo pg, Vector3 c, Vector3 outward, float w, float h, char glyph, Color32 board, Color32 glyphCol, bool glowBoard = false)
        {
            Quaternion q = Quaternion.LookRotation(outward);
            Vector3 right = q * Vector3.right;
            PG.OBox(pg.shell, c, new Vector3(w * 0.5f + 0.08f, h * 0.5f + 0.08f, 0.06f), q, Pal.WoodDark);
            PG.OBox(glowBoard ? pg.glow : pg.shell, c + outward * 0.05f, new Vector3(w * 0.5f, h * 0.5f, 0.02f), q, board);
            Glyph(pg.glow, c + outward * 0.075f, right, Up, Mathf.Min(w, h) * 0.78f, 0.045f * Mathf.Min(w, h) / 0.6f, glyph, glyphCol);
            // gold corners
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    PG.OBox(pg.shell, c + right * (sx * w * 0.5f) + Up * (sy * h * 0.5f) + outward * 0.07f, new Vector3(0.05f, 0.05f, 0.02f), q, Pal.Gold);
        }

        /// <summary>Shelves with jars / tins / books against a wall. p = floor centre at the wall, inward = into the room.</summary>
        public static void Shelf(PGeo pg, Vector3 p, Vector3 inward, float w, float h, float d, int rows, System.Random rng, int content)
        {
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            var g = pg.shell;
            Vector3 c = p + inward * (d * 0.5f + 0.02f);
            Color32 wood = Pal.WoodDark;
            PG.OBox(g, c + Up * (h * 0.5f) - inward * (d * 0.5f - 0.02f), new Vector3(w * 0.5f, h * 0.5f, 0.02f), q, Sh(wood, 0.8f));
            for (int s = -1; s <= 1; s += 2) PG.OBox(g, c + right * (s * w * 0.5f) + Up * (h * 0.5f), new Vector3(0.03f, h * 0.5f, d * 0.5f), q, wood);
            for (int r = 0; r <= rows; r++)
            {
                float y = 0.08f + (h - 0.12f) * r / rows;
                PG.OBox(g, c + Up * y, new Vector3(w * 0.5f, 0.02f, d * 0.5f), q, wood);
                if (r == rows) break;
                float x = -w * 0.5f + 0.12f;
                while (x < w * 0.5f - 0.12f)
                {
                    float sz = 0.07f + (float)rng.NextDouble() * 0.06f;
                    Vector3 b = c + right * x + Up * (y + 0.02f);
                    switch (content)
                    {
                        case 0: // tea jars and tins
                            {
                                Color32 jc = rng.NextDouble() < 0.5 ? Pal.Porcelain : (rng.NextDouble() < 0.5 ? C(160, 60, 50) : C(70, 120, 90));
                                float jh = 0.16f + (float)rng.NextDouble() * 0.12f;
                                g.Frustum(b, b + Up * jh, sz, sz, sz * 0.8f, sz * 0.8f, 7, 0f, jc, Sh(jc, 1.05f), false, true);
                                if (jc.r > 200) g.Frustum(b + Up * jh * 0.4f, b + Up * jh * 0.55f, sz * 1.01f, sz * 1.01f, sz * 0.98f, sz * 0.98f, 7, 0f, Pal.PorcelainBlue, Pal.PorcelainBlue, false, false);
                                g.Frustum(b + Up * jh, b + Up * (jh + 0.04f), sz * 0.5f, sz * 0.5f, sz * 0.4f, sz * 0.4f, 6, 0f, Pal.WoodDark, Pal.Wood, false, true);
                                break;
                            }
                        case 1: // books / boxes
                            {
                                float bh = 0.18f + (float)rng.NextDouble() * 0.12f;
                                Color32 bc = PropShapes.Silks[rng.Next(PropShapes.Silks.Length)];
                                PG.OBox(g, b + Up * (bh * 0.5f), new Vector3(sz * 0.6f, bh * 0.5f, d * 0.35f), q, Sh(bc, 0.8f));
                                break;
                            }
                        case 2: // bottles / mugs
                            {
                                Color32 bc = rng.NextDouble() < 0.5 ? C(120, 70, 40) : C(220, 210, 190);
                                g.Frustum(b, b + Up * 0.14f, sz * 0.6f, sz * 0.6f, sz * 0.6f, sz * 0.6f, 6, 0f, bc, bc, false, true);
                                break;
                            }
                        default: // helmets / parts
                            {
                                Color32 hc = PropShapes.Silks[rng.Next(PropShapes.Silks.Length)];
                                g.Ellipsoid(b + Up * 0.11f, new Vector3(0.13f, 0.11f, 0.14f), 7, 4, hc, Sh(hc, 0.7f));
                                sz = 0.12f;
                                break;
                            }
                    }
                    x += sz * 2f + 0.05f + (float)rng.NextDouble() * 0.06f;
                }
            }
            pg.Col(c + Up * (h * 0.5f), new Vector3(w, h, d), Quaternion.LookRotation(inward).eulerAngles.y);
        }

        /// <summary>Counter with a top slab and panelled front. p = floor centre, faceDir = where customers stand.</summary>
        public static void Counter(PGeo pg, Vector3 p, Vector3 faceDir, float w, float h, float d, Color32 front, Color32 top)
        {
            Quaternion q = Quaternion.LookRotation(faceDir);
            Vector3 right = q * Vector3.right;
            var g = pg.shell;
            PG.OBox(g, p + Up * (h * 0.5f - 0.03f), new Vector3(w * 0.5f, h * 0.5f - 0.03f, d * 0.5f), q, front);
            PG.OBox(g, p + Up * (h - 0.03f) + faceDir * 0.04f, new Vector3(w * 0.5f + 0.06f, 0.035f, d * 0.5f + 0.06f), q, top);
            int np = Mathf.Max(1, Mathf.RoundToInt(w / 0.8f));
            for (int i = 0; i < np; i++)
            {
                float x = -w * 0.5f + (i + 0.5f) * w / np;
                PG.OBox(g, p + right * x + Up * (h * 0.5f) + faceDir * (d * 0.5f + 0.01f), new Vector3(w / np * 0.5f - 0.06f, h * 0.32f, 0.012f), q, Sh(front, 0.82f));
            }
            PG.OBox(g, p + Up * 0.05f + faceDir * (d * 0.5f), new Vector3(w * 0.5f, 0.05f, 0.03f), q, Sh(front, 0.6f));
            pg.Col(p + Up * (h * 0.5f), new Vector3(w + 0.1f, h, d + 0.1f), q.eulerAngles.y);
        }

        /// <summary>Potted plant (bonsai-ish lumpy crown). p = floor point.</summary>
        public static void Plant(PGeo pg, Vector3 p, float scale, System.Random rng, bool blossom = false)
        {
            var g = pg.shell;
            Color32 pot = rng.NextDouble() < 0.5 ? Pal.Porcelain : C(150, 80, 60);
            g.Frustum(p, p + Up * 0.38f * scale, 0.2f * scale, 0.2f * scale, 0.26f * scale, 0.26f * scale, 8, 0f, Sh(pot, 0.9f), pot, false, true);
            if (pot.r > 200) g.Frustum(p + Up * 0.18f * scale, p + Up * 0.24f * scale, 0.235f * scale, 0.235f * scale, 0.245f * scale, 0.245f * scale, 8, 0f, Pal.PorcelainBlue, Pal.PorcelainBlue, false, false);
            PG.Rod(g, p + Up * 0.36f * scale, p + new Vector3(0.05f, 0.85f, 0.02f) * scale, 0.035f * scale, Pal.WoodDark, 5);
            for (int k = 0; k < 3; k++)
            {
                Vector3 cc = p + new Vector3(Mathf.Cos(k * 2.1f) * 0.18f, 0.85f + 0.12f * k, Mathf.Sin(k * 2.1f) * 0.18f) * scale;
                int s = g.v.Count;
                Color32 top = blossom ? C(246, 170, 200) : C(96, 150, 80), bot = blossom ? C(220, 130, 160) : C(66, 110, 60);
                g.Ellipsoid(cc, new Vector3(0.26f, 0.18f, 0.26f) * scale, 7, 4, top, bot);
                PG.Lumpy(g, s, cc, 0.18f, k * 1.7f + scale);
            }
            pg.Col(p + Up * 0.5f * scale, new Vector3(0.5f, 1f, 0.5f) * scale);
        }

        /// <summary>Rug on the floor with a border and a simple medallion pattern.</summary>
        public static void Rug(PGeo pg, Vector3 c, float w, float d, Color32 field, Color32 border, Color32 accent)
        {
            var g = pg.shell;
            float y = c.y + 0.012f;
            g.QuadOut(new Vector3(c.x - w * 0.5f, y, c.z - d * 0.5f), new Vector3(c.x + w * 0.5f, y, c.z - d * 0.5f), new Vector3(c.x + w * 0.5f, y, c.z + d * 0.5f), new Vector3(c.x - w * 0.5f, y, c.z + d * 0.5f), Up, border, border, border, border);
            float b = Mathf.Min(w, d) * 0.12f;
            y += 0.003f;
            g.QuadOut(new Vector3(c.x - w * 0.5f + b, y, c.z - d * 0.5f + b), new Vector3(c.x + w * 0.5f - b, y, c.z - d * 0.5f + b), new Vector3(c.x + w * 0.5f - b, y, c.z + d * 0.5f - b), new Vector3(c.x - w * 0.5f + b, y, c.z + d * 0.5f - b), Up, field, field, field, field);
            y += 0.003f;
            float r = Mathf.Min(w, d) * 0.25f;
            g.QuadOut(new Vector3(c.x, y, c.z - r), new Vector3(c.x + r, y, c.z), new Vector3(c.x, y, c.z + r), new Vector3(c.x - r, y, c.z), Up, accent, accent, accent, accent);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 cc = new Vector3(c.x + sx * (w * 0.5f - b * 1.9f), y, c.z + sz * (d * 0.5f - b * 1.9f));
                    float rr = b * 0.5f;
                    g.QuadOut(cc + new Vector3(0f, 0f, -rr), cc + new Vector3(rr, 0f, 0f), cc + new Vector3(0f, 0f, rr), cc + new Vector3(-rr, 0f, 0f), Up, accent, accent, accent, accent);
                }
        }

        public static void Cushion(PGeo pg, Vector3 p, float r, Color32 col)
        {
            pg.shell.Frustum(p, p + Up * 0.09f, r, r, r * 0.92f, r * 0.92f, 10, 0f, Sh(col, 0.8f), col, false, true);
            pg.shell.Ellipsoid(p + Up * 0.1f, new Vector3(r * 0.25f, 0.02f, r * 0.25f), 6, 2, Pal.Gold, Pal.GoldDark);
        }

        /// <summary>Low square table (oasis / temple offering).</summary>
        public static void LowTable(PGeo pg, Vector3 p, float w, float d, float h, Color32 top)
        {
            var g = pg.shell;
            PG.Box2(g, p + Up * (h - 0.03f), new Vector3(w * 0.5f, 0.03f, d * 0.5f), Sh(top, 0.85f), top);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.Box2(g, p + new Vector3(sx * (w * 0.5f - 0.06f), (h - 0.06f) * 0.5f, sz * (d * 0.5f - 0.06f)), new Vector3(0.04f, (h - 0.06f) * 0.5f, 0.04f), Sh(top, 0.6f), Sh(top, 0.6f));
            pg.Col(p + Up * (h * 0.5f), new Vector3(w, h, d));
        }

        /// <summary>Wooden armchair facing 'yaw' (seat height 0.45).</summary>
        public static void Chair(PGeo pg, Vector3 p, float yaw, Color32 wood, Color32 seat)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var g = pg.shell;
            PG.OBox(g, p + q * new Vector3(0f, 0.44f, 0f), new Vector3(0.26f, 0.035f, 0.25f), q, seat);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.OBox(g, p + q * new Vector3(sx * 0.22f, 0.21f, sz * 0.21f), new Vector3(0.025f, 0.21f, 0.025f), q, wood);
            PG.OBox(g, p + q * new Vector3(0f, 0.78f, -0.23f), new Vector3(0.24f, 0.3f, 0.02f), q, wood);
            PG.OBox(g, p + q * new Vector3(0f, 1.06f, -0.23f), new Vector3(0.3f, 0.03f, 0.035f), q, Sh(wood, 0.8f));
        }

        /// <summary>Sofa / bench with cushions facing 'yaw'.</summary>
        public static void Sofa(PGeo pg, Vector3 p, float yaw, float w, Color32 frame, Color32 cushion)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var g = pg.shell;
            PG.OBox(g, p + q * new Vector3(0f, 0.22f, 0f), new Vector3(w * 0.5f, 0.22f, 0.42f), q, frame);
            PG.OBox(g, p + q * new Vector3(0f, 0.5f, 0.04f), new Vector3(w * 0.5f - 0.15f, 0.07f, 0.36f), q, cushion);
            PG.OBox(g, p + q * new Vector3(0f, 0.72f, -0.36f), new Vector3(w * 0.5f, 0.32f, 0.08f), q, frame);
            for (int s = -1; s <= 1; s += 2) PG.OBox(g, p + q * new Vector3(s * (w * 0.5f - 0.07f), 0.55f, 0f), new Vector3(0.08f, 0.14f, 0.42f), q, frame);
            pg.Col(p + q * new Vector3(0f, 0.4f, -0.05f), new Vector3(w, 0.8f, 0.85f), yaw);
        }

        /// <summary>Job board on a wall: wooden frame, red header with the 工 glyph, pinned paper notes.</summary>
        public static void JobBoard(PGeo pg, Vector3 c, Vector3 inward, System.Random rng)
        {
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            var g = pg.shell;
            PG.OBox(g, c + inward * 0.03f, new Vector3(0.75f, 0.55f, 0.03f), q, C(186, 150, 104));
            PG.OBox(g, c + inward * 0.02f, new Vector3(0.82f, 0.62f, 0.02f), q, Pal.WoodDark);
            PG.OBox(g, c + Up * 0.72f + inward * 0.04f, new Vector3(0.5f, 0.14f, 0.03f), q, Pal.Red);
            Glyph(pg.glow, c + Up * 0.72f + inward * 0.075f, right, Up, 0.22f, 0.02f, '工', Pal.Gold);
            for (int k = 0; k < 6; k++)
            {
                float x = -0.5f + (k % 3) * 0.5f + (float)(rng.NextDouble() - 0.5) * 0.1f, y = (k < 3 ? 0.2f : -0.25f) + (float)(rng.NextDouble() - 0.5) * 0.08f;
                Quaternion tilt = q * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 10f);
                PG.OBox(g, c + right * x + Up * y + inward * 0.065f, new Vector3(0.17f, 0.2f, 0.005f), tilt, k % 4 == 1 ? C(250, 226, 170) : C(246, 242, 232));
                for (int l = 0; l < 3; l++) PG.OBox(g, c + right * x + Up * (y + 0.1f - l * 0.07f) + inward * 0.071f, new Vector3(0.11f, 0.008f, 0.002f), tilt, C(90, 80, 80));
                g.Ellipsoid(c + right * x + Up * (y + 0.18f) + inward * 0.075f, new Vector3(0.02f, 0.02f, 0.012f), 5, 2, Pal.Red, Pal.RedDeep);
            }
        }

        /// <summary>Tea set on a counter / table top: tray, pot and cups (top = surface point).</summary>
        public static Vector3 TeaSet(PGeo pg, Vector3 top, float yaw, System.Random rng)
        {
            var g = pg.shell;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            PG.OBox(g, top + Up * 0.012f, new Vector3(0.24f, 0.012f, 0.16f), q, Pal.WoodDark);
            Color32 clay = rng.NextDouble() < 0.5 ? C(128, 76, 62) : C(150, 196, 176);
            Vector3 pot = top + Up * 0.025f + q * new Vector3(0.08f, 0f, 0f);
            g.Ellipsoid(pot + Up * 0.07f, new Vector3(0.09f, 0.07f, 0.09f), 7, 4, clay, Sh(clay, 0.8f));
            g.Frustum(pot + new Vector3(0f, 0.13f, 0f), pot + new Vector3(0f, 0.16f, 0f), 0.03f, 0.03f, 0.02f, 0.02f, 5, 0f, clay, clay, false, true);
            g.Frustum(pot + q * new Vector3(0.07f, 0.05f, 0f), pot + q * new Vector3(0.16f, 0.13f, 0f), 0.02f, 0.02f, 0.01f, 0.01f, 4, 0f, clay, clay, false, true);
            for (int i = 0; i < 3; i++)
            {
                Vector3 cp = top + Up * 0.025f + q * new Vector3(-0.12f + i * 0.0f, 0f, -0.08f + i * 0.08f);
                g.Frustum(cp, cp + Up * 0.045f, 0.026f, 0.026f, 0.036f, 0.036f, 6, 0f, Pal.Porcelain, Pal.Porcelain, true, false);
                g.Disc(cp + Up * 0.04f, 0.03f, 6, 0f, Vector3.up, C(170, 140, 60), C(170, 140, 60));
            }
            return pot + Up * 0.2f;
        }

        /// <summary>Wall-mounted candle rack (rows of small glowing candles). p = floor centre at the wall.</summary>
        public static void CandleWall(PGeo pg, Vector3 p, Vector3 inward, float w, int rows, System.Random rng)
        {
            Quaternion q = Quaternion.LookRotation(inward);
            Vector3 right = q * Vector3.right;
            var g = pg.shell;
            for (int r = 0; r < rows; r++)
            {
                float y = 0.75f + r * 0.36f;
                Vector3 c = p + inward * (0.18f + r * -0.03f) + Up * y;
                PG.OBox(g, c, new Vector3(w * 0.5f, 0.025f, 0.16f), q, Pal.WoodDark);
                int n = Mathf.RoundToInt(w / 0.13f);
                for (int i = 0; i < n; i++)
                {
                    if (rng.NextDouble() < 0.12) continue;
                    Vector3 cb = c + right * (-w * 0.5f + (i + 0.5f) * w / n) + inward * ((float)rng.NextDouble() * 0.12f - 0.06f) + Up * 0.025f;
                    float ch = 0.06f + (float)rng.NextDouble() * 0.08f;
                    g.Frustum(cb, cb + Up * ch, 0.022f, 0.022f, 0.022f, 0.022f, 5, 0f, C(240, 226, 196), C(250, 240, 220), false, true);
                    pg.glow.Ellipsoid(cb + Up * (ch + 0.03f), new Vector3(0.012f, 0.03f, 0.012f), 4, 3, C(255, 220, 120), C(255, 150, 60));
                }
            }
            PG.OBox(g, p + inward * 0.05f + Up * (0.75f + rows * 0.18f), new Vector3(w * 0.5f + 0.1f, rows * 0.2f + 0.1f, 0.03f), q, Sh(Pal.WoodDark, 0.7f));
            pg.Col(p + inward * 0.2f + Up * 1f, new Vector3(w + 0.2f, 2f, 0.45f), q.eulerAngles.y);
        }

        // ================================================================== glyphs
        // Simplified stroke sets (unit square, y up) for the hanzi on signs and scrolls.
        static readonly Dictionary<char, float[]> Glyphs = new Dictionary<char, float[]>
        {
            { '茶', new[] { 0.1f,0.86f,0.9f,0.86f, 0.33f,0.97f,0.33f,0.76f, 0.67f,0.97f,0.67f,0.76f, 0.5f,0.72f,0.1f,0.48f, 0.5f,0.72f,0.9f,0.48f,
                            0.3f,0.44f,0.7f,0.44f, 0.5f,0.56f,0.5f,0.02f, 0.4f,0.3f,0.22f,0.12f, 0.6f,0.3f,0.78f,0.12f } },
            { '寺', new[] { 0.25f,0.86f,0.75f,0.86f, 0.5f,0.98f,0.5f,0.62f, 0.1f,0.62f,0.9f,0.62f, 0.1f,0.42f,0.9f,0.42f, 0.68f,0.55f,0.68f,0.06f,
                            0.68f,0.06f,0.55f,0.14f, 0.3f,0.3f,0.42f,0.18f } },
            { '车', new[] { 0.15f,0.82f,0.85f,0.82f, 0.48f,0.96f,0.26f,0.52f, 0.26f,0.52f,0.82f,0.52f, 0.08f,0.25f,0.92f,0.25f, 0.56f,0.68f,0.56f,0.0f } },
            { '鱼', new[] { 0.42f,0.98f,0.22f,0.74f, 0.4f,0.9f,0.66f,0.9f, 0.66f,0.9f,0.5f,0.74f, 0.2f,0.72f,0.8f,0.72f, 0.2f,0.72f,0.2f,0.3f,
                            0.8f,0.72f,0.8f,0.3f, 0.2f,0.3f,0.8f,0.3f, 0.5f,0.72f,0.5f,0.3f, 0.2f,0.51f,0.8f,0.51f, 0.06f,0.1f,0.94f,0.1f } },
            { '雪', new[] { 0.15f,0.96f,0.85f,0.96f, 0.08f,0.84f,0.92f,0.84f, 0.08f,0.84f,0.08f,0.64f, 0.92f,0.84f,0.92f,0.64f, 0.5f,0.96f,0.5f,0.6f,
                            0.22f,0.76f,0.38f,0.72f, 0.62f,0.76f,0.78f,0.72f, 0.22f,0.66f,0.38f,0.62f, 0.62f,0.66f,0.78f,0.62f,
                            0.2f,0.44f,0.8f,0.44f, 0.26f,0.27f,0.8f,0.27f, 0.14f,0.08f,0.8f,0.08f, 0.8f,0.44f,0.8f,0.08f } },
            { '工', new[] { 0.15f,0.85f,0.85f,0.85f, 0.5f,0.85f,0.5f,0.15f, 0.05f,0.15f,0.95f,0.15f } },
            { '福', new[] { 0.2f,0.95f,0.25f,0.85f, 0.08f,0.78f,0.36f,0.78f, 0.36f,0.78f,0.12f,0.48f, 0.24f,0.6f,0.24f,0.02f, 0.28f,0.5f,0.38f,0.4f,
                            0.48f,0.92f,0.92f,0.92f, 0.52f,0.8f,0.88f,0.8f, 0.52f,0.8f,0.52f,0.64f, 0.88f,0.8f,0.88f,0.64f, 0.52f,0.64f,0.88f,0.64f,
                            0.48f,0.52f,0.92f,0.52f, 0.48f,0.52f,0.48f,0.04f, 0.92f,0.52f,0.92f,0.04f, 0.48f,0.28f,0.92f,0.28f, 0.7f,0.52f,0.7f,0.04f, 0.48f,0.04f,0.92f,0.04f } },
            { '禅', new[] { 0.2f,0.95f,0.25f,0.85f, 0.08f,0.78f,0.36f,0.78f, 0.36f,0.78f,0.12f,0.48f, 0.24f,0.6f,0.24f,0.02f, 0.28f,0.5f,0.38f,0.4f,
                            0.55f,0.96f,0.6f,0.86f, 0.8f,0.96f,0.75f,0.86f, 0.5f,0.82f,0.9f,0.82f, 0.5f,0.82f,0.5f,0.52f, 0.9f,0.82f,0.9f,0.52f,
                            0.5f,0.67f,0.9f,0.67f, 0.5f,0.52f,0.9f,0.52f, 0.44f,0.34f,0.96f,0.34f, 0.7f,0.82f,0.7f,0.0f } },
            { '暖', new[] { 0.06f,0.8f,0.3f,0.8f, 0.06f,0.8f,0.06f,0.2f, 0.3f,0.8f,0.3f,0.2f, 0.06f,0.5f,0.3f,0.5f, 0.06f,0.2f,0.3f,0.2f,
                            0.9f,0.95f,0.45f,0.86f, 0.42f,0.72f,0.94f,0.72f, 0.48f,0.56f,0.9f,0.56f, 0.62f,0.72f,0.5f,0.02f, 0.5f,0.38f,0.9f,0.38f, 0.66f,0.38f,0.94f,0.02f } },
            { '饭', new[] { 0.22f,0.96f,0.08f,0.7f, 0.2f,0.82f,0.36f,0.82f, 0.18f,0.66f,0.18f,0.08f, 0.18f,0.08f,0.34f,0.2f,
                            0.5f,0.9f,0.92f,0.9f, 0.5f,0.9f,0.44f,0.08f, 0.52f,0.62f,0.88f,0.62f, 0.88f,0.62f,0.56f,0.06f, 0.6f,0.5f,0.94f,0.04f } },
            { 'P', new[] { 0.3f,0.05f,0.3f,0.95f, 0.3f,0.95f,0.62f,0.95f, 0.62f,0.95f,0.76f,0.84f, 0.76f,0.84f,0.76f,0.62f, 0.76f,0.62f,0.62f,0.5f, 0.62f,0.5f,0.3f,0.5f } },
        };

        /// <summary>Glyph strokes as thin beams centred at c in the plane (right, up), size = square side.</summary>
        public static void Glyph(LifeGeo g, Vector3 c, Vector3 right, Vector3 up, float size, float thick, char ch, Color32 col)
        {
            float[] s;
            if (!Glyphs.TryGetValue(ch, out s)) return;
            Vector3 n = Vector3.Cross(right, up);
            for (int i = 0; i + 3 < s.Length; i += 4)
            {
                Vector3 a = c + right * ((s[i] - 0.5f) * size) + up * ((s[i + 1] - 0.5f) * size);
                Vector3 b = c + right * ((s[i + 2] - 0.5f) * size) + up * ((s[i + 3] - 0.5f) * size);
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 1e-4f) continue;
                Vector3 dir = d / len, side = Vector3.Cross(n, dir).normalized * thick;
                Vector3 ext = dir * thick * 0.6f;
                Vector3 a0 = a - ext - side, a1 = a - ext + side, b0 = b + ext - side, b1 = b + ext + side;
                g.QuadOut(a0, b0, b1, a1, -n, col, col, col, col);
                g.QuadOut(a0, b0, b1, a1, n, col, col, col, col);
            }
        }
    }
}

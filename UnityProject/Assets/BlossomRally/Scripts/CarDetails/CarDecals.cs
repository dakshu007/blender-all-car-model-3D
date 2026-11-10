// CarDecals.cs - rally livery details generated in code (all names / logos fictional):
//  * a 256x256 sticker atlas per car (cached for the session): rally door-number panel, "BV 009" style number
//    plate, "LOTUS TEA", "JADE OIL", "PANDA TYRES" stickers, a "BLOSSOM RALLY" windscreen banner and the
//    speedo / rev-counter dial faces, drawn with a 5x7 pixel font
//  * stickers are PROJECTED onto the body's paint triangles (the decal mesh follows the curved panels, is lit
//    with the body normals, gets muddy like the paint and crumples with CarDamage); plates, the windscreen
//    banner and the dial faces are flat quads. Everything is one mesh / one material = one draw call.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>Builds BR_Decal meshes: uv0 = position inside the decal, uv1 = atlas rect, colour.a = glow.</summary>
    internal sealed class DecalMeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector4> uv0 = new List<Vector4>();
        public readonly List<Vector4> uv1 = new List<Vector4>();
        public readonly List<Color32> c = new List<Color32>();
        public readonly List<int> t = new List<int>();

        static void Frame(Vector3 viewDir, Vector3 upHint, out Vector3 right, out Vector3 up)
        {
            right = Vector3.Cross(upHint, viewDir).normalized;
            up = Vector3.Cross(viewDir, right).normalized;
        }

        /// <summary>Flat quad facing 'normal' (the viewer looks along -normal).</summary>
        public void Quad(Vector3 centre, Vector3 normal, Vector3 upHint, float w, float h, Vector4 rect, Color32 col)
        {
            Vector3 right, up;
            Frame(-normal, upHint, out right, out up);
            int i0 = v.Count;
            Vector3[] p = { centre - right * w * 0.5f - up * h * 0.5f, centre + right * w * 0.5f - up * h * 0.5f,
                            centre + right * w * 0.5f + up * h * 0.5f, centre - right * w * 0.5f + up * h * 0.5f };
            Vector2[] q = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int k = 0; k < 4; k++) { v.Add(p[k]); n.Add(normal); uv0.Add(new Vector4(q[k].x, q[k].y, 0f, 0f)); uv1.Add(rect); c.Add(col); }
            bool front = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) > 0f;
            if (front) { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3); }
            else { t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2); }
        }

        /// <summary>Project a sticker along viewDir onto the body's paint triangles around 'centre'.</summary>
        public int Projected(CarAnatomy a, Vector3 centre, Vector3 viewDir, Vector3 upHint, float w, float h, float depth, Vector4 rect, Color32 col)
        {
            if (a.bodyV == null || a.bodyPaintTris == null) return 0;
            Vector3 right, up;
            viewDir.Normalize();
            Frame(viewDir, upHint, out right, out up);
            var V = a.bodyV;
            var N = a.bodyN;
            var tris = a.bodyPaintTris;
            int added = 0;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                Vector3 nAvg = N[i0] + N[i1] + N[i2];
                if (Vector3.Dot(nAvg, -viewDir) < 0.3f * nAvg.magnitude) continue;
                float minX = 9f, maxX = -9f, minY = 9f, maxY = -9f;
                bool ok = true;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 q = V[tris[i + k]] - centre;
                    float z = Vector3.Dot(q, viewDir);
                    if (Mathf.Abs(z) > depth) { ok = false; break; }
                    float x = Vector3.Dot(q, right) / w + 0.5f, y = Vector3.Dot(q, up) / h + 0.5f;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (!ok || maxX < 0f || minX > 1f || maxY < 0f || minY > 1f) continue;
                int b = v.Count;
                for (int k = 0; k < 3; k++)
                {
                    int vi = tris[i + k];
                    Vector3 q = V[vi] - centre;
                    v.Add(V[vi] + N[vi] * 0.0025f);
                    n.Add(N[vi]);
                    uv0.Add(new Vector4(Vector3.Dot(q, right) / w + 0.5f, Vector3.Dot(q, up) / h + 0.5f, 0f, 0f));
                    uv1.Add(rect);
                    c.Add(col);
                }
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                added++;
            }
            return added;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    internal sealed class CarDecals : CarDetailSystem
    {
        const int AS = 256;
        static readonly Dictionary<string, Texture2D> atlases = new Dictionary<string, Texture2D>();
        static readonly RectInt RNum = new RectInt(0, 0, 128, 96), RPlate = new RectInt(128, 0, 128, 32),
            RLotus = new RectInt(128, 32, 128, 32), RJade = new RectInt(128, 64, 128, 32), RPanda = new RectInt(128, 96, 128, 32),
            RBanner = new RectInt(0, 128, 256, 32), RDialS = new RectInt(0, 160, 64, 64), RDialR = new RectInt(64, 160, 64, 64);

        CarDetails det;
        GameObject go;
        MeshFilter mf;
        Mesh mesh;
        Material mat;
        static readonly int IdW2L = Shader.PropertyToID("_CarW2L");

        static Vector4 UV(RectInt r)
        {
            return new Vector4((r.x + 1f) / AS, (r.y + 1f) / AS, (r.width - 2f) / AS, (r.height - 2f) / AS);
        }

        internal static int CarNumber(string id)
        {
            int num = 0;
            foreach (char ch in id) if (ch >= '0' && ch <= '9') num = num * 10 + (ch - '0');
            return num == 0 ? 7 : num;
        }

        static Texture2D Atlas(string id)
        {
            Texture2D tex;
            if (atlases.TryGetValue(id, out tex) && tex != null) return tex;
            var c = new PixelCanvas(AS, AS);
            c.Clear(new Color32(255, 255, 255, 0));
            int num = CarNumber(id);
            Color32 black = new Color32(20, 20, 24, 255), white = new Color32(246, 244, 238, 255);
            Color32 pink = new Color32(236, 112, 158, 255), plum = new Color32(70, 26, 64, 255);

            // rally door number panel
            c.RoundRect(RNum.x + 2, RNum.y + 2, RNum.width - 4, RNum.height - 4, 12, black);
            c.RoundRect(RNum.x + 5, RNum.y + 5, RNum.width - 10, RNum.height - 10, 9, white);
            c.Rect(RNum.x + 5, RNum.y + 68, RNum.width - 10, 20, pink);
            c.TextFit("BLOSSOM", RNum.x + 8, RNum.y + 69, RNum.width - 16, 18, white, 2);
            c.TextFit(num.ToString(), RNum.x + 10, RNum.y + 9, RNum.width - 20, 56, black, 8);

            // number plate (Blossom Valley)
            c.RoundRect(RPlate.x + 1, RPlate.y + 1, RPlate.width - 2, RPlate.height - 2, 4, black);
            c.RoundRect(RPlate.x + 3, RPlate.y + 3, RPlate.width - 6, RPlate.height - 6, 3, new Color32(250, 214, 70, 255));
            c.Rect(RPlate.x + 3, RPlate.y + 3, 14, RPlate.height - 6, new Color32(30, 70, 160, 255));
            c.Text("*", RPlate.x + 5, RPlate.y + 12, 1, new Color32(250, 214, 70, 255));
            c.TextFit("BV " + (num % 1000).ToString("000"), RPlate.x + 19, RPlate.y + 4, RPlate.width - 23, RPlate.height - 8, black, 3);

            // LOTUS TEA
            c.RoundRect(RLotus.x + 1, RLotus.y + 2, RLotus.width - 2, RLotus.height - 4, 9, pink);
            float lx = RLotus.x + 16, ly = RLotus.y + 14;
            c.Disc(lx - 6, ly + 2, 5, new Color32(255, 220, 232, 255));
            c.Disc(lx + 6, ly + 2, 5, new Color32(255, 220, 232, 255));
            c.Disc(lx, ly + 5, 6, white);
            c.Rect((int)lx - 8, (int)ly - 6, 16, 3, new Color32(80, 160, 90, 255));
            c.TextFit("LOTUS TEA", RLotus.x + 28, RLotus.y + 4, RLotus.width - 32, RLotus.height - 8, white, 3);

            // JADE OIL
            c.RoundRect(RJade.x + 1, RJade.y + 2, RJade.width - 2, RJade.height - 4, 6, new Color32(28, 132, 96, 255));
            c.Disc(RJade.x + 15, RJade.y + 13, 7, new Color32(170, 240, 200, 255));
            c.Line(RJade.x + 9, RJade.y + 16, RJade.x + 15, RJade.y + 27, 3f, new Color32(170, 240, 200, 255));
            c.Line(RJade.x + 21, RJade.y + 16, RJade.x + 15, RJade.y + 27, 3f, new Color32(170, 240, 200, 255));
            c.TextFit("JADE OIL", RJade.x + 28, RJade.y + 4, RJade.width - 32, RJade.height - 8, white, 3);

            // PANDA TYRES
            c.RoundRect(RPanda.x + 1, RPanda.y + 2, RPanda.width - 2, RPanda.height - 4, 6, black);
            c.RoundRect(RPanda.x + 3, RPanda.y + 4, RPanda.width - 6, RPanda.height - 8, 5, white);
            float px = RPanda.x + 16, py = RPanda.y + 15;
            c.Disc(px - 7, py + 7, 4, black);
            c.Disc(px + 7, py + 7, 4, black);
            c.Disc(px, py, 9, white);
            c.Ring(px, py, 9, 1.5f, black);
            c.Disc(px - 4, py + 1, 2.5f, black);
            c.Disc(px + 4, py + 1, 2.5f, black);
            c.Disc(px, py - 4, 1.5f, black);
            c.TextFit("PANDA TYRES", RPanda.x + 28, RPanda.y + 5, RPanda.width - 32, RPanda.height - 10, black, 2);

            // windscreen banner
            c.Rect(RBanner.x, RBanner.y + 1, RBanner.width, RBanner.height - 2, plum);
            for (int k = 0; k < 2; k++)
            {
                float bx = k == 0 ? RBanner.x + 14 : RBanner.xMax - 14, by = RBanner.y + 16;
                for (int pp = 0; pp < 5; pp++)
                {
                    float an = pp * Mathf.PI * 2f / 5f;
                    c.Disc(bx + Mathf.Cos(an) * 5f, by + Mathf.Sin(an) * 5f, 4f, new Color32(255, 196, 214, 255));
                }
                c.Disc(bx, by, 2.5f, new Color32(250, 214, 70, 255));
            }
            c.TextFit("BLOSSOM RALLY", RBanner.x + 30, RBanner.y + 4, RBanner.width - 60, RBanner.height - 8, new Color32(255, 186, 210, 255), 3);

            DrawDial(c, RDialS, 11, -1, "KMH");
            DrawDial(c, RDialR, 9, 7, "RPM");

            tex = c.ToTexture("BR_Decals_" + id, true);
            atlases[id] = tex;
            return tex;
        }

        static void DrawDial(PixelCanvas c, RectInt r, int ticks, int redFrom, string label)
        {
            float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
            c.Disc(cx, cy, 30.5f, new Color32(60, 60, 64, 255));
            c.Disc(cx, cy, 28.5f, new Color32(16, 16, 18, 255));
            for (int i = 0; i < ticks; i++)
            {
                float k = (float)i / (ticks - 1);
                // same convention as the needles: clockwise from the top, -135 .. +135 deg
                float ang = (-135f + 270f * k) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
                Color32 col = redFrom >= 0 && i >= redFrom ? new Color32(240, 60, 40, 255) : new Color32(240, 240, 236, 255);
                c.Line(cx + d.x * 20f, cy + d.y * 20f, cx + d.x * 26.5f, cy + d.y * 26.5f, 2.2f, col);
                if (i < ticks - 1)
                {
                    float km = (i + 0.5f) / (ticks - 1);
                    Vector2 e = new Vector2(Mathf.Sin((-135f + 270f * km) * Mathf.Deg2Rad), Mathf.Cos((-135f + 270f * km) * Mathf.Deg2Rad));
                    c.Line(cx + e.x * 23.5f, cy + e.y * 23.5f, cx + e.x * 26.5f, cy + e.y * 26.5f, 1.2f, col);
                }
            }
            c.TextFit(label, (int)cx - 14, (int)cy - 20, 28, 9, new Color32(230, 200, 120, 255), 1);
        }

        public override void Build(CarDetails d)
        {
            det = d;
            var a = d.a;
            var b = new DecalMeshBuilder();
            Color32 plain = new Color32(255, 255, 255, 0), lit = new Color32(255, 255, 255, 255);
            Vector3 e = a.ext;
            float z0 = e.z * 0.05f;
            float y0 = e.y * 0.45f;
            float panelH = Mathf.Min(0.34f, (a.scrO.y - 0.08f) - (y0 - 0.17f));
            panelH = Mathf.Clamp(panelH, 0.22f, 0.34f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 view = new Vector3(-s, 0f, 0f);
                float sx = a.SurfaceX(y0, z0, 0.16f);
                b.Projected(a, new Vector3(s * sx, y0, z0), view, Vector3.up, panelH * 1.333f, panelH, 0.14f, UV(RNum), plain);
                float yl = Mathf.Max(a.bodyBounds.min.y + 0.12f, e.y * 0.25f);
                float sxl = a.SurfaceX(yl, z0, 0.16f);
                b.Projected(a, new Vector3(s * sxl, yl, z0), view, Vector3.up, 0.5f, 0.1f, 0.14f, UV(RPanda), plain);
                float zq = Mathf.Lerp(a.rearAxleZ, z0, 0.48f);
                float yq = Mathf.Min(e.y * 0.5f, a.scrO.y - 0.13f);
                float sxq = a.SurfaceX(yq, zq, 0.16f);
                b.Projected(a, new Vector3(s * sxq, yq, zq), view, Vector3.up, 0.38f, 0.095f, 0.14f, UV(RJade), plain);
            }
            // bonnet sponsor (reads from behind / the cockpit), roof number (from above, nose up)
            float zb = Mathf.Lerp(a.scrO.z, a.bodyBounds.max.z, 0.52f);
            b.Projected(a, new Vector3(0f, a.SurfaceY(0f, zb, 0.15f), zb), Vector3.down, Vector3.forward, 0.56f, 0.14f, 0.15f, UV(RLotus), plain);
            if (d.quality >= 1)
            {
                float zr = (a.roofMinZ + a.roofMaxZ) * 0.5f;
                b.Projected(a, new Vector3(0f, a.roofY, zr), Vector3.down, Vector3.forward, 0.36f, 0.27f, 0.1f, UV(RNum), plain);
            }
            // plates
            for (int p = 0; p < 2; p++)
                if (a.hasPlate[p])
                {
                    Vector3 nrm = p == 0 ? Vector3.forward : Vector3.back;
                    b.Quad(a.plate[p] + nrm * 0.004f, nrm, Vector3.up, a.plateSize[p].x * 0.98f, a.plateSize[p].y * 0.98f, UV(RPlate), plain);
                }
            // windscreen banner
            if (a.hasScreen)
                b.Quad(a.Screen(0f, a.scrH - 0.055f, 0.003f), a.scrN, a.scrUp, a.scrTopHW * 1.84f, 0.085f, UV(RBanner), plain);
            // dial faces
            var L = CarCockpit.GetLayout(a);
            b.Quad(L.dialSpeed, L.dialN, L.dialUp, L.dialR * 2f, L.dialR * 2f, UV(RDialS), lit);
            b.Quad(L.dialRev, L.dialN, L.dialUp, L.dialR * 2f, L.dialR * 2f, UV(RDialR), lit);

            mesh = b.ToMesh("BR_CarDecals");
            go = new GameObject("CarDecals");
            go.layer = CarExtras.CarLayer;
            go.transform.SetParent(d.transform, false);
            mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            mat = new Material(MaterialLibrary.FindShader("BlossomRally/Decal")) { name = "BR_Decals_" + a.id };
            mat.SetTexture("_MainTex", Atlas(a.id));
            mat.SetVector("_CarExt", e);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            d.RegisterDeformable(mf);
        }

        public override void Dispose()
        {
            if (det != null && det.damage != null && mf != null) det.damage.UnregisterDeformable(mf);
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (mat != null) Object.Destroy(mat);
        }

        public override void Tick(CarDetails d, float dt)
        {
            if (mat == null) return;
            mat.SetMatrix(IdW2L, d.transform.worldToLocalMatrix);
            Color dc;
            mat.SetFloat("_Dirt", d.PaintDirt(out dc));
            mat.SetColor("_DirtColor", dc);
            mat.SetFloat("_Glow", 0.12f + Atmosphere.NightFactor * 0.9f);
        }
    }
}

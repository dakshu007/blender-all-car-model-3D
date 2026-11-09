// RallyGeo.cs - geometry plumbing for RallyLife: static prop chunks (every static prop in a 128 m cell
// merged into one mesh / draw call with the shared vertex-colour wind material), template stamping,
// 7-segment digits (static boards) and small glowing vertex-colour meshes whose elements change
// colour at runtime (LED timing boards, start lights) - world text without textures or RenderTextures.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    internal static class RallyGeo
    {
        /// <summary>Append a template into dst through a matrix (vertex colours and alpha kept).</summary>
        public static void Append(LifeGeo dst, LifeGeo src, Matrix4x4 m)
        {
            int o = dst.v.Count;
            for (int i = 0; i < src.v.Count; i++)
            {
                dst.v.Add(m.MultiplyPoint3x4(src.v[i]));
                dst.n.Add(m.MultiplyVector(src.n[i]).normalized);
                dst.c.Add(src.c[i]);
            }
            for (int i = 0; i < src.t.Count; i++) dst.t.Add(src.t[i] + o);
        }

        /// <summary>Same template with every vertex colour replaced (keeps per-vertex alpha).</summary>
        public static LifeGeo Recolor(LifeGeo src, System.Func<Color32, Color32> f)
        {
            var g = new LifeGeo();
            g.v.AddRange(src.v);
            g.n.AddRange(src.n);
            g.t.AddRange(src.t);
            for (int i = 0; i < src.c.Count; i++) g.c.Add(f(src.c[i]));
            return g;
        }

        /// <summary>Set the wind weight (vertex alpha) of every vertex added since 'from'.</summary>
        public static void Wind(LifeGeo g, int from, System.Func<Vector3, float> weight)
        {
            for (int i = from; i < g.c.Count; i++)
            {
                var c = g.c[i];
                c.a = (byte)Mathf.Clamp(weight(g.v[i]) * 255f, 0f, 255f);
                g.c[i] = c;
            }
        }

        /// <summary>Flat quad, both faces.</summary>
        public static void Quad2(LifeGeo g, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            g.Tri2(a, b, c, col, col, col);
            g.Tri2(a, c, d, col, col, col);
        }

        /// <summary>Oriented box (centre, half extents along right / up / forward).</summary>
        public static void OBox(LifeGeo g, Vector3 ctr, Vector3 right, Vector3 up, Vector3 fwd, Vector3 h, Color32 col)
        {
            Vector3 x = right * h.x, y = up * h.y, z = fwd * h.z;
            for (int s = -1; s <= 1; s += 2)
            {
                g.QuadOut(ctr + x * s - y - z, ctr + x * s + y - z, ctr + x * s + y + z, ctr + x * s - y + z, x * s, col, col, col, col);
                g.QuadOut(ctr + y * s - x - z, ctr + y * s + x - z, ctr + y * s + x + z, ctr + y * s - x + z, y * s, col, col, col, col);
                g.QuadOut(ctr + z * s - x - y, ctr + z * s + x - y, ctr + z * s + x + y, ctr + z * s - x + y, z * s, col, col, col, col);
            }
        }

        /// <summary>Box between two points (beam / post) with a square cross-section.</summary>
        public static void Beam(LifeGeo g, Vector3 a, Vector3 b, float half, Color32 col)
        {
            Vector3 f = b - a;
            float len = f.magnitude;
            if (len < 1e-4f) return;
            f /= len;
            Vector3 r = Mathf.Abs(f.y) > 0.95f ? Vector3.right : Vector3.Cross(Vector3.up, f).normalized;
            Vector3 u = Vector3.Cross(f, r);
            OBox(g, (a + b) * 0.5f, r, u, f, new Vector3(half, half, len * 0.5f), col);
        }

        // ------------------------------------------------------------ 7-segment digits
        //  bit 0 a top, 1 b upper right, 2 c lower right, 3 d bottom, 4 e lower left, 5 f upper left, 6 g middle
        public static readonly int[] DigitMask = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        public const int Dash = 0x40;

        /// <summary>Segment s of a digit (origin = bottom-left, size w x h) as a 6-point bevelled bar.</summary>
        public static void Segment(LifeGeo g, Vector3 o, Vector3 right, Vector3 up, Vector3 normal, float w, float h, int s, Color32 col)
        {
            float t = h * 0.12f, half = h * 0.5f, inset = t * 0.6f;
            Vector3 a, b;
            bool horiz;
            switch (s)
            {
                case 0: a = new Vector3(0f, h); b = new Vector3(w, h); horiz = true; break;
                case 1: a = new Vector3(w, h); b = new Vector3(w, half); horiz = false; break;
                case 2: a = new Vector3(w, half); b = new Vector3(w, 0f); horiz = false; break;
                case 3: a = new Vector3(0f, 0f); b = new Vector3(w, 0f); horiz = true; break;
                case 4: a = new Vector3(0f, half); b = new Vector3(0f, 0f); horiz = false; break;
                case 5: a = new Vector3(0f, h); b = new Vector3(0f, half); horiz = false; break;
                default: a = new Vector3(0f, half); b = new Vector3(w, half); horiz = true; break;
            }
            Vector3 dir = (b - a).normalized, perp = horiz ? new Vector3(0f, 1f) : new Vector3(1f, 0f);
            Vector3 p0 = a + dir * inset, p1 = b - dir * inset;
            Vector3[] q =
            {
                p0, p0 + dir * (t * 0.5f) + perp * (t * 0.5f), p1 - dir * (t * 0.5f) + perp * (t * 0.5f),
                p1, p1 - dir * (t * 0.5f) - perp * (t * 0.5f), p0 + dir * (t * 0.5f) - perp * (t * 0.5f)
            };
            var w3 = new Vector3[6];
            for (int i = 0; i < 6; i++) w3[i] = o + right * q[i].x + up * q[i].y;
            g.TriOut(w3[0], w3[1], w3[2], normal, col, col, col);
            g.TriOut(w3[0], w3[2], w3[3], normal, col, col, col);
            g.TriOut(w3[0], w3[3], w3[4], normal, col, col, col);
            g.TriOut(w3[0], w3[4], w3[5], normal, col, col, col);
        }

        /// <summary>Static number (lit segments only), centred on 'ctr'. Digit height h.</summary>
        public static void Number(LifeGeo g, string digits, Vector3 ctr, Vector3 right, Vector3 up, Vector3 normal, float h, Color32 col)
        {
            float w = h * 0.55f, gap = h * 0.22f;
            float total = digits.Length * w + (digits.Length - 1) * gap;
            Vector3 o = ctr - right * (total * 0.5f) - up * (h * 0.5f);
            for (int k = 0; k < digits.Length; k++)
            {
                int d = digits[k] - '0';
                int mask = d >= 0 && d <= 9 ? DigitMask[d] : Dash;
                for (int s = 0; s < 7; s++)
                    if ((mask & (1 << s)) != 0) Segment(g, o, right, up, normal, w, h, s, col);
                o += right * (w + gap);
            }
        }

        public static MeshRenderer MakeRenderer(string name, Mesh mesh, Material mat, Transform parent, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }
    }

    /// <summary>All static rally props merged per 128 m cell: one mesh + one draw call per cell.</summary>
    internal sealed class StaticChunks
    {
        const float Cell = 128f;
        readonly Dictionary<long, LifeGeo> cells = new Dictionary<long, LifeGeo>();
        public int Renderers { get; private set; }
        public int Vertices { get; private set; }

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public LifeGeo At(Vector3 p)
        {
            long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
            LifeGeo g;
            if (!cells.TryGetValue(k, out g)) { g = new LifeGeo(); cells[k] = g; }
            return g;
        }

        /// <summary>Stamp a template at a world pose.</summary>
        public void Add(LifeGeo template, Vector3 pos, Quaternion rot, float scale = 1f)
        {
            RallyGeo.Append(At(pos), template, Matrix4x4.TRS(pos, rot, new Vector3(scale, scale, scale)));
        }

        /// <summary>Stamp through a matrix (identity = geometry already in world space); the cell is
        /// chosen from the transformed bounds centre so culling stays tight.</summary>
        public void Add(LifeGeo template, Matrix4x4 m)
        {
            if (template.v.Count == 0) return;
            Vector3 mn = template.v[0], mx = mn;
            for (int i = 1; i < template.v.Count; i++) { mn = Vector3.Min(mn, template.v[i]); mx = Vector3.Max(mx, template.v[i]); }
            RallyGeo.Append(At(m.MultiplyPoint3x4((mn + mx) * 0.5f)), template, m);
        }

        public void Finish(string name, Transform parent, Material mat, bool shadows = true)
        {
            int i = 0;
            foreach (var kv in cells)
            {
                var g = kv.Value;
                if (g.v.Count == 0) continue;
                var mesh = g.ToMesh(name + "_" + i);
                RallyGeo.MakeRenderer(name + "_" + i, mesh, mat, parent, shadows);
                Vertices += g.v.Count;
                Renderers++;
                i++;
            }
            cells.Clear();
        }
    }

    /// <summary>
    /// A small glowing vertex-colour mesh made of elements (segments, lamps) whose colours change at
    /// runtime. Colours are only re-uploaded when something actually changed.
    /// </summary>
    internal sealed class GlowMesh
    {
        public readonly LifeGeo geo = new LifeGeo();
        readonly List<int> elemStart = new List<int>(), elemCount = new List<int>();
        Color32[] colors;
        Mesh mesh;
        bool dirty;
        public MeshRenderer Renderer { get; private set; }

        public int Begin() { elemStart.Add(geo.v.Count); elemCount.Add(0); return elemStart.Count - 1; }
        public void End(int e) { elemCount[e] = geo.v.Count - elemStart[e]; }

        public void Finish(string name, Transform parent, Material mat)
        {
            mesh = geo.ToMesh(name);
            mesh.MarkDynamic();
            colors = geo.c.ToArray();
            Renderer = RallyGeo.MakeRenderer(name, mesh, mat, parent, false);
        }

        public void Set(int e, Color32 c)
        {
            if (colors == null) return;
            int s = elemStart[e], n = elemCount[e];
            if (n == 0) return;
            Color32 o = colors[s];
            if (o.r == c.r && o.g == c.g && o.b == c.b) return;
            for (int i = s; i < s + n; i++) colors[i] = c;
            dirty = true;
        }

        public void Upload()
        {
            if (!dirty || mesh == null) return;
            dirty = false;
            mesh.colors32 = colors;
        }
    }

    /// <summary>LED timing board: "M:SS.cc" in 7-segment digits on a GlowMesh.</summary>
    internal sealed class LedBoard
    {
        readonly GlowMesh glow;
        readonly int[,] seg;            // digit, segment -> element
        readonly int colon, dot;
        public readonly int Digits;
        static readonly Color32 On = new Color32(255, 92, 40, 255), Off = new Color32(34, 10, 8, 255);
        static readonly Color32 OnGold = new Color32(255, 196, 60, 255);
        static readonly Color32 OnGreen = new Color32(90, 255, 120, 255);
        int shownKey = int.MinValue;

        /// <summary>Builds 5 digits + colon + decimal point centred on ctr, facing 'normal'.</summary>
        public LedBoard(GlowMesh glow, Vector3 ctr, Vector3 right, Vector3 up, Vector3 normal, float h)
        {
            this.glow = glow;
            Digits = 5;
            seg = new int[Digits, 7];
            float w = h * 0.55f, gap = h * 0.24f, sep = h * 0.32f;
            float total = Digits * w + (Digits - 1) * gap + 2f * sep;
            Vector3 o = ctr - right * (total * 0.5f) - up * (h * 0.5f) + normal * 0.01f;
            var g = glow.geo;
            for (int d = 0; d < Digits; d++)
            {
                for (int s = 0; s < 7; s++)
                {
                    int e = glow.Begin();
                    RallyGeo.Segment(g, o, right, up, normal, w, h, s, Off);
                    glow.End(e);
                    seg[d, s] = e;
                }
                o += right * (w + gap);
                if (d == 0)
                {
                    colon = glow.Begin();
                    Dot(g, o + right * (sep * 0.5f - gap * 0.5f) + up * (h * 0.3f), right, up, normal, h * 0.07f);
                    Dot(g, o + right * (sep * 0.5f - gap * 0.5f) + up * (h * 0.72f), right, up, normal, h * 0.07f);
                    glow.End(colon);
                    o += right * sep;
                }
                if (d == 2)
                {
                    dot = glow.Begin();
                    Dot(g, o + right * (sep * 0.5f - gap * 0.5f) + up * (h * 0.05f), right, up, normal, h * 0.07f);
                    glow.End(dot);
                    o += right * sep;
                }
            }
        }

        static void Dot(LifeGeo g, Vector3 c, Vector3 r, Vector3 u, Vector3 n, float s)
        {
            g.QuadOut(c - r * s - u * s, c + r * s - u * s, c + r * s + u * s, c - r * s + u * s, n, Off, Off, Off, Off);
        }

        /// <summary>Show a time (s); negative shows dashes. style: 0 normal, 1 gold (record), 2 green (go).</summary>
        public void ShowTime(float t, int style)
        {
            int key;
            if (t < 0f) key = -1;
            else
            {
                int cs = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, 9 * 6000 + 5999);
                key = cs;
            }
            key = key * 4 + style;
            if (key == shownKey) return;
            shownKey = key;
            Color32 on = style == 1 ? OnGold : style == 2 ? OnGreen : On;
            if (t < 0f)
            {
                for (int d = 0; d < Digits; d++) SetDigit(d, RallyGeo.Dash, on);
                glow.Set(colon, Off);
                glow.Set(dot, Off);
                return;
            }
            int c = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, 9 * 6000 + 5999);
            int min = c / 6000, sec = (c / 100) % 60, hund = c % 100;
            SetDigit(0, RallyGeo.DigitMask[min], on);
            SetDigit(1, RallyGeo.DigitMask[sec / 10], on);
            SetDigit(2, RallyGeo.DigitMask[sec % 10], on);
            SetDigit(3, RallyGeo.DigitMask[hund / 10], on);
            SetDigit(4, RallyGeo.DigitMask[hund % 10], on);
            glow.Set(colon, on);
            glow.Set(dot, on);
        }

        void SetDigit(int d, int mask, Color32 on)
        {
            for (int s = 0; s < 7; s++) glow.Set(seg[d, s], (mask & (1 << s)) != 0 ? on : Off);
        }
    }
}

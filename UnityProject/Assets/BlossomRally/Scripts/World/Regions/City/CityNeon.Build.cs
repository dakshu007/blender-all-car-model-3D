// CityNeon.Build.cs - neon signage of Jade Harbour City: blade signs, facade boards and rooftop frames with
// abstract hanzi-like glyphs and tube outlines. The tubes are split over four glow groups, each with its own
// PropGlow material, so Tick can pulse, chase, flicker and sweep them with four SetFloat calls per frame.
// Aircraft beacons on the spires are one more mesh that blinks.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CityNeon
    {
        public const int Groups = 4;
        readonly LifeGeo[] tubes = new LifeGeo[Groups];
        readonly Material[] mats = new Material[Groups];
        readonly LifeGeo beaconGeo = new LifeGeo();
        Material beaconMat;
        public int Signs { get; private set; }

        public CityNeon()
        {
            for (int i = 0; i < Groups; i++) tubes[i] = new LifeGeo();
        }

        public LifeGeo Group(int i) { return tubes[i & 3]; }

        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }

        /// <summary>Rectangle outline of tubes in the plane right / up around c, facing n.</summary>
        public static void Outline(LifeGeo g, Vector3 c, Vector3 right, Vector3 up, Vector3 n, float w, float h, float t, Color32 col)
        {
            Vector3 o = c + n * 0.035f;
            Vector3 a = o - right * w * 0.5f - up * h * 0.5f, b = o + right * w * 0.5f - up * h * 0.5f;
            Vector3 d = o + right * w * 0.5f + up * h * 0.5f, e = o - right * w * 0.5f + up * h * 0.5f;
            System.Action<Vector3, Vector3, Vector3> bar = (p, q, side) => g.QuadOut(p - side, q - side, q + side, p + side, n, col, col, col, col);
            bar(a - right * t * 0.5f, b + right * t * 0.5f, up * t * 0.5f);
            bar(e - right * t * 0.5f, d + right * t * 0.5f, up * t * 0.5f);
            bar(a, e, right * t * 0.5f);
            bar(b, d, right * t * 0.5f);
        }

        /// <summary>A sign at a world spot. Boards go into 'geo' (merged statics); tubes into the glow groups.</summary>
        public void AddSign(CityChunkGeo geo, CitySignSpot s, System.Random rng)
        {
            Vector3 n = s.n.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 up = Vector3.up;
            Color32 board = C(24, 26, 34);
            Color32 frame = C(60, 64, 72);
            int ga = rng.Next(Groups), gb = (ga + 1 + rng.Next(Groups - 1)) % Groups;
            Color32 ca = CityKit.Neon[rng.Next(CityKit.Neon.Length)], cb = CityKit.Neon[rng.Next(CityKit.Neon.Length)];
            if (ca.r == cb.r && ca.g == cb.g) cb = C(255, 250, 240);
            Vector3 o;
            if (s.kind == 0)
            {
                // blade sign: thin board perpendicular to the facade, glyphs stacked on both faces
                var g = geo.At(PK.Landmark, s.pos, out o);
                PG.OBox(g, s.pos - o, new Vector3(s.w * 0.5f + 0.1f, s.h * 0.5f + 0.1f, 0.12f), Quaternion.LookRotation(n), board);
                PG.Beam(g, s.pos - o + up * (s.h * 0.5f + 0.2f) - right * s.w * 0.5f, s.pos - o + up * (s.h * 0.5f + 0.2f) - right * (s.w * 0.5f + 1.2f), 0.05f, 0.05f, frame);
                int count = Mathf.Max(1, Mathf.FloorToInt(s.h / (s.w * 1.05f)));
                float gs = Mathf.Min(s.w * 0.72f, s.h / count * 0.8f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 nn = n * side;
                    Vector3 rr = right * side;
                    Vector3 fc = s.pos + nn * 0.12f;
                    Outline(tubes[ga], fc, rr, up, nn, s.w + 0.05f, s.h + 0.05f, 0.09f, ca);
                    for (int k = 0; k < count; k++)
                    {
                        float y = (s.h * 0.5f) - (k + 0.5f) * s.h / count;
                        CityKit.Glyph(tubes[gb], fc + up * y, rr, up, nn, gs, gs * 0.11f, cb, new System.Random(rng.Next() ^ side));
                    }
                }
            }
            else
            {
                // facade board / rooftop frame facing n
                var g = geo.At(PK.Landmark, s.pos, out o);
                PG.OBox(g, s.pos - o - n * 0.15f, new Vector3(s.w * 0.5f + 0.15f, s.h * 0.5f + 0.15f, 0.15f), Quaternion.LookRotation(n), board);
                if (s.kind == 2)
                {
                    // lattice legs down to the roof
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 a = s.pos - o + right * k * s.w * 0.35f - n * 0.4f;
                        PG.Beam(g, a - up * (s.h * 0.5f + 3.6f), a - up * (s.h * 0.5f), 0.12f, 0.12f, frame);
                        PG.Beam(g, a - up * (s.h * 0.5f + 3.6f) - n * 2.5f, a - up * (s.h * 0.5f) - n * 0.1f, 0.08f, 0.08f, frame);
                    }
                }
                Outline(tubes[ga], s.pos, right, up, n, s.w, s.h, s.kind == 2 ? 0.22f : 0.1f, ca);
                int count = Mathf.Max(1, Mathf.FloorToInt(s.w / (s.h * 1.05f)));
                float gs = Mathf.Min(s.h * 0.72f, s.w / count * 0.8f);
                for (int k = 0; k < count; k++)
                {
                    float x = -s.w * 0.5f + (k + 0.5f) * s.w / count;
                    CityKit.Glyph(tubes[gb], s.pos + right * x, right, up, n, gs, gs * (s.kind == 2 ? 0.12f : 0.1f), cb, new System.Random(rng.Next()));
                }
            }
            Signs++;
        }

        public void AddBeacon(Vector3 p)
        {
            Color32 red = C(255, 40, 30);
            beaconGeo.Frustum(p - Vector3.up * 0.45f, p, 0.05f, 0.05f, 0.45f, 0.45f, 4, 0f, red, red, false, false);
            beaconGeo.Frustum(p, p + Vector3.up * 0.45f, 0.45f, 0.45f, 0.05f, 0.05f, 4, 0f, red, red, false, false);
        }

        public int Finish(RegionContext ctx)
        {
            int renderers = 0;
            var sh = MaterialLibrary.FindShader("BlossomRally/PropGlow");
            for (int i = 0; i < Groups; i++)
            {
                if (tubes[i].v.Count == 0) continue;
                mats[i] = new Material(sh) { name = "BR_CityNeon" + i };
                mats[i].SetFloat("_WindStrength", 0f);
                ctx.AddMesh("City_Neon" + i, tubes[i].ToMesh("City_Neon" + i), mats[i], 0, false);
                renderers++;
            }
            if (beaconGeo.v.Count > 0)
            {
                beaconMat = new Material(sh) { name = "BR_CityBeacons" };
                beaconMat.SetFloat("_WindStrength", 0f);
                ctx.AddMesh("City_Beacons", beaconGeo.ToMesh("City_Beacons"), beaconMat, 0, false, true);
                renderers++;
            }
            return renderers;
        }

        static float Hash(int i) { return WorldNoise.Hash01(i, 7, 1301); }

        /// <summary>Per-frame animation: four material floats and the beacon blink (no allocations).</summary>
        public void Tick(float t, float night)
        {
            float baseGlow = 0.45f + 2.9f * night;
            for (int i = 0; i < Groups; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                float k;
                switch (i)
                {
                    case 0: k = 0.78f + 0.22f * Mathf.Sin(t * 2.4f); break;                                     // breathing
                    case 1: k = Mathf.Repeat(t * 1.4f, 1f) < 0.55f ? 1.05f : 0.38f; break;                       // chase blink
                    case 2:                                                                                       // faulty tube flicker
                        {
                            int f = Mathf.FloorToInt(t * 14f);
                            float h = Hash(f);
                            k = h > 0.9f ? 0.15f : h > 0.82f ? 0.6f : 1f;
                            break;
                        }
                    default: k = 0.85f + 0.15f * Mathf.Sin(t * 0.9f + 1.3f); break;                               // slow sweep
                }
                m.SetFloat("_Glow", baseGlow * k);
            }
            if (beaconMat != null)
            {
                float ph = Mathf.Repeat(t * 0.8f, 1f);
                beaconMat.SetFloat("_Glow", ph < 0.18f ? 2.5f + 3f * night : 0.05f);
            }
        }
    }
}

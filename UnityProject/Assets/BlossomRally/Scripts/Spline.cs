// Spline.cs - Catmull-Rom sampling and simple 2D geometry helpers.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class Spline
    {
        public static List<Vector2> Sample(Vector2[] pts, bool closed, float step)
        {
            var outPts = new List<Vector2>();
            int n = pts.Length;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector2 p0 = pts[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
                Vector2 p1 = pts[i];
                Vector2 p2 = pts[(i + 1) % n];
                Vector2 p3 = pts[closed ? (i + 2) % n : Mathf.Min(i + 2, n - 1)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    outPts.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            if (!closed) outPts.Add(pts[n - 1]);
            return outPts;
        }

        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return Vector2.Distance(p, a + ab * t);
        }

        public static bool SegmentIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = Vector2.zero;
            Vector2 r = b - a, s = d - c;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            Vector2 ca = c - a;
            float t = (ca.x * s.y - ca.y * s.x) / den;
            float u = (ca.x * r.y - ca.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            hit = a + r * t;
            return true;
        }

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}

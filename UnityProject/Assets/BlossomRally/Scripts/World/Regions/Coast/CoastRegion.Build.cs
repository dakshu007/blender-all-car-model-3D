// CoastRegion.Build.cs - Lotus Coast content: orchestration, colour grade, shared placement helpers and the
// per-frame tick. The pieces live in CoastRegion.Sea / Harbour / Shore / Land / Life (.Build.cs).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        RegionContext cx;
        System.Random rnd;
        int palms, pines, rocks, houses, people;

        // warm turquoise and gold, sun-kissed: golden lift, teal shadows, a touch more saturation
        public override Grade Grade
        {
            get
            {
                return new Grade
                {
                    tint = new Vector3(1.045f, 1.01f, 0.93f), sat = 1.09f, con = 1.03f,
                    shadow = new Vector3(0.84f, 1.0f, 1.07f), shadowAmt = 0.09f,
                    lift = new Vector3(1.0f, 0.88f, 0.68f), liftAmt = 0.035f
                };
            }
        }

        public override IEnumerator Build(RegionContext ctx)
        {
            cx = ctx;
            rnd = new System.Random(52017);
            float t0 = Time.realtimeSinceStartup;
            BuildSea(); ctx.Progress = 0.08f; yield return null;
            BuildShoreFoam(); ctx.Progress = 0.18f; yield return null;
            BuildHeadland(); ctx.Progress = 0.26f; yield return null;
            BuildIslands(); ctx.Progress = 0.32f; yield return null;
            BuildHarbour(); ctx.Progress = 0.46f; yield return null;
            BuildBeach(); ctx.Progress = 0.56f; yield return null;
            BuildLagoons(); ctx.Progress = 0.62f; yield return null;
            BuildRoadside(); ctx.Progress = 0.72f; yield return null;
            BuildVegetation(); ctx.Progress = 0.88f; yield return null;
            BuildLife(); ctx.Progress = 1f;
            Debug.Log("[BlossomRally] Lotus Coast placed: " + palms + " palms, " + pines + " pines, " + rocks + " rocks, " + houses + " buildings, "
                      + people + " people, " + ctx.ColliderCount + " colliders in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }

        // ------------------------------------------------------------------ helpers
        float Rn(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        bool Ch(float p) { return rnd.NextDouble() < p; }
        float Gnd(float x, float z) { return cx.Ground(x, z); }
        float Gnd(Vector2 p) { return cx.Ground(p.x, p.y); }
        static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        static float YawOf(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }
        static Vector2 Dir(float yawDeg) { float a = yawDeg * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(a), Mathf.Cos(a)); }
        float Edge(Vector2 p) { return cx.EdgeDist(p); }
        bool InTile(Vector2 p, float margin) { return cx.InRegion(p, margin); }

        /// <summary>Signed distance to the mainland shore (positive inland), cheap.</summary>
        static float ShoreD(Vector2 p) { return p.x - ShoreX(p.y); }

        /// <summary>Ground slope (1 - normal.y) estimated from the mesh height.</summary>
        float Slope(Vector2 p)
        {
            float hx = Gnd(p.x + 2f, p.y) - Gnd(p.x - 2f, p.y), hz = Gnd(p.x, p.y + 2f) - Gnd(p.x, p.y - 2f);
            return 1f - 4f / Mathf.Sqrt(16f + hx * hx + hz * hz);
        }

        bool Claim(Vector2 p, float r)
        {
            if (!cx.Free(p, r)) return false;
            cx.Reserve(p, r);
            return true;
        }

        void Stamp(PropModel m, Vector2 p, float y, float yaw, float scale = 1f) { cx.Stamp(m, V3(p, y), yaw, scale); }
        void StampGround(PropModel m, Vector2 p, float yaw, float scale = 1f, float sink = 0.05f) { cx.Stamp(m, V3(p, Gnd(p) - sink), yaw, scale); }

        void BoxCol(Vector2 p, float y0, Vector3 size, float yaw) { cx.Box(new Vector3(p.x, y0 + size.y * 0.5f, p.y), size, yaw); }
        void Trunk(Vector2 p, float y, float r, float h) { cx.Capsule(new Vector3(p.x, y, p.y), r, h); }

        /// <summary>Samples of one of this region's roads (null when missing).</summary>
        List<RoadSample> Road(string name)
        {
            int i = cx.Field.RoadIndex(name);
            return i < 0 ? null : cx.Field.roadSamples[i];
        }

        // ------------------------------------------------------------------ tick
        public override void Tick(float dt, Vector3 eye, float presence)
        {
            TickLife(dt, eye);
        }
    }
}

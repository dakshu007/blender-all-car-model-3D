// ColorGrading.cs - region + time-of-day + weather colour grade, blended by camera position.
// Warm golden village, fresh green bamboo forest, bright tea terraces, cool clear lake, rosy sunset,
// lilac blue hour, deep blue night, cool desaturated rain. PostFX multiplies the result into its own
// grade every frame; Identity leaves the base look untouched (that is today's look).
using UnityEngine;

namespace BlossomRally
{
    public struct Grade
    {
        public Vector3 tint;       // multiplies the grade tint (perceptual space)
        public float sat, con;     // multiply saturation / contrast
        public Vector3 shadow;     // multiplies the shadow tint colour
        public float shadowAmt;    // added to the shadow tint amount
        public Vector3 lift;       // lift colour this grade pulls towards
        public float liftAmt;      // added lift amount

        public static Grade Identity
        {
            get { return new Grade { tint = Vector3.one, sat = 1f, con = 1f, shadow = Vector3.one, shadowAmt = 0f, lift = new Vector3(0.9f, 0.85f, 0.9f), liftAmt = 0f }; }
        }

        static Grade G(float tr, float tg, float tb, float sat, float con, float sr, float sg, float sb, float sa, float lr, float lg, float lb, float la)
        {
            return new Grade { tint = new Vector3(tr, tg, tb), sat = sat, con = con, shadow = new Vector3(sr, sg, sb), shadowAmt = sa, lift = new Vector3(lr, lg, lb), liftAmt = la };
        }

        // ---- regions ----
        public static readonly Grade Village = G(1.05f, 1.0f, 0.90f, 1.04f, 1.02f, 1.02f, 0.93f, 0.90f, 0.05f, 1.0f, 0.86f, 0.70f, 0.035f);
        public static readonly Grade Bamboo = G(0.95f, 1.05f, 0.96f, 1.07f, 1.03f, 0.86f, 1.0f, 0.94f, 0.12f, 0.84f, 0.95f, 0.84f, 0.03f);
        public static readonly Grade Terraces = G(1.01f, 1.04f, 0.93f, 1.06f, 1.01f, 0.92f, 1.0f, 0.95f, 0.05f, 0.96f, 0.98f, 0.82f, 0.02f);
        public static readonly Grade Lake = G(0.97f, 1.0f, 1.05f, 1.02f, 1.0f, 0.90f, 0.96f, 1.08f, 0.06f, 0.86f, 0.92f, 1.0f, 0.03f);
        public static readonly Grade Peak = G(1.0f, 0.99f, 1.03f, 0.98f, 1.04f, 0.92f, 0.94f, 1.06f, 0.05f, 0.9f, 0.9f, 1.0f, 0.02f);

        // ---- times of day (Atmosphere.TimeIndex order) ----
        public static readonly Grade[] Times =
        {
            G(1.03f, 0.99f, 0.95f, 1.02f, 1.0f, 0.96f, 0.92f, 1.04f, 0.04f, 1.0f, 0.88f, 0.80f, 0.04f),   // Morning: peach haze
            Identity,                                                                                     // Spring Day
            G(1.06f, 0.95f, 0.93f, 1.07f, 1.03f, 0.90f, 0.82f, 1.06f, 0.12f, 1.0f, 0.78f, 0.74f, 0.04f),  // Golden Sunset: rosy, violet shadows
            G(0.93f, 0.95f, 1.06f, 0.96f, 1.02f, 0.86f, 0.88f, 1.14f, 0.12f, 0.70f, 0.66f, 0.90f, 0.03f), // Blue Hour: lilac
            G(0.86f, 0.92f, 1.08f, 0.90f, 1.06f, 0.80f, 0.86f, 1.18f, 0.18f, 0.10f, 0.13f, 0.26f, 0.04f), // Night: deep blue
        };

        public static readonly Grade Rain = G(0.93f, 0.97f, 1.05f, 0.86f, 0.98f, 0.88f, 0.94f, 1.10f, 0.10f, 0.78f, 0.82f, 0.90f, 0.04f);

        /// <summary>Weighted accumulation (call Begin, Add several, End).</summary>
        public void Begin() { this = new Grade { tint = Vector3.zero, sat = 0f, con = 0f, shadow = Vector3.zero, shadowAmt = 0f, lift = Vector3.zero, liftAmt = 0f }; }

        public void Add(Grade g, float w)
        {
            tint += g.tint * w; sat += g.sat * w; con += g.con * w; shadow += g.shadow * w;
            shadowAmt += g.shadowAmt * w; lift += g.lift * w; liftAmt += g.liftAmt * w;
        }

        public void End(float wsum)
        {
            float k = 1f / Mathf.Max(1e-4f, wsum);
            tint *= k; sat *= k; con *= k; shadow *= k; shadowAmt *= k; lift *= k; liftAmt *= k;
        }

        /// <summary>Apply grade b on top of a (multiplicative parts multiply, additive parts add).</summary>
        public static Grade Combine(Grade a, Grade b)
        {
            float la = a.liftAmt + b.liftAmt;
            return new Grade
            {
                tint = Vector3.Scale(a.tint, b.tint), sat = a.sat * b.sat, con = a.con * b.con,
                shadow = Vector3.Scale(a.shadow, b.shadow), shadowAmt = a.shadowAmt + b.shadowAmt,
                lift = la > 1e-4f ? (a.lift * a.liftAmt + b.lift * b.liftAmt) / la : a.lift, liftAmt = la
            };
        }

        public static Grade Lerp(Grade a, Grade b, float t)
        {
            return new Grade
            {
                tint = Vector3.Lerp(a.tint, b.tint, t), sat = Mathf.Lerp(a.sat, b.sat, t), con = Mathf.Lerp(a.con, b.con, t),
                shadow = Vector3.Lerp(a.shadow, b.shadow, t), shadowAmt = Mathf.Lerp(a.shadowAmt, b.shadowAmt, t),
                lift = Vector3.Lerp(a.lift, b.lift, t), liftAmt = Mathf.Lerp(a.liftAmt, b.liftAmt, t)
            };
        }
    }

    /// <summary>Evaluates the live grade for a camera position; smooth over time.</summary>
    public class RegionGrader
    {
        Grade live = Grade.Identity;
        bool init;
        readonly float[] timeW = new float[5];
        float checkTimer;
        Grade regionTarget = Grade.Identity;

        public Grade Current { get { return live; } }

        static float RectWeight(Vector2 p, Vector2 a, Vector2 b, float soft)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y);
            float dx = Mathf.Max(0f, Mathf.Max(x0 - p.x, p.x - x1)), dz = Mathf.Max(0f, Mathf.Max(z0 - p.y, p.y - z1));
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Clamp01(1f - d / soft);
        }

        Grade EvaluateRegion(Vector3 cam)
        {
            Vector2 p = new Vector2(cam.x, cam.z);
            var g = new Grade();
            g.Begin();
            float wv = RectWeight(p, MapData.P(MapData.VillagePx.xMin, MapData.VillagePx.yMin), MapData.P(MapData.VillagePx.xMax, MapData.VillagePx.yMax + 60), 45f);
            float wb = RectWeight(p, MapData.P(62, 310), MapData.P(350, 1270), 40f);
            float wt = RectWeight(p, MapData.P(1190, 340), MapData.P(1506, 1310), 40f);
            float k = MapData.LakeK(p);
            float wl = Mathf.Clamp01((2.2f - k) / 1.0f);
            var world = WorldHooks.World;
            if (world != null)
            {
                float dr = world.RiverDistance(p);
                wl = Mathf.Max(wl, Mathf.Clamp01((40f - dr) / 25f) * 0.6f);
            }
            float wp = Mathf.Clamp01((cam.y - 22f) / 25f) * Mathf.Clamp01((220f - Vector2.Distance(p, MapData.MountainCenter)) / 80f);
            float wsum = 1f;
            g.Add(Grade.Identity, 1f);
            g.Add(Grade.Village, wv * 1.6f); wsum += wv * 1.6f;
            g.Add(Grade.Bamboo, wb * 1.6f); wsum += wb * 1.6f;
            g.Add(Grade.Terraces, wt * 1.2f); wsum += wt * 1.2f;
            g.Add(Grade.Lake, wl); wsum += wl;
            g.Add(Grade.Peak, wp); wsum += wp;
            g.End(wsum);
            return g;
        }

        /// <summary>Advance the blend (dt unscaled) and return the grade for this frame.</summary>
        public Grade Update(Vector3 camPos, float dt)
        {
            var atm = Atmosphere.Instance;
            int ti = atm != null ? atm.TimeIndex : 1;
            // region lookup is throttled (RiverDistance is a grid search)
            checkTimer -= dt;
            if (checkTimer <= 0f || !init)
            {
                checkTimer = 0.25f;
                regionTarget = EvaluateRegion(camPos);
            }
            float kt = init ? 1f - Mathf.Exp(-dt / 0.9f) : 1f;
            for (int i = 0; i < timeW.Length; i++) timeW[i] = Mathf.Lerp(timeW[i], i == ti ? 1f : 0f, kt);
            var tg = new Grade();
            tg.Begin();
            float ws = 0f;
            for (int i = 0; i < timeW.Length; i++) { tg.Add(Grade.Times[i], timeW[i]); ws += timeW[i]; }
            tg.End(ws);
            // at night the time grade dominates, regions fade back
            Grade region = Grade.Lerp(Grade.Identity, regionTarget, 1f - 0.65f * Atmosphere.NightFactor);
            Grade target = Grade.Combine(region, tg);
            target = Grade.Combine(target, Grade.Lerp(Grade.Identity, Grade.Rain, Mathf.Clamp01(Atmosphere.RainIntensity)));
            float kr = init ? 1f - Mathf.Exp(-dt / 1.2f) : 1f;
            live = Grade.Lerp(live, target, kr);
            init = true;
            return live;
        }
    }
}

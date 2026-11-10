// TyreModel.cs - slip-based tyre forces with per-surface grip.
//
// Combined slip (normalised "similarity" method): longitudinal slip ratio k and lateral slip tan(a) are
// divided by the surface's peak values, giving one slip magnitude s. Force = mu * load * g(s) where
// g(s) = sin(C * atan(B * s)) rises to 1 at s = 1 (the peak, ~7 deg / 10 % on tarmac) and falls gently
// towards sin(C * pi / 2) when sliding (C sets how much grip a sliding tyre keeps). Near the peak the force
// points along the normalised slip; in a full slide it opposes the real slip velocity (Coulomb friction),
// so locked or spinning wheels lose side grip exactly as much as they should.
// Load sensitivity: a tyre's friction coefficient drops a little as its load rises, so weight transfer
// costs total grip (the basis of understeer / oversteer balance changes under brake and throttle).
using UnityEngine;

namespace BlossomRally
{
    public struct SurfaceGrip
    {
        public float mu;            // peak friction coefficient
        public float peakSlipDeg;   // slip angle of peak side force
        public float peakRatio;     // slip ratio of peak drive / brake force
        public float shapeC;        // falloff: higher = less grip kept when sliding
        public float rolling;       // rolling resistance coefficient
        public float looseDrag;     // extra speed-proportional drag (gravel / grass ploughing), N per (m/s) per kN load
        public float wetLoss;       // fraction of mu lost when fully wet
        // derived
        public float shapeB, tanPeak, slideFraction;

        public SurfaceGrip(float mu, float peakSlipDeg, float peakRatio, float c, float rolling, float looseDrag, float wetLoss)
        {
            this.mu = mu; this.peakSlipDeg = peakSlipDeg; this.peakRatio = peakRatio; shapeC = c;
            this.rolling = rolling; this.looseDrag = looseDrag; this.wetLoss = wetLoss;
            shapeB = Mathf.Tan(Mathf.PI / (2f * c));
            tanPeak = Mathf.Tan(peakSlipDeg * Mathf.Deg2Rad);
            slideFraction = Mathf.Sin(c * Mathf.PI * 0.5f);
        }
    }

    public static class TyreModel
    {
        //                                         mu    peak  ratio  C     roll   loose  wet
        static readonly SurfaceGrip asphalt = new SurfaceGrip(1.18f, 7.0f, 0.10f, 1.42f, 0.013f, 0.00f, 0.24f);
        static readonly SurfaceGrip paving  = new SurfaceGrip(1.08f, 7.5f, 0.10f, 1.45f, 0.015f, 0.00f, 0.30f);
        static readonly SurfaceGrip gravel  = new SurfaceGrip(0.84f, 11.0f, 0.16f, 1.24f, 0.028f, 0.010f, 0.08f);
        static readonly SurfaceGrip dirt    = new SurfaceGrip(0.76f, 12.0f, 0.18f, 1.20f, 0.032f, 0.014f, 0.22f);
        static readonly SurfaceGrip grass   = new SurfaceGrip(0.62f, 10.0f, 0.15f, 1.32f, 0.050f, 0.030f, 0.28f);

        public static SurfaceGrip For(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: return asphalt;
                case Surface.Paving: return paving;
                case Surface.Gravel: return gravel;
                case Surface.Dirt: return dirt;
                default: return grass;
            }
        }

        public static bool Loose(Surface s) { return s == Surface.Gravel || s == Surface.Dirt || s == Surface.Grass; }

        /// <summary>Normalised force for normalised slip s (1 at s = 1).</summary>
        public static float Curve(float s, in SurfaceGrip g)
        {
            return Mathf.Sin(g.shapeC * Mathf.Atan(g.shapeB * s));
        }

        /// <summary>d Curve / d s.</summary>
        public static float Slope(float s, in SurfaceGrip g)
        {
            float bs = g.shapeB * s;
            return Mathf.Cos(g.shapeC * Mathf.Atan(bs)) * g.shapeC * g.shapeB / (1f + bs * bs);
        }

        /// <summary>
        /// Tyre force in the wheel frame. slipRatio k = (wr - vx) / |vx|, tanAlpha = vy / |vx| (vy = sideways speed).
        /// Returns fx (forward, + drives the car) and fy (along the wheel's right axis, opposes vy).
        /// muLoad = effective mu * load (N).
        /// </summary>
        public static void Force(float k, float tanAlpha, float muLoad, in SurfaceGrip g, out float fx, out float fy, out float sigma)
        {
            float sx = k / g.peakRatio;
            float sy = tanAlpha / g.tanPeak;
            sigma = Mathf.Sqrt(sx * sx + sy * sy);
            if (sigma < 1e-6f) { fx = fy = 0f; return; }
            float f = muLoad * Curve(sigma, g);
            // direction: normalised slip near the peak, real slip velocity once sliding
            float ux = sx / sigma, uy = sy / sigma;
            float raw = Mathf.Sqrt(k * k + tanAlpha * tanAlpha);
            float w = Mathf.Clamp01((sigma - 1f) * 0.5f);
            if (raw > 1e-6f && w > 0f)
            {
                ux = Mathf.Lerp(ux, k / raw, w);
                uy = Mathf.Lerp(uy, tanAlpha / raw, w);
                float n = Mathf.Sqrt(ux * ux + uy * uy);
                if (n > 1e-6f) { ux /= n; uy /= n; }
            }
            fx = f * ux;
            fy = -f * uy;
        }
    }
}

// CarWindscreen.cs - per-car glass material (BR_Glass extras): the windscreen gets dirty as the car's mud
// level (read from its paint material, fed by CarExtras) rises; in the rain two tandem wipers sweep and clean
// arcs out of the dirt (a 64x32 CPU mask: R = dirt, G = freshly wiped so rain drops vanish behind the blades).
// Other windows get a uniform dust film. Cracks from CarDamage are painted into a 256x128 crack texture.
// The per-car glass is also slightly less dark so the crew is visible from outside.
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarWindscreen : CarDetailSystem
    {
        const int MW = 64, MH = 32, CW = 256, CH = 128;
        const float SweepMax = 102f;

        CarDetails det;
        CarAnatomy a;
        Renderer glassR;
        Material[] savedMats;
        Material glassMat;
        Texture2D maskTex, crackTex;
        Color32[] maskPx;
        float[] dirt, fresh;
        // per pixel polar coordinates around both wiper pivots (r, angle deg)
        float[] polR0, polA0, polR1, polA1;
        Vector3[] pivot = new Vector3[2];
        float r0, r1;
        int[] wiper = { -1, -1 };
        float phase, angle, prevAngle, sweptMin, sweptMax, maskTimer;
        float lastCarDirt = -1f;
        bool wiping;
        int crackCount = -1;
        static readonly int IdW2L = Shader.PropertyToID("_CarW2L");

        public override void Build(CarDetails d)
        {
            det = d;
            a = d.a;
            if (a.glass == null) return;
            glassR = a.glass.GetComponent<Renderer>();
            if (glassR == null) return;
            savedMats = glassR.sharedMaterials;
            int gi = CarAnatomy.FindSubmesh(glassR, "Glass");
            if (gi < 0) gi = 0;
            glassMat = new Material(savedMats[gi] != null ? savedMats[gi] : MaterialLibrary.Glass) { name = "BR_Glass_" + a.id };
            var c = glassMat.HasProperty("_Color") ? glassMat.GetColor("_Color") : new Color(0.04f, 0.06f, 0.08f, 0.82f);
            c.a = Mathf.Min(c.a, 0.62f);
            glassMat.SetColor("_Color", c);
            var mats = (Material[])savedMats.Clone();
            mats[gi] = glassMat;
            glassR.sharedMaterials = mats;

            maskTex = new Texture2D(MW, MH, TextureFormat.RGBA32, false, true) { name = "BR_WipeMask", wrapMode = TextureWrapMode.Clamp };
            maskPx = new Color32[MW * MH];
            dirt = new float[MW * MH];
            fresh = new float[MW * MH];
            crackTex = new Texture2D(CW, CH, TextureFormat.RGBA32, true, true) { name = "BR_Cracks", wrapMode = TextureWrapMode.Clamp };
            glassMat.SetTexture("_WipeTex", maskTex);
            glassMat.SetTexture("_CrackTex", crackTex);
            glassMat.SetTexture("_NoiseTex", CarDetailTextures.Noise);
            glassMat.SetVector("_ScreenO", new Vector4(a.scrO.x, a.scrO.y, a.scrO.z, 0f));
            glassMat.SetVector("_ScreenU", new Vector4(a.scrUp.x, a.scrUp.y, a.scrUp.z, a.hasScreen ? a.scrH : 0f));
            glassMat.SetVector("_ScreenN", new Vector4(a.scrN.x, a.scrN.y, a.scrN.z, a.scrHW));

            // wipers: tandem, both parked pointing left along the bottom edge
            r1 = Mathf.Min(a.scrH * 0.9f, a.scrHW * 0.95f);
            r0 = r1 * 0.33f;
            pivot[0] = new Vector3(a.scrHW * 0.02f, 0.035f, 0f);
            pivot[1] = new Vector3(a.scrHW * 0.66f, 0.035f, 0f);
            polR0 = new float[MW * MH]; polA0 = new float[MW * MH]; polR1 = new float[MW * MH]; polA1 = new float[MW * MH];
            for (int y = 0; y < MH; y++)
                for (int x = 0; x < MW; x++)
                {
                    float u = ((x + 0.5f) / MW - 0.5f) * 2f * a.scrHW, v = (y + 0.5f) / MH * a.scrH;
                    int i = y * MW + x;
                    Polar(u - pivot[0].x, v - pivot[0].y, out polR0[i], out polA0[i]);
                    Polar(u - pivot[1].x, v - pivot[1].y, out polR1[i], out polA1[i]);
                }
            if (a.hasScreen)
            {
                Color32 blade = LifeGeo.C(18, 18, 20), arm = LifeGeo.C(30, 30, 33);
                var g = new LifeGeo();
                g.Frustum(Vector3.zero, new Vector3(0f, 0.012f, 0f), 0.012f, 0.012f, 0.009f, 0.009f, 6, 0f, arm, arm, false, true);
                g.Box(new Vector3(0f, 0.016f, (r0 + r1) * 0.25f), new Vector3(0.006f, 0.004f, (r0 + r1) * 0.25f), arm);
                g.Box(new Vector3(0f, 0.008f, (r0 + r1) * 0.5f), new Vector3(0.008f, 0.006f, (r1 - r0) * 0.5f), blade);
                int tpl = d.batch.AddTemplate(g);
                wiper[0] = d.batch.AddPart(tpl);
                wiper[1] = d.batch.AddPart(tpl);
            }
            float cd;
            Color dc;
            cd = d.PaintDirt(out dc);
            for (int i = 0; i < dirt.Length; i++) dirt[i] = cd * 0.85f;
            lastCarDirt = cd;
            UploadMask();
            DrawCracks();
        }

        static void Polar(float x, float y, out float r, out float angDeg)
        {
            r = Mathf.Sqrt(x * x + y * y);
            // 0 deg = pointing left (-u), increasing towards up the glass
            angDeg = Mathf.Atan2(y, -x) * Mathf.Rad2Deg;
        }

        public override void Dispose()
        {
            if (glassR != null && savedMats != null) glassR.sharedMaterials = savedMats;
            if (glassMat != null) Object.Destroy(glassMat);
            if (maskTex != null) Object.Destroy(maskTex);
            if (crackTex != null) Object.Destroy(crackTex);
        }

        void UploadMask()
        {
            for (int i = 0; i < maskPx.Length; i++)
                maskPx[i] = new Color32((byte)(Mathf.Clamp01(dirt[i]) * 255f), (byte)(Mathf.Clamp01(fresh[i]) * 255f), 0, 255);
            maskTex.SetPixels32(maskPx);
            maskTex.Apply(false, false);
        }

        public override void Tick(CarDetails d, float dt)
        {
            if (glassMat == null) return;
            glassMat.SetMatrix(IdW2L, d.transform.worldToLocalMatrix);
            if (d.damage != null && d.damage.state != null && d.damage.state.cracks.Count != crackCount) DrawCracks();

            Color dc;
            float carDirt = d.PaintDirt(out dc);
            glassMat.SetFloat("_Dirt", carDirt);
            glassMat.SetColor("_DirtColor", dc);
            float rain = Atmosphere.RainIntensity;
            glassMat.SetFloat("_Rain", Mathf.Clamp01(rain * 1.2f));

            // wipers
            bool want = rain > 0.05f && !d.showroom;
            float period = Mathf.Lerp(2.2f, 1.1f, Mathf.Clamp01(rain));
            if (dt > 0f)
            {
                if (want || phase > 0f)
                {
                    phase += dt / period;
                    if (phase >= 1f)
                    {
                        // intermittent in light rain: park for a moment between sweeps
                        phase = want ? (rain < 0.35f ? -Mathf.Lerp(1.6f, 0.2f, rain / 0.35f) : 0f) : 0f;
                        if (!want) phase = 0f;
                    }
                }
                else if (phase < 0f) phase = Mathf.Min(0f, phase + dt);
                float ph = Mathf.Clamp01(phase);
                angle = SweepMax * 0.5f * (1f - Mathf.Cos(ph * Mathf.PI * 2f));
            }
            wiping = angle > 0.5f;
            sweptMin = Mathf.Min(sweptMin, Mathf.Min(prevAngle, angle));
            sweptMax = Mathf.Max(sweptMax, Mathf.Max(prevAngle, angle));
            prevAngle = angle;
            for (int w = 0; w < 2; w++)
            {
                if (wiper[w] < 0) continue;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dirS = -Vector3.right * Mathf.Cos(rad) + a.scrUp * Mathf.Sin(rad);
                Vector3 pos = a.Screen(pivot[w].x, pivot[w].y, 0.004f);
                d.batch.Set(wiper[w], Matrix4x4.TRS(pos, Quaternion.LookRotation(dirS, a.scrN), Vector3.one));
            }

            // dirt / wipe mask at ~12 Hz
            maskTimer -= dt;
            if (maskTimer > 0f) return;
            float step = 0.08f - maskTimer;
            maskTimer = 0.08f;
            bool changed = false;
            float target = carDirt * 0.85f;
            float rise = lastCarDirt >= 0f ? Mathf.Max(0f, carDirt - lastCarDirt) * 0.85f : 0f;
            lastCarDirt = carDirt;
            bool swept = sweptMax - sweptMin > 0.01f;
            float lo = sweptMin - 2f, hi = sweptMax + 2f;
            for (int i = 0; i < dirt.Length; i++)
            {
                float dv = dirt[i], fv = fresh[i];
                if (rise > 0f) dv = Mathf.Min(1f, dv + rise);
                if (dv > target + 0.02f) dv = Mathf.MoveTowards(dv, target, step * 0.5f);     // washed / cleaned
                if (swept)
                {
                    bool hit = (polR0[i] > r0 && polR0[i] < r1 && polA0[i] >= lo && polA0[i] <= hi) ||
                               (polR1[i] > r0 && polR1[i] < r1 && polA1[i] >= lo && polA1[i] <= hi);
                    if (hit) { dv *= 0.25f; fv = 1f; }
                }
                fv = Mathf.Max(0f, fv - step * 0.7f);
                if (Mathf.Abs(dv - dirt[i]) > 1e-4f || Mathf.Abs(fv - fresh[i]) > 1e-4f) changed = true;
                dirt[i] = dv;
                fresh[i] = fv;
            }
            sweptMin = sweptMax = angle;
            if (changed) UploadMask();
        }

        void DrawCracks()
        {
            var st = det != null && det.damage != null ? det.damage.state : null;
            crackCount = st != null ? st.cracks.Count : 0;
            var px = new Color32[CW * CH];
            var c = new PixelCanvasView(px, CW, CH);
            if (st != null)
                foreach (var cr in st.cracks) PaintCrack(c, cr);
            crackTex.SetPixels32(px);
            crackTex.Apply(true, false);
        }

        void PaintCrack(PixelCanvasView c, CarDamage.Crack cr)
        {
            var rnd = new System.Random(cr.seed);
            float sx = CW / (2f * a.scrHW), sy = CH / Mathf.Max(0.1f, a.scrH);
            float cx = (cr.p.x + a.scrHW) * sx, cy = cr.p.y * sy;
            c.Disc(cx, cy, 2.2f, 255);
            int rays = 7 + (int)(cr.size * 7f);
            var ends = new Vector2[rays];
            float baseA = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int k = 0; k < rays; k++)
            {
                float ang = baseA + k * Mathf.PI * 2f / rays + ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                float len = (0.08f + 0.27f * (float)rnd.NextDouble()) * (0.5f + cr.size);
                Vector2 p = new Vector2(cr.p.x, cr.p.y);
                int segs = 6;
                for (int s = 0; s < segs; s++)
                {
                    ang += ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                    Vector2 q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / segs);
                    c.Line((p.x + a.scrHW) * sx, p.y * sy, (q.x + a.scrHW) * sx, q.y * sy, (byte)(230 - s * 18));
                    if (rnd.NextDouble() < 0.18)
                    {
                        float ba = ang + (rnd.NextDouble() < 0.5 ? 0.7f : -0.7f);
                        Vector2 b = q + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * (len / segs * 1.5f);
                        c.Line((q.x + a.scrHW) * sx, q.y * sy, (b.x + a.scrHW) * sx, b.y * sy, 150);
                    }
                    p = q;
                    if (s == 1) ends[k] = p;
                }
            }
            // spider-web rings between neighbouring rays
            for (int k = 0; k < rays; k++)
            {
                if (rnd.NextDouble() > 0.7) continue;
                Vector2 p = ends[k], q = ends[(k + 1) % rays];
                c.Line((p.x + a.scrHW) * sx, p.y * sy, (q.x + a.scrHW) * sx, q.y * sy, 170);
            }
        }

        /// <summary>Single-channel line drawing into the red channel of an RGBA pixel array.</summary>
        sealed class PixelCanvasView
        {
            readonly Color32[] px;
            readonly int w, h;
            public PixelCanvasView(Color32[] px, int w, int h) { this.px = px; this.w = w; this.h = h; }

            void Put(int x, int y, float v)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                byte b = (byte)Mathf.Clamp(v, 0f, 255f);
                if (b > px[i].r) px[i] = new Color32(b, b, b, 255);
            }

            public void Disc(float cx, float cy, float r, byte v)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                    for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d <= r) Put(x, y, v * Mathf.Clamp01(r - d + 0.5f));
                    }
            }

            public void Line(float x0, float y0, float x1, float y1, byte v)
            {
                float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int n = Mathf.Max(1, Mathf.CeilToInt(len * 1.5f));
                for (int i = 0; i <= n; i++)
                {
                    float t = (float)i / n;
                    float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    Put(ix, iy, v);
                    Put(ix + 1, iy, v * 0.35f);
                    Put(ix, iy + 1, v * 0.35f);
                }
            }
        }
    }
}

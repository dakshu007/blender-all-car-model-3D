// CarCockpit.cs - working instruments: speedometer + rev-counter needles behind the steering wheel (dial
// faces are decals from CarDecals), a backlit rally trip computer on the co-driver's side (clock + trip
// kilometres, pixel-font LCD texture refreshed 4x a second), a rear-view mirror and a red Chinese lucky
// knot hanging from it that swings with the g-forces (a point mass on a string in car space).
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarCockpit : CarDetailSystem
    {
        /// <summary>Shared cockpit layout (car space) used by the needles and the dial-face decals.</summary>
        internal struct Layout
        {
            public Vector3 eye, coEye;
            public Vector3 dialSpeed, dialRev, dialN, dialUp;
            public float dialR;
            public Vector3 pod;
            public Vector3 lcd, lcdN, lcdUp;
            public Vector2 lcdSize;
            public Vector3 mirror;
        }

        internal static Layout GetLayout(CarAnatomy a)
        {
            var L = new Layout();
            L.eye = new Vector3(a.wheelC.x, a.wheelC.y + 0.30f, a.wheelC.z - 0.40f);
            L.coEye = new Vector3(a.hipR.x, a.hipR.y + 0.62f, a.hipR.z + 0.08f);
            L.pod = a.wheelC + new Vector3(0f, 0.062f, 0.125f);
            Vector3 n = (L.eye - L.pod).normalized;
            L.dialN = n;
            L.dialUp = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(L.dialUp, -n).normalized;
            L.dialSpeed = L.pod - right * 0.058f;
            L.dialRev = L.pod + right * 0.058f;
            L.dialR = 0.034f;
            L.lcd = new Vector3(a.hipR.x - 0.05f, a.wheelC.y + 0.115f, a.wheelC.z + 0.10f);
            L.lcdN = (L.coEye - L.lcd).normalized;
            L.lcdUp = Vector3.ProjectOnPlane(Vector3.up, L.lcdN).normalized;
            L.lcdSize = new Vector2(0.11f, 0.05f);
            L.mirror = a.Screen(0f, a.scrH - 0.07f, -0.05f);
            return L;
        }

        Layout L;
        int needleS = -1, needleR = -1, knot = -1;
        float speedVal, revVal;
        Vector3 knotAnchor, bob, bobV;
        float twist, twistV;
        const float KnotLen = 0.14f;
        // trip computer
        GameObject lcdGo;
        Mesh lcdMesh;
        Material lcdMat;
        Texture2D lcdTex;
        PixelCanvas lcdCanvas;
        float lcdTimer, tripKm;
        string lastLcd = "";

        public override void Build(CarDetails d)
        {
            var a = d.a;
            var batch = d.batch;
            L = GetLayout(a);
            Color32 dark = LifeGeo.C(28, 28, 32), darker = LifeGeo.C(16, 16, 18);

            // instrument pod behind the dials
            var pod = new LifeGeo();
            pod.Box(Vector3.zero, new Vector3(0.125f, 0.048f, 0.02f), dark);
            int podPart = batch.AddPart(batch.AddTemplate(pod));
            batch.Set(podPart, Matrix4x4.TRS(L.pod - L.dialN * 0.022f, Quaternion.LookRotation(-L.dialN, L.dialUp), Vector3.one));

            // needles (dial frame: X right, Y up, Z into the dial)
            var ng = new LifeGeo();
            Color32 red = LifeGeo.C(255, 90, 40);
            ng.Tri2(new Vector3(-0.0028f, -0.004f, -0.004f), new Vector3(0.0028f, -0.004f, -0.004f), new Vector3(0f, 0.029f, -0.004f), red, red, red);
            ng.Ellipsoid(new Vector3(0f, 0f, -0.004f), new Vector3(0.0045f, 0.0045f, 0.002f), 6, 2, darker, darker);
            int nt = batch.AddTemplate(ng);
            needleS = batch.AddPart(nt);
            needleR = batch.AddPart(nt);

            // trip computer housing
            var hb = new LifeGeo();
            hb.Box(new Vector3(0f, 0f, 0.012f), new Vector3(L.lcdSize.x * 0.5f + 0.012f, L.lcdSize.y * 0.5f + 0.01f, 0.012f), dark);
            int hp = batch.AddPart(batch.AddTemplate(hb));
            batch.Set(hp, Matrix4x4.TRS(L.lcd, Quaternion.LookRotation(-L.lcdN, L.lcdUp), Vector3.one));

            // rear-view mirror
            var mg = new LifeGeo();
            mg.Box(Vector3.zero, new Vector3(0.105f, 0.03f, 0.012f), dark);
            mg.Box(new Vector3(0f, 0f, -0.0125f), new Vector3(0.095f, 0.022f, 0.001f), LifeGeo.C(150, 165, 180));
            mg.Frustum(new Vector3(0f, 0.025f, 0.01f), new Vector3(0f, 0.06f, 0.03f), 0.008f, 0.008f, 0.008f, 0.008f, 5, 0f, dark, dark, false, false);
            int mp = batch.AddPart(batch.AddTemplate(mg));
            Vector3 toEye = (L.eye - L.mirror).normalized;
            batch.Set(mp, Matrix4x4.TRS(L.mirror, Quaternion.LookRotation(-toEye, Vector3.up), Vector3.one));

            // lucky knot (red Chinese knot with gold bead and tassel)
            if (d.quality >= 1)
            {
                knotAnchor = L.mirror + Vector3.down * 0.03f;
                var kg = new LifeGeo();
                Color32 kr = LifeGeo.C(205, 22, 30), kd = LifeGeo.C(150, 12, 20), gold = LifeGeo.C(235, 180, 60);
                kg.Frustum(Vector3.zero, new Vector3(0f, -0.105f, 0f), 0.0022f, 0.0022f, 0.0022f, 0.0022f, 4, 0f, kr, kr, false, false);
                kg.Ellipsoid(new Vector3(0f, -0.103f, 0f), new Vector3(0.008f, 0.008f, 0.008f), 6, 3, gold, gold);
                kg.Ellipsoid(new Vector3(0f, -0.14f, 0f), new Vector3(0.032f, 0.032f, 0.009f), 4, 2, kr, kd);
                kg.Ellipsoid(new Vector3(0f, -0.14f, 0f), new Vector3(0.018f, 0.018f, 0.011f), 4, 2, kd, kr);
                kg.Ellipsoid(new Vector3(0f, -0.177f, 0f), new Vector3(0.007f, 0.007f, 0.007f), 6, 3, gold, gold);
                kg.Frustum(new Vector3(0f, -0.18f, 0f), new Vector3(0f, -0.255f, 0f), 0.005f, 0.005f, 0.016f, 0.016f, 6, 0f, kr, kd, false, true);
                knot = batch.AddPart(batch.AddTemplate(kg));
                bob = knotAnchor + Vector3.down * KnotLen;
            }

            BuildLcd(d);
        }

        void BuildLcd(CarDetails d)
        {
            lcdCanvas = new PixelCanvas(64, 32);
            lcdTex = lcdCanvas.ToTexture("BR_TripLcd", false);
            lcdTex.filterMode = FilterMode.Point;
            lcdMat = new Material(MaterialLibrary.FindShader("BlossomRally/Decal")) { name = "BR_TripLcd" };
            lcdMat.SetTexture("_MainTex", lcdTex);
            lcdMat.SetFloat("_Glow", 1.4f);
            lcdMat.SetFloat("_Glossiness", 0.8f);
            var qb = new DecalMeshBuilder();
            qb.Quad(L.lcd + L.lcdN * 0.002f, L.lcdN, L.lcdUp, L.lcdSize.x, L.lcdSize.y, new Vector4(0f, 0f, 1f, 1f), new Color32(255, 255, 255, 255));
            lcdMesh = qb.ToMesh("BR_TripLcd");
            lcdGo = new GameObject("TripComputer");
            lcdGo.layer = CarExtras.CarLayer;
            lcdGo.transform.SetParent(d.transform, false);
            lcdGo.AddComponent<MeshFilter>().sharedMesh = lcdMesh;
            var r = lcdGo.AddComponent<MeshRenderer>();
            r.sharedMaterial = lcdMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            DrawLcd();
        }

        void DrawLcd()
        {
            var now = System.DateTime.Now;
            string l1 = now.ToString("HH:mm");
            string l2 = tripKm.ToString("000.0");
            string key = l1 + l2;
            if (key == lastLcd) return;
            lastLcd = key;
            var c = lcdCanvas;
            c.Clear(new Color32(22, 14, 6, 255));
            Color32 amber = new Color32(255, 170, 40, 255), dim = new Color32(70, 42, 12, 255);
            c.Text("88:88", 2, 17, 2, dim);
            c.Text(l1, 2, 17, 2, amber);
            c.Text("888.8", 2, 2, 2, dim);
            c.Text(l2, 2, 2, 2, amber);
            c.Text("KM", 60 - PixelCanvas.TextWidth("KM", 1) + 2, 22, 1, amber);
            c.Upload(lcdTex, false);
        }

        public override void Dispose()
        {
            if (lcdGo != null) Object.Destroy(lcdGo);
            if (lcdMesh != null) Object.Destroy(lcdMesh);
            if (lcdMat != null) Object.Destroy(lcdMat);
            if (lcdTex != null) Object.Destroy(lcdTex);
        }

        public override void Tick(CarDetails d, float dt)
        {
            var car = d.car;
            var batch = d.batch;
            float k = dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f;
            speedVal = Mathf.Lerp(speedVal, Mathf.Clamp01(Mathf.Abs(car.SpeedKmh) / 220f), k);
            float rpm = d.showroom ? 900f + Mathf.Sin(Time.time * 3f) * 40f : car.EngineRpm;
            revVal = Mathf.Lerp(revVal, Mathf.Clamp01(rpm / 8000f), k);
            Quaternion q = Quaternion.LookRotation(-L.dialN, L.dialUp);
            batch.Set(needleS, Matrix4x4.TRS(L.dialSpeed, q * Quaternion.AngleAxis(-(-135f + 270f * speedVal), Vector3.forward), Vector3.one));
            batch.Set(needleR, Matrix4x4.TRS(L.dialRev, q * Quaternion.AngleAxis(-(-135f + 270f * revVal), Vector3.forward), Vector3.one));

            if (!d.showroom) tripKm += d.speedMs * dt / 1000f;
            if (tripKm >= 1000f) tripKm -= 1000f;
            lcdTimer -= dt;
            if (lcdTimer <= 0f) { lcdTimer = 0.25f; DrawLcd(); }

            if (knot >= 0)
            {
                if (dt > 0f)
                {
                    float h = Mathf.Min(dt, 0.033f);
                    Vector3 g = d.showroom ? Vector3.down * 9.81f : d.FeltGravity();
                    bobV += g * h;
                    bobV *= Mathf.Exp(-h * 0.8f);
                    bob += bobV * h;
                    Vector3 rel = bob - knotAnchor;
                    if (rel.sqrMagnitude < 1e-6f) rel = Vector3.down;
                    Vector3 dir = rel.normalized;
                    // the knot cannot swing through the windscreen
                    float into = Vector3.Dot(dir, d.a.scrN);
                    if (into > 0.05f) { dir = (dir - d.a.scrN * (into - 0.05f)).normalized; bobV -= d.a.scrN * Mathf.Max(0f, Vector3.Dot(bobV, d.a.scrN)); }
                    bob = knotAnchor + dir * KnotLen;
                    bobV -= dir * Vector3.Dot(bobV, dir);
                    twistV += (-twist * 6f - twistV * 0.6f + bobV.x * 40f) * h;
                    twist += twistV * h;
                }
                Vector3 dd = (bob - knotAnchor).normalized;
                Quaternion r = Quaternion.FromToRotation(Vector3.down, dd) * Quaternion.AngleAxis(twist * Mathf.Rad2Deg, Vector3.up);
                batch.Set(knot, Matrix4x4.TRS(knotAnchor, r, Vector3.one));
            }
        }
    }
}

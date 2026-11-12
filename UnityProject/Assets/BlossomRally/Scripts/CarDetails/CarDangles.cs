// CarDangles.cs - things that move with the air and the bumps:
//  * mud flaps: the model's merged <ID>_MudFlaps mesh is split per wheel (one LifeBatch with the car's accent
//    material, 1 draw call) and each flap swings about its top edge - blown back with speed, kicked by the
//    suspension and by braking / acceleration (spring-damper per flap)
//  * a whip antenna on the roof (5 segments) that bends back with speed and wobbles with the g-forces
//  * Group B cars: a spinning turbine roof vent driven by the airflow
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarDangles : CarDetailSystem
    {
        // mud flaps
        LifeBatch flapBatch;
        Renderer flapSource;
        readonly int[] flapPart = new int[4];
        readonly Vector3[] flapPivot = new Vector3[4];
        readonly int[] flapWheel = new int[4];
        readonly float[] flapA = new float[4], flapV = new float[4], prevSpring = new float[4];
        int flaps;

        // antenna
        const int Segs = 5;
        readonly int[] seg = new int[Segs];
        float segLen;
        Vector3 antBase;
        Vector2 bend, bendV;
        float gustT;

        // roof vent
        int rotor = -1;
        Vector3 ventPos;
        float ventAngle;

        public override void Build(CarDetails d)
        {
            var a = d.a;
            BuildFlaps(d);

            // antenna on the roof, rear right
            var batch = d.batch;
            float ax = a.roofHalfW * 0.55f, az = Mathf.Lerp(a.roofMinZ, a.roofMaxZ, 0.12f);
            antBase = new Vector3(ax, a.SurfaceY(ax, az, 0.12f) - 0.004f, az);
            var mount = new LifeGeo();
            mount.Frustum(Vector3.zero, new Vector3(0f, 0.025f, 0f), 0.014f, 0.014f, 0.007f, 0.007f, 6, 0f, LifeGeo.C(25, 25, 28), LifeGeo.C(35, 35, 38), false, true);
            int mp = batch.AddPart(batch.AddTemplate(mount));
            batch.Set(mp, Matrix4x4.Translate(antBase));
            antBase += Vector3.up * 0.024f;
            segLen = 0.72f / Segs;
            for (int i = 0; i < Segs; i++)
            {
                var g = new LifeGeo();
                float r0 = Mathf.Lerp(0.0042f, 0.0016f, (float)i / Segs), r1 = Mathf.Lerp(0.0042f, 0.0016f, (float)(i + 1) / Segs);
                Color32 c = LifeGeo.C(20, 20, 22);
                g.Frustum(Vector3.zero, new Vector3(0f, segLen, 0f), r0, r0, r1, r1, 4, 0f, c, c, false, i == Segs - 1);
                if (i == Segs - 1) g.Ellipsoid(new Vector3(0f, segLen, 0f), new Vector3(0.005f, 0.006f, 0.005f), 5, 3, c, c);
                seg[i] = batch.AddPart(batch.AddTemplate(g));
            }

            // spinning roof vent on the Group B cars (not on open-frame cars: no roof)
            if (a.groupB && a.glass != null)
            {
                float vz = Mathf.Lerp(a.roofMinZ, a.roofMaxZ, 0.6f);
                ventPos = new Vector3(0f, a.SurfaceY(0f, vz, 0.1f) - 0.006f, vz);
                Color32 grey = LifeGeo.C(70, 72, 76), dark = LifeGeo.C(36, 36, 40), lite = LifeGeo.C(170, 172, 176);
                var bg = new LifeGeo();
                bg.Frustum(Vector3.zero, new Vector3(0f, 0.03f, 0f), 0.085f, 0.085f, 0.06f, 0.06f, 10, 0f, dark, grey, false, true);
                int bp = batch.AddPart(batch.AddTemplate(bg));
                batch.Set(bp, Matrix4x4.Translate(ventPos));
                var rg = new LifeGeo();
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI * 2f / 8f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 tan = new Vector3(-dir.z, 0f, dir.x);
                    Vector3 p0 = dir * 0.05f + Vector3.up * 0.005f, p1 = dir * 0.058f + tan * 0.018f + Vector3.up * 0.055f;
                    rg.Frustum(p0, p1, 0.012f, 0.003f, 0.012f, 0.003f, 4, 0f, lite, grey, true, true);
                }
                rg.Frustum(new Vector3(0f, 0.055f, 0f), new Vector3(0f, 0.07f, 0f), 0.068f, 0.068f, 0.03f, 0.03f, 10, 0f, grey, lite, true, true);
                rotor = batch.AddPart(batch.AddTemplate(rg));
                ventPos += Vector3.up * 0.03f;
            }
        }

        void BuildFlaps(CarDetails d)
        {
            var a = d.a;
            var mf = a.mudFlaps;
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;
            flapSource = mf.GetComponent<Renderer>();
            if (flapSource == null || flapSource.sharedMaterial == null) return;
            var mesh = mf.sharedMesh;
            var m = a.ToCar(mf.transform);
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            var cv = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) cv[i] = m.MultiplyPoint3x4(v[i]);
            var wheels = d.car.wheels;
            int wc = Mathf.Min(4, wheels.Length);
            if (wc == 0) return;
            var geos = new LifeGeo[wc];
            var top = new float[wc];
            var cx = new Vector3[wc];
            var cnt = new int[wc];
            for (int w = 0; w < wc; w++) { geos[w] = new LifeGeo(); top[w] = -9f; }
            var owner = new int[t.Length / 3];
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 c = (cv[t[i]] + cv[t[i + 1]] + cv[t[i + 2]]) / 3f;
                int best = 0;
                float bd = float.MaxValue;
                for (int w = 0; w < wc; w++)
                {
                    Vector3 wl = wheels[w].local;
                    float dd = (c.x - wl.x) * (c.x - wl.x) + (c.z - wl.z) * (c.z - wl.z);
                    if (dd < bd) { bd = dd; best = w; }
                }
                owner[i / 3] = best;
                for (int k = 0; k < 3; k++) { top[best] = Mathf.Max(top[best], cv[t[i + k]].y); cx[best] += cv[t[i + k]]; cnt[best]++; }
            }
            Color32 white = new Color32(255, 255, 255, 0);
            for (int w = 0; w < wc; w++)
            {
                if (cnt[w] == 0) continue;
                Vector3 c = cx[w] / cnt[w];
                flapPivot[w] = new Vector3(c.x, top[w], c.z);
            }
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int w = owner[i / 3];
                var g = geos[w];
                for (int k = 0; k < 3; k++)
                {
                    int vi = t[i + k];
                    g.t.Add(g.v.Count);
                    g.v.Add(cv[vi] - flapPivot[w]);
                    g.n.Add(n != null && n.Length == v.Length ? m.MultiplyVector(n[vi]).normalized : Vector3.back);
                    g.c.Add(white);
                }
            }
            flapBatch = new LifeBatch();
            flaps = 0;
            for (int w = 0; w < wc; w++)
            {
                if (cnt[w] == 0) continue;
                flapPart[flaps] = flapBatch.AddPart(flapBatch.AddTemplate(geos[w]));
                flapPivot[flaps] = flapPivot[w];
                flapWheel[flaps] = w;
                flaps++;
            }
            if (flaps == 0) { flapBatch = null; return; }
            flapBatch.Finish("MudFlapsAnimated", flapSource.sharedMaterial, d.transform, CarExtras.CarLayer, true);
            for (int i = 0; i < flaps; i++) flapBatch.Set(flapPart[i], Matrix4x4.Translate(flapPivot[i]));
            flapBatch.Upload();
            flapSource.enabled = false;
        }

        public override void Dispose()
        {
            if (flapSource != null) flapSource.enabled = true;
            if (flapBatch != null && flapBatch.Renderer != null)
            {
                Object.Destroy(flapBatch.Mesh);
                Object.Destroy(flapBatch.Renderer.gameObject);
            }
            flapBatch = null;
        }

        public override void Tick(CarDetails d, float dt)
        {
            var car = d.car;
            float h = Mathf.Min(dt, 0.04f);
            float vz = d.velLocal.z;
            Vector3 acc = d.showroom ? Vector3.zero : d.accLocal;

            // mud flaps
            if (flapBatch != null)
            {
                float blow = Mathf.Clamp(vz * Mathf.Abs(vz) * 0.00035f, -0.3f, 0.95f);
                for (int i = 0; i < flaps; i++)
                {
                    var w = car.wheels[flapWheel[i]];
                    float sv = h > 0f ? (w.springLength - prevSpring[i]) / h : 0f;
                    prevSpring[i] = w.springLength;
                    float target = blow + Mathf.Sin(Time.time * (9f + i) + i * 1.7f) * 0.02f * Mathf.Clamp01(d.speedMs / 15f);
                    float f = -(flapA[i] - target) * 160f - flapV[i] * 5f + acc.z * 1.6f + Mathf.Clamp(sv, -3f, 3f) * 30f;
                    if (h > 0f)
                    {
                        flapV[i] += f * h;
                        flapA[i] = Mathf.Clamp(flapA[i] + flapV[i] * h, -0.5f, 1.15f);
                    }
                    flapBatch.Set(flapPart[i], Matrix4x4.TRS(flapPivot[i], Quaternion.AngleAxis(flapA[i] * Mathf.Rad2Deg, Vector3.right), Vector3.one));
                }
                flapBatch.Upload();
            }

            // antenna: bending spring (tip deflection in car x / z), dragged back by the airflow
            if (h > 0f)
            {
                gustT += h;
                float drag = Mathf.Clamp(vz * Mathf.Abs(vz) * 0.0006f, -0.5f, 0.95f);
                Vector2 rest = new Vector2(Mathf.Sin(gustT * 7.3f) * 0.02f * Mathf.Clamp01(d.speedMs / 20f), -drag);
                Vector2 force = new Vector2(-acc.x, -acc.z) * 0.05f;
                const float w0 = 9f, z0 = 0.07f;
                bendV += ((rest - bend) * (w0 * w0) - bendV * (2f * z0 * w0) + force * (w0 * w0) * 0.1f) * h;
                bend += bendV * h;
                if (bend.magnitude > 1.2f) bend = bend.normalized * 1.2f;
            }
            float beta = bend.magnitude * Mathf.Rad2Deg;
            Vector3 dirH = beta > 0.01f ? new Vector3(bend.x, 0f, bend.y) / bend.magnitude : Vector3.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, dirH);
            Vector3 p = antBase;
            for (int i = 0; i < Segs; i++)
            {
                float ang = beta * (i + 1) / Segs * 0.9f;
                Quaternion r = Quaternion.AngleAxis(ang, axis);
                d.batch.Set(seg[i], Matrix4x4.TRS(p, r, Vector3.one));
                p += r * Vector3.up * segLen;
            }

            // roof vent
            if (rotor >= 0)
            {
                ventAngle = Mathf.Repeat(ventAngle + (d.speedMs * 1.8f + 0.6f) * Mathf.Rad2Deg * dt, 360f);
                d.batch.Set(rotor, Matrix4x4.TRS(ventPos, Quaternion.AngleAxis(ventAngle, Vector3.up), Vector3.one));
            }
        }
    }
}

// CarCrew.cs - procedural low-poly driver (left seat) and co-driver (right seat) in team-coloured race
// suits and helmets. The driver's gloves stay on the steering wheel rim, which turns with car.SteerAngle
// (the model's <ID>_SteeringWheel is rotated about its column too); the right hand reaches for the gear
// lever on every gear change. Torsos and heads lean with the felt lateral / longitudinal g (spring-damped,
// so they lag and wobble); the driver looks into corners; the co-driver reads the pace-note book and points
// into tight corners. People are scaled / reclined to the car's head room, and the driver's head is hidden
// when the camera sits inside it (cockpit view). All parts go into the car's shared LifeBatch.
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarCrew : CarDetailSystem
    {
        const float WheelRatio = 5.5f;      // steering wheel turns per road-wheel degree

        sealed class Person
        {
            public bool driver;
            public Vector3 hip;
            public float s, armS, lean;
            public int legs, torso, head, upL, foL, upR, foR, book = -1;
            public float L1, L2;
            public Vector2 tilt, tiltV, htilt, htiltV;
            public float yaw, pitch;
            public Vector3 headWorld;
        }

        Person drv, co;
        int lever = -1, leverBase = -1;
        Vector3 leverPos;
        float leverPitch, leverSide;
        int lastGear = 1;
        float shiftT = 10f;
        int fromGear = 1;
        float pointW, pointSide = 1f, pointTimer;
        Transform steering;
        Vector3 steerRestPos;
        Quaternion steerRestRot;
        float idleT;
        CarAnatomy a;

        public override void Build(CarDetails d)
        {
            a = d.a;
            var batch = d.batch;
            drv = MakePerson(d, true);
            if (d.quality >= 1) co = MakePerson(d, false);

            // gear lever on the tunnel, right of the driver
            leverPos = new Vector3(Mathf.Lerp(a.hipL.x, a.hipR.x, 0.42f), a.seatTopY + 0.06f, drv.hip.z + 0.42f * drv.s);
            var gb = new LifeGeo();
            gb.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.05f, 0.03f, 0.06f), LifeGeo.C(30, 30, 34));
            gb.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, 0f), 0.045f, 0.045f, 0.02f, 0.02f, 6, 0f, LifeGeo.C(22, 22, 24), LifeGeo.C(30, 30, 32), false, true);
            leverBase = batch.AddPart(batch.AddTemplate(gb));
            batch.Set(leverBase, Matrix4x4.Translate(leverPos));
            var lg = new LifeGeo();
            lg.Frustum(Vector3.zero, new Vector3(0f, 0.17f, 0f), 0.009f, 0.009f, 0.008f, 0.008f, 5, 0f, LifeGeo.C(150, 150, 155), LifeGeo.C(170, 170, 175), false, false);
            lg.Ellipsoid(new Vector3(0f, 0.18f, 0f), new Vector3(0.024f, 0.024f, 0.024f), 6, 4, LifeGeo.C(40, 40, 44), LifeGeo.C(20, 20, 22));
            lever = batch.AddPart(batch.AddTemplate(lg));

            steering = a.steering;
            if (steering != null)
            {
                steerRestPos = a.root.InverseTransformPoint(steering.position);
                steerRestRot = Quaternion.Inverse(a.root.rotation) * steering.rotation;
            }
            lastGear = d.car.Gear;
        }

        public override void Dispose()
        {
            if (steering != null && a != null && a.root != null)
                steering.SetPositionAndRotation(a.root.TransformPoint(steerRestPos), a.root.rotation * steerRestRot);
        }

        Person MakePerson(CarDetails d, bool driver)
        {
            var p = new Person { driver = driver };
            Vector3 seatHip = driver ? a.hipL : a.hipR;
            float roof = driver ? a.roofL : a.roofR;
            float avail = roof - a.seatTopY;
            float lean = 16f;
            float need = 0.09f + 0.83f * Mathf.Cos(lean * Mathf.Deg2Rad);
            p.s = Mathf.Clamp(avail / need, 0.74f, 1f);
            if (p.s * need > avail)
                lean = Mathf.Acos(Mathf.Clamp((avail / p.s - 0.09f) / 0.83f, Mathf.Cos(27f * Mathf.Deg2Rad), 1f)) * Mathf.Rad2Deg;
            p.lean = lean;
            p.armS = Mathf.Lerp(p.s, 1f, 0.5f);
            p.L1 = 0.29f * p.armS;
            p.L2 = 0.27f * p.armS + 0.05f * p.armS;
            p.hip = new Vector3(seatHip.x, a.seatTopY + 0.09f * p.s, seatHip.z);
            if (driver)
            {
                // slide forward on the seat until the arms comfortably reach the 9-and-3 grips
                Vector3 sh = ShoulderOffset(p, -1f, p.lean, 0f, 0f);
                Vector3 grip = a.Rim(-90f) + a.wheelAxis * 0.02f;
                float target = (p.L1 + p.L2) * 0.88f;
                float dx = grip.x - (p.hip.x + sh.x), dy = grip.y - (p.hip.y + sh.y);
                float dz2 = target * target - dx * dx - dy * dy;
                if (dz2 > 0f)
                {
                    float hipZ = grip.z - Mathf.Sqrt(dz2) - sh.z;
                    p.hip.z = Mathf.Clamp(Mathf.Max(p.hip.z, hipZ), seatHip.z - 0.1f, seatHip.z + 0.22f);
                }
            }

            Color32 suit = CarDetails.C(d.suit), suitDark = CarDetails.C(d.suit, 0.72f), acc = CarDetails.C(d.accent);
            Color32 helm = CarDetails.C(d.helmet), dark = LifeGeo.C(26, 26, 30), visor = LifeGeo.C(18, 22, 34);
            Color32 glove = driver ? dark : CarDetails.C(d.accent, 0.8f);
            float s = p.s;
            var batch = d.batch;

            // legs (hip space)
            var lg = new LifeGeo();
            lg.Box(new Vector3(0f, 0f, -0.02f) * s, new Vector3(0.16f, 0.08f, 0.11f) * s, suitDark);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 h0 = new Vector3(k * 0.09f, 0f, 0.02f) * s, knee = new Vector3(k * 0.11f, 0.10f, 0.38f) * s;
                Vector3 ank = new Vector3(k * 0.12f, -0.15f, 0.57f) * s;
                lg.Frustum(h0, knee, 0.075f * s, 0.075f * s, 0.06f * s, 0.06f * s, 6, 0f, suit, suit, false, true);
                lg.Frustum(knee, ank, 0.055f * s, 0.055f * s, 0.045f * s, 0.045f * s, 6, 0f, suit, suitDark, true, false);
                lg.Box(ank + new Vector3(0f, -0.03f, 0.05f) * s, new Vector3(0.045f, 0.04f, 0.09f) * s, dark);
            }
            p.legs = batch.AddPart(batch.AddTemplate(lg));

            // torso (hip space, upright; lean applied when posing)
            var tg = new LifeGeo();
            tg.Paint = c => (c.y > 0.27f * s && c.y < 0.33f * s) ? acc : suit;
            tg.Frustum(new Vector3(0f, 0.04f, -0.03f) * s, new Vector3(0f, 0.46f, -0.03f) * s, 0.15f * s, 0.10f * s, 0.19f * s, 0.11f * s, 8, 0f, suit, suit, true, true);
            tg.Paint = null;
            for (int k = -1; k <= 1; k += 2)   // harness straps
                tg.Frustum(new Vector3(k * 0.07f, 0.45f, 0.075f) * s, new Vector3(k * 0.05f, 0.12f, 0.10f) * s, 0.022f * s, 0.006f * s, 0.022f * s, 0.006f * s, 4, Mathf.PI * 0.25f, dark, dark, false, false);
            tg.Frustum(new Vector3(0f, 0.45f, -0.03f) * s, new Vector3(0f, 0.54f, -0.015f) * s, 0.05f * s, 0.05f * s, 0.045f * s, 0.045f * s, 6, 0f, dark, dark, false, true);
            p.torso = batch.AddPart(batch.AddTemplate(tg));

            // helmet (neck space)
            var hg = new LifeGeo();
            hg.Paint = c =>
            {
                if (c.z > 0.075f * s && c.y > 0.07f * s && c.y < 0.165f * s && Mathf.Abs(c.x) < 0.11f * s) return visor;
                if (Mathf.Abs(c.x) < 0.03f * s && c.y > 0.15f * s) return acc;
                if (c.y < 0.045f * s) return acc;
                return helm;
            };
            hg.Ellipsoid(new Vector3(0f, 0.12f, 0.015f) * s, new Vector3(0.13f, 0.14f, 0.15f) * s, 10, 7, helm, helm);
            p.head = batch.AddPart(batch.AddTemplate(hg));

            // arms (along +Z)
            for (int k = 0; k < 2; k++)
            {
                var ug = new LifeGeo();
                ug.Frustum(Vector3.zero, new Vector3(0f, 0f, p.L1), 0.055f * p.armS, 0.055f * p.armS, 0.045f * p.armS, 0.045f * p.armS, 6, 0f, suit, suit, true, true);
                int up = batch.AddPart(batch.AddTemplate(ug));
                var fg = new LifeGeo();
                float l2 = p.L2 - 0.05f * p.armS;
                fg.Frustum(Vector3.zero, new Vector3(0f, 0f, l2), 0.045f * p.armS, 0.045f * p.armS, 0.037f * p.armS, 0.037f * p.armS, 6, 0f, suit, acc, true, false);
                fg.Box(new Vector3(0f, 0f, l2 + 0.03f * p.armS), new Vector3(0.032f, 0.024f, 0.048f) * p.armS, glove);
                int fo = batch.AddPart(batch.AddTemplate(fg));
                if (k == 0) { p.upL = up; p.foL = fo; } else { p.upR = up; p.foR = fo; }
            }

            if (!driver)
            {
                var bg = new LifeGeo();
                bg.Box(Vector3.zero, new Vector3(0.105f, 0.008f, 0.075f), CarDetails.C(d.accent, 0.5f));
                bg.Box(new Vector3(0f, 0.01f, 0f), new Vector3(0.098f, 0.004f, 0.07f), LifeGeo.C(244, 240, 228));
                bg.Box(new Vector3(-0.002f, 0.0145f, 0.03f), new Vector3(0.08f, 0.0005f, 0.003f), LifeGeo.C(60, 60, 70));
                bg.Box(new Vector3(-0.012f, 0.0145f, 0.0f), new Vector3(0.07f, 0.0005f, 0.003f), LifeGeo.C(60, 60, 70));
                bg.Box(new Vector3(0.004f, 0.0145f, -0.03f), new Vector3(0.076f, 0.0005f, 0.003f), LifeGeo.C(190, 40, 50));
                p.book = batch.AddPart(batch.AddTemplate(bg));
            }
            return p;
        }

        static Vector3 ShoulderOffset(Person p, float side, float lean, float pitch, float roll)
        {
            return TorsoRot(lean, pitch, roll) * (new Vector3(side * 0.2f, 0.43f, -0.03f) * p.s);
        }

        static Quaternion TorsoRot(float lean, float pitch, float roll)
        {
            return Quaternion.AngleAxis(-lean + pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward);
        }

        static void Spring(ref Vector2 x, ref Vector2 v, Vector2 target, float w, float z, float dt)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.05f);
            Vector2 acc = (target - x) * (w * w) - v * (2f * z * w);
            v += acc * dt;
            x += v * dt;
        }

        public override void Tick(CarDetails d, float dt)
        {
            var car = d.car;
            var batch = d.batch;
            idleT += dt;
            Vector3 felt = d.FeltGravity();
            Vector2 g = new Vector2(Mathf.Clamp(felt.x / 9.81f, -1.3f, 1.3f), Mathf.Clamp(felt.z / 9.81f, -1.3f, 1.3f));
            if (d.showroom) g = Vector2.zero;

            // ---- steering wheel
            float wheelRot = car.SteerAngle * WheelRatio;
            if (d.showroom) wheelRot = Mathf.Sin(idleT * 0.4f) * 12f;
            if (steering != null)
            {
                Quaternion R = Quaternion.AngleAxis(wheelRot, a.wheelAxis);
                Vector3 p = a.wheelC + R * (steerRestPos - a.wheelC);
                steering.SetPositionAndRotation(a.root.TransformPoint(p), a.root.rotation * (R * steerRestRot));
            }

            // ---- gear changes
            int gear = car.Gear;
            if (gear != lastGear)
            {
                if (!d.showroom && d.speedMs > 1.5f) { shiftT = 0f; fromGear = lastGear; }
                lastGear = gear;
            }
            shiftT += dt;
            float shiftW = shiftT < 0.12f ? shiftT / 0.12f : shiftT < 0.30f ? 1f : Mathf.Clamp01(1f - (shiftT - 0.30f) / 0.15f);
            shiftW = shiftW * shiftW * (3f - 2f * shiftW);
            float leverK = Mathf.Clamp01((shiftT - 0.12f) / 0.16f);
            int g0 = shiftT < 0.45f ? fromGear : gear;
            float pitch0 = (g0 % 2 == 1 ? 14f : -14f), side0 = ((g0 - 1) / 2 - 1) * 10f;
            float pitch1 = (gear % 2 == 1 ? 14f : -14f), side1 = ((gear - 1) / 2 - 1) * 10f;
            float midPitch = Mathf.Lerp(pitch0, pitch1, leverK) * Mathf.Abs(leverK * 2f - 1f);   // passes through neutral
            leverPitch = shiftT < 0.45f ? midPitch : pitch1;
            leverSide = Mathf.Lerp(side0, side1, leverK);
            Quaternion lr = Quaternion.AngleAxis(leverPitch, Vector3.right) * Quaternion.AngleAxis(-leverSide, Vector3.forward);
            batch.Set(lever, Matrix4x4.TRS(leverPos, lr, Vector3.one));
            Vector3 knob = leverPos + lr * new Vector3(0f, 0.18f, 0f);

            // ---- driver
            float look = Mathf.Clamp(car.SteerAngle * 0.7f + (car.IsDrifting ? d.DriftSide() * car.DriftAngle * 0.25f : 0f), -28f, 28f);
            if (d.showroom) look = Mathf.Sin(idleT * 0.23f) * 20f * Mathf.Clamp01(Mathf.Sin(idleT * 0.11f) * 3f);
            drv.yaw = Mathf.Lerp(drv.yaw, look, 1f - Mathf.Exp(-dt * 5f));
            drv.pitch = Mathf.Lerp(drv.pitch, -4f, 1f - Mathf.Exp(-dt * 4f));
            float handRot = Mathf.Clamp(wheelRot, -100f, 100f);
            Vector3 gripL = a.Rim(-90f + handRot) + a.wheelAxis * 0.02f;
            Vector3 gripR = a.Rim(90f + handRot) + a.wheelAxis * 0.02f;
            gripR = Vector3.Lerp(gripR, knob + Vector3.up * 0.01f, shiftW);
            PosePerson(d, drv, g, dt, gripL, gripR);

            // ---- co-driver
            if (co != null)
            {
                bool tight = !d.showroom && car.SpeedKmh > 25f && (Mathf.Abs(car.SteerAngle) > 12f || car.IsDrifting);
                if (tight) { pointTimer = 0.6f; pointSide = car.IsDrifting ? d.DriftSide() : Mathf.Sign(car.SteerAngle); }
                pointTimer -= dt;
                pointW = Mathf.MoveTowards(pointW, pointTimer > 0f ? 1f : 0f, dt * 3.5f);
                co.yaw = Mathf.Lerp(co.yaw, pointW * pointSide * 22f, 1f - Mathf.Exp(-dt * 4f));
                co.pitch = Mathf.Lerp(co.pitch, Mathf.Lerp(22f, -2f, pointW), 1f - Mathf.Exp(-dt * 4f));

                Quaternion bookRot = Quaternion.AngleAxis(-48f, Vector3.right);
                Vector3 bookPos = co.hip + new Vector3(0f, 0.20f, 0.30f) * co.s + Vector3.up * Mathf.Clamp(-g.y, -0.01f, 0.01f);
                bookPos += Vector3.forward * (co.tilt.y * 0.02f) + Vector3.right * (co.tilt.x * 0.015f);
                batch.Set(co.book, Matrix4x4.TRS(bookPos, bookRot, Vector3.one));
                Vector3 bR = bookRot * Vector3.right;
                Vector3 hl = bookPos - bR * 0.1f - (bookRot * Vector3.up) * 0.015f;
                Vector3 hr = bookPos + bR * 0.1f - (bookRot * Vector3.up) * 0.015f;
                if (pointW > 0.001f)
                {
                    Quaternion tr = TorsoRot(co.lean, co.tilt.y * 6f, -co.tilt.x * 7f);
                    float sd = pointSide;
                    Vector3 sh = co.hip + tr * (new Vector3(sd * 0.2f, 0.43f, -0.03f) * co.s);
                    Vector3 dir = (Vector3.forward + Vector3.right * (0.55f * sd) + Vector3.up * 0.3f).normalized;
                    Vector3 pt = sh + dir * (co.L1 + co.L2) * 0.96f + Vector3.up * (Mathf.Sin(idleT * 9f) * 0.02f);
                    if (sd > 0f) hr = Vector3.Lerp(hr, pt, pointW); else hl = Vector3.Lerp(hl, pt, pointW);
                }
                PosePerson(d, co, g, dt, hl, hr);
            }
        }

        void PosePerson(CarDetails d, Person p, Vector2 g, float dt, Vector3 targetL, Vector3 targetR)
        {
            var batch = d.batch;
            Spring(ref p.tilt, ref p.tiltV, g, 10f, 0.45f, dt);
            Spring(ref p.htilt, ref p.htiltV, g, 7f, 0.3f, dt);
            float roll = Mathf.Clamp(-p.tilt.x * 7f, -9f, 9f), pitch = Mathf.Clamp(p.tilt.y * 6f, -7f, 9f);
            Quaternion tr = TorsoRot(p.lean, pitch, roll);
            Vector3 hip = p.hip + Vector3.up * Mathf.Clamp((p.tilt.y - g.y) * 0.004f, -0.006f, 0.006f);
            batch.Set(p.legs, Matrix4x4.Translate(hip));
            batch.Set(p.torso, Matrix4x4.TRS(hip, tr, Vector3.one));
            Vector3 neck = hip + tr * (new Vector3(0f, 0.53f, -0.01f) * p.s);
            float hroll = Mathf.Clamp(-(p.htilt.x - p.tilt.x) * 10f, -12f, 12f);
            float hpitch = Mathf.Clamp((p.htilt.y - p.tilt.y) * 8f, -10f, 12f);
            Quaternion hr = tr * Quaternion.AngleAxis(p.lean * 0.7f, Vector3.right) * Quaternion.AngleAxis(p.yaw, Vector3.up) *
                            Quaternion.AngleAxis(p.pitch + hpitch, Vector3.right) * Quaternion.AngleAxis(hroll, Vector3.forward);
            p.headWorld = d.transform.TransformPoint(neck + hr * (new Vector3(0f, 0.12f, 0.015f) * p.s));
            bool hideHead = p.driver && d.cam != null && (d.cam.transform.position - p.headWorld).sqrMagnitude < 0.5f * 0.5f;
            batch.Set(p.head, hideHead ? Matrix4x4.zero : Matrix4x4.TRS(neck, hr, Vector3.one));

            Vector3 shL = hip + tr * (new Vector3(-0.2f, 0.43f, -0.03f) * p.s);
            Vector3 shR = hip + tr * (new Vector3(0.2f, 0.43f, -0.03f) * p.s);
            PoseArm(batch, p.upL, p.foL, shL, targetL, p.L1, p.L2, new Vector3(-1f, -1.2f, -0.3f));
            PoseArm(batch, p.upR, p.foR, shR, targetR, p.L1, p.L2, new Vector3(1f, -1.2f, -0.3f));
        }

        static void PoseArm(LifeBatch batch, int up, int fo, Vector3 s, Vector3 t, float a, float b, Vector3 pole)
        {
            Vector3 dv = t - s;
            float L = dv.magnitude;
            float maxL = (a + b) * 0.995f, minL = Mathf.Abs(a - b) + 0.02f;
            if (L < 1e-4f) { dv = Vector3.forward * minL; L = minL; }
            Vector3 dn = dv / L;
            L = Mathf.Clamp(L, minL, maxL);
            float x = (a * a - b * b + L * L) / (2f * L);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 pd = Vector3.ProjectOnPlane(pole, dn);
            if (pd.sqrMagnitude < 1e-6f) pd = Vector3.ProjectOnPlane(Vector3.down, dn);
            pd.Normalize();
            Vector3 e = s + dn * x + pd * h;
            Vector3 hand = s + dn * L;
            Vector3 upHint = Vector3.Cross(e - s, pd);
            if (upHint.sqrMagnitude < 1e-6f) upHint = Vector3.up;
            batch.Set(up, Matrix4x4.TRS(s, Quaternion.LookRotation(e - s, upHint), Vector3.one));
            batch.Set(fo, Matrix4x4.TRS(e, Quaternion.LookRotation(hand - e, upHint), Vector3.one));
        }
    }
}

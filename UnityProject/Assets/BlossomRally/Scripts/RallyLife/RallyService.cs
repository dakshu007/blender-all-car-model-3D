// RallyService.cs - the service park next to the village garage (team tents with rally cars, tool
// boxes, tyre stacks, flags and a few busy mechanics; well clear of the turntable and the spawn road)
// and the TV camera helicopter that circles high above the player during events (procedural rotor
// sound, no colliders, flies off and hides when the event ends).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyServicePark : RallyModule
    {
        sealed class Mech
        {
            public int body, armL, armR, legL = -1, legR = -1;
            public Vector3 a, b;          // work spots (walkers go between them)
            public float yawA, yawB, phase, u;
            public int kind;              // 0 working at a car, 1 kneeling, 2 walking
        }

        readonly List<Mech> mechs = new List<Mech>();
        LifeBatch batch;
        Vector3 centre;
        public bool Built { get; private set; }
        public override string Summary { get { return Built ? mechs.Count + " mechanics" : "no free spot"; } }

        public override void Build()
        {
            Vector2 g = MapData.Garage, spot = Garage.Spot;
            Vector2 spawn = new Vector2(W.SpawnPosition.x, W.SpawnPosition.z);
            Vector2 best = Vector2.zero;
            bool found = false;
            float need = 11f;
            for (int ring = 0; ring < 12 && !found; ring++)
            {
                if (ring == 6) need = 9f;          // second pass: a tighter park
                float rad = 26f + (ring % 6) * 10f;
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f + ring * 0.37f;
                    Vector2 c = g + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                    if ((c - spot).sqrMagnitude < 24f * 24f || (c - spawn).sqrMagnitude < 26f * 26f) continue;
                    if (!PlacementRegistry.CanPlace(W, c, need, 5f)) continue;
                    if (L.Bumpiness(c, need - 2f) > 1.3f || L.Bumpiness(c, 5f) > 0.8f) continue;
                    if (!L.AreaClear(c, need)) continue;
                    best = c;
                    found = true;
                }
            }
            if (!found) return;
            PlacementRegistry.Reserve(best, need);
            centre = L.Ground(best);
            // rows face the garage
            Vector2 toG = (g - best).normalized;
            Vector3 fwd = new Vector3(toG.x, 0f, toG.y), right = new Vector3(fwd.z, 0f, -fwd.x);
            Quaternion face = Quaternion.LookRotation(fwd, Vector3.up);
            var S = L.Static;
            Color32[,] teams =
            {
                { LifeGeo.C(214, 40, 48), LifeGeo.C(246, 246, 240) },
                { LifeGeo.C(46, 96, 190), LifeGeo.C(250, 204, 60) },
                { LifeGeo.C(240, 140, 176), LifeGeo.C(60, 60, 70) },
            };
            batch = new LifeBatch();
            for (int t = 0; t < 3; t++)
            {
                Vector3 tc = Pt(right * ((t - 1) * 5.2f));
                Color32 a = teams[t, 0], b = teams[t, 1];
                S.Add(RallyShapes.Tent(a, b, 4f), tc - Vector3.up * 0.05f, face);
                Quaternion carRot = face * Quaternion.Euler(0f, 180f + R(-8f, 8f), 0f);
                Vector3 carPos = tc;
                S.Add(RallyShapes.RallyCar(a, b, 7 + t * 11), carPos, carRot);
                AddCollider(carPos + Vector3.up * 0.78f, carRot, new Vector3(1.75f, 1.45f, 3.95f));
                // tool boxes and tyre stacks at the back corners of each tent
                Vector3 tb = Pt(right * ((t - 1) * 5.2f + 1.4f) - fwd * 1.6f);
                S.Add(RallyShapes.ToolBox(t == 1 ? LifeGeo.C(40, 40, 46) : LifeGeo.C(200, 40, 40)), tb, face * Quaternion.Euler(0f, 180f, 0f));
                AddCollider(tb + Vector3.up * 0.5f, face, new Vector3(0.8f, 1f, 0.55f));
                Vector3 tr = Pt(right * ((t - 1) * 5.2f - 1.5f) - fwd * 1.5f);
                S.Add(RallyShapes.TyreRack(4), tr, Quaternion.identity);
                S.Add(RallyShapes.TyreRack(2 + t % 2), tr + right * 0.7f + fwd * 0.2f, Quaternion.identity);
                S.Add(RallyShapes.FeatherFlag(a, b, 4f), Pt(right * ((t - 1) * 5.2f + 2.3f) + fwd * 2.4f), Quaternion.Euler(0f, R(0f, 360f), 0f));
                // mechanics
                var look = RallyShapes.RandomLook(Rng, RallyShapes.Standing);
                look.top = a; look.bottom = LifeGeo.C(40, 42, 50); look.hatStyle = RallyShapes.HatCap; look.hat = b;
                if (t == 0)
                {
                    var wl = look; wl.body = RallyShapes.LegsSeparate;
                    var m = NewMech(wl, 2);
                    m.a = Pt(right * (-5.2f + 1.1f) - fwd * 1.0f);
                    m.b = Pt(right * (5.2f - 1.1f) - fwd * 1.0f);
                    m.phase = R(0f, 10f);
                }
                {
                    var m = NewMech(look, 0);
                    m.a = m.b = tc + right * 1.25f + fwd * 1.3f;
                    m.yawA = m.yawB = RallyLife.Yaw(-right) + R(-15f, 15f);
                    m.phase = R(0f, 10f);
                }
                if (t != 1)
                {
                    var kl = look; kl.body = RallyShapes.Sitting; kl.hatStyle = RallyShapes.HatNone;
                    var m = NewMech(kl, 1);
                    m.a = m.b = tc - right * 1.35f - fwd * 1.2f;
                    m.yawA = m.yawB = RallyLife.Yaw(right);
                    m.phase = R(0f, 10f);
                }
            }
            batch.Finish("ServiceMechanics", MaterialLibrary.VertexColorWind, Root, 0, true);
            Tick(0f);
            Built = true;
        }

        Vector3 Pt(Vector3 off)
        {
            Vector3 p = centre + off;
            p.y = L.Ground(p.x, p.z);
            return p;
        }

        Mech NewMech(RallyShapes.Look look, int kind)
        {
            var m = new Mech { kind = kind };
            m.body = batch.AddPart(batch.AddTemplate(RallyShapes.Person(look)));
            int at = batch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
            m.armL = batch.AddPart(at);
            m.armR = batch.AddPart(at);
            if (kind == 2)
            {
                int lt = batch.AddTemplate(RallyShapes.Leg(look.bottom));
                m.legL = batch.AddPart(lt);
                m.legR = batch.AddPart(lt);
            }
            mechs.Add(m);
            return m;
        }

        void AddCollider(Vector3 c, Quaternion r, Vector3 size)
        {
            var go = new GameObject("ServiceCollider");
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(c, r);
            go.AddComponent<BoxCollider>().size = size;
        }

        public override void Tick(float dt)
        {
            if (batch == null || (dt > 0f && !L.Due(centre, 3, 70f, 140f, 220f))) return;
            float clock = L.Clock;
            for (int i = 0; i < mechs.Count; i++)
            {
                var m = mechs[i];
                float t = clock + m.phase;
                Vector3 p = m.a;
                float yaw = m.yawA, walk = 0f;
                if (m.kind == 2)
                {
                    // walk across the park, pause at each end
                    float cyc = Mathf.Repeat(t * 0.06f, 1f);
                    float u = cyc < 0.4f ? cyc / 0.4f : cyc < 0.5f ? 1f : cyc < 0.9f ? 1f - (cyc - 0.5f) / 0.4f : 0f;
                    bool moving = (cyc < 0.4f) || (cyc >= 0.5f && cyc < 0.9f);
                    u = u * u * (3f - 2f * u);
                    p = Vector3.Lerp(m.a, m.b, u);
                    p.y = L.Ground(p.x, p.z);
                    Vector3 d = (cyc < 0.5f ? m.b - m.a : m.a - m.b);
                    yaw = RallyLife.Yaw(d);
                    walk = moving ? 1f : 0f;
                }
                Matrix4x4 b = Matrix4x4.TRS(p, Quaternion.Euler(m.kind == 0 ? 18f : 0f, yaw, 0f), Vector3.one);
                if (m.kind == 1) b = Matrix4x4.TRS(p - Vector3.up * 0.28f, Quaternion.Euler(10f, yaw, 0f), Vector3.one);
                batch.Set(m.body, b);
                float work = Mathf.Sin(t * 5f);
                Quaternion ra, la;
                if (m.kind == 2)
                {
                    float ph = Mathf.Sin(t * 7f) * walk;
                    ra = Quaternion.Euler(-ph * 30f, 0f, 6f);
                    la = Quaternion.Euler(ph * 30f, 0f, -6f);
                    batch.Set(m.legR, b * Matrix4x4.TRS(RallyShapes.HipR, Quaternion.Euler(ph * 32f, 0f, 0f), Vector3.one));
                    batch.Set(m.legL, b * Matrix4x4.TRS(RallyShapes.HipL, Quaternion.Euler(-ph * 32f, 0f, 0f), Vector3.one));
                }
                else
                {
                    // spanner work: arms forward, one cranking
                    ra = Quaternion.Euler(-70f + work * 18f, 0f, 8f + work * 6f);
                    la = Quaternion.Euler(-55f - work * 4f, 0f, -10f);
                }
                Vector3 sr = RallyShapes.ShoulderR, sl = RallyShapes.ShoulderL;
                if (m.kind == 1) { sr.y -= RallyShapes.SitDrop; sl.y -= RallyShapes.SitDrop; }
                batch.Set(m.armR, b * Matrix4x4.TRS(sr, ra, Vector3.one));
                batch.Set(m.armL, b * Matrix4x4.TRS(sl, la, Vector3.one));
            }
            batch.Upload();
        }
    }

    internal sealed class RallyHelicopter : RallyModule
    {
        LifeBatch batch;
        int body, rotor, tail;
        Vector3 pos, vel, park;
        float yaw, bank, spin, tailSpin, orbit;
        AudioSource sound;
        Transform audioT;
        bool hidden = true;

        public override void Build()
        {
            batch = new LifeBatch();
            body = batch.AddPart(batch.AddTemplate(RallyShapes.HeliBody()));
            rotor = batch.AddPart(batch.AddTemplate(RallyShapes.HeliRotor(4.6f, 2, 0.32f)));
            tail = batch.AddPart(batch.AddTemplate(RallyShapes.HeliRotor(0.75f, 2, 0.12f)));
            batch.Finish("CameraHelicopter", MaterialLibrary.VertexColorWind, Root, 0, true);
            park = new Vector3(430f, 140f, 430f);
            pos = park;
            batch.Renderer.enabled = false;
            audioT = new GameObject("HeliAudio").transform;
            audioT.SetParent(Root, false);
            sound = RallyAudio.MakeLoop(audioT, RallyAudio.Rotor, 25f, 420f);
            orbit = R(0f, 6f);
        }

        public override void Pause(bool paused)
        {
            if (sound == null) return;
            if (paused) sound.Pause(); else if (!hidden) sound.UnPause();
        }

        public override void Tick(float dt)
        {
            if (batch == null || dt <= 0f) return;
            bool want = L.Stage.Phase != StageWatch.Idle && L.HasCar;
            Vector3 target;
            if (want)
            {
                Vector3 car = L.CarPos;
                orbit += dt * 0.16f;
                float r = 42f;
                Vector3 lead = car + new Vector3(L.CarVel.x, 0f, L.CarVel.z) * 1.2f;
                target = lead + new Vector3(Mathf.Cos(orbit) * r, 0f, Mathf.Sin(orbit) * r);
                target.y = Mathf.Max(L.Ground(target.x, target.z), car.y) + 44f;
                if (hidden)
                {
                    // enter from far away, behind the camera's back
                    hidden = false;
                    pos = car + (park - car).normalized * 260f;
                    pos.y = car.y + 70f;
                    vel = Vector3.zero;
                    batch.Renderer.enabled = true;
                    if (sound != null) sound.Play();
                }
            }
            else target = park;
            if (hidden) return;

            // critically damped follow with a speed cap (smooth swoops)
            Vector3 to = target - pos;
            Vector3 desired = Vector3.ClampMagnitude(to * 0.6f, want ? 55f : 40f);
            Vector3 acc = Vector3.ClampMagnitude((desired - vel) * 1.5f, 14f);
            vel += acc * dt;
            pos += vel * dt;
            float minY = L.Ground(pos.x, pos.z) + 25f;
            if (pos.y < minY) { pos.y = minY; if (vel.y < 0f) vel.y = 0f; }
            if (!want && ((pos - park).sqrMagnitude < 40f * 40f || (pos - L.Eye).sqrMagnitude > 520f * 520f))
            {
                hidden = true;
                batch.Renderer.enabled = false;
                if (sound != null) sound.Stop();
                return;
            }

            // nose: towards the car while filming, along the flight path otherwise
            Vector3 look = want ? (L.CarPos - pos) : vel;
            look.y = 0f;
            if (look.sqrMagnitude > 0.5f) yaw = Mathf.MoveTowardsAngle(yaw, RallyLife.Yaw(look), 40f * dt);
            Quaternion yq = Quaternion.Euler(0f, yaw, 0f);
            Vector3 localAcc = Quaternion.Inverse(yq) * acc;
            bank = Mathf.Lerp(bank, Mathf.Clamp(-localAcc.x * 2.2f, -20f, 20f), 1f - Mathf.Exp(-dt * 2f));
            float pitch = Mathf.Clamp(localAcc.z * 1.6f + Vector3.Dot(vel, yq * Vector3.forward) * 0.25f, -14f, 14f);
            Quaternion rot = yq * Quaternion.Euler(pitch, 0f, bank);
            spin += dt * 2400f;
            tailSpin += dt * 3600f;
            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            batch.Set(body, m);
            batch.Set(rotor, m * Matrix4x4.TRS(new Vector3(0f, 1.08f, 0.1f), Quaternion.Euler(0f, spin, 0f), Vector3.one));
            batch.Set(tail, m * Matrix4x4.TRS(new Vector3(0.12f, 0.9f, -5.55f), Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, tailSpin, 0f), Vector3.one));
            batch.Upload();
            audioT.position = pos;
            if (sound != null) sound.volume = 0.75f * GameSettings.SfxVolume;
        }

        public override void Shutdown()
        {
            if (sound != null) sound.Stop();
        }
    }
}

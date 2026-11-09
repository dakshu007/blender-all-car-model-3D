// RallyLake.cs - life on the lake: two dragon boats (dragon head, drum, ten paddlers in sync with a
// drummer, a steersman) racing up and down two lanes north of the pavilion (race pace at Golden
// Sunset / Blue Hour, an easy paddle otherwise) with 3D drum beats, two fishermen in small sampans and
// families of ducks paddling along the south shore. One LifeBatch (one draw call) for everything.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyLake : RallyModule
    {
        sealed class Paddler { public int body, armL, armR, paddle; public Vector3 seat; public int side; public float lag; }

        sealed class DragonBoat
        {
            public int hull, drummer, drumArmL, drumArmR, steer, steerArmL, steerArmR, oar;
            public readonly List<Paddler> crew = new List<Paddler>();
            public float laneZ, x, dir = 1f, speed, heading, turn, stroke;
            public int state;              // 0 paddling, 1 turning
            public int lastBeat;
        }

        sealed class Sampan { public int hull, body, armL, armR, rod, line; public Vector2 anchor; public float yaw, phase, jerk; }

        sealed class Duck { public int part; public Vector2 pos; public float yaw, phase, scale; public int leader = -1; }

        LifeBatch batch;
        readonly List<DragonBoat> boats = new List<DragonBoat>();
        readonly List<Sampan> sampans = new List<Sampan>();
        readonly List<Duck> ducks = new List<Duck>();
        readonly List<Vector2> duckTargets = new List<Vector2>();
        Vector2 lc, lr;
        const float WaterY = WorldBuilder.WaterY;
        public override string Summary { get { return boats.Count + " dragon boats, " + sampans.Count + " sampans, " + ducks.Count + " ducks"; } }

        public override void Build()
        {
            lc = MapData.LakeCenter;
            lr = MapData.LakeRadii;
            batch = new LifeBatch();
            BuildDragonBoats();
            BuildSampans();
            BuildDucks();
            batch.Finish("LakeLife", MaterialLibrary.VertexColorWind, Root, 0, true);
            Step(0f, true);
        }

        // ---------------------------------------------------------------- dragon boats
        void BuildDragonBoats()
        {
            Color32[] hulls = { LifeGeo.C(206, 40, 40), LifeGeo.C(46, 120, 90) };
            Color32[] stripes = { LifeGeo.C(246, 196, 70), LifeGeo.C(246, 220, 120) };
            Color32[] shirts = { LifeGeo.C(250, 214, 60), LifeGeo.C(220, 50, 50) };
            float[] lanes = { 1.5f, 12.5f };
            for (int b = 0; b < 2; b++)
            {
                var boat = new DragonBoat { laneZ = lc.y + lanes[b], x = lc.x + R(-10f, 10f), dir = b == 0 ? 1f : -1f };
                boat.heading = boat.dir > 0f ? 90f : -90f;
                boat.hull = batch.AddPart(batch.AddTemplate(RallyShapes.DragonBoat(hulls[b], stripes[b])));
                var look = new RallyShapes.Look
                {
                    top = shirts[b], bottom = LifeGeo.C(40, 40, 50), skin = RallyShapes.Skins[b], hair = RallyShapes.Hairs[0],
                    hat = hulls[b], body = RallyShapes.Sitting, hatStyle = RallyShapes.HatBand
                };
                int bodyT = batch.AddTemplate(RallyShapes.Person(look));
                int armT = batch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
                int padT = batch.AddTemplate(RallyShapes.Paddle());
                for (int row = 0; row < 5; row++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var p = new Paddler
                        {
                            body = batch.AddPart(bodyT), armL = batch.AddPart(armT), armR = batch.AddPart(armT), paddle = batch.AddPart(padT),
                            seat = new Vector3(0.3f * s, 0.12f, 2.6f - row * 1.35f), side = s, lag = row * 0.04f + R(0f, 0.03f)
                        };
                        boat.crew.Add(p);
                    }
                var dl = look; dl.hatStyle = RallyShapes.HatBand; dl.top = LifeGeo.C(240, 240, 236);
                boat.drummer = batch.AddPart(batch.AddTemplate(RallyShapes.Person(dl)));
                int dArm = batch.AddTemplate(RallyShapes.Arm(dl.top, dl.skin));
                boat.drumArmL = batch.AddPart(dArm);
                boat.drumArmR = batch.AddPart(dArm);
                var sl = look; sl.body = RallyShapes.Standing; sl.hatStyle = RallyShapes.HatConical; sl.hat = LifeGeo.C(214, 184, 118);
                boat.steer = batch.AddPart(batch.AddTemplate(RallyShapes.Person(sl)));
                int sArm = batch.AddTemplate(RallyShapes.Arm(sl.top, sl.skin));
                boat.steerArmL = batch.AddPart(sArm);
                boat.steerArmR = batch.AddPart(sArm);
                boat.oar = batch.AddPart(padT);
                boats.Add(boat);
            }
        }

        static bool Festival
        {
            get { var a = Atmosphere.Instance; return a != null && (a.TimeIndex == 2 || a.TimeIndex == 3); }
        }

        void StepBoat(DragonBoat b, int bi, float dt, bool pose)
        {
            bool race = Festival;
            float cruise = race ? 4.6f : 1.5f;
            float rate = race ? 1.45f : 0.75f;      // strokes per second
            const float End = 26f;
            if (b.state == 0)
            {
                float remaining = b.dir > 0f ? (lc.x + End) - b.x : b.x - (lc.x - End);
                float target = remaining < 6f ? Mathf.Max(0.3f, cruise * remaining / 6f) : cruise;
                b.speed = Mathf.MoveTowards(b.speed, target, dt * (race ? 1.6f : 0.6f));
                b.x += b.dir * b.speed * dt;
                if (remaining < 0.3f) { b.state = 1; b.turn = 0f; }
                b.stroke += dt * rate * Mathf.Clamp01(b.speed / cruise + 0.2f);
            }
            else
            {
                // pivot round: the bow swings away from the other lane
                float rateDeg = race ? 26f : 14f;
                float sign = (bi == 0 ? -1f : 1f) * b.dir;
                b.turn += rateDeg * dt;
                b.heading += sign * rateDeg * dt;
                b.speed = 0.3f;
                b.stroke += dt * rate * 0.5f;
                if (b.turn >= 180f)
                {
                    b.state = 0;
                    b.dir = -b.dir;
                    b.heading = b.dir > 0f ? 90f : -90f;
                }
            }
            // drum beat sound on every stroke
            int beat = Mathf.FloorToInt(b.stroke);
            if (beat != b.lastBeat)
            {
                b.lastBeat = beat;
                Vector3 dp = new Vector3(b.x, WaterY + 0.8f, b.laneZ);
                if ((dp - L.Eye).sqrMagnitude < 150f * 150f)
                    L.Audio.Play(RallyAudio.Drum, dp, race ? 0.85f : 0.5f, R(0.96f, 1.04f), 10f, 160f);
            }
            if (!pose) return;
            float t = L.Clock;
            Vector3 pos = new Vector3(b.x, WaterY - 0.22f + Mathf.Sin(t * 1.4f + bi) * 0.03f, b.laneZ);
            Quaternion rot = Quaternion.Euler(Mathf.Sin(t * 1.1f + bi) * 0.8f, b.heading, Mathf.Sin(t * 0.9f + bi * 2f) * 1.2f);
            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            batch.Set(b.hull, m);
            float sh = 1f - RallyShapes.SitDrop;
            foreach (var p in b.crew)
            {
                float ph = (b.stroke - p.lag) * Mathf.PI * 2f;
                float st = Mathf.Sin(ph);                  // +1 reach forward, -1 end of the pull
                float lean = st * 14f;
                Matrix4x4 bm = m * Matrix4x4.TRS(p.seat, Quaternion.Euler(lean, 0f, -p.side * 6f), Vector3.one);
                batch.Set(p.body, bm);
                float lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.18f;   // blade out of the water on the recovery
                Vector3 grip = new Vector3(p.side * 0.1f, sh + 0.25f + lift, 0.28f + st * 0.25f);
                Quaternion pr = Quaternion.Euler(-st * 32f, 0f, p.side * 24f);
                batch.Set(p.paddle, bm * Matrix4x4.TRS(grip, pr, Vector3.one));
                Quaternion arm = Quaternion.Euler(-70f - st * 20f, 0f, p.side * 18f);
                batch.Set(p.armR, bm * Matrix4x4.TRS(new Vector3(0.2f, sh, 0f), arm, Vector3.one));
                batch.Set(p.armL, bm * Matrix4x4.TRS(new Vector3(-0.2f, sh, 0f), arm, Vector3.one));
            }
            // drummer faces the crew, alternating sticks on the beat
            Matrix4x4 dmm = m * Matrix4x4.TRS(new Vector3(0f, 0.12f, 4.85f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            batch.Set(b.drummer, dmm);
            float beatPh = Mathf.Repeat(b.stroke, 1f);
            float hitR = beatPh < 0.15f ? 1f - beatPh / 0.15f : 0f, hitL = Mathf.Abs(beatPh - 0.5f) < 0.08f ? 1f : 0f;
            batch.Set(b.drumArmR, dmm * Matrix4x4.TRS(new Vector3(0.2f, sh, 0f), Quaternion.Euler(-60f - (1f - hitR) * 50f, 0f, 10f), Vector3.one));
            batch.Set(b.drumArmL, dmm * Matrix4x4.TRS(new Vector3(-0.2f, sh, 0f), Quaternion.Euler(-60f - (1f - hitL) * 50f, 0f, -10f), Vector3.one));
            // steersman standing at the stern with a long oar trailing behind
            Matrix4x4 sm = m * Matrix4x4.TRS(new Vector3(0f, 0.1f, -4.6f), Quaternion.Euler(0f, Mathf.Sin(t * 0.4f) * 8f, 0f), Vector3.one);
            batch.Set(b.steer, sm);
            batch.Set(b.oar, sm * Matrix4x4.TRS(new Vector3(0.35f, 1.15f, -0.2f), Quaternion.Euler(-55f, 0f, 12f), new Vector3(1f, 1.6f, 1f)));
            batch.Set(b.steerArmR, sm * Matrix4x4.TRS(RallyShapes.ShoulderR, Quaternion.Euler(-30f, 0f, 20f), Vector3.one));
            batch.Set(b.steerArmL, sm * Matrix4x4.TRS(RallyShapes.ShoulderL, Quaternion.Euler(-40f, 0f, 10f), Vector3.one));
        }

        // ---------------------------------------------------------------- sampans
        void BuildSampans()
        {
            Vector2[] spots = { new Vector2(lc.x - 24f, lc.y - 9f), new Vector2(lc.x + 25f, lc.y - 7f) };
            var look = RallyShapes.RandomLook(Rng, RallyShapes.Sitting);
            look.hatStyle = RallyShapes.HatConical;
            look.hat = LifeGeo.C(214, 184, 118);
            look.top = LifeGeo.C(70, 96, 130);
            int hullT = batch.AddTemplate(RallyShapes.Sampan());
            int bodyT = batch.AddTemplate(RallyShapes.Person(look));
            int armT = batch.AddTemplate(RallyShapes.Arm(look.top, look.skin));
            int rodT = batch.AddTemplate(RallyShapes.FishingRod());
            int lineT = batch.AddTemplate(RallyShapes.StringUnit(LifeGeo.C(230, 230, 230)));
            foreach (var sp in spots)
            {
                if (MapData.LakeK(sp) > 0.8f || !LifeShapes.ClearOfPavilion(sp, 6f)) continue;
                sampans.Add(new Sampan
                {
                    hull = batch.AddPart(hullT), body = batch.AddPart(bodyT), armL = batch.AddPart(armT), armR = batch.AddPart(armT),
                    rod = batch.AddPart(rodT), line = batch.AddPart(lineT), anchor = sp, yaw = R(0f, 360f), phase = R(0f, 30f)
                });
            }
        }

        void PoseSampan(Sampan s, float dt)
        {
            float t = L.Clock + s.phase;
            s.yaw += Mathf.Sin(t * 0.05f) * 3f * dt;
            Vector2 drift = new Vector2(Mathf.Sin(t * 0.031f), Mathf.Cos(t * 0.027f)) * 2.5f;
            Vector3 pos = new Vector3(s.anchor.x + drift.x, WaterY - 0.15f + Mathf.Sin(t * 1.2f) * 0.03f, s.anchor.y + drift.y);
            Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.Euler(Mathf.Sin(t * 1.0f) * 1.5f, s.yaw, Mathf.Sin(t * 0.8f) * 2.5f), Vector3.one);
            batch.Set(s.hull, m);
            Matrix4x4 bm = m * Matrix4x4.TRS(new Vector3(0f, 0.12f, -1.5f), Quaternion.Euler(0f, 60f, 0f), Vector3.one);
            batch.Set(s.body, bm);
            // occasional strike: the rod jerks up
            if (s.jerk <= 0f && Mathf.Repeat(t, 23f) < dt) s.jerk = 1f;
            s.jerk = Mathf.Max(0f, s.jerk - dt * 0.8f);
            float up = 22f + Mathf.Sin(t * 0.7f) * 3f + s.jerk * 25f;
            float sh = 1.33f - RallyShapes.SitDrop;
            Vector3 hand = new Vector3(0.05f, sh - 0.2f, 0.4f);
            Quaternion rr = Quaternion.Euler(-up, 0f, 0f);
            Matrix4x4 rodM = bm * Matrix4x4.TRS(hand, rr, Vector3.one);
            batch.Set(s.rod, rodM);
            batch.Set(s.armR, bm * Matrix4x4.TRS(new Vector3(0.2f, sh, 0f), Quaternion.Euler(-65f, 0f, 12f), Vector3.one));
            batch.Set(s.armL, bm * Matrix4x4.TRS(new Vector3(-0.2f, sh, 0f), Quaternion.Euler(-60f, 0f, -22f), Vector3.one));
            Vector3 tip = rodM.MultiplyPoint3x4(new Vector3(0f, 0f, 3f));
            Vector3 bob = new Vector3(tip.x, WaterY, tip.z) + new Vector3(Mathf.Sin(t * 0.5f), 0f, Mathf.Cos(t * 0.4f)) * 0.3f;
            Vector3 dl = bob - tip;
            batch.Set(s.line, Matrix4x4.TRS(tip, Quaternion.LookRotation(dl.normalized, Vector3.forward), new Vector3(0.6f, 0.6f, dl.magnitude)));
        }

        // ---------------------------------------------------------------- ducks
        bool DuckWater(Vector2 p)
        {
            return MapData.LakeK(p) < 0.82f && p.y < lc.y - 4f && LifeShapes.ClearOfPavilion(p, 2.5f);
        }

        Vector2 DuckPoint()
        {
            for (int k = 0; k < 40; k++)
            {
                float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0f, 1f)) * 0.8f;
                Vector2 p = lc + new Vector2(Mathf.Cos(a) * lr.x * r, Mathf.Sin(a) * lr.y * r);
                if (DuckWater(p)) return p;
            }
            return lc + new Vector2(-20f, -12f);
        }

        void BuildDucks()
        {
            int drake = batch.AddTemplate(RallyShapes.Duck(0));
            int hen = batch.AddTemplate(RallyShapes.Duck(1));
            int chick = batch.AddTemplate(RallyShapes.Duck(2));
            int families = L.Density >= 0.75f ? 2 : 1;
            for (int f = 0; f < families; f++)
            {
                int lead = ducks.Count;
                var mother = new Duck { part = batch.AddPart(hen), pos = DuckPoint(), yaw = R(0f, 360f), phase = R(0f, 9f), scale = 1.6f };
                ducks.Add(mother);
                duckTargets.Add(DuckPoint());
                int n = 3 + RI(2);
                for (int k = 0; k < n; k++)
                {
                    ducks.Add(new Duck { part = batch.AddPart(chick), pos = mother.pos - new Vector2(0f, 0.6f * (k + 1)), yaw = mother.yaw, phase = R(0f, 9f), scale = 0.75f, leader = ducks.Count - 1 });
                    duckTargets.Add(Vector2.zero);
                }
                ducks.Add(new Duck { part = batch.AddPart(drake), pos = DuckPoint(), yaw = R(0f, 360f), phase = R(0f, 9f), scale = 1.7f });
                duckTargets.Add(DuckPoint());
            }
        }

        void StepDucks(float dt, bool pose)
        {
            float t = L.Clock;
            for (int i = 0; i < ducks.Count; i++)
            {
                var d = ducks[i];
                if (d.leader < 0)
                {
                    Vector2 to = duckTargets[i] - d.pos;
                    if (to.sqrMagnitude < 0.5f) { duckTargets[i] = DuckPoint(); to = duckTargets[i] - d.pos; }
                    float want = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
                    d.yaw = Mathf.MoveTowardsAngle(d.yaw, want, 40f * dt);
                    float rad = d.yaw * Mathf.Deg2Rad;
                    Vector2 np = d.pos + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (0.35f * dt);
                    if (DuckWater(np)) d.pos = np; else duckTargets[i] = DuckPoint();
                }
                else
                {
                    var lead = ducks[d.leader];
                    Vector2 to = lead.pos - d.pos;
                    float dist = to.magnitude;
                    if (dist > 0.55f)
                    {
                        d.pos += to / dist * Mathf.Min(dist - 0.55f, 0.6f * dt + (dist - 0.55f) * dt);
                        d.yaw = Mathf.MoveTowardsAngle(d.yaw, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg, 120f * dt);
                    }
                }
                if (!pose) continue;
                Vector3 p = new Vector3(d.pos.x, WaterY - 0.03f * d.scale + Mathf.Sin(t * 2f + d.phase) * 0.012f, d.pos.y);
                Quaternion r = Quaternion.Euler(Mathf.Sin(t * 1.7f + d.phase) * 3f, d.yaw, Mathf.Sin(t * 1.3f + d.phase) * 3f);
                batch.Set(d.part, Matrix4x4.TRS(p, r, new Vector3(d.scale, d.scale, d.scale)));
            }
        }

        // ---------------------------------------------------------------- update
        public override void Tick(float dt)
        {
            Step(dt, false);
        }

        void Step(float dt, bool force)
        {
            if (batch == null) return;
            Vector3 c = new Vector3(lc.x, WaterY, lc.y);
            bool pose = force || L.Due(c, 2, 90f, 180f, 330f);
            for (int i = 0; i < boats.Count; i++) StepBoat(boats[i], i, dt, pose);
            StepDucks(dt, pose);
            if (pose)
            {
                for (int i = 0; i < sampans.Count; i++) PoseSampan(sampans[i], dt);
                batch.Upload();
            }
        }
    }
}

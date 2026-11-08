// ValleyLife.cs - living details for the Blossom Valley: flocks of birds perched near the roads and
// blossom trees that burst up when a car comes close, villagers by the paifang gate and along Lantern
// Street who turn and wave at the player, kites over the meadows and glowing Kongming sky lanterns
// drifting up over the lake (plus small lotus lanterns floating on the water).
//
// Cheap by design: every animated group is ONE CPU-posed mesh (LifeBatch) drawn with one shared
// material, posed from a handful of matrices per creature; no per-frame allocations; everything uses
// Time.deltaTime so it freezes while the game is paused (timeScale 0). No colliders on people or birds.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class ValleyLife : MonoBehaviour
    {
        const float ScareRadius = 25f;
        const float WaveRadius = 35f;
        /// <summary>Prevailing wind (matches the petal drift / vertex wind direction).</summary>
        public static readonly Vector3 Wind = new Vector3(1f, 0f, 0.35f).normalized;

        /// <summary>Build all valley life. Call once after WorldBuilder.Build() has finished.</summary>
        public static ValleyLife Create(WorldBuilder w)
        {
            var go = new GameObject("ValleyLife");
            var life = go.AddComponent<ValleyLife>();
            life.world = w;
            life.StartCoroutine(life.Build());
            return life;
        }

        public bool Ready { get; private set; }

        WorldBuilder world;
        readonly System.Random rng = new System.Random(51507);
        float clock;
        int frame;

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        IEnumerator Build()
        {
            while (world != null && !world.Done) yield return null;
            if (world == null) yield break;
            Physics.SyncTransforms();
            BuildBirds();
            yield return null;
            BuildVillagers();
            yield return null;
            BuildKites();
            yield return null;
            BuildLanterns();
            Ready = true;
            Step(0f, true);
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;          // paused
            clock += dt;
            frame++;
            Step(dt, false);
        }

        void Step(float dt, bool force)
        {
            Vector3 car;
            bool hasCar = CarPos(out car);
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : (hasCar ? car : Vector3.zero);
            UpdateBirds(hasCar, car, eye, dt, force);
            UpdateVillagers(hasCar, car, eye, dt, force);
            UpdateKites();
            UpdateLanterns(dt);
        }

        static bool CarPos(out Vector3 p)
        {
            var gm = GameManager.Instance;
            // only a car that is actually being driven (not the one on the garage turntable) disturbs life
            if (gm != null && gm.IsDriving && gm.Car != null) { p = gm.Car.transform.position; return true; }
            p = Vector3.zero;
            return false;
        }

        bool OffRoad(Vector2 p, float margin)
        {
            RoadSample rs;
            float d = world.NearestRoad(p, out rs, 40f);
            return d == float.MaxValue || d > rs.halfWidth + margin;
        }

        Vector3 Ground(Vector3 p) { return new Vector3(p.x, world.TerrainHeight(p.x, p.z), p.z); }

        // ================================================================== birds
        class Bird
        {
            public int body, wingL, wingR;
            public float scale, phase, delay, yaw;
            public Vector3 offset, pos, from, to;
            public float circR, circPh, circW, alt;
            public bool landed = true;
        }

        class Flock
        {
            public Bird[] birds;
            public int spot;
            public bool flying;
            public float t0, dur, cool;
        }

        static readonly Vector3 WingRootR = new Vector3(0.045f, 0.092f, 0.03f);
        static readonly Vector3 WingRootL = new Vector3(-0.045f, 0.092f, 0.03f);
        LifeBatch birdBatch;
        readonly List<Vector3> perches = new List<Vector3>();
        bool[] perchUsed;
        Flock[] flocks;

        void BuildBirds()
        {
            CollectPerches();
            if (perches.Count < 4) return;
            perchUsed = new bool[perches.Count];
            birdBatch = new LifeBatch();
            var tpl = new int[9];
            for (int s = 0; s < 3; s++)
            {
                tpl[s * 3] = birdBatch.AddTemplate(LifeShapes.BirdBody(s));
                tpl[s * 3 + 1] = birdBatch.AddTemplate(LifeShapes.BirdWing(s, 1));
                tpl[s * 3 + 2] = birdBatch.AddTemplate(LifeShapes.BirdWing(s, -1));
            }
            int nFlocks = Mathf.Min(7, perches.Count / 3);
            flocks = new Flock[nFlocks];
            for (int f = 0; f < nFlocks; f++)
            {
                int spot = rng.Next(perches.Count);
                for (int k = 0; k < perches.Count && perchUsed[spot]; k++) spot = (spot + 1) % perches.Count;
                perchUsed[spot] = true;
                int species = f % 3;
                int n = rng.Next(4, 8);
                var fl = new Flock { birds = new Bird[n], spot = spot };
                float baseScale = species == 0 ? 1.1f : (species == 1 ? 0.8f : 0.9f);
                for (int i = 0; i < n; i++)
                {
                    var b = new Bird();
                    b.body = birdBatch.AddPart(tpl[species * 3]);
                    b.wingR = birdBatch.AddPart(tpl[species * 3 + 1]);
                    b.wingL = birdBatch.AddPart(tpl[species * 3 + 2]);
                    b.scale = baseScale * Rand(0.9f, 1.12f);
                    b.phase = Rand(0f, 100f);
                    float a = Rand(0f, Mathf.PI * 2f), r = Rand(0.3f, 2.6f);
                    b.offset = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    b.yaw = Rand(0f, 360f);
                    b.pos = Ground(perches[spot] + b.offset);
                    fl.birds[i] = b;
                }
                flocks[f] = fl;
            }
            birdBatch.Finish("Life_Birds", MaterialLibrary.VertexColorWind, transform, 0, true);
        }

        void CollectPerches()
        {
            var trees = new List<Vector3>();
            var root = GameObject.Find("World_Static");
            if (root != null)
            {
                Transform rt = root.transform;
                for (int i = 0; i < rt.childCount; i++)
                {
                    Transform ch = rt.GetChild(i);
                    if (ch.name.StartsWith("ENV_Cherry")) trees.Add(ch.position);
                }
            }
            const int fromTrees = 22;
            for (int tries = 0; tries < 500 && perches.Count < fromTrees && trees.Count > 0; tries++)
            {
                Vector3 t = trees[rng.Next(trees.Count)];
                float a = Rand(0f, Mathf.PI * 2f), r = Rand(3.8f, 6.5f);
                TryPerch(new Vector2(t.x + Mathf.Cos(a) * r, t.z + Mathf.Sin(a) * r));
            }
            for (int tries = 0; tries < 500 && perches.Count < fromTrees + 16; tries++)
            {
                var list = world.roadSamples[rng.Next(world.roadSamples.Count)];
                if (list.Count == 0) continue;
                var s = list[rng.Next(list.Count)];
                if (s.bridge) continue;
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                float side = rng.Next(2) == 0 ? -1f : 1f;
                TryPerch(s.pos + nrm * side * (s.halfWidth + Rand(5.5f, 10f)));
            }
        }

        void TryPerch(Vector2 p)
        {
            if (Mathf.Abs(p.x) > 470f || Mathf.Abs(p.y) > 470f) return;
            if (!OffRoad(p, 5f) || world.RiverDistance(p) < 15f || MapData.LakeK(p) < 1.35f) return;
            for (int i = 0; i < perches.Count; i++)
            {
                Vector3 q = perches[i];
                if ((new Vector2(q.x, q.z) - p).sqrMagnitude < 20f * 20f) return;
            }
            perches.Add(new Vector3(p.x, world.TerrainHeight(p.x, p.y), p.y));
        }

        void TakeOff(Flock fl, Vector3 car)
        {
            Vector3 home = perches[fl.spot];
            int best = -1;
            for (int tries = 0; tries < 24 && best < 0; tries++)
            {
                int k = rng.Next(perches.Count);
                if (perchUsed[k]) continue;
                float dHome = Vector3.Distance(perches[k], home), dCar = Vector3.Distance(perches[k], car);
                if (dHome > 40f && dHome < 230f && dCar > 60f) best = k;
            }
            if (best < 0)
            {
                float far = -1f;
                for (int k = 0; k < perches.Count; k++)
                {
                    if (perchUsed[k]) continue;
                    float d = (perches[k] - car).sqrMagnitude;
                    if (d > far) { far = d; best = k; }
                }
            }
            if (best < 0) return;
            perchUsed[fl.spot] = false;
            perchUsed[best] = true;
            fl.spot = best;
            fl.flying = true;
            fl.t0 = clock;
            fl.dur = Rand(10f, 20f);
            float circR = Rand(7f, 14f), circW = (rng.Next(2) == 0 ? -1f : 1f) * Rand(0.35f, 0.6f);
            float circPh = Rand(0f, Mathf.PI * 2f), alt = Rand(10f, 18f);
            Vector3 target = perches[best];
            foreach (var b in fl.birds)
            {
                b.from = b.pos;
                b.to = Ground(target + b.offset);
                b.delay = Rand(0f, 0.45f);
                b.circR = circR + Rand(-1.5f, 1.5f);
                b.circW = circW;
                b.circPh = circPh + Rand(-0.25f, 0.25f);
                b.alt = alt + Rand(-2f, 2f);
                b.landed = false;
            }
        }

        void UpdateBirds(bool hasCar, Vector3 car, Vector3 eye, float dt, bool force)
        {
            if (flocks == null) return;
            for (int f = 0; f < flocks.Length; f++)
            {
                var fl = flocks[f];
                if (!fl.flying)
                {
                    fl.cool -= dt;
                    if (hasCar && fl.cool <= 0f && (car - perches[fl.spot]).sqrMagnitude < ScareRadius * ScareRadius) TakeOff(fl, car);
                }
                // idle perched birds: animate when visible-ish; far ones only every third frame
                float dEye = (eye - perches[fl.spot]).sqrMagnitude;
                bool near = force || dEye < 60f * 60f || (dEye < 150f * 150f && (frame + f) % 3 == 0);
                bool allLanded = true;
                for (int i = 0; i < fl.birds.Length; i++)
                {
                    var b = fl.birds[i];
                    if (fl.flying && !b.landed)
                    {
                        float u = (clock - fl.t0 - b.delay) / fl.dur;
                        if (u >= 1f)
                        {
                            b.landed = true;
                            b.pos = b.to;
                            PerchBird(b, dt, 0f);
                        }
                        else
                        {
                            allLanded = false;
                            if (u > 0f) FlyBird(b, u, fl.dur);
                            else PerchBird(b, dt, 1f);
                        }
                    }
                    else if (near) PerchBird(b, dt, 0f);
                }
                if (fl.flying && allLanded) { fl.flying = false; fl.cool = 2f; }
            }
            birdBatch.Upload();
        }

        void PerchBird(Bird b, float dt, float alert)
        {
            float pk = Mathf.Sin(clock * 1.9f + b.phase);
            pk = pk > 0.55f ? (pk - 0.55f) / 0.45f : 0f;
            float peck = pk * pk * 38f * (1f - alert);
            b.yaw += Mathf.Sin(clock * 0.37f + b.phase * 1.3f) * 30f * dt;
            float hop = Mathf.Max(0f, Mathf.Sin(clock * 0.8f + b.phase * 2.1f) - 0.97f) * 4f;
            Vector3 p = b.pos;
            p.y += hop;
            float flap = 30f + Mathf.Sin(clock * 40f + b.phase) * 25f;
            PoseBird(p, Quaternion.Euler(peck - alert * 12f, b.yaw, 0f), b, flap, alert * 0.35f);
        }

        void FlyBird(Bird b, float u, float dur)
        {
            float su = u * u * (3f - 2f * u);
            float env = Mathf.Sin(Mathf.PI * u);
            float ang = b.circPh + b.circW * u * dur;
            Vector3 p = Vector3.Lerp(b.from, b.to, su);
            p.x += Mathf.Cos(ang) * b.circR * env;
            p.z += Mathf.Sin(ang) * b.circR * env;
            float ground = world.TerrainHeight(p.x, p.z);
            p.y = Mathf.Max(Mathf.Lerp(b.from.y, b.to.y, su), ground) + b.alt * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.55f));
            Vector3 v = p - b.pos;
            float pitch = 0f;
            float h = Mathf.Sqrt(v.x * v.x + v.z * v.z);
            if (h > 1e-4f)
            {
                b.yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(-Mathf.Atan2(v.y, h) * Mathf.Rad2Deg, -35f, 35f);
            }
            b.pos = p;
            float roll = -Mathf.Sign(b.circW) * 24f * env;
            float rate, amp, bias = 5f;
            if (u < 0.2f) { rate = 11f; amp = 68f; }
            else if (u > 0.86f) { rate = 9f; amp = 45f; bias = 18f; }
            else { rate = 7f; amp = 58f * Mathf.Clamp01(0.45f + 0.9f * Mathf.Sin(clock * 0.8f + b.phase)); }
            float flap = bias + amp * Mathf.Sin(clock * rate * 6.2832f + b.phase);
            float spread = Mathf.Clamp01(u / 0.03f) * Mathf.Clamp01((1f - u) / 0.025f);
            PoseBird(p, Quaternion.Euler(pitch, b.yaw, roll), b, flap, spread);
        }

        void PoseBird(Vector3 p, Quaternion rot, Bird b, float flap, float spread)
        {
            Matrix4x4 m = Matrix4x4.TRS(p, rot, new Vector3(b.scale, b.scale, b.scale));
            birdBatch.Set(b.body, m);
            Quaternion r = Quaternion.Slerp(Quaternion.Euler(0f, 80f, -6f), Quaternion.Euler(0f, 0f, flap), spread);
            birdBatch.Set(b.wingR, m * Matrix4x4.TRS(WingRootR, r, Vector3.one));
            Quaternion l = Quaternion.Slerp(Quaternion.Euler(0f, -80f, 6f), Quaternion.Euler(0f, 0f, -flap), spread);
            birdBatch.Set(b.wingL, m * Matrix4x4.TRS(WingRootL, l, Vector3.one));
        }

        // ================================================================== villagers
        class Villager
        {
            public int body, armL, armR;
            public Vector3 pos;
            public float baseYaw, yaw, scale, phase, wave;
        }

        static readonly Vector3 ShoulderR = new Vector3(0.205f, 1.33f, 0f);
        static readonly Vector3 ShoulderL = new Vector3(-0.205f, 1.33f, 0f);
        LifeBatch peopleBatch;
        Villager[] villagers;

        void BuildVillagers()
        {
            var spots = new List<Vector4>();   // x, y, z, facing yaw
            Vector2 g = MapData.Gate;
            float[] gx = { -10.8f, 10.6f, -12.6f, 12.4f, -10.5f, 10.8f };
            float[] gz = { -2.2f, -1.4f, 1.8f, 1.2f, -5.0f, -4.6f };
            for (int i = 0; i < gx.Length && spots.Count < 3; i++)
            {
                Vector2 p = g + new Vector2(gx[i], gz[i]);
                Vector2 look = g + new Vector2(0f, -7f) - p;           // towards the street just south of the gate
                TrySpot(spots, p, Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg);
            }
            if (world.roadSamples.Count > 4)
            {
                var street = world.roadSamples[4];
                float side = 1f;
                for (int pass = 0; pass < 2 && spots.Count < 9; pass++)
                    for (int i = 9 + pass * 4; i < street.Count - 4 && spots.Count < 9; i += 9)
                    {
                        var s = street[i];
                        Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                        for (int k = 0; k < 2; k++)
                        {
                            float sd = k == 0 ? side : -side;
                            Vector2 p = s.pos + nrm * sd * (s.halfWidth + Rand(3.2f, 4.2f)) + s.tangent * Rand(-1.2f, 1.2f);
                            Vector2 face = -nrm * sd + s.tangent * Rand(-0.4f, 0.4f);
                            if (TrySpot(spots, p, Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg)) { side = -sd; break; }
                        }
                    }
            }
            if (spots.Count == 0) return;

            Color32[] tunics =
            {
                LifeGeo.C(204, 50, 44), LifeGeo.C(72, 160, 122), LifeGeo.C(56, 66, 128), LifeGeo.C(208, 148, 52),
                LifeGeo.C(226, 120, 150), LifeGeo.C(46, 128, 120), LifeGeo.C(176, 40, 52), LifeGeo.C(92, 112, 176), LifeGeo.C(222, 176, 70)
            };
            Color32[] trims =
            {
                LifeGeo.C(236, 196, 84), LifeGeo.C(238, 232, 214), LifeGeo.C(222, 72, 60), LifeGeo.C(150, 52, 40),
                LifeGeo.C(250, 230, 236), LifeGeo.C(236, 196, 84), LifeGeo.C(40, 36, 44), LifeGeo.C(238, 220, 160), LifeGeo.C(180, 50, 44)
            };
            Color32[] trousers = { LifeGeo.C(48, 44, 60), LifeGeo.C(70, 58, 50), LifeGeo.C(38, 48, 72) };
            Color32 straw = LifeGeo.C(214, 184, 118), cap = LifeGeo.C(36, 34, 40);

            peopleBatch = new LifeBatch();
            villagers = new Villager[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                int style = i % 3;   // 0 straw hat, 1 hair bun, 2 scholar cap
                int bodyT = peopleBatch.AddTemplate(LifeShapes.VillagerBody(tunics[i % tunics.Length], trims[i % trims.Length],
                    trousers[i % trousers.Length], style, style == 2 ? cap : straw));
                int armT = peopleBatch.AddTemplate(LifeShapes.VillagerArm(tunics[i % tunics.Length], trims[i % trims.Length]));
                var v = new Villager();
                v.body = peopleBatch.AddPart(bodyT);
                v.armR = peopleBatch.AddPart(armT);
                v.armL = peopleBatch.AddPart(armT);
                Vector4 s = spots[i];
                v.pos = new Vector3(s.x, s.y - 0.02f, s.z);
                v.baseYaw = v.yaw = s.w;
                v.scale = (i % 5 == 3) ? Rand(0.66f, 0.72f) : Rand(0.92f, 1.06f);   // a few children
                v.phase = Rand(0f, 50f);
                villagers[i] = v;
            }
            peopleBatch.Finish("Life_Villagers", MaterialLibrary.VertexColorWind, transform, 0, true);
        }

        bool TrySpot(List<Vector4> spots, Vector2 p, float yaw)
        {
            if (!OffRoad(p, 3f)) return false;
            if (world.RiverDistance(p) < 12f || MapData.LakeK(p) < 1.3f) return false;
            for (int i = 0; i < spots.Count; i++)
                if ((new Vector2(spots[i].x, spots[i].z) - p).sqrMagnitude < 9f) return false;
            float y = world.TerrainHeight(p.x, p.y);
            // keep clear of houses, lantern posts, gate pillars and tree trunks (all have colliders)
            if (Physics.CheckSphere(new Vector3(p.x, y + 1.05f, p.y), 0.6f, ~0, QueryTriggerInteraction.Ignore)) return false;
            spots.Add(new Vector4(p.x, y, p.y, yaw));
            return true;
        }

        void UpdateVillagers(bool hasCar, Vector3 car, Vector3 eye, float dt, bool force)
        {
            if (villagers == null) return;
            for (int i = 0; i < villagers.Length; i++)
            {
                var v = villagers[i];
                float dEye = (eye - v.pos).sqrMagnitude;
                if (!force && dEye > 140f * 140f) continue;                          // frozen when nobody can see
                if (!force && dEye > 60f * 60f && ((frame + i) & 3) != 0) continue;  // distant: quarter rate
                float target = v.baseYaw, waveT = 0f;
                if (hasCar)
                {
                    Vector3 d = car - v.pos;
                    d.y = 0f;
                    if (d.sqrMagnitude < WaveRadius * WaveRadius)
                    {
                        target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        waveT = 1f;
                    }
                }
                v.yaw = Mathf.MoveTowardsAngle(v.yaw, target, 150f * dt);
                v.wave = Mathf.MoveTowards(v.wave, waveT, dt * 2.5f);
                float t = clock + v.phase;
                Quaternion rot = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 1.2f, v.yaw, Mathf.Sin(t * 0.9f) * 2.2f);
                Vector3 p = v.pos;
                p.y += Mathf.Max(0f, Mathf.Sin(t * 7f)) * 0.04f * v.wave;           // excited little bounce
                Matrix4x4 m = Matrix4x4.TRS(p, rot, new Vector3(v.scale, v.scale, v.scale));
                peopleBatch.Set(v.body, m);
                float idle = Mathf.Sin(t * 1.1f) * 4f;
                float raise = Mathf.Lerp(7f + idle * 0.5f, 150f + Mathf.Sin(t * 9f) * 24f, v.wave);
                Quaternion rArm = Quaternion.Euler(Mathf.Lerp(idle * 1.5f, 14f, v.wave), 0f, raise);
                Quaternion lArm = Quaternion.Euler(-idle * 1.5f, 0f, -(7f - idle * 0.5f));
                peopleBatch.Set(v.armR, m * Matrix4x4.TRS(ShoulderR, rArm, Vector3.one));
                peopleBatch.Set(v.armL, m * Matrix4x4.TRS(ShoulderL, lArm, Vector3.one));
            }
            peopleBatch.Upload();
        }

        // ================================================================== kites
        class Kite
        {
            public int part, stake;
            public Vector3 anchor;
            public float height, dist, phase, size, tailLen, tailTip;
            public LineRenderer line, tail;
            public Vector3[] linePts, tailPts;
        }

        LifeBatch kiteBatch;
        Kite[] kites;
        Material lineMat;

        void BuildKites()
        {
            var anchors = new List<Vector3>();
            for (int tries = 0; tries < 800 && anchors.Count < 5; tries++)
            {
                Vector2 p = new Vector2(Rand(-410f, 410f), Rand(-410f, 400f));
                if (!OffRoad(p, 24f) || world.RiverDistance(p) < 25f || MapData.LakeK(p) < 1.8f) continue;
                if (MapData.InBambooForest(p) || MapData.InTeaTerraces(p) || MapData.InVillage(p)) continue;
                if (Vector2.Distance(p, MapData.MountainCenter) < 170f) continue;
                bool ok = true;
                for (int i = 0; i < anchors.Count && ok; i++)
                    if ((new Vector2(anchors[i].x, anchors[i].z) - p).sqrMagnitude < 110f * 110f) ok = false;
                if (!ok) continue;
                float y = world.TerrainHeight(p.x, p.y);
                if (Physics.CheckSphere(new Vector3(p.x, y + 1.3f, p.y), 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                anchors.Add(new Vector3(p.x, y, p.y));
            }
            if (anchors.Count == 0) return;

            lineMat = MaterialLibrary.Particle("LifeLine", Texture2D.whiteTexture, Color.white, 0.55f);
            kiteBatch = new LifeBatch();
            int stakeT = kiteBatch.AddTemplate(LifeShapes.KiteStake());
            Color32[,] pal =
            {
                { LifeGeo.C(226, 52, 44), LifeGeo.C(250, 204, 70) },
                { LifeGeo.C(36, 40, 60), LifeGeo.C(222, 60, 50) },
                { LifeGeo.C(64, 168, 128), LifeGeo.C(246, 150, 180) },
                { LifeGeo.C(40, 70, 150), LifeGeo.C(246, 196, 64) },
                { LifeGeo.C(70, 70, 160), LifeGeo.C(248, 136, 52) },
            };
            kites = new Kite[anchors.Count];
            for (int i = 0; i < anchors.Count; i++)
            {
                bool swallow = i % 2 == 1;
                Color32 a = pal[i % 5, 0], b = pal[i % 5, 1];
                int kt = kiteBatch.AddTemplate(swallow ? LifeShapes.KiteSwallow(a, b) : LifeShapes.KiteDiamond(a, b));
                var k = new Kite();
                k.part = kiteBatch.AddPart(kt);
                k.anchor = anchors[i];
                k.height = Rand(25f, 45f);
                k.dist = k.height * Rand(0.5f, 0.75f);
                k.phase = Rand(0f, 100f);
                k.size = Rand(1.35f, 1.8f);
                k.tailLen = swallow ? Rand(2.5f, 3.5f) : Rand(5f, 7f);
                k.tailTip = swallow ? -0.8f : -0.9f;
                k.stake = kiteBatch.AddPart(stakeT);
                k.linePts = new Vector3[12];
                k.tailPts = new Vector3[10];
                k.line = MakeLine("KiteString_" + i, k.linePts.Length, 0.04f, 0.03f, new Color(0.95f, 0.93f, 0.88f, 0.9f), new Color(0.95f, 0.93f, 0.88f, 0.9f));
                Color tc = b;
                k.tail = MakeLine("KiteTail_" + i, k.tailPts.Length, 0.22f, 0.05f, tc, new Color(tc.r, tc.g, tc.b, 0.6f));
                kites[i] = k;
            }
            kiteBatch.Finish("Life_Kites", MaterialLibrary.VertexColorWind, transform, 0, true);
            foreach (var k in kites)
                kiteBatch.Set(k.stake, Matrix4x4.TRS(k.anchor, Quaternion.Euler(0f, Rand(0f, 360f), 0f), Vector3.one));
        }

        LineRenderer MakeLine(string name, int count, float w0, float w1, Color c0, Color c1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = count;
            lr.startWidth = w0;
            lr.endWidth = w1;
            lr.startColor = c0;
            lr.endColor = c1;
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sharedMaterial = lineMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        void UpdateKites()
        {
            if (kites == null) return;
            float gust = Atmosphere.Gust;
            Vector3 side = new Vector3(-Wind.z, 0f, Wind.x);
            for (int i = 0; i < kites.Length; i++)
            {
                var k = kites[i];
                float t = clock + k.phase;
                float sway = 2f + 3.5f * gust;
                float lateral = Mathf.Sin(t * 0.31f) * sway + Mathf.Sin(t * 0.83f + 1.3f) * 0.5f * sway;
                Vector3 pos = k.anchor + Wind * (k.dist + Mathf.Sin(t * 0.23f) * 2f)
                              + Vector3.up * (k.height + Mathf.Sin(t * 0.41f) * 1.2f + gust * 2.5f)
                              + side * lateral;
                float roll = Mathf.Sin(t * 0.9f) * (6f + 12f * gust) - Mathf.Cos(t * 0.31f) * 12f;
                // the sail faces back down its string (towards the flyer), slightly into the wind
                Vector3 toAnchor = (k.anchor + Vector3.up * 0.72f - pos).normalized;
                Vector3 fwd = (toAnchor * 0.7f - Wind * 0.3f).normalized;
                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(Mathf.Sin(t * 2.1f) * (2f + 4f * gust), 0f, roll);
                Matrix4x4 m = Matrix4x4.TRS(pos, rot, new Vector3(k.size, k.size, k.size));
                kiteBatch.Set(k.part, m);

                // string: stake top -> bridle, sagging under its own weight
                Vector3 a = k.anchor + Vector3.up * 0.72f;
                Vector3 b = m.MultiplyPoint3x4(new Vector3(0f, 0.1f, 0.12f));
                float sag = (b - a).magnitude * 0.045f;
                int n = k.linePts.Length;
                for (int j = 0; j < n; j++)
                {
                    float s = j / (float)(n - 1);
                    float bow = 4f * s * (1f - s);
                    k.linePts[j] = Vector3.Lerp(a, b, s) + Vector3.down * (sag * bow) + Wind * (sag * 0.4f * bow);
                }
                k.line.SetPositions(k.linePts);

                // ribbon tail streaming downwind from the kite's lower tip
                Vector3 tb = m.MultiplyPoint3x4(new Vector3(0f, k.tailTip, 0f));
                Vector3 kd = rot * Vector3.down;
                Vector3 dir = (kd * 0.45f + Vector3.down * 0.35f + Wind * 0.75f).normalized;
                int tn = k.tailPts.Length;
                for (int j = 0; j < tn; j++)
                {
                    float f = j / (float)(tn - 1);
                    k.tailPts[j] = tb + dir * (k.tailLen * f)
                                   + side * (Mathf.Sin(clock * 4.2f + k.phase - j * 0.8f) * 0.45f * f)
                                   + Vector3.up * (Mathf.Cos(clock * 3.1f + k.phase - j * 0.6f) * 0.25f * f);
                }
                k.tail.SetPositions(k.tailPts);
            }
            kiteBatch.Upload();
        }

        // ================================================================== lanterns
        class SkyLantern { public int part; public Vector3 origin; public float age, life, rise, phase, scale; }
        class FloatLantern { public int part; public Vector2 pos, target; public float heading, phase, scale; }

        LifeBatch glowBatch;
        Material glowMat;
        SkyLantern[] skyLanterns;
        FloatLantern[] floatLanterns;
        float glowSet = -1f;

        void BuildLanterns()
        {
            glowMat = new Material(MaterialLibrary.FindShader("BlossomRally/LifeGlow")) { name = "BR_LifeGlow" };
            glowBatch = new LifeBatch();
            int[] skyT =
            {
                glowBatch.AddTemplate(LifeShapes.SkyLantern(LifeGeo.C(236, 106, 64, 255), LifeGeo.C(255, 196, 120, 255))),
                glowBatch.AddTemplate(LifeShapes.SkyLantern(LifeGeo.C(222, 64, 52, 255), LifeGeo.C(255, 150, 96, 255))),
                glowBatch.AddTemplate(LifeShapes.SkyLantern(LifeGeo.C(244, 170, 76, 255), LifeGeo.C(255, 226, 150, 255))),
            };
            int floatT = glowBatch.AddTemplate(LifeShapes.FloatLantern());
            skyLanterns = new SkyLantern[12];
            for (int i = 0; i < skyLanterns.Length; i++)
            {
                var l = new SkyLantern { part = glowBatch.AddPart(skyT[i % skyT.Length]) };
                RespawnSky(l);
                l.age = Rand(0f, l.life * 0.92f);
                skyLanterns[i] = l;
            }
            floatLanterns = new FloatLantern[7];
            for (int i = 0; i < floatLanterns.Length; i++)
            {
                var f = new FloatLantern { part = glowBatch.AddPart(floatT) };
                f.pos = RandomLakePoint(0.8f);
                f.target = RandomLakePoint(0.8f);
                f.heading = Rand(0f, Mathf.PI * 2f);
                f.phase = Rand(0f, 50f);
                f.scale = Rand(1.4f, 1.8f);
                floatLanterns[i] = f;
            }
            glowBatch.Finish("Life_Lanterns", glowMat, transform, 0, false);
        }

        Vector2 RandomLakePoint(float kMax)
        {
            Vector2 c = MapData.LakeCenter, r = MapData.LakeRadii;
            for (int tries = 0; tries < 40; tries++)
            {
                float a = Rand(0f, Mathf.PI * 2f), k = Mathf.Sqrt(Rand(0f, 1f)) * kMax;
                Vector2 p = c + new Vector2(Mathf.Cos(a) * r.x * k, Mathf.Sin(a) * r.y * k);
                if (LifeShapes.ClearOfPavilion(p, 1.5f)) return p;
            }
            return c + new Vector2(r.x * 0.4f, r.y * 0.3f);
        }

        void RespawnSky(SkyLantern l)
        {
            if (rng.NextDouble() < 0.4)
            {
                // released just off the lake pavilion (outside its roof, which reaches ~5 m from the centre)
                Vector2 pc = LifeShapes.PavilionCentre;
                float a = Rand(-0.2f, Mathf.PI + 0.2f), r = Rand(6f, 7.5f);
                l.origin = new Vector3(pc.x + Mathf.Cos(a) * r, WorldBuilder.WaterY + Rand(2.2f, 3.2f), pc.y + Mathf.Sin(a) * r);
            }
            else
            {
                Vector2 p = RandomLakePoint(0.85f);
                l.origin = new Vector3(p.x, WorldBuilder.WaterY + Rand(1.5f, 4f), p.y);
            }
            l.age = 0f;
            l.life = Rand(55f, 85f);
            l.rise = Rand(0.45f, 0.8f);
            l.phase = Rand(0f, 100f);
            l.scale = Rand(1.05f, 1.35f);
        }

        void UpdateLanterns(float dt)
        {
            if (glowBatch == null) return;
            float glow = Mathf.Lerp(0.55f, 2.8f, Atmosphere.NightFactor);
            if (Mathf.Abs(glow - glowSet) > 0.01f) { glowSet = glow; glowMat.SetFloat("_Glow", glow); }
            for (int i = 0; i < skyLanterns.Length; i++)
            {
                var l = skyLanterns[i];
                l.age += dt;
                if (l.age > l.life) RespawnSky(l);
                float a = Mathf.Clamp01(l.age / 4f) * Mathf.Clamp01((l.life - l.age) / 8f);
                glowBatch.SetAlpha(l.part, (byte)(a * 255f));
                float t = clock + l.phase;
                Vector3 p = l.origin + Vector3.up * (l.rise * l.age) + Wind * (0.45f * l.age)
                            + new Vector3(Mathf.Sin(t * 0.4f) * 0.8f, Mathf.Sin(t * 0.9f) * 0.15f, Mathf.Cos(t * 0.33f) * 0.8f);
                Quaternion r = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 4f, l.phase * 40f + clock * 6f, Mathf.Cos(t * 0.6f) * 4f);
                glowBatch.Set(l.part, Matrix4x4.TRS(p, r, new Vector3(l.scale, l.scale, l.scale)));
            }
            for (int i = 0; i < floatLanterns.Length; i++)
            {
                var f = floatLanterns[i];
                Vector2 to = f.target - f.pos;
                if (to.sqrMagnitude < 1f) { f.target = RandomLakePoint(0.8f); to = f.target - f.pos; }
                float want = Mathf.Atan2(to.x, to.y);
                f.heading += Mathf.Clamp(Mathf.DeltaAngle(f.heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad, -0.3f * dt, 0.3f * dt);
                Vector2 np = f.pos + new Vector2(Mathf.Sin(f.heading), Mathf.Cos(f.heading)) * (0.22f * dt);
                if (MapData.LakeK(np) < 0.86f && LifeShapes.ClearOfPavilion(np, 1f)) f.pos = np;
                else f.target = RandomLakePoint(0.6f);
                float t = clock + f.phase;
                Vector3 p = new Vector3(f.pos.x, WorldBuilder.WaterY + 0.06f + Mathf.Sin(t * 1.3f) * 0.025f, f.pos.y);
                Quaternion r = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f, f.phase * 30f + clock * 4f, Mathf.Cos(t * 0.9f) * 3f);
                glowBatch.Set(f.part, Matrix4x4.TRS(p, r, new Vector3(f.scale, f.scale, f.scale)));
            }
            glowBatch.Upload();
        }
    }

    // ====================================================================== shared helpers
    /// <summary>Flat-shaded low-poly mesh template builder (vertex colours in sRGB; alpha meaning depends on the shader).</summary>
    internal sealed class LifeGeo
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Color32> c = new List<Color32>();
        public readonly List<int> t = new List<int>();
        public readonly List<float> bend = new List<float>();
        /// <summary>Optional per-face colouring by face centre (overrides the colours passed in).</summary>
        public System.Func<Vector3, Color32> Paint;

        public static Color32 C(int r, int g, int b, int a = 0) { return new Color32((byte)r, (byte)g, (byte)b, (byte)a); }

        public static Color32 Shade(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0f, 255f), (byte)Mathf.Clamp(c.g * k, 0f, 255f), (byte)Mathf.Clamp(c.b * k, 0f, 255f), c.a);
        }

        /// <summary>Triangle with Unity winding (clockwise seen from the front; normal = cross(b-a, c-a)).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 d, Color32 ca, Color32 cb, Color32 cd)
        {
            Vector3 nn = Vector3.Cross(b - a, d - a);
            float m = nn.magnitude;
            if (m < 1e-10f) return;
            nn /= m;
            if (Paint != null) { Color32 pc = Paint((a + b + d) / 3f); ca = pc; cb = pc; cd = pc; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            n.Add(nn); n.Add(nn); n.Add(nn);
            c.Add(ca); c.Add(cb); c.Add(cd);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void TriOut(Vector3 a, Vector3 b, Vector3 d, Vector3 outward, Color32 ca, Color32 cb, Color32 cd)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), outward) < 0f) Tri(a, d, b, ca, cd, cb);
            else Tri(a, b, d, ca, cb, cd);
        }

        public void Tri2(Vector3 a, Vector3 b, Vector3 d, Color32 ca, Color32 cb, Color32 cd)
        {
            Tri(a, b, d, ca, cb, cd);
            Tri(a, d, b, ca, cd, cb);
        }

        public void QuadOut(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Vector3 outward, Color32 ca, Color32 cb, Color32 cd, Color32 ce)
        {
            TriOut(a, b, d, outward, ca, cb, cd);
            TriOut(a, d, e, outward, ca, cd, ce);
        }

        /// <summary>Elliptical frustum / cylinder / cone from p0 to p1 (radii along two axes at each end).</summary>
        public void Frustum(Vector3 p0, Vector3 p1, float a0, float b0, float a1, float b1, int sides, float rot,
                            Color32 c0, Color32 c1, bool cap0, bool cap1)
        {
            Vector3 axis = p1 - p0;
            float len = axis.magnitude;
            if (len < 1e-6f) return;
            axis /= len;
            Vector3 u, w;
            if (Mathf.Abs(axis.y) > 0.99f) { u = Vector3.right; w = Vector3.forward; }
            else { u = Vector3.Cross(Vector3.up, axis).normalized; w = Vector3.Cross(axis, u); }
            float step = Mathf.PI * 2f / sides;
            for (int k = 0; k < sides; k++)
            {
                float t0 = rot + k * step, t1 = t0 + step;
                float cs0 = Mathf.Cos(t0), sn0 = Mathf.Sin(t0), cs1 = Mathf.Cos(t1), sn1 = Mathf.Sin(t1);
                Vector3 q00 = p0 + u * (cs0 * a0) + w * (sn0 * b0);
                Vector3 q01 = p0 + u * (cs1 * a0) + w * (sn1 * b0);
                Vector3 q10 = p1 + u * (cs0 * a1) + w * (sn0 * b1);
                Vector3 q11 = p1 + u * (cs1 * a1) + w * (sn1 * b1);
                Vector3 outward = u * Mathf.Cos(t0 + step * 0.5f) + w * Mathf.Sin(t0 + step * 0.5f);
                TriOut(q00, q01, q11, outward, c0, c0, c1);
                TriOut(q00, q11, q10, outward, c0, c1, c1);
                if (cap0) TriOut(p0, q00, q01, -axis, c0, c0, c0);
                if (cap1) TriOut(p1, q10, q11, axis, c1, c1, c1);
            }
        }

        public void Ellipsoid(Vector3 ctr, Vector3 r, int lon, int lat, Color32 top, Color32 bottom)
        {
            for (int j = 0; j < lat; j++)
            {
                float th0 = -Mathf.PI * 0.5f + Mathf.PI * j / lat, th1 = -Mathf.PI * 0.5f + Mathf.PI * (j + 1) / lat;
                for (int i = 0; i < lon; i++)
                {
                    float ph0 = Mathf.PI * 2f * i / lon, ph1 = Mathf.PI * 2f * (i + 1) / lon;
                    Vector3 a = E(ctr, r, th0, ph0), b = E(ctr, r, th0, ph1), d = E(ctr, r, th1, ph1), e = E(ctr, r, th1, ph0);
                    Color32 cl = Color32.Lerp(bottom, top, (j + 0.5f) / lat);
                    QuadOut(a, b, d, e, (a + b + d + e) * 0.25f - ctr, cl, cl, cl, cl);
                }
            }
        }

        static Vector3 E(Vector3 c, Vector3 r, float th, float ph)
        {
            return c + new Vector3(Mathf.Cos(th) * Mathf.Cos(ph) * r.x, Mathf.Sin(th) * r.y, Mathf.Cos(th) * Mathf.Sin(ph) * r.z);
        }

        public void Box(Vector3 ctr, Vector3 h, Color32 col)
        {
            Vector3 x = new Vector3(h.x, 0f, 0f), y = new Vector3(0f, h.y, 0f), z = new Vector3(0f, 0f, h.z);
            for (int s = -1; s <= 1; s += 2)
            {
                QuadOut(ctr + x * s - y - z, ctr + x * s + y - z, ctr + x * s + y + z, ctr + x * s - y + z, x * s, col, col, col, col);
                QuadOut(ctr + y * s - x - z, ctr + y * s + x - z, ctr + y * s + x + z, ctr + y * s - x + z, y * s, col, col, col, col);
                QuadOut(ctr + z * s - x - y, ctr + z * s + x - y, ctr + z * s + x + y, ctr + z * s - x + y, z * s, col, col, col, col);
            }
        }

        /// <summary>Flat disc (triangle fan) facing 'normal'.</summary>
        public void Disc(Vector3 ctr, float r, int sides, float rot, Vector3 normal, Color32 cc, Color32 ce)
        {
            Vector3 u = Mathf.Abs(normal.y) > 0.99f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 w = Vector3.Cross(normal, u);
            float step = Mathf.PI * 2f / sides;
            for (int k = 0; k < sides; k++)
            {
                float t0 = rot + k * step, t1 = t0 + step;
                TriOut(ctr, ctr + (u * Mathf.Cos(t0) + w * Mathf.Sin(t0)) * r, ctr + (u * Mathf.Cos(t1) + w * Mathf.Sin(t1)) * r, normal, cc, ce, ce);
            }
        }

        public void ComputeBend(System.Func<Vector3, float> weight)
        {
            bend.Clear();
            for (int i = 0; i < v.Count; i++) bend.Add(weight(v[i]));
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>
    /// Many small animated parts merged into one dynamic mesh (one draw call). Each part is a copy of a
    /// template posed by a matrix; only parts whose matrix changed are re-transformed. Optional per-part
    /// sideways "bend" wave (fish tails) and per-part vertex alpha (fading).
    /// </summary>
    internal sealed class LifeBatch
    {
        readonly List<LifeGeo> templates = new List<LifeGeo>();
        readonly List<int> partTemplate = new List<int>();
        Vector3[] srcV, srcN, v, n;
        Color32[] c;
        float[] srcBend, bendAmp, bendPhase;
        int[] start, count;
        Matrix4x4[] mats;
        bool[] dirty;
        bool anyDirty, colorsDirty;
        public float BendK = 9f;
        public Mesh Mesh { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public int PartCount { get { return partTemplate.Count; } }

        public int AddTemplate(LifeGeo g) { templates.Add(g); return templates.Count - 1; }
        public int AddPart(int template) { partTemplate.Add(template); return partTemplate.Count - 1; }

        public GameObject Finish(string name, Material mat, Transform parent, int layer, bool shadows)
        {
            int parts = partTemplate.Count, total = 0, totalIdx = 0;
            for (int p = 0; p < parts; p++) { total += templates[partTemplate[p]].v.Count; totalIdx += templates[partTemplate[p]].t.Count; }
            srcV = new Vector3[total]; srcN = new Vector3[total]; v = new Vector3[total]; n = new Vector3[total];
            c = new Color32[total]; srcBend = new float[total];
            start = new int[parts]; count = new int[parts]; mats = new Matrix4x4[parts]; dirty = new bool[parts];
            bendAmp = new float[parts]; bendPhase = new float[parts];
            var tris = new int[totalIdx];
            int vo = 0, io = 0;
            for (int p = 0; p < parts; p++)
            {
                var g = templates[partTemplate[p]];
                start[p] = vo;
                count[p] = g.v.Count;
                bool hasBend = g.bend.Count == g.v.Count;
                for (int i = 0; i < g.v.Count; i++)
                {
                    srcV[vo + i] = g.v[i];
                    srcN[vo + i] = g.n[i];
                    c[vo + i] = g.c[i];
                    srcBend[vo + i] = hasBend ? g.bend[i] : 0f;
                }
                for (int k = 0; k < g.t.Count; k++) tris[io + k] = g.t[k] + vo;
                vo += g.v.Count;
                io += g.t.Count;
                // matrices start all-zero: every vertex collapses to the origin (invisible) until posed
            }
            Mesh = new Mesh { name = name };
            if (total > 65000) Mesh.indexFormat = IndexFormat.UInt32;
            Mesh.MarkDynamic();
            Mesh.vertices = v;
            Mesh.normals = srcN;
            Mesh.colors32 = c;
            Mesh.triangles = tris;
            Mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = mat;
            Renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            Renderer.receiveShadows = shadows;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        public void Set(int part, Matrix4x4 m)
        {
            if (mats == null) return;
            mats[part] = m;
            dirty[part] = true;
            anyDirty = true;
        }

        public void SetBend(int part, float amp, float phase)
        {
            if (bendAmp == null) return;
            bendAmp[part] = amp;
            bendPhase[part] = phase;
        }

        public void SetAlpha(int part, byte a)
        {
            if (start == null) return;
            int s = start[part], e = s + count[part];
            if (e == s || c[s].a == a) return;
            for (int i = s; i < e; i++) c[i].a = a;
            colorsDirty = true;
        }

        public void Upload()
        {
            if (Mesh == null) return;
            if (anyDirty)
            {
                anyDirty = false;
                for (int p = 0; p < dirty.Length; p++)
                {
                    if (!dirty[p]) continue;
                    dirty[p] = false;
                    Matrix4x4 m = mats[p];
                    int s = start[p], e = s + count[p];
                    float amp = bendAmp[p];
                    if (amp != 0f)
                    {
                        float ph = bendPhase[p];
                        for (int i = s; i < e; i++)
                        {
                            Vector3 q = srcV[i];
                            q.x += amp * srcBend[i] * Mathf.Sin(ph - q.z * BendK);
                            v[i] = m.MultiplyPoint3x4(q);
                            n[i] = m.MultiplyVector(srcN[i]);
                        }
                    }
                    else
                    {
                        for (int i = s; i < e; i++)
                        {
                            v[i] = m.MultiplyPoint3x4(srcV[i]);
                            n[i] = m.MultiplyVector(srcN[i]);
                        }
                    }
                }
                Mesh.vertices = v;
                Mesh.normals = n;
                Mesh.RecalculateBounds();
            }
            if (colorsDirty)
            {
                colorsDirty = false;
                Mesh.colors32 = c;
            }
        }
    }

    /// <summary>Procedural low-poly creature / prop templates.</summary>
    internal static class LifeShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }

        public static Vector2 PavilionCentre
        {
            get { return new Vector2(MapData.LakeCenter.x, MapData.LakeCenter.y - (MapData.LakeRadii.y - 16f)); }
        }

        /// <summary>False inside the lake pavilion or over its zig-zag walkway to the south shore.</summary>
        public static bool ClearOfPavilion(Vector2 p, float margin)
        {
            Vector2 pc = PavilionCentre;
            float r = 4.4f + margin;
            if ((p - pc).sqrMagnitude < r * r) return false;
            if (p.y < pc.y && p.y > pc.y - 17f && Mathf.Abs(p.x - pc.x) < 3.4f + margin) return false;
            return true;
        }

        // ------------------------------------------------------------ birds (0 magpie, 1 sparrow, 2 swallow)
        public static LifeGeo BirdBody(int species)
        {
            Color32 back, belly, head, beak;
            switch (species)
            {
                case 0: back = C(34, 38, 56); belly = C(242, 242, 238); head = C(26, 28, 40); beak = C(30, 30, 34); break;
                case 1: back = C(150, 104, 68); belly = C(234, 216, 186); head = C(118, 78, 54); beak = C(80, 66, 54); break;
                default: back = C(40, 54, 104); belly = C(246, 240, 230); head = C(196, 64, 52); beak = C(36, 36, 40); break;
            }
            var g = new LifeGeo();
            g.Paint = p => p.z > 0.168f ? beak : (p.z > 0.115f ? head : (p.y > 0.064f ? back : belly));
            // rings: z, y, half width, half height (front to back)
            Vector4[] rings =
            {
                new Vector4(0.215f, 0.09f, 0f, 0f),
                new Vector4(0.17f, 0.098f, 0.03f, 0.03f),
                new Vector4(0.125f, 0.102f, 0.045f, 0.045f),
                new Vector4(0.07f, 0.074f, 0.06f, 0.062f),
                new Vector4(-0.04f, 0.064f, 0.055f, 0.05f),
                new Vector4(-0.11f, 0.07f, 0.028f, 0.022f),
            };
            for (int i = 0; i < rings.Length - 1; i++)
            {
                Vector4 a = rings[i + 1], b = rings[i];
                g.Frustum(new Vector3(0f, a.y, a.x), new Vector3(0f, b.y, b.x), a.z, a.w, b.z, b.w, 5, Mathf.PI * 0.5f, back, back, i == rings.Length - 2, false);
            }
            // tail (forked for swallows)
            g.Paint = null;
            Color32 tail = species == 0 ? C(30, 40, 70) : back;
            Vector3 bl = new Vector3(-0.022f, 0.072f, -0.1f), br = new Vector3(0.022f, 0.072f, -0.1f);
            float tl = species == 0 ? 0.3f : (species == 2 ? 0.27f : 0.2f);
            Vector3 tL = new Vector3(species == 2 ? -0.07f : -0.045f, 0.08f, -0.1f - tl), tR = new Vector3(-tL.x, tL.y, tL.z);
            if (species == 2)
            {
                Vector3 notch = new Vector3(0f, 0.076f, -0.18f);
                g.Tri2(bl, br, notch, tail, tail, tail);
                g.Tri2(bl, notch, tL, tail, tail, tail);
                g.Tri2(br, tR, notch, tail, tail, tail);
            }
            else
            {
                g.Tri2(bl, br, tR, tail, tail, tail);
                g.Tri2(bl, tR, tL, tail, tail, tail);
            }
            return g;
        }

        public static LifeGeo BirdWing(int species, int side)
        {
            Color32 inner, outer;
            switch (species)
            {
                case 0: inner = C(30, 34, 52); outer = C(236, 238, 240); break;
                case 1: inner = C(142, 96, 62); outer = C(96, 70, 50); break;
                default: inner = C(38, 50, 98); outer = C(26, 32, 66); break;
            }
            var g = new LifeGeo();
            float s = side;
            Vector3 rf = new Vector3(0f, 0f, 0.05f), rb = new Vector3(0f, 0f, -0.06f);
            Vector3 mf = new Vector3(0.14f * s, 0.01f, 0.035f), mb = new Vector3(0.13f * s, 0.005f, -0.07f);
            Vector3 tip = new Vector3(0.28f * s, 0f, -0.09f);
            g.Tri2(rf, mf, mb, inner, inner, inner);
            g.Tri2(rf, mb, rb, inner, inner, inner);
            g.Tri2(mf, tip, mb, outer, outer, outer);
            return g;
        }

        // ------------------------------------------------------------ villagers (feet at origin, facing +Z)
        public static LifeGeo VillagerBody(Color32 tunic, Color32 trim, Color32 trousers, int headStyle, Color32 hat)
        {
            var g = new LifeGeo();
            Color32 skin = C(242, 204, 170), hair = C(34, 28, 30), shoe = C(40, 34, 36);
            const float oct = Mathf.PI / 8f;
            // legs + shoes
            for (int s = -1; s <= 1; s += 2)
            {
                g.Frustum(new Vector3(0.085f * s, 0f, 0.02f), new Vector3(0.085f * s, 0.08f, 0.02f), 0.07f, 0.1f, 0.07f, 0.1f, 6, 0f, shoe, shoe, true, false);
                g.Frustum(new Vector3(0.085f * s, 0.08f, 0f), new Vector3(0.085f * s, 0.55f, 0f), 0.068f, 0.072f, 0.08f, 0.085f, 6, 0f, trousers, trousers, false, false);
            }
            // long tunic, hem band, sash, collar
            g.Frustum(new Vector3(0f, 0.4f, 0f), new Vector3(0f, 1.36f, 0f), 0.25f, 0.2f, 0.175f, 0.125f, 8, oct, tunic, LifeGeo.Shade(tunic, 1.08f), true, true);
            g.Frustum(new Vector3(0f, 0.38f, 0f), new Vector3(0f, 0.47f, 0f), 0.262f, 0.212f, 0.254f, 0.206f, 8, oct, trim, trim, true, false);
            g.Frustum(new Vector3(0f, 0.86f, 0f), new Vector3(0f, 0.95f, 0f), 0.218f, 0.168f, 0.212f, 0.162f, 8, oct, trim, trim, false, false);
            g.Frustum(new Vector3(0f, 1.33f, 0f), new Vector3(0f, 1.4f, 0f), 0.125f, 0.095f, 0.07f, 0.06f, 8, oct, trim, trim, false, true);
            // neck, head, eyes, hair
            g.Frustum(new Vector3(0f, 1.38f, 0f), new Vector3(0f, 1.47f, 0f), 0.05f, 0.05f, 0.05f, 0.05f, 6, 0f, skin, skin, false, false);
            g.Ellipsoid(new Vector3(0f, 1.56f, 0.005f), new Vector3(0.115f, 0.13f, 0.12f), 8, 5, skin, skin);
            Color32 cheek = C(240, 150, 140);
            for (int s = -1; s <= 1; s += 2)
            {
                // eyes and rosy cheeks: small quads just in front of the face
                Vector3 e = new Vector3(0.042f * s, 1.575f, 0.124f), k = new Vector3(0.062f * s, 1.528f, 0.118f);
                g.QuadOut(e + new Vector3(-0.011f, -0.015f, 0f), e + new Vector3(0.011f, -0.015f, 0f), e + new Vector3(0.011f, 0.015f, 0f), e + new Vector3(-0.011f, 0.015f, 0f), Vector3.forward, hair, hair, hair, hair);
                g.QuadOut(k + new Vector3(-0.018f, -0.01f, 0f), k + new Vector3(0.018f, -0.01f, 0f), k + new Vector3(0.018f, 0.01f, 0f), k + new Vector3(-0.018f, 0.01f, 0f), Vector3.forward, cheek, cheek, cheek, cheek);
            }
            g.Ellipsoid(new Vector3(0f, 1.6f, -0.014f), new Vector3(0.123f, 0.105f, 0.122f), 8, 4, hair, hair);
            switch (headStyle)
            {
                case 0:   // conical straw hat (douli)
                    g.Frustum(new Vector3(0f, 1.65f, 0f), new Vector3(0f, 1.85f, 0f), 0.36f, 0.36f, 0f, 0f, 10, 0f, hat, LifeGeo.Shade(hat, 1.1f), true, false);
                    break;
                case 1:   // hair bun with a red hairpin
                    g.Ellipsoid(new Vector3(0f, 1.71f, -0.06f), new Vector3(0.07f, 0.065f, 0.07f), 6, 4, hair, hair);
                    g.Box(new Vector3(0f, 1.72f, -0.06f), new Vector3(0.1f, 0.008f, 0.008f), C(214, 50, 44));
                    g.Ellipsoid(new Vector3(0.1f, 1.72f, -0.06f), new Vector3(0.018f, 0.018f, 0.018f), 4, 3, C(250, 150, 170), C(250, 150, 170));
                    break;
                default:  // scholar's cap
                    g.Frustum(new Vector3(0f, 1.63f, -0.01f), new Vector3(0f, 1.74f, -0.01f), 0.115f, 0.115f, 0.105f, 0.105f, 8, 0f, hat, hat, false, true);
                    g.Box(new Vector3(0f, 1.7f, -0.12f), new Vector3(0.11f, 0.02f, 0.01f), hat);
                    break;
            }
            return g;
        }

        /// <summary>Arm hanging down from a shoulder pivot at the origin.</summary>
        public static LifeGeo VillagerArm(Color32 tunic, Color32 trim)
        {
            var g = new LifeGeo();
            Color32 skin = C(242, 204, 170);
            g.Frustum(new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.4f, 0f), 0.06f, 0.06f, 0.078f, 0.078f, 6, 0f, tunic, tunic, true, true);
            g.Frustum(new Vector3(0f, -0.36f, 0f), new Vector3(0f, -0.41f, 0f), 0.082f, 0.082f, 0.082f, 0.082f, 6, 0f, trim, trim, false, true);
            g.Ellipsoid(new Vector3(0f, -0.47f, 0f), new Vector3(0.045f, 0.055f, 0.045f), 6, 4, skin, skin);
            return g;
        }

        // ------------------------------------------------------------ kites (in the local XY plane, nose +Y)
        public static LifeGeo KiteDiamond(Color32 a, Color32 b)
        {
            var g = new LifeGeo();
            Vector3 top = new Vector3(0f, 1f, 0f), right = new Vector3(0.72f, 0.18f, 0f), bot = new Vector3(0f, -0.9f, 0f), left = new Vector3(-0.72f, 0.18f, 0f);
            Vector3 mid = new Vector3(0f, 0.18f, 0.08f);
            g.Tri2(top, right, mid, a, a, a);
            g.Tri2(right, bot, mid, b, b, b);
            g.Tri2(bot, left, mid, a, a, a);
            g.Tri2(left, top, mid, b, b, b);
            // painted centre diamond + spars
            Color32 e = C(250, 246, 236);
            Vector3 et = new Vector3(0f, 0.42f, 0.075f), eb = new Vector3(0f, -0.08f, 0.085f);
            g.Tri2(et, new Vector3(0.2f, 0.18f, 0.1f), eb, e, e, e);
            g.Tri2(et, eb, new Vector3(-0.2f, 0.18f, 0.1f), e, e, e);
            Color32 spar = C(110, 80, 50);
            g.Frustum(new Vector3(0f, -0.9f, 0.02f), new Vector3(0f, 1f, 0.02f), 0.012f, 0.012f, 0.012f, 0.012f, 4, 0f, spar, spar, false, false);
            g.Frustum(new Vector3(-0.72f, 0.18f, 0.1f), new Vector3(0.72f, 0.18f, 0.1f), 0.012f, 0.012f, 0.012f, 0.012f, 4, 0f, spar, spar, false, false);
            return g;
        }

        /// <summary>Traditional Beijing "sha yan" swallow kite.</summary>
        public static LifeGeo KiteSwallow(Color32 body, Color32 accent)
        {
            var g = new LifeGeo();
            Color32 white = C(248, 244, 234), blue = C(60, 110, 190);
            System.Func<float, float, Vector3> P = (x, y) => new Vector3(x, y, Mathf.Abs(x) * 0.12f);
            // head
            g.Tri2(P(0f, 0.78f), P(0.2f, 0.46f), P(-0.2f, 0.46f), white, white, white);
            g.Tri2(P(-0.08f, 0.62f), P(0.08f, 0.62f), P(0f, 0.7f), body, body, body);
            // body
            g.Tri2(P(-0.2f, 0.46f), P(0.2f, 0.46f), P(0.15f, -0.2f), body, body, body);
            g.Tri2(P(-0.2f, 0.46f), P(0.15f, -0.2f), P(-0.15f, -0.2f), body, body, body);
            g.Tri2(P(-0.08f, 0.25f), P(0.08f, 0.25f), P(0f, -0.05f), accent, accent, accent);
            for (int s = -1; s <= 1; s += 2)
            {
                // wings: leading edge in the body colour, accent band, white trailing panel
                g.Tri2(P(0.2f * s, 0.46f), P(1.08f * s, 0.4f), P(0.62f * s, 0.24f), body, body, body);
                g.Tri2(P(0.2f * s, 0.46f), P(0.62f * s, 0.24f), P(0.2f * s, 0.12f), accent, accent, accent);
                g.Tri2(P(0.2f * s, 0.12f), P(0.62f * s, 0.24f), P(0.62f * s, 0.02f), white, white, white);
                g.Tri2(P(0.2f * s, 0.12f), P(0.62f * s, 0.02f), P(0.15f * s, -0.2f), white, white, white);
                g.Tri2(P(0.62f * s, 0.24f), P(1.08f * s, 0.4f), P(0.62f * s, 0.02f), blue, blue, blue);
                // forked tail
                g.Tri2(P(0.15f * s, -0.2f), P(0.32f * s, -0.92f), P(0f, -0.48f), accent, accent, accent);
            }
            g.Tri2(P(-0.15f, -0.2f), P(0.15f, -0.2f), P(0f, -0.48f), body, body, body);
            return g;
        }

        public static LifeGeo KiteStake()
        {
            var g = new LifeGeo();
            Color32 wood = C(130, 96, 60), reel = C(196, 60, 48);
            g.Frustum(new Vector3(0f, -0.1f, 0f), new Vector3(0f, 0.75f, 0f), 0.035f, 0.035f, 0.03f, 0.03f, 5, 0f, wood, wood, false, true);
            g.Frustum(new Vector3(-0.09f, 0.45f, 0f), new Vector3(0.09f, 0.45f, 0f), 0.08f, 0.08f, 0.08f, 0.08f, 8, 0f, reel, reel, true, true);
            return g;
        }

        // ------------------------------------------------------------ lanterns (alpha 255 for the glow shader)
        public static LifeGeo SkyLantern(Color32 top, Color32 bottom)
        {
            var g = new LifeGeo();
            const float oct = Mathf.PI / 8f;
            g.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.95f, 0f), 0.3f, 0.3f, 0.42f, 0.42f, 8, oct, bottom, top, false, true);
            // inside of the open bottom: bright flame light
            g.Disc(new Vector3(0f, 0.05f, 0f), 0.3f, 8, oct, Vector3.down, C(255, 246, 200, 255), C(255, 214, 140, 255));
            // rim hoop
            Color32 rim = C(150, 70, 40, 255);
            g.Frustum(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.03f, 0f), 0.31f, 0.31f, 0.31f, 0.31f, 8, oct, rim, rim, false, false);
            return g;
        }

        public static LifeGeo FloatLantern()
        {
            var g = new LifeGeo();
            Color32 pinkD = C(222, 96, 140, 255), pinkL = C(252, 176, 204, 255), pale = C(255, 226, 234, 255), flame = C(255, 238, 170, 255);
            g.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.1f, 0f), 0.15f, 0.15f, 0.25f, 0.25f, 6, 0f, pinkD, pinkL, true, false);
            g.Disc(new Vector3(0f, 0.09f, 0f), 0.25f, 6, 0f, Vector3.up, pale, pinkL);
            for (int k = 0; k < 6; k++)
            {
                float a0 = k * Mathf.PI / 3f, a1 = a0 + Mathf.PI / 3f, am = a0 + Mathf.PI / 6f;
                Vector3 l = new Vector3(Mathf.Cos(a0) * 0.25f, 0.1f, Mathf.Sin(a0) * 0.25f);
                Vector3 r = new Vector3(Mathf.Cos(a1) * 0.25f, 0.1f, Mathf.Sin(a1) * 0.25f);
                Vector3 tip = new Vector3(Mathf.Cos(am) * 0.31f, 0.24f, Mathf.Sin(am) * 0.31f);
                g.Tri2(l, r, tip, pinkL, pinkL, pinkD);
            }
            g.Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.035f, 0.06f, 0.035f), flame);
            return g;
        }
    }
}

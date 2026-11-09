// CarJuice.cs - per-car "juice" (added to every player car by JuiceDirector via WorldHooks.CarSpawned):
//  * near-miss detection: one oriented OverlapBoxNonAlloc per physics step above 45 km/h tracks solid
//    obstacles beside the car (trees, poles, walls, parked cars - not terrain / roads / grass / the car);
//    an obstacle that passed the car's side within 1.2 m at > 60 km/h without touching it raises
//    GameEvents.RaiseNearMiss(pos, clearance). Close passes also trigger a doppler fly-by whoosh.
//  * petal / leaf blasts: under cherry canopies, through tall grass and brushing past trees / bamboo
//  * water: big white splash sheets + droplets when wheels cross the river / lake shallows (WaterY) or the
//    deep puddles the road shader draws in the wet (same noise field), with a splash sound
//  * lingering dust clouds on gravel / dirt that drift downwind for several seconds (thinner in rain)
//  * richer gamepad rumble: landing thump (two beats), bump / kerb ticks, gravel crackle, dirt lumps,
//    rev-limiter buzz and drift-angle feedback (refreshed every frame, before InputReader's driver runs)
// Exposes the surface / brush / water state for CarJuiceAudio. Everything idles outside drive mode,
// in replays and while paused. No per-frame allocations.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(-50)]          // LateUpdate before InputReader's rumble driver (order 0)
    [RequireComponent(typeof(ArcadeCar))]
    public class CarJuice : MonoBehaviour
    {
        ArcadeCar car;
        Rigidbody rb;
        CarJuiceAudio sound;
        PetalWeather weather;
        float halfX = 0.9f, halfZ = 2.1f, centerZ, roofY = 1.35f;

        // ---- state shared with CarJuiceAudio ----
        public bool Live { get; private set; }
        public float GravelFrac { get; private set; }
        public float DirtFrac { get; private set; }
        public float GrassFrac { get; private set; }
        public float Brush { get; private set; }
        public byte BrushKind { get; private set; }
        public float TallGrass { get; private set; }
        public float Water { get; private set; }

        // per wheel
        float[] prevSpring = new float[0];
        bool[] wheelWet = new bool[0];
        float[] sheetAcc = new float[0], dropAcc = new float[0];

        float dustAcc, petalAcc, grassAcc, brushAcc, lastSplash = -10f;

        // rumble
        float bumpTick, tickCooldown, landTimer2, landK2, limiterHold, lastRpm, rpmRate, grainTimer, grain;

        // near miss tracking
        const int MaxTracks = 12;
        struct Track
        {
            public Collider col;
            public float minClear, maxKmh;
            public Vector3 minPos;
            public bool side, hit, whooshed;
            public int seen;
        }
        readonly Track[] tracks = new Track[MaxTracks];
        int trackCount, step;
        readonly Collider[] hits = new Collider[24];
        readonly Collider[] recent = new Collider[8];
        readonly float[] recentTime = new float[8];
        int recentIdx;
        Collider lastContact;
        float lastContactTime = -10f;

        const float NearMissClearance = 1.2f, NearMissKmh = 60f, WhooshClearance = 2.4f;
        static int obstacleMask;

        static readonly Color32[] PetalCols =
        {
            new Color32(255, 196, 214, 255), new Color32(255, 176, 200, 255), new Color32(255, 232, 238, 255),
            new Color32(255, 214, 226, 255), new Color32(250, 246, 246, 255)
        };
        static readonly Color32[] GrassCols =
        {
            new Color32(112, 168, 64, 255), new Color32(146, 196, 84, 255), new Color32(92, 140, 52, 255),
            new Color32(180, 210, 110, 255), new Color32(240, 220, 120, 255)
        };
        static readonly Color32[] LeafCols =
        {
            new Color32(98, 150, 60, 255), new Color32(130, 182, 76, 255), new Color32(170, 200, 96, 255)
        };

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            rb = car.Body != null ? car.Body : GetComponent<Rigidbody>();
            sound = GetComponent<CarJuiceAudio>();
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                halfX = box.size.x * 0.5f / 0.92f;
                halfZ = box.size.z * 0.5f / 0.95f;
                centerZ = box.center.z;
                roofY = box.center.y + box.size.y * 0.5f;
            }
            if (obstacleMask == 0)
                obstacleMask = ~((1 << CarExtras.CarLayer) | (1 << WorldBuilder.LayerGrass) | (1 << 2) | (1 << 4) | (1 << 5));
#if UNITY_2022_2_OR_NEWER
            weather = Object.FindAnyObjectByType<PetalWeather>();
#else
            weather = Object.FindObjectOfType<PetalWeather>();
#endif
        }

        void OnEnable() { GameEvents.Landed += OnLanded; }
        void OnDisable() { GameEvents.Landed -= OnLanded; trackCount = 0; }

        bool ComputeLive()
        {
            var gm = GameManager.Instance;
            if (car == null || rb == null || gm == null || !gm.IsDriving || gm.Car != car || rb.isKinematic) return false;
            if (ReplaySystem.Instance != null && ReplaySystem.Instance.Active) return false;
            return true;
        }

        void EnsureWheels(int n)
        {
            if (prevSpring.Length == n) return;
            prevSpring = new float[n];
            wheelWet = new bool[n];
            sheetAcc = new float[n];
            dropAcc = new float[n];
            for (int i = 0; i < n; i++) prevSpring[i] = car.wheels[i].springLength;
        }

        void OnCollisionEnter(Collision c)
        {
            lastContact = c.collider;
            lastContactTime = Time.time;
            for (int i = 0; i < trackCount; i++) if (tracks[i].col == c.collider) tracks[i].hit = true;
        }

        void OnCollisionStay(Collision c)
        {
            if (c.collider == lastContact) lastContactTime = Time.time;
        }

        void OnLanded(float air, float vy)
        {
            if (!Live || !car.controlsEnabled) return;
            float k = Mathf.Clamp01(Mathf.Abs(vy) / 9f) * Mathf.Clamp01(air / 0.35f);
            if (k < 0.05f) return;
            InputReader.Rumble(0.3f + 0.6f * k, 0.1f + 0.3f * k, 0.1f + 0.15f * k);
            landTimer2 = 0.14f;                         // "ba-dum": the rebound of the suspension
            landK2 = k;
        }

        // ------------------------------------------------------------------ near misses
        void FixedUpdate()
        {
            if (!Live) { trackCount = 0; return; }
            float kmh = car.SpeedKmh;
            if (kmh < 45f) { trackCount = 0; return; }
            step++;
            Transform tr = transform;
            Vector3 centre = tr.TransformPoint(new Vector3(0f, 0.95f, centerZ));
            Vector3 half = new Vector3(halfX + WhooshClearance + 0.2f, 0.75f, halfZ + 1.2f);
            int n = Physics.OverlapBoxNonAlloc(centre, half, hits, tr.rotation, obstacleMask, QueryTriggerInteraction.Ignore);
            float baseY = tr.position.y;
            for (int i = 0; i < n; i++)
            {
                Collider col = hits[i];
                hits[i] = null;
                if (col == null || col is MeshCollider || col is TerrainCollider) continue;
                if (rb != null && col.attachedRigidbody == rb) continue;
                Bounds b = col.bounds;
                if (b.max.y < baseY + 0.4f) continue;                          // kerbs, low edging
                if (b.size.x > 40f || b.size.z > 40f) continue;                // map boundary walls
                Vector3 cp = col.ClosestPoint(centre);
                Vector3 lp = tr.InverseTransformPoint(cp);
                float ax = Mathf.Abs(lp.x), az = Mathf.Abs(lp.z - centerZ);
                float dx = Mathf.Max(0f, ax - halfX), dz = Mathf.Max(0f, az - halfZ);
                float clear = Mathf.Sqrt(dx * dx + dz * dz);
                bool side = ax > halfX - 0.05f && az < halfZ + 0.5f;
                int ti = FindTrack(col);
                if (ti < 0)
                {
                    if (trackCount >= MaxTracks) continue;
                    ti = trackCount++;
                    tracks[ti] = new Track { col = col, minClear = 99f, hit = col == lastContact && Time.time - lastContactTime < 0.5f };
                }
                tracks[ti].seen = step;
                if (kmh > tracks[ti].maxKmh) tracks[ti].maxKmh = kmh;
                if (clear < tracks[ti].minClear)
                {
                    tracks[ti].minClear = clear;
                    tracks[ti].minPos = cp;
                    tracks[ti].side = side;
                }
                if (!tracks[ti].whooshed && side && clear < WhooshClearance && kmh > 50f)
                {
                    tracks[ti].whooshed = true;
                    if (sound != null) sound.Whoosh(cp, kmh, clear);
                }
            }
            // obstacles that left the zone: judge them
            for (int i = trackCount - 1; i >= 0; i--)
            {
                if (tracks[i].seen == step) continue;
                Track t = tracks[i];
                tracks[i] = tracks[--trackCount];
                tracks[trackCount].col = null;
                if (t.col == null || t.hit || !t.side || t.minClear > NearMissClearance || t.minClear < 0.04f || t.maxKmh < NearMissKmh) continue;
                if (t.col == lastContact && Time.time - lastContactTime < 1f) continue;
                if (JuiceDirector.Instance != null && JuiceDirector.Instance.SinceImpact < 1f) continue;
                if (Recent(t.col)) continue;
                recent[recentIdx] = t.col;
                recentTime[recentIdx] = Time.time;
                recentIdx = (recentIdx + 1) % recent.Length;
                GameEvents.RaiseNearMiss(t.minPos, t.minClear);
            }
        }

        int FindTrack(Collider c)
        {
            for (int i = 0; i < trackCount; i++) if (tracks[i].col == c) return i;
            return -1;
        }

        bool Recent(Collider c)
        {
            for (int i = 0; i < recent.Length; i++) if (recent[i] == c && Time.time - recentTime[i] < 4f) return true;
            return false;
        }

        // ------------------------------------------------------------------ effects
        void Update()
        {
            Live = ComputeLive();
            float dt = Time.deltaTime;
            if (!Live || dt <= 0f)
            {
                if (!Live) { GravelFrac = DirtFrac = GrassFrac = Brush = TallGrass = Water = 0f; }
                return;
            }
            var fx = JuiceFX.Instance;
            float density = JuiceFX.Density;
            Vector3 vel = rb.GetVelocity();
            float speed = vel.magnitude, kmh = car.SpeedKmh;
            float wet = Atmosphere.Wetness, rain = Atmosphere.RainIntensity;
            Transform tr = transform;
            var wheels = car.wheels;
            int n = wheels.Length;
            EnsureWheels(n);

            // ---- wheels: bumps, surfaces, water
            int grounded = 0;
            float g = 0f, d = 0f, gr = 0f, water = 0f;
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                float comp = prevSpring[i] - w.springLength;
                prevSpring[i] = w.springLength;
                if (w.grounded && comp > 0.01f) bumpTick = Mathf.Max(bumpTick, Mathf.Clamp01((comp - 0.01f) * 30f));
                if (w.grounded)
                {
                    grounded++;
                    if (w.surface == Surface.Gravel) g += 1f;
                    else if (w.surface == Surface.Dirt) d += 1f;
                    else if (w.surface == Surface.Grass) gr += 1f;
                }

                Vector3 wp = w.grounded ? w.contact : tr.TransformPoint(w.local) - Vector3.up * w.radius;
                float depth = WorldBuilder.WaterY + 0.06f - wp.y;
                float amt = 0f;
                if (depth > 0f) amt = Mathf.Clamp01(0.5f + depth * 2.5f);
                else if (w.grounded && wet > 0.6f)
                {
                    float pud = JuiceWorld.Puddle(w.contact.x, w.contact.z, w.surface);
                    if (pud > 0.45f) amt = pud * 0.55f;
                }
                if (amt > water) water = amt;
                bool isWet = amt > 0.05f;
                if (fx != null && isWet)
                {
                    Vector3 sp = wp;
                    if (depth > 0f) sp.y = WorldBuilder.WaterY + 0.03f;
                    Vector3 side = tr.right * (w.local.x >= 0f ? 1f : -1f);
                    if (!wheelWet[i] && speed > 5f && Time.time - lastSplash > 0.25f)
                    {
                        lastSplash = Time.time;
                        float big = Mathf.Clamp01(amt * speed / 16f);
                        SplashBurst(fx, sp, side, vel, big, density);
                        if (sound != null) sound.Splash(sp, big);
                    }
                    if (speed > 3f) SplashStream(fx, i, sp, side, vel, amt, speed, w.front, density, dt);
                }
                wheelWet[i] = isWet;
            }
            float inv = grounded > 0 ? 1f / n : 0f;
            GravelFrac = g * inv; DirtFrac = d * inv; GrassFrac = gr * inv;
            Water = water;

            // ---- surroundings: blossom canopies, tall grass, branches
            float blossom, tall, brush;
            byte kind;
            Vector3 brushDir;
            JuiceWorld.Sample(tr.position, halfX, out blossom, out tall, out brush, out kind, out brushDir);
            TallGrass = grounded > 0 ? tall : 0f;
            Brush = speed > 1.5f ? brush : 0f;
            BrushKind = kind;

            if (fx == null) return;
            Vector3 fwd = tr.forward, right = tr.right;

            // ---- lingering dust on gravel / dirt
            float dustThin = (1f - 0.85f * rain) * (1f - 0.75f * Mathf.Clamp01((wet - 0.25f) / 0.5f));
            float dustSurf = GravelFrac + DirtFrac * 1.15f;
            if (dustThin > 0.05f && dustSurf > 0f && kmh > 22f)
            {
                dustAcc += dt * dustSurf * Mathf.Clamp01((kmh - 18f) / 90f) * (9f + 14f * car.DriftIntensity) * density * dustThin;
                int guard = 0;
                while (dustAcc >= 1f && guard++ < 4)
                {
                    dustAcc -= 1f;
                    var w = wheels[Random.Range(0, n)];
                    if (!w.grounded || (w.surface != Surface.Gravel && w.surface != Surface.Dirt)) continue;
                    bool dirt = w.surface == Surface.Dirt;
                    float a = Random.Range(0.17f, 0.27f) * dustThin;
                    Color32 c = dirt ? new Color32(196, 166, 128, (byte)(a * 255f)) : new Color32(232, 218, 192, (byte)(a * 255f));
                    Vector3 p = w.contact + Vector3.up * Random.Range(0.4f, 0.8f) - fwd * 0.6f + right * Random.Range(-0.4f, 0.4f);
                    Vector3 v = vel * 0.04f + Vector3.up * Random.Range(0.15f, 0.5f) + Random.insideUnitSphere * 0.45f;
                    fx.Dust(p, v, c, Random.Range(2.2f, 3.6f) * (0.8f + 0.4f * Mathf.Clamp01(kmh / 120f)));
                }
            }
            else dustAcc = 0f;

            // ---- petals blasted up behind the car under cherry canopies
            int level = weather != null ? weather.level : 1;
            float petalRate = blossom * Mathf.Clamp01((kmh - 30f) / 90f) * (35f + 30f * level) * density * (1f - 0.6f * wet);
            petalAcc += dt * petalRate;
            for (int guard = 0; petalAcc >= 1f && guard < 6; guard++)
            {
                petalAcc -= 1f;
                Vector3 p = tr.TransformPoint(new Vector3(Random.Range(-halfX, halfX), Random.Range(0.1f, 0.5f), centerZ - halfZ - 0.2f));
                Vector3 swirl = Vector3.Cross(Vector3.up, p - tr.position).normalized * Random.Range(-3f, 3f);
                Vector3 v = vel * 0.55f + Vector3.up * Random.Range(1.5f, 4.5f) + right * Random.Range(-2f, 2f) + swirl;
                fx.Petal(p, v, PetalCols[Random.Range(0, PetalCols.Length)], Random.Range(0.08f, 0.15f));
            }
            if (petalRate <= 0f) petalAcc = 0f;

            // ---- grass blades / seeds kicked up through tall grass and meadow
            float grassRate = Mathf.Max(TallGrass, GrassFrac * 0.35f) * Mathf.Clamp01((kmh - 25f) / 70f) * 55f * density;
            grassAcc += dt * grassRate;
            for (int guard = 0; grassAcc >= 1f && guard < 6; guard++)
            {
                grassAcc -= 1f;
                var w = wheels[Random.Range(0, n)];
                Vector3 p = (w.grounded ? w.contact : tr.TransformPoint(w.local)) + Vector3.up * 0.25f;
                Vector3 v = vel * 0.3f + Vector3.up * Random.Range(2f, 4.5f) - fwd * Random.Range(1f, 3f) + Random.insideUnitSphere * 1.2f;
                fx.Petal(p, v, GrassCols[Random.Range(0, GrassCols.Length)], Random.Range(0.07f, 0.13f));
            }
            if (grassRate <= 0f) grassAcc = 0f;

            // ---- leaves / petals shaken off branches and bamboo the car brushes past
            float brushRate = Brush * Mathf.Clamp01(kmh / 40f) * 60f * density;
            brushAcc += dt * brushRate;
            for (int guard = 0; brushAcc >= 1f && guard < 6; guard++)
            {
                brushAcc -= 1f;
                Vector3 p = tr.position + brushDir * (halfX + 0.25f) + Vector3.up * Random.Range(0.6f, roofY + 0.5f) + fwd * Random.Range(-halfZ, halfZ);
                Vector3 v = vel * 0.25f + Vector3.up * Random.Range(0.4f, 2f) + Random.insideUnitSphere * 1.5f;
                Color32 c = kind == JuiceWorld.KindCherry && Random.value < 0.75f ? PetalCols[Random.Range(0, PetalCols.Length)] : LeafCols[Random.Range(0, LeafCols.Length)];
                fx.Petal(p, v, c, Random.Range(0.09f, 0.16f));
            }
            if (brushRate <= 0f) brushAcc = 0f;
        }

        static void SplashBurst(JuiceFX fx, Vector3 p, Vector3 side, Vector3 vel, float big, float density)
        {
            int sheets = (int)((5f + 10f * big) * Mathf.Max(0.5f, density));
            int drops = (int)((12f + 30f * big) * Mathf.Max(0.5f, density));
            for (int i = 0; i < sheets; i++)
            {
                Vector3 v = side * Random.Range(1.5f, 4.5f) * (0.5f + big) + Vector3.up * Random.Range(3f, 6.5f) * (0.5f + 0.6f * big) + vel * 0.3f;
                fx.Sheet(p + Random.insideUnitSphere * 0.3f, v, Random.Range(0.9f, 1.8f) * (0.7f + 0.5f * big), Random.Range(0.55f, 0.8f));
            }
            for (int i = 0; i < drops; i++)
            {
                Vector3 v = (side * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere) * Random.Range(1.5f, 4f) * (0.6f + big)
                            + Vector3.up * Random.Range(2.5f, 6f) + vel * 0.35f;
                fx.Droplet(p + Random.insideUnitSphere * 0.3f, v, Random.Range(0.04f, 0.09f));
            }
        }

        void SplashStream(JuiceFX fx, int i, Vector3 p, Vector3 side, Vector3 vel, float amt, float speed, bool front, float density, float dt)
        {
            float k = amt * Mathf.Clamp01(speed / 18f);
            sheetAcc[i] += dt * k * (front ? 26f : 12f) * density;
            dropAcc[i] += dt * k * (front ? 70f : 35f) * density;
            for (int guard = 0; sheetAcc[i] >= 1f && guard < 6; guard++)
            {
                sheetAcc[i] -= 1f;
                // bow wave: a V of white sheets thrown out sideways and up from the tyre
                Vector3 v = side * Random.Range(1.5f, 3.5f) * (0.4f + 0.6f * k) + Vector3.up * Random.Range(2f, 4.5f) * (0.5f + 0.5f * k) + vel * 0.35f;
                fx.Sheet(p + Random.insideUnitSphere * 0.2f, v, Random.Range(0.6f, 1.3f) * (0.7f + 0.5f * amt), Random.Range(0.4f, 0.7f));
            }
            for (int guard = 0; dropAcc[i] >= 1f && guard < 10; guard++)
            {
                dropAcc[i] -= 1f;
                Vector3 v = (side * Random.Range(0.3f, 1.2f) + Random.insideUnitSphere * 0.8f) * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(1.5f, 5f) + vel * 0.4f;
                fx.Droplet(p + Random.insideUnitSphere * 0.25f, v, Random.Range(0.035f, 0.08f));
            }
        }

        // ------------------------------------------------------------------ rumble
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (!Live || !car.controlsEnabled || dt <= 0f) { bumpTick = 0f; return; }
            float now = Time.unscaledTime;
            float kmh = car.SpeedKmh;
            float k = Mathf.Clamp01(kmh / 90f);
            grainTimer -= Time.unscaledDeltaTime;
            if (grainTimer <= 0f) { grainTimer = Random.Range(0.025f, 0.06f); grain = Random.value; }

            float lo = 0f, hi = 0f;
            // gravel: crackly high-frequency texture
            hi += GravelFrac * k * (0.04f + 0.16f * grain * grain);
            lo += GravelFrac * k * 0.06f;
            // dirt: lumpy low-frequency
            lo += DirtFrac * k * (0.06f + 0.11f * Mathf.PerlinNoise(now * 5f, 0.37f));
            hi += DirtFrac * k * 0.03f;
            // grass: soft
            lo += GrassFrac * k * 0.06f;
            hi += GrassFrac * k * 0.015f;
            // branches / tall grass scraping
            hi += Mathf.Max(Brush, TallGrass * 0.5f) * Mathf.Clamp01(kmh / 30f) * 0.12f * (0.5f + grain);
            // water drag
            lo += Water * Mathf.Clamp01(kmh / 40f) * 0.3f;
            // drift-angle feedback: buzz grows with angle, a soft pulse warns near the limit
            if (car.IsDrifting && car.Grounded)
            {
                float a = Mathf.Clamp01(Mathf.Abs(car.DriftAngle) / 60f);
                hi = Mathf.Max(hi, 0.05f + 0.22f * a);
                if (a > 0.7f) lo += 0.12f * (a - 0.7f) / 0.3f * (0.5f + 0.5f * Mathf.Sin(now * 18.8f));
            }
            // rev limiter buzz
            float rpm = car.EngineRpm;
            float rate = (rpm - lastRpm) / dt;
            lastRpm = rpm;
            rpmRate = Mathf.Lerp(rpmRate, rate, Mathf.Clamp01(dt * 10f));
            if (car.throttle > 0.85f && rpm >= 6900f && Mathf.Abs(rpmRate) < 1500f) limiterHold += dt;
            else limiterHold = 0f;
            if (limiterHold > 0.1f)
            {
                bool on = ((int)(now * 26f) & 1) == 0;
                hi = Mathf.Max(hi, on ? 0.32f : 0.06f);
                if (on) lo += 0.06f;
            }
            // bump / kerb ticks
            if (tickCooldown > 0f) tickCooldown -= dt;
            if (bumpTick > 0.05f && tickCooldown <= 0f)
            {
                InputReader.Rumble(0.08f + 0.3f * bumpTick, 0.2f + 0.4f * bumpTick, 0.045f + 0.03f * bumpTick);
                tickCooldown = 0.07f;
            }
            bumpTick = 0f;
            // second beat of the landing thump
            if (landTimer2 > 0f)
            {
                landTimer2 -= dt;
                if (landTimer2 <= 0f) InputReader.Rumble(0.45f * landK2, 0.1f * landK2, 0.08f);
            }
            if (lo > 0.005f || hi > 0.005f) InputReader.RumbleContinuous(Mathf.Min(lo, 0.8f), Mathf.Min(hi, 0.8f));
        }
    }
}

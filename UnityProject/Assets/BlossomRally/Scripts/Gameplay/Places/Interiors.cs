// Interiors.cs - the pocket-interior runtime of the Places module:
//  * Enter(place): fade to black, build the interior lazily (first visit), teleport the player person into the
//    pocket space far below the building (Place.PocketY), fade in. Exit() reverses it at the building's door.
//  * While inside: the sun is switched off (walls cannot leak it on Low, which has no shadows), ambient light / fog /
//    camera far plane get interior values (restored on exit; changes made by Atmosphere meanwhile are respected),
//    rain / petals keep following a proxy at the door outside and the Frost Summit snowfall is hidden.
//  * Camera: runs LAST (execution order 30000). Whatever camera the on-foot module drives is clamped to a short
//    orbit with collision against the room; if no camera follows the person (none exists yet / it lags behind a
//    teleport), a simple follow camera takes over. Sitting and viewpoints use a slow cinematic orbit.
//  * Sitting (Sit pose, steam over the cup), viewpoint visits, bell / incense / wishes are driven from here.
// Other modules can read Interiors.Inside / CurrentPlace / CameraMaxDistance and listen to Entered / Exited / Teleported.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>A seat the player can take (local to the interior).</summary>
    internal sealed class Seat
    {
        public Vector3 local, standLocal, focusLocal;
        public float yawLocal;
        public bool steam;
        public Vector3 steamLocal;
    }

    /// <summary>One built pocket interior.</summary>
    internal sealed class InteriorSpace
    {
        public Place place;
        public Transform root;
        public Vector3 origin;
        public Quaternion rot = Quaternion.identity;
        public float W = 12f, D = 10f, H = 3.6f;
        public Vector3 spawnLocal;
        public float camDist = 3.2f;
        public Color ambSky = new Color(0.55f, 0.45f, 0.36f), ambEq = new Color(0.5f, 0.4f, 0.32f), ambGround = new Color(0.3f, 0.24f, 0.2f);
        public readonly List<Light> lights = new List<Light>();
        public readonly List<float> lightBase = new List<float>();
        public readonly List<Vector3> steamPts = new List<Vector3>();      // world
        public readonly List<Vector3> embers = new List<Vector3>();        // world (fireplace)
        public Light fireLight;
        public Transform bell, striker;
        public float bellT = 99f;
        public Vector3 incensePt; public float incenseT;
        public GameObject incenseStick;
        public AudioSource audio;
        public bool steamAll = true;                                       // guests' cups steam too (not only the player's)

        public Vector3 W2(Vector3 local) { return origin + rot * local; }
        public float Yaw(float localYaw) { return rot.eulerAngles.y + localYaw; }
        public Vector3 Local(Vector3 world) { return Quaternion.Inverse(rot) * (world - origin); }
    }

    [DefaultExecutionOrder(30000)]
    public class Interiors : MonoBehaviour
    {
        public static Interiors Instance { get; private set; }
        /// <summary>True while the player is inside a pocket interior.</summary>
        public static bool Inside { get { return Instance != null && Instance.current != null; } }
        public static Place CurrentPlace { get { return Instance != null && Instance.current != null ? Instance.current.place : null; } }
        /// <summary>Suggested maximum orbit distance for an on-foot camera while inside (0 when outside).</summary>
        public static float CameraMaxDistance { get { return Inside ? Instance.current.camDist : 0f; } }
        /// <summary>True while this module drives the camera itself (sitting, viewpoint, fallback follow).</summary>
        public static bool CameraOverride { get; private set; }
        /// <summary>True while sitting at a table / by the fire.</summary>
        public static bool Sitting { get { return Instance != null && Instance.seat != null; } }
        public static event System.Action<Place> Entered, Exited;
        /// <summary>The person was moved by this module (position, yaw) - cameras may snap.</summary>
        public static event System.Action<Vector3, float> Teleported;

        internal static Interiors Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Interiors");
            Instance = go.AddComponent<Interiors>();
            Instance.useGUILayout = false;
            return Instance;
        }

        InteriorSpace current;
        Transform pocketRoot, proxy;

        // ---- transitions
        float fade, fadeTarget, fadeSpeed = 2.4f;
        System.Action atBlack;
        float holdT;
        bool menuHeld;
        public static bool Busy { get { return Instance != null && (Instance.atBlack != null || Instance.fade > 0.02f && Instance.fadeTarget > 0f); } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ================================================================== public API
        internal void Enter(Place p)
        {
            if (p == null || Busy || current != null || GameplayHooks.MenuOpen) return;
            var person = PlayerState.Person;
            if (person == null || !PlayerState.OnFoot) return;
            StartFade(() =>
            {
                if (p.interior == null) p.interior = InteriorLayouts.Build(p, PocketRoot());
                if (p.interior == null) return;
                current = p.interior;
                current.root.gameObject.SetActive(true);
                ApplyLightQuality();
                var pr = PlayerState.Person;
                Vector3 sp = current.W2(current.spawnLocal);
                TeleportPerson(pr, sp, current.Yaw(0f));
                SetupOutside(p);
                envActive = true;
                ApplyEnv();
                fbInit = false;
                bannerT = 0f;
                if (Entered != null) Entered(p);
            });
        }

        internal void Exit()
        {
            if (current == null || Busy) return;
            StartFade(() => { LeaveTo(current.place.Door, current.place.OutYaw); });
        }

        void LeaveTo(Vector3 pos, float yaw)
        {
            var p = current != null ? current.place : null;
            if (seat != null) { seat = null; SetMenu(false); }
            Cleanup();
            var pr = PlayerState.Person;
            if (pr != null) TeleportPerson(pr, GroundAt(pos), yaw);
            if (p != null && Exited != null) Exited(p);
        }

        /// <summary>Leave the interior state without moving the person (they were moved by someone else).</summary>
        void Cleanup()
        {
            if (current != null && current.root != null) current.root.gameObject.SetActive(false);
            current = null;
            seat = null;
            envActive = false;
            RestoreEnv();
            CameraOverride = false;
        }

        internal void Sit(InteriorSpace sp, Seat s)
        {
            if (s == null || Busy || seat != null || sp != current) return;
            fadeSpeed = 4f;
            StartFade(() =>
            {
                seat = s;
                seatT = 0f;
                SetMenu(true);
                PinSeat();
                orbit = sp.Yaw(s.yawLocal) + 180f;
            });
        }

        void Stand()
        {
            if (seat == null || Busy) return;
            fadeSpeed = 4f;
            var s = seat;
            StartFade(() =>
            {
                seat = null;
                SetMenu(false);
                var pr = PlayerState.Person;
                if (pr != null && current != null)
                {
                    TeleportPerson(pr, current.W2(s.standLocal), current.Yaw(s.yawLocal));
                    SetPose(pr, PersonPose.Idle);
                }
                fbInit = false;
            });
        }

        /// <summary>Leave the interior to the place's viewpoint and play a short panning shot there.</summary>
        internal void Viewpoint(Place p)
        {
            if (p == null || !p.HasViewpoint || Busy) return;
            StartFade(() =>
            {
                Cleanup();
                var pr = PlayerState.Person;
                if (pr != null) TeleportPerson(pr, GroundAt(p.Viewpoint), p.ViewpointYaw);
                if (Exited != null) Exited(p);
                cineT = 0f;
                cineYaw = p.ViewpointYaw;
                cineActive = true;
                string key = "BR_Viewpoint_" + p.id;
                bool first = true;
                try { first = PlayerPrefs.GetInt(key, 0) == 0; PlayerPrefs.SetInt(key, 1); } catch (System.Exception) { }
                if (first) { Wallet.Add(30, "Viewpoint"); PlacesManager.Toast("Viewpoint discovered   +" + Wallet.Format(30)); }
                else PlacesManager.Toast("The view from " + p.name);
            });
        }

        // ================================================================== interior actions
        internal void RingBell(InteriorSpace sp)
        {
            if (sp == null || sp.bellT < 2.5f) return;
            sp.bellT = 0f;
            if (sp.audio == null)
            {
                sp.audio = sp.root.gameObject.AddComponent<AudioSource>();
                sp.audio.spatialBlend = 0f;
                sp.audio.playOnAwake = false;
                sp.audio.clip = PropFX.TempleBell();
            }
            sp.audio.volume = 0.8f * GameSettings.SfxVolume * GameSettings.MasterVolume;
            sp.audio.Play();
            PlacesManager.Toast("The bell's hum fills the hall");
        }

        internal void LightIncense(InteriorSpace sp)
        {
            if (sp == null) return;
            if (!Wallet.TrySpend(5, "Incense")) { PlacesManager.Toast("Incense costs " + Wallet.Format(5)); return; }
            sp.incenseT = 40f;
            if (sp.incenseStick != null) sp.incenseStick.SetActive(true);
            Buffs.Add("blessing", "Blessing", "福", BuffKind.JobPay, 0.1f, 10f, Buffs.Blessing);
            PlacesManager.Toast("Incense lit - a blessing: +10% job pay for 10 min");
            UI.Sound("MenuSelect");
        }

        static readonly string[] Fortunes =
        {
            "A gentle wind will follow your wheels.", "Tea shared is joy doubled.", "The blossom opens for the patient.",
            "A friend you have not met waits on the coast road.", "Your next corner will be your cleanest.",
            "Clouds part for those who climb.", "Small coins, big smiles.", "The lantern you light lights others too.",
            "Slow down - the view is the prize.", "Good luck rides in the passenger seat today."
        };

        internal void MakeWish(InteriorSpace sp)
        {
            if (!Wallet.TrySpend(10, "Wish")) { PlacesManager.Toast("A wish costs a " + Wallet.Format(10) + " coin"); return; }
            Buffs.Add("wish", "Good fortune", "福", BuffKind.Luck, 0.25f, 15f, Buffs.LuckRed);
            PlacesManager.Toast("“" + Fortunes[Random.Range(0, Fortunes.Length)] + "”   (luck for 15 min)");
            UI.Sound("MenuSelect");
        }

        // ================================================================== frame
        Seat seat;
        float seatT, orbit;
        bool cineActive;
        float cineT, cineYaw;
        float bannerT = -1f;
        ParticleSystem fx;
        float fxAcc, glowT;
        Camera cam;

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            // ---- fade
            if (atBlack != null || fadeTarget > 0f)
            {
                fade = Mathf.MoveTowards(fade, fadeTarget, udt * fadeSpeed);
                if (fadeTarget > 0f && fade >= 1f)
                {
                    if (atBlack != null)
                    {
                        var a = atBlack;
                        atBlack = null;
                        try { a(); } catch (System.Exception e) { Debug.LogException(e); }
                        holdT = 0.12f;
                    }
                    holdT -= udt;
                    if (holdT <= 0f) fadeTarget = 0f;
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 0f, udt * fadeSpeed);
                if (fade <= 0f && menuHeld && seat == null) { SetMenu(false); fadeSpeed = 2.4f; }
            }
            if (bannerT >= 0f) { bannerT += udt; if (bannerT > 4f) bannerT = -1f; }

            if (current == null && !cineActive) return;
            var person = PlayerState.Person;
            // ---- the person left the pocket some other way (fast travel, respawn, back in a car): forget the interior
            if (current != null && atBlack == null)
            {
                if (person == null || !PlayerState.OnFoot || (person.position - current.origin).sqrMagnitude > 70f * 70f)
                {
                    var p = current.place;
                    Cleanup();
                    SetMenu(false);
                    if (Exited != null) Exited(p);
                    return;
                }
            }
            float dt = Time.deltaTime;
            bool paused = dt <= 0f;
            bool input = !paused && atBlack == null && fade < 0.5f;
            // ---- sitting: stand up on E / Space / pad X / pad B
            if (seat != null)
            {
                seatT += udt;
                if (input && seatT > 0.6f && (InputReader.Down(KeyCode.E) || InputReader.Down(KeyCode.Space) ||
                    GamepadBridge.ButtonDown(PadButton.West) || GamepadBridge.ButtonDown(PadButton.East))) Stand();
            }
            if (cineActive)
            {
                cineT += udt;
                if (cineT > 7.5f || (input && cineT > 0.8f && (InputReader.Down(KeyCode.E) || InputReader.Down(KeyCode.Space) || GamepadBridge.ButtonDown(PadButton.West))))
                { cineActive = false; fbInit = false; }
            }
            if (current == null) return;
            // ---- effects: steam, embers, incense smoke, bell swing, candle / fire flicker
            if (!paused) TickFx(dt);
            if (Atmosphere.Sun != null && Atmosphere.Sun.enabled) envDirty = true;
        }

        void TickFx(float dt)
        {
            var sp = current;
            glowT -= dt;
            if (glowT <= 0f)
            {
                glowT = 0.05f;
                float t = Time.time;
                PlaceMats.Glow.SetFloat("_Glow", 1.85f + 0.12f * Mathf.Sin(t * 7.1f) * Mathf.Sin(t * 2.3f));
                if (sp.fireLight != null && sp.fireLight.enabled)
                    sp.fireLight.intensity = 1.5f + 0.35f * Mathf.PerlinNoise(t * 3.1f, 0.3f) + 0.15f * Mathf.Sin(t * 13f);
            }
            if (sp.bell != null)
            {
                sp.bellT += dt;
                float a = sp.bellT < 6f ? Mathf.Sin(sp.bellT * 3.2f) * Mathf.Exp(-sp.bellT * 0.7f) : 0f;
                sp.bell.localRotation = Quaternion.Euler(a * 6f, 0f, 0f);
                if (sp.striker != null)
                {
                    float s = sp.bellT < 0.35f ? Mathf.Sin(sp.bellT / 0.35f * Mathf.PI) : 0f;
                    sp.striker.localRotation = Quaternion.Euler(-s * 22f, 0f, 0f);
                }
            }
            if (sp.incenseT > 0f)
            {
                sp.incenseT -= dt;
                if (sp.incenseT <= 0f && sp.incenseStick != null) sp.incenseStick.SetActive(false);
            }
            if (fx == null) fx = MakeFx();
            float q = GameSettings.Quality == 0 ? 0.5f : 1f;
            fxAcc += dt * q;
            if (fxAcc < 0.25f) return;
            float step = fxAcc;
            fxAcc = 0f;
            var ep = new ParticleSystem.EmitParams();
            // steam over the cups (only the player's cup when sitting on Low)
            for (int i = 0; i < sp.steamPts.Count; i++)
            {
                if (!sp.steamAll && GameSettings.Quality == 0) break;
                if (Random.value > step * 1.6f) continue;
                ep.position = sp.steamPts[i] + Random.insideUnitSphere * 0.02f;
                ep.velocity = new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(0.12f, 0.2f), Random.Range(-0.03f, 0.03f));
                ep.startSize = Random.Range(0.07f, 0.13f);
                ep.startLifetime = Random.Range(1.6f, 2.4f);
                ep.startColor = new Color(1f, 1f, 1f, 0.32f);
                fx.Emit(ep, 1);
            }
            if (seat != null && seat.steam)
            {
                ep.position = sp.W2(seat.steamLocal);
                ep.velocity = new Vector3(0f, 0.16f, 0f);
                ep.startSize = 0.1f;
                ep.startLifetime = 2.2f;
                ep.startColor = new Color(1f, 1f, 1f, 0.38f);
                fx.Emit(ep, 1);
            }
            for (int i = 0; i < sp.embers.Count; i++)
            {
                int n = Random.value < step * 3f * q ? 1 : 0;
                for (int k = 0; k < n; k++)
                {
                    ep.position = sp.embers[i] + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.15f, 0.15f));
                    ep.velocity = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.5f, 1.0f), Random.Range(-0.05f, 0.05f));
                    ep.startSize = Random.Range(0.025f, 0.05f);
                    ep.startLifetime = Random.Range(0.6f, 1.2f);
                    ep.startColor = new Color(1f, 0.6f, 0.25f, 0.9f);
                    fx.Emit(ep, 1);
                }
            }
            if (sp.incenseT > 0f)
            {
                ep.position = sp.incensePt + Random.insideUnitSphere * 0.03f;
                ep.velocity = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0.25f, 0.35f), Random.Range(-0.04f, 0.04f));
                ep.startSize = Random.Range(0.08f, 0.16f);
                ep.startLifetime = Random.Range(2.5f, 3.5f);
                ep.startColor = new Color(0.92f, 0.9f, 0.88f, 0.35f);
                fx.Emit(ep, 1);
            }
        }

        ParticleSystem MakeFx()
        {
            var go = new GameObject("Interior_FX");
            go.transform.SetParent(PocketRoot(), false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle("PlaceSteam", ProceduralTextures.Soft, Color.white, 0f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // ================================================================== camera (runs after every other camera script)
        static readonly RaycastHit[] hitBuf = new RaycastHit[12];
        bool fbInit;
        float fbYaw;
        Vector3 fbPos;

        void LateUpdate()
        {
            if (envActive) ApplyEnv();
            if (seat != null) PinSeat();
            CameraOverride = false;
            if (current == null && !cineActive) return;
            if ((PhotoMode.Instance != null && PhotoMode.Instance.Active) || (ReplaySystem.Instance != null && ReplaySystem.Instance.Active)) return;
            if (cam == null) cam = Camera.main;
            var person = PlayerState.Person;
            if (cam == null || person == null) return;
            float udt = Time.unscaledDeltaTime;
            Vector3 head = person.position + Vector3.up * 1.55f;
            Transform ct = cam.transform;

            if (cineActive && current == null)
            {
                // viewpoint: pull back and up while panning across the view
                CameraOverride = true;
                float t = Mathf.Clamp01(cineT / 7.5f), e = t * t * (3f - 2f * t);
                float yaw = cineYaw - 35f + 70f * e;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 pos = head + q * new Vector3(1.2f, 0.6f + 2.8f * e, -3.2f - 5f * e);
                ct.position = pos;
                ct.rotation = Quaternion.LookRotation(head + q * new Vector3(0f, -2f, 40f) - pos);
                return;
            }
            if (current == null) return;
            Vector3 lo = new Vector3(-current.W * 0.5f + 0.3f, 0.3f, -current.D * 0.5f + 0.3f), hi = new Vector3(current.W * 0.5f - 0.3f, current.H - 0.25f, current.D * 0.5f - 0.3f);
            if (seat != null)
            {
                // slow cinematic orbit around the table
                CameraOverride = true;
                orbit += udt * 5f;
                Vector3 focus = current.W2(seat.focusLocal);
                Quaternion q = Quaternion.Euler(0f, orbit, 0f);
                Vector3 want = focus + q * new Vector3(0f, 0.75f, -2.5f);
                Vector3 local = ClampLocal(current.Local(want), lo, hi);
                Vector3 pos = current.W2(local);
                pos = Collide(focus, pos, 0.2f);
                ct.position = pos;
                ct.rotation = Quaternion.LookRotation(focus - pos);
                return;
            }
            Vector3 c = ct.position;
            Vector3 v = c - head;
            float dist = v.magnitude;
            if (dist > 25f || dist < 0.05f)
            {
                // nobody follows the person in here: simple follow camera behind them (right stick orbits)
                CameraOverride = true;
                float py = person.eulerAngles.y;
                if (!fbInit) { fbInit = true; fbYaw = py; fbPos = head; }
                float stick = GamepadBridge.RightStick.x;
                if (Mathf.Abs(stick) > 0.2f) fbYaw += stick * 110f * udt;
                else fbYaw = Mathf.LerpAngle(fbYaw, py, 1f - Mathf.Exp(-udt * 2.5f));
                Quaternion q = Quaternion.Euler(14f, fbYaw, 0f);
                Vector3 want = head + q * new Vector3(0f, 0.2f, -current.camDist);
                want = current.W2(ClampLocal(current.Local(want), lo, hi));
                want = Collide(head, want, 0.22f);
                fbPos = Vector3.Lerp(fbPos, want, 1f - Mathf.Exp(-udt * 10f));
                if ((fbPos - want).sqrMagnitude > 9f) fbPos = want;
                ct.position = fbPos;
                ct.rotation = Quaternion.LookRotation(head - fbPos);
                return;
            }
            // clamp the on-foot camera: shorter orbit, collision with the room, inside the walls
            fbInit = false;
            Vector3 dir = v / dist;
            float d = Mathf.Min(dist, current.camDist);
            Vector3 p2 = Collide(head, head + dir * d, 0.22f);
            p2 = current.W2(ClampLocal(current.Local(p2), lo, hi));
            ct.position = p2;
        }

        static Vector3 ClampLocal(Vector3 p, Vector3 lo, Vector3 hi)
        {
            return new Vector3(Mathf.Clamp(p.x, lo.x, hi.x), Mathf.Clamp(p.y, lo.y, hi.y), Mathf.Clamp(p.z, lo.z, hi.z));
        }

        /// <summary>Sphere cast from 'from' to 'to' against this interior's colliders; returns the reachable point.</summary>
        Vector3 Collide(Vector3 from, Vector3 to, float radius)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f || current == null) return to;
            d /= len;
            int n = Physics.SphereCastNonAlloc(from, radius, d, hitBuf, len, ~(1 << CarExtras.CarLayer), QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                var h = hitBuf[i];
                if (h.collider == null || h.distance <= 0f) continue;
                if (!h.collider.transform.IsChildOf(current.root)) continue;
                if (h.distance < best) best = h.distance;
            }
            return from + d * Mathf.Max(0.35f, best - 0.05f);
        }

        // ================================================================== environment override
        bool envActive, envDirty;
        bool sunWasOn, haveOutside, outFog, wFog;
        Color outSky, outEq, outGround;
        Color wSky = new Color(-1f, 0f, 0f), wEq, wGround;
        float outFar = -1f, wFar = -1f;
        RainFX rain; PetalWeather petals;
        Transform rainFollow, petalFollow;
        ParticleSystemRenderer snowfall;
        bool snowWasOn;

        void SetupOutside(Place p)
        {
            if (proxy == null) { proxy = new GameObject("Interior_WeatherProxy").transform; proxy.SetParent(PocketRoot(), false); }
            proxy.SetPositionAndRotation(p.Door + Vector3.up * 1.6f, Quaternion.Euler(0f, p.OutYaw + 180f, 0f));
            if (rain == null) rain = FindAnyObjectByType<RainFX>();
            if (petals == null) petals = FindAnyObjectByType<PetalWeather>();
            if (rain != null && rain.follow != proxy) { rainFollow = rain.follow; rain.follow = proxy; }
            if (petals != null && petals.follow != proxy) { petalFollow = petals.follow; petals.follow = proxy; }
            var sf = GameObject.Find("Snow_FX_Snowfall");
            snowfall = sf != null ? sf.GetComponent<ParticleSystemRenderer>() : null;
            if (snowfall != null) { snowWasOn = snowfall.enabled; snowfall.enabled = false; }
            haveOutside = false;
            wSky = new Color(-1f, 0f, 0f);
            wFar = -1f;
        }

        void ApplyEnv()
        {
            if (current == null) return;
            var sun = Atmosphere.Sun;
            if (sun != null && sun.enabled) { sunWasOn = true; sun.enabled = false; }
            envDirty = false;
            if (!haveOutside || RenderSettings.ambientSkyColor != wSky || RenderSettings.ambientEquatorColor != wEq || RenderSettings.ambientGroundColor != wGround)
            {
                // first frame inside, or Atmosphere blended to new values meanwhile: those are the outside values now
                outSky = RenderSettings.ambientSkyColor; outEq = RenderSettings.ambientEquatorColor; outGround = RenderSettings.ambientGroundColor;
                if (!haveOutside || RenderSettings.fog != wFog) outFog = RenderSettings.fog;
                haveOutside = true;
            }
            else if (RenderSettings.fog != wFog) outFog = RenderSettings.fog;
            RenderSettings.ambientSkyColor = wSky = current.ambSky;
            RenderSettings.ambientEquatorColor = wEq = current.ambEq;
            RenderSettings.ambientGroundColor = wGround = current.ambGround;
            RenderSettings.fog = wFog = false;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                if (cam.farClipPlane != wFar) outFar = cam.farClipPlane;
                cam.farClipPlane = wFar = 160f;
            }
        }

        void RestoreEnv()
        {
            var sun = Atmosphere.Sun;
            if (sun != null && sunWasOn) sun.enabled = true;
            sunWasOn = false;
            if (haveOutside)
            {
                RenderSettings.ambientSkyColor = outSky;
                RenderSettings.ambientEquatorColor = outEq;
                RenderSettings.ambientGroundColor = outGround;
                RenderSettings.fog = outFog;
                haveOutside = false;
            }
            if (cam != null && outFar > 0f && cam.farClipPlane == wFar) cam.farClipPlane = outFar;
            outFar = wFar = -1f;
            if (rain != null && rain.follow == proxy) rain.follow = rainFollow != null ? rainFollow : (cam != null ? cam.transform : null);
            if (petals != null && petals.follow == proxy) petals.follow = petalFollow != null ? petalFollow : (cam != null ? cam.transform : null);
            if (snowfall != null) snowfall.enabled = snowWasOn || snowfall.enabled;
            snowfall = null;
        }

        internal void ApplyLightQuality()
        {
            if (current == null) return;
            bool on = GameSettings.Quality > 0;
            foreach (var l in current.lights) if (l != null) l.enabled = on;
        }

        // ================================================================== helpers
        Transform PocketRoot()
        {
            if (pocketRoot == null) { pocketRoot = new GameObject("PlacesPockets").transform; pocketRoot.SetParent(transform, false); }
            return pocketRoot;
        }

        void StartFade(System.Action a)
        {
            atBlack = a;
            fadeTarget = 1f;
            SetMenu(true);
        }

        void SetMenu(bool on)
        {
            if (on) { GameplayHooks.MenuOpen = true; menuHeld = true; }
            else if (menuHeld) { GameplayHooks.MenuOpen = false; menuHeld = false; }
        }

        void PinSeat()
        {
            var pr = PlayerState.Person;
            if (pr == null || current == null || seat == null) return;
            Vector3 p = current.W2(seat.local);
            Quaternion r = Quaternion.Euler(0f, current.Yaw(seat.yawLocal), 0f);
            if ((pr.position - p).sqrMagnitude > 1e-4f || Quaternion.Angle(pr.rotation, r) > 0.5f)
                TeleportPerson(pr, p, r.eulerAngles.y, false);
            SetPose(pr, PersonPose.Sit);
        }

        static void SetPose(Transform person, PersonPose pose)
        {
            var rig = person.GetComponent<PersonRig>();
            if (rig == null) rig = person.GetComponentInChildren<PersonRig>();
            if (rig != null && rig.pose != pose) rig.SetPose(pose);
            if (rig != null) rig.speed = 0f;
        }

        Vector3 GroundAt(Vector3 p)
        {
            var w = WorldHooks.World;
            float y = p.y;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hit, 8f, ~((1 << CarExtras.CarLayer) | (1 << WorldBuilder.LayerGrass)), QueryTriggerInteraction.Ignore))
                y = Mathf.Max(y, hit.point.y);
            else if (w != null && w.Done) y = Mathf.Max(y, w.TerrainHeight(p.x, p.z));
            return new Vector3(p.x, y + 0.05f, p.z);
        }

        static void TeleportPerson(Transform person, Vector3 pos, float yaw, bool notify = true)
        {
            if (person == null) return;
            var cc = person.GetComponent<CharacterController>();
            bool ccOn = cc != null && cc.enabled;
            if (ccOn) cc.enabled = false;
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            person.SetPositionAndRotation(pos, r);
            var rb = person.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = pos;
                rb.rotation = r;
                if (!rb.isKinematic) { rb.SetVelocity(Vector3.zero); rb.angularVelocity = Vector3.zero; }
            }
            if (ccOn) cc.enabled = true;
            Physics.SyncTransforms();
            if (notify && Teleported != null) Teleported(pos, yaw);
        }

        // ================================================================== GUI: fade, banner, hints
        string bannerName, bannerHanzi;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float W = Screen.width, H = Screen.height;
            float s = Mathf.Clamp(H / 1080f, 0.6f, 2f);
            UI.Styles();
            if (current != null && bannerT >= 0f && fade < 0.5f)
            {
                if (bannerName != current.place.name) { bannerName = current.place.name; bannerHanzi = current.place.hanzi; }
                float a = Mathf.Clamp01(bannerT / 0.6f) * Mathf.Clamp01((4f - bannerT) / 0.8f);
                var r = new Rect(W * 0.5f - 300f * s, H * 0.12f, 600f * s, 92f * s);
                UI.Panel(r, 0.45f * a);
                UI.Label(new Rect(r.x, r.y + 6f * s, r.width, 26f * s), bannerHanzi, UI.Mid, new Color(1f, 0.84f, 0.42f, a));
                UI.Label(new Rect(r.x, r.y + 34f * s, r.width, 48f * s), bannerName, UI.Title, new Color(1f, 0.97f, 0.96f, a));
            }
            if ((seat != null || cineActive) && fade < 0.3f && !GameManager.HideHud)
            {
                var r = new Rect(W * 0.5f - 140f * s, H - 92f * s, 280f * s, 40f * s);
                UI.Panel(r, 0.5f);
                UI.Label(r, seat != null ? (InputReader.PadConnected ? "X   Stand up" : "E   Stand up") : (InputReader.PadConnected ? "X   Continue" : "E   Continue"), UI.Mid, UI.White);
            }
            if (fade > 0.001f)
            {
                var old = GUI.color;
                GUI.color = new Color(0.04f, 0.02f, 0.03f, Mathf.SmoothStep(0f, 1f, fade));
                GUI.depth = -100;
                GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
    }
}

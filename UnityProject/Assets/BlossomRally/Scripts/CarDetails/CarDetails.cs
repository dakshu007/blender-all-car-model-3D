// CarDetails.cs - makes the player's car look alive up close. Attached to every player car through
// WorldHooks.CarSpawned (incl. the garage turntable car). One coordinator component drives small
// subsystems (plain classes, no extra MonoBehaviours):
//   CarCrew       driver + co-driver (steering, gear changes, g-force head lean, pace notes, pointing)
//   CarCockpit    speedo / rev needles, trip computer + clock, rear-view mirror, swinging lucky knot
//   CarDangles    flapping mud flaps, swaying antenna, spinning roof vent (Group B)
//   CarWindscreen windscreen dirt that builds with the car's mud, wiper arcs in the rain, cracks
//   CarLightsFX   headlight beams in fog / rain / night, glowing brake discs, exhaust flames + heat haze
//   CarDebris     gravel stones and dirt / turf clods thrown by the tyres
//   CarDecals     number plates, door / roof numbers, sponsor stickers, windscreen banner, gauge faces
// plus CarDamage (separate component: ICarModifier + Repair()).
// All small animated parts of one car (crew, needles, wipers, antenna, vent, knot...) are posed into ONE
// LifeBatch mesh = one draw call with the shared vertex-colour material. Quality Low drops the co-driver,
// beams, haze, exhaust light, debris and the knot, and poses at a reduced rate.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class CarDetailsBoot
    {
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.CarSpawned += Attach;
        }

        static void Attach(ArcadeCar car)
        {
            if (car == null || car.GetComponent<CarDetails>() != null) return;
            var anatomy = CarAnatomy.Build(car);
            var dmg = car.gameObject.AddComponent<CarDamage>();
            dmg.Setup(anatomy);
            var det = car.gameObject.AddComponent<CarDetails>();
            det.Setup(anatomy, dmg);
        }
    }

    internal abstract class CarDetailSystem
    {
        /// <summary>Create objects; add templates / parts to d.Batch (finished after all systems built).</summary>
        public virtual void Build(CarDetails d) { }
        /// <summary>Per frame (after the car moved). Pose batch parts here.</summary>
        public virtual void Tick(CarDetails d, float dt) { }
        public virtual void Dispose() { }
    }

    [RequireComponent(typeof(ArcadeCar))]
    public class CarDetails : MonoBehaviour
    {
        internal ArcadeCar car;
        internal CarAnatomy a;
        internal CarDamage damage;
        internal CarExtras extras;
        internal LifeBatch batch;
        internal int quality;

        // motion (car space)
        internal Vector3 accLocal;        // smoothed acceleration of the car (m/s^2), car space, without gravity
        internal Vector3 velLocal;
        internal float speedMs;
        internal bool showroom, frozen;
        internal Camera cam;
        internal float camDist = 10f;
        internal bool cockpitView;
        internal Color suit, accent, helmet;

        readonly List<CarDetailSystem> systems = new List<CarDetailSystem>();
        internal CarWindscreen windscreen;
        internal CarLightsFX lightsFx;
        internal CarDecals decals;
        Vector3 prevPos, prevVel;
        bool haveMotion;
        float camTimer, liveryTimer;
        int liveryKey = -1, builtQuality = -1;
        int frame;

        internal void Setup(CarAnatomy anatomy, CarDamage dmg)
        {
            a = anatomy;
            damage = dmg;
            car = anatomy.car;
        }

        void Start()
        {
            if (car == null) car = GetComponent<ArcadeCar>();
            if (a == null) a = CarAnatomy.Build(car);
            if (damage == null) damage = GetComponent<CarDamage>();
            extras = GetComponent<CarExtras>();
            Rebuild();
        }

        void OnDestroy() { Teardown(); }

        int LiveryKey()
        {
            return GameSettings.LiveryColour(a.id) * 31 + GameSettings.LiveryPattern(a.id);
        }

        void PickColours()
        {
            int col = GameSettings.LiveryColour(a.id);
            suit = col == 0 ? LiveryLibrary.PaintKey(a.id) : LiveryLibrary.Colour(col);
            accent = LiveryLibrary.PatternColour(a.id, col);
            // keep suits readable: very dark / very light paint gets a slightly lifted suit
            float l = suit.r * 0.3f + suit.g * 0.55f + suit.b * 0.15f;
            if (l < 0.12f) suit = Color.Lerp(suit, new Color(0.25f, 0.25f, 0.3f), 0.5f);
            helmet = Color.Lerp(suit, Color.white, 0.25f);
            liveryKey = LiveryKey();
        }

        void Rebuild()
        {
            Teardown();
            quality = GameSettings.Quality;
            builtQuality = quality;
            PickColours();
            batch = new LifeBatch();
            decals = new CarDecals();
            windscreen = new CarWindscreen();
            lightsFx = new CarLightsFX();
            systems.Add(decals);
            systems.Add(new CarCrew());
            systems.Add(new CarCockpit());
            systems.Add(new CarDangles());
            systems.Add(windscreen);
            systems.Add(lightsFx);
            if (quality >= 1) systems.Add(new CarDebris());
            foreach (var s in systems)
            {
                try { s.Build(this); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            if (batch.PartCount > 0)
            {
                var go = batch.Finish("CarDetailParts", MaterialLibrary.Terrain, transform, CarExtras.CarLayer, false);
                batch.Renderer.receiveShadows = true;
                batch.Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }
            // first pose straight away so nothing pops in collapsed
            foreach (var s in systems)
            {
                try { s.Tick(this, 0f); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            batch.Upload();
        }

        void Teardown()
        {
            foreach (var s in systems)
            {
                try { s.Dispose(); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            systems.Clear();
            if (batch != null && batch.Renderer != null)
            {
                Destroy(batch.Mesh);
                Destroy(batch.Renderer.gameObject);
            }
            batch = null;
        }

        /// <summary>Called by the decal system so stickers crumple with the body.</summary>
        internal void RegisterDeformable(MeshFilter mf) { if (damage != null) damage.RegisterDeformable(mf); }

        void UpdateMotion(float dt)
        {
            Vector3 pos = transform.position;
            if (!haveMotion || dt <= 0f) { prevPos = pos; prevVel = Vector3.zero; haveMotion = true; return; }
            Vector3 v = (pos - prevPos) / dt;
            if ((pos - prevPos).sqrMagnitude > 25f) v = prevVel;           // teleport / respawn
            Vector3 acc = (v - prevVel) / dt;
            if (acc.sqrMagnitude > 1600f) acc = acc.normalized * 40f;
            prevPos = pos;
            prevVel = v;
            Vector3 al = transform.InverseTransformDirection(acc);
            accLocal = Vector3.Lerp(accLocal, al, 1f - Mathf.Exp(-dt / 0.07f));
            velLocal = transform.InverseTransformDirection(v);
            speedMs = v.magnitude;
        }

        void LateUpdate()
        {
            if (car == null) return;
            float dt = Time.deltaTime;
            frame++;
            if (GameSettings.Quality != builtQuality) { Rebuild(); return; }
            frozen = car.Body != null && car.Body.isKinematic;
            showroom = (extras != null && extras.ShowroomMode) || (frozen && !car.controlsEnabled);
            UpdateMotion(dt);

            liveryTimer -= Time.unscaledDeltaTime;
            if (liveryTimer <= 0f)
            {
                liveryTimer = 0.5f;
                if (LiveryKey() != liveryKey) { Rebuild(); return; }
            }

            camTimer -= Time.unscaledDeltaTime;
            if (cam == null || camTimer <= 0f) { cam = Camera.main; camTimer = 1f; }
            camDist = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 10f;
            cockpitView = cam != null && camDist < 2.2f && transform.InverseTransformPoint(cam.transform.position).y < a.ext.y;
            if (dt <= 0f) return;

            // far away: nothing up close to see - pose rarely (lights / particles still update)
            int every = camDist > 150f ? 8 : camDist > 60f ? 3 : (quality == 0 ? 2 : 1);
            bool pose = frame % every == 0;
            float pdt = dt * every;
            foreach (var s in systems)
            {
                if (!pose && !(s is CarLightsFX) && !(s is CarDebris) && !(s is CarWindscreen)) continue;
                try { s.Tick(this, s is CarLightsFX || s is CarDebris || s is CarWindscreen ? dt : pdt); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            if (pose && batch != null) batch.Upload();
        }

        // ------------------------------------------------------------- shared helpers for subsystems

        /// <summary>Gravity minus the car's acceleration, in car space: the "felt" g vector (points down at rest).</summary>
        internal Vector3 FeltGravity()
        {
            return transform.InverseTransformDirection(Physics.gravity) - accLocal;
        }

        internal static Color32 C(Color c, float k = 1f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * k * 255f, 0f, 255f),
                               (byte)Mathf.Clamp(c.b * k * 255f, 0f, 255f), 0);
        }

        /// <summary>Paint dirt of this car (read from its paint material, which CarExtras feeds).</summary>
        Material paint;
        string paintKey;
        static readonly int IdDirt = Shader.PropertyToID("_Dirt");
        static readonly int IdDirtColor = Shader.PropertyToID("_DirtColor");

        internal float PaintDirt(out Color dirtColour)
        {
            dirtColour = new Color(0.45f, 0.36f, 0.26f);
            if (paint == null)
            {
                if (paintKey == null) paintKey = "Paint_" + a.id;
                paint = MaterialLibrary.TryGet(paintKey);
            }
            var m = paint;
            if (m == null || !m.HasProperty(IdDirt)) return 0f;
            if (m.HasProperty(IdDirtColor)) dirtColour = m.GetColor(IdDirtColor);
            return m.GetFloat(IdDirt);
        }

        /// <summary>Is this wheel driven (from the car's drivetrain)?</summary>
        internal bool Driven(ArcadeCar.Wheel w)
        {
            var dr = car.spec != null ? car.spec.drive : Drive.RWD;
            return dr == Drive.AWD || (dr == Drive.RWD && !w.front) || (dr == Drive.FWD && w.front);
        }

        /// <summary>+1 = drifting to the right (tail out left), -1 = left; from the slip angle.</summary>
        internal float DriftSide()
        {
            return car.SlipAngle > 0f ? -1f : 1f;
        }
    }
}

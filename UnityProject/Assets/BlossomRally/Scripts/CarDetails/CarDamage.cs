// CarDamage.cs - visible and mechanical damage for the player's car, accumulated per car id for the session.
//  * dents: body / bumpers / mirrors / spoiler / trim / lights (and the decals) crumple inward near the contact
//    point (per-instance mesh copies made on the first dent, smooth falloff + position-hashed wrinkles, capped)
//  * a hard front hit breaks a headlight (its lens becomes a dark broken lamp, no emission) + glass shards
//  * cracks in the windscreen (drawn by CarWindscreen from Cracks), bumpers hang / tilt from one end
//  * ICarModifier: heavy hits bend the steering (pull up to 3 deg), weaken the engine (to 0.8) and cost grip
//  * Repair() restores everything (the garage calls it); RepairSaved(id) clears a car that is not spawned.
// Collisions come from this component's own OnCollisionEnter (same GameObject as the Rigidbody).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    [RequireComponent(typeof(ArcadeCar))]
    public class CarDamage : MonoBehaviour, ICarModifier
    {
        public const float MaxDent = 0.12f;          // crumple cap per vertex (m)
        public const float MaxSteerPull = 3f;        // deg
        public const float MinPower = 0.8f;
        public const float MinGrip = 0.92f;

        internal sealed class Dent { public Vector3 c, d; public float r, k; public int seed; }
        internal struct Crack { public Vector2 p; public float size; public int seed; }

        internal sealed class State
        {
            public readonly List<Dent> dents = new List<Dent>();
            public readonly List<Crack> cracks = new List<Crack>();
            public readonly bool[] head = new bool[2];
            public readonly bool[] aux = new bool[2];
            public float hangF, hangR;
            public int sideF = 1, sideR = -1;
            public float power = 1f, grip = 1f, pull;
        }

        static readonly Dictionary<string, State> saved = new Dictionary<string, State>();

        sealed class Deformable
        {
            public MeshFilter mf;
            public Mesh original, copy;
            public bool external;           // mesh owned by another system (decals): deform in place
            public Vector3[] baseV, baseN, work, workN;
            public Vector3[] carV;
            public Matrix4x4 carToMesh;
            public Bounds carBounds;
            public bool recalcNormals = true;
            public bool dirty;
        }

        // ---- ICarModifier
        public float PowerFactor { get { return state != null ? state.power : 1f; } }
        public float GripFactor { get { return state != null ? state.grip : 1f; } }
        public float SteerPullDeg { get { return state != null ? state.pull : 0f; } }

        /// <summary>0 = pristine .. 1 = wrecked (for HUD / garage repair price).</summary>
        public float Damage01
        {
            get
            {
                if (state == null) return 0f;
                float dent = 0f;
                foreach (var d in state.dents) dent += d.k;
                float mech = Mathf.Max((1f - state.power) / (1f - MinPower), Mathf.Abs(state.pull) / MaxSteerPull);
                return Mathf.Clamp01(Mathf.Max(dent / 0.8f, mech) + state.cracks.Count * 0.05f + (state.head[0] || state.head[1] ? 0.1f : 0f));
            }
        }

        /// <summary>Raised after a new hit or a repair (windscreen / beams refresh from it).</summary>
        public event System.Action DamageChanged;

        ArcadeCar car;
        internal CarAnatomy anatomy;
        internal State state;
        readonly List<Deformable> parts = new List<Deformable>();
        Deformable lightsPart;
        int[][] lightsTris;
        Material[] lightsMats;
        ParticleSystem bits;
        float lastHit = -10f, lastHitSpeed;
        // bumper hang
        Transform bumperF, bumperR;
        Vector3 restPosF, restPosR;
        Quaternion restRotF, restRotR;
        float wobF, wobVF, wobR, wobVR, prevVy;
        Vector3 prevPos;

        internal void Setup(CarAnatomy a)
        {
            anatomy = a;
            car = a.car;
            if (!saved.TryGetValue(a.id, out state)) { state = new State(); saved[a.id] = state; }
            foreach (var mf in new[] { a.body, a.bumperF, a.bumperR, a.mirrors, a.spoiler, a.trim, a.lights })
            {
                var d = AddDeformable(mf, false);
                if (mf == a.lights) lightsPart = d;
            }
            if (a.bumperF != null) { bumperF = a.bumperF.transform; restPosF = a.root.InverseTransformPoint(bumperF.position); restRotF = Quaternion.Inverse(a.root.rotation) * bumperF.rotation; }
            if (a.bumperR != null) { bumperR = a.bumperR.transform; restPosR = a.root.InverseTransformPoint(bumperR.position); restRotR = Quaternion.Inverse(a.root.rotation) * bumperR.rotation; }
            prevPos = transform.position;
            ApplyDents();
            ApplyLamps();
        }

        Deformable AddDeformable(MeshFilter mf, bool external)
        {
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return null;
            var d = new Deformable { mf = mf, original = mf.sharedMesh, external = external };
            d.carToMesh = anatomy.ToCar(mf.transform).inverse;
            var m = anatomy.ToCar(mf.transform);
            d.baseV = d.original.vertices;
            d.baseN = d.original.normals;
            d.carV = new Vector3[d.baseV.Length];
            for (int i = 0; i < d.baseV.Length; i++) d.carV[i] = m.MultiplyPoint3x4(d.baseV[i]);
            Bounds b = new Bounds(d.carV.Length > 0 ? d.carV[0] : Vector3.zero, Vector3.zero);
            for (int i = 1; i < d.carV.Length; i++) b.Encapsulate(d.carV[i]);
            d.carBounds = b;
            d.recalcNormals = !external;
            if (external) d.copy = d.original;
            parts.Add(d);
            return d;
        }

        /// <summary>Decals and other car-space meshes that should crumple with the body (deformed in place).</summary>
        internal void RegisterDeformable(MeshFilter mf)
        {
            var d = AddDeformable(mf, true);
            if (d != null && state != null && state.dents.Count > 0) { DeformPart(d); }
        }

        internal void UnregisterDeformable(MeshFilter mf)
        {
            for (int i = parts.Count - 1; i >= 0; i--) if (parts[i].mf == mf) parts.RemoveAt(i);
        }

        void OnDestroy()
        {
            foreach (var d in parts)
                if (!d.external && d.copy != null && d.copy != d.original) Destroy(d.copy);
            if (bits != null) Destroy(bits.gameObject);
        }

        // ------------------------------------------------------------------ collisions

        void OnCollisionEnter(Collision c)
        {
            if (car == null || state == null) return;
            if (car.Body != null && car.Body.isKinematic) return;
            int n = c.contactCount;
            if (n == 0) return;
            Vector3 p = Vector3.zero, nrm = Vector3.zero;
            int k = Mathf.Min(n, 4);
            for (int i = 0; i < k; i++) { var cp = c.GetContact(i); p += cp.point; nrm += cp.normal; }
            p /= k;
            if (nrm.sqrMagnitude < 1e-6f) return;
            nrm.Normalize();
            float vn = Mathf.Abs(Vector3.Dot(c.relativeVelocity, nrm));
            ApplyHit(p, nrm, vn);
        }

        /// <summary>Damage the car: world contact point, a normal (either sign), normal impact speed (m/s).</summary>
        public void ApplyHit(Vector3 worldPoint, Vector3 worldNormal, float speed)
        {
            if (state == null || anatomy == null || speed < 3.5f) return;
            float now = Time.time;
            if (now - lastHit < 0.2f && speed < lastHitSpeed * 1.4f) return;
            lastHit = now;
            lastHitSpeed = speed;
            Vector3 lp = transform.InverseTransformPoint(worldPoint);
            Vector3 dir = transform.InverseTransformDirection(worldNormal);
            Vector3 toCentre = new Vector3(0f, anatomy.ext.y * 0.45f, 0f) - lp;
            if (Vector3.Dot(dir, toCentre) < 0f) dir = -dir;
            // the underside bottoming out on a landing is not a dent
            if (dir.y > 0.75f && lp.y < 0.5f) return;
            dir = Vector3.Lerp(dir, toCentre.normalized, 0.25f).normalized;
            float s = Mathf.Clamp01((speed - 3.5f) / 16f);
            Vector3 centre = lp - dir * 0.06f;
            AddDent(centre, dir, 0.35f + 0.45f * s, 0.025f + 0.09f * s);

            Vector3 e = anatomy.ext;
            bool front = lp.z > e.z * 0.6f, rear = lp.z < -e.z * 0.6f;
            int side = lp.x < 0f ? 0 : 1;
            // headlights
            if (front && speed > 7f)
            {
                if (Mathf.Abs(lp.x) < 0.2f && speed < 12f) side = Random.value < 0.5f ? 0 : 1;
                BreakLamp(side, speed > 11f);
                if (Mathf.Abs(lp.x) < 0.2f && speed > 12f) BreakLamp(1 - side, false);
            }
            // windscreen cracks: front half or roof hits
            if (s > 0.3f && (lp.z > -0.2f * e.z || lp.y > e.y * 0.75f) && state.cracks.Count < 4)
            {
                float hw = anatomy.scrHW, h = anatomy.scrH;
                Vector3 q = lp - anatomy.scrO;
                float u = Mathf.Clamp(lp.x, -hw * 0.8f, hw * 0.8f);
                float v = Mathf.Abs(Vector3.Dot(q, anatomy.scrN)) < 0.25f ? Mathf.Clamp(Vector3.Dot(q, anatomy.scrUp), h * 0.15f, h * 0.85f)
                                                                            : Random.Range(0.3f, 0.75f) * h;
                state.cracks.Add(new Crack { p = new Vector2(u, v), size = 0.45f + 0.55f * s, seed = Random.Range(1, 100000) });
            }
            // bumpers hang from one end
            if (front && speed > 7f && bumperF != null)
            {
                if (state.hangF <= 0f) state.sideF = lp.x < 0f ? -1 : 1;
                state.hangF = Mathf.Min(16f, state.hangF + 4f + 8f * s);
                wobVF += 40f * s;
            }
            if (rear && speed > 7f && bumperR != null)
            {
                if (state.hangR <= 0f) state.sideR = lp.x < 0f ? -1 : 1;
                state.hangR = Mathf.Min(16f, state.hangR + 4f + 8f * s);
                wobVR += 40f * s;
            }
            // mechanical damage on heavy hits
            if (speed > 8f)
            {
                float sh = Mathf.Clamp01((speed - 8f) / 14f);
                float sx = lp.x < 0f ? -1f : 1f;
                if (Mathf.Abs(lp.z - anatomy.frontAxleZ) < 0.8f && Mathf.Abs(lp.x) > 0.25f) state.pull += sx * sh * 1.4f;
                else if (lp.z > 0f) state.pull += sx * sh * 0.5f;
                state.pull = Mathf.Clamp(state.pull, -MaxSteerPull, MaxSteerPull);
                bool engineEnd = anatomy.rearEngine ? lp.z < 0f : lp.z > 0f;
                state.power = Mathf.Max(MinPower, state.power - sh * 0.05f - (engineEnd ? sh * 0.05f : 0f));
                state.grip = Mathf.Max(MinGrip, state.grip - sh * 0.02f);
            }
            EmitBits(worldPoint, transform.TransformDirection(-dir), s, false);
            ApplyDents();
            if (DamageChanged != null) DamageChanged();
        }

        void AddDent(Vector3 c, Vector3 d, float r, float k)
        {
            Dent best = null;
            float bd = 0.25f;
            foreach (var x in state.dents)
            {
                float dist = Vector3.Distance(x.c, c);
                if (dist < bd) { bd = dist; best = x; }
            }
            if (best == null && state.dents.Count >= 40)
            {
                bd = float.MaxValue;
                foreach (var x in state.dents) { float dist = Vector3.Distance(x.c, c); if (dist < bd) { bd = dist; best = x; } }
            }
            if (best != null)
            {
                best.k = Mathf.Min(MaxDent, best.k + k * 0.6f);
                best.r = Mathf.Max(best.r, r);
                best.d = (best.d + d).normalized;
                return;
            }
            state.dents.Add(new Dent { c = c, d = d, r = r, k = Mathf.Min(MaxDent, k), seed = Random.Range(1, 1000) });
        }

        // ------------------------------------------------------------------ deformation

        void ApplyDents()
        {
            foreach (var d in parts) DeformPart(d);
        }

        static float Hash(Vector3 p, int seed)
        {
            int x = Mathf.RoundToInt(p.x * 60f), y = Mathf.RoundToInt(p.y * 60f), z = Mathf.RoundToInt(p.z * 60f);
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761u);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        void DeformPart(Deformable d)
        {
            if (d == null || d.mf == null) return;
            bool touched = false;
            foreach (var dent in state.dents)
            {
                Bounds b = d.carBounds;
                b.Expand(dent.r * 2f);
                if (b.Contains(dent.c)) { touched = true; break; }
            }
            if (!touched)
            {
                if (d.dirty) RestorePart(d);
                return;
            }
            if (d.copy == null)
            {
                d.copy = Instantiate(d.original);
                d.copy.name = d.original.name + "_Dented";
                d.mf.sharedMesh = d.copy;
                if (d == lightsPart && lightsTris != null) ApplyLamps();
            }
            int n = d.baseV.Length;
            if (d.work == null) { d.work = new Vector3[n]; d.workN = new Vector3[n]; }
            var moved = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = d.carV[i];
                Vector3 disp = Vector3.zero;
                foreach (var dent in state.dents)
                {
                    float dist = (p - dent.c).sqrMagnitude;
                    float r2 = dent.r * dent.r;
                    if (dist >= r2) continue;
                    float f = 1f - dist / r2;
                    f *= f;
                    float wr = 0.65f + 0.7f * Hash(p, dent.seed);
                    disp += dent.d * dent.k * f * wr;
                }
                float m = disp.magnitude;
                if (m > MaxDent) disp *= MaxDent / m;
                moved[i] = m > 0.0005f;
                d.work[i] = moved[i] ? d.baseV[i] + d.carToMesh.MultiplyVector(disp) : d.baseV[i];
            }
            d.copy.vertices = d.work;
            if (d.recalcNormals && d.baseN != null && d.baseN.Length == n)
            {
                d.copy.RecalculateNormals();
                var rn = d.copy.normals;
                for (int i = 0; i < n; i++) d.workN[i] = moved[i] ? rn[i] : d.baseN[i];
                d.copy.normals = d.workN;
            }
            d.copy.RecalculateBounds();
            d.dirty = true;
        }

        void RestorePart(Deformable d)
        {
            d.dirty = false;
            if (d.external)
            {
                if (d.copy != null) { d.copy.vertices = d.baseV; d.copy.RecalculateBounds(); }
                return;
            }
            if (d == lightsPart && (state.head[0] || state.head[1] || state.aux[0] || state.aux[1])) { d.copy.vertices = d.baseV; d.copy.normals = d.baseN; d.copy.RecalculateBounds(); return; }
            if (d.copy != null && d.copy != d.original) { d.mf.sharedMesh = d.original; Destroy(d.copy); }
            d.copy = null;
            if (d == lightsPart) lightsTris = null;
        }

        // ------------------------------------------------------------------ lamps

        public bool HeadlightBroken(int side) { return state != null && side >= 0 && side < 2 && state.head[side]; }
        public bool AuxBroken(int side) { return state != null && side >= 0 && side < 2 && state.aux[side]; }

        void BreakLamp(int side, bool auxToo)
        {
            bool changed = false;
            if (!state.head[side] && anatomy.hasHead[side])
            {
                state.head[side] = true;
                changed = true;
                EmitBits(transform.TransformPoint(anatomy.head[side]), transform.forward, 1f, true);
            }
            if (auxToo && !state.aux[side] && anatomy.hasAux[side])
            {
                state.aux[side] = true;
                changed = true;
                EmitBits(transform.TransformPoint(anatomy.aux[side]), transform.forward, 0.6f, true);
            }
            if (changed) ApplyLamps();
        }

        static Material brokenLamp;

        void ApplyLamps()
        {
            var d = lightsPart;
            if (d == null || d.mf == null) return;
            bool any = state.head[0] || state.head[1] || state.aux[0] || state.aux[1];
            var r = d.mf.GetComponent<Renderer>();
            if (r == null) return;
            if (!any)
            {
                if (lightsTris != null && d.copy != null)
                {
                    d.copy.subMeshCount = lightsTris.Length;
                    for (int i = 0; i < lightsTris.Length; i++) d.copy.SetTriangles(lightsTris[i], i);
                    r.sharedMaterials = lightsMats;
                }
                return;
            }
            if (d.copy == null)
            {
                d.copy = Instantiate(d.original);
                d.copy.name = d.original.name + "_Broken";
                d.mf.sharedMesh = d.copy;
                d.dirty = true;
            }
            if (lightsTris == null)
            {
                lightsTris = new int[d.original.subMeshCount][];
                for (int i = 0; i < lightsTris.Length; i++) lightsTris[i] = d.original.GetTriangles(i);
                lightsMats = r.sharedMaterials;
            }
            if (brokenLamp == null) brokenLamp = MaterialLibrary.Standard("BrokenLamp", new Color(0.07f, 0.07f, 0.08f), 0.55f, 0.1f);
            int headSub = CarAnatomy.FindSubmesh(r, "Light_Head");
            int auxSub = CarAnatomy.FindSubmesh(r, "Light_Aux");
            var broken = new List<int>();
            int subs = lightsTris.Length;
            d.copy.subMeshCount = subs + 1;
            var keep = new List<int>();
            for (int s = 0; s < subs; s++)
            {
                var src = lightsTris[s];
                bool[] flags = s == headSub ? state.head : s == auxSub ? state.aux : null;
                if (flags == null || s >= lightsMats.Length) { d.copy.SetTriangles(src, s); continue; }
                keep.Clear();
                for (int i = 0; i + 2 < src.Length; i += 3)
                {
                    Vector3 c = (d.carV[src[i]] + d.carV[src[i + 1]] + d.carV[src[i + 2]]) / 3f;
                    int side = c.x < 0f ? 0 : 1;
                    var list = c.z > 0f && flags[side] ? broken : keep;
                    list.Add(src[i]); list.Add(src[i + 1]); list.Add(src[i + 2]);
                }
                d.copy.SetTriangles(keep, s);
            }
            d.copy.SetTriangles(broken, subs);
            var mats = new Material[subs + 1];
            for (int i = 0; i < subs; i++) mats[i] = i < lightsMats.Length ? lightsMats[i] : brokenLamp;
            mats[subs] = brokenLamp;
            r.sharedMaterials = mats;
        }

        // ------------------------------------------------------------------ bits & shards

        void EnsureBits()
        {
            if (bits != null) return;
            var go = new GameObject("ImpactBits");
            bits = go.AddComponent<ParticleSystem>();
            bits.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = bits.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.maxParticles = 160;
            var em = bits.emission;
            em.rateOverTime = 0f;
            var rot = bits.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
            var col = bits.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            if (GameSettings.Quality >= 2)
            {
                var c = bits.collision;
                c.enabled = true;
                c.type = ParticleSystemCollisionType.World;
                c.quality = ParticleSystemCollisionQuality.Low;
                c.bounce = 0.3f;
                c.dampen = 0.5f;
                c.collidesWith = ~(1 << CarExtras.CarLayer);
            }
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = MaterialLibrary.Particle("CarShard", CarDetailTextures.Shard, Color.white, 0.7f);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bits.Play();
        }

        void EmitBits(Vector3 pos, Vector3 outDir, float s, bool glass)
        {
            int count = glass ? 22 : Mathf.RoundToInt(4f + 14f * s);
            if (GameSettings.Quality == 0) count /= 2;
            if (count <= 0) return;
            EnsureBits();
            Vector3 v0 = car != null && car.Body != null ? car.Body.GetVelocity() * 0.6f : Vector3.zero;
            Color paint = LiveryLibrary.PaintKey(anatomy.id);
            for (int i = 0; i < count; i++)
            {
                Vector3 v = v0 + (outDir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f).normalized * Random.Range(1.5f, 4.5f);
                Color c = glass ? new Color(0.92f, 0.95f, 1f, 0.95f) : (Random.value < 0.5f ? paint : new Color(0.08f, 0.08f, 0.09f));
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + Random.insideUnitSphere * 0.12f,
                    velocity = v,
                    startColor = c,
                    startSize = glass ? Random.Range(0.025f, 0.06f) : Random.Range(0.03f, 0.08f),
                    startLifetime = Random.Range(1.2f, 2.4f)
                };
                bits.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ bumpers

        void LateUpdate()
        {
            if (state == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // vertical jolts make a loose bumper bounce
            Vector3 pos = transform.position;
            float vy = (pos.y - prevPos.y) / dt;
            if ((pos - prevPos).sqrMagnitude > 25f) vy = prevVy;
            prevPos = pos;
            float ay = Mathf.Clamp((vy - prevVy) / dt, -60f, 60f);
            prevVy = vy;
            if (state.hangF > 0f && bumperF != null)
            {
                Spring(ref wobF, ref wobVF, ay, dt);
                Pose(bumperF, anatomy.bumperFB, restPosF, restRotF, state.hangF + wobF, state.sideF, true);
            }
            if (state.hangR > 0f && bumperR != null)
            {
                Spring(ref wobR, ref wobVR, ay, dt);
                Pose(bumperR, anatomy.bumperRB, restPosR, restRotR, state.hangR + wobR, state.sideR, false);
            }
        }

        static void Spring(ref float x, ref float v, float ay, float dt)
        {
            v += (-x * 90f - v * 3.5f - ay * 0.35f) * dt;
            x += v * dt;
            x = Mathf.Clamp(x, -6f, 6f);
        }

        void Pose(Transform t, Bounds b, Vector3 restPos, Quaternion restRot, float angle, int side, bool front)
        {
            angle = Mathf.Max(0f, angle);
            Vector3 pivot = new Vector3(b.center.x - side * b.extents.x * 0.95f, b.center.y, b.center.z);
            Quaternion R = Quaternion.AngleAxis(-side * angle, Vector3.forward) *
                           Quaternion.AngleAxis((front ? -side : side) * angle * 0.25f, Vector3.up);
            Vector3 p = pivot + R * (restPos - pivot);
            t.SetPositionAndRotation(transform.TransformPoint(p), transform.rotation * (R * restRot));
        }

        // ------------------------------------------------------------------ repair

        /// <summary>Restore the car: dents, lamps, windscreen, bumpers and the mechanical values.</summary>
        public void Repair()
        {
            if (state == null) return;
            state.dents.Clear();
            state.cracks.Clear();
            state.head[0] = state.head[1] = state.aux[0] = state.aux[1] = false;
            state.hangF = state.hangR = 0f;
            state.power = 1f; state.grip = 1f; state.pull = 0f;
            wobF = wobVF = wobR = wobVR = 0f;
            ApplyLamps();
            ApplyDents();
            if (bumperF != null) bumperF.SetPositionAndRotation(transform.TransformPoint(restPosF), transform.rotation * restRotF);
            if (bumperR != null) bumperR.SetPositionAndRotation(transform.TransformPoint(restPosR), transform.rotation * restRotR);
            if (DamageChanged != null) DamageChanged();
        }

        /// <summary>Forget the saved damage of a car that is not currently spawned.</summary>
        public static void RepairSaved(string carId)
        {
            State s;
            if (carId != null && saved.TryGetValue(carId, out s)) saved.Remove(carId);
        }

        /// <summary>True if the car (spawned or not) carries damage from this session.</summary>
        public static bool HasDamage(string carId)
        {
            State s;
            if (carId == null || !saved.TryGetValue(carId, out s)) return false;
            return s.dents.Count > 0 || s.cracks.Count > 0 || s.head[0] || s.head[1] || s.aux[0] || s.aux[1] ||
                   s.hangF > 0f || s.hangR > 0f || s.power < 1f || s.grip < 1f || s.pull != 0f;
        }
    }
}

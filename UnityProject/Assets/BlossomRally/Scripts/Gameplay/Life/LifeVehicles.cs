// LifeVehicles.cs - the look of everything the world-life module moves or parks:
//   * ambient / parked cars: the real CarCatalog model (own paint and light materials, so the player's livery,
//     dirt and brake lights never leak onto them) in a LODGroup with a 60-triangle vertex-coloured proxy for the
//     distance, a body box collider, optional kinematic body, and additive head-light cones / tail glows;
//   * the jade city bus, the valley bicycle (with a PersonFactory rider), the harbour fishing boat, benches and
//     bus-stop signs: low-poly vertex-coloured meshes (one renderer each, built once and shared).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class VehicleRig
    {
        public GameObject go;
        public Transform tr;
        public Rigidbody rb;
        public BoxCollider col;
        public string id;
        public float halfW = 0.95f, halfL = 2.2f, height = 1.4f;
        public Transform[] wheels;
        public Quaternion[] wheelRest;
        public float wheelR = 0.33f, spin;
        public Renderer head, tail, windows;
        public bool lightsOn, brakeOn;
        public PersonRig rider;
    }

    internal static class LifeVehicles
    {
        static readonly Dictionary<string, Material> paints = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> proxies = new Dictionary<string, Mesh>();
        static Material headMat, tailMat, lampHead, lampTail;
        static MaterialPropertyBlock mpb;
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static float lastNight = -1f;

        /// <summary>Civilian paint palette (LiveryLibrary colour indices; 0 = factory livery).</summary>
        static readonly int[] PaintChoices = { 0, 0, 1, 2, 3, 4, 5, 5, 6, 7, 8, 9 };

        public static int RandomPaint(System.Random r) { return PaintChoices[r.Next(PaintChoices.Length)]; }

        static Material Paint(string id, int colour)
        {
            string key = id + "_" + colour;
            Material m;
            if (paints.TryGetValue(key, out m) && m != null) return m;
            m = new Material(MaterialLibrary.CarPaint(id)) { name = "BR_LifePaint_" + key };
            if (m.HasProperty("_Pattern")) m.SetFloat("_Pattern", 0f);
            if (m.HasProperty("_Dirt")) m.SetFloat("_Dirt", 0f);
            if (m.HasProperty("_PaintColor"))
            {
                m.SetColor("_PaintColor", LiveryLibrary.Colour(colour));
                m.SetFloat("_PaintMix", colour == 0 ? 0f : 1f);
            }
            paints[key] = m;
            return m;
        }

        static Material LampHead
        {
            get
            {
                if (lampHead == null)
                {
                    lampHead = new Material(MaterialLibrary.FindShader("Standard")) { name = "BR_LifeLampHead" };
                    lampHead.SetColor("_Color", new Color(1f, 0.97f, 0.9f));
                    lampHead.SetFloat("_Glossiness", 0.8f);
                    lampHead.EnableKeyword("_EMISSION");
                    lampHead.SetColor("_EmissionColor", new Color(1.4f, 1.3f, 1.1f));
                }
                return lampHead;
            }
        }

        static Material LampTail
        {
            get
            {
                if (lampTail == null)
                {
                    lampTail = new Material(MaterialLibrary.FindShader("Standard")) { name = "BR_LifeLampTail" };
                    lampTail.SetColor("_Color", new Color(0.7f, 0.06f, 0.06f));
                    lampTail.SetFloat("_Glossiness", 0.7f);
                    lampTail.EnableKeyword("_EMISSION");
                    lampTail.SetColor("_EmissionColor", new Color(1.2f, 0.08f, 0.05f));
                }
                return lampTail;
            }
        }

        static Material HeadMat
        {
            get
            {
                if (headMat == null)
                {
                    headMat = new Material(LifeMesh.Additive) { name = "BR_LifeHeadBeams" };
                    if (headMat.HasProperty("_Intensity")) headMat.SetFloat("_Intensity", 0.6f);
                }
                return headMat;
            }
        }

        static Material TailMat
        {
            get
            {
                if (tailMat == null)
                {
                    tailMat = new Material(LifeMesh.Additive) { name = "BR_LifeTailGlow" };
                    if (tailMat.HasProperty("_Intensity")) tailMat.SetFloat("_Intensity", 0.9f);
                }
                return tailMat;
            }
        }

        /// <summary>Once per frame: lamp emission follows the night (one material update when it changes).</summary>
        public static void UpdateNight(float night)
        {
            if (Mathf.Abs(night - lastNight) < 0.02f) return;
            lastNight = night;
            if (lampHead != null) lampHead.SetColor("_EmissionColor", new Color(1.4f, 1.3f, 1.1f) * (0.6f + night * 2.6f));
            if (lampTail != null) lampTail.SetColor("_EmissionColor", new Color(1.2f, 0.08f, 0.05f) * (0.7f + night * 1.6f));
            if (headMat != null && headMat.HasProperty("_Intensity")) headMat.SetFloat("_Intensity", 0.25f + 0.55f * night);
        }

        // ------------------------------------------------------------------ cars
        /// <summary>Build a car from its CarCatalog model. Returns null when the model is missing.</summary>
        public static VehicleRig BuildCar(string id, int colour, Transform parent, bool kinematicBody, bool lights)
        {
            var prefab = Resources.Load<GameObject>("Cars/" + id + "/" + id + "_Export");
            if (prefab == null) return null;
            var go = new GameObject("LifeCar_" + id);
            go.transform.SetParent(parent, false);
            var model = Object.Instantiate(prefab, go.transform, false);
            model.name = id + "_LifeModel";
            AxisFix.Calibrate();
            AxisFix.Apply(model.transform);
            foreach (var an in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(an);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            MaterialLibrary.Remap(model, id);
            var paintSrc = MaterialLibrary.TryGet("Paint_" + id);
            var tailSrc = MaterialLibrary.TryGet("Std_M_Light_Tail");
            var headSrc = MaterialLibrary.TryGet("Std_M_Light_Head");
            var paint = Paint(id, colour);
            var rens = model.GetComponentsInChildren<Renderer>();
            bool shadows = GameSettings.Quality >= 1;
            Bounds b = new Bounds(new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.3f, 4.2f));
            bool first = true;
            foreach (var r in rens)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i] == paintSrc) mats[i] = paint;
                    else if (mats[i] == tailSrc) mats[i] = LampTail;
                    else if (mats[i] == headSrc) mats[i] = LampHead;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            var rig = new VehicleRig { go = go, tr = go.transform, id = id };
            // the parent may be posed anywhere: bounds into the car's own space
            Vector3 lc = go.transform.InverseTransformPoint(b.center);
            b = new Bounds(lc, b.size);
            rig.halfW = Mathf.Clamp(b.extents.x, 0.6f, 1.3f);
            rig.halfL = Mathf.Clamp(b.extents.z, 1.4f, 3.2f);
            rig.height = Mathf.Clamp(b.max.y, 1.0f, 2.4f);

            // wheels (spun while near)
            var data = CarFactory.LoadData(id);
            rig.wheels = new Transform[4];
            rig.wheelRest = new Quaternion[4];
            string[] slots = { "FL", "FR", "RL", "RR" };
            for (int i = 0; i < 4; i++)
            {
                rig.wheels[i] = CarFactory.FindDeep(model.transform, id + "_Wheel_" + slots[i]);
                if (rig.wheels[i] != null) rig.wheelRest[i] = Quaternion.Inverse(go.transform.rotation) * rig.wheels[i].rotation;
            }
            if (data != null && data.wheel_list != null)
                foreach (var w in data.wheel_list) if (w != null && w.radius > 0.1f) { rig.wheelR = w.radius; break; }

            // distance proxy + LOD
            Color body = colour == 0 ? LiveryLibrary.PaintKey(id) : LiveryLibrary.Colour(colour);
            var proxy = new GameObject("Proxy");
            proxy.transform.SetParent(go.transform, false);
            proxy.AddComponent<MeshFilter>().sharedMesh = Proxy(id, colour, b, body);
            var pr = proxy.AddComponent<MeshRenderer>();
            pr.sharedMaterial = LifeMesh.VertexColour;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            var lod = go.AddComponent<LODGroup>();
            float q = GameSettings.Quality;
            float near = q <= 0 ? 0.085f : q <= 1 ? 0.06f : q <= 2 ? 0.045f : 0.032f;
            lod.SetLODs(new[] { new LOD(near, rens), new LOD(0.008f, new Renderer[] { pr }) });
            lod.RecalculateBounds();

            if (lights) AddLights(rig, b);

            // body collider (kept above the road; the nudge physics slides on it)
            var col = go.AddComponent<BoxCollider>();
            float bottom = Mathf.Max(0.22f, b.min.y + 0.06f);
            col.center = new Vector3(b.center.x, (bottom + b.max.y) * 0.5f, b.center.z);
            col.size = new Vector3(b.size.x * 0.92f, Mathf.Max(0.4f, b.max.y - bottom), b.size.z * 0.95f);
            rig.col = col;
            if (kinematicBody)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                CarSpec spec = null;
                foreach (var s in CarCatalog.Cars) if (s.id == id) { spec = s; break; }
                rb.mass = spec != null ? spec.mass : 1300f;
                rig.rb = rb;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CarExtras.CarLayer;
            return rig;
        }

        static Mesh Proxy(string id, int colour, Bounds b, Color body)
        {
            string key = id + "_" + colour;
            Mesh m;
            if (proxies.TryGetValue(key, out m) && m != null) return m;
            var g = new LifeGeo();
            Color32 paint = LifeMesh.C(body), dark = LifeGeo.C(40, 42, 48), glass = LifeGeo.C(70, 86, 104), tyre = LifeGeo.C(26, 26, 28);
            float hw = b.extents.x * 0.95f, hl = b.extents.z * 0.97f;
            float y0 = Mathf.Max(0.25f, b.min.y + 0.2f), y1 = b.min.y + (b.max.y - b.min.y) * 0.55f, y2 = b.max.y;
            Vector3 c = b.center;
            PG.Box2(g, new Vector3(c.x, (y0 + y1) * 0.5f, c.z), new Vector3(hw, (y1 - y0) * 0.5f, hl), paint, paint);
            PG.Box2(g, new Vector3(c.x, (y1 + y2) * 0.5f, c.z - hl * 0.08f), new Vector3(hw * 0.86f, (y2 - y1) * 0.5f, hl * 0.52f), glass, paint);
            float wr = 0.32f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.Box2(g, new Vector3(c.x + sx * (hw - 0.08f), wr, c.z + sz * hl * 0.66f), new Vector3(0.13f, wr, wr), tyre, tyre);
            PG.Box2(g, new Vector3(c.x, y0 + 0.05f, c.z + hl - 0.02f), new Vector3(hw * 0.9f, 0.06f, 0.03f), dark, dark);
            m = g.ToMesh("LifeProxy_" + key);
            m.UploadMeshData(true);
            proxies[key] = m;
            return m;
        }

        static void AddLights(VehicleRig rig, Bounds b)
        {
            float hw = b.extents.x, front = b.max.z - 0.1f, back = b.min.z + 0.08f;
            float hy = b.min.y + (b.max.y - b.min.y) * 0.36f;
            var hg = new LifeGeo();
            Color32 warm = LifeGeo.C(255, 236, 196, 150), warm0 = LifeGeo.C(255, 236, 196, 0);
            Color32 core = LifeGeo.C(255, 246, 220, 255), core0 = LifeGeo.C(255, 246, 220, 0);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lamp = new Vector3(b.center.x + s * (hw - 0.32f), hy, front);
                hg.Frustum(lamp, lamp + new Vector3(s * 0.4f, -0.55f, 9f), 0.14f, 0.1f, 1.7f, 1.0f, 6, 0f, warm, warm0, false, false);
                hg.Disc(lamp + Vector3.forward * 0.03f, 0.32f, 8, 0f, Vector3.forward, core, core0);
            }
            var head = LifeMesh.Make("HeadBeams", hg, HeadMat, rig.tr, false, CarExtras.CarLayer);
            rig.head = head.GetComponent<Renderer>();
            rig.head.enabled = false;
            var tg = new LifeGeo();
            Color32 red = LifeGeo.C(255, 40, 30, 200), red0 = LifeGeo.C(255, 40, 30, 0);
            for (int s = -1; s <= 1; s += 2)
                tg.Disc(new Vector3(b.center.x + s * (hw - 0.3f), hy + 0.05f, back), 0.3f, 8, 0f, Vector3.back, red, red0);
            var tail = LifeMesh.Make("TailGlow", tg, TailMat, rig.tr, false, CarExtras.CarLayer);
            rig.tail = tail.GetComponent<Renderer>();
            rig.tail.enabled = false;
        }

        /// <summary>Show / hide head and tail glows (only touches renderers when something changed).</summary>
        public static void SetLights(VehicleRig rig, bool on, bool brake)
        {
            if (rig == null) return;
            if (rig.head != null && rig.lightsOn != on) rig.head.enabled = on;
            if (rig.windows != null && rig.lightsOn != on) rig.windows.enabled = on;
            if (rig.tail != null && (rig.lightsOn != on || rig.brakeOn != brake))
            {
                rig.tail.enabled = on || brake;
                if (mpb == null) mpb = new MaterialPropertyBlock();
                mpb.SetColor(IdColor, brake ? new Color(2.2f, 2.2f, 2.2f, 1f) : Color.white);
                rig.tail.SetPropertyBlock(mpb);
            }
            rig.lightsOn = on;
            rig.brakeOn = brake;
        }

        /// <summary>Spin the wheel meshes (only for cars close to the camera).</summary>
        public static void SpinWheels(VehicleRig rig, float ds)
        {
            if (rig.wheels == null) return;
            rig.spin = (rig.spin + ds / Mathf.Max(0.2f, rig.wheelR) * Mathf.Rad2Deg) % 360f;
            Quaternion rot = rig.tr.rotation, spin = Quaternion.AngleAxis(rig.spin, Vector3.right);
            for (int i = 0; i < 4; i++)
                if (rig.wheels[i] != null) rig.wheels[i].rotation = rot * spin * rig.wheelRest[i];
        }

        // ------------------------------------------------------------------ city bus
        static Mesh busMesh, busGlow;

        public static VehicleRig BuildBus(Transform parent)
        {
            if (busMesh == null)
            {
                var g = new LifeGeo();
                Color32 jade = LifeGeo.C(62, 160, 136), cream = LifeGeo.C(240, 234, 214), roof = LifeGeo.C(226, 228, 226);
                Color32 glass = LifeGeo.C(52, 70, 86), tyre = LifeGeo.C(28, 28, 30), red = LifeGeo.C(206, 64, 60), dark = LifeGeo.C(46, 48, 52);
                PG.Box2(g, new Vector3(0f, 0.95f, 0f), new Vector3(1.25f, 0.6f, 5.5f), jade, jade);
                PG.Box2(g, new Vector3(0f, 1.62f, 0f), new Vector3(1.26f, 0.08f, 5.52f), red, red);
                PG.Box2(g, new Vector3(0f, 2.15f, 0f), new Vector3(1.24f, 0.45f, 5.45f), glass, glass);
                PG.Box2(g, new Vector3(0f, 2.78f, 0f), new Vector3(1.25f, 0.18f, 5.5f), cream, roof);
                PG.Box2(g, new Vector3(0f, 3.0f, -1.2f), new Vector3(0.7f, 0.08f, 1.4f), roof, roof);         // roof unit
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int k = -2; k <= 2; k++) PG.Box2(g, new Vector3(s * 1.255f, 2.15f, k * 2.1f), new Vector3(0.02f, 0.47f, 0.07f), cream, cream);   // pillars
                    PG.Box2(g, new Vector3(s * 1.1f, 0.48f, 3.6f), new Vector3(0.2f, 0.48f, 0.48f), tyre, tyre);
                    PG.Box2(g, new Vector3(s * 1.1f, 0.48f, -3.4f), new Vector3(0.2f, 0.48f, 0.48f), tyre, tyre);
                    PG.Box2(g, new Vector3(s * 0.75f, 0.75f, 5.52f), new Vector3(0.22f, 0.1f, 0.03f), LifeGeo.C(250, 244, 220), LifeGeo.C(250, 244, 220));
                }
                PG.Box2(g, new Vector3(0f, 0.55f, 5.53f), new Vector3(1.1f, 0.12f, 0.04f), dark, dark);                // bumper
                PG.Box2(g, new Vector3(0.55f, 1.6f, 5.51f), new Vector3(0.5f, 0.9f, 0.02f), glass, glass);            // door
                busMesh = g.ToMesh("LifeBus");
                busMesh.UploadMeshData(true);
                var w = new LifeGeo();
                Color32 lit = LifeGeo.C(255, 220, 150);
                for (int s = -1; s <= 1; s += 2) PG.Box2(w, new Vector3(s * 1.245f, 2.15f, 0f), new Vector3(0.01f, 0.38f, 5.2f), lit, lit);
                PG.Box2(w, new Vector3(0f, 2.62f, 5.47f), new Vector3(0.8f, 0.12f, 0.02f), LifeGeo.C(255, 170, 60), LifeGeo.C(255, 170, 60));
                busGlow = w.ToMesh("LifeBusWindows");
                busGlow.UploadMeshData(true);
            }
            var go = new GameObject("LifeBus");
            go.transform.SetParent(parent, false);
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = busMesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LifeMesh.VertexColour;
            mr.shadowCastingMode = GameSettings.Quality >= 1 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var win = new GameObject("Windows");
            win.transform.SetParent(go.transform, false);
            win.AddComponent<MeshFilter>().sharedMesh = busGlow;
            var wr = win.AddComponent<MeshRenderer>();
            wr.sharedMaterial = LifeMesh.Glow;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wr.enabled = false;
            var rig = new VehicleRig { go = go, tr = go.transform, id = "BUS", halfW = 1.3f, halfL = 5.6f, height = 3.1f, windows = wr };
            AddLights(rig, new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(2.5f, 3f, 11.1f)));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.7f, 0f);
            col.size = new Vector3(2.5f, 2.6f, 11f);
            rig.col = col;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = 9000f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rig.rb = rb;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CarExtras.CarLayer;
            return rig;
        }

        // ------------------------------------------------------------------ bicycle (+ rider)
        static readonly Dictionary<int, Mesh> bikeMeshes = new Dictionary<int, Mesh>();
        static readonly Color32[] BikePaint = { LifeGeo.C(226, 96, 112), LifeGeo.C(80, 170, 160), LifeGeo.C(236, 196, 90), LifeGeo.C(120, 150, 220), LifeGeo.C(240, 240, 236) };

        public static VehicleRig BuildBicycle(Transform parent, int seed)
        {
            int v = ((seed % BikePaint.Length) + BikePaint.Length) % BikePaint.Length;
            Mesh mesh;
            if (!bikeMeshes.TryGetValue(v, out mesh) || mesh == null)
            {
                var g = new LifeGeo();
                Color32 frame = BikePaint[v], tyre = LifeGeo.C(30, 30, 32), steel = LifeGeo.C(190, 190, 196), seat = LifeGeo.C(60, 44, 36), basket = LifeGeo.C(184, 142, 96);
                Vector3 rear = new Vector3(0f, 0.34f, -0.52f), front = new Vector3(0f, 0.34f, 0.52f);
                g.Frustum(rear + Vector3.left * 0.025f, rear + Vector3.right * 0.025f, 0.34f, 0.34f, 0.34f, 0.34f, 10, 0f, tyre, tyre, true, true);
                g.Frustum(front + Vector3.left * 0.025f, front + Vector3.right * 0.025f, 0.34f, 0.34f, 0.34f, 0.34f, 10, 0f, tyre, tyre, true, true);
                Vector3 crank = new Vector3(0f, 0.32f, -0.02f), seatTop = new Vector3(0f, 0.86f, -0.2f), head = new Vector3(0f, 0.84f, 0.38f);
                PG.Rod(g, rear, crank, 0.022f, frame);
                PG.Rod(g, rear, seatTop, 0.02f, frame);
                PG.Rod(g, crank, seatTop, 0.026f, frame);
                PG.Rod(g, crank, head, 0.028f, frame);
                PG.Rod(g, seatTop + new Vector3(0f, -0.06f, 0f), head, 0.024f, frame);
                PG.Rod(g, head, front, 0.022f, steel);
                PG.Rod(g, head, head + new Vector3(0f, 0.16f, -0.05f), 0.02f, steel);
                PG.Rod(g, head + new Vector3(-0.26f, 0.16f, -0.05f), head + new Vector3(0.26f, 0.16f, -0.05f), 0.018f, steel);
                PG.Box2(g, seatTop + new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.025f, 0.13f), seat, seat);
                PG.Box2(g, front + new Vector3(0f, 0.42f, 0.12f), new Vector3(0.17f, 0.1f, 0.13f), basket, basket);
                mesh = g.ToMesh("LifeBicycle_" + v);
                mesh.UploadMeshData(true);
                bikeMeshes[v] = mesh;
            }
            var go = new GameObject("LifeBicycle");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LifeMesh.VertexColour;
            mr.shadowCastingMode = GameSettings.Quality >= 2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var rig = new VehicleRig { go = go, tr = go.transform, id = "BICYCLE", halfW = 0.35f, halfL = 0.9f, height = 1.7f };
            var look = PersonLook.Random(9000 + seed * 31);
            look.hat = seed % 3 == 0;
            var rider = PersonFactory.Create(look, Vector3.zero, 0f, go.transform, 0);
            if (rider != null)
            {
                rider.transform.localPosition = new Vector3(0f, 0.22f, -0.12f);
                rider.transform.localRotation = Quaternion.identity;
                StripPhysics(rider.gameObject);
                rider.SetPose(PersonPose.Ride);
                rig.rider = rider;
            }
            return rig;
        }

        /// <summary>Ambient people never block the car: their colliders are switched off, bodies made kinematic.</summary>
        public static void StripPhysics(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var cc in go.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
        }

        // ------------------------------------------------------------------ fishing boat
        static readonly Color32[] HullPaint = { LifeGeo.C(64, 120, 186), LifeGeo.C(196, 70, 62), LifeGeo.C(60, 150, 130), LifeGeo.C(232, 226, 210) };

        public static GameObject BuildBoat(Transform parent, int seed, out Renderer lantern)
        {
            var g = new LifeGeo();
            Color32 hull = HullPaint[((seed % HullPaint.Length) + HullPaint.Length) % HullPaint.Length];
            Color32 hullD = LifeGeo.Shade(hull, 0.75f), deck = LifeGeo.C(170, 128, 86), white = LifeGeo.C(238, 234, 224), red = LifeGeo.C(190, 52, 44), wood = LifeGeo.C(120, 84, 56);
            // hull outline (half widths) at deck (y 0.9) and chine (y 0.05)
            float[] z = { -3.8f, -2f, 0.5f, 2.6f, 4.4f };
            float[] wDeck = { 1.2f, 1.5f, 1.5f, 1.1f, 0.05f };
            float[] wChine = { 0.9f, 1.05f, 1.0f, 0.6f, 0.02f };
            for (int i = 0; i < z.Length - 1; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = new Vector3(s * wDeck[i], 0.9f, z[i]), b = new Vector3(s * wDeck[i + 1], 0.9f, z[i + 1]);
                    Vector3 c = new Vector3(s * wChine[i + 1], 0.05f, z[i + 1]), d = new Vector3(s * wChine[i], 0.05f, z[i]);
                    g.QuadOut(a, b, c, d, new Vector3(s, -0.2f, 0f), hull, hull, hullD, hullD);
                    // deck
                    g.QuadOut(new Vector3(0f, 0.9f, z[i]), new Vector3(0f, 0.9f, z[i + 1]), b, a, Vector3.up, deck, deck, deck, deck);
                    // bottom
                    g.QuadOut(new Vector3(0f, 0.0f, z[i]), new Vector3(0f, 0.0f, z[i + 1]), c, d, Vector3.down, hullD, hullD, hullD, hullD);
                }
            g.QuadOut(new Vector3(-wDeck[0], 0.9f, z[0]), new Vector3(wDeck[0], 0.9f, z[0]), new Vector3(wChine[0], 0.05f, z[0]), new Vector3(-wChine[0], 0.05f, z[0]), Vector3.back, hull, hull, hullD, hullD);
            PG.Box2(g, new Vector3(0f, 1.0f, 0f), new Vector3(1.52f, 0.08f, 3.0f), white, white);           // gunwale band
            PG.Box2(g, new Vector3(0f, 1.75f, -1.6f), new Vector3(0.95f, 0.75f, 1.1f), white, white);       // wheelhouse
            PG.Box2(g, new Vector3(0f, 2.15f, -0.92f), new Vector3(0.8f, 0.25f, 0.02f), LifeGeo.C(60, 80, 96), LifeGeo.C(60, 80, 96));
            PG.Box2(g, new Vector3(0f, 2.58f, -1.6f), new Vector3(1.1f, 0.08f, 1.3f), red, red);            // roof
            PG.Rod(g, new Vector3(0f, 0.9f, 1.2f), new Vector3(0f, 5.2f, 1.2f), 0.06f, wood);                // mast
            PG.Rod(g, new Vector3(0f, 3.2f, 1.2f), new Vector3(0f, 1.6f, 4.0f), 0.04f, wood);                // net boom
            g.QuadOut(new Vector3(0f, 5.1f, 1.2f), new Vector3(0f, 5.1f, 0.4f), new Vector3(0f, 4.6f, 0.4f), new Vector3(0f, 4.6f, 1.2f), Vector3.right, red, red, red, red);
            g.QuadOut(new Vector3(0f, 5.1f, 1.2f), new Vector3(0f, 4.6f, 1.2f), new Vector3(0f, 4.6f, 0.4f), new Vector3(0f, 5.1f, 0.4f), Vector3.left, red, red, red, red);
            var go = LifeMesh.Make("LifeFishingBoat", g, LifeMesh.VertexColour, parent, GameSettings.Quality >= 1);
            var lg = new LifeGeo();
            Color32 lamp = LifeGeo.C(255, 200, 120);
            PG.Box2(lg, new Vector3(0f, 4.4f, 1.2f), new Vector3(0.14f, 0.18f, 0.14f), lamp, lamp);
            PG.Box2(lg, new Vector3(0f, 1.9f, -0.48f), new Vector3(0.5f, 0.2f, 0.02f), lamp, lamp);
            var l = LifeMesh.Make("Lantern", lg, LifeMesh.Glow, go.transform, false);
            lantern = l.GetComponent<Renderer>();
            lantern.enabled = false;
            return go;
        }

        // ------------------------------------------------------------------ benches and bus stops (merged into a LifeGeo)
        /// <summary>A park bench (1.7 m, seat 0.45 m high) facing +Z, stamped at pos / yaw.</summary>
        public static void Bench(LifeGeo g, Vector3 pos, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Color32 wood = LifeGeo.C(150, 98, 62), woodL = LifeGeo.C(176, 120, 78), stone = LifeGeo.C(180, 176, 168);
            for (int s = -1; s <= 1; s += 2)
                PG.OBox(g, pos + q * new Vector3(s * 0.7f, 0.21f, -0.02f), new Vector3(0.07f, 0.21f, 0.22f), q, stone);
            for (int k = 0; k < 3; k++)
                PG.OBox(g, pos + q * new Vector3(0f, 0.45f, 0.14f - k * 0.14f), new Vector3(0.85f, 0.025f, 0.055f), q, k == 1 ? woodL : wood);
            for (int k = 0; k < 2; k++)
                PG.OBox(g, pos + q * new Vector3(0f, 0.62f + k * 0.17f, -0.24f), new Vector3(0.85f, 0.05f, 0.025f), q, k == 1 ? woodL : wood);
            for (int s = -1; s <= 1; s += 2)
                PG.OBox(g, pos + q * new Vector3(s * 0.7f, 0.62f, -0.25f), new Vector3(0.04f, 0.2f, 0.03f), q, wood);
        }

        /// <summary>A bus-stop sign (pole, round jade sign, timetable board) at pos.</summary>
        public static void BusStop(LifeGeo g, Vector3 pos, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Color32 pole = LifeGeo.C(200, 200, 204), jade = LifeGeo.C(62, 160, 136), white = LifeGeo.C(246, 244, 236), board = LifeGeo.C(226, 210, 160);
            PG.Rod(g, pos, pos + Vector3.up * 2.7f, 0.05f, pole, 6);
            g.Frustum(pos + q * new Vector3(0f, 2.55f, -0.03f), pos + q * new Vector3(0f, 2.55f, 0.03f), 0.32f, 0.32f, 0.32f, 0.32f, 10, 0f, jade, jade, true, true);
            PG.OBox(g, pos + q * new Vector3(0f, 2.55f, 0.04f), new Vector3(0.2f, 0.06f, 0.005f), q, white);
            PG.OBox(g, pos + q * new Vector3(0f, 1.45f, 0.06f), new Vector3(0.22f, 0.3f, 0.02f), q, board);
            Bench(g, pos + q * new Vector3(1.6f, 0f, 0.3f), yaw);
        }
    }
}

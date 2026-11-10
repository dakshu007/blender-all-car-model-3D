// Garage.cs - the car-select scene: a lacquered turntable inside an open-front village workshop
// (GarageWorkshop: benches, tyres, lift, lanterns, warm pendant lights and three mechanics who
// react when you change cars), a slowly drifting camera with smooth fly-in / car-swap transitions,
// the livery picker (colour + pattern) and the showroom walk-around: free orbit (mouse drag / sticks
// / arrows), zoom (wheel / triggers / Q E) and preset views (front, 3/4, side, rear, interior) with
// a spec sheet. Toggle the showroom with V or gamepad Y while in the garage.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(1000)]     // runs after the camera rig and GameManager so it owns the camera while active
    public class Garage : MonoBehaviour
    {
        /// <summary>Turntable centre (x, z) - kept flat and free of plants by WorldBuilder.</summary>
        public static Vector2 Spot { get { return MapData.Garage + new Vector2(2f, -15f); } }
        public bool Active { get; private set; }
        /// <summary>When false (title screen) the car still turns on the table but the camera is free.</summary>
        public bool OwnsCamera = true;
        public Vector3 TablePos { get; private set; }
        /// <summary>True while the showroom walk-around is open (GameManager should skip its garage keys + panel).</summary>
        public bool Showroom { get { return showroom; } }

        Transform table;
        readonly System.Collections.Generic.List<Light> lights = new System.Collections.Generic.List<Light>();
        GarageWorkshop workshop;
        float spin, camT;
        int colour, pattern;
        string carId, lastShownId;

        // camera transitions
        bool wasOwning;
        Vector3 fromPos;
        Quaternion fromRot = Quaternion.identity;
        float fromFov = 40f, transStart = -10f, transDur = 1f, transArc;
        float swapT = -10f, dropT = -10f;

        // showroom
        bool showroom, autoSpin, hidePanel, interior;
        int preset = 1;
        float spinTarget, yaw, pitch = 10f, dist = 6f, yawT, pitchT = 10f, distT = 6f, iyaw, ipitch, iyawT, ipitchT;
        float showroomTime;

        struct View { public string name; public float spin, yaw, pitch, dist; public bool interior; }
        static readonly View[] Views =
        {
            new View { name = "Front", spin = 0f, yaw = 0f, pitch = 8f, dist = 5.6f },
            new View { name = "3/4", spin = 32f, yaw = -24f, pitch = 13f, dist = 6.3f },
            new View { name = "Side", spin = 90f, yaw = 0f, pitch = 6f, dist = 6.0f },
            new View { name = "Rear", spin = 180f, yaw = 8f, pitch = 11f, dist = 5.8f },
            new View { name = "Interior", spin = 0f, yaw = 0f, pitch = 0f, dist = 0f, interior = true },
        };
        public string ViewName { get { return Views[preset].name; } }

        public void Build(WorldBuilder w)
        {
            // open ground beside the garage, ~15 m clear of Lantern Street and the houses
            Vector2 sp = Spot;
            float baseY = w.HeightAt(sp.x, sp.y) + 0.02f, maxH = baseY;
            for (float x = -7.5f; x <= 7.51f; x += 2.5f)
                for (float z = -6.8f; z <= 5.7f; z += 2.5f)
                    maxH = Mathf.Max(maxH, w.HeightAt(sp.x + x, sp.y + z) + 0.03f);
            float floorY = Mathf.Clamp(maxH, baseY, baseY + 0.8f);
            Vector3 p = new Vector3(sp.x, floorY, sp.y);
            TablePos = p + Vector3.up * 0.16f;
            PlacementRegistry.Reserve(sp + new Vector2(0f, -0.5f), 10.5f);

            var root = new GameObject("Garage_Turntable");
            root.transform.position = p;
            table = root.transform;
            BuildDisc(root.transform);
            workshop = new GarageWorkshop();
            try
            {
                workshop.Build(w, p, null);
                workshop.AddLights(lights);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                workshop = null;
            }
            SetLights(0f);
        }

        void BuildDisc(Transform parent)
        {
            const int seg = 48;
            const float r = 3.3f, h = 0.16f;
            var mesh = new Mesh { name = "Turntable" };
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color32>();
            var t = new System.Collections.Generic.List<int>();
            Color32 top = new Color32(214, 196, 170, 0), rim = new Color32(170, 34, 40, 0), inlay = new Color32(196, 150, 70, 0);
            int centre = v.Count; v.Add(new Vector3(0, h, 0)); c.Add(top);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // top: inner stone, gold inlay ring, red lacquer rim
                v.Add(d * (r * 0.78f) + Vector3.up * h); c.Add(top);
                v.Add(d * (r * 0.82f) + Vector3.up * h); c.Add(inlay);
                v.Add(d * (r * 0.86f) + Vector3.up * h); c.Add(inlay);
                v.Add(d * r + Vector3.up * h); c.Add(rim);
                v.Add(d * r); c.Add(rim);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i * 5, b = 1 + (i + 1) * 5;
                t.AddRange(new[] { centre, b, a });
                for (int k = 0; k < 4; k++) t.AddRange(new[] { a + k, b + k, a + k + 1, a + k + 1, b + k, b + k + 1 });
            }
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            var tris = mesh.triangles;
            var vv = mesh.vertices;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(vv[tris[i + 1]] - vv[tris[i]], vv[tris[i + 2]] - vv[tris[i]]);
                Vector3 mid = (vv[tris[i]] + vv[tris[i + 1]] + vv[tris[i + 2]]) / 3f;
                Vector3 outward = mid.y > h - 0.01f && Mathf.Abs(vv[tris[i]].y - vv[tris[i + 1]].y) < 0.001f && Mathf.Abs(vv[tris[i]].y - vv[tris[i + 2]].y) < 0.001f
                    ? Vector3.up : new Vector3(mid.x, 0f, mid.z);
                if (Vector3.Dot(n, outward) < 0f) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Disc");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Terrain;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void SetLights(float k)
        {
            float night = Mathf.Lerp(0.6f, 1.6f, Atmosphere.NightFactor);
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = k * night * (i == 2 ? 0.7f : 1.15f);
                lights[i].enabled = k > 0.01f;
            }
        }

        /// <summary>Show a car on the turntable (kinematic, no controls).</summary>
        public void Show(ArcadeCar car)
        {
            bool wasActive = Active;
            Active = true;
            if (car == null) return;
            carId = car.spec.id;
            if (workshop != null)
            {
                if (!wasActive) workshop.ResetCrew();
                else if (lastShownId != null && lastShownId != carId)
                {
                    workshop.React();
                    swapT = dropT = Time.unscaledTime;
                }
            }
            lastShownId = carId;
            colour = GameSettings.LiveryColour(carId);
            pattern = GameSettings.LiveryPattern(carId);
            car.controlsEnabled = false;
            if (car.Body != null) { car.Body.SetVelocity(Vector3.zero); car.Body.angularVelocity = Vector3.zero; car.Body.isKinematic = true; }
            var ex = car.GetComponent<CarExtras>();
            if (ex != null) ex.ShowroomMode = true;
            Place(car);
        }

        /// <summary>Leave the garage: the car becomes a normal physics car again.</summary>
        public void Release(ArcadeCar car)
        {
            Active = false;
            showroom = false;
            wasOwning = false;
            SetLights(0f);
            if (car == null) return;
            if (car.Body != null) car.Body.isKinematic = false;
            var ex = car.GetComponent<CarExtras>();
            if (ex != null) ex.ShowroomMode = false;
        }

        void Place(ArcadeCar car)
        {
            Quaternion r = Quaternion.Euler(0f, spin, 0f);
            float t = Time.unscaledTime - dropT;
            float drop = t >= 0f && t < 1.2f ? 0.45f * Mathf.Exp(-t * 5.5f) * Mathf.Abs(Mathf.Cos(t * 11f)) : 0f;
            Vector3 p = TablePos + Vector3.up * drop;
            car.transform.SetPositionAndRotation(p, r);
            if (car.Body != null) { car.Body.position = p; car.Body.rotation = r; }
        }

        public void ChangeColour(int d)
        {
            colour = (colour + d + LiveryLibrary.ColourNames.Length) % LiveryLibrary.ColourNames.Length;
            Save();
        }

        public void ChangePattern(int d)
        {
            pattern = (pattern + d + LiveryLibrary.PatternNames.Length) % LiveryLibrary.PatternNames.Length;
            Save();
        }

        void Save()
        {
            if (carId == null) return;
            GameSettings.SetLivery(carId, colour, pattern);
            LiveryLibrary.Apply(carId, colour, pattern);
        }

        public string ColourName { get { return LiveryLibrary.ColourNames[colour]; } }
        public string PatternName { get { return LiveryLibrary.PatternNames[pattern]; } }

        // ================================================================== showroom
        /// <summary>Open / close the showroom walk-around (only while the garage owns the camera).</summary>
        public void SetShowroom(bool on)
        {
            if (on && (!Active || !OwnsCamera)) return;
            if (on == showroom) return;
            showroom = on;
            showroomTime = Time.unscaledTime;
            StartTransition(0.9f);
            if (on)
            {
                hidePanel = false;
                ApplyView(1, false);
                yaw = yawT; pitch = pitchT; dist = distT;
            }
            else interior = false;
            if (workshop != null) workshop.StepAside(on);
            UI.Sound("Camera");
        }

        void ApplyView(int i, bool sound = true)
        {
            preset = (i % Views.Length + Views.Length) % Views.Length;
            var v = Views[preset];
            bool wasInterior = interior;
            interior = v.interior;
            autoSpin = false;
            spinTarget = v.spin;
            if (!interior) { yawT = v.yaw; pitchT = v.pitch; distT = v.dist; }
            else { iyawT = 0f; ipitchT = 2f; iyaw = 0f; ipitch = 2f; }
            if (wasInterior != interior) StartTransition(0.8f);
            if (sound) UI.Sound("MenuMove");
        }

        void Update()
        {
            if (!Active || !OwnsCamera) return;
            if ((SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) || PhotoAlbum.IsOpen || ValleyMap.IsOpen) return;
            bool toggle = InputReader.Down(KeyCode.V) || GamepadBridge.ButtonDown(PadButton.North);
            if (!showroom)
            {
                if (toggle) SetShowroom(true);
                return;
            }
            if (toggle || InputReader.MenuBack) { SetShowroom(false); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < Views.Length; i++) if (InputReader.Down(KeyCode.Alpha1 + i)) ApplyView(i);
            if (InputReader.Down(KeyCode.Tab) || GamepadBridge.ButtonDown(PadButton.DpadRight) || GamepadBridge.ButtonDown(PadButton.RightShoulder)) ApplyView(preset + 1);
            if (GamepadBridge.ButtonDown(PadButton.DpadLeft) || GamepadBridge.ButtonDown(PadButton.LeftShoulder)) ApplyView(preset - 1);
            if (InputReader.Down(KeyCode.Space) || GamepadBridge.ButtonDown(PadButton.South))
            {
                if (interior) ApplyView(1);
                autoSpin = !autoSpin;
                UI.Sound("MenuMove");
            }
            if (InputReader.Down(KeyCode.H) || GamepadBridge.ButtonDown(PadButton.Select)) hidePanel = !hidePanel;
            if (InputReader.Down(KeyCode.X) || GamepadBridge.ButtonDown(PadButton.West))
            {
                var gm = GameManager.Instance;
                var ex = gm != null && gm.Car != null ? gm.Car.GetComponent<CarExtras>() : null;
                if (ex != null) { ex.Clean(); UI.Sound("MenuSelect"); }
            }

            float h = 0f, v = 0f, z = 0f;
            if (InputReader.Key(KeyCode.LeftArrow) || InputReader.Key(KeyCode.A)) h -= 1f;
            if (InputReader.Key(KeyCode.RightArrow) || InputReader.Key(KeyCode.D)) h += 1f;
            if (InputReader.Key(KeyCode.UpArrow) || InputReader.Key(KeyCode.W)) v += 1f;
            if (InputReader.Key(KeyCode.DownArrow) || InputReader.Key(KeyCode.S)) v -= 1f;
            if (InputReader.Key(KeyCode.Q) || InputReader.Key(KeyCode.Minus) || InputReader.Key(KeyCode.KeypadMinus)) z += 1f;
            if (InputReader.Key(KeyCode.E) || InputReader.Key(KeyCode.Equals) || InputReader.Key(KeyCode.KeypadPlus)) z -= 1f;
            Vector2 ls = GamepadBridge.LeftStick, rs = GamepadBridge.RightStick;
            h += DeadZone(ls.x) + DeadZone(rs.x);
            v += DeadZone(ls.y) + DeadZone(rs.y);
            z += GamepadBridge.LeftTrigger - GamepadBridge.RightTrigger;
            h = Mathf.Clamp(h, -1f, 1f); v = Mathf.Clamp(v, -1f, 1f);
            if (interior)
            {
                iyawT = Mathf.Clamp(iyawT + h * 80f * dt, -130f, 130f);
                ipitchT = Mathf.Clamp(ipitchT - v * 60f * dt, -25f, 30f);
            }
            else
            {
                yawT -= h * 75f * dt;
                pitchT = Mathf.Clamp(pitchT + v * 45f * dt, -2f, 60f);
                distT = Mathf.Clamp(distT * Mathf.Exp(z * 1.2f * dt), 3.4f, 9f);
            }
        }

        static float DeadZone(float v) { return Mathf.Abs(v) < 0.15f ? 0f : (v - Mathf.Sign(v) * 0.15f) / 0.85f; }

        // ================================================================== animation + camera
        void StartTransition(float dur)
        {
            var cam = Camera.main;
            if (cam == null) return;
            fromPos = cam.transform.position;
            fromRot = cam.transform.rotation;
            fromFov = cam.fieldOfView;
            transStart = Time.unscaledTime;
            transDur = dur;
            transArc = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var cam = Camera.main;
            if (!Active)
            {
                // the crew keeps working when someone drives past
                if (workshop != null && cam != null && (cam.transform.position - workshop.Origin).sqrMagnitude < 60f * 60f) workshop.Tick(Mathf.Min(dt, 0.05f), false);
                wasOwning = false;
                return;
            }
            if (showroom && !OwnsCamera) { showroom = false; interior = false; if (workshop != null) workshop.StepAside(false); }
            camT += dt;
            if (showroom)
            {
                if (autoSpin) spin = Mathf.Repeat(spin + dt * 10f, 360f);
                else spin = Mathf.Repeat(Mathf.LerpAngle(spin, spinTarget, MenuArt.Damp(dt, 2.6f)), 360f);
            }
            else spin = Mathf.Repeat(spin + dt * 14f, 360f);
            SetLights(1f);
            if (table != null) table.rotation = Quaternion.Euler(0f, spin, 0f);
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null) Place(gm.Car);
            if (workshop != null && (OwnsCamera || (cam != null && (cam.transform.position - workshop.Origin).sqrMagnitude < 60f * 60f)))
                workshop.Tick(Mathf.Min(dt, 0.05f), false);
            if (cam == null || !OwnsCamera) { wasOwning = false; return; }

            if (!wasOwning)
            {
                // fly in from wherever the camera was (title flyover, driving)
                StartTransition(1.7f);
                transArc = Mathf.Clamp((fromPos - TablePos).magnitude * 0.12f, 0f, 30f);
                wasOwning = true;
            }

            Vector3 pos;
            Quaternion rot;
            float fov;
            if (showroom) ShowroomPose(gm, dt, out pos, out rot, out fov);
            else GaragePose(out pos, out rot, out fov);

            float k = (Time.unscaledTime - transStart) / Mathf.Max(0.01f, transDur);
            if (k < 1f)
            {
                float e = MenuArt.EaseInOut(k);
                Vector3 bp = Vector3.Lerp(fromPos, pos, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * transArc;
                cam.transform.SetPositionAndRotation(bp, Quaternion.Slerp(fromRot, rot, e));
                cam.fieldOfView = Mathf.Lerp(fromFov, fov, e);
            }
            else
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 3f));
            }
        }

        void GaragePose(out Vector3 pos, out Quaternion rot, out float fov)
        {
            // slow cinematic drift in front of the workshop, framing the car slightly left of centre;
            // a car swap kicks the camera back and round, then it settles again
            float st = Time.unscaledTime - swapT;
            float kick = st >= 0f && st < 1.3f ? Mathf.Sin(Mathf.Clamp01(st / 1.3f) * Mathf.PI) * (1f - st / 1.3f * 0.3f) : 0f;
            float ang = 16f + Mathf.Sin(camT * 0.12f) * 22f + kick * 16f;
            float d = 7.4f + Mathf.Sin(camT * 0.21f) * 0.5f + kick * 1.4f;
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            pos = TablePos + dir * d + Vector3.up * (1.45f + Mathf.Sin(camT * 0.17f) * 0.22f + kick * 0.5f);
            pos = ClampToWorkshop(pos);
            Vector3 look = TablePos + Vector3.up * 0.6f + Quaternion.Euler(0f, ang, 0f) * Vector3.left * 0.9f;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
            fov = 40f;
        }

        void ShowroomPose(GameManager gm, float dt, out Vector3 pos, out Quaternion rot, out float fov)
        {
            float k = MenuArt.Damp(dt, 5f);
            if (interior && gm != null && gm.Car != null)
            {
                iyaw = Mathf.Lerp(iyaw, iyawT, k);
                ipitch = Mathf.Lerp(ipitch, ipitchT, k);
                Transform ct = gm.Car.transform;
                pos = ct.TransformPoint(new Vector3(-0.36f, 1.06f, 0.05f));
                rot = ct.rotation * Quaternion.Euler(ipitch, iyaw, 0f);
                fov = 70f;
                return;
            }
            yaw = Mathf.LerpAngle(yaw, yawT, k);
            pitch = Mathf.Lerp(pitch, pitchT, k);
            dist = Mathf.Lerp(dist, distT, k);
            Vector3 look = TablePos + Vector3.up * 0.55f;
            float pr = pitch * Mathf.Deg2Rad;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Mathf.Sin(pr), Mathf.Cos(pr));
            pos = ClampToWorkshop(look + dir * dist);
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
            fov = 44f;
        }

        Vector3 ClampToWorkshop(Vector3 p)
        {
            Vector3 o = workshop != null ? workshop.Origin : TablePos - Vector3.up * 0.16f;
            Vector3 l = p - o;
            l.x = Mathf.Clamp(l.x, GarageWorkshop.CamMinX, GarageWorkshop.CamMaxX);
            l.z = Mathf.Clamp(l.z, GarageWorkshop.CamMinZ, workshop != null ? workshop.CamMaxZ : 8.2f);
            l.y = Mathf.Clamp(l.y, GarageWorkshop.CamMinY, GarageWorkshop.CamMaxY);
            return o + l;
        }

        // ================================================================== UI
        void OnGUI()
        {
            if (!Active || !OwnsCamera) return;
            if ((SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) || PhotoAlbum.IsOpen || ValleyMap.IsOpen) return;
            MenuArt.Styles();
            float W = Screen.width, H = Screen.height, s = MenuArt.S;
            bool pad = InputReader.PadConnected;
            if (!showroom)
            {
                bool c;
                MenuArt.Chip(24f * s, H - 52f * s, pad ? "Y" : "V", "showroom walk-around", true, out c);
                if (c) SetShowroom(true);
                return;
            }
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null || gm.Car.spec == null) return;
            var e = Event.current;
            // mouse orbit / look + wheel zoom
            if (e.type == EventType.MouseDrag)
            {
                if (interior)
                {
                    iyawT = Mathf.Clamp(iyawT + e.delta.x * 0.25f, -130f, 130f);
                    ipitchT = Mathf.Clamp(ipitchT + e.delta.y * 0.2f, -25f, 30f);
                }
                else
                {
                    yawT -= e.delta.x * 0.3f;
                    pitchT = Mathf.Clamp(pitchT + e.delta.y * 0.2f, -2f, 60f);
                }
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && !interior)
            {
                distT = Mathf.Clamp(distT * Mathf.Exp(e.delta.y * 0.05f), 3.4f, 9f);
                e.Use();
            }

            float a = MenuArt.EaseOut((Time.unscaledTime - showroomTime) / 0.5f);
            var spec = gm.Car.spec;
            MenuArt.Text(new Rect(0, 18f * s, W, 44f * s), "SHOWROOM", MenuArt.Title, MenuArt.A(UI.Pink, a));
            MenuArt.Text(new Rect(0, 60f * s, W, 26f * s), spec.name + "  ·  " + Views[preset].name + " view" + (autoSpin ? "  ·  turntable" : ""), MenuArt.SmallC, MenuArt.A(UI.White, 0.85f * a));
            if (!hidePanel) SpecCard(new Rect(28f * s - (1f - a) * 60f * s, H * 0.17f, 430f * s, 560f * s), spec, s, a);

            // view chips (clickable)
            float cw = 120f * s, ch = 40f * s, gap = 10f * s;
            float total = Views.Length * cw + (Views.Length - 1) * gap;
            float x = (W - total) * 0.5f, y = H - 128f * s;
            for (int i = 0; i < Views.Length; i++)
            {
                Rect r = new Rect(x + i * (cw + gap), y, cw, ch);
                bool sel = i == preset && !autoSpin;
                MenuArt.Fill(r, sel ? MenuArt.A(UI.Pink, 0.92f * a) : new Color(MenuArt.Plum.r, MenuArt.Plum.g, MenuArt.Plum.b, 0.75f * a));
                MenuArt.Text(r, (i + 1) + "  " + Views[i].name, MenuArt.BodyC, sel ? MenuArt.A(MenuArt.Plum, a) : MenuArt.A(UI.White, a), !sel);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) ApplyView(i);
            }
            // controls
            float fx = (W - 1100f * s) * 0.5f, fy = H - 66f * s;
            bool c1, c2, c3;
            fx += MenuArt.Chip(fx, fy, pad ? "STICKS" : "DRAG / ARROWS", interior ? "look around" : "orbit");
            fx += MenuArt.Chip(fx, fy, pad ? "LT RT" : "WHEEL / Q E", "zoom");
            fx += MenuArt.Chip(fx, fy, pad ? "LB RB" : "1-5 / TAB", "views");
            fx += MenuArt.Chip(fx, fy, pad ? "A" : "SPACE", autoSpin ? "stop turntable" : "spin turntable", true, out c1);
            fx += MenuArt.Chip(fx, fy, pad ? "SELECT" : "H", hidePanel ? "show specs" : "hide specs", true, out c2);
            MenuArt.Chip(fx, fy, pad ? "B / Y" : "ESC / V", "back to garage", true, out c3);
            if (c1) { if (interior) ApplyView(1); autoSpin = !autoSpin; UI.Sound("MenuMove"); }
            if (c2) hidePanel = !hidePanel;
            if (c3) SetShowroom(false);
        }

        void SpecCard(Rect r, CarSpec spec, float s, float a)
        {
            MenuArt.Card(r, a);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float x = r.x + 24f * s, w = r.width - 48f * s, y = r.y + 16f * s;
            MenuArt.Text(new Rect(x, y, w, 40f * s), spec.name, MenuArt.Big, MenuArt.A(UI.White, a));
            y += 40f * s;
            MenuArt.Text(new Rect(x, y, w, 24f * s), spec.carClass + "  ·  " + spec.era, MenuArt.Small, MenuArt.A(UI.Pink, a));
            y += 34f * s;
            string[] k =
            {
                "Drivetrain", "Weight", "Top speed", "Drive force", "Power to weight", "Tyre grip", "Steering lock"
            };
            string[] v =
            {
                spec.drive.ToString(), Mathf.RoundToInt(spec.mass) + " kg", Mathf.RoundToInt(spec.maxSpeedKmh) + " km/h",
                (spec.power / 1000f).ToString("0.0", inv) + " kN", (spec.power / spec.mass).ToString("0.00", inv) + " N/kg",
                "μ " + spec.grip.ToString("0.00", inv), Mathf.RoundToInt(spec.maxSteer) + "°"
            };
            for (int i = 0; i < k.Length; i++)
            {
                if (i % 2 == 0) MenuArt.Fill(new Rect(x - 8f * s, y, w + 16f * s, 28f * s), new Color(1f, 1f, 1f, 0.05f * a));
                MenuArt.Text(new Rect(x, y, w * 0.55f, 28f * s), k[i], MenuArt.Small, MenuArt.A(UI.White, 0.75f * a));
                MenuArt.Text(new Rect(x + w * 0.45f, y, w * 0.55f, 28f * s), v[i], MenuArt.BodyR, MenuArt.A(UI.Gold, a));
                y += 28f * s;
            }
            y += 14f * s;
            string[] names = { "Power", "Top speed", "Grip", "Drift", "Agility" };
            float[] vals =
            {
                Mathf.InverseLerp(4.5f, 7.6f, spec.power / spec.mass),
                Mathf.InverseLerp(160f, 215f, spec.maxSpeedKmh),
                Mathf.InverseLerp(0.95f, 1.15f, spec.grip),
                Mathf.InverseLerp(0.75f, 0.52f, spec.driftGrip),
                Mathf.InverseLerp(1250f, 820f, spec.mass)
            };
            for (int i = 0; i < names.Length; i++)
            {
                MenuArt.Text(new Rect(x, y, 120f * s, 24f * s), names[i], MenuArt.Small, MenuArt.A(UI.White, a));
                Rect bar = new Rect(x + 120f * s, y + 8f * s, w - 120f * s, 9f * s);
                MenuArt.Fill(bar, new Color(0f, 0f, 0f, 0.35f * a));
                MenuArt.Fill(new Rect(bar.x, bar.y, bar.width * (0.12f + vals[i] * 0.88f) * MenuArt.EaseOut(a), bar.height), MenuArt.A(UI.Pink, a));
                y += 28f * s;
            }
            y += 12f * s;
            MenuArt.Fill(new Rect(x, y, w, 1f), new Color(1f, 1f, 1f, 0.2f * a));
            y += 8f * s;
            MenuArt.Text(new Rect(x, y, w, 26f * s), "Livery", MenuArt.Small, MenuArt.A(UI.White, 0.75f * a));
            MenuArt.Text(new Rect(x, y, w, 26f * s), ColourName + "  ·  " + PatternName, MenuArt.BodyR, MenuArt.A(UI.Gold, a));
            y += 30f * s;
            MenuArt.Text(new Rect(x, y, w, 24f * s), (InputReader.PadConnected ? "X" : "X") + "  wash and polish", MenuArt.Tiny, MenuArt.A(UI.White, 0.6f * a));
        }
    }
}

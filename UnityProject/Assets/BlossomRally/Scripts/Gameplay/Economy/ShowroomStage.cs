// ShowroomStage.cs - the showroom's turntable preview: a small studio (soft floor, lacquered turntable, key +
// rim lights) far below the world on its own layer, filmed by a dedicated camera into a RenderTexture that
// the Showroom draws in its IMGUI panel. The model is the car's visual prefab (same pipeline as CarFactory:
// AxisFix + MaterialLibrary.Remap + saved livery); vehicles without a prefab (e.g. bikes built in code) fall
// back to CarFactory.Create with every behaviour, collider, light and sound switched off.
// The camera and lights only run while the showroom is open.
using UnityEngine;

namespace BlossomRally
{
    public sealed class ShowroomStage
    {
        public const int Layer = 31;
        static readonly Vector3 Origin = new Vector3(0f, -3000f, 0f);
        static readonly int IdW2L = Shader.PropertyToID("_CarW2L");
        static readonly int IdExt = Shader.PropertyToID("_CarExt");

        GameObject root, model, turntable;
        Camera cam;
        Light key, rim;
        RenderTexture rt;
        bool active;
        float yaw = 30f, idleT, camDist = 7f, camHeight = 1.2f, lookHeight = 0.6f;
        string modelId;
        Material paint;
        Vector3 paintExt;
        // paint-frame swap around the preview render (the paint material is shared with the player's car)
        Matrix4x4 savedW2L;
        Vector4 savedExt;
        bool savedFog, swapped;

        public bool Missing { get; private set; }

        void Ensure()
        {
            if (root != null) return;
            root = new GameObject("BR_ShowroomStage");
            Object.DontDestroyOnLoad(root);
            root.transform.position = Origin;
            BuildStudio(root.transform);
            var cgo = new GameObject("ShowroomCam");
            cgo.transform.SetParent(root.transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.09f, 0.14f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 120f;
            cam.depth = -20f;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;
            key = MakeLight("Key", new Vector3(38f, -35f, 0f), new Color(1f, 0.95f, 0.9f), 1.1f);
            rim = MakeLight("Rim", new Vector3(20f, 160f, 0f), new Color(1f, 0.72f, 0.84f), 0.7f);
            Camera.onPreRender += PreRender;
            Camera.onPostRender += PostRender;
            root.SetActive(false);
        }

        Light MakeLight(string name, Vector3 euler, Color c, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        void BuildStudio(Transform parent)
        {
            // studio floor fading into the background colour, then the turntable on top
            var floor = new LifeGeo();
            const int seg = 40;
            Color32 inner = new Color32(120, 92, 104, 0), outer = new Color32(44, 23, 36, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                floor.Tri(Vector3.zero, d1 * 9f, d0 * 9f, inner, inner, inner);
                floor.Tri(d0 * 9f, d1 * 9f, d1 * 40f, inner, inner, outer);
                floor.Tri(d0 * 9f, d1 * 40f, d0 * 40f, inner, outer, outer);
            }
            AddMesh(parent, floor.ToMesh("ShowroomFloor"), Vector3.zero);

            var disc = new LifeGeo();
            Color32 top = new Color32(214, 196, 170, 0), inlay = new Color32(196, 150, 70, 0), rimC = new Color32(170, 34, 40, 0);
            const float r = 3.3f, h = 0.16f;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i / 48f * Mathf.PI * 2f, a1 = (i + 1) / 48f * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 up = Vector3.up * h;
                disc.Tri(up, d1 * r * 0.78f + up, d0 * r * 0.78f + up, top, top, top);
                Ring(disc, d0, d1, r * 0.78f, r * 0.86f, h, inlay);
                Ring(disc, d0, d1, r * 0.86f, r, h, rimC);
                disc.QuadOut(d0 * r, d1 * r, d1 * r + up, d0 * r + up, (d0 + d1) * 0.5f, rimC, rimC, rimC, rimC);
            }
            turntable = AddMesh(parent, disc.ToMesh("ShowroomTurntable"), Vector3.up * 0.01f);
        }

        static void Ring(LifeGeo g, Vector3 d0, Vector3 d1, float r0, float r1, float h, Color32 c)
        {
            Vector3 up = Vector3.up * h;
            g.TriOut(d0 * r0 + up, d1 * r0 + up, d1 * r1 + up, Vector3.up, c, c, c);
            g.TriOut(d0 * r0 + up, d1 * r1 + up, d0 * r1 + up, Vector3.up, c, c, c);
        }

        static GameObject AddMesh(Transform parent, Mesh m, Vector3 local)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.layer = Layer;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Terrain;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ public
        public void SetActive(bool on)
        {
            if (on) Ensure();
            if (root == null) return;
            active = on;
            root.SetActive(on);
            cam.enabled = on;
            if (!on) ClearModel();
        }

        /// <summary>Put a vehicle on the turntable (null clears it).</summary>
        public void Show(CarSpec spec)
        {
            Ensure();
            if (spec != null && spec.id == modelId && model != null) return;
            ClearModel();
            Missing = false;
            if (spec == null) return;
            modelId = spec.id;
            try { model = BuildModel(spec); }
            catch (System.Exception e) { Debug.LogException(e); model = null; }
            if (model == null) { Missing = true; return; }
            Frame();
            idleT = 0f;
        }

        public void Nudge(float deg) { yaw += deg; idleT = 0f; }

        public void Tick(float dt)
        {
            if (!active) return;
            idleT += dt;
            if (idleT > 1.5f) yaw += dt * 12f * Mathf.Clamp01((idleT - 1.5f) * 0.7f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (model != null) model.transform.rotation = q;
            if (turntable != null) turntable.transform.rotation = q;
            // gentle camera drift
            float t = Time.unscaledTime;
            Vector3 look = Origin + Vector3.up * lookHeight;
            Vector3 dir = Quaternion.Euler(0f, 205f + Mathf.Sin(t * 0.15f) * 6f, 0f) * Vector3.forward;
            cam.transform.position = look + dir * camDist + Vector3.up * (camHeight + Mathf.Sin(t * 0.21f) * 0.12f);
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position, Vector3.up);
            // lights: fill in for a dim sun (night) without blowing out at noon
            float sun = Atmosphere.Sun != null && Atmosphere.Sun.enabled ? Atmosphere.Sun.intensity : 0f;
            key.intensity = Mathf.Clamp(1.5f - sun * 0.8f, 0.45f, 1.4f);
            rim.intensity = 0.55f + 0.35f * Atmosphere.NightFactor;
        }

        /// <summary>Render target sized for the panel (re-created when the size changes noticeably).</summary>
        public Texture Render(int w, int h)
        {
            if (!active || cam == null) return null;
            float scale = GameSettings.Quality == 0 ? 0.6f : GameSettings.Quality == 1 ? 0.8f : 1f;
            int tw = Mathf.Clamp(Mathf.RoundToInt(w * scale), 64, 2048), th = Mathf.Clamp(Mathf.RoundToInt(h * scale), 64, 2048);
            if (rt == null || Mathf.Abs(rt.width - tw) > 8 || Mathf.Abs(rt.height - th) > 8)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(tw, th, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "BR_ShowroomRT", antiAliasing = GameSettings.Quality >= 2 ? 4 : 2, hideFlags = HideFlags.DontSave };
                cam.targetTexture = rt;
                cam.aspect = tw / (float)th;
            }
            return rt;
        }

        public void Dispose()
        {
            Camera.onPreRender -= PreRender;
            Camera.onPostRender -= PostRender;
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
            if (root != null) Object.Destroy(root);
            root = null;
        }

        // ------------------------------------------------------------------ model
        GameObject BuildModel(CarSpec spec)
        {
            var go = new GameObject("ShowroomModel_" + spec.id);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            var prefab = Resources.Load<GameObject>("Cars/" + spec.id + "/" + spec.id + "_Export");
            if (prefab != null)
            {
                var m = Object.Instantiate(prefab, go.transform, false);
                AxisFix.Calibrate();
                AxisFix.Apply(m.transform);
                foreach (var an in m.GetComponentsInChildren<Animator>(true)) Object.Destroy(an);
                MaterialLibrary.Remap(m, spec.id);
                LiveryLibrary.ApplySaved(spec.id);
            }
            else
            {
                // bikes and other code-built vehicles: a real instance, switched off
                var car = CarFactory.Create(spec, Origin, Quaternion.identity);
                if (car == null) { Object.Destroy(go); return null; }
                car.controlsEnabled = false;
                if (car.Body != null) { car.Body.isKinematic = true; car.Body.detectCollisions = false; }
                foreach (var b in car.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                foreach (var a in car.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; }
                foreach (var l in car.GetComponentsInChildren<Light>(true)) l.enabled = false;
                car.transform.SetParent(go.transform, false);
                car.transform.localPosition = Vector3.zero;
                car.transform.localRotation = Quaternion.identity;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            paint = MaterialLibrary.TryGet("Paint_" + spec.id);
            return go;
        }

        void Frame()
        {
            model.transform.rotation = Quaternion.identity;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            // stand it on the turntable; the car's own pivot (centred between the wheels) stays on the spin axis,
            // so the paint shader's car frame matches the in-game one
            Vector3 off = new Vector3(0f, Origin.y + 0.17f - b.min.y, 0f);
            model.transform.position += off;
            b.center += off;
            paintExt = new Vector3(b.extents.x, b.max.y - model.transform.position.y, b.extents.z);
            float size = Mathf.Max(b.extents.x, b.extents.z, b.extents.y * 1.3f);
            camDist = Mathf.Clamp(size * 3.2f + 1.2f, 3.2f, 14f);
            lookHeight = Mathf.Clamp(b.extents.y * 0.9f, 0.4f, 1.6f);
            camHeight = Mathf.Clamp(size * 0.45f, 0.6f, 2.4f);
        }

        void ClearModel()
        {
            if (model != null) Object.Destroy(model);
            model = null;
            modelId = null;
            paint = null;
        }

        // ------------------------------------------------------------------ render callbacks
        void PreRender(Camera c)
        {
            if (c != cam || cam == null) return;
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
            swapped = false;
            if (paint != null && model != null)
            {
                savedW2L = paint.GetMatrix(IdW2L);
                savedExt = paint.GetVector(IdExt);
                paint.SetMatrix(IdW2L, model.transform.worldToLocalMatrix);
                paint.SetVector(IdExt, paintExt);
                swapped = true;
            }
        }

        void PostRender(Camera c)
        {
            if (c != cam || cam == null) return;
            RenderSettings.fog = savedFog;
            if (swapped && paint != null)
            {
                paint.SetMatrix(IdW2L, savedW2L);
                paint.SetVector(IdExt, savedExt);
            }
            swapped = false;
        }
    }
}

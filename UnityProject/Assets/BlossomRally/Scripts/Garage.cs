// Garage.cs - the car-select scene: a lacquered turntable in front of the village garage, warm
// lantern lights, a slowly drifting camera, stat bars and the livery picker (colour + pattern).
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(1000)]     // runs after the camera rig so it owns the camera while active
    public class Garage : MonoBehaviour
    {
        /// <summary>Turntable centre (x, z) - kept flat and free of plants by WorldBuilder.</summary>
        public static Vector2 Spot { get { return MapData.Garage + new Vector2(2f, -15f); } }
        public bool Active { get; private set; }
        /// <summary>When false (title screen) the car still turns on the table but the camera is free.</summary>
        public bool OwnsCamera = true;
        public Vector3 TablePos { get; private set; }
        Transform table;
        Light[] lights;
        float spin, camT;
        int colour, pattern;
        string carId;

        public void Build(WorldBuilder w)
        {
            // open ground beside the garage, ~15 m clear of Lantern Street and the houses
            Vector3 p = new Vector3(Spot.x, 0f, Spot.y);
            p.y = w.HeightAt(p.x, p.z) + 0.02f;
            TablePos = p + Vector3.up * 0.16f;
            var root = new GameObject("Garage_Turntable");
            root.transform.position = p;
            table = root.transform;
            BuildDisc(root.transform);
            var stage = new GameObject("Lights");
            stage.transform.SetParent(root.transform, false);
            lights = new Light[3];
            Vector3[] lp = { new Vector3(-4f, 4.5f, -3f), new Vector3(4.5f, 3.5f, -2f), new Vector3(0f, 5.5f, 4.5f) };
            Color[] lc = { new Color(1f, 0.82f, 0.62f), new Color(1f, 0.70f, 0.78f), new Color(0.75f, 0.82f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                var lg = new GameObject("GarageLight" + i);
                lg.transform.SetParent(stage.transform, false);
                lg.transform.localPosition = lp[i];
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 13f;
                l.color = lc[i];
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                lights[i] = l;
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
            if (lights == null) return;
            float night = Mathf.Lerp(0.6f, 1.6f, Atmosphere.NightFactor);
            for (int i = 0; i < lights.Length; i++) { lights[i].intensity = k * night * (i == 2 ? 0.8f : 1.2f); lights[i].enabled = k > 0.01f; }
        }

        /// <summary>Show a car on the turntable (kinematic, no controls).</summary>
        public void Show(ArcadeCar car)
        {
            Active = true;
            if (car == null) return;
            carId = car.spec.id;
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
            SetLights(0f);
            if (car == null) return;
            if (car.Body != null) car.Body.isKinematic = false;
            var ex = car.GetComponent<CarExtras>();
            if (ex != null) ex.ShowroomMode = false;
        }

        void Place(ArcadeCar car)
        {
            Quaternion r = Quaternion.Euler(0f, spin, 0f);
            car.transform.SetPositionAndRotation(TablePos, r);
            if (car.Body != null) { car.Body.position = TablePos; car.Body.rotation = r; }
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

        void LateUpdate()
        {
            if (!Active) return;
            float dt = Time.unscaledDeltaTime;
            spin = Mathf.Repeat(spin + dt * 14f, 360f);
            camT += dt;
            SetLights(1f);
            if (table != null) table.rotation = Quaternion.Euler(0f, spin, 0f);
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null) Place(gm.Car);
            var cam = Camera.main;
            if (cam == null || !OwnsCamera) return;
            // slow cinematic drift around the table, framing the car slightly left of centre
            float ang = 205f + Mathf.Sin(camT * 0.12f) * 28f;
            float dist = 7.6f + Mathf.Sin(camT * 0.21f) * 0.6f;
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            Vector3 pos = TablePos + dir * dist + Vector3.up * (1.5f + Mathf.Sin(camT * 0.17f) * 0.25f);
            Vector3 look = TablePos + Vector3.up * 0.6f + Quaternion.Euler(0f, ang, 0f) * Vector3.right * 0.9f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 40f, dt * 3f);
        }
    }
}

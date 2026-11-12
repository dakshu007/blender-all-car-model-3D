// CarAnatomy.cs - measures one spawned car model once (in car-root space, +Z forward, +Y up) so the
// detail systems can place things on any of the nine cars: seats / hip points, steering wheel frame,
// windscreen frame, headlight + aux lamp centres, number plates, bumpers, exhaust, roof surface.
// Uses the object names written by the Blender builders (<ID>_Body, _Glass, _Lights, _Interior,
// _SteeringWheel, _MudFlaps, _Bumper_Front/_Rear, _Mirrors, _Spoiler, _Trim) and the remapped material
// names (BR_Paint_*, *_Seat, *Light_Head, *Light_Aux, *_Plate); everything has a bounds-based fallback.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarAnatomy
    {
        public ArcadeCar car;
        public Transform root;
        public string id;
        public bool groupB, rearEngine, electric;
        /// <summary>Half width, top height, half length (same convention as CarExtras / the paint shader).</summary>
        public Vector3 ext = new Vector3(0.9f, 1.35f, 2.1f);
        public Bounds bodyBounds = new Bounds(new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.3f, 4.1f));

        public MeshFilter body, glass, lights, interior, mudFlaps, bumperF, bumperR, mirrors, spoiler, trim;
        public Transform steering;

        // steering wheel: centre, column axis (unit, pointing back at the driver), up / right in the wheel plane
        public Vector3 wheelC, wheelAxis, wheelUp, wheelRight;
        public float wheelR = 0.165f;

        // seats: hip points + head room (inner roof height above the hip)
        public Vector3 hipL, hipR;
        public float roofL = 1.25f, roofR = 1.25f;
        public float seatTopY = 0.4f;

        // windscreen: bottom-centre, up the glass (unit), outward normal (unit), slant height, half width
        public bool hasScreen;
        public Vector3 scrO, scrUp, scrN;
        public float scrH = 0.55f, scrHW = 0.65f, scrTopHW = 0.55f;

        // lamps (index 0 = left / -X, 1 = right / +X)
        public readonly bool[] hasHead = new bool[2], hasAux = new bool[2];
        public readonly Vector3[] head = new Vector3[2], aux = new Vector3[2];
        public readonly float[] headR = new float[2], auxR = new float[2];

        // number plates (index 0 front, 1 rear): centre, size (w, h)
        public readonly bool[] hasPlate = new bool[2];
        public readonly Vector3[] plate = new Vector3[2];
        public readonly Vector2[] plateSize = new Vector2[2];

        public Bounds bumperFB, bumperRB;
        public Vector3 exhaust;
        public float frontAxleZ = 1.2f, rearAxleZ = -1.2f;

        // body surface (car space) for decals and roof queries
        public Vector3[] bodyV, bodyN;
        public int[] bodyPaintTris;
        public float roofY = 1.3f, roofMinZ = -0.6f, roofMaxZ = 0.3f, roofHalfW = 0.6f;

        public Matrix4x4 ToCar(Transform t) { return root.worldToLocalMatrix * t.localToWorldMatrix; }

        public static CarAnatomy Build(ArcadeCar car)
        {
            var a = new CarAnatomy { car = car, root = car.transform };
            a.id = car.spec != null ? car.spec.id : car.name;
            // sound family: the new classes map onto Group B / Group 4 / Classic RWD / Electric (CarCatalog.SoundFor)
            string cls = car.spec != null ? car.spec.SoundClass : "";
            a.groupB = cls == "Group B";
            a.electric = cls == "Electric";
            a.rearEngine = a.id == "CAR-008" || a.id == "CAR-019" || a.id == "CAR-025";
            var box = car.GetComponent<BoxCollider>();
            if (box != null)
                a.ext = new Vector3(box.size.x / 0.92f * 0.5f, box.center.y + box.size.y * 0.5f, box.size.z / 0.95f * 0.5f);
            foreach (var w in car.wheels)
            {
                if (w.front) a.frontAxleZ = w.local.z; else a.rearAxleZ = w.local.z;
            }

            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.name;
                if (n.EndsWith("_Body")) a.body = mf;
                else if (n.EndsWith("_Glass")) a.glass = mf;
                else if (n.EndsWith("_Lights")) a.lights = mf;
                else if (n.EndsWith("_Interior")) a.interior = mf;
                else if (n.EndsWith("_MudFlaps")) a.mudFlaps = mf;
                else if (n.EndsWith("_Bumper_Front")) a.bumperF = mf;
                else if (n.EndsWith("_Bumper_Rear")) a.bumperR = mf;
                else if (n.EndsWith("_Mirrors")) a.mirrors = mf;
                else if (n.EndsWith("_Spoiler")) a.spoiler = mf;
                else if (n.EndsWith("_Trim")) a.trim = mf;
                else if (n.EndsWith("_SteeringWheel")) a.steering = mf.transform;
            }
            a.MeasureBody();
            a.MeasureSteering();
            a.MeasureSeats();
            a.MeasureScreen();
            a.MeasureLamps();
            a.MeasurePlates();
            a.MeasureBumpers();
            return a;
        }

        static bool Readable(MeshFilter mf) { return mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable; }

        public static int FindSubmesh(Renderer r, string contains)
        {
            if (r == null) return -1;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.Contains(contains)) return i;
            return -1;
        }

        /// <summary>Car-space vertices of the given submesh's triangles, split by the sign of a coordinate.</summary>
        void SubmeshBounds(MeshFilter mf, string mat, int axis, out Bounds neg, out bool hasNeg, out Bounds pos, out bool hasPos)
        {
            neg = pos = new Bounds();
            hasNeg = hasPos = false;
            if (!Readable(mf)) return;
            int sub = FindSubmesh(mf.GetComponent<Renderer>(), mat);
            var mesh = mf.sharedMesh;
            if (sub < 0 || sub >= mesh.subMeshCount) return;
            var m = ToCar(mf.transform);
            var v = mesh.vertices;
            var t = mesh.GetTriangles(sub);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 p0 = m.MultiplyPoint3x4(v[t[i]]), p1 = m.MultiplyPoint3x4(v[t[i + 1]]), p2 = m.MultiplyPoint3x4(v[t[i + 2]]);
                float c = (p0[axis] + p1[axis] + p2[axis]) / 3f;
                if (c < 0f)
                {
                    if (!hasNeg) { neg = new Bounds(p0, Vector3.zero); hasNeg = true; }
                    neg.Encapsulate(p0); neg.Encapsulate(p1); neg.Encapsulate(p2);
                }
                else
                {
                    if (!hasPos) { pos = new Bounds(p0, Vector3.zero); hasPos = true; }
                    pos.Encapsulate(p0); pos.Encapsulate(p1); pos.Encapsulate(p2);
                }
            }
        }

        void MeasureBody()
        {
            if (!Readable(body))
            {
                bodyBounds = new Bounds(new Vector3(0f, ext.y * 0.5f, 0f), new Vector3(ext.x * 2f, ext.y, ext.z * 2f));
                roofY = ext.y;
                return;
            }
            var mesh = body.sharedMesh;
            var m = ToCar(body.transform);
            var v = mesh.vertices;
            var nn = mesh.normals;
            bodyV = new Vector3[v.Length];
            bodyN = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                bodyV[i] = m.MultiplyPoint3x4(v[i]);
                bodyN[i] = nn != null && nn.Length == v.Length ? m.MultiplyVector(nn[i]).normalized : Vector3.up;
            }
            var b = new Bounds(bodyV[0], Vector3.zero);
            for (int i = 1; i < bodyV.Length; i++) b.Encapsulate(bodyV[i]);
            bodyBounds = b;
            int sub = FindSubmesh(body.GetComponent<Renderer>(), "Paint");
            bodyPaintTris = mesh.GetTriangles(Mathf.Max(0, Mathf.Min(sub, mesh.subMeshCount - 1)));
            // roof: the top skin of the body
            roofY = b.max.y;
            float minZ = 9f, maxZ = -9f, hw = 0f;
            for (int i = 0; i < bodyV.Length; i++)
            {
                if (bodyV[i].y < b.max.y - 0.045f) continue;
                minZ = Mathf.Min(minZ, bodyV[i].z);
                maxZ = Mathf.Max(maxZ, bodyV[i].z);
                hw = Mathf.Max(hw, Mathf.Abs(bodyV[i].x));
            }
            if (maxZ > minZ) { roofMinZ = minZ; roofMaxZ = maxZ; roofHalfW = hw; }
        }

        /// <summary>Top surface height of the body around (x, z) (falls back to the roof height).</summary>
        public float SurfaceY(float x, float z, float r)
        {
            if (bodyV == null) return roofY;
            float best = -1f, r2 = r * r;
            for (int i = 0; i < bodyV.Length; i++)
            {
                float dx = bodyV[i].x - x, dz = bodyV[i].z - z;
                if (dx * dx + dz * dz < r2 && bodyV[i].y > best) best = bodyV[i].y;
            }
            return best > 0f ? best : roofY;
        }

        /// <summary>Outer side surface |x| of the body around (y, z).</summary>
        public float SurfaceX(float y, float z, float r)
        {
            if (bodyV == null) return ext.x;
            float best = -1f, r2 = r * r;
            for (int i = 0; i < bodyV.Length; i++)
            {
                float dy = bodyV[i].y - y, dz = bodyV[i].z - z;
                if (dy * dy + dz * dz < r2) best = Mathf.Max(best, Mathf.Abs(bodyV[i].x));
            }
            return best > 0f ? best : ext.x;
        }

        void MeasureSteering()
        {
            if (steering != null)
            {
                var mf = steering.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds lb = mf.sharedMesh.bounds;
                    var m = ToCar(steering);
                    Vector3 s = lb.size;
                    // thinnest local extent = column axis
                    Vector3 axisL = s.x <= s.y && s.x <= s.z ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
                    float ring = Mathf.Max(Vector3.Scale(s, Vector3.one - axisL).x, Mathf.Max(Vector3.Scale(s, Vector3.one - axisL).y, Vector3.Scale(s, Vector3.one - axisL).z));
                    wheelAxis = m.MultiplyVector(axisL).normalized;
                    if (wheelAxis.z > 0f) wheelAxis = -wheelAxis;          // point back at the driver
                    wheelC = m.MultiplyPoint3x4(lb.center);
                    wheelR = Mathf.Clamp(ring * 0.5f - 0.016f, 0.12f, 0.22f);
                    BuildWheelFrame();
                    return;
                }
            }
            // fallback: left-hand drive wheel in front of the cockpit camera seat
            wheelC = new Vector3(-0.36f, ext.y * 0.6f, 0.35f);
            wheelAxis = Quaternion.AngleAxis(-22f, Vector3.right) * Vector3.back;
            BuildWheelFrame();
        }

        void BuildWheelFrame()
        {
            wheelUp = Vector3.ProjectOnPlane(Vector3.up, wheelAxis).normalized;
            wheelRight = Vector3.Cross(wheelAxis, wheelUp).normalized;
        }

        /// <summary>Point on the steering wheel rim; phi = 0 at the top, positive = clockwise seen by the driver.</summary>
        public Vector3 Rim(float phiDeg, float radiusScale = 1f)
        {
            float r = phiDeg * Mathf.Deg2Rad;
            return wheelC + (wheelUp * Mathf.Cos(r) + wheelRight * Mathf.Sin(r)) * wheelR * radiusScale;
        }

        void MeasureSeats()
        {
            Bounds l, r;
            bool hl, hr;
            SubmeshBounds(interior, "Seat", 0, out l, out hl, out r, out hr);
            Vector3 fallL = new Vector3(-0.36f, 0.49f, wheelC.z - 0.62f);
            hipL = hl ? new Vector3(l.center.x, l.min.y + 0.19f, l.max.z - 0.30f) : fallL;
            hipR = hr ? new Vector3(r.center.x, r.min.y + 0.19f, r.max.z - 0.30f) : new Vector3(-fallL.x, fallL.y, fallL.z);
            seatTopY = (hl ? l.min.y : 0.3f) + 0.1f;
            roofL = SurfaceY(hipL.x, hipL.z - 0.05f, 0.16f) - 0.07f;
            roofR = SurfaceY(hipR.x, hipR.z - 0.05f, 0.16f) - 0.07f;
        }

        void MeasureScreen()
        {
            hasScreen = false;
            if (!Readable(glass)) { ScreenFallback(); return; }
            var mesh = glass.sharedMesh;
            var m = ToCar(glass.transform);
            var v = mesh.vertices;
            var t = mesh.triangles;
            var cv = new Vector3[v.Length];
            Vector3 gc = Vector3.zero;
            for (int i = 0; i < v.Length; i++) { cv[i] = m.MultiplyPoint3x4(v[i]); gc += cv[i]; }
            if (v.Length > 0) gc /= v.Length;
            var pts = new List<Vector3>();
            Vector3 nsum = Vector3.zero;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = cv[t[i]], b = cv[t[i + 1]], c = cv[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-8f) continue;
                Vector3 nu = n.normalized;
                Vector3 ctr = (a + b + c) / 3f;
                if (Mathf.Abs(nu.z) < 0.45f || ctr.z < gc.z) continue;     // front-facing glass in front of the cabin centre
                if (nu.z < 0f) n = -n;
                nsum += n;
                pts.Add(a); pts.Add(b); pts.Add(c);
            }
            if (pts.Count < 3) { ScreenFallback(); return; }
            float yMin = 9f, yMax = -9f, xMax = 0f;
            foreach (var p in pts) { yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y); xMax = Mathf.Max(xMax, Mathf.Abs(p.x)); }
            Vector3 bot = Vector3.zero, top = Vector3.zero;
            int nb = 0, nt = 0;
            float topHW = 0f;
            foreach (var p in pts)
            {
                if (p.y < yMin + 0.03f) { bot += p; nb++; }
                if (p.y > yMax - 0.03f) { top += p; nt++; topHW = Mathf.Max(topHW, Mathf.Abs(p.x)); }
            }
            bot /= Mathf.Max(1, nb);
            top /= Mathf.Max(1, nt);
            bot.x = top.x = 0f;
            scrN = nsum.normalized;
            scrO = bot;
            scrUp = (top - bot).normalized;
            scrH = (top - bot).magnitude;
            scrHW = xMax;
            scrTopHW = topHW > 0.1f ? topHW : xMax * 0.85f;
            hasScreen = scrH > 0.15f && scrHW > 0.3f;
            if (!hasScreen) ScreenFallback();
        }

        void ScreenFallback()
        {
            scrO = new Vector3(0f, ext.y * 0.66f, wheelC.z + 0.3f);
            scrUp = new Vector3(0f, 0.66f, -0.75f).normalized;
            scrN = new Vector3(0f, 0.75f, 0.66f).normalized;
            scrH = 0.55f;
            scrHW = ext.x * 0.72f;
            scrTopHW = scrHW * 0.85f;
            hasScreen = false;
        }

        /// <summary>Point on the windscreen plane: u across (car +X), v up the glass from the bottom edge.</summary>
        public Vector3 Screen(float u, float v, float lift = 0f)
        {
            return scrO + Vector3.right * u + scrUp * v + scrN * lift;
        }

        void MeasureLamps()
        {
            Bounds l, r;
            bool hl, hr;
            SubmeshBounds(lights, "Light_Head", 0, out l, out hl, out r, out hr);
            // only front-facing head lamps count
            hasHead[0] = hl && l.center.z > 0f;
            hasHead[1] = hr && r.center.z > 0f;
            if (hasHead[0]) { head[0] = l.center; headR[0] = Mathf.Max(l.extents.x, l.extents.y); }
            if (hasHead[1]) { head[1] = r.center; headR[1] = Mathf.Max(r.extents.x, r.extents.y); }
            for (int s = 0; s < 2; s++)
                if (!hasHead[s])
                {
                    head[s] = new Vector3((s == 0 ? -1f : 1f) * ext.x * 0.62f, ext.y * 0.42f, ext.z);
                    headR[s] = 0.08f;
                }
            SubmeshBounds(lights, "Light_Aux", 0, out l, out hl, out r, out hr);
            hasAux[0] = hl && l.center.z > 0f;
            hasAux[1] = hr && r.center.z > 0f;
            if (hasAux[0]) { aux[0] = l.center; auxR[0] = Mathf.Max(l.extents.x, l.extents.y); }
            if (hasAux[1]) { aux[1] = r.center; auxR[1] = Mathf.Max(r.extents.x, r.extents.y); }
        }

        void MeasurePlates()
        {
            Bounds rear, front;
            bool hrr, hf;
            SubmeshBounds(trim, "Plate", 2, out rear, out hrr, out front, out hf);
            hasPlate[0] = hf; hasPlate[1] = hrr;
            if (hf) { plate[0] = new Vector3(front.center.x, front.center.y, front.max.z); plateSize[0] = new Vector2(front.size.x, front.size.y); }
            if (hrr) { plate[1] = new Vector3(rear.center.x, rear.center.y, rear.min.z); plateSize[1] = new Vector2(rear.size.x, rear.size.y); }
        }

        static Bounds CarBounds(CarAnatomy a, MeshFilter mf, Bounds fallback)
        {
            if (mf == null || mf.sharedMesh == null) return fallback;
            var m = a.ToCar(mf.transform);
            Bounds lb = mf.sharedMesh.bounds;
            Bounds b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return b;
        }

        void MeasureBumpers()
        {
            Bounds bb = bodyBounds;
            bumperFB = CarBounds(this, bumperF, new Bounds(new Vector3(0f, 0.32f, bb.max.z - 0.06f), new Vector3(bb.size.x * 0.9f, 0.12f, 0.14f)));
            bumperRB = CarBounds(this, bumperR, new Bounds(new Vector3(0f, 0.32f, bb.min.z + 0.06f), new Vector3(bb.size.x * 0.9f, 0.12f, 0.14f)));
            float x = bumperRB.center.x + bumperRB.extents.x * 0.58f;
            exhaust = new Vector3(x, Mathf.Max(0.17f, bumperRB.min.y - 0.035f), bumperRB.min.z + 0.05f);
        }
    }
}

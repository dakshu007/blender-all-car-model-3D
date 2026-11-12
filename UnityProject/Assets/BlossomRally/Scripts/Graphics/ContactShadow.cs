// ContactShadow.cs - the Low preset renders no realtime shadows (that is most of its speed), so a car
// would float. This puts a soft, rounded, plum-tinted contact shadow under every spawned car: one
// 4-vertex quad conformed to the ground by four short raycasts, fading out as the car leaves the ground.
// Costs five raycasts and one tiny draw per car; hidden whenever realtime shadows are on.
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class ContactShadow : MonoBehaviour
    {
        ArcadeCar car;
        GameObject go;
        MeshRenderer mr;
        Mesh mesh;
        MaterialPropertyBlock mpb;
        readonly Vector3[] verts = new Vector3[4];
        float halfW = 0.95f, halfL = 2.2f, alpha = -1f;
        int mask;

        static Material mat;
        static Texture2D tex;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly Color Tint = new Color(0.10f, 0.06f, 0.12f, 0.62f);

        public static void Attach(ArcadeCar c)
        {
            if (c == null || c.GetComponent<ContactShadow>() != null) return;
            c.gameObject.AddComponent<ContactShadow>();
        }

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                halfW = box.size.x * 0.5f * 1.22f;
                halfL = box.size.z * 0.5f * 1.12f;
            }
            mask = ~((1 << CarExtras.CarLayer) | (1 << WorldBuilder.LayerGrass) | (1 << 2) | (1 << 31));
            if (mat == null)
            {
                var sh = MaterialLibrary.FindShader("BlossomRally/Particle");
                if (sh == null) { enabled = false; return; }
                mat = new Material(sh) { name = "BR_ContactShadow" };
                mat.SetTexture("_MainTex", Texture());
                mat.SetFloat("_Lit", 0f);
            }
            go = new GameObject("BR_ContactShadow") { layer = CarExtras.CarLayer };
            mesh = new Mesh { name = "ContactShadow" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mpb = new MaterialPropertyBlock();
            mr.enabled = false;
        }

        void OnDestroy()
        {
            if (go != null) Destroy(go);
            if (mesh != null) Destroy(mesh);
        }

        void LateUpdate()
        {
            if (mr == null) return;
            bool want = !GameSettings.Shadows && car != null && car.isActiveAndEnabled;
            if (!want) { if (mr.enabled) mr.enabled = false; return; }
            Transform t = transform;
            Vector3 up = Vector3.up;
            RaycastHit hit;
            Vector3 origin = t.position + up * 0.9f;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 7f, mask, QueryTriggerInteraction.Ignore)) { mr.enabled = false; return; }
            float height = Mathf.Max(0f, hit.distance - 0.9f);
            float a = Mathf.Clamp01(1f - height / 3f);
            if (a < 0.02f) { mr.enabled = false; return; }
            float grow = 1f + height * 0.25f;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, hit.normal);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(hit.normal, fwd);
            Vector3 c = hit.point;
            Corner(0, c - right * (halfW * grow) - fwd * (halfL * grow), hit.point.y, hit.normal);
            Corner(1, c + right * (halfW * grow) - fwd * (halfL * grow), hit.point.y, hit.normal);
            Corner(2, c + right * (halfW * grow) + fwd * (halfL * grow), hit.point.y, hit.normal);
            Corner(3, c - right * (halfW * grow) + fwd * (halfL * grow), hit.point.y, hit.normal);
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            if (Mathf.Abs(a - alpha) > 0.01f)
            {
                alpha = a;
                Color k = Tint;
                k.a *= a / (grow * grow);
                mpb.SetColor(IdTint, k);
                mr.SetPropertyBlock(mpb);
            }
            if (!mr.enabled) mr.enabled = true;
        }

        // each corner sits on its own ground height so cambered roads and kerbs do not cut the quad
        void Corner(int i, Vector3 p, float y0, Vector3 n)
        {
            RaycastHit h;
            Vector3 from = new Vector3(p.x, y0 + 1.2f, p.z);
            if (Physics.Raycast(from, Vector3.down, out h, 2.6f, mask, QueryTriggerInteraction.Ignore)) verts[i] = h.point + h.normal * 0.045f;
            else verts[i] = p + n * 0.045f;
        }

        /// <summary>Soft rounded rectangle (white, alpha falloff).</summary>
        static Texture2D Texture()
        {
            if (tex != null) return tex;
            const int n = 64;
            tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BR_ContactShadowTex", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n) * 2f - 1f, v = ((y + 0.5f) / n) * 2f - 1f;
                    float qx = Mathf.Abs(u) - 0.42f, qy = Mathf.Abs(v) - 0.55f;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.42f, outside));
                    byte b = (byte)(Mathf.Clamp01(k * k) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, b);
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}

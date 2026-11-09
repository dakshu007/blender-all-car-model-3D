// WaterFX.cs - water upgrade driver: water globals (foam / car ripples / fine ripples), the original
// water look when the upgrade is off (shader LOD 200), and a cheap low-res planar reflection for the
// lake only on High / Ultra (pagoda, trees and pavilion mirrored in the water). The reflection camera
// renders with a replacement shader (vertex colour + sun, no shadows, no grass) and is only updated while
// the lake is on screen and near; High refreshes it every second frame.
using UnityEngine;

namespace BlossomRally
{
    public class WaterFX : MonoBehaviour
    {
        public WorldBuilder world;
        Material lakeMat;
        LakeReflection lake;
        int originalLOD = -1;
        bool lodCached;

        static readonly int IdWaterFx = Shader.PropertyToID("_BR_WaterFx");

        void Start()
        {
            var lakeGo = GameObject.Find("Water_Lake");
            if (lakeGo != null)
            {
                var mr = lakeGo.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    // own material so only the lake samples the planar reflection
                    lakeMat = new Material(mr.sharedMaterial) { name = "BR_Water_Lake" };
                    lakeMat.SetFloat("_PlanarAmount", 0f);
                    mr.sharedMaterial = lakeMat;
                    lake = lakeGo.AddComponent<LakeReflection>();
                    lake.material = lakeMat;
                }
            }
            ApplyQuality();
        }

        public void ApplyQuality()
        {
            int q = GraphicsFX.Q;
            var water = MaterialLibrary.Water;
            if (water != null && water.shader != null)
            {
                if (!lodCached) { originalLOD = water.shader.maximumLOD; lodCached = true; }
                water.shader.maximumLOD = q < 0 ? 200 : originalLOD;
            }
            Shader.SetGlobalVector(IdWaterFx, new Vector4(q < 0 ? 0f : 1f, WorldBuilder.WaterY, q < 1 ? 0f : 1f, 0f));
            if (lake != null)
            {
                lake.quality = q;
                if (q < 2 && lakeMat != null) lakeMat.SetFloat("_PlanarAmount", 0f);
            }
        }

        void OnDestroy()
        {
            var water = MaterialLibrary.Water;
            if (lodCached && water != null && water.shader != null) water.shader.maximumLOD = originalLOD;
        }
    }

    /// <summary>On the lake renderer: renders the mirrored camera when the main camera sees the lake.</summary>
    public class LakeReflection : MonoBehaviour
    {
        public Material material;
        public int quality = 2;
        Camera reflCam;
        RenderTexture rt;
        Shader replace;
        bool rendering;
        int lastFrame = -1;
        float amount;
        readonly float[] cull = new float[32];

        static readonly int IdRefl = Shader.PropertyToID("_ReflectionTex");
        static readonly int IdAmount = Shader.PropertyToID("_PlanarAmount");

        void OnWillRenderObject()
        {
            Camera cam = Camera.current;
            if (cam == null || rendering || material == null || cam != Camera.main) return;
            var r = GetComponent<Renderer>();
            Vector3 centre = r != null ? r.bounds.center : transform.position;   // the mesh is built in world space
            bool want = quality >= 2 && Vector3.Distance(cam.transform.position, centre) < 520f;
            amount = Mathf.MoveTowards(amount, want ? 0.85f : 0f, Time.unscaledDeltaTime * 2f);
            material.SetFloat(IdAmount, rt != null ? amount : 0f);
            if (!want) return;
            int every = quality >= 3 ? 1 : 2;
            if (rt != null && Time.frameCount - lastFrame < every) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (replace == null) replace = Shader.Find("Hidden/BlossomRally/ReflectionReplace");
            if (replace == null) return;
            rendering = true;
            try { Render(cam); }
            finally { rendering = false; }
            material.SetTexture(IdRefl, rt);
            material.SetFloat(IdAmount, amount);
        }

        void Render(Camera cam)
        {
            int h = quality >= 3 ? 432 : 288;
            int w = Mathf.Max(64, Mathf.RoundToInt(h * cam.aspect));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                rt = new RenderTexture(w, h, 16, fmt) { name = "BR_LakeReflection", hideFlags = HideFlags.DontSave };
                rt.Create();
            }
            if (reflCam == null)
            {
                var go = new GameObject("BR_LakeReflectionCam") { hideFlags = HideFlags.HideAndDontSave };
                reflCam = go.AddComponent<Camera>();
                reflCam.enabled = false;
            }
            reflCam.CopyFrom(cam);
            reflCam.enabled = false;
            reflCam.targetTexture = rt;
            reflCam.clearFlags = CameraClearFlags.Skybox;
            reflCam.depthTextureMode = DepthTextureMode.None;
            reflCam.allowMSAA = false;
            reflCam.useOcclusionCulling = false;
            reflCam.farClipPlane = 650f;
            reflCam.renderingPath = RenderingPath.Forward;
            reflCam.cullingMask = cam.cullingMask & ~((1 << WorldBuilder.LayerGrass) | (1 << 4) | (1 << 31));
            for (int i = 0; i < cull.Length; i++) cull[i] = 0f;
            cull[WorldBuilder.LayerTrees] = 260f;
            cull[CarExtras.CarLayer] = 120f;
            reflCam.layerCullDistances = cull;
            reflCam.layerCullSpherical = true;

            float y = WorldBuilder.WaterY;
            Vector3 normal = Vector3.up;
            Vector4 plane = new Vector4(0f, 1f, 0f, -y);
            Matrix4x4 refl = ReflectionMatrix(plane);
            Vector3 camPos = cam.transform.position;
            reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            Vector4 clip = CameraSpacePlane(reflCam, new Vector3(0f, y, 0f), normal, 0.06f);
            reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clip);
            Vector3 e = cam.transform.eulerAngles;
            reflCam.transform.SetPositionAndRotation(refl.MultiplyPoint(camPos), Quaternion.Euler(-e.x, e.y, e.z));

            float shadowDist = QualitySettings.shadowDistance;
            int pixelLights = QualitySettings.pixelLightCount;
            bool inverted = GL.invertCulling;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.pixelLightCount = 0;
            GL.invertCulling = !inverted;
            try { reflCam.RenderWithShader(replace, "RenderType"); }
            finally
            {
                GL.invertCulling = inverted;
                QualitySettings.shadowDistance = shadowDist;
                QualitySettings.pixelLightCount = pixelLights;
            }
        }

        static Vector4 CameraSpacePlane(Camera c, Vector3 pos, Vector3 normal, float offset)
        {
            Matrix4x4 m = c.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(pos + normal * offset);
            Vector3 cn = m.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cpos, cn));
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        void OnDestroy()
        {
            if (reflCam != null) Destroy(reflCam.gameObject);
            if (rt != null) { rt.Release(); Destroy(rt); }
        }
    }
}

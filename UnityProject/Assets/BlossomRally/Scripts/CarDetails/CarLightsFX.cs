// CarLightsFX.cs - light-related car details:
//  * headlight beams: soft additive cones from the head lamps (and the rally aux lamps at night), visible in
//    fog / rain / at night (Atmosphere.NightFactor, RainIntensity, RenderSettings fog density); a broken lamp
//    (CarDamage) loses its beam. 2 draw calls (head + aux), skipped at Quality Low.
//  * brake discs glowing red-orange: a brake temperature per wheel heats with braking power and cools with
//    time and airflow; drives the emission of a hot variant of M_Brake_Disc through a per-submesh
//    MaterialPropertyBlock (one shared material, no per-car material copies).
//  * exhaust: pipe tips (rear-low from the rear bumper bounds), short orange / blue flame cones + an
//    orange light flicker on EngineAudio.Backfire (and flat-shift flames on Group B upshifts), and a
//    heat-haze shimmer quad (GrabPass, Quality High+).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class CarLightsFX : CarDetailSystem
    {
        static Material beamMat, flameMat, hazeMat;
        static readonly Dictionary<Material, Material> hotDisc = new Dictionary<Material, Material>();
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        static readonly int IdStrength = Shader.PropertyToID("_Strength");

        CarDetails det;
        CarAnatomy a;
        MaterialPropertyBlock mpb;

        // beams
        GameObject headGo, auxGo;
        Mesh headMesh, auxMesh;
        MeshRenderer headR, auxR;

        // brakes
        sealed class Disc { public Renderer r; public int index; public Material orig; public float temp, shown = -1f; public bool front; }
        readonly List<Disc> discs = new List<Disc>();

        // exhaust
        readonly List<Vector3> tips = new List<Vector3>();
        readonly List<Transform> flames = new List<Transform>();
        readonly List<MeshRenderer> flameR = new List<MeshRenderer>();
        Mesh flameMesh;
        Light exLight;
        float flameT, flameDur, flameLen, flameLoud;
        Transform haze;
        MeshRenderer hazeR;
        Mesh hazeMesh;
        float heat;

        public override void Build(CarDetails d)
        {
            det = d;
            a = d.a;
            mpb = new MaterialPropertyBlock();
            if (d.quality >= 1) BuildBeams();
            BuildBrakes(d);
            BuildExhaust(d);
            EngineAudio.Backfire += OnBackfire;
            EngineAudio.GearShift += OnShift;
            if (d.damage != null) d.damage.DamageChanged += OnDamage;
        }

        public override void Dispose()
        {
            EngineAudio.Backfire -= OnBackfire;
            EngineAudio.GearShift -= OnShift;
            if (det != null && det.damage != null) det.damage.DamageChanged -= OnDamage;
            foreach (var o in new Object[] { headGo, auxGo, headMesh, auxMesh, flameMesh, hazeMesh }) if (o != null) Object.Destroy(o);
            foreach (var f in flames) if (f != null) Object.Destroy(f.gameObject);
            if (exLight != null) Object.Destroy(exLight.gameObject);
            if (haze != null) Object.Destroy(haze.gameObject);
            foreach (var dsc in discs)
            {
                if (dsc.r == null) continue;
                var mats = dsc.r.sharedMaterials;
                if (dsc.index < mats.Length) { mats[dsc.index] = dsc.orig; dsc.r.sharedMaterials = mats; }
                if (mpb != null) { mpb.Clear(); dsc.r.SetPropertyBlock(mpb, dsc.index); }
            }
            discs.Clear();
        }

        void OnDamage() { if (headGo != null) RebuildBeamMeshes(); }

        // ------------------------------------------------------------------ beams

        static Material BeamMat()
        {
            if (beamMat == null)
            {
                beamMat = new Material(MaterialLibrary.FindShader("BlossomRally/Additive")) { name = "BR_HeadlightBeam" };
                beamMat.SetFloat("_Soft", 1.8f);
            }
            return beamMat;
        }

        void BuildBeams()
        {
            headGo = MakeObj("HeadlightBeams", out headR);
            auxGo = MakeObj("AuxBeams", out auxR);
            headR.sharedMaterial = BeamMat();
            auxR.sharedMaterial = BeamMat();
            RebuildBeamMeshes();
        }

        GameObject MakeObj(string name, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.layer = CarExtras.CarLayer;
            go.transform.SetParent(det.transform, false);
            go.AddComponent<MeshFilter>();
            r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return go;
        }

        void RebuildBeamMeshes()
        {
            var dmg = det.damage;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var t = new List<int>();
            for (int s = 0; s < 2; s++)
                if (dmg == null || !dmg.HeadlightBroken(s))
                    Cone(v, n, c, t, a.head[s] + Vector3.forward * 0.02f, Quaternion.AngleAxis(4f, Vector3.right) * Quaternion.AngleAxis(s == 0 ? -3f : 3f, Vector3.up) * Vector3.forward,
                         Mathf.Max(0.05f, a.headR[s] * 0.9f), 16f, 12f, new Color(1f, 0.93f, 0.8f));
            SetMesh(ref headMesh, headGo, "HeadlightBeams", v, n, c, t);
            v.Clear(); n.Clear(); c.Clear(); t.Clear();
            for (int s = 0; s < 2; s++)
                if (a.hasAux[s] && (dmg == null || !dmg.AuxBroken(s)))
                    Cone(v, n, c, t, a.aux[s] + Vector3.forward * 0.02f, Quaternion.AngleAxis(2.5f, Vector3.right) * Vector3.forward,
                         Mathf.Max(0.05f, a.auxR[s] * 0.9f), 24f, 7f, new Color(1f, 0.88f, 0.65f));
            SetMesh(ref auxMesh, auxGo, "AuxBeams", v, n, c, t);
        }

        static void SetMesh(ref Mesh m, GameObject go, string name, List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t)
        {
            if (m == null) { m = new Mesh { name = name }; go.GetComponent<MeshFilter>().sharedMesh = m; }
            m.Clear();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
        }

        static void Cone(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t, Vector3 apex, Vector3 dir, float r0, float len, float halfAngle, Color col)
        {
            const int sides = 14, rings = 7;
            dir.Normalize();
            Vector3 u = Vector3.Cross(dir, Vector3.up).normalized, w = Vector3.Cross(u, dir);
            float r1 = Mathf.Tan(halfAngle * Mathf.Deg2Rad) * len + r0;
            int baseIdx = v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float k = (float)i / rings;
                float tt = k * k;            // denser rings near the lamp
                float r = Mathf.Lerp(r0, r1, tt);
                float alpha = Mathf.Pow(1f - tt, 1.7f) * Mathf.Clamp01(tt * 30f + 0.25f);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s * Mathf.PI * 2f / sides;
                    Vector3 radial = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    v.Add(apex + dir * (len * tt) + radial * r);
                    n.Add(radial);
                    c.Add(new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(alpha * 255f)));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = baseIdx + i * sides + s, a1 = baseIdx + i * sides + (s + 1) % sides;
                    int b0 = a0 + sides, b1 = a1 + sides;
                    t.Add(a0); t.Add(b0); t.Add(a1);
                    t.Add(a1); t.Add(b0); t.Add(b1);
                }
        }

        void TickBeams(CarDetails d)
        {
            if (headR == null) return;
            float night = Atmosphere.NightFactor, rain = Atmosphere.RainIntensity;
            float fog = RenderSettings.fog ? Mathf.Clamp01(RenderSettings.fogDensity / 0.007f) : 0f;
            float on = Mathf.Clamp01((night - 0.12f) * 1.6f) + rain * 0.6f;      // same switch-on rule as CarExtras
            float k = d.showroom ? 0f : Mathf.Clamp(on, 0f, 1.4f) * (0.16f + 0.45f * fog + 0.35f * rain) * (0.35f + 0.65f * night);
            bool show = k > 0.01f;
            headR.enabled = show;
            if (show)
            {
                mpb.Clear();
                mpb.SetFloat(IdIntensity, k * 0.55f);
                mpb.SetColor(IdColor, Color.white);
                headR.SetPropertyBlock(mpb);
            }
            float ka = k * Mathf.Clamp01((night - 0.4f) * 2f);
            auxR.enabled = ka > 0.01f && auxMesh != null && auxMesh.vertexCount > 0;
            if (auxR.enabled)
            {
                mpb.Clear();
                mpb.SetFloat(IdIntensity, ka * 0.5f);
                mpb.SetColor(IdColor, Color.white);
                auxR.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------------ brakes

        void BuildBrakes(CarDetails d)
        {
            foreach (var w in d.car.wheels)
            {
                if (w.visual == null) continue;
                foreach (var r in w.visual.GetComponentsInChildren<Renderer>(true))
                {
                    int idx = CarAnatomy.FindSubmesh(r, "Brake_Disc");
                    if (idx < 0) continue;
                    var mats = r.sharedMaterials;
                    var src = mats[idx];
                    Material hot;
                    if (!hotDisc.TryGetValue(src, out hot) || hot == null)
                    {
                        hot = new Material(src) { name = src.name + "_Hot" };
                        hot.EnableKeyword("_EMISSION");
                        hot.SetColor(IdEmission, Color.black);
                        hot.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        hotDisc[src] = hot;
                    }
                    mats[idx] = hot;
                    r.sharedMaterials = mats;
                    discs.Add(new Disc { r = r, index = idx, orig = src, front = w.front });
                }
            }
        }

        void TickBrakes(CarDetails d, float dt)
        {
            if (discs.Count == 0 || dt <= 0f) return;
            var car = d.car;
            float v = Mathf.Abs(car.ForwardSpeed);
            bool braking = !d.showroom && car.brake > 0.1f && car.ForwardSpeed > 1.5f;
            float cool = 0.045f + Mathf.Min(v, 50f) / 42f * 0.11f;
            foreach (var dsc in discs)
            {
                float heatIn = 0f;
                if (braking) heatIn += car.brake * v / 42f * (dsc.front ? 0.16f : 0.11f);
                if (!dsc.front && car.handbrake > 0.5f && !d.showroom) heatIn += v / 42f * 0.06f;
                dsc.temp = Mathf.Clamp(dsc.temp + (heatIn - dsc.temp * cool) * dt, 0f, 1.3f);
                float glow = Mathf.Clamp01((dsc.temp - 0.42f) / 0.58f);
                glow = glow * glow * (3f - 2f * glow);
                if (Mathf.Abs(glow - dsc.shown) < 0.01f) continue;
                dsc.shown = glow;
                Color e = Color.Lerp(new Color(0.55f, 0.03f, 0f), new Color(2.6f, 0.95f, 0.18f), glow) * glow * 2.2f;
                mpb.Clear();
                mpb.SetColor(IdEmission, e);
                if (dsc.r != null) dsc.r.SetPropertyBlock(mpb, dsc.index);
            }
        }

        // ------------------------------------------------------------------ exhaust

        void BuildExhaust(CarDetails d)
        {
            if (a.electric) return;     // no tailpipe, no flames, no heat haze
            Vector3 ex = a.exhaust;
            tips.Add(ex);
            if (a.groupB) tips.Add(ex + Vector3.left * 0.085f);
            Color32 pipe = LifeGeo.C(70, 66, 62), soot = LifeGeo.C(14, 12, 12);
            var g = new LifeGeo();
            foreach (var p in tips)
            {
                g.Frustum(p + Vector3.forward * 0.12f, p, 0.032f, 0.032f, 0.034f, 0.034f, 8, 0f, pipe, pipe, false, false);
                g.Disc(p + Vector3.forward * 0.004f, 0.03f, 8, 0f, Vector3.back, soot, soot);
            }
            int part = d.batch.AddPart(d.batch.AddTemplate(g));
            d.batch.Set(part, Matrix4x4.identity);

            if (flameMat == null)
            {
                flameMat = new Material(MaterialLibrary.FindShader("BlossomRally/Additive")) { name = "BR_ExhaustFlame" };
                flameMat.SetFloat("_Soft", 0.7f);
            }
            flameMesh = BuildFlameMesh();
            foreach (var p in tips)
            {
                MeshRenderer r;
                var go = MakeObj("ExhaustFlame", out r);
                go.GetComponent<MeshFilter>().sharedMesh = flameMesh;
                r.sharedMaterial = flameMat;
                go.transform.localPosition = p + Vector3.back * 0.01f;
                go.transform.localRotation = Quaternion.LookRotation(Vector3.back + Vector3.down * 0.05f);
                flames.Add(go.transform);
                flameR.Add(r);
            }
            if (d.quality >= 2)
            {
                var lg = new GameObject("ExhaustLight");
                lg.transform.SetParent(d.transform, false);
                lg.transform.localPosition = ex + Vector3.back * 0.3f + Vector3.up * 0.05f;
                exLight = lg.AddComponent<Light>();
                exLight.type = LightType.Point;
                exLight.color = new Color(1f, 0.55f, 0.2f);
                exLight.range = 3.5f;
                exLight.shadows = LightShadows.None;
                exLight.intensity = 0f;
                exLight.enabled = false;

                if (hazeMat == null)
                {
                    hazeMat = new Material(MaterialLibrary.FindShader("BlossomRally/HeatHaze")) { name = "BR_HeatHaze" };
                    hazeMat.SetTexture("_NoiseTex", CarDetailTextures.Noise);
                }
                var hgo = MakeObj("HeatHaze", out hazeR);
                hazeMesh = new Mesh { name = "HeatHazeQuad" };
                hazeMesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
                hazeMesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                hazeMesh.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
                hazeMesh.RecalculateBounds();
                hgo.GetComponent<MeshFilter>().sharedMesh = hazeMesh;
                hazeR.sharedMaterial = hazeMat;
                haze = hgo.transform;
                haze.localScale = new Vector3(0.6f, 0.5f, 1f);
            }
        }

        static Mesh BuildFlameMesh()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var t = new List<int>();
            // outer orange cone (length 1 along +Z), inner blue core
            FlameCone(v, n, c, t, 1f, 0.07f, new Color32(255, 214, 120, 255), new Color32(255, 110, 30, 200), new Color32(200, 40, 10, 0));
            FlameCone(v, n, c, t, 0.42f, 0.035f, new Color32(120, 150, 255, 255), new Color32(200, 220, 255, 220), new Color32(255, 200, 120, 0));
            var m = new Mesh { name = "ExhaustFlame" };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static void FlameCone(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t, float len, float r, Color32 c0, Color32 c1, Color32 c2)
        {
            const int sides = 8;
            float[] zs = { 0f, 0.25f, 1f };
            float[] rs = { r * 0.8f, r, 0.002f };
            Color32[] cs = { c0, c1, c2 };
            int b = v.Count;
            for (int i = 0; i < 3; i++)
                for (int s = 0; s < sides; s++)
                {
                    float ang = s * Mathf.PI * 2f / sides;
                    Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    v.Add(radial * rs[i] + Vector3.forward * zs[i] * len);
                    n.Add(radial);
                    c.Add(cs[i]);
                }
            for (int i = 0; i < 2; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = b + i * sides + s, a1 = b + i * sides + (s + 1) % sides;
                    t.Add(a0); t.Add(a0 + sides); t.Add(a1);
                    t.Add(a1); t.Add(a0 + sides); t.Add(a1 + sides);
                }
        }

        void OnBackfire(ArcadeCar car, float loud)
        {
            if (det == null || car != det.car || det.showroom) return;
            Fire(loud);
        }

        void OnShift(ArcadeCar car, int from, int to)
        {
            if (det == null || car != det.car || det.showroom || !a.groupB) return;
            if (to > from && car.throttle > 0.6f && car.EngineRpm > 4500f && Random.value < 0.5f) Fire(0.4f);
        }

        void Fire(float loud)
        {
            flameLoud = Mathf.Max(flameLoud * (flameT > 0f ? 1f : 0f), loud);
            flameDur = Random.Range(0.06f, 0.13f) * (0.6f + loud);
            flameT = flameDur;
            flameLen = Random.Range(0.22f, 0.32f) + 0.38f * loud;
        }

        void TickExhaust(CarDetails d, float dt)
        {
            var car = d.car;
            bool on = flameT > 0f;
            float k = 0f;
            if (on)
            {
                flameT -= dt;
                k = Mathf.Clamp01(flameT / Mathf.Max(0.01f, flameDur));
            }
            for (int i = 0; i < flames.Count; i++)
            {
                flameR[i].enabled = on;
                if (!on) continue;
                float flick = Random.Range(0.75f, 1.15f);
                float len = flameLen * (0.5f + 0.5f * k) * flick;
                flames[i].localScale = new Vector3(Random.Range(0.8f, 1.2f), Random.Range(0.8f, 1.2f), len);
                mpb.Clear();
                mpb.SetFloat(IdIntensity, (0.8f + 1.6f * k) * (0.6f + 0.6f * flameLoud));
                mpb.SetColor(IdColor, Color.white);
                flameR[i].SetPropertyBlock(mpb);
            }
            if (exLight != null)
            {
                exLight.enabled = on;
                if (on) exLight.intensity = (1.2f + 2.2f * flameLoud) * k * Random.Range(0.6f, 1.1f) * (0.5f + Atmosphere.NightFactor);
            }

            if (haze != null && dt > 0f)
            {
                float rpm01 = Mathf.InverseLerp(900f, 7600f, car.EngineRpm);
                float target = d.showroom ? 0.3f : 0.25f + 0.45f * rpm01 + 0.4f * car.throttle;
                heat = Mathf.Lerp(heat, target, 1f - Mathf.Exp(-dt * 0.5f));
                float strength = heat * (1f - Mathf.Clamp01(d.speedMs / 18f)) * (1f - Atmosphere.RainIntensity * 0.6f) + (on ? 0.4f * k : 0f);
                bool show = strength > 0.03f && d.cam != null && d.camDist < 45f;
                hazeR.enabled = show;
                if (show)
                {
                    Vector3 wp = d.transform.TransformPoint(a.exhaust + Vector3.back * 0.32f + Vector3.up * 0.12f);
                    Vector3 toCam = d.cam.transform.position - wp;
                    haze.SetPositionAndRotation(wp, Quaternion.LookRotation(-toCam, Vector3.up));
                    mpb.Clear();
                    mpb.SetFloat(IdStrength, Mathf.Clamp(strength, 0f, 1.5f));
                    hazeR.SetPropertyBlock(mpb);
                }
            }
        }

        public override void Tick(CarDetails d, float dt)
        {
            TickBeams(d);
            TickBrakes(d, dt);
            TickExhaust(d, dt);
        }
    }
}

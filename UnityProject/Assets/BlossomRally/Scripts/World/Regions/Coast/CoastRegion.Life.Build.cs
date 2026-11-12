// CoastRegion.Life.Build.cs - what moves on the Lotus Coast: junks and sampans bobbing at their moorings,
// seagull flocks gliding over the harbour, the beach and the headland, kites tugging on their strings over
// the dunes, and the lighthouse beam sweeping the sea at night. All CPU-posed in two LifeBatches (two draw
// calls) plus one additive beam mesh; Tick allocates nothing.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        LifeBatch boatBatch, skyBatch;
        float[] boatPhase;
        int boatFrame;
        Transform beam;
        Material beamMat;
        float beamYaw;

        struct Gull { public Vector3 centre; public float radius, speed, angle, alt, phase; public int body, wingL, wingR; }
        Gull[] gulls;
        int[] kiteParts;

        void BuildLife()
        {
            // ---- boats
            if (boatSpots.Count > 0)
            {
                boatBatch = new LifeBatch();
                int[] junkT = new int[3], sampT = new int[4];
                for (int i = 0; i < 3; i++) junkT[i] = boatBatch.AddTemplate(CoastShapes.Junk(new System.Random(31 + i)));
                for (int i = 0; i < 4; i++) sampT[i] = boatBatch.AddTemplate(CoastShapes.Sampan(new System.Random(51 + i)));
                boatPhase = new float[boatSpots.Count];
                for (int i = 0; i < boatSpots.Count; i++)
                {
                    boatBatch.AddPart(boatSpots[i].w < 0.5f ? junkT[i % 3] : sampT[i % 4]);
                    boatPhase[i] = Rn(0f, 6.28f);
                }
                boatBatch.Finish("Coast_Boats", MaterialLibrary.VertexColorWind, cx.Root, WorldBuilder.LayerTrees, true);
                PoseBoats(0f, Vector3.zero, true);
            }

            // ---- gulls and kites
            skyBatch = new LifeBatch();
            int bodyT = skyBatch.AddTemplate(GullBody()), wingLT = skyBatch.AddTemplate(GullWing(-1)), wingRT = skyBatch.AddTemplate(GullWing(1));
            Vector4[] flocks =
            {
                new Vector4(-1262f, 250f, 16f, 7f), new Vector4(-1190f, -262f, 14f, 6f), new Vector4(-1390f, 0f, 44f, 5f), new Vector4(-1452f, 214f, 22f, 4f),
            };
            var gl = new List<Gull>();
            foreach (var f in flocks)
                for (int i = 0; i < (int)f.w; i++)
                {
                    var g = new Gull
                    {
                        centre = new Vector3(f.x + Rn(-20f, 20f), f.z + Rn(-4f, 6f), f.y + Rn(-20f, 20f)), radius = Rn(18f, 55f),
                        speed = Rn(6.5f, 9.5f) * (Ch(0.5f) ? 1f : -1f), angle = Rn(0f, 6.28f), alt = Rn(2f, 6f), phase = Rn(0f, 30f)
                    };
                    g.body = skyBatch.AddPart(bodyT);
                    g.wingL = skyBatch.AddPart(wingLT);
                    g.wingR = skyBatch.AddPart(wingRT);
                    gl.Add(g);
                }
            gulls = gl.ToArray();
            kiteParts = new int[kiteSpots.Count];
            Color32[] ka = { LifeGeo.C(226, 60, 50), LifeGeo.C(250, 196, 60), LifeGeo.C(60, 140, 210), LifeGeo.C(240, 120, 170), LifeGeo.C(80, 180, 140), LifeGeo.C(250, 140, 60) };
            for (int i = 0; i < kiteSpots.Count; i++)
                kiteParts[i] = skyBatch.AddPart(skyBatch.AddTemplate(CoastShapes.KiteOnString(i % 2, ka[i % ka.Length], ka[(i + 3) % ka.Length], Rn(14f, 24f), Rn(12f, 20f))));
            skyBatch.Finish("Coast_SkyLife", MaterialLibrary.VertexColorWind, cx.Root, WorldBuilder.LayerTrees, false);
            PoseSky(0f, true);

            // ---- lighthouse beam (additive, sweeps at night)
            beamMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_CoastBeam" };
            beamMat.SetTexture("_MainTex", ProceduralTextures.Soft);
            beamMat.SetVector("_Fade", new Vector4(0.5f, 4f, 2400f, 3200f));
            beamMat.SetFloat("_EdgeFade", 0.45f);
            beamMat.SetFloat("_VertexGamma", 1f);
            beamMat.SetColor("_Tint", new Color(1f, 0.95f, 0.8f, 0f));
            var go = cx.AddMesh("Coast_LighthouseBeam", BeamMesh(), beamMat, 0, false, true);
            beam = go.transform;
            beam.position = lanternPos;
            go.SetActive(false);
        }

        static LifeGeo GullBody()
        {
            var g = new LifeGeo();
            Color32 white = LifeGeo.C(246, 246, 242), grey = LifeGeo.C(178, 186, 196), beak = LifeGeo.C(246, 196, 60), eye = LifeGeo.C(30, 30, 34);
            g.Ellipsoid(new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.1f, 0.3f), 7, 4, grey, white);
            g.Ellipsoid(new Vector3(0f, 0.05f, 0.27f), new Vector3(0.075f, 0.075f, 0.085f), 6, 4, white, white);
            g.Frustum(new Vector3(0f, 0.045f, 0.34f), new Vector3(0f, 0.035f, 0.44f), 0.025f, 0.02f, 0.004f, 0.004f, 4, 0f, beak, beak, true, false);
            for (int s = -1; s <= 1; s += 2) g.Box(new Vector3(s * 0.06f, 0.075f, 0.3f), new Vector3(0.008f, 0.012f, 0.012f), eye);
            g.Tri2(new Vector3(-0.07f, 0.02f, -0.24f), new Vector3(0.07f, 0.02f, -0.24f), new Vector3(0f, 0.03f, -0.42f), white, white, grey);
            return g;
        }

        static LifeGeo GullWing(int side)
        {
            var g = new LifeGeo();
            float s = side;
            Color32 grey = LifeGeo.C(170, 178, 190), light = LifeGeo.C(206, 212, 220), black = LifeGeo.C(36, 36, 40);
            Vector3 rf = new Vector3(0f, 0f, 0.12f), rb = new Vector3(0f, 0f, -0.14f);
            Vector3 mf = new Vector3(0.38f * s, 0.03f, 0.1f), mb = new Vector3(0.36f * s, 0.02f, -0.12f);
            Vector3 tf = new Vector3(0.66f * s, 0f, 0.02f), tb = new Vector3(0.6f * s, 0f, -0.1f), tip = new Vector3(0.8f * s, -0.02f, -0.06f);
            g.Tri2(rf, mf, mb, light, grey, grey);
            g.Tri2(rf, mb, rb, light, grey, light);
            g.Tri2(mf, tf, tb, grey, grey, grey);
            g.Tri2(mf, tb, mb, grey, grey, grey);
            g.Tri2(tf, tip, tb, black, black, black);
            return g;
        }

        static Mesh BeamMesh()
        {
            var v = new List<Vector3>(); var c = new List<Color32>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
            float[] tt = { 0f, 0.08f, 0.35f, 1f };
            byte[] aa = { 0, 210, 120, 0 };
            const float L = 240f;
            for (int dir = -1; dir <= 1; dir += 2)
                for (int q = 0; q < 3; q++)
                {
                    float rot = q * Mathf.PI / 3f;
                    Vector3 side = new Vector3(Mathf.Cos(rot), Mathf.Sin(rot), 0f);
                    Vector3 nrm = new Vector3(-side.y, side.x, 0f);
                    int s0 = v.Count;
                    for (int k = 0; k < tt.Length; k++)
                    {
                        float z = dir * (0.5f + tt[k] * L);
                        float w = Mathf.Lerp(0.7f, 16f, tt[k]);
                        Vector3 cpos = new Vector3(0f, -tt[k] * L * 0.035f, z);
                        Color32 col = new Color32(255, 248, 220, aa[k]);
                        v.Add(cpos - side * w); c.Add(col); uv.Add(new Vector2(0f, 0.5f)); n.Add(nrm);
                        v.Add(cpos + side * w); c.Add(col); uv.Add(new Vector2(1f, 0.5f)); n.Add(nrm);
                        if (k > 0)
                        {
                            int a = s0 + (k - 1) * 2;
                            t.Add(a); t.Add(a + 2); t.Add(a + 1);
                            t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
                        }
                    }
                }
            var m = new Mesh { name = "Coast_Beam" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, new Vector3(2f * L, 40f, 2f * L));
            return m;
        }

        // ------------------------------------------------------------------ tick
        void TickLife(float dt, Vector3 eye)
        {
            float t = Time.time;
            if (boatBatch != null && (++boatFrame % 3) == 0) PoseBoats(t, eye, false);
            if (skyBatch != null)
            {
                float dx = eye.x + 1250f, dz = eye.z;
                if (dx * dx + dz * dz < 900f * 900f) PoseSky(t, false);
            }
            if (beam != null)
            {
                float night = Atmosphere.NightFactor;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.6f, night)) * (1f - 0.35f * Atmosphere.RainIntensity);
                bool on = a > 0.01f;
                if (beam.gameObject.activeSelf != on) beam.gameObject.SetActive(on);
                if (on)
                {
                    beamYaw = Mathf.Repeat(beamYaw + dt * 36f, 360f);
                    beam.rotation = Quaternion.Euler(0f, beamYaw, 0f);
                    beamMat.SetColor("_Tint", new Color(1f, 0.95f, 0.8f, 0.85f * a));
                }
            }
        }

        void PoseBoats(float t, Vector3 eye, bool force)
        {
            bool any = false;
            for (int i = 0; i < boatSpots.Count; i++)
            {
                var b = boatSpots[i];
                float dx = b.x - eye.x, dz = b.y - eye.z;
                if (!force && dx * dx + dz * dz > 420f * 420f) continue;
                float ph = boatPhase[i];
                bool junk = b.w < 0.5f;
                float k = junk ? 0.6f : 1f;
                float heave = (0.1f * Mathf.Sin(t * 0.9f + ph) + 0.04f * Mathf.Sin(t * 2.1f + ph * 2f)) * k;
                float pitch = 1.4f * k * Mathf.Sin(t * 0.7f + ph * 1.3f), roll = 2.6f * k * Mathf.Sin(t * 0.83f + ph);
                float yaw = b.z + 3f * Mathf.Sin(t * 0.07f + ph);
                boatBatch.Set(i, Matrix4x4.TRS(new Vector3(b.x, WorldField.WaterY + (junk ? 0.05f : 0.12f) + heave, b.y), Quaternion.Euler(pitch, yaw, roll), Vector3.one));
                any = true;
            }
            if (any) boatBatch.Upload();
        }

        void PoseSky(float t, bool force)
        {
            for (int i = 0; i < gulls.Length; i++)
            {
                var g = gulls[i];
                float w = g.speed / g.radius;
                float a = g.angle + w * t;
                float wob = 1f + 0.18f * Mathf.Sin(t * 0.21f + g.phase);
                Vector3 pos = g.centre + new Vector3(Mathf.Cos(a) * g.radius * wob, g.alt * Mathf.Sin(t * 0.33f + g.phase), Mathf.Sin(a) * g.radius);
                Vector3 vel = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(g.speed);
                float bank = -Mathf.Sign(g.speed) * 14f;
                Quaternion q = Quaternion.LookRotation(vel, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
                Matrix4x4 m = Matrix4x4.TRS(pos, q, Vector3.one * 1.15f);
                // glide, then a few flaps every now and then
                float cyc = Mathf.Repeat(t + g.phase, 7f);
                float flap = cyc < 1.4f ? 34f * Mathf.Sin(cyc * 15f) : 6f + 3f * Mathf.Sin(t * 1.3f + g.phase);
                skyBatch.Set(g.body, m);
                skyBatch.Set(g.wingL, m * Matrix4x4.TRS(new Vector3(-0.07f, 0.04f, 0.03f), Quaternion.Euler(0f, 0f, -flap), Vector3.one));
                skyBatch.Set(g.wingR, m * Matrix4x4.TRS(new Vector3(0.07f, 0.04f, 0.03f), Quaternion.Euler(0f, 0f, flap), Vector3.one));
            }
            for (int i = 0; i < kiteParts.Length; i++)
            {
                var k = kiteSpots[i];
                float ph = i * 1.7f;
                float gust = 1f + Atmosphere.Gust;
                Quaternion q = Quaternion.Euler(-4f * Mathf.Sin(t * 0.8f + ph) * gust, k.w + 6f * Mathf.Sin(t * 0.37f + ph) * gust, 5f * Mathf.Sin(t * 1.1f + ph) * gust);
                skyBatch.Set(kiteParts[i], Matrix4x4.TRS(new Vector3(k.x, k.y, k.z), q, Vector3.one));
            }
            skyBatch.Upload();
        }
    }
}

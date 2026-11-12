// CityRegion.Build.cs - builds Jade Harbour City: every tower lot of CityPlan, the harbour (water, quays,
// bridge, terminal, ships, lighthouse), the landmarks, the street detail, parks and escarpment rocks, the neon
// and a fountain spray particle system. Tick animates neon, beacons, boats, the lighthouse beam and the spray
// (no allocations). The colour grade gives cool teal shadows with warm neon highlights.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public sealed partial class CityRegion
    {
        CityChunkGeo geo;
        CityNeon neon;
        System.Random rng;
        int renderers;
        float tickT, glowT;
        ParticleSystem spray;
        ParticleSystem.EmitParams ep;
        Vector3[] jetPos;
        Vector4[] jetVel;
        readonly List<Vector4> fountainVel = new List<Vector4>();

        static readonly Grade CityGrade = new Grade
        {
            tint = new Vector3(0.98f, 1.0f, 1.03f), sat = 1.08f, con = 1.05f,
            shadow = new Vector3(0.78f, 0.98f, 1.10f), shadowAmt = 0.12f,
            lift = new Vector3(1.0f, 0.80f, 0.66f), liftAmt = 0.035f
        };

        public override Grade Grade { get { return CityGrade; } }

        public override IEnumerator Build(RegionContext ctx)
        {
            float t0 = Time.realtimeSinceStartup;
            rng = new System.Random(8088);
            geo = new CityChunkGeo();
            neon = new CityNeon();
            IndexRoads(ctx);
            ctx.Progress = 0.02f;
            yield return null;

            int k = 0;
            foreach (var lot in CityPlan.Lots)
            {
                BuildLot(ctx, lot);
                if (++k % 6 == 0) { ctx.Progress = 0.02f + 0.3f * k / CityPlan.Lots.Count; yield return null; }
            }
            BuildWater(ctx);
            BuildQuays(ctx);
            ctx.Progress = 0.38f;
            yield return null;
            BuildBridge(ctx);
            BuildTerminal(ctx);
            ctx.Progress = 0.45f;
            yield return null;
            BuildShips(ctx);
            BuildLighthouse(ctx);
            ctx.Progress = 0.5f;
            yield return null;
            BuildLanternTower(ctx);
            BuildGatewayArch(ctx);
            ctx.Progress = 0.56f;
            yield return null;
            BuildOldTown(ctx);
            ctx.Progress = 0.62f;
            yield return null;
            BuildStadium(ctx);
            BuildPlazas(ctx);
            ctx.Progress = 0.68f;
            yield return null;
            BuildKerbs(ctx);
            ctx.Progress = 0.74f;
            yield return null;
            BuildStreetFurniture(ctx);
            ctx.Progress = 0.82f;
            yield return null;
            BuildJunctions(ctx);
            BuildBarriers(ctx);
            BuildCircusDecor(ctx);
            BuildBillboards(ctx);
            ctx.Progress = 0.9f;
            yield return null;
            BuildParks(ctx);
            ctx.Progress = 0.96f;
            yield return null;
            geo.Flush(ctx);
            renderers += neon.Finish(ctx);
            BuildSpray(ctx);
            ctx.Progress = 1f;
            Debug.Log("[BlossomRally] Jade Harbour City: " + CityPlan.Lots.Count + " buildings, " + neon.Signs + " neon signs, " + boats.Count + " boats, "
                      + renderers + " own renderers, " + ctx.ColliderCount + " colliders, built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }

        void BuildLot(RegionContext ctx, CityLot lot)
        {
            var beacons = new List<Vector3>();
            var signs = new List<CitySignSpot>();
            var model = CityTowers.Build(lot, beacons, signs);
            Vector3 pos = new Vector3(lot.c.x, lot.baseY, lot.c.y);
            if (lot.skyline) ctx.StampSkyline(model, pos, lot.yaw);
            else ctx.Stamp(model, pos, lot.yaw);
            float hc = Mathf.Min(lot.height, 40f);
            ctx.Box(pos + Up * (hc * 0.5f), new Vector3(lot.half.x * 2f, hc, lot.half.y * 2f), lot.yaw);
            if (lot.style != CityStyle.Shophouse)
            {
                // entrance canopy posts on the street face
                Vector2 f = lot.face, side = new Vector2(-f.y, f.x);
                float reach = Mathf.Abs(f.x) > 0.5f ? lot.half.x : lot.half.y;
                float cw = Mathf.Min(7f, (Mathf.Abs(f.x) > 0.5f ? lot.half.y : lot.half.x) * 0.6f);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 p = lot.c + f * (reach + 3.3f) + side * s * (cw - 0.3f);
                    ctx.Capsule(new Vector3(p.x, lot.baseY, p.y), 0.2f, 4.2f);
                }
            }
            foreach (var b in beacons) if (lot.height > 50f) neon.AddBeacon(pos + b);
            foreach (var s in signs)
            {
                var w = s;
                w.pos += pos;
                neon.AddSign(geo, w, rng);
            }
        }

        // ------------------------------------------------------------------ parks, escarpment rocks and pines
        void BuildParks(RegionContext ctx)
        {
            var r = new System.Random(919);
            var trees = new PropModel[6];
            for (int i = 0; i < 6; i++) trees[i] = CityKit.StreetTree(new System.Random(500 + i), i % 4 == 3 ? (i == 3 ? 3 : 0) : i % 4, false);
            var pines = new PropModel[3];
            for (int i = 0; i < 3; i++) pines[i] = NatureShapes.Pine(new System.Random(600 + i), 1.1f + i * 0.15f);
            var blossom = new[] { NatureShapes.BlossomTree(true, new System.Random(71)), NatureShapes.BlossomTree(false, new System.Random(72)) };
            int placed = 0;
            for (int t = 0; t < 1400 && placed < 190; t++)
            {
                Vector2 p = new Vector2(CityKit.R(r, 600f, 1460f), CityKit.R(r, -380f, 372f));
                int zi = CityPlan.ZoneAt(p.x, p.y);
                bool park = zi >= 0 && (CityPlan.Zones[zi].kind == CityZoneKind.Park || CityPlan.Zones[zi].kind == CityZoneKind.Garden);
                float rampW = p.x < 1225f ? CityPlan.RampW(p.y) : CityPlan.RampE(p.y);
                bool hill = zi < 0 && rampW > 0.04f && rampW < 0.96f;
                bool green = park || hill || (zi < 0 && p.x < 726f);
                if (!green) continue;
                if (ctx.EdgeDist(p) < 5f || CityPlan.StreetEdge(p) < 5f || CityPlan.ShoreSD(p.x, p.y) > -12f) continue;
                if (!ctx.Free(p, 5f)) continue;
                ctx.Reserve(p, 4f);
                float gy = ctx.Ground(p);
                PropModel m;
                double q = r.NextDouble();
                if (q < 0.12) m = blossom[r.Next(2)];
                else if (q < 0.22 && p.x < 726f) m = pines[r.Next(3)];
                else m = trees[r.Next(trees.Length)];
                ctx.Stamp(m, new Vector3(p.x, gy - 0.1f, p.y), (float)r.NextDouble() * 360f, CityKit.R(r, 0.9f, 1.35f));
                ctx.Capsule(new Vector3(p.x, gy, p.y), 0.35f, 3f);
                placed++;
            }
            // footpaths and benches in the greenbelt
            var bench = CityKit.Bench();
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = new Vector2(CityKit.R(r, 640f, 718f), CityKit.R(r, -90f, 340f));
                if (ctx.EdgeDist(p) < 3f) continue;
                ctx.Stamp(bench, new Vector3(p.x, ctx.Ground(p), p.y), (float)r.NextDouble() * 360f);
            }
            // escarpment under Frost Summit: lumpy rock outcrops on the steep slopes and Huangshan pines
            var rocks = new PropModel[4];
            for (int i = 0; i < 4; i++)
            {
                rocks[i] = new PropModel();
                var g = rocks[i][PK.Landmark];
                int n = 3 + i;
                for (int k = 0; k < n; k++)
                {
                    Vector3 c = new Vector3(CityKit.R(r, -5f, 5f), CityKit.R(r, 0f, 3f), CityKit.R(r, -5f, 5f));
                    PG.Boulder(g, c, new Vector3(CityKit.R(r, 3f, 6f), CityKit.R(r, 2.5f, 5f), CityKit.R(r, 3f, 6f)), C(178, 174, 162), C(136, 132, 122), k * 3.1f + i);
                }
            }
            int rocksPlaced = 0, pinesPlaced = 0;
            for (float x = 600f; x < 1480f; x += 22f)
                for (float z = 384f; z < 520f; z += 18f)
                {
                    Vector2 p = new Vector2(x + CityKit.R(r, -8f, 8f), z + CityKit.R(r, -6f, 6f));
                    float h0 = ctx.Ground(p), hx = ctx.Ground(p.x + 4f, p.y), hz = ctx.Ground(p.x, p.y + 4f);
                    float slope = Mathf.Sqrt((hx - h0) * (hx - h0) + (hz - h0) * (hz - h0)) / 4f;
                    if (slope > 0.75f && r.NextDouble() < 0.28)
                    {
                        float sc = CityKit.R(r, 0.8f, 1.6f);
                        ctx.Stamp(rocks[r.Next(4)], new Vector3(p.x, h0 - 2f, p.y), (float)r.NextDouble() * 360f, sc);
                        if (z < 430f) ctx.Box(new Vector3(p.x, h0 + 1f, p.y), new Vector3(8f * sc, 6f, 8f * sc), 0f);
                        rocksPlaced++;
                    }
                    else if (slope > 0.25f && r.NextDouble() < 0.35)
                    {
                        ctx.Stamp(pines[r.Next(3)], new Vector3(p.x, h0 - 0.2f, p.y), (float)r.NextDouble() * 360f, CityKit.R(r, 1f, 1.6f));
                        pinesPlaced++;
                    }
                }
        }

        // ------------------------------------------------------------------ fountain spray (one particle system)
        void BuildSpray(RegionContext ctx)
        {
            jetPos = fountainJets.ToArray();
            jetVel = new Vector4[jetPos.Length];
            // centre jets go straight up, ring jets arc inwards, the seafront row dances
            for (int i = 0; i < jetPos.Length; i++)
            {
                Vector3 p = jetPos[i];
                if (p.y < CityPlan.Low + 1f) { jetVel[i] = new Vector4(0f, 6f, 0f, 1f); continue; }
                // nearest centre jet (the one 2.4 m higher in the same fountain)
                Vector3 c = p;
                for (int j = 0; j < jetPos.Length; j++)
                    if (jetPos[j].y > p.y + 1f && (new Vector2(jetPos[j].x - p.x, jetPos[j].z - p.z)).sqrMagnitude < 150f) c = jetPos[j];
                if (c == p) jetVel[i] = new Vector4(0f, 7f, 0f, 0f);
                else
                {
                    Vector3 d = new Vector3(c.x - p.x, 0f, c.z - p.z).normalized;
                    jetVel[i] = new Vector4(d.x * 1.6f, 4.6f, d.z * 1.6f, 0f);
                }
            }
            var go = new GameObject("City_FountainSpray");
            go.layer = WorldBuilder.LayerTrees;
            go.transform.SetParent(ctx.Root, false);
            spray = go.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 1.3f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1800;
            main.gravityModifier = 0.9f;
            main.playOnAwake = false;
            var em = spray.emission;
            em.enabled = false;
            var sh = spray.shape;
            sh.enabled = false;
            var col = spray.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var sz = spray.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.4f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = MaterialLibrary.Particle("CityFountain", ProceduralTextures.Soft, new Color(0.9f, 0.97f, 1f, 0.75f), 0.8f);
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.lightProbeUsage = LightProbeUsage.Off;
            pr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            spray.Play();
        }

        void TickSpray(float dt, Vector3 eye)
        {
            if (spray == null || jetPos == null) return;
            if (spray.particleCount > 1700) return;
            for (int i = 0; i < jetPos.Length; i++)
            {
                Vector3 p = jetPos[i];
                float dx = p.x - eye.x, dz = p.z - eye.z;
                if (dx * dx + dz * dz > 170f * 170f) continue;
                Vector4 v = jetVel[i];
                float rate = v.w > 0.5f ? 22f : (v.x == 0f && v.z == 0f ? 34f : 14f);
                float want = rate * dt + (float)rng.NextDouble();
                int n = Mathf.Min(3, Mathf.FloorToInt(want));
                float k = 1f;
                if (v.w > 0.5f) k = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(tickT * 1.3f + i * 0.7f));
                for (int j = 0; j < n; j++)
                {
                    ep = new ParticleSystem.EmitParams();
                    ep.position = p;
                    ep.velocity = new Vector3(v.x + (float)(rng.NextDouble() - 0.5) * 0.6f, v.y * k * (0.9f + 0.2f * (float)rng.NextDouble()), v.z + (float)(rng.NextDouble() - 0.5) * 0.6f);
                    ep.startSize = 0.3f + 0.25f * (float)rng.NextDouble();
                    ep.startLifetime = 1.1f + 0.4f * (float)rng.NextDouble();
                    ep.rotation = (float)rng.NextDouble() * 360f;
                    spray.Emit(ep, 1);
                }
            }
        }

        // ------------------------------------------------------------------ per frame
        public override void Tick(float dt, Vector3 eye, float presence)
        {
            tickT += dt;
            float night = Atmosphere.NightFactor;
            if (neon != null) neon.Tick(tickT, night);
            TickBoats(tickT, dt);
            TickLighthouse(tickT, night);
            glowT -= dt;
            if (glowT <= 0f)
            {
                glowT = 0.5f;
                if (shipGlowMat != null) shipGlowMat.SetFloat("_Glow", 0.3f + 2.3f * night);
            }
            TickSpray(dt, eye);
        }
    }
}

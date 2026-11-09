// RallyStages.cs - the furniture of every rally stage (4 time attacks + the drift zone, which shares
// the Pagoda Climb course): a red-carpet start ramp with an inflatable red arch, flags and a 5-lamp
// start light that counts down with the event, and a truss finish gantry with a chequered banner and
// a live LED timing board (7-segment vertex-colour mesh: running time during the event, the stage
// record otherwise). Arch feet and gantry towers have colliders; everything else is decoration.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyStages : RallyModule
    {
        sealed class End
        {
            public RallyStageEnds st;
            public LedBoard board;
            public GlowMesh boardGlow, lights;
            public int[] lamp = new int[5];
            public Vector3 boardPos, gantryTop;
            public string recordKey;
        }

        readonly List<End> ends = new List<End>();
        Material ledMat;
        static readonly Color32 LampOff = new Color32(30, 12, 12, 255), LampRed = new Color32(255, 40, 30, 255), LampGreen = new Color32(60, 255, 90, 255);

        public int Count { get { return ends.Count; } }
        public override string Summary { get { return ends.Count + " start/finish sets"; } }

        public override void Build()
        {
            ledMat = new Material(MaterialLibrary.FindShader("BlossomRally/VertexColorGlow")) { name = "BR_RallyLed" };
            ledMat.SetFloat("_Glow", 1.7f);
            foreach (var st in L.Track.stages)
            {
                var e = new End { st = st, recordKey = "Stage_" + st.def.key };
                BuildStart(e);
                BuildFinish(e);
                ends.Add(e);
            }
        }

        // ---------------------------------------------------------------- start
        void BuildStart(End e)
        {
            var c = e.st.course;
            var S = L.Static;
            // red carpet "ramp" under the spawn point (5 cm deck with bevelled ends, no collider)
            int i0 = c.IndexAt(c.startD - 13f), i1 = c.IndexAt(c.startD - 1.5f);
            var carpet = new LifeGeo();
            Color32 red = LifeGeo.C(186, 28, 36), gold = LifeGeo.C(232, 186, 70), redD = LifeGeo.C(140, 20, 28);
            int n = c.samples.Count;
            int steps = ((i1 - i0) % n + n) % n;
            for (int k = 0; k < steps; k++)
            {
                var a = c.samples[(i0 + k) % n];
                var b = c.samples[(i0 + k + 1) % n];
                float w = Mathf.Min(a.halfWidth * 0.62f, 3.2f);
                float ya = a.height + 0.05f, yb = b.height + 0.05f;
                Vector2 ra = RallyTrack.Right(a), rb = RallyTrack.Right(b);
                Vector3 al = V(a.pos - ra * w, ya), ar = V(a.pos + ra * w, ya), bl = V(b.pos - rb * w, yb), br = V(b.pos + rb * w, yb);
                carpet.QuadOut(al, bl, br, ar, Vector3.up, red, red, red, red);
                // gold edge strips + side skirts
                Vector3 alo = V(a.pos - ra * (w + 0.12f), ya), aro = V(a.pos + ra * (w + 0.12f), ya), blo = V(b.pos - rb * (w + 0.12f), yb), bro = V(b.pos + rb * (w + 0.12f), yb);
                carpet.QuadOut(alo, blo, bl, al, Vector3.up, gold, gold, gold, gold);
                carpet.QuadOut(ar, br, bro, aro, Vector3.up, gold, gold, gold, gold);
                carpet.QuadOut(alo, alo - Vector3.up * 0.12f, blo - Vector3.up * 0.12f, blo, V(-ra, 0f), redD, redD, redD, redD);
                carpet.QuadOut(aro, bro, bro - Vector3.up * 0.12f, aro - Vector3.up * 0.12f, V(ra, 0f), redD, redD, redD, redD);
                if (k == 0 || k == steps - 1)
                {
                    // bevel down to the road
                    var s = k == 0 ? a : b;
                    Vector2 rr = RallyTrack.Right(s);
                    float sign = k == 0 ? -1f : 1f;
                    Vector2 off = s.tangent * (0.5f * sign);
                    float y = s.height + 0.05f;
                    Vector3 p0 = V(s.pos - rr * (w + 0.12f), y), p1 = V(s.pos + rr * (w + 0.12f), y);
                    Vector3 q0 = V(s.pos - rr * (w + 0.12f) + off, s.height - 0.03f), q1 = V(s.pos + rr * (w + 0.12f) + off, s.height - 0.03f);
                    carpet.QuadOut(p0, p1, q1, q0, Vector3.up, redD, redD, redD, redD);
                }
            }
            S.Add(carpet, Matrix4x4.identity);
            // white start line just before the gate
            {
                var g0 = c.gates[0];
                var line = new LifeGeo();
                Color32 white = LifeGeo.C(244, 244, 240);
                Vector3 ctr = g0.centre + Vector3.up * 0.02f - g0.fwd * 0.4f;
                RallyGeo.OBox(line, ctr, g0.right, Vector3.up, g0.fwd, new Vector3(g0.halfWidth - 0.2f, 0.02f, 0.18f), white);
                S.Add(line, Matrix4x4.identity);
            }

            // inflatable arch over the carpet
            int ia = c.IndexAt(c.startD - 9f);
            var rs = c.samples[ia];
            Vector3 ctrA = V(rs.pos, rs.height);
            Vector3 fwd = V(rs.tangent, 0f).normalized, right = V(RallyTrack.Right(rs), 0f);
            float A = rs.halfWidth + 2.2f, H = 6.4f, tube = 0.48f;
            var arch = new LifeGeo();
            Color32 archRed = LifeGeo.C(214, 36, 44), archWhite = LifeGeo.C(246, 244, 240), archPink = LifeGeo.C(244, 150, 180);
            int segs = 16;
            Vector3 prev = Vector3.zero;
            for (int k = 0; k <= segs; k++)
            {
                float th = Mathf.PI * k / segs;
                Vector3 p = ctrA + right * (A * Mathf.Cos(th)) + Vector3.up * (H * Mathf.Sin(th) + tube * 0.6f);
                if (k > 0)
                {
                    Color32 col = (k == 4 || k == 12) ? archWhite : (k == 8 ? archPink : archRed);
                    arch.Frustum(prev, p, tube, tube, tube, tube, 8, 0f, col, col, false, false);
                    arch.Ellipsoid(p, new Vector3(tube, tube, tube), 8, 3, col, col);
                }
                prev = p;
            }
            // feet down to the ground + sandbag bases
            Color32 sand = LifeGeo.C(200, 176, 120);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = ctrA + right * (A * s);
                float gy = L.Ground(foot.x, foot.z);
                Vector3 top = foot + Vector3.up * (tube * 0.6f);
                Vector3 bottom = new Vector3(foot.x, Mathf.Min(gy, top.y) - 0.1f, foot.z);
                arch.Frustum(bottom, top, tube * 1.05f, tube * 1.05f, tube, tube, 8, 0f, archRed, archRed, true, false);
                RallyGeo.OBox(arch, new Vector3(foot.x, gy + 0.15f, foot.z) - fwd * 0.75f, right, Vector3.up, fwd, new Vector3(0.35f, 0.15f, 0.22f), sand);
                RallyGeo.OBox(arch, new Vector3(foot.x, gy + 0.15f, foot.z) + fwd * 0.75f, right, Vector3.up, fwd, new Vector3(0.35f, 0.15f, 0.22f), sand);
                // guy ropes
                RallyGeo.Beam(arch, foot + Vector3.up * (H * 0.55f), new Vector3(foot.x, gy, foot.z) + right * (s * 1.6f) - fwd * 1.4f, 0.012f, LifeGeo.C(230, 230, 220));
                RallyGeo.Beam(arch, foot + Vector3.up * (H * 0.55f), new Vector3(foot.x, gy, foot.z) + right * (s * 1.6f) + fwd * 1.4f, 0.012f, LifeGeo.C(230, 230, 220));
                Collider(foot + Vector3.up * 1.2f, right, fwd, new Vector3(tube * 1.6f, 2.4f, tube * 1.6f), "ArchFoot");
                PlacementRegistry.Reserve(new Vector2(foot.x, foot.z), 1.2f);
            }
            // "START" panel hanging under the apex: chequered band + pink panel
            Vector3 apex = ctrA + Vector3.up * (H + tube * 0.6f - tube);
            Vector3 pc = apex - Vector3.up * 0.6f;
            RallyGeo.OBox(arch, pc, right, Vector3.up, fwd, new Vector3(2.4f, 0.42f, 0.05f), archPink);
            Checker(arch, pc - fwd * 0.06f, right, Vector3.up, -fwd, 2.3f, 0.3f, 12, 2);
            Checker(arch, pc + fwd * 0.06f, right, Vector3.up, fwd, 2.3f, 0.3f, 12, 2);
            S.Add(arch, Matrix4x4.identity);

            // flags along both sides behind the arch
            Color32[] fc = { LifeGeo.C(226, 40, 50), LifeGeo.C(246, 196, 70), LifeGeo.C(240, 140, 170), LifeGeo.C(250, 250, 246) };
            int fi = 0;
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    int idx = c.IndexAt(c.startD - 13f - k * 3.2f);
                    var q = c.samples[idx];
                    Vector2 p = q.pos + RallyTrack.Right(q) * (s * (q.halfWidth + 3.4f));
                    if (!PlacementRegistry.IsFree(p, 0.4f) || L.EdgeDist(p) < 2.5f) continue;
                    PlacementRegistry.Reserve(p, 0.5f);
                    var flag = RallyShapes.FlagPole(fc[fi % 4], fc[(fi + 1) % 4], 5.2f + (k % 2) * 0.6f);
                    fi++;
                    S.Add(flag, L.Ground(p), Quaternion.Euler(0f, R(0f, 360f), 0f));
                }
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 p = rs.pos + RallyTrack.Right(rs) * (s * (rs.halfWidth + 4.2f)) + rs.tangent * 1.5f;
                if (!PlacementRegistry.IsFree(p, 0.4f) || L.EdgeDist(p) < 3f) continue;
                PlacementRegistry.Reserve(p, 0.5f);
                S.Add(RallyShapes.FeatherFlag(LifeGeo.C(236, 120, 160), LifeGeo.C(250, 230, 120), 4.2f), L.Ground(p), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

            // start lights on a post on the right, just before the line, facing the waiting car
            {
                int il = c.IndexAt(c.startD - 3f);
                var q = c.samples[il];
                Vector2 p = q.pos + RallyTrack.Right(q) * (q.halfWidth + 1.9f);
                Vector3 baseP = L.Ground(p);
                Vector3 f = V(q.tangent, 0f).normalized, r = V(RallyTrack.Right(q), 0f);
                var post = new LifeGeo();
                Color32 dark = LifeGeo.C(36, 36, 40), grey = LifeGeo.C(140, 140, 146);
                RallyGeo.Beam(post, baseP - Vector3.up * 0.3f, baseP + Vector3.up * 2.4f, 0.06f, grey);
                Vector3 boxC = baseP + Vector3.up * 2.75f - r * 0.9f;
                RallyGeo.OBox(post, boxC, r, Vector3.up, f, new Vector3(1.15f, 0.3f, 0.12f), dark);
                RallyGeo.Beam(post, baseP + Vector3.up * 2.4f, boxC + r * 1.0f, 0.04f, grey);
                S.Add(post, Matrix4x4.identity);
                PlacementRegistry.Reserve(p, 0.6f);
                Collider(baseP + Vector3.up * 1.2f, r, f, new Vector3(0.2f, 2.4f, 0.2f), "StartLightPost");
                e.lights = new GlowMesh();
                for (int k = 0; k < 5; k++)
                {
                    e.lamp[k] = e.lights.Begin();
                    Vector3 lc = boxC + r * (-0.88f + k * 0.44f) - f * 0.125f;
                    e.lights.geo.Disc(lc, 0.15f, 8, 0f, -f, LampOff, LampOff);
                    e.lights.End(e.lamp[k]);
                }
                e.lights.Finish("StartLights_" + e.st.def.key, Root, ledMat);
            }
        }

        // ---------------------------------------------------------------- finish
        void BuildFinish(End e)
        {
            var c = e.st.course;
            float d = c.closed ? c.startD + 15f : Mathf.Min(c.endD + 10f, c.roadLength - 3f);
            int idx = c.IndexAt(d);
            var rs = c.samples[idx];
            Vector3 ctr = V(rs.pos, rs.height);
            Vector3 fwd = V(rs.tangent, 0f).normalized, right = V(RallyTrack.Right(rs), 0f);
            float half = rs.halfWidth + 2.3f, top = 6.6f;
            var g = new LifeGeo();
            Color32 truss = LifeGeo.C(196, 198, 206), trussD = LifeGeo.C(150, 152, 160), dark = LifeGeo.C(30, 30, 36), pink = LifeGeo.C(236, 120, 160);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 baseC = ctr + right * (half * s);
                float gy = L.Ground(baseC.x, baseC.z);
                Vector3 b0 = new Vector3(baseC.x, Mathf.Min(gy, ctr.y) - 0.2f, baseC.z);
                // 4-post tower with zig-zag bracing
                Vector3[] corner = { -right * 0.3f - fwd * 0.3f, right * 0.3f - fwd * 0.3f, right * 0.3f + fwd * 0.3f, -right * 0.3f + fwd * 0.3f };
                float y0 = b0.y, y1 = ctr.y + top;
                for (int k = 0; k < 4; k++) RallyGeo.Beam(g, new Vector3(b0.x, y0, b0.z) + corner[k], new Vector3(b0.x, y1, b0.z) + corner[k], 0.035f, truss);
                int rungs = Mathf.Max(3, Mathf.RoundToInt((y1 - y0) / 0.6f));
                for (int k = 0; k < rungs; k++)
                {
                    float ya = Mathf.Lerp(y0, y1, k / (float)rungs), yb = Mathf.Lerp(y0, y1, (k + 1) / (float)rungs);
                    for (int f = 0; f < 4; f++)
                    {
                        Vector3 pa = new Vector3(b0.x, ya, b0.z) + corner[f], pb = new Vector3(b0.x, yb, b0.z) + corner[(f + 1) % 4];
                        RallyGeo.Beam(g, pa, pb, 0.015f, trussD);
                    }
                }
                RallyGeo.OBox(g, new Vector3(b0.x, gy + 0.1f, b0.z), right, Vector3.up, fwd, new Vector3(0.5f, 0.1f, 0.5f), dark);
                Collider(new Vector3(baseC.x, (y0 + y1) * 0.5f, baseC.z), right, fwd, new Vector3(0.7f, y1 - y0, 0.7f), "GantryTower");
                PlacementRegistry.Reserve(new Vector2(baseC.x, baseC.z), 1.3f);
            }
            // top truss beam (box section)
            float yTop = ctr.y + top;
            Vector3 l = ctr - right * half, r = ctr + right * half;
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    Vector3 off = Vector3.up * (yTop - ctr.y - 0.75f + a * 0.7f) + fwd * (b == 0 ? -0.3f : 0.3f);
                    RallyGeo.Beam(g, l + off, r + off, 0.04f, truss);
                }
            int bays = Mathf.RoundToInt(half * 2f / 0.8f);
            for (int k = 0; k < bays; k++)
            {
                Vector3 pa = Vector3.Lerp(l, r, k / (float)bays), pb = Vector3.Lerp(l, r, (k + 1) / (float)bays);
                for (int b = 0; b < 2; b++)
                {
                    Vector3 fo = fwd * (b == 0 ? -0.3f : 0.3f);
                    RallyGeo.Beam(g, pa + fo + Vector3.up * (top - 0.75f), pb + fo + Vector3.up * (top - 0.05f), 0.015f, trussD);
                }
            }
            // chequered banner on both faces under the beam
            Vector3 bc = ctr + Vector3.up * (top - 1.35f);
            RallyGeo.OBox(g, bc, right, Vector3.up, fwd, new Vector3(half - 0.5f, 0.5f, 0.04f), pink);
            Checker(g, bc - fwd * 0.05f, right, Vector3.up, -fwd, half - 0.6f, 0.4f, 24, 2);
            Checker(g, bc + fwd * 0.05f, right, Vector3.up, fwd, half - 0.6f, 0.4f, 24, 2);
            // timing board box on top, facing the approaching car (-fwd)
            Vector3 boardC = ctr + Vector3.up * (top + 0.75f);
            RallyGeo.OBox(g, boardC, right, Vector3.up, fwd, new Vector3(2.2f, 0.62f, 0.18f), dark);
            RallyGeo.OBox(g, boardC + Vector3.up * 0.68f, right, Vector3.up, fwd, new Vector3(2.25f, 0.07f, 0.2f), pink);
            for (int s = -1; s <= 1; s += 2) RallyGeo.Beam(g, boardC + right * (1.6f * s) - Vector3.up * 0.6f, boardC + right * (1.6f * s) - Vector3.up * 0.95f, 0.05f, truss);
            L.Static.Add(g, Matrix4x4.identity);
            e.boardPos = boardC;
            e.gantryTop = ctr + Vector3.up * (top + 2f);
            e.boardGlow = new GlowMesh();
            // the driver looks along +fwd, so the digits read along +right on the face pointing at -fwd
            e.board = new LedBoard(e.boardGlow, boardC - fwd * 0.19f, right, Vector3.up, -fwd, 0.72f);
            e.boardGlow.Finish("TimingBoard_" + e.st.def.key, Root, ledMat);
            e.board.ShowTime(BestOf(e), 0);
            e.boardGlow.Upload();
        }

        static Vector3 V(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }

        /// <summary>Two-row chequered band of 'cols' squares centred on c (half width hw, half height hh).</summary>
        static void Checker(LifeGeo g, Vector3 c, Vector3 right, Vector3 up, Vector3 normal, float hw, float hh, int cols, int rows)
        {
            Color32 ink = LifeGeo.C(24, 24, 28), white = LifeGeo.C(246, 246, 240);
            float cw = hw * 2f / cols, ch = hh * 2f / rows;
            Vector3 o = c - right * hw - up * hh;
            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                {
                    Color32 col = ((x + y) & 1) == 0 ? ink : white;
                    Vector3 a = o + right * (x * cw) + up * (y * ch);
                    g.QuadOut(a, a + right * cw, a + right * cw + up * ch, a + up * ch, normal, col, col, col, col);
                }
        }

        void Collider(Vector3 centre, Vector3 right, Vector3 fwd, Vector3 size, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(fwd, Vector3.up));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }

        float BestOf(End e)
        {
            var def = e.st.def;
            if (def.kind != StageKind.TimeAttack) return -1f;
            float b = GameSettings.BestTime(e.recordKey);
            return b > 0f ? b : -1f;
        }

        bool Owns(End e, int ev)
        {
            if (ev < 0) return false;
            if (e.st.defIndex == ev) return true;
            return e.st.sharedWith.Contains(ev);
        }

        /// <summary>World point above the finish gantry of an event (for the medal fireworks).</summary>
        public bool FinishPoint(int ev, out Vector3 p)
        {
            foreach (var e in ends)
                if (Owns(e, ev)) { p = e.gantryTop; return true; }
            p = Vector3.zero;
            return false;
        }

        public override void Tick(float dt)
        {
            var sw = L.Stage;
            for (int i = 0; i < ends.Count; i++)
            {
                var e = ends[i];
                bool mine = sw.Phase != StageWatch.Idle && Owns(e, sw.Event);
                // timing board
                if (L.Due(e.boardPos, i, 120f, 260f, 400f) || mine)
                {
                    if (!mine) e.board.ShowTime(BestOf(e), 0);
                    else if (sw.Phase == StageWatch.Countdown) e.board.ShowTime(0f, 0);
                    else if (sw.Phase == StageWatch.Run) e.board.ShowTime(sw.Time, 0);
                    else e.board.ShowTime(sw.FinalTime >= 0f ? sw.FinalTime : sw.Time, Mathf.Repeat(L.Clock, 1f) < 0.65f ? 1 : 0);
                    e.boardGlow.Upload();
                }
                // start lights: red lamps fill during the countdown, all green on "GO"
                int reds = 0;
                bool green = false;
                if (mine && sw.Phase == StageWatch.Countdown) reds = Mathf.Clamp(Mathf.FloorToInt((3.8f - sw.CountdownLeft) / 0.7f) + 1, 0, 5);
                if (mine && sw.Phase == StageWatch.Run && sw.Time < 2.5f) green = true;
                for (int k = 0; k < 5; k++) e.lights.Set(e.lamp[k], green ? LampGreen : (k < reds ? LampRed : LampOff));
                e.lights.Upload();
            }
        }
    }
}

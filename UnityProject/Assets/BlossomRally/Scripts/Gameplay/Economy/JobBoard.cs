// JobBoard.cs - the work menu: GameplayHooks.OpenJobBoard(id) from tea houses / harbour / taxi rank / lodge
// boards (places module), the roadside job markers (EconomyWorld), and the "Blossom Jobs" phone (J, or the
// pause menu's "Jobs nearby" on a gamepad) that
// lists every job in the current region. Left: the jobs on offer (kind tag, title, destination, pay, time);
// right: the selected job's story, route and estimate, Accept. Keyboard, gamepad and mouse.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class JobBoard : MonoBehaviour
    {
        public static JobBoard Instance { get; private set; }
        public bool IsOpen { get { return EcoMenus.Current == (object)this; } }

        struct Row
        {
            public JobDef job;
            public int payV;
            public string forClient, pay, info, route, limit, dest;
        }

        readonly List<Row> rows = new List<Row>();
        string boardId = "", title = "", subtitle = "", stats = "", currentJob = "";
        bool phone;
        int sel;               // -1 = "cancel current job" row
        float openT, scroll;
        RegionId region;
        string emptyText = "";

        void Awake() { Instance = this; }

        // ------------------------------------------------------------------ open / close
        public void Open(string id, bool asPhone)
        {
            var sm = StageManager.Instance;
            if (sm != null && sm.Running) { EconomyDirector.Toast("Finish the event first"); return; }
            if (!EcoMenus.TryOpen(this)) return;
            boardId = string.IsNullOrEmpty(id) ? "jobs" : id;
            phone = asPhone;
            openT = Time.unscaledTime;
            JobData.Resolve(WorldHooks.World);
            JobData.Ensure();
            Vector3 me = PlayerState.Position;
            region = WorldLayout.At(new Vector2(me.x, me.z));
            title = JobData.BoardTitle(boardId, region);
            subtitle = MapMarkers.RegionName(region) + (phone ? "   ·   jobs nearby" : "");
            var d = EconomySave.Data;
            stats = d.jobsDone + (d.jobsDone == 1 ? " job done" : " jobs done") + "   ·   earned " + Wallet.Format(d.jobsEarned);
            Rebuild(me);
            sel = JobSystem.HasJob ? -1 : 0;
            scroll = 0f;
            UI.Sound("MenuSelect");
        }

        public void Close()
        {
            if (!IsOpen) return;
            EcoMenus.Close(this);
            UI.Sound("MenuBack");
        }

        void Rebuild(Vector3 me)
        {
            rows.Clear();
            var list = JobData.Jobs;
            for (int i = 0; i < list.Count; i++)
            {
                var j = list[i];
                if (j.region != region || !JobData.BoardOffers(boardId, j.kind) || JobSystem.Resting(j)) continue;
                rows.Add(MakeRow(j, me));
            }
            // board specialities first, then by pay
            rows.Sort((a, b) =>
            {
                int pa = Priority(a.job.kind), pb = Priority(b.job.kind);
                if (pa != pb) return pa.CompareTo(pb);
                return b.payV.CompareTo(a.payV);
            });
            if (rows.Count > 9) rows.RemoveRange(9, rows.Count - 9);
            emptyText = rows.Count == 0 ? "No work here right now - come back in a few minutes." : "";
            currentJob = JobSystem.HasJob ? "Current job:  " + JobSystem.Instance.Job.title : "";
        }

        int Priority(JobKind k)
        {
            string id = boardId;
            System.StringComparison o = System.StringComparison.OrdinalIgnoreCase;
            if (k == JobKind.Tea && id.IndexOf("tea", o) >= 0) return 0;
            if (k == JobKind.Taxi && (id.IndexOf("taxi", o) >= 0 || id.IndexOf("harbour", o) >= 0)) return 0;
            if (k == JobKind.Snow && id.IndexOf("lodge", o) >= 0) return 0;
            if (k == JobKind.Desert && (id.IndexOf("oasis", o) >= 0 || id.IndexOf("caravan", o) >= 0)) return 0;
            return 1 + (int)k;
        }

        Row MakeRow(JobDef j, Vector3 me)
        {
            bool skipPickup = !phone || j.kind == JobKind.Photo || Flat(me, j.from.pos) < 160f;
            Vector3 start = skipPickup ? me : j.from.pos;
            int pay; float limit, route;
            JobSystem.Estimate(j, start, out pay, out limit, out route);
            var r = new Row { job = j, payV = pay, forClient = "for " + j.client };
            r.pay = Wallet.Format(pay) + (j.kind == JobKind.Taxi ? " + tip" : "");
            r.dest = (j.via != null ? "via " + j.via[0].name + "  →  " : "→  ") + j.to.name;
            string km = (route / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km";
            r.limit = limit > 0f ? Mathf.FloorToInt(limit / 60f) + ":" + (Mathf.RoundToInt(limit) % 60).ToString("00") : "no rush";
            r.info = km + "   ·   " + r.limit;
            r.route = skipPickup ? (j.kind == JobKind.Photo ? "Head to " + j.to.name + " and take the shot" : "Starts here")
                                 : "Pick up at " + j.from.name + "  (" + (Flat(me, j.from.pos) / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km away)";
            return r;
        }

        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // ------------------------------------------------------------------ input
        void Update()
        {
            var gm = GameManager.Instance;
            if (!IsOpen)
            {
                // the "Blossom Jobs" phone
                if (gm != null && gm.IsDriving && !GameplayHooks.MenuOpen && !EcoMenus.IsOpen && !PhotoMode.BlockingInput && !ReplaySystem.BlockingInput
                    && InputReader.Down(KeyCode.J))
                    Open("phone", true);
                return;
            }
            if (EcoMenus.Blocked) { EcoMenus.Close(this); return; }
            if (!EcoMenus.InputFor(this)) return;
            int min = JobSystem.HasJob ? -1 : 0;
            int max = rows.Count - 1;
            if (InputReader.MenuUp) { sel = Mathf.Max(min, sel - 1); UI.Sound("MenuMove"); }
            if (InputReader.MenuDown) { sel = Mathf.Min(Mathf.Max(min, max), sel + 1); UI.Sound("MenuMove"); }
            if (InputReader.MenuConfirm) Activate();
            else if (InputReader.MenuBack || InputReader.Down(KeyCode.J)) Close();
        }

        void Activate()
        {
            if (sel < 0)
            {
                if (JobSystem.Instance != null) JobSystem.Instance.Abandon();
                Rebuild(PlayerState.Position);
                sel = 0;
                return;
            }
            if (sel >= rows.Count) return;
            if (JobSystem.HasJob) { UI.Sound("MenuBack"); EconomyDirector.Toast("Finish or cancel your current job first"); return; }
            var j = rows[sel].job;
            EcoMenus.Close(this);
            if (JobSystem.Instance != null) JobSystem.Instance.Accept(j, !phone);
        }

        // ------------------------------------------------------------------ drawing
        void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -10;
            EcoGui.Styles();
            float s = EcoGui.S, W = Screen.width, H = Screen.height;
            float a = MenuArt.EaseOut((Time.unscaledTime - openT) / 0.3f);
            EcoGui.Dim(a);
            float pw = Mathf.Min(W - 40f * s, 1180f * s), ph = Mathf.Min(H - 60f * s, 700f * s);
            Rect panel = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f + (1f - a) * 30f * s, pw, ph);
            MenuArt.Card(panel, a);
            MenuArt.PaperFill(new Rect(panel.x, panel.y + 3f * s, panel.width, 84f * s), new Color(1f, 0.9f, 0.94f, 0.06f * a));
            // header
            MenuArt.Text(new Rect(panel.x + 30f * s, panel.y + 14f * s, pw * 0.6f, 44f * s), title, MenuArt.Big, MenuArt.A(UI.Pink, a));
            MenuArt.Text(new Rect(panel.x + 30f * s, panel.y + 54f * s, pw * 0.6f, 24f * s), subtitle, MenuArt.Small, MenuArt.A(UI.White, 0.8f * a));
            MenuArt.Text(new Rect(panel.x, panel.y + 20f * s, pw - 30f * s, 24f * s), stats, MenuArt.SmallR, MenuArt.A(UI.Gold, a));
            MenuArt.Fill(new Rect(panel.x + 30f * s, panel.y + 88f * s, pw - 60f * s, 1f), MenuArt.A(UI.White, 0.18f * a));

            float listX = panel.x + 24f * s, listY = panel.y + 100f * s, listW = pw * 0.52f, listH = ph - 170f * s;
            float rowH = 66f * s, y = listY;
            if (JobSystem.HasJob)
            {
                Rect cr = new Rect(listX, y, listW, 46f * s);
                bool cs = sel == -1;
                MenuArt.Fill(cr, cs ? MenuArt.A(EcoGui.Coral, 0.9f * a) : new Color(1f, 1f, 1f, 0.06f * a));
                MenuArt.Text(new Rect(cr.x + 14f * s, cr.y, cr.width * 0.65f, cr.height), currentJob, MenuArt.Body, MenuArt.A(cs ? MenuArt.PlumDeep : UI.White, a), !cs);
                MenuArt.Text(new Rect(cr.x, cr.y, cr.width - 14f * s, cr.height), "Cancel job", MenuArt.BodyR, MenuArt.A(cs ? MenuArt.PlumDeep : EcoGui.Coral, a), !cs);
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) { if (sel == -1) Activate(); else { sel = -1; UI.Sound("MenuMove"); } }
                y += 56f * s;
            }
            if (rows.Count == 0)
                MenuArt.Text(new Rect(listX, y + 20f * s, listW, 60f * s), emptyText, MenuArt.Body, MenuArt.A(UI.White, 0.7f * a));
            // keep the selection in view
            float visible = listY + listH - y;
            if (sel >= 0)
            {
                float top = sel * rowH, bottom = top + rowH;
                if (top < scroll) scroll = top;
                if (bottom > scroll + visible) scroll = bottom - visible;
            }
            GUI.BeginClip(new Rect(listX, y, listW, visible));
            for (int i = 0; i < rows.Count; i++)
            {
                Rect r = new Rect(0f, i * rowH - scroll, listW, rowH - 8f * s);
                if (r.yMax < 0f || r.y > visible) continue;
                DrawRow(r, i, i == sel, a, s);
            }
            GUI.EndClip();

            // details
            Rect det = new Rect(panel.x + pw * 0.52f + 44f * s, listY, pw * 0.48f - 68f * s, listH);
            if (sel >= 0 && sel < rows.Count) DrawDetails(det, rows[sel], a, s);
            else if (sel == -1 && JobSystem.HasJob)
            {
                MenuArt.Text(new Rect(det.x, det.y, det.width, 40f * s), "Cancel the current job?", MenuArt.MidL, MenuArt.A(UI.White, a));
                MenuArt.Text(new Rect(det.x, det.y + 46f * s, det.width, 80f * s), "No hard feelings - the cargo goes back and you can take another job straight away.", MenuArt.Body, MenuArt.A(UI.White, 0.75f * a));
            }

            // footer chips
            bool pad = InputReader.PadConnected, c1, c2;
            float fx = panel.x + 30f * s, fy = panel.yMax - 50f * s;
            bool c0;
            fx += EcoGui.Chip(fx, fy, pad ? "D-PAD" : "↑ ↓", "choose", out c0);
            fx += EcoGui.Chip(fx, fy, pad ? "A" : "ENTER", sel == -1 ? "cancel job" : "accept", out c1);
            EcoGui.Chip(fx, fy, pad ? "B" : "ESC", "leave", out c2);
            if (c1) Activate();
            if (c2) Close();
        }

        void DrawRow(Rect r, int idx, bool selected, float a, float s)
        {
            Row row = rows[idx];
            var j = row.job;
            Color kc = JobData.KindColours[(int)j.kind];
            MenuArt.Fill(r, selected ? new Color(1f, 1f, 1f, 0.14f * a) : new Color(1f, 1f, 1f, 0.05f * a));
            MenuArt.Fill(new Rect(r.x, r.y, Mathf.Max(3f, 4f * s), r.height), MenuArt.A(kc, (selected ? 1f : 0.6f) * a));
            if (selected) MenuArt.Outline(r, Mathf.Max(1f, 2f * s), MenuArt.A(UI.Pink, 0.9f * a));
            string tag = JobData.KindTags[(int)j.kind];
            EcoGui.Tag(new Rect(r.x + 14f * s, r.y + 9f * s, EcoGui.Width(MenuArt.TinyC, tag) + 12f * s, 18f * s), tag, kc, a);
            MenuArt.Text(new Rect(r.x + 14f * s, r.y + 28f * s, r.width * 0.62f, 28f * s), j.title, MenuArt.MidL, MenuArt.A(UI.White, a));
            MenuArt.Text(new Rect(r.x + r.width * 0.30f, r.y + 6f * s, r.width * 0.68f, 22f * s), row.dest, MenuArt.SmallR, MenuArt.A(UI.White, 0.7f * a));
            MenuArt.Text(new Rect(r.x, r.y + 28f * s, r.width - 14f * s, 28f * s), row.pay, MenuArt.MidR, MenuArt.A(UI.Gold, a));
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (selected) Activate();
                else { sel = idx; UI.Sound("MenuMove"); }
            }
        }

        void DrawDetails(Rect d, Row row, float a, float s)
        {
            var j = row.job;
            Color kc = JobData.KindColours[(int)j.kind];
            float y = d.y;
            string kn = JobData.KindNames[(int)j.kind];
            EcoGui.Tag(new Rect(d.x, y, EcoGui.Width(MenuArt.TinyC, kn) + 16f * s, 22f * s), kn, kc, a);
            y += 30f * s;
            MenuArt.Text(new Rect(d.x, y, d.width, 40f * s), j.title, MenuArt.Big, MenuArt.A(UI.White, a));
            y += 40f * s;
            MenuArt.Text(new Rect(d.x, y, d.width, 22f * s), row.forClient, MenuArt.Small, MenuArt.A(UI.Pink, a));
            y += 30f * s;
            MenuArt.Text(new Rect(d.x, y, d.width, 96f * s), j.flavour, MenuArt.Body, MenuArt.A(UI.White, 0.9f * a));
            y += 104f * s;
            MenuArt.Fill(new Rect(d.x, y, d.width, 1f), MenuArt.A(UI.White, 0.18f * a));
            y += 10f * s;
            Field(d.x, ref y, d.width, "Route", row.route, a, s);
            Field(d.x, ref y, d.width, "Destination", row.dest, a, s);
            Field(d.x, ref y, d.width, "Distance · time", row.info, a, s);
            Field(d.x, ref y, d.width, "Pays", row.pay, a, s, true);
            y += 6f * s;
            string tip = Hint(j.kind);
            MenuArt.Text(new Rect(d.x, y, d.width, 44f * s), tip, MenuArt.Small, MenuArt.A(UI.White, 0.6f * a));
            Rect b = new Rect(d.x, d.yMax - 52f * s, d.width, 50f * s);
            bool can = !JobSystem.HasJob;
            if (EcoGui.Button(b, can ? "Accept job" : "Finish your current job first", true, can, UI.Pink, a)) Activate();
        }

        static string Hint(JobKind k)
        {
            switch (k)
            {
                case JobKind.Tea: return "Drive smoothly: knocks and hard landings spill the tea, and it cools as time passes.";
                case JobKind.Taxi: return "A comfy, punctual ride earns a better tip. Near misses make passengers nervous.";
                case JobKind.Courier: return "Long haul. Arrive early for a time bonus - late still pays the base fee.";
                case JobKind.Photo: return "At the spot, open photo mode and take the shot.";
                case JobKind.Snow: return "Icy roads and fragile cargo: smooth driving keeps the bonus.";
                default: return "Several stops across the canyon. Follow the GPS from stop to stop.";
            }
        }

        static void Field(float x, ref float y, float w, string k, string v, float a, float s, bool gold = false)
        {
            MenuArt.Text(new Rect(x, y, w * 0.36f, 26f * s), k, MenuArt.Small, MenuArt.A(UI.White, 0.65f * a));
            MenuArt.Text(new Rect(x + w * 0.34f, y, w * 0.66f, 26f * s), v, gold ? MenuArt.MidR : MenuArt.BodyR, MenuArt.A(gold ? UI.Gold : UI.White, a));
            y += 30f * s;
        }
    }
}

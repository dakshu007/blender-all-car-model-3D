// EconomyWorld.cs - economy things placed in the world by this module, so work and shopping function even
// without the places module: roadside job boards (one or two per region: a lacquered notice board with a
// lantern, a glowing ring and a soft light pillar) and temporary showroom desks (Jade Harbour City for cars,
// Lotus Harbour for bikes). Each carries an Interactable (onFoot + inVehicle). The temporary showroom desks
// hide themselves as soon as another module provides a "Showroom" interactable.
// If no interaction system is present (the on-foot module shows "E  prompt" and calls Use), a small fallback
// here shows the prompt and handles E / gamepad X near these markers.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class EconomyWorld : MonoBehaviour
    {
        public static EconomyWorld Instance { get; private set; }
        /// <summary>Set false (e.g. by the places module) to skip the temporary showroom desks.</summary>
        public static bool PlaceTemporaryShowrooms = true;

        sealed class Marker
        {
            public GameObject go;
            public Interactable inter;
            public string label;
            public bool showroom;
            public Color colour;
        }

        readonly List<Marker> markers = new List<Marker>();
        Transform root;
        Material beamMat, ringMat;
        int lastUseFrame = -1, fallbackUseFrame = -1;
        bool otherSystem, checkedTypes;
        float showroomCheckT;
        Marker near;
        string nearPrompt = "";

        struct Spot { public string place, board, label; public float along; public bool showroom; }
        static readonly Spot[] Spots =
        {
            new Spot { place = "Lantern Street Tea House", board = "teahouse", label = "Tea House Orders" },
            new Spot { place = "Jade Plaza Taxi Rank", board = "taxi_rank", label = "Taxi Rank" },
            new Spot { place = "Ferry Pier", board = "harbour", label = "Harbour Notice Board" },
            new Spot { place = "Sea Breeze Tea House", board = "harbour", label = "Harbour Notice Board" },
            new Spot { place = "Cable Car Tea Stall", board = "teahouse", label = "Tea Stall Orders" },
            new Spot { place = "Ski Lodge", board = "lodge", label = "Lodge Supply Board" },
            new Spot { place = "Oasis Tea House", board = "oasis", label = "Caravan Board" },
            new Spot { place = "Caravanserai", board = "caravanserai", label = "Caravan Board" },
            new Spot { place = "Jade Plaza Taxi Rank", board = "city_cars", label = "Jade Harbour Showroom", along = 34f, showroom = true },
            new Spot { place = "Lotus Harbour", board = "coast_bikes", label = "Lotus Coast Motorbikes", along = -30f, showroom = true },
        };

        void Awake()
        {
            Instance = this;
            WorldHooks.WorldBuilt += Build;
        }

        // ------------------------------------------------------------------ build
        void Build(WorldBuilder w)
        {
            if (root != null) return;
            JobData.Resolve(w);
            root = new GameObject("BR_EconomyMarkers").transform;
            var sh = MaterialLibrary.FindShader("BlossomRally/Additive");
            if (sh != null)
            {
                beamMat = new Material(sh) { name = "BR_BoardBeam" };
                beamMat.SetFloat("_Soft", 1.4f);
                beamMat.SetFloat("_Intensity", 0.7f);
                ringMat = new Material(sh) { name = "BR_BoardRing" };
                ringMat.SetFloat("_Soft", 0f);
                ringMat.SetFloat("_Intensity", 0.8f);
            }
            foreach (var sp in Spots)
            {
                if (sp.showroom && !PlaceTemporaryShowrooms) continue;
                var pl = JobData.Place(sp.place);
                if (pl == null) continue;
                try { Place(w, pl, sp); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        void Place(WorldBuilder w, JobPlace pl, Spot sp)
        {
            Vector3 dir = pl.roadDir.sqrMagnitude > 0.01f ? pl.roadDir.normalized : Vector3.forward;
            Vector3 p = pl.pos + dir * sp.along;
            // keep clear of buildings / props: slide along the road until the spot is free
            for (int k = 0; k < 7; k++)
            {
                float off = (k == 0 ? 0f : ((k + 1) / 2) * 7f * (k % 2 == 0 ? -1f : 1f));
                Vector3 q = p + dir * off;
                q.y = w.TerrainHeight(q.x, q.z);
                if (!Physics.CheckSphere(q + Vector3.up * 1.6f, 1.1f, ~0, QueryTriggerInteraction.Ignore) || k == 6) { p = q; break; }
            }
            p.y = w.TerrainHeight(p.x, p.z);
            Color col = sp.showroom ? UI.Gold : BoardColour(sp.board);
            var go = new GameObject(sp.showroom ? "Showroom_" + sp.board : "JobBoard_" + sp.board);
            go.transform.SetParent(root, false);
            go.transform.position = p;
            // face the road
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 toRoad = pl.onRoad ? -side : -dir;
            Vector2 flatToRoad = new Vector2(pl.raw.x - pl.pos.x, pl.raw.y - pl.pos.z);
            if (pl.onRoad && flatToRoad.sqrMagnitude > 0.01f)
            {
                // the place sits off the road on the 'raw' side, so the road is the other way
                Vector3 away = new Vector3(flatToRoad.x, 0f, flatToRoad.y).normalized;
                toRoad = -away;
            }
            go.transform.rotation = Quaternion.LookRotation(toRoad, Vector3.up);

            var g = new LifeGeo();
            BuildBoard(g, sp.showroom);
            AddRenderer(go.transform, g.ToMesh(sp.showroom ? "ShowroomDesk" : "JobBoard"), MaterialLibrary.Terrain, true);
            var glow = new LifeGeo();
            PG.Lantern(glow, new Vector3(0f, 2.75f, 0.35f), 0.22f, 0.4f, sp.showroom ? Pal.Gold : Pal.RedBright, 0);
            var glowMat = MaterialLibrary.Glow;
            if (glow.v.Count > 0 && glowMat != null) AddRenderer(go.transform, glow.ToMesh("BoardLantern"), glowMat, false);
            if (beamMat != null)
            {
                var ring = AddRenderer(go.transform, JobSystem.RingMesh(2.6f, 3.4f, 28), ringMat, false);
                ring.transform.localPosition = new Vector3(0f, 0f, 2.5f);
                var beam = AddRenderer(go.transform, JobSystem.BeamMesh(0.9f, 12f, 12), beamMat, false);
                beam.transform.localPosition = new Vector3(0f, 0f, 2.5f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", col);
                ring.SetPropertyBlock(mpb);
                beam.SetPropertyBlock(mpb);
            }

            var m = new Marker { go = go, label = sp.label, showroom = sp.showroom, colour = col };
            var inter = Interactable.Create(go.transform, p + go.transform.forward * 2.5f + Vector3.up * 0.5f, sp.label, null, 9f);
            inter.onFoot = true;
            inter.inVehicle = true;
            inter.detail = sp.showroom ? "Browse and buy vehicles" : "Deliveries, fares and photo jobs";
            string board = sp.board;
            bool showroom = sp.showroom;
            inter.available = () => Usable();
            inter.onUse = () => Use(board, showroom);
            m.inter = inter;
            markers.Add(m);
        }

        static Color BoardColour(string board)
        {
            if (board.Contains("taxi")) return JobData.KindColours[(int)JobKind.Taxi];
            if (board.Contains("harbour")) return JobData.KindColours[(int)JobKind.Courier];
            if (board.Contains("lodge")) return JobData.KindColours[(int)JobKind.Snow];
            if (board.Contains("oasis") || board.Contains("caravan")) return JobData.KindColours[(int)JobKind.Desert];
            return JobData.KindColours[(int)JobKind.Tea];
        }

        static void BuildBoard(LifeGeo g, bool showroom)
        {
            Color32 post = Pal.Red, wood = Pal.WoodDark, paper = Pal.Plaster, roof = Pal.Tile;
            // two lacquered posts
            PG.BoxMM(g, new Vector3(-0.85f, 0f, -0.08f), new Vector3(-0.7f, 2.6f, 0.08f), post);
            PG.BoxMM(g, new Vector3(0.7f, 0f, -0.08f), new Vector3(0.85f, 2.6f, 0.08f), post);
            // board with frame
            PG.BoxMM(g, new Vector3(-0.75f, 0.95f, -0.06f), new Vector3(0.75f, 2.15f, 0.06f), wood);
            PG.BoxMM(g, new Vector3(-0.66f, 1.04f, 0.06f), new Vector3(0.66f, 2.06f, 0.09f), showroom ? Pal.Gold : paper);
            // notes pinned on the board
            if (!showroom)
            {
                PG.BoxMM(g, new Vector3(-0.55f, 1.55f, 0.09f), new Vector3(-0.15f, 1.95f, 0.1f), Pal.PorcelainBlue);
                PG.BoxMM(g, new Vector3(0.05f, 1.6f, 0.09f), new Vector3(0.5f, 1.98f, 0.1f), Pal.RedBright);
                PG.BoxMM(g, new Vector3(-0.4f, 1.12f, 0.09f), new Vector3(0.25f, 1.45f, 0.1f), Pal.GreenTile2);
            }
            else
            {
                PG.BoxMM(g, new Vector3(-0.5f, 1.3f, 0.09f), new Vector3(0.5f, 1.8f, 0.1f), Pal.RedDeep);
            }
            // little tiled roof
            PG.BoxMM(g, new Vector3(-1.05f, 2.6f, -0.32f), new Vector3(1.05f, 2.7f, 0.32f), roof);
            PG.BoxMM(g, new Vector3(-0.9f, 2.7f, -0.2f), new Vector3(0.9f, 2.8f, 0.2f), Pal.Tile2);
            // stone footing
            PG.BoxMM(g, new Vector3(-1.0f, -0.3f, -0.25f), new Vector3(1.0f, 0.12f, 0.25f), Pal.Stone);
        }

        static MeshRenderer AddRenderer(Transform parent, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return r;
        }

        // ------------------------------------------------------------------ use
        static bool Usable()
        {
            if (EcoMenus.IsOpen) return false;
            var car = PlayerState.Vehicle;
            return PlayerState.OnFoot || car == null || car.SpeedKmh < 22f;
        }

        void Use(string board, bool showroom)
        {
            if (Time.frameCount == lastUseFrame) return;      // fallback and interaction system on the same press
            if (Time.frameCount != fallbackUseFrame) otherSystem = true;    // somebody else called Use: they own prompts
            lastUseFrame = Time.frameCount;
            if (!Usable()) { EconomyDirector.Toast("Stop the car first"); return; }
            if (showroom) { if (GameplayHooks.OpenShowroom != null) GameplayHooks.OpenShowroom(board); }
            else if (GameplayHooks.OpenJobBoard != null) GameplayHooks.OpenJobBoard(board);
        }

        /// <summary>Is there an interaction system (a MonoBehaviour named like "Interact...") besides ours?</summary>
        void CheckTypes()
        {
            checkedTypes = true;
            try
            {
                foreach (var t in typeof(Interactable).Assembly.GetTypes())
                {
                    if (t == typeof(Interactable) || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                    if (t.Name.IndexOf("Interact", System.StringComparison.OrdinalIgnoreCase) >= 0) { otherSystem = true; return; }
                }
            }
            catch (System.Exception) { }
        }

        void Update()
        {
            if (!checkedTypes) CheckTypes();
            near = null;
            if (markers.Count == 0) return;
            // temporary showroom desks step aside once the places module provides real showrooms
            showroomCheckT -= Time.unscaledDeltaTime;
            if (showroomCheckT <= 0f)
            {
                showroomCheckT = 5f;
                bool real = false;
                var all = Interactable.All;
                for (int i = 0; i < all.Count && !real; i++)
                {
                    var it = all[i];
                    if (it == null || it.prompt == null || IsOurs(it)) continue;
                    if (it.prompt.IndexOf("showroom", System.StringComparison.OrdinalIgnoreCase) >= 0) real = true;
                }
                if (real)
                    for (int i = 0; i < markers.Count; i++)
                        if (markers[i].showroom && markers[i].go != null && markers[i].go.activeSelf) markers[i].go.SetActive(false);
            }
            if (otherSystem) return;
            // fallback interaction (no on-foot module): nearest marker in range
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || EcoMenus.IsOpen || GameplayHooks.MenuOpen) return;
            Vector3 me = PlayerState.Position;
            float best = float.MaxValue;
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m.go == null || !m.go.activeInHierarchy || m.inter == null) continue;
                float d = (m.inter.transform.position - me).sqrMagnitude;
                if (d < m.inter.radius * m.inter.radius && d < best) { best = d; near = m; }
            }
            if (near == null) return;
            if (!ReferenceEquals(nearPrompt, near.label)) nearPrompt = near.label;
            if (InputReader.Down(KeyCode.E) || GamepadBridge.ButtonDown(PadButton.West))
            {
                fallbackUseFrame = Time.frameCount;
                near.inter.Use();
            }
        }

        bool IsOurs(Interactable it)
        {
            for (int i = 0; i < markers.Count; i++) if (markers[i].inter == it) return true;
            return false;
        }

        void OnGUI()
        {
            if (otherSystem || near == null || EcoMenus.IsOpen) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || GameManager.HideHud) return;
            EcoGui.Styles();
            float s = EcoGui.S, W = Screen.width, H = Screen.height;
            bool usable = Usable();
            string key = InputReader.PadConnected ? "X" : "E";
            string label = usable ? nearPrompt : "Stop to use";
            float w = EcoGui.ChipWidth(key, label);
            Rect bg = new Rect((W - w) * 0.5f - 12f * s, H * 0.72f - 8f * s, w + 6f * s, 44f * s);
            MenuArt.Card(bg, 0.9f, false);
            MenuArt.Fill(new Rect(bg.x, bg.y, Mathf.Max(3f, 4f * s), bg.height), near.colour);
            bool c;
            EcoGui.Chip(bg.x + 14f * s, bg.y + 8f * s, key, label, out c);
            if (c && usable) { fallbackUseFrame = Time.frameCount; near.inter.Use(); }
        }
    }
}

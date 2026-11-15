// PlacesManager.cs - entry point of the Places module (tea houses, temples, showroom, scooter shop, ski lodge,
// oasis tea house, seafood restaurant). Hooks in by itself (WorldHooks.WorldBuilt). Once WorldProps has placed the
// valley buildings it resolves every place one per frame: finds the existing building by its collider (or a free
// site for the new ones), builds the exterior (new buildings, lantern posts, glowing glyph sign, parking bay) under
// the region root (streams with the region) or its own root in the valley, adds the "Enter ..." door Interactable
// and a map marker. Interiors are built lazily on the first visit (Interiors / InteriorLayouts).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class PlacesManager : MonoBehaviour
    {
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += w => Create(w);
            WorldHooks.CarSpawned += OnCarSpawned;
            GameSettings.Changed += () =>
            {
                PlaceMats.ApplyQuality();
                if (Interiors.Instance != null) Interiors.Instance.ApplyLightQuality();
            };
        }

        public static PlacesManager Instance { get; private set; }
        static readonly List<Place> all = new List<Place>();
        /// <summary>Every place (resolved ones have Ready = true): door, parking spot, viewpoint.</summary>
        public static List<Place> All { get { return all; } }

        public static Place Find(string id)
        {
            for (int i = 0; i < all.Count; i++) if (all[i].id == id) return all[i];
            return null;
        }

        /// <summary>A car bought / chosen in a showroom while inside appears on the showroom's parking bay.</summary>
        static void OnCarSpawned(ArcadeCar car)
        {
            var p = Interiors.CurrentPlace;
            if (car == null || p == null || !PlayerState.OnFoot) return;
            if (p.kind != PlaceKind.Showroom && p.kind != PlaceKind.BikeShop) return;
            if ((p.Parking - p.Door).sqrMagnitude < 1f) return;      // no bay was found
            Vector3 pos = p.Parking + Vector3.up * 0.45f;
            Quaternion rot = Quaternion.Euler(0f, p.ParkingYaw, 0f);
            car.transform.SetPositionAndRotation(pos, rot);
            if (car.Body != null)
            {
                car.Body.position = pos;
                car.Body.rotation = rot;
                if (!car.Body.isKinematic) { car.Body.SetVelocity(Vector3.zero); car.Body.angularVelocity = Vector3.zero; }
            }
        }

        static PlacesManager Create(WorldBuilder w)
        {
            Interiors.Shutdown();
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("PlacesManager");
            var m = go.AddComponent<PlacesManager>();
            m.useGUILayout = false;
            Instance = m;
            m.world = w;
            Interiors.Ensure().transform.SetParent(go.transform, false);
            m.StartCoroutine(m.Setup());
            return m;
        }

        WorldBuilder world;
        PlaceSites sites;
        Transform ownRoot;
        float matT, poiT;

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Setup()
        {
            while (world != null && !world.Done) yield return null;
            if (world == null) yield break;
            // WorldProps places the valley tea house / temple over a few frames after WorldBuilt
            float t0 = Time.realtimeSinceStartup;
            while (WorldProps.Instance != null && !WorldProps.Instance.Ready && Time.realtimeSinceStartup - t0 < 40f) yield return null;
            yield return null;
            float tb = Time.realtimeSinceStartup;
            ownRoot = new GameObject("Places_Exteriors").transform;
            ownRoot.SetParent(transform, false);
            sites = new PlaceSites(world);
            all.Clear();
            all.AddRange(PlaceTable.Build());
            int ok = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var p in all)
            {
                try { Resolve(p); }
                catch (System.Exception e) { Debug.LogException(e); }
                if (p.Ready) ok++;
                else sb.Append(' ').Append(p.id);
                yield return null;
            }
            Debug.Log("[BlossomRally] Places: " + ok + "/" + all.Count + " places ready" + (sb.Length > 0 ? " (missing:" + sb + ")" : "") +
                      ", built in " + ((Time.realtimeSinceStartup - tb) * 1000f).ToString("0") + " ms of frame time");
        }

        // ------------------------------------------------------------------ resolve one place
        Region RegionObj(RegionId id) { return world.Field != null ? world.Field.RegionOf(id) : null; }

        Transform RegionRoot(RegionId id)
        {
            var r = RegionObj(id);
            return r != null && r.Context != null && r.Context.Root != null && id != RegionId.Valley ? r.Context.Root : null;
        }

        void Resolve(Place p)
        {
            Transform searchRoot = p.inWorldProps ? (WorldProps.Instance != null ? WorldProps.Instance.transform : null) : RegionRoot(p.region);
            Transform parent = p.region == RegionId.Valley ? ownRoot : RegionRoot(p.region);
            if (parent == null) parent = ownRoot;
            var pg = new PGeo();
            Color32 board = p.kind == PlaceKind.Temple ? LifeGeo.C(40, 60, 110) : (p.kind == PlaceKind.Restaurant ? LifeGeo.C(40, 80, 130) : LifeGeo.C(150, 40, 34));
            var ext = new GameObject("Place_" + p.id).transform;
            List<System.Action> after = new List<System.Action>();

            if (p.existing)
            {
                if (searchRoot == null) { Debug.LogWarning("[BlossomRally] Places: no root to search for " + p.id); Destroy(ext.gameObject); return; }
                var b = PlaceSites.FindBox(searchRoot, p.colSize, p.colTol, p.near, p.searchR);
                if (b == null) { Debug.LogWarning("[BlossomRally] Places: building for " + p.id + " not found"); Destroy(ext.gameObject); return; }
                sites.Index(searchRoot);
                Vector3 c = b.transform.TransformPoint(b.center);
                float yaw = b.transform.eulerAngles.y;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                float doorZ = p.doorLocal.z;
                if (p.stepRise > 0f)
                {
                    float top = c.y + b.size.y * 0.5f;
                    float h = p.stepFromSize ? b.size.y : top - world.TerrainHeight(c.x, c.z);
                    int steps = h > 0.15f ? Mathf.Clamp(Mathf.CeilToInt(h / p.stepRise), 1, p.stepMax) : 0;
                    doorZ = Mathf.Sign(p.doorLocal.z) * (p.stepFrom + steps * p.stepDepth + 0.75f);
                }
                Vector3 dw = c + q * new Vector3(p.doorLocal.x, 0f, doorZ);
                dw.y = p.doorOnColliderBase ? c.y - b.size.y * 0.5f + p.doorLift : world.TerrainHeight(dw.x, dw.z);
                Vector3 od = q * p.outLocal;
                p.Door = dw;
                p.OutYaw = Mathf.Atan2(od.x, od.z) * Mathf.Rad2Deg;
                p.buildingPos = c;
                p.buildingYaw = yaw;
                ExteriorKit.Entrance(pg, sites, world, dw, p.OutYaw, p.glyph, board, p.doorOnColliderBase);
            }
            else
            {
                var reg = RegionObj(p.region);
                if (reg == null || reg.Context == null) { Destroy(ext.gameObject); return; }
                sites.Index(reg.Context.Root);
                if (WorldProps.Instance != null && p.region == RegionId.Valley) sites.Index(WorldProps.Instance.transform);
                Rect r = reg.Rect;
                Rect area = new Rect(r.xMin + 40f, r.yMin + 40f, r.width - 80f, r.height - 80f);
                PlaceSites.Site s;
                if (!sites.FreeSite(p.prefer, p.prefR, p.hx, p.hz, 6f, 24f, 1.6f, WorldBuilder.WaterY + 2.2f, area, out s))
                {
                    Debug.LogWarning("[BlossomRally] Places: no free site for " + p.id);
                    Destroy(ext.gameObject);
                    return;
                }
                float front = Vector2.Distance(s.c, s.roadPoint) - p.hz;
                Vector3 door;
                if (p.kind == PlaceKind.Showroom) door = ExteriorKit.Showroom(pg, s.c, s.yaw, s.floorY, s.minY, p.hx, p.hz, front);
                else door = ExteriorKit.BikeShop(pg, s.c, s.yaw, s.floorY, s.minY, p.hx, p.hz, new System.Random(77));
                sites.Reserve(s.c, p.hx + 2f, p.hz + 3f, s.yaw, s.minY - 1f, s.floorY + 10f);
                {
                    Quaternion qs = Quaternion.Euler(0f, s.yaw, 0f);
                    float edge = p.kind == PlaceKind.Showroom ? p.hz + 1.8f : p.hz + 1.5f;
                    ExteriorKit.FrontSteps(pg, world, new Vector3(s.c.x, s.floorY, s.c.y) + qs * new Vector3(0f, 0f, edge), s.yaw, 3.2f);
                }
                p.Door = door;
                p.OutYaw = s.yaw;
                p.buildingPos = new Vector3(s.c.x, s.floorY, s.c.y);
                p.buildingYaw = s.yaw;
                Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
                // forecourt parking (and a featured car on a podium in front of the showroom)
                Vector3 bay = p.buildingPos + q * new Vector3(p.hx - 3f, 0f, p.hz + 5.6f);
                if (front > 9f)
                {
                    ExteriorKit.ParkingBay(pg, world, bay, s.yaw, true);
                    p.Parking = new Vector3(bay.x, world.TerrainHeight(bay.x, bay.z), bay.z);
                    p.ParkingYaw = s.yaw + 180f;
                }
                else ExistingParking(p, pg);
                if (p.kind == PlaceKind.Showroom && front > 9f)
                {
                    Vector3 pod = p.buildingPos + q * new Vector3(-p.hx + 4f, 0f, p.hz + 5.4f);
                    float py = world.TerrainHeight(pod.x, pod.z);
                    pod.y = py;
                    pg.shell.Frustum(pod - Vector3.up * 0.4f, pod + Vector3.up * 0.25f, 2.6f, 2.6f, 2.5f, 2.5f, 18, 0f, LifeGeo.C(200, 202, 206), LifeGeo.C(240, 242, 244), false, true);
                    pg.glow.Frustum(pod + Vector3.up * 0.05f, pod + Vector3.up * 0.1f, 2.62f, 2.62f, 2.62f, 2.62f, 18, 0f, LifeGeo.C(40, 190, 150), LifeGeo.C(40, 190, 150), false, false);
                    pg.Col(pod + Vector3.up * 0.8f, new Vector3(5f, 1.6f, 5f), s.yaw);
                    Vector3 carPos = pod + Vector3.up * 0.26f;
                    float carYaw = s.yaw + 140f;
                    after.Add(() => ExteriorKit.DisplayCar("CAR-022", carPos, carYaw, ext, false));
                }
            }

            if (p.existing) ExistingParking(p, pg);
            Viewpoints(p);

            // exterior meshes (world space) under the region root, then the door
            ext.SetParent(parent, true);
            pg.Finish(ext, false, "Place_" + p.id);
            foreach (var a in after) a();
            var door2 = Interactable.Create(ext, p.Door + Vector3.up * 1f, "Enter " + p.name, () => { if (Interiors.Instance != null) Interiors.Instance.Enter(p); }, 2.6f);
            door2.detail = p.hanzi + "   ·   " + p.hint;
            door2.available = () => !Interiors.Busy && !Interiors.Inside && !GameplayHooks.MenuOpen;
            p.exterior = ext;
            p.poi = new MapPoi { name = p.name, hint = p.hint, pos = new Vector2(p.Door.x, p.Door.z), icon = p.icon, region = p.region, landmark = true };
            p.Ready = true;
            poiT = 0f;
        }

        void ExistingParking(Place p, PGeo pg)
        {
            Vector2 d2 = new Vector2(p.Door.x, p.Door.z);
            RoadSample rs;
            float dist = world.NearestRoad(d2, out rs, 60f);
            p.Parking = p.Door;
            p.ParkingYaw = p.OutYaw;
            if (dist >= 60f) return;
            Vector2 n = d2 - rs.pos;
            if (n.sqrMagnitude < 1e-3f) return;
            n.Normalize();
            Vector2 t = rs.tangent.sqrMagnitude > 1e-4f ? rs.tangent.normalized : new Vector2(-n.y, n.x);
            float yaw = Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg;
            float[] shifts = { 6f, -6f, 12f, -12f, 0f, 18f, -18f };
            foreach (float sft in shifts)
            {
                Vector2 c = rs.pos + n * (rs.halfWidth + 1.7f) + t * sft;
                if (world.Field.EdgeDistance(c) < 1.0f) continue;
                float y = world.TerrainHeight(c.x, c.y);
                if (!sites.FootprintFree(c, 1.5f, 2.9f, yaw, y - 0.2f, y + 2.5f)) continue;
                float y2 = world.TerrainHeight(c.x + t.x * 2.5f, c.y + t.y * 2.5f), y3 = world.TerrainHeight(c.x - t.x * 2.5f, c.y - t.y * 2.5f);
                if (Mathf.Abs(y2 - y3) > 0.9f) continue;
                Vector3 cw = new Vector3(c.x, y, c.y);
                ExteriorKit.ParkingBay(pg, world, cw, yaw, true);
                sites.Reserve(c, 1.5f, 2.9f, yaw, y - 0.2f, y + 2.5f);
                p.Parking = cw;
                p.ParkingYaw = yaw;
                return;
            }
        }

        void Viewpoints(Place p)
        {
            if (p.id == "valley_temple" && world.roadSamples.Count > 3 && world.roadSamples[3].Count > 1)
            {
                // the summit of Pagoda hill above the temple (end of the pagoda road)
                var hp = world.roadSamples[3];
                Vector2 end = hp[hp.Count - 1].pos;
                Vector2 dir = (end - MapData.PagodaTop);
                if (dir.sqrMagnitude < 1e-3f) dir = Vector2.up;
                dir.Normalize();
                Vector2 v = end + dir * 3f;
                p.Viewpoint = new Vector3(v.x, world.TerrainHeight(v.x, v.y), v.y);
                p.ViewpointYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                p.HasViewpoint = true;
            }
            else if (p.id == "mountain_monastery")
            {
                // front edge of the pagoda terrace (MountainsLandmarks: T3 at +9 m, monastery yaw 12, looking down the hill)
                Vector3 l = Quaternion.Euler(0f, 12f, 0f) * new Vector3(-6f, 0f, 55f);
                Vector2 v = new Vector2(214f + l.x, 1386f + l.z);
                p.Viewpoint = new Vector3(v.x, Mathf.Max(176f + 9f, world.TerrainHeight(v.x, v.y)), v.y);
                p.ViewpointYaw = 192f;
                p.HasViewpoint = true;
            }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            matT -= udt;
            if (matT <= 0f) { matT = 0.5f; PlaceMats.UpdateNight(); }
            poiT -= udt;
            if (poiT <= 0f)
            {
                poiT = 2f;
                // MapMarkers rebuilds its list when the world / event list changes: keep our markers in it
                var list = MapMarkers.All;
                for (int i = 0; i < all.Count; i++)
                    if (all[i].poi != null && !list.Contains(all[i].poi)) list.Add(all[i].poi);
            }
            if (toastT > 0f) toastT -= udt;
        }

        // ------------------------------------------------------------------ toasts (through the game manager when wired)
        static string toastMsg;
        static float toastT;

        public static void Toast(string msg)
        {
            if (GameplayHooks.Toast != null) { GameplayHooks.Toast(msg); return; }
            toastMsg = msg;
            toastT = 3.5f;
        }

        void OnGUI()
        {
            if (toastT <= 0f || Event.current.type != EventType.Repaint || string.IsNullOrEmpty(toastMsg)) return;
            UI.Styles();
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            var r = new Rect(Screen.width * 0.5f - 320f * s, Screen.height * 0.72f, 640f * s, 44f * s);
            float a = Mathf.Clamp01(toastT / 0.4f);
            UI.Panel(r, 0.55f * a);
            UI.Label(r, toastMsg, UI.Mid, new Color(1f, 0.97f, 0.96f, a));
        }
    }
}

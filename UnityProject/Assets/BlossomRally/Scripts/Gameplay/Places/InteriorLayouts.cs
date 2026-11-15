// InteriorLayouts.cs - the cosy pocket interiors, one builder per place kind: tea house (valley / city variant),
// seafood restaurant, temple hall (valley / golden monastery variant), car showroom, scooter shop, ski lodge and the
// oasis tea house. Each builds its room (PlaceKit), furniture and decor into one PGeo (baked light), 1-2 warm point
// lights (off on Low: the bake carries the look), NPCs from PersonFactory (shopkeepers Work, guests Sit, monks
// Pray), seats, steam points and the Interactables (exit, shop, job board, sit, bell, incense, wish, viewpoint,
// showroom). Built lazily on the first visit, while the screen is black.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class InteriorLayouts
    {
        static readonly Vector3 Up = Vector3.up;
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        static readonly Color WarmLight = new Color(1f, 0.78f, 0.55f);

        public static InteriorSpace Build(Place p, Transform pocketRoot)
        {
            var sp = new InteriorSpace { place = p };
            switch (p.kind)
            {
                case PlaceKind.TeaHouse: if (p.variant == 1) { sp.W = 10f; sp.D = 9f; sp.H = 3.4f; } else { sp.W = 12f; sp.D = 10f; sp.H = 3.6f; } break;
                case PlaceKind.Restaurant: sp.W = 13f; sp.D = 11f; sp.H = 3.8f; break;
                case PlaceKind.Temple: if (p.variant == 1) { sp.W = 18f; sp.D = 20f; sp.H = 8.5f; } else { sp.W = 14f; sp.D = 16f; sp.H = 7f; } sp.camDist = 4.2f; break;
                case PlaceKind.Showroom: sp.W = 22f; sp.D = 16f; sp.H = 6f; sp.camDist = 4.6f; break;
                case PlaceKind.BikeShop: sp.W = 12f; sp.D = 10f; sp.H = 4f; break;
                case PlaceKind.SkiLodge: sp.W = 14f; sp.D = 11f; sp.H = 4.6f; sp.camDist = 3.6f; break;
                case PlaceKind.OasisTea: sp.W = 10f; sp.D = 9f; sp.H = 3.4f; break;
            }
            sp.rot = Quaternion.Euler(0f, p.OutYaw + 180f, 0f);
            Vector3 door = p.Door;
            sp.origin = new Vector3(door.x, Place.PocketY, door.z) + sp.rot * new Vector3(0f, 0f, sp.D * 0.5f);
            var go = new GameObject("Interior_" + p.id);
            go.SetActive(false);
            go.transform.SetParent(pocketRoot, false);
            go.transform.SetPositionAndRotation(sp.origin, sp.rot);
            sp.root = go.transform;
            sp.spawnLocal = new Vector3(0f, 0.05f, -sp.D * 0.5f + 1.3f);
            var pg = new PGeo();
            var rng = new System.Random(p.id.GetHashCode() & 0x7fffffff);
            switch (p.kind)
            {
                case PlaceKind.TeaHouse: TeaHouse(sp, pg, rng); break;
                case PlaceKind.Restaurant: Restaurant(sp, pg, rng); break;
                case PlaceKind.Temple: Temple(sp, pg, rng); break;
                case PlaceKind.Showroom: Showroom(sp, pg, rng); break;
                case PlaceKind.BikeShop: BikeShop(sp, pg, rng); break;
                case PlaceKind.SkiLodge: SkiLodge(sp, pg, rng); break;
                case PlaceKind.OasisTea: Oasis(sp, pg, rng); break;
            }
            pg.Finish(sp.root, true, p.id);
            Exit(sp);
            return sp;
        }

        // ================================================================== shared helpers
        static System.Func<bool> Free { get { return () => !Interiors.Busy && !Interiors.Sitting && !GameplayHooks.MenuOpen; } }

        static Interactable Act(InteriorSpace sp, Vector3 local, string prompt, string detail, System.Action use, float radius = 1.8f)
        {
            var i = Interactable.Create(sp.root, sp.W2(local), prompt, use, radius);
            i.detail = detail ?? "";
            i.available = Free;
            return i;
        }

        static void Exit(InteriorSpace sp)
        {
            Act(sp, new Vector3(0f, 1f, -sp.D * 0.5f + 0.55f), "Leave", sp.place.name, () => Interiors.Instance.Exit(), 1.6f);
        }

        static PersonRig Npc(InteriorSpace sp, PersonLook look, Vector3 local, float yaw, PersonPose pose)
        {
            var r = PersonFactory.Create(look, sp.W2(local), sp.Yaw(yaw), sp.root, 0);
            if (r != null) r.SetPose(pose);
            return r;
        }

        static PersonLook Keeper(int seed, Color apron)
        {
            var l = PersonLook.Random(seed);
            l.outfit = 2; l.bottom = apron; l.accent = apron; l.hat = false;
            return l;
        }

        static PersonLook Guest(int seed, int outfit = 0)
        {
            var l = PersonLook.Random(seed);
            l.outfit = outfit;
            return l;
        }

        static PersonLook Monk(int seed)
        {
            var l = PersonLook.Random(seed);
            l.outfit = 3; l.top = new Color(0.86f, 0.52f, 0.18f); l.bottom = new Color(0.62f, 0.26f, 0.14f); l.accent = l.bottom;
            l.hairStyle = 0; l.hat = false; l.female = false; l.hair = l.skin;
            return l;
        }

        static void Light(InteriorSpace sp, PGeo pg, Vector3 local, Color c, float range, float intensity, float bake = 0.75f)
        {
            pg.Light(local, c, range * 0.85f, bake);
            var go = new GameObject("Light");
            go.transform.SetParent(sp.root, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            sp.lights.Add(l);
            sp.lightBase.Add(intensity);
        }

        static Seat AddSeat(InteriorSpace sp, Vector3 floorLocal, Vector3 tableLocal, string prompt, bool steam, Vector3 steamLocal)
        {
            Vector3 d = tableLocal - floorLocal; d.y = 0f;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var s = new Seat
            {
                local = floorLocal, yawLocal = yaw, focusLocal = new Vector3(tableLocal.x, 0.9f, tableLocal.z),
                standLocal = floorLocal - d.normalized * 0.75f, steam = steam, steamLocal = steamLocal
            };
            Act(sp, floorLocal + Up * 0.8f, prompt, "", () => Interiors.Instance.Sit(sp, s), 1.3f);
            return s;
        }

        static void Shop(InteriorSpace sp, Vector3 local, string prompt, ShopDef def)
        {
            Act(sp, local, prompt, def.subtitle, () =>
            {
                if (GameplayHooks.OpenShop != null) { GameplayHooks.OpenShop(def); return; }
                // no economy module: buy the first item directly
                var it = def.items[0];
                if (Wallet.TrySpend(it.price, it.name)) { if (it.onBought != null) it.onBought(); }
                else PlacesManager.Toast("Not enough money for " + it.name);
            });
        }

        static void Jobs(InteriorSpace sp, Vector3 local, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Act(sp, local, "Job board", "Work for the house", () =>
            {
                if (GameplayHooks.OpenJobBoard != null) GameplayHooks.OpenJobBoard(id);
                else PlacesManager.Toast("No jobs posted today");
            });
        }

        static void Showroom(InteriorSpace sp, Vector3 local, string prompt, string detail)
        {
            string id = sp.place.showroom;
            Act(sp, local, prompt, detail, () =>
            {
                if (GameplayHooks.OpenShowroom != null) GameplayHooks.OpenShowroom(id);
                else PlacesManager.Toast("The showroom opens soon");
            }, 2.4f);
        }

        static ShopItem Item(string name, string desc, int price, System.Action bought)
        {
            return new ShopItem { name = name, description = desc, price = price, onBought = bought };
        }

        static ShopItem TeaItem(string name, int price, float pay, float minutes)
        {
            string d = "+" + Mathf.RoundToInt(pay * 100f) + "% job pay for " + minutes + " min";
            return Item(name, d, price, () => { Buffs.Tea(name, pay, minutes); PlacesManager.Toast(name + " - " + d); });
        }

        static ShopItem FoodItem(string name, int price, float pay, float minutes)
        {
            string d = "+" + Mathf.RoundToInt(pay * 100f) + "% job pay for " + minutes + " min";
            return Item(name, d, price, () => { Buffs.Food(name, pay, minutes); PlacesManager.Toast(name + " - " + d); });
        }

        static GameObject MeshObj(Transform parent, string name, LifeGeo g, Material m, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = g.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return go;
        }

        /// <summary>A PropShapes tea table + stools at 'at' (local). Returns the steam point; stool offsets via StoolOffset.</summary>
        static Vector3 TeaTable(PGeo pg, Vector3 at, System.Random rng)
        {
            var m = new PropModel();
            Vector3 steam = PropShapes.TeaTable(m, Vector3.zero, rng);
            pg.Add(m, at, 0f);
            pg.Col(at + Up * 0.4f, new Vector3(1.15f, 0.8f, 1.15f));
            return at + steam;
        }

        static Vector3 Stool(int k)
        {
            float a = k * Mathf.PI * 0.5f + 0.4f;
            return new Vector3(Mathf.Cos(a) * 0.9f, 0f, Mathf.Sin(a) * 0.9f);
        }

        static float FaceYaw(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        // ================================================================== tea house
        static void TeaHouse(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            bool city = sp.place.variant == 1;
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style();
            if (city)
            {
                st.wallLow = C(52, 96, 80); st.post = C(40, 84, 70); st.wallHigh = C(232, 226, 204); st.floorA = C(150, 108, 72); st.floorB = C(132, 92, 60); st.ceiling = C(150, 112, 80);
            }
            PlaceKit.Room(pg, W, D, H, st, 11);
            pg.ambient = new Color(0.66f, 0.58f, 0.5f);
            // garden / lake view on the back wall, a side window
            if (city) PlaceKit.Window(pg, new Vector3(0f, 1.75f, hz), Vector3.back, 2.2f, 2.2f, PlaceKit.View.Garden, Pal.WoodDark, false, true);
            else PlaceKit.Window(pg, new Vector3(0.6f, 1.7f, hz), Vector3.back, 4.6f, 1.6f, PlaceKit.View.Lake, Pal.WoodDark);
            PlaceKit.Window(pg, new Vector3(hx, 1.65f, 1.6f), Vector3.left, 2.4f, 1.4f, PlaceKit.View.Garden, Pal.WoodDark);
            // tea counter + shelves along the left wall, keeper behind
            Vector3 cp = new Vector3(-hx + 1.55f, 0f, 0.6f);
            PlaceKit.Counter(pg, cp, Vector3.right, 3.4f, 1.0f, 0.7f, city ? C(60, 100, 84) : C(120, 74, 48), C(196, 150, 100));
            PlaceKit.Shelf(pg, new Vector3(-hx, 0f, 0.6f), Vector3.right, 3.4f, 2.4f, 0.42f, 4, rng, 0);
            sp.steamPts.Add(sp.W2(PlaceKit.TeaSet(pg, cp + new Vector3(0.05f, 1.0f, -0.9f), 90f, rng)));
            PlaceKit.TeaSet(pg, cp + new Vector3(0.05f, 1.0f, 0.9f), 90f, rng);
            Npc(sp, Keeper(31 + sp.place.variant, city ? new Color(0.2f, 0.45f, 0.36f) : new Color(0.55f, 0.18f, 0.16f)), new Vector3(-hx + 0.8f, 0f, 0.6f), 90f, PersonPose.Work);
            var tea = new ShopDef { id = "tea_" + sp.place.id, title = sp.place.name, subtitle = city ? "Old Town teas" : "Teas by the lotus lake" };
            if (city)
            {
                tea.items.Add(TeaItem("Aged pu-erh", 20, 0.12f, 8f));
                tea.items.Add(TeaItem("Chrysanthemum tea", 14, 0.08f, 6f));
                tea.items.Add(Item("Tea gift tin", "5% off in every shop for 15 min", 30, () => { Buffs.Add("tin", "Tea gift tin", "茶", BuffKind.Discount, 0.05f, 15f, Buffs.TeaGreen); PlacesManager.Toast("Tea gift tin - 5% off in shops for 15 min"); }));
                tea.items.Add(FoodItem("Egg tarts", 12, 0.06f, 6f));
            }
            else
            {
                tea.items.Add(TeaItem("Jasmine tea", 12, 0.10f, 5f));
                tea.items.Add(TeaItem("Longjing green tea", 18, 0.12f, 8f));
                tea.items.Add(TeaItem("Oolong & almond cookies", 24, 0.15f, 8f));
                tea.items.Add(FoodItem("Osmanthus cake", 15, 0.08f, 6f));
            }
            Shop(sp, cp + new Vector3(0.9f, 1f, 0f), "Buy tea", tea);
            // tables
            float tx0 = city ? 0.2f : 0.6f, tx1 = city ? 2.9f : 3.5f, tz0 = city ? -1.3f : -1.5f, tz1 = city ? 1.9f : 2.1f;
            Vector3[] tables = { new Vector3(tx0, 0f, tz0), new Vector3(tx1, 0f, tz0), new Vector3(tx0, 0f, tz1), new Vector3(tx1, 0f, tz1) };
            for (int i = 0; i < tables.Length; i++)
            {
                Vector3 steam = TeaTable(pg, tables[i], rng);
                PlaceKit.Lantern(pg, tables[i] + Up * (H - 0.5f), H, 0.27f, 0.44f, i % 2 == 0 ? Pal.RedBright : C(246, 190, 60));
                pg.Light(tables[i] + Up * (H - 0.8f), WarmLight, 3.6f, 0.4f);
                if (i == 0 || i == 3)
                {
                    sp.steamPts.Add(sp.W2(steam));
                    int guests = i == 0 ? 2 : 1;
                    for (int g = 0; g < guests; g++)
                    {
                        Vector3 so = tables[i] + Stool(g == 0 ? 0 : 1);
                        Npc(sp, Guest(100 + i * 7 + g, (i + g) % 3 == 0 ? 6 : 0), so, FaceYaw(so, tables[i]), PersonPose.Sit);
                    }
                }
                else AddSeat(sp, tables[i] + Stool(2), tables[i], "Sit down for tea", true, steam - tables[i] + tables[i]);
            }
            // decor
            Jobs(sp, new Vector3(hx - 0.8f, 1f, -2.4f), sp.place.jobBoard);
            PlaceKit.JobBoard(pg, new Vector3(hx, 1.45f, -2.4f), Vector3.left, rng);
            PlaceKit.Scroll(pg, new Vector3(-2.6f, 1.75f, -hz), Vector3.forward, 0.55f, 1.15f, '福', C(244, 236, 214), Pal.Ink);
            PlaceKit.Scroll(pg, new Vector3(2.6f, 1.75f, -hz), Vector3.forward, 0.55f, 1.15f, '茶', C(244, 236, 214), Pal.Ink);
            PlaceKit.Scroll(pg, new Vector3(-hx + 0.02f, 1.8f, hz - 1.0f), Vector3.right, 0.5f, 1.0f, '禅', C(244, 236, 214), Pal.Ink);
            PlaceKit.Plant(pg, new Vector3(hx - 0.55f, 0f, hz - 0.55f), 1.1f, rng, true);
            PlaceKit.Plant(pg, new Vector3(hx - 0.55f, 0f, -hz + 0.55f), 0.9f, rng);
            PlaceKit.Plant(pg, new Vector3(-hx + 0.55f, 0f, hz - 0.55f), 1.0f, rng);
            PlaceKit.FloorLantern(pg, new Vector3(1.5f, 0f, -hz + 0.45f), 1.25f, C(255, 226, 170));
            PlaceKit.FloorLantern(pg, new Vector3(-1.5f, 0f, -hz + 0.45f), 1.25f, C(255, 226, 170));
            PlaceKit.Lantern(pg, new Vector3(-hx + 1.6f, H - 0.5f, -0.6f), H, 0.24f, 0.4f, Pal.RedBright);
            PlaceKit.Lantern(pg, new Vector3(-hx + 1.6f, H - 0.5f, 1.8f), H, 0.24f, 0.4f, Pal.RedBright);
            PlaceKit.Rug(pg, new Vector3(1.9f, 0f, 0.3f), 2.2f, 1.2f, C(150, 50, 44), C(70, 60, 56), Pal.Gold);
            Npc(sp, Keeper(57, new Color(0.3f, 0.3f, 0.34f)), new Vector3(1.9f, 0f, 0.4f), 200f, PersonPose.Idle);
            Light(sp, pg, new Vector3(-2f, H - 0.6f, 0.3f), WarmLight, 9f, 1.2f);
            Light(sp, pg, new Vector3(2.4f, H - 0.6f, 0.4f), WarmLight, 9f, 1.1f);
        }

        // ================================================================== seafood restaurant
        static void RoundTable(PGeo pg, Vector3 at, System.Random rng, bool food)
        {
            var g = pg.shell;
            Color32 cloth = C(190, 46, 40), clothD = C(150, 34, 32);
            g.Frustum(at, at + Up * 0.05f, 0.35f, 0.35f, 0.3f, 0.3f, 8, 0f, Pal.WoodDark, Pal.WoodDark, false, true);
            g.Frustum(at + Up * 0.05f, at + Up * 0.72f, 0.07f, 0.07f, 0.07f, 0.07f, 6, 0f, Pal.WoodDark, Pal.WoodDark, false, false);
            g.Frustum(at + Up * 0.5f, at + Up * 0.77f, 0.9f, 0.9f, 0.86f, 0.86f, 14, 0f, clothD, cloth, false, true);
            g.Frustum(at + Up * 0.77f, at + Up * 0.8f, 0.45f, 0.45f, 0.45f, 0.45f, 12, 0f, Pal.WoodLight, Pal.WoodLight, false, true);
            if (food)
            {
                Vector3 pl = at + Up * 0.8f;
                g.Disc(pl + Up * 0.005f, 0.26f, 10, 0f, Vector3.up, Pal.Porcelain, Pal.PorcelainBlue);
                g.Ellipsoid(pl + Up * 0.04f, new Vector3(0.2f, 0.04f, 0.08f), 7, 3, C(230, 150, 80), C(200, 110, 60));
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.1f + 0.5f;
                    Vector3 b = at + new Vector3(Mathf.Cos(a) * 0.62f, 0.775f, Mathf.Sin(a) * 0.62f);
                    g.Frustum(b, b + Up * 0.06f, 0.05f, 0.05f, 0.08f, 0.08f, 7, 0f, Pal.Porcelain, Pal.Porcelain, true, false);
                    g.Disc(b + Up * 0.05f, 0.07f, 7, 0f, Vector3.up, k == 0 ? C(240, 236, 220) : C(120, 160, 90), C(240, 236, 220));
                }
            }
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                Vector3 cp = at + new Vector3(Mathf.Cos(a) * 1.18f, 0f, Mathf.Sin(a) * 1.18f);
                PlaceKit.Chair(pg, cp, FaceYaw(cp, at), Pal.WoodDark, C(150, 46, 40));
            }
            pg.Col(at + Up * 0.4f, new Vector3(1.7f, 0.8f, 1.7f));
        }

        static Vector3 Chair(int k) { float a = k * Mathf.PI * 0.5f + 0.4f; return new Vector3(Mathf.Cos(a) * 1.18f, 0f, Mathf.Sin(a) * 1.18f); }

        static void Restaurant(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style
            {
                floor = PlaceKit.Floor.Tiles, floorA = C(236, 236, 230), floorB = C(110, 150, 196), wallLow = C(76, 126, 176), wallHigh = C(244, 242, 236),
                trim = C(60, 90, 130), post = C(90, 60, 42), ceiling = C(232, 228, 216), beam = C(120, 84, 56), wainscot = 1.1f
            };
            PlaceKit.Room(pg, W, D, H, st, 21);
            pg.ambient = new Color(0.7f, 0.66f, 0.62f);
            for (int k = -1; k <= 1; k += 2) PlaceKit.Window(pg, new Vector3(hx, 1.75f, k * 2.4f), Vector3.left, 2.6f, 1.5f, PlaceKit.View.Sea, Pal.WoodDark);
            PlaceKit.Window(pg, new Vector3(3.2f, 1.9f, hz), Vector3.back, 2.0f, 2.0f, PlaceKit.View.Sea, Pal.WoodDark, false, true);
            // fish tanks along the left wall (glowing water, little fish)
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = new Vector3(-hx + 0.5f, 0f, -2.6f + k * 2.1f);
                PG.Box2(pg.shell, c + Up * 0.4f, new Vector3(0.38f, 0.4f, 0.95f), C(60, 60, 66), C(80, 80, 86));
                pg.glow.Box(c + Up * 1.3f, new Vector3(0.34f, 0.48f, 0.92f), C(70, 160, 205));
                PG.Box2(pg.shell, c + Up * 1.82f, new Vector3(0.38f, 0.04f, 0.95f), C(60, 60, 66), C(60, 60, 66));
                for (int f = 0; f < 4; f++)
                {
                    Vector3 fp = c + new Vector3(0.36f, 1.0f + (float)rng.NextDouble() * 0.6f, -0.7f + (float)rng.NextDouble() * 1.4f);
                    pg.glow.Ellipsoid(fp, new Vector3(0.02f, 0.06f, 0.13f), 6, 3, f % 2 == 0 ? C(250, 140, 60) : C(250, 230, 210), C(220, 100, 40));
                }
                pg.Col(c + Up * 0.95f, new Vector3(0.8f, 1.9f, 1.95f));
            }
            // kitchen pass + cook at the back
            Vector3 kp = new Vector3(-2.6f, 0f, hz - 1.4f);
            PlaceKit.Counter(pg, kp, Vector3.back, 4.2f, 1.05f, 0.7f, C(76, 126, 176), C(220, 220, 214));
            PlaceKit.Shelf(pg, new Vector3(-2.6f, 0f, hz), Vector3.back, 4f, 2.2f, 0.4f, 3, rng, 2);
            sp.steamPts.Add(sp.W2(kp + new Vector3(-1f, 1.25f, 0f)));
            sp.steamPts.Add(sp.W2(kp + new Vector3(0.8f, 1.25f, 0f)));
            pg.shell.Frustum(kp + new Vector3(-1f, 1.05f, 0f), kp + new Vector3(-1f, 1.2f, 0f), 0.2f, 0.2f, 0.22f, 0.22f, 8, 0f, C(80, 80, 86), C(120, 120, 126), true, false);
            Npc(sp, Keeper(41, new Color(0.9f, 0.9f, 0.9f)), kp + new Vector3(0f, 0f, 0.8f), 180f, PersonPose.Work);
            var food = new ShopDef { id = "food_" + sp.place.id, title = sp.place.name, subtitle = "Fresh from the harbour" };
            food.items.Add(FoodItem("Steamed fish with ginger", 38, 0.15f, 10f));
            food.items.Add(FoodItem("Salt & pepper squid", 26, 0.10f, 8f));
            food.items.Add(FoodItem("Crab congee", 22, 0.08f, 8f));
            food.items.Add(TeaItem("Iced lemon tea", 10, 0.05f, 5f));
            Shop(sp, kp + new Vector3(0f, 1f, -0.9f), "Order food", food);
            // tables
            Vector3[] tables = { new Vector3(1.0f, 0f, -2.3f), new Vector3(4.2f, 0f, -2.3f), new Vector3(1.0f, 0f, 1.2f), new Vector3(4.2f, 0f, 1.4f), new Vector3(-2.6f, 0f, -1.4f) };
            for (int i = 0; i < tables.Length; i++)
            {
                bool busy = i == 1 || i == 2 || i == 4;
                RoundTable(pg, tables[i], rng, busy || i == 0);
                PlaceKit.Lantern(pg, tables[i] + Up * (H - 0.55f), H, 0.3f, 0.5f, Pal.RedBright);
                pg.Light(tables[i] + Up * (H - 0.9f), WarmLight, 3.8f, 0.35f);
                if (busy)
                {
                    sp.steamPts.Add(sp.W2(tables[i] + Up * 0.92f));
                    int n = i == 4 ? 3 : 2;
                    for (int g = 0; g < n; g++)
                    {
                        Vector3 cp = tables[i] + Chair(g);
                        Npc(sp, Guest(200 + i * 5 + g), cp, FaceYaw(cp, tables[i]), PersonPose.Sit);
                    }
                }
                else AddSeat(sp, tables[i] + Chair(2), tables[i], "Sit down", true, tables[i] + Up * 0.92f);
            }
            Jobs(sp, new Vector3(3.6f, 1f, -hz + 0.8f), sp.place.jobBoard);
            PlaceKit.JobBoard(pg, new Vector3(3.6f, 1.45f, -hz), Vector3.forward, rng);
            PlaceKit.Scroll(pg, new Vector3(-3.2f, 1.8f, -hz), Vector3.forward, 0.55f, 1.1f, '鱼', C(244, 236, 214), Pal.RedDeep);
            PlaceKit.Plant(pg, new Vector3(hx - 0.5f, 0f, hz - 0.5f), 1.1f, rng);
            PlaceKit.Plant(pg, new Vector3(-hx + 0.5f, 0f, -hz + 0.5f), 0.9f, rng);
            Npc(sp, Keeper(77, new Color(0.25f, 0.35f, 0.55f)), new Vector3(2.6f, 0f, -0.4f), 30f, PersonPose.Idle);
            Light(sp, pg, new Vector3(-2.4f, H - 0.6f, 0.6f), WarmLight, 10f, 1.2f);
            Light(sp, pg, new Vector3(2.8f, H - 0.6f, -0.4f), WarmLight, 10f, 1.1f);
        }

        // ================================================================== temple hall
        static void Statue(PGeo pg, Vector3 basePt, float s, bool gold)
        {
            var g = pg.shell;
            Color32 body = gold ? C(232, 178, 64) : C(206, 160, 70), bodyD = gold ? C(184, 132, 44) : C(160, 120, 50), robe = gold ? C(214, 150, 50) : C(190, 120, 60);
            // lotus base
            g.Frustum(basePt, basePt + Up * 0.35f * s, 1.1f * s, 1.1f * s, 1.25f * s, 1.25f * s, 14, 0f, C(170, 60, 50), C(200, 80, 64), false, true);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI * 2f / 12f;
                Vector3 pb = basePt + new Vector3(Mathf.Cos(a) * 1.15f, 0.35f, Mathf.Sin(a) * 1.15f) * s;
                PG.Petal(g, pb, pb + new Vector3(Mathf.Cos(a) * 0.35f, 0.45f, Mathf.Sin(a) * 0.35f) * s, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), 0.22f * s, C(240, 170, 170), C(250, 220, 220));
            }
            Vector3 b = basePt + Up * 0.4f * s;
            // crossed legs, body, arms, head, ushnisha
            g.Ellipsoid(b + Up * 0.25f * s, new Vector3(0.95f, 0.3f, 0.65f) * s, 10, 4, robe, Sh(robe, 0.8f));
            g.Ellipsoid(b + Up * 1.0f * s, new Vector3(0.6f, 0.75f, 0.42f) * s, 10, 5, body, bodyD);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                PG.Beam(g, b + new Vector3(sx * 0.55f, 1.4f, 0f) * s, b + new Vector3(sx * 0.62f, 0.8f, -0.2f) * s, 0.13f * s, 0.13f * s, robe);
                PG.Beam(g, b + new Vector3(sx * 0.62f, 0.8f, -0.2f) * s, b + new Vector3(sx * 0.15f, 0.62f, -0.38f) * s, 0.11f * s, 0.11f * s, body);
            }
            g.Ellipsoid(b + new Vector3(0f, 2.05f, 0f) * s, new Vector3(0.36f, 0.42f, 0.36f) * s, 9, 6, body, bodyD);
            g.Ellipsoid(b + new Vector3(0f, 2.45f, 0f) * s, new Vector3(0.18f, 0.16f, 0.18f) * s, 7, 4, bodyD, bodyD);
            for (int sx = -1; sx <= 1; sx += 2) g.Ellipsoid(b + new Vector3(sx * 0.36f, 1.95f, 0f) * s, new Vector3(0.06f, 0.2f, 0.08f) * s, 5, 3, body, bodyD);
            // halo (glow) behind
            Vector3 hc = b + new Vector3(0f, 2.05f, 0.42f) * s;
            int N = 24;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                Vector3 i0 = hc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.62f * s, i1 = hc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.62f * s;
                Vector3 o0 = hc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.78f * s, o1 = hc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.78f * s;
                pg.glow.QuadOut(i0, i1, o1, o0, Vector3.back, C(255, 210, 110), C(255, 210, 110), C(255, 180, 70), C(255, 180, 70));
            }
            Vector3 mc = b + new Vector3(0f, 1.2f, 0.5f) * s;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                Vector3 i0 = mc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 1.3f, 0f) * 1.05f * s, i1 = mc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 1.3f, 0f) * 1.05f * s;
                Vector3 o0 = mc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 1.3f, 0f) * 1.2f * s, o1 = mc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 1.3f, 0f) * 1.2f * s;
                if (Mathf.Sin(a0) < -0.6f) continue;
                pg.glow.QuadOut(i0, i1, o1, o0, Vector3.back, C(250, 190, 90), C(250, 190, 90), C(230, 150, 60), C(230, 150, 60));
            }
        }

        static void Temple(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            bool gold = sp.place.variant == 1;
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style
            {
                floor = PlaceKit.Floor.Stone, floorA = C(196, 190, 178), floorB = C(176, 170, 160), wallLow = C(140, 38, 32), wallHigh = gold ? C(190, 70, 46) : C(176, 62, 46),
                trim = C(90, 40, 30), post = C(170, 44, 36), ceiling = C(56, 118, 122), beam = gold ? C(214, 160, 60) : C(60, 100, 104), wainscot = 1.2f,
                postStep = 3.6f, beamStep = 2.4f
            };
            PlaceKit.Room(pg, W, D, H, st, 31);
            pg.ambient = new Color(0.52f, 0.44f, 0.38f);
            sp.ambSky = new Color(0.45f, 0.36f, 0.3f); sp.ambEq = new Color(0.42f, 0.32f, 0.26f);
            // coffered ceiling: gold grid
            for (float x = -hx + 2f; x < hx - 1f; x += 2f) PG.Box2(pg.shell, new Vector3(x, H - 0.06f, 0f), new Vector3(0.05f, 0.05f, hz), Pal.Gold, Pal.Gold);
            for (float z = -hz + 2f; z < hz - 1f; z += 2f) PG.Box2(pg.shell, new Vector3(0f, H - 0.06f, z), new Vector3(hx, 0.05f, 0.05f), Pal.Gold, Pal.Gold);
            // two rows of great red columns
            float cx = hx - 3.2f;
            for (float z = -hz + 3.2f; z < hz - 3f; z += 3.4f)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 b = new Vector3(s * cx, 0f, z);
                    PG.Box2(pg.shell, b + Up * 0.15f, new Vector3(0.45f, 0.15f, 0.45f), Pal.StoneDark, Pal.Stone);
                    PG.Column(pg.shell, b, b + Up * H, 0.3f, st.post, 10);
                    PG.Box2(pg.shell, b + Up * (H - 0.4f), new Vector3(0.42f, 0.2f, 0.42f), st.beam, st.beam);
                    pg.Col(b + Up * (H * 0.5f), new Vector3(0.7f, H, 0.7f));
                    // silk banner between the columns
                    Color32 bc = s > 0 ? C(240, 196, 60) : C(200, 50, 44);
                    PG.OBox(pg.shell, b + new Vector3(-s * 0.55f, H - 2.3f, 0f), new Vector3(0.24f, 1.4f, 0.01f), Quaternion.identity, bc);
                    PlaceKit.Glyph(pg.shell, b + new Vector3(-s * 0.55f, H - 1.9f, -0.015f), Vector3.left, Up, 0.36f, 0.025f, s > 0 ? '福' : '禅', s > 0 ? Pal.RedDeep : Pal.Gold);
                    PlaceKit.Glyph(pg.shell, b + new Vector3(-s * 0.55f, H - 1.9f, 0.015f), Vector3.right, Up, 0.36f, 0.025f, s > 0 ? '福' : '禅', s > 0 ? Pal.RedDeep : Pal.Gold);
                }
            // altar + statue
            Vector3 ap = new Vector3(0f, 0f, hz - 2.4f);
            PG.Box2(pg.shell, ap + Up * 0.35f, new Vector3(3.2f, 0.35f, 1.7f), Pal.StoneDark, Pal.StoneLight);
            PG.Box2(pg.shell, ap + Up * 0.78f, new Vector3(2.4f, 0.08f, 1.3f), C(150, 40, 34), C(170, 50, 40));
            Statue(pg, ap + Up * 0.86f, gold ? 1.45f : 1.15f, gold);
            pg.Col(ap + Up * 2.2f, new Vector3(6.4f, 4.4f, 3.4f));
            // offering table: fruit, candles, a small censer
            Vector3 op = new Vector3(0f, 0f, hz - 4.6f);
            PlaceKit.LowTable(pg, op, 2.6f, 0.9f, 0.92f, C(150, 40, 34));
            for (int k = 0; k < 3; k++)
            {
                Vector3 bp = op + new Vector3(-0.8f + k * 0.8f, 0.92f, 0f);
                pg.shell.Disc(bp + Up * 0.01f, 0.18f, 8, 0f, Vector3.up, Pal.Porcelain, Pal.PorcelainBlue);
                for (int f = 0; f < 4; f++) pg.shell.Ellipsoid(bp + new Vector3(Mathf.Cos(f * 1.6f) * 0.07f, 0.07f + (f == 3 ? 0.08f : 0f), Mathf.Sin(f * 1.6f) * 0.07f), new Vector3(0.06f, 0.06f, 0.06f), 6, 3, C(250, 150, 50), C(220, 110, 30));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 cb = op + new Vector3(s * 1.15f, 0.92f, 0f);
                pg.shell.Frustum(cb, cb + Up * 0.35f, 0.045f, 0.045f, 0.045f, 0.045f, 6, 0f, C(200, 40, 40), C(220, 60, 50), false, true);
                pg.glow.Ellipsoid(cb + Up * 0.4f, new Vector3(0.02f, 0.05f, 0.02f), 4, 3, C(255, 220, 120), C(255, 150, 60));
                pg.Light(cb + Up * 0.5f, WarmLight, 3f, 0.5f);
            }
            // candle walls on both sides near the altar
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 wp = new Vector3(s * hx, 0f, hz - 4.6f);
                PlaceKit.CandleWall(pg, wp, s > 0 ? Vector3.left : Vector3.right, 3.2f, 4, rng);
                pg.Light(wp + new Vector3(-s * 0.4f, 1.3f, 0f), WarmLight, 4f, 0.8f);
            }
            // cushions in rows; monks praying
            int mi = 0;
            Seat medit = null;
            for (int r = 0; r < 2; r++)
                for (int k = 0; k < 4; k++)
                {
                    Vector3 cp = new Vector3(-2.4f + k * 1.6f, 0f, hz - 6.4f - r * 1.5f);
                    PlaceKit.Cushion(pg, cp, 0.36f, r == 0 ? C(220, 160, 50) : C(180, 60, 50));
                    if ((r == 0 && k != 2) && mi < 3) { Npc(sp, Monk(300 + mi), cp, 0f, PersonPose.Pray); mi++; }
                    else if (r == 1 && k == 1 && medit == null)
                    {
                        medit = new Seat { local = cp, yawLocal = 0f, focusLocal = new Vector3(0f, 2.4f, hz - 2.4f), standLocal = cp - new Vector3(0f, 0f, 0.9f) };
                        var m = medit;
                        Act(sp, cp + Up * 0.8f, "Sit and meditate", "", () => Interiors.Instance.Sit(sp, m), 1.2f);
                    }
                }
            // great incense burner in front
            Vector3 ip = new Vector3(0f, 0f, -hz + 4.6f);
            pg.Add(PropShapes.IncenseBurner(rng), ip, 0f);
            pg.Col(ip + Up * 0.75f, new Vector3(1.7f, 1.5f, 1.7f));
            sp.incensePt = sp.W2(ip + Up * 1.9f);
            {
                var sticks = new LifeGeo();
                for (int k = 0; k < 3; k++)
                {
                    Vector3 sb = new Vector3(-0.12f + k * 0.12f, 1.35f, 0f);
                    PG.Rod(sticks, sb, sb + new Vector3(0f, 0.45f, 0.02f * k), 0.012f, C(150, 60, 40), 4);
                    sticks.Ellipsoid(sb + new Vector3(0f, 0.46f, 0.02f * k), new Vector3(0.018f, 0.025f, 0.018f), 4, 2, C(255, 120, 60), C(255, 80, 40));
                }
                sp.incenseStick = MeshObj(sp.root, "IncenseSticks", sticks, PlaceMats.Glow, ip);
                sp.incenseStick.SetActive(false);
            }
            Act(sp, ip + new Vector3(0f, 1f, -1.2f), "Light incense", Wallet.Format(5) + "  ·  a blessing for 10 min", () => Interiors.Instance.LightIncense(sp), 1.8f);
            // bell on a frame (front left)
            Vector3 bf = new Vector3(-hx + 1.5f, 0f, -hz + 3.4f);
            float bh = Mathf.Min(H - 0.8f, 3.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(pg.shell, bf + new Vector3(s * 1.0f, bh * 0.5f, 0f), new Vector3(0.12f, bh * 0.5f, 0.12f), Pal.RedDeep, Pal.Red);
                pg.Col(bf + new Vector3(s * 1.0f, bh * 0.5f, 0f), new Vector3(0.3f, bh, 0.3f));
            }
            PG.Box2(pg.shell, bf + Up * bh, new Vector3(1.35f, 0.14f, 0.16f), Pal.WoodDark, Pal.Wood);
            pg.Col(bf + Up * (bh - 1.0f), new Vector3(1.6f, 1.8f, 1.6f));
            sp.bell = MeshObj(sp.root, "Bell", PropShapes.Bell(), PlaceMats.Shell, bf + Up * (bh - 0.14f)).transform;
            sp.striker = MeshObj(sp.root, "Striker", PropShapes.StrikerLog(), PlaceMats.Shell, bf + new Vector3(0f, bh - 0.14f, -1.6f)).transform;
            PG.Box2(pg.shell, bf + new Vector3(0f, bh, -1.6f), new Vector3(0.35f, 0.08f, 0.08f), Pal.WoodDark, Pal.WoodDark);
            PG.Rod(pg.shell, bf + new Vector3(0f, bh, -1.6f), bf + new Vector3(0f, H, -1.6f), 0.03f, Pal.WoodDark, 4);
            Act(sp, bf + new Vector3(1.4f, 1f, -0.6f), "Ring the bell", "", () => Interiors.Instance.RingBell(sp), 2f);
            Npc(sp, Monk(320), bf + new Vector3(1.4f, 0f, 1.2f), 230f, PersonPose.Idle);
            // wishing tree + wish box (front right)
            Vector3 wt = new Vector3(hx - 1.2f, 0f, -hz + 3.0f);
            PlaceKit.Plant(pg, wt, 1.9f, rng, true);
            for (int k = 0; k < 14; k++)
            {
                float a = k * 2.4f, r = 0.25f + 0.3f * (k % 3) / 2f;
                Vector3 rp = wt + new Vector3(Mathf.Cos(a) * r, 1.35f + 0.25f * (k % 4), Mathf.Sin(a) * r);
                PG.OBox(pg.glow, rp - Up * 0.15f, new Vector3(0.03f, 0.13f, 0.004f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), C(220, 40, 40));
            }
            Vector3 wb = wt + new Vector3(0f, 0f, 1.5f);
            PG.Box2(pg.shell, wb + Up * 0.45f, new Vector3(0.4f, 0.45f, 0.3f), Pal.RedDeep, Pal.Gold);
            PlaceKit.Glyph(pg.shell, wb + new Vector3(0f, 0.5f, -0.31f), Vector3.right, Up, 0.42f, 0.03f, '福', Pal.Gold);
            pg.Col(wb + Up * 0.45f, new Vector3(0.8f, 0.9f, 0.6f));
            Act(sp, wb + new Vector3(-0.9f, 1f, 0f), "Make a wish", Wallet.Format(10) + " coin  ·  luck for 15 min", () => Interiors.Instance.MakeWish(sp), 1.6f);
            // viewpoint stairs (back right corner)
            if (sp.place.HasViewpoint)
            {
                Vector3 s0 = new Vector3(hx - 0.9f, 0f, -1.9f);
                for (int k = 0; k < 10; k++)
                {
                    Vector3 sc = s0 + new Vector3(0f, 0.18f + k * 0.34f, 0.3f * k);
                    PG.Box2(pg.shell, sc, new Vector3(0.6f, 0.05f, 0.16f), Pal.Wood, Pal.WoodLight);
                }
                for (int s = -1; s <= 1; s += 2) PG.Beam(pg.shell, s0 + new Vector3(s * 0.62f, 0.9f, -0.1f), s0 + new Vector3(s * 0.62f, 4.3f, 3f), 0.04f, 0.04f, Pal.RedDeep);
                PG.Box2(pg.shell, s0 + new Vector3(0f, Mathf.Min(H - 0.3f, 3.6f), 3.4f), new Vector3(0.7f, 0.06f, 0.6f), Pal.WoodDark, Pal.Wood);
                pg.Col(s0 + new Vector3(0f, 1.8f, 1.6f), new Vector3(1.4f, 3.6f, 3.4f));
                Act(sp, s0 + new Vector3(-0.9f, 1f, -0.3f), "Climb to the viewpoint", "", () => Interiors.Instance.Viewpoint(sp.place), 1.6f);
            }
            // windows, lanterns, scrolls
            for (int s = -1; s <= 1; s += 2)
                PlaceKit.Window(pg, new Vector3(s * hx, 3.2f, -1.6f), s > 0 ? Vector3.left : Vector3.right, 2.0f, 1.8f, gold ? PlaceKit.View.Peaks : PlaceKit.View.Garden, Pal.WoodDark);
            for (float z = -hz + 2.5f; z < hz - 4f; z += 3.4f) PlaceKit.Lantern(pg, new Vector3(0f, H - 1.2f, z), H, 0.42f, 0.7f, (int)z % 2 == 0 ? Pal.RedBright : C(246, 190, 60));
            PlaceKit.Scroll(pg, new Vector3(-2.8f, 2.2f, -hz), Vector3.forward, 0.7f, 1.6f, '禅', C(244, 236, 214), Pal.Ink);
            PlaceKit.Scroll(pg, new Vector3(2.8f, 2.2f, -hz), Vector3.forward, 0.7f, 1.6f, '福', C(244, 236, 214), Pal.RedDeep);
            PlaceKit.SignPanel(pg, new Vector3(0f, H - 1.4f, hz - 0.08f), Vector3.back, 1.6f, 1.0f, sp.place.glyph, C(40, 60, 110), Pal.Gold);
            Light(sp, pg, new Vector3(0f, 4.2f, hz - 5f), new Color(1f, 0.82f, 0.5f), gold ? 12f : 10f, 1.5f, 0.9f);
            Light(sp, pg, new Vector3(0f, H - 1.6f, -hz + 4f), WarmLight, 11f, 1.1f);
        }

        // ================================================================== car showroom
        static readonly string[] ShowCars = { "CAR-030", "CAR-029", "CAR-022", "CAR-028" };

        static void Showroom(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            Color32 jade = C(40, 190, 150);
            var st = new PlaceKit.Style
            {
                floor = PlaceKit.Floor.Polished, floorA = C(226, 228, 232), floorB = C(214, 216, 222), wallLow = C(60, 64, 72), wallHigh = C(240, 242, 244),
                trim = C(70, 74, 82), ceiling = C(244, 244, 246), wainscot = 0.15f, posts = false, beams = false, door = false
            };
            PlaceKit.Room(pg, W, D, H, st, 41);
            pg.ambient = new Color(0.86f, 0.86f, 0.88f);
            sp.ambSky = new Color(0.85f, 0.85f, 0.9f); sp.ambEq = new Color(0.75f, 0.74f, 0.74f); sp.ambGround = new Color(0.45f, 0.44f, 0.44f);
            // glass storefront with the street at dusk behind it
            PlaceKit.Paint(pg.glow, new Vector3(0f, 2.9f, -hz + 0.015f), Vector3.right, Up, W - 0.4f, 5.4f, PlaceKit.View.City, false);
            {
                Vector3 a = new Vector3(-hx + 0.2f, 0.1f, -hz + 0.25f), b = new Vector3(hx - 0.2f, 0.1f, -hz + 0.25f);
                Color32 gc = new Color32(255, 255, 255, 255);
                pg.glass.QuadOut(a, b, b + Up * (H - 0.4f), a + Up * (H - 0.4f), Vector3.forward, gc, gc, gc, gc);
                for (int i = 0; i <= 9; i++)
                {
                    float x = Mathf.Lerp(-hx + 0.2f, hx - 0.2f, i / 9f);
                    PG.Box2(pg.shell, new Vector3(x, H * 0.5f, -hz + 0.3f), new Vector3(0.05f, H * 0.5f, 0.06f), C(56, 60, 68), C(56, 60, 68));
                }
                PG.Box2(pg.shell, new Vector3(0f, 3.2f, -hz + 0.3f), new Vector3(hx, 0.05f, 0.06f), C(56, 60, 68), C(56, 60, 68));
                for (int s = -1; s <= 1; s += 2) PG.Box2(pg.shell, new Vector3(s * 0.15f, 1.1f, -hz + 0.38f), new Vector3(0.02f, 0.4f, 0.02f), C(220, 220, 226), C(220, 220, 226));
            }
            // ceiling light strips
            for (int k = 0; k < 4; k++)
            {
                float z = -hz + 2.2f + k * (D - 4.4f) / 3f;
                pg.glow.Box(new Vector3(0f, H - 0.03f, z), new Vector3(hx - 1.5f, 0.02f, 0.14f), C(255, 252, 240));
                for (int s = -1; s <= 1; s += 2) pg.Light(new Vector3(s * 5.5f, H - 0.5f, z), new Color(1f, 0.97f, 0.9f), 5f, 0.25f);
            }
            // podiums with real cars
            Vector3[] pods = { new Vector3(-6f, 0f, -2.6f), new Vector3(6f, 0f, -2.6f), new Vector3(-6f, 0f, 3.2f), new Vector3(6f, 0f, 3.2f) };
            for (int i = 0; i < pods.Length; i++)
            {
                Vector3 pc = pods[i];
                pg.shell.Frustum(pc, pc + Up * 0.26f, 2.7f, 2.7f, 2.6f, 2.6f, 20, 0f, C(200, 202, 206), C(246, 246, 248), false, true);
                pg.glow.Frustum(pc + Up * 0.06f, pc + Up * 0.1f, 2.72f, 2.72f, 2.72f, 2.72f, 20, 0f, jade, jade, false, false);
                pg.Light(pc + Up * 2.5f, new Color(1f, 1f, 1f), 3.5f, 0.35f);
                pg.Col(pc + Up * 0.9f, new Vector3(5f, 1.8f, 5f));
                float yaw = (pc.x < 0f ? 1f : -1f) * (pc.z < 0f ? 145f : 35f);
                string id = ShowCars[i];
                var car = ExteriorKit.DisplayCar(id, sp.W2(pc + Up * 0.27f), sp.Yaw(yaw), sp.root, false);
                if (car == null) ExteriorKit.CarShape(pg.shell, pc + Up * 0.26f, yaw, PropShapes.Silks[i]);
                var spec = FindSpec(id);
                string nm = spec != null ? spec.name : id;
                Showroom(sp, pc + new Vector3(pc.x < 0f ? 2.9f : -2.9f, 1f, 0f), "Look at " + nm, spec != null ? spec.carClass + "  ·  " + spec.era : "");
            }
            // reception desk + brand wall
            Vector3 dp = new Vector3(0f, 0f, hz - 2.6f);
            PlaceKit.Counter(pg, dp, Vector3.back, 3.8f, 1.05f, 0.8f, C(244, 244, 246), C(60, 64, 72));
            pg.glow.Box(dp + new Vector3(0f, 0.55f, -0.42f), new Vector3(1.85f, 0.03f, 0.01f), jade);
            Npc(sp, SalesLook(), dp + new Vector3(0f, 0f, 0.85f), 180f, PersonPose.Work);
            Showroom(sp, dp + new Vector3(0f, 1f, -1.1f), "Browse cars", "Prices, colours and test the stock");
            pg.glow.Box(new Vector3(0f, 3.3f, hz - 0.05f), new Vector3(hx - 2f, 0.07f, 0.02f), jade);
            PlaceKit.SignPanel(pg, new Vector3(0f, 4.3f, hz - 0.1f), Vector3.back, 1.8f, 1.4f, '车', C(30, 34, 44), C(120, 255, 220));
            // lounge corner
            Vector3 lp = new Vector3(hx - 2.6f, 0f, hz - 2.2f);
            PlaceKit.Sofa(pg, lp + new Vector3(0f, 0f, 1.0f), 180f, 2.2f, C(60, 64, 72), C(200, 200, 206));
            PlaceKit.LowTable(pg, lp + new Vector3(0f, 0f, -0.3f), 1.2f, 0.7f, 0.42f, C(240, 240, 242));
            var lounge = new Seat { local = lp + new Vector3(0f, 0f, 1.0f), yawLocal = 180f, focusLocal = new Vector3(6f, 1f, 3.2f), standLocal = lp + new Vector3(0f, 0f, -1.2f) };
            Act(sp, lp + new Vector3(0f, 0.8f, 0.4f), "Sit in the lounge", "", () => Interiors.Instance.Sit(sp, lounge), 1.4f);
            for (int s = -1; s <= 1; s += 2) PlaceKit.Plant(pg, new Vector3(s * (hx - 0.6f), 0f, -hz + 1.0f), 1.3f, rng);
            PlaceKit.Plant(pg, new Vector3(-hx + 0.6f, 0f, hz - 0.6f), 1.4f, rng);
            Npc(sp, Guest(410), pods[0] + new Vector3(2.5f, 0f, 1.6f), 230f, PersonPose.Idle);
            Npc(sp, Guest(411), pods[0] + new Vector3(3.1f, 0f, 0.9f), 260f, PersonPose.Idle);
            Npc(sp, Guest(412, 4), pods[3] + new Vector3(-2.8f, 0f, -1.4f), 60f, PersonPose.Idle);
            Light(sp, pg, new Vector3(-5f, H - 0.7f, 0.2f), new Color(1f, 0.95f, 0.88f), 14f, 1.3f, 0.3f);
            Light(sp, pg, new Vector3(5f, H - 0.7f, 0.2f), new Color(1f, 0.95f, 0.88f), 14f, 1.3f, 0.3f);
        }

        static PersonLook SalesLook()
        {
            var l = PersonLook.Random(404);
            l.outfit = 4; l.top = new Color(0.16f, 0.2f, 0.26f); l.bottom = new Color(0.14f, 0.15f, 0.18f); l.accent = new Color(0.2f, 0.75f, 0.6f);
            return l;
        }

        static CarSpec FindSpec(string id)
        {
            foreach (var c in CarCatalog.Cars) if (c.id == id) return c;
            return null;
        }

        // ================================================================== scooter shop
        static void BikeShop(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style
            {
                floor = PlaceKit.Floor.Concrete, floorA = C(170, 170, 166), floorB = C(160, 160, 156), wallLow = C(60, 140, 170), wallHigh = C(242, 240, 232),
                trim = C(50, 110, 140), ceiling = C(226, 226, 220), beam = C(80, 84, 90), posts = false, wainscot = 1.0f
            };
            PlaceKit.Room(pg, W, D, H, st, 51);
            pg.ambient = new Color(0.72f, 0.7f, 0.66f);
            PlaceKit.Window(pg, new Vector3(hx, 1.8f, 1.2f), Vector3.left, 3f, 1.5f, PlaceKit.View.Sea, C(50, 110, 140), false);
            // scooters on display
            Color32[] cols = { C(240, 120, 80), C(110, 200, 190), C(250, 220, 90), C(230, 90, 120), C(120, 150, 230) };
            Vector3[] spots = { new Vector3(1.6f, 0f, -2.2f), new Vector3(3.6f, 0f, -2.2f), new Vector3(1.6f, 0f, 0.4f), new Vector3(3.6f, 0f, 0.4f), new Vector3(-1.2f, 0f, -1.0f) };
            for (int i = 0; i < spots.Length; i++)
            {
                float yaw = i == 4 ? 30f : (i % 2 == 0 ? -30f : 30f);
                if (i == 4) PG.Box2(pg.shell, spots[i] + Up * 0.12f, new Vector3(1.2f, 0.12f, 1.2f), C(60, 64, 72), C(80, 84, 92));
                pg.Add(ExteriorKit.Scooter(cols[i], C(40, 40, 44)), spots[i] + (i == 4 ? Up * 0.24f : Vector3.zero), yaw);
                pg.Col(spots[i] + Up * 0.55f, new Vector3(0.7f, 1.1f, 1.6f), yaw);
                Showroom(sp, spots[i] + new Vector3(-0.9f, 1f, -0.6f), "Look at the scooter", "Prices at the counter");
            }
            // bicycles along the left wall
            for (int k = 0; k < 4; k++)
            {
                var bike = new PropModel();
                PG.Append(bike[PK.Props], PropShapes.Bicycle(PropShapes.Silks[(k * 3) % PropShapes.Silks.Length], k % 2 == 0, rng), Matrix4x4.identity);
                pg.Add(bike, new Vector3(-hx + 0.45f, 0f, -3.2f + k * 0.9f), 90f);
            }
            pg.Col(new Vector3(-hx + 0.45f, 0.5f, -1.8f), new Vector3(0.7f, 1f, 3.8f));
            // workbench + pegboard + mechanic (back left)
            Vector3 wb = new Vector3(-hx + 1.0f, 0f, hz - 1.8f);
            PlaceKit.Counter(pg, wb, Vector3.right, 2.4f, 0.95f, 0.8f, C(90, 94, 100), C(140, 110, 80));
            PG.Box2(pg.shell, new Vector3(-hx + 0.03f, 1.8f, hz - 1.8f), new Vector3(0.02f, 0.6f, 1.2f), C(196, 170, 130), C(196, 170, 130));
            for (int k = 0; k < 8; k++)
            {
                Vector3 tp = new Vector3(-hx + 0.08f, 1.5f + (k % 2) * 0.45f, hz - 2.8f + (k / 2) * 0.5f);
                PG.Box2(pg.shell, tp, new Vector3(0.02f, 0.14f, 0.025f), k % 3 == 0 ? Pal.Red : C(60, 60, 66), C(180, 180, 186));
            }
            Npc(sp, Keeper(61, new Color(0.2f, 0.3f, 0.45f)), wb + new Vector3(0.9f, 0f, 0f), 270f, PersonPose.Work);
            // shop counter + helmets
            Vector3 cp = new Vector3(1.6f, 0f, hz - 1.6f);
            PlaceKit.Counter(pg, cp, Vector3.back, 3f, 1.05f, 0.7f, C(60, 140, 170), C(240, 240, 236));
            PlaceKit.Shelf(pg, new Vector3(1.6f, 0f, hz), Vector3.back, 3.2f, 2.2f, 0.4f, 3, rng, 3);
            Npc(sp, Keeper(62, new Color(0.25f, 0.55f, 0.66f)), cp + new Vector3(0f, 0f, 0.8f), 180f, PersonPose.Work);
            Showroom(sp, cp + new Vector3(0f, 1f, -1.0f), "Browse bikes", "Scooters and motorbikes");
            // tyres, oil drum, sign
            for (int k = 0; k < 4; k++)
            {
                Vector3 tp = new Vector3(hx - 0.6f, 0.12f + k * 0.2f, hz - 0.6f);
                pg.shell.Frustum(tp - Up * 0.1f, tp + Up * 0.1f, 0.34f, 0.34f, 0.34f, 0.34f, 10, 0f, C(40, 40, 44), C(54, 54, 58), true, true);
            }
            pg.Col(new Vector3(hx - 0.6f, 0.45f, hz - 0.6f), new Vector3(0.7f, 0.9f, 0.7f));
            pg.shell.Frustum(new Vector3(hx - 0.6f, 0f, -hz + 0.7f), new Vector3(hx - 0.6f, 0.9f, -hz + 0.7f), 0.3f, 0.3f, 0.3f, 0.3f, 10, 0f, C(40, 110, 170), C(60, 130, 190), false, true);
            pg.Col(new Vector3(hx - 0.6f, 0.45f, -hz + 0.7f), new Vector3(0.6f, 0.9f, 0.6f));
            PlaceKit.SignPanel(pg, new Vector3(-1.4f, 2.8f, hz - 0.08f), Vector3.back, 1.0f, 0.8f, '车', C(250, 250, 246), C(30, 120, 160), true);
            Npc(sp, Guest(520), new Vector3(2.6f, 0f, -0.9f), 160f, PersonPose.Idle);
            Light(sp, pg, new Vector3(0f, H - 0.5f, 0f), new Color(1f, 0.92f, 0.8f), 11f, 1.3f, 0.6f);
        }

        // ================================================================== ski lodge
        static void SkiLodge(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style
            {
                floorA = C(150, 104, 66), floorB = C(132, 90, 56), wallLow = C(110, 72, 46), wallHigh = C(160, 112, 70),
                trim = C(86, 56, 36), post = C(100, 66, 42), ceiling = C(140, 98, 62), beam = C(90, 60, 40), wainscot = 0.6f
            };
            PlaceKit.Room(pg, W, D, H, st, 61);
            pg.ambient = new Color(0.56f, 0.46f, 0.38f);
            sp.ambSky = new Color(0.5f, 0.4f, 0.32f);
            // log courses on the walls
            for (float y = 0.75f; y < H - 0.2f; y += 0.36f)
            {
                Color32 lc = Sh(st.wallHigh, 0.82f + 0.1f * Mathf.Sin(y * 7f));
                PG.Beam(pg.shell, new Vector3(-hx + 0.04f, y, -hz), new Vector3(-hx + 0.04f, y, hz), 0.05f, 0.05f, lc);
                PG.Beam(pg.shell, new Vector3(hx - 0.04f, y, -hz), new Vector3(hx - 0.04f, y, hz), 0.05f, 0.05f, lc);
                PG.Beam(pg.shell, new Vector3(-hx, y, hz - 0.04f), new Vector3(hx, y, hz - 0.04f), 0.05f, 0.05f, lc);
                PG.Beam(pg.shell, new Vector3(-hx, y, -hz + 0.04f), new Vector3(hx, y, -hz + 0.04f), 0.05f, 0.05f, lc);
            }
            // stone fireplace on the back wall with a crackling fire
            Vector3 fp = new Vector3(0f, 0f, hz);
            {
                var g = pg.shell;
                int s0 = g.v.Count;
                PG.Box2(g, fp + new Vector3(0f, H * 0.5f, -0.5f), new Vector3(1.5f, H * 0.5f, 0.5f), C(150, 146, 140), C(160, 156, 150));
                for (int k = 0; k < 18; k++)
                {
                    Vector3 sc = fp + new Vector3(-1.2f + (k % 5) * 0.6f + ((k / 5) % 2) * 0.3f, 0.4f + (k / 5) * 0.95f, -1.01f);
                    PG.Box2(g, sc, new Vector3(0.26f, 0.2f, 0.02f), Vary(C(170, 166, 156), k), C(170, 166, 156));
                }
                PG.Box2(g, fp + new Vector3(0f, 0.6f, -1.0f), new Vector3(0.85f, 0.55f, 0.03f), C(30, 24, 22), C(30, 24, 22));
                PG.Box2(g, fp + new Vector3(0f, 1.35f, -1.15f), new Vector3(1.7f, 0.08f, 0.22f), Pal.WoodDark, Pal.Wood);
                PG.Box2(g, fp + new Vector3(0f, 0.06f, -1.4f), new Vector3(1.4f, 0.06f, 0.45f), C(120, 116, 110), C(140, 136, 130));
                for (int k = 0; k < 3; k++)
                    g.Frustum(fp + new Vector3(-0.45f, 0.18f + k * 0.04f, -0.75f + k * 0.08f), fp + new Vector3(0.45f, 0.2f + k * 0.03f, -0.7f), 0.08f, 0.08f, 0.08f, 0.08f, 6, 0f, Pal.WoodDark, Pal.Wood, true, true);
                for (int k = 0; k < 5; k++)
                {
                    float x = -0.4f + k * 0.2f, h = 0.35f + 0.25f * Mathf.Abs(Mathf.Sin(k * 1.9f));
                    pg.glow.Frustum(fp + new Vector3(x, 0.2f, -0.72f), fp + new Vector3(x * 0.8f, 0.2f + h, -0.72f), 0.12f, 0.06f, 0.0f, 0.0f, 5, 0f, C(255, 130, 40), C(255, 230, 120), true, false);
                }
                for (int k = 0; k < 4; k++)
                {
                    Vector3 mug = fp + new Vector3(-1.2f + k * 0.25f, 1.43f, -1.15f);
                    g.Frustum(mug, mug + Up * 0.1f, 0.04f, 0.04f, 0.045f, 0.045f, 6, 0f, PropShapes.Silks[k], PropShapes.Silks[k], true, false);
                }
                pg.glow.Frustum(fp + new Vector3(1.1f, 1.43f, -1.15f), fp + new Vector3(1.1f, 1.6f, -1.15f), 0.04f, 0.04f, 0.04f, 0.04f, 6, 0f, C(255, 240, 210), C(255, 230, 190), false, true);
                pg.Col(fp + new Vector3(0f, H * 0.5f, -0.6f), new Vector3(3.2f, H, 1.4f));
                sp.embers.Add(sp.W2(fp + new Vector3(0f, 0.35f, -0.72f)));
                pg.Light(fp + new Vector3(0f, 0.6f, -1.6f), new Color(1f, 0.55f, 0.25f), 6.5f, 1.3f);
            }
            {
                var go = new GameObject("FireLight");
                go.transform.SetParent(sp.root, false);
                go.transform.localPosition = fp + new Vector3(0f, 0.7f, -1.6f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.25f); l.range = 7.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
                sp.lights.Add(l); sp.lightBase.Add(1.6f); sp.fireLight = l;
            }
            // rug, sofas, armchair (seat), coffee table
            Vector3 rc = new Vector3(0f, 0f, hz - 3.6f);
            PlaceKit.Rug(pg, rc, 3.6f, 2.6f, C(150, 46, 40), C(220, 200, 160), C(60, 100, 80));
            PlaceKit.Sofa(pg, rc + new Vector3(-1.9f, 0f, 0f), 90f, 2.2f, C(110, 70, 46), C(200, 60, 50));
            PlaceKit.Sofa(pg, rc + new Vector3(0f, 0f, -1.6f), 0f, 2.4f, C(110, 70, 46), C(60, 100, 80));
            PlaceKit.LowTable(pg, rc, 1.1f, 0.7f, 0.42f, Pal.Wood);
            for (int k = 0; k < 2; k++)
            {
                Vector3 mug = rc + new Vector3(-0.2f + k * 0.35f, 0.42f, 0.1f);
                pg.shell.Frustum(mug, mug + Up * 0.1f, 0.045f, 0.045f, 0.05f, 0.05f, 6, 0f, C(240, 236, 226), C(240, 236, 226), true, false);
                pg.shell.Disc(mug + Up * 0.095f, 0.042f, 6, 0f, Vector3.up, C(110, 66, 40), C(110, 66, 40));
                sp.steamPts.Add(sp.W2(mug + Up * 0.14f));
            }
            Npc(sp, Guest(610, 5), rc + new Vector3(-1.85f, 0f, 0.4f), 90f, PersonPose.Sit);
            Npc(sp, Guest(611, 5), rc + new Vector3(-1.85f, 0f, -0.4f), 90f, PersonPose.Sit);
            Vector3 arm = rc + new Vector3(1.8f, 0f, 0.1f);
            PlaceKit.Chair(pg, arm, -90f, Pal.WoodDark, C(200, 60, 50));
            AddSeat(sp, arm, fp + new Vector3(0f, 0f, -0.9f), "Sit by the fire", true, rc + new Vector3(0.3f, 0.55f, 0.1f));
            // hot drinks bar on the left wall
            Vector3 cp = new Vector3(-hx + 1.5f, 0f, -0.6f);
            PlaceKit.Counter(pg, cp, Vector3.right, 3.4f, 1.05f, 0.7f, C(110, 72, 46), C(170, 130, 90));
            PlaceKit.Shelf(pg, new Vector3(-hx, 0f, -0.6f), Vector3.right, 3.2f, 2.2f, 0.4f, 3, rng, 2);
            sp.steamPts.Add(sp.W2(cp + new Vector3(0.1f, 1.25f, 0.8f)));
            pg.shell.Frustum(cp + new Vector3(0.1f, 1.05f, 0.8f), cp + new Vector3(0.1f, 1.22f, 0.8f), 0.12f, 0.12f, 0.1f, 0.1f, 8, 0f, C(150, 150, 156), C(190, 190, 196), false, true);
            Npc(sp, Keeper(66, new Color(0.6f, 0.2f, 0.18f)), cp + new Vector3(-0.75f, 0f, 0f), 90f, PersonPose.Work);
            Npc(sp, Guest(612, 5), cp + new Vector3(1.0f, 0f, 1.1f), 250f, PersonPose.Idle);
            var drinks = new ShopDef { id = "drinks_" + sp.place.id, title = sp.place.name, subtitle = "Warm up by the fire" };
            drinks.items.Add(Item("Hot chocolate", "+10% job pay for 6 min", 14, () => { Buffs.WarmDrink("Hot chocolate", 0.10f, 6f); PlacesManager.Toast("Hot chocolate - +10% job pay for 6 min"); }));
            drinks.items.Add(TeaItem("Ginger tea", 12, 0.08f, 6f));
            drinks.items.Add(FoodItem("Sweet potato soup", 18, 0.12f, 8f));
            drinks.items.Add(FoodItem("Roasted chestnuts", 9, 0.05f, 5f));
            Shop(sp, cp + new Vector3(0.9f, 1f, 0f), "Buy a hot drink", drinks);
            // skis by the door, windows with the summit
            for (int k = 0; k < 6; k++)
            {
                Vector3 sb = new Vector3(hx - 0.25f, 0f, -hz + 1.2f + k * 0.28f);
                PG.OBox(pg.shell, sb + Up * 0.85f, new Vector3(0.015f, 0.85f, 0.05f), Quaternion.Euler(0f, 0f, 8f), PropShapes.Silks[(k * 2) % PropShapes.Silks.Length]);
            }
            pg.Col(new Vector3(hx - 0.3f, 0.9f, -hz + 1.9f), new Vector3(0.5f, 1.8f, 1.9f));
            PlaceKit.Window(pg, new Vector3(hx, 1.8f, 1.6f), Vector3.left, 2.6f, 1.6f, PlaceKit.View.Snow, Pal.WoodDark);
            for (int s = -1; s <= 1; s += 2) PlaceKit.Window(pg, new Vector3(s * 3.6f, 2.0f, hz), Vector3.back, 1.6f, 1.6f, PlaceKit.View.Snow, Pal.WoodDark);
            Jobs(sp, new Vector3(-3.2f, 1f, -hz + 0.8f), sp.place.jobBoard);
            PlaceKit.JobBoard(pg, new Vector3(-3.2f, 1.45f, -hz), Vector3.forward, rng);
            PlaceKit.SignPanel(pg, new Vector3(3.2f, 2.2f, -hz + 0.08f), Vector3.forward, 0.8f, 0.8f, '暖', C(150, 40, 34), Pal.Gold);
            for (int k = 0; k < 3; k++) PlaceKit.Lantern(pg, new Vector3(-3f + k * 3f, H - 0.5f, -1.2f), H, 0.26f, 0.42f, C(255, 214, 140), false);
            PlaceKit.Plant(pg, new Vector3(hx - 0.6f, 0f, hz - 0.6f), 1f, rng);
            Light(sp, pg, new Vector3(-2f, H - 0.6f, -1.2f), WarmLight, 10f, 1f);
        }

        static Color32 Vary(Color32 c, int k) { return PlaceKit.Vary(c, (k * 37 % 11) / 10f, 0.08f); }

        // ================================================================== oasis tea house
        static void Oasis(InteriorSpace sp, PGeo pg, System.Random rng)
        {
            float W = sp.W, D = sp.D, H = sp.H, hx = W * 0.5f, hz = D * 0.5f;
            var st = new PlaceKit.Style
            {
                floor = PlaceKit.Floor.Tiles, floorA = C(196, 120, 80), floorB = C(180, 106, 70), wallLow = C(200, 160, 118), wallHigh = C(228, 198, 158),
                trim = C(140, 96, 60), post = C(130, 86, 54), ceiling = C(190, 166, 110), beam = C(110, 74, 46), wainscot = 0.9f
            };
            PlaceKit.Room(pg, W, D, H, st, 71);
            pg.ambient = new Color(0.66f, 0.56f, 0.46f);
            PlaceKit.Window(pg, new Vector3(0.8f, 1.75f, hz), Vector3.back, 2.0f, 2.0f, PlaceKit.View.Desert, Pal.WoodDark, false, true);
            PlaceKit.Window(pg, new Vector3(hx, 1.7f, 1.0f), Vector3.left, 2.2f, 1.3f, PlaceKit.View.Desert, Pal.WoodDark);
            // carpets, low tables, cushions
            Vector3[] tables = { new Vector3(1.2f, 0f, -1.2f), new Vector3(2.0f, 0f, 2.0f) };
            Color32[] cush = { C(190, 50, 50), C(60, 100, 160), C(230, 170, 60), C(70, 130, 90) };
            for (int i = 0; i < tables.Length; i++)
            {
                Vector3 t = tables[i];
                PlaceKit.Rug(pg, t, 2.8f, 2.2f, i == 0 ? C(150, 40, 44) : C(40, 70, 120), C(220, 190, 130), C(240, 200, 90));
                PlaceKit.LowTable(pg, t, 0.9f, 0.9f, 0.38f, C(120, 80, 50));
                Vector3 steam = PlaceKit.TeaSet(pg, t + Up * 0.38f, 30f, rng);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 co = t + Quaternion.Euler(0f, k * 90f, 0f) * new Vector3(0f, 0f, 0.95f);
                    PlaceKit.Cushion(pg, co, 0.32f, cush[(k + i) % cush.Length]);
                }
                if (i == 0)
                {
                    sp.steamPts.Add(sp.W2(steam));
                    for (int g = 0; g < 2; g++)
                    {
                        Vector3 co = t + Quaternion.Euler(0f, g * 90f, 0f) * new Vector3(0f, 0f, 0.95f);
                        Npc(sp, Guest(700 + g, g == 0 ? 6 : 0), co, FaceYaw(co, t), PersonPose.Sit);
                    }
                }
                else AddSeat(sp, t + new Vector3(0f, 0f, -0.95f), t, "Sit down for tea", true, steam);
            }
            // tea counter with a brass urn
            Vector3 cp = new Vector3(-hx + 1.5f, 0f, 0.8f);
            PlaceKit.Counter(pg, cp, Vector3.right, 3f, 1.0f, 0.7f, C(170, 120, 80), C(200, 160, 110));
            PlaceKit.Shelf(pg, new Vector3(-hx, 0f, 0.8f), Vector3.right, 3f, 2.2f, 0.4f, 3, rng, 0);
            Vector3 urn = cp + new Vector3(0.05f, 1.0f, -0.8f);
            pg.shell.Frustum(urn, urn + Up * 0.15f, 0.12f, 0.12f, 0.16f, 0.16f, 8, 0f, Pal.GoldDark, Pal.Gold, false, false);
            pg.shell.Ellipsoid(urn + Up * 0.3f, new Vector3(0.18f, 0.2f, 0.18f), 8, 4, Pal.Gold, Pal.GoldDark);
            pg.shell.Frustum(urn + Up * 0.48f, urn + Up * 0.62f, 0.06f, 0.06f, 0.03f, 0.03f, 6, 0f, Pal.Gold, Pal.Gold, false, true);
            sp.steamPts.Add(sp.W2(urn + Up * 0.66f));
            Npc(sp, Keeper(71, new Color(0.5f, 0.3f, 0.2f)), cp + new Vector3(-0.75f, 0f, 0f), 90f, PersonPose.Work);
            var tea = new ShopDef { id = "tea_" + sp.place.id, title = sp.place.name, subtitle = "Silk Road teas" };
            tea.items.Add(TeaItem("Spiced Silk Road tea", 14, 0.10f, 6f));
            tea.items.Add(Item("Rose tea", "a little luck for 10 min", 16, () => { Buffs.Add("rose", "Rose tea", "茶", BuffKind.Luck, 0.1f, 10f, Buffs.LuckRed); PlacesManager.Toast("Rose tea - a little luck for 10 min"); }));
            tea.items.Add(FoodItem("Naan & yoghurt", 18, 0.12f, 8f));
            tea.items.Add(FoodItem("Dried apricots & nuts", 12, 0.08f, 6f));
            Shop(sp, cp + new Vector3(0.9f, 1f, 0f), "Buy tea", tea);
            // brass lanterns, scroll, plants, job board
            for (int k = 0; k < 3; k++) PlaceKit.Lantern(pg, new Vector3(-1f + k * 1.8f, H - 0.55f, 0.4f), H, 0.2f, 0.38f, C(240, 180, 80), false);
            Jobs(sp, new Vector3(2.6f, 1f, -hz + 0.8f), sp.place.jobBoard);
            PlaceKit.JobBoard(pg, new Vector3(2.6f, 1.45f, -hz), Vector3.forward, rng);
            PlaceKit.Scroll(pg, new Vector3(-2.4f, 1.75f, -hz), Vector3.forward, 0.55f, 1.1f, '茶', C(244, 236, 214), Pal.RedDeep);
            PlaceKit.Plant(pg, new Vector3(hx - 0.5f, 0f, hz - 0.5f), 1.2f, rng);
            PlaceKit.Plant(pg, new Vector3(hx - 0.5f, 0f, -hz + 0.5f), 0.9f, rng);
            Light(sp, pg, new Vector3(0f, H - 0.6f, 0.4f), new Color(1f, 0.74f, 0.45f), 9f, 1.25f);
        }
    }
}

// RallyAnimals.cs - valley animals: red-crowned cranes flying slowly across the valley in a loose V,
// cats sitting on the ridges of the village houses (tail swish, watching passing cars), chickens
// pecking in the yards behind the houses (they scatter from a car) and a water buffalo grazing in a
// flat meadow near the river. Each group is one LifeBatch; updates are distance throttled.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyAnimals : RallyModule
    {
        // ---- cranes
        sealed class Crane { public int body, wingL, wingR; public Vector3 offset; public float phase; public Vector3 pos; }
        LifeBatch craneBatch;
        readonly List<Crane> cranes = new List<Crane>();
        float craneU;

        // ---- cats
        sealed class Cat { public int body, tail; public Vector3 pos; public float baseYaw, yaw, phase; }
        LifeBatch catBatch;
        readonly List<Cat> cats = new List<Cat>();

        // ---- chickens
        sealed class Hen
        {
            public int body, head;
            public Vector3 pos, target;
            public float yaw, phase, pause, peck, speed;
            public Vector3 flock;
        }
        LifeBatch henBatch;
        readonly List<Hen> hens = new List<Hen>();
        readonly List<Vector3> flocks = new List<Vector3>();

        // ---- buffalo
        LifeBatch buffaloBatch;
        int bufBody, bufHead, bufTail;
        Vector3 bufHome, bufPos, bufTarget;
        float bufYaw, bufGraze, bufPause;
        bool hasBuffalo;

        public int CatCount { get { return cats.Count; } }
        public int HenCount { get { return hens.Count; } }
        public override string Summary { get { return cranes.Count + " cranes, " + cats.Count + " cats, " + hens.Count + " chickens, buffalo " + hasBuffalo; } }

        public override void Build()
        {
            BuildCranes();
            var houses = Houses();
            BuildCats(houses);
            BuildHens(houses);
            BuildBuffalo();
        }

        // ================================================================ cranes
        void BuildCranes()
        {
            craneBatch = new LifeBatch();
            int bt = craneBatch.AddTemplate(RallyShapes.CraneBody());
            int wl = craneBatch.AddTemplate(RallyShapes.CraneWing(-1));
            int wr = craneBatch.AddTemplate(RallyShapes.CraneWing(1));
            Vector3[] form = { Vector3.zero, new Vector3(-4f, 0.6f, -4.5f), new Vector3(4.2f, 0.3f, -5f), new Vector3(-8f, 1.1f, -9f), new Vector3(8.3f, 0.8f, -9.5f) };
            int n = L.Density >= 1f ? 5 : 3;
            for (int i = 0; i < n; i++)
                cranes.Add(new Crane { body = craneBatch.AddPart(bt), wingL = craneBatch.AddPart(wl), wingR = craneBatch.AddPart(wr), offset = form[i], phase = R(0f, 0.6f) });
            craneBatch.Finish("Cranes", MaterialLibrary.VertexColorWind, Root, 0, false);
            craneU = R(0f, 1f);
        }

        static Vector3 CranePath(float u)
        {
            float a = u * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * 340f + Mathf.Sin(a * 2f) * 40f, 92f + Mathf.Sin(a * 3f) * 8f, Mathf.Sin(a) * 250f);
        }

        void UpdateCranes(float dt)
        {
            craneU = Mathf.Repeat(craneU + dt * 8.5f / 1900f, 1f);      // ~8.5 m/s around a ~1.9 km loop
            Vector3 p = CranePath(craneU), q = CranePath(craneU + 0.002f);
            Vector3 fwd = (q - p).normalized;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            if ((p - L.Eye).sqrMagnitude > 600f * 600f && L.Frame % 6 != 0) return;
            float clock = L.Clock;
            for (int i = 0; i < cranes.Count; i++)
            {
                var c = cranes[i];
                float t = clock + c.phase;
                Vector3 pos = p + rot * c.offset + Vector3.up * Mathf.Sin(t * 0.7f) * 0.4f;
                // slow deep beats, then a long glide
                float cyc = Mathf.Repeat(t / 7f, 1f);
                float flap = cyc < 0.55f ? Mathf.Sin(t * 1.25f * Mathf.PI * 2f) * 34f : 4f + Mathf.Sin(t) * 2f;
                Quaternion r = rot * Quaternion.Euler(-2f, 0f, Mathf.Sin(t * 0.3f) * 5f);
                Matrix4x4 m = Matrix4x4.TRS(pos, r, Vector3.one);
                craneBatch.Set(c.body, m);
                craneBatch.Set(c.wingR, m * Matrix4x4.TRS(new Vector3(0.1f, 0.08f, 0.05f), Quaternion.Euler(0f, 0f, flap), Vector3.one));
                craneBatch.Set(c.wingL, m * Matrix4x4.TRS(new Vector3(-0.1f, 0.08f, 0.05f), Quaternion.Euler(0f, 0f, -flap), Vector3.one));
            }
            craneBatch.Upload();
        }

        // ================================================================ houses
        struct House { public Transform tr; public Vector3 min, max; }

        List<House> Houses()
        {
            var list = new List<House>();
            var root = GameObject.Find("World_Static");
            if (root == null) return list;
            Transform rt = root.transform;
            for (int i = 0; i < rt.childCount; i++)
            {
                Transform h = rt.GetChild(i);
                if (!h.name.StartsWith("ENV_HouseTwoStorey") && !h.name.StartsWith("ENV_HouseShop")) continue;
                Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
                Matrix4x4 w2l = h.worldToLocalMatrix;
                foreach (var mf in h.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    Matrix4x4 m = w2l * mf.transform.localToWorldMatrix;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        Vector3 q = m.MultiplyPoint3x4(c);
                        mn = Vector3.Min(mn, q);
                        mx = Vector3.Max(mx, q);
                    }
                }
                if (mx.x < mn.x || mx.y < 2f) continue;
                list.Add(new House { tr = h, min = mn, max = mx });
            }
            return list;
        }

        // ================================================================ cats
        void BuildCats(List<House> houses)
        {
            if (houses.Count == 0) return;
            int want = Mathf.Clamp(Mathf.RoundToInt(5 * L.Density), 2, 7);
            var order = new List<int>();
            for (int i = 0; i < houses.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = RI(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            catBatch = new LifeBatch();
            for (int k = 0; k < order.Count && cats.Count < want; k++)
            {
                var h = houses[order[k]];
                Vector3 ext = h.max - h.min;
                bool longX = ext.x >= ext.z;
                float along = R(-0.7f, 0.7f);
                Vector3 local = new Vector3(longX ? along : 0f, h.max.y - 0.03f, longX ? 0f : along);
                Vector3 world = h.tr.TransformPoint(local);
                // look out over the street (local -Z), turned a little
                float yaw = RallyLife.Yaw(h.tr.TransformDirection(Vector3.back)) + R(-50f, 50f);
                Color32 fur = RallyShapes.CatFur[RI(RallyShapes.CatFur.Length)];
                var c = new Cat
                {
                    body = catBatch.AddPart(catBatch.AddTemplate(RallyShapes.CatBody(fur))),
                    tail = catBatch.AddPart(catBatch.AddTemplate(RallyShapes.CatTail(fur))),
                    pos = world, baseYaw = yaw, yaw = yaw, phase = R(0f, 40f)
                };
                cats.Add(c);
            }
            catBatch.Finish("RoofCats", MaterialLibrary.VertexColorWind, Root, 0, true);
            for (int i = 0; i < cats.Count; i++) PoseCat(cats[i]);
            catBatch.Upload();
        }

        void PoseCat(Cat c)
        {
            float t = L.Clock + c.phase;
            float s = 1.1f;
            Matrix4x4 m = Matrix4x4.TRS(c.pos, Quaternion.Euler(0f, c.yaw, 0f), new Vector3(s, s, s));
            catBatch.Set(c.body, m);
            float swish = Mathf.Sin(t * 1.6f) * 28f + Mathf.Sin(t * 0.37f) * 18f;
            catBatch.Set(c.tail, m * Matrix4x4.TRS(new Vector3(0f, 0.05f, -0.14f), Quaternion.Euler(Mathf.Sin(t * 0.9f) * 10f, swish, 0f), Vector3.one));
        }

        void UpdateCats(float dt)
        {
            if (catBatch == null) return;
            bool any = false;
            for (int i = 0; i < cats.Count; i++)
            {
                var c = cats[i];
                if (!L.Due(c.pos, i, 50f, 110f, 160f)) continue;
                float target = c.baseYaw + Mathf.Sin((L.Clock + c.phase) * 0.05f) * 60f;
                if (L.HasCar && (L.CarPos - c.pos).sqrMagnitude < 40f * 40f) target = RallyLife.Yaw(L.CarPos - c.pos);
                c.yaw = Mathf.MoveTowardsAngle(c.yaw, target, 60f * dt);
                PoseCat(c);
                any = true;
            }
            if (any) catBatch.Upload();
        }

        // ================================================================ chickens
        void BuildHens(List<House> houses)
        {
            int wantFlocks = Mathf.Clamp(Mathf.RoundToInt(3 * L.Density), 2, 4);
            var order = new List<int>();
            for (int i = 0; i < houses.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = RI(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            foreach (int hi in order)
            {
                if (flocks.Count >= wantFlocks) break;
                var h = houses[hi];
                float back = h.max.z + 4.5f;
                for (int tries = 0; tries < 4; tries++)
                {
                    Vector3 local = new Vector3(R(-3f, 3f), 0f, back + tries * 1.5f);
                    Vector3 w = h.tr.TransformPoint(local);
                    Vector2 p = new Vector2(w.x, w.z);
                    if (!MapData.InVillage(p) || L.EdgeDist(p) < 8f) continue;
                    if (!TryPlace(p, 3.5f, 7f, 0.8f)) continue;
                    flocks.Add(L.Ground(p));
                    break;
                }
            }
            if (flocks.Count == 0) return;
            henBatch = new LifeBatch();
            Color32[] plum = { LifeGeo.C(170, 90, 50), LifeGeo.C(244, 240, 230), LifeGeo.C(40, 36, 40), LifeGeo.C(196, 140, 80) };
            var bodyT = new int[plum.Length]; var headT = new int[plum.Length];
            for (int k = 0; k < plum.Length; k++)
            {
                bodyT[k] = henBatch.AddTemplate(RallyShapes.ChickenBody(plum[k]));
                headT[k] = henBatch.AddTemplate(RallyShapes.ChickenHead(plum[k]));
            }
            foreach (var f in flocks)
            {
                int n = 3 + RI(3);
                for (int i = 0; i < n; i++)
                {
                    int k = RI(plum.Length);
                    var hn = new Hen { body = henBatch.AddPart(bodyT[k]), head = henBatch.AddPart(headT[k]), flock = f, phase = R(0f, 20f) };
                    hn.pos = f + new Vector3(R(-2f, 2f), 0f, R(-2f, 2f));
                    hn.pos.y = L.Ground(hn.pos.x, hn.pos.z);
                    hn.target = hn.pos;
                    hn.yaw = R(0f, 360f);
                    hens.Add(hn);
                }
            }
            henBatch.Finish("Chickens", MaterialLibrary.VertexColorWind, Root, 0, true);
            for (int i = 0; i < hens.Count; i++) PoseHen(hens[i]);
            henBatch.Upload();
        }

        void PoseHen(Hen h)
        {
            float s = 1.25f;
            Matrix4x4 m = Matrix4x4.TRS(h.pos, Quaternion.Euler(0f, h.yaw, 0f), new Vector3(s, s, s));
            henBatch.Set(h.body, m);
            float peck = h.peck * 75f;
            float bob = h.speed > 0.6f ? Mathf.Sin((L.Clock + h.phase) * 16f) * 12f : Mathf.Sin((L.Clock + h.phase) * 3f) * 6f;
            henBatch.Set(h.head, m * Matrix4x4.TRS(new Vector3(0f, 0.28f, 0.1f), Quaternion.Euler(peck + bob, 0f, 0f), Vector3.one));
        }

        void UpdateHens(float dt)
        {
            if (henBatch == null) return;
            bool any = false;
            for (int i = 0; i < hens.Count; i++)
            {
                var h = hens[i];
                if (!L.Due(h.flock, i, 40f, 90f, 130f)) continue;
                float step = Mathf.Max(dt, 0.016f) * ((h.flock - L.Eye).sqrMagnitude < 40f * 40f ? 1f : 3f);
                // scatter from a car
                bool scared = false;
                if (L.HasCar && L.CarSpeed > 2.5f)
                {
                    Vector3 d = h.pos - L.CarPos;
                    d.y = 0f;
                    if (d.sqrMagnitude < 13f * 13f)
                    {
                        scared = true;
                        Vector3 away = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
                        Vector3 t = h.flock + Vector3.ClampMagnitude((h.pos - h.flock) + away * 3f, 3.6f);
                        h.target = new Vector3(t.x, 0f, t.z);
                        h.pause = 0f;
                    }
                }
                Vector3 to = h.target - h.pos;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    h.speed = 0f;
                    h.pause -= step;
                    float pk = Mathf.Sin((L.Clock + h.phase) * 5f);
                    h.peck = Mathf.MoveTowards(h.peck, pk > 0.3f ? 1f : 0f, step * 6f);
                    if (h.pause <= 0f)
                    {
                        Vector2 r = Random2() * 3f;
                        h.target = h.flock + new Vector3(r.x, 0f, r.y);
                        h.pause = R(1.5f, 5f);
                    }
                }
                else
                {
                    h.peck = Mathf.MoveTowards(h.peck, 0f, step * 6f);
                    h.speed = scared ? 2.6f : 0.45f;
                    Vector3 mv = Vector3.ClampMagnitude(to, h.speed * step);
                    h.pos += mv;
                    h.pos.y = L.Ground(h.pos.x, h.pos.z);
                    h.yaw = Mathf.MoveTowardsAngle(h.yaw, RallyLife.Yaw(to), 400f * step);
                }
                PoseHen(h);
                any = true;
            }
            if (any) henBatch.Upload();
        }

        Vector2 Random2()
        {
            float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0f, 1f));
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        // ================================================================ buffalo
        void BuildBuffalo()
        {
            var river = MapData.River;
            Vector2 spot = Vector2.zero;
            bool found = false;
            for (int tries = 0; tries < 300 && !found; tries++)
            {
                Vector2 a = river[RI(river.Length - 1)];
                Vector2 b = river[Mathf.Min(river.Length - 1, System.Array.IndexOf(river, a) + 1)];
                Vector2 on = Vector2.Lerp(a, b, R(0f, 1f));
                Vector2 dir = (b - a).normalized;
                Vector2 p = on + new Vector2(-dir.y, dir.x) * (Chance(0.5f) ? 1f : -1f) * R(24f, 40f);
                float rd = W.RiverDistance(p);
                if (rd < 22f || rd > 45f) continue;
                if (MapData.InVillage(p) || MapData.InTeaTerraces(p) || MapData.InBambooForest(p)) continue;
                if (L.EdgeDist(p) < 18f || L.Bumpiness(p, 5f) > 0.7f) continue;
                if (!TryPlace(p, 6f, 15f, 0.8f)) continue;
                spot = p;
                found = true;
            }
            if (!found) return;
            buffaloBatch = new LifeBatch();
            bufBody = buffaloBatch.AddPart(buffaloBatch.AddTemplate(RallyShapes.BuffaloBody()));
            bufHead = buffaloBatch.AddPart(buffaloBatch.AddTemplate(RallyShapes.BuffaloHead()));
            bufTail = buffaloBatch.AddPart(buffaloBatch.AddTemplate(RallyShapes.BuffaloTail()));
            buffaloBatch.Finish("WaterBuffalo", MaterialLibrary.VertexColorWind, Root, 0, true);
            bufHome = L.Ground(spot);
            bufPos = bufTarget = bufHome;
            bufYaw = R(0f, 360f);
            hasBuffalo = true;
            PoseBuffalo();
            buffaloBatch.Upload();
        }

        void PoseBuffalo()
        {
            float t = L.Clock;
            Matrix4x4 m = Matrix4x4.TRS(bufPos, Quaternion.Euler(0f, bufYaw, 0f), Vector3.one);
            buffaloBatch.Set(bufBody, m);
            float headPitch = Mathf.Lerp(-5f, 48f, bufGraze) + Mathf.Sin(t * 2.2f) * 3f * bufGraze;
            float headYaw = Mathf.Sin(t * 0.3f) * 12f;
            buffaloBatch.Set(bufHead, m * Matrix4x4.TRS(new Vector3(0f, 1.3f, 1.05f), Quaternion.Euler(headPitch, headYaw, 0f), Vector3.one));
            buffaloBatch.Set(bufTail, m * Matrix4x4.TRS(new Vector3(0f, 1.3f, -1.12f), Quaternion.Euler(10f, 0f, Mathf.Sin(t * 1.7f) * 25f), Vector3.one));
        }

        void UpdateBuffalo(float dt)
        {
            if (!hasBuffalo || !L.Due(bufPos, 11, 60f, 140f, 220f)) return;
            float step = Mathf.Max(dt, 0.016f) * ((bufPos - L.Eye).sqrMagnitude < 60f * 60f ? 1f : 3f);
            Vector3 to = bufTarget - bufPos;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f)
            {
                bufGraze = Mathf.MoveTowards(bufGraze, 1f, step * 0.6f);
                bufPause -= step;
                if (bufPause <= 0f)
                {
                    Vector2 r = Random2() * 5f;
                    bufTarget = bufHome + new Vector3(r.x, 0f, r.y);
                    bufPause = R(8f, 20f);
                }
            }
            else
            {
                bufGraze = Mathf.MoveTowards(bufGraze, 0.2f, step * 0.8f);
                float want = RallyLife.Yaw(to);
                bufYaw = Mathf.MoveTowardsAngle(bufYaw, want, 12f * step);
                if (Mathf.Abs(Mathf.DeltaAngle(bufYaw, want)) < 30f)
                {
                    bufPos += Vector3.ClampMagnitude(to, 0.35f * step);
                    bufPos.y = L.Ground(bufPos.x, bufPos.z);
                }
            }
            PoseBuffalo();
            buffaloBatch.Upload();
        }

        public override void Tick(float dt)
        {
            UpdateCranes(dt);
            UpdateCats(dt);
            UpdateHens(dt);
            UpdateBuffalo(dt);
        }
    }
}

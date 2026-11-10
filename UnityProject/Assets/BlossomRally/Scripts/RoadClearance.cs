// RoadClearance.cs - safety net: after the valley and all its props are built, any solid collider that ends up on a
// road surface (inside the drivable width, within car height) is switched off and logged, so nothing can ever
// block or crash the car on a road. Runs a few times while the modules finish building over their first seconds.
using System.Collections;
using UnityEngine;

namespace BlossomRally
{
    public class RoadClearance : MonoBehaviour
    {
        static bool registered;
        WorldBuilder world;
        int removed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += w =>
            {
                var go = new GameObject("RoadClearance");
                var rc = go.AddComponent<RoadClearance>();
                rc.world = w;
            };
        }

        IEnumerator Start()
        {
            float[] waits = { 1f, 4f, 10f, 25f };
            foreach (float t in waits)
            {
                yield return new WaitForSecondsRealtime(t);
                Sweep();
            }
            if (removed > 0) Debug.LogWarning("[BlossomRally] RoadClearance switched off " + removed + " collider(s) that sat on a road.");
        }

        void Sweep()
        {
            if (world == null) return;
            foreach (var col in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                var go = col.gameObject;
                if (!go.scene.IsValid() || !go.activeInHierarchy) continue;
                if (go.layer == CarExtras.CarLayer) continue;
                if (col is TerrainCollider) continue;
                var mc = col as MeshCollider;
                if (mc != null && !mc.convex) continue;                  // roads, terrain, buildings with mesh colliders
                if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue;
                Bounds b = col.bounds;
                if (b.size.x > 60f || b.size.z > 60f) continue;         // valley boundary walls
                RoadSample rs;
                Vector2 c2 = new Vector2(b.center.x, b.center.z);
                float d = world.NearestRoad(c2, out rs, 40f);
                if (d > 40f) continue;
                Vector3 probe = new Vector3(rs.pos.x, rs.height + 0.6f, rs.pos.y);
                Vector3 q = col.ClosestPoint(probe);
                RoadSample rs2;
                float d2 = world.NearestRoad(new Vector2(q.x, q.z), out rs2, 20f);
                if (d2 > 20f) continue;
                bool onRoad = d2 < rs2.halfWidth - 0.1f;
                bool carHeight = q.y < rs2.height + 1.8f && q.y > rs2.height - 0.6f;
                if (onRoad && carHeight)
                {
                    col.enabled = false;
                    removed++;
                    Debug.LogWarning("[BlossomRally] RoadClearance: removed collider on the road: " + PathOf(go.transform) +
                                     " at " + q.ToString("F1") + " (road " + world.roads[rs2.road].name + ")");
                }
            }
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}

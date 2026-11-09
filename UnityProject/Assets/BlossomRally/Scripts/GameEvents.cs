// GameEvents.cs - shared hooks so modules plug in without editing GameManager:
//  * WorldHooks.WorldBuilt(world)  - after the valley is built (place props, life, effects)
//  * WorldHooks.CarSpawned(car)    - every time a player car is created (add per-car components)
//  * GameEvents.*                  - gameplay moments for camera / sound / crowd / pop-up "juice"
// Modules subscribe from a [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
// static method so they are registered before GameManager raises anything.
using System;
using UnityEngine;

namespace BlossomRally
{
    public static class WorldHooks
    {
        public static event Action<WorldBuilder> WorldBuilt;
        public static event Action<ArcadeCar> CarSpawned;
        public static WorldBuilder World { get; private set; }

        public static void RaiseWorldBuilt(WorldBuilder w)
        {
            World = w;
            if (WorldBuilt == null) return;
            foreach (Action<WorldBuilder> h in WorldBuilt.GetInvocationList())
            {
                try { h(w); }
                catch (Exception e) { Debug.LogException(e); }   // one broken module must not stop the others
            }
        }

        public static void RaiseCarSpawned(ArcadeCar car)
        {
            if (CarSpawned == null || car == null) return;
            foreach (Action<ArcadeCar> h in CarSpawned.GetInvocationList())
            {
                try { h(car); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    /// <summary>Gameplay moments. Raise from physics / scoring, listen from camera, audio, crowds, HUD.</summary>
    public static class GameEvents
    {
        /// <summary>Car left the ground (all wheels) - position.</summary>
        public static event Action<Vector3> TakeOff;
        /// <summary>Car landed: air time (s), landing speed into the ground (m/s).</summary>
        public static event Action<float, float> Landed;
        /// <summary>Collision: impact speed (m/s), contact point.</summary>
        public static event Action<float, Vector3> Impact;
        /// <summary>A drift chain was banked: points, peak angle (deg).</summary>
        public static event Action<float, float> DriftBanked;
        /// <summary>Passed very close to an obstacle at speed: position, clearance (m).</summary>
        public static event Action<Vector3, float> NearMiss;
        /// <summary>Stage checkpoint passed: index, split delta vs best (s, negative = faster, NaN = no best).</summary>
        public static event Action<int, float> Checkpoint;
        /// <summary>Event finished: medal 0..3, time or points.</summary>
        public static event Action<int, float> Finished;
        /// <summary>Crowd / spectators should cheer at a position with a loudness 0..1.</summary>
        public static event Action<Vector3, float> Cheer;

        static void Safe<T>(Action<T> a, T x) { if (a != null) try { a(x); } catch (Exception e) { Debug.LogException(e); } }
        static void Safe<T1, T2>(Action<T1, T2> a, T1 x, T2 y) { if (a != null) try { a(x, y); } catch (Exception e) { Debug.LogException(e); } }

        public static void RaiseTakeOff(Vector3 p) { Safe(TakeOff, p); }
        public static void RaiseLanded(float air, float vy) { Safe(Landed, air, vy); }
        public static void RaiseImpact(float speed, Vector3 p) { Safe(Impact, speed, p); }
        public static void RaiseDriftBanked(float points, float peak) { Safe(DriftBanked, points, peak); }
        public static void RaiseNearMiss(Vector3 p, float clearance) { Safe(NearMiss, p, clearance); }
        public static void RaiseCheckpoint(int i, float delta) { Safe(Checkpoint, i, delta); }
        public static void RaiseFinished(int medal, float result) { Safe(Finished, medal, result); }
        public static void RaiseCheer(Vector3 p, float loud) { Safe(Cheer, p, loud); }
    }
}

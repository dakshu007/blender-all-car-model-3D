// PlayerState.cs - SHARED CONTRACT: where the player is and what they are doing (on foot or in a vehicle).
// Owned by the on-foot module (it sets these); everyone else only reads them.
using UnityEngine;

namespace BlossomRally
{
    public static class PlayerState
    {
        /// <summary>True while the player walks around (out of any vehicle).</summary>
        public static bool OnFoot { get; set; }
        /// <summary>The player's person (null while driving and no person exists).</summary>
        public static Transform Person { get; set; }
        /// <summary>The vehicle the player is in, or last left (cars and bikes are both ArcadeCar).</summary>
        public static ArcadeCar Vehicle
        {
            get { var gm = GameManager.Instance; return gm != null ? gm.Car : null; }
        }
        /// <summary>World position of the player: the person on foot, otherwise the vehicle.</summary>
        public static Vector3 Position
        {
            get
            {
                if (OnFoot && Person != null) return Person.position;
                var v = Vehicle;
                return v != null ? v.transform.position : Vector3.zero;
            }
        }
        /// <summary>Flat heading of the player (forward of the person or vehicle).</summary>
        public static Vector3 Forward
        {
            get
            {
                Transform t = OnFoot && Person != null ? Person : (Vehicle != null ? Vehicle.transform : null);
                if (t == null) return Vector3.forward;
                Vector3 f = t.forward; f.y = 0f;
                return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            }
        }
        /// <summary>Raised after the player got out (true) or into (false) a vehicle.</summary>
        public static event System.Action<bool> OnFootChanged;
        public static void RaiseOnFootChanged(bool onFoot) { if (OnFootChanged != null) OnFootChanged(onFoot); }

        /// <summary>True while getting in / out of a vehicle (the short door animation).</summary>
        public static bool InTransition { get; set; }

        // ---- parked, empty vehicles (crew hidden, lights off): the car the player left, a left-behind car
        static readonly System.Collections.Generic.List<ArcadeCar> empty = new System.Collections.Generic.List<ArcadeCar>(4);

        /// <summary>True when nobody sits in this vehicle (the player got out): cockpit crew is hidden.</summary>
        public static bool IsVehicleEmpty(ArcadeCar v)
        {
            if (v == null) return false;
            for (int i = 0; i < empty.Count; i++) if (empty[i] == v) return true;
            return false;
        }

        public static void SetVehicleEmpty(ArcadeCar v, bool isEmpty)
        {
            for (int i = empty.Count - 1; i >= 0; i--)
                if (empty[i] == null || empty[i] == v) empty.RemoveAt(i);
            if (isEmpty && v != null) empty.Add(v);
        }
    }
}

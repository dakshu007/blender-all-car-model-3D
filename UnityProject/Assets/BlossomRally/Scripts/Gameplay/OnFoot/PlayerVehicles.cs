// PlayerVehicles.cs - vehicles the player may get into on foot (F / gamepad Y), besides the current car
// (GameManager.Car, always enterable). Other modules register the owned cars / bikes they spawn parked in the
// world; the on-foot module then walks the person to the door, seats them and hands the vehicle to the
// GameManager (AdoptVehicle). Seat geometry is optional: by default it comes from the car's cockpit anatomy
// or its BoxCollider; bikes should pass PersonPose.Ride (the rider then stays visible in the saddle).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class PlayerVehicles
    {
        public sealed class Entry
        {
            public ArcadeCar vehicle;
            /// <summary>Shown in the prompt ("F  Get in" / detail line).</summary>
            public string name;
            /// <summary>Drive (cars) or Ride (motorbikes).</summary>
            public PersonPose pose = PersonPose.Drive;
            /// <summary>Optional, vehicle space: the floor point under the driver's hips / where to stand at the door.</summary>
            public Vector3? seatLocal, doorLocal;
            /// <summary>Seat surface above seatLocal (m); negative = pose default.</summary>
            public float seatHeight = -1f;
            /// <summary>Registered by another module (never destroyed by the game manager / on-foot module).</summary>
            public bool moduleOwned = true;
            /// <summary>Optional: called after the player got in / out.</summary>
            public System.Action onEntered, onExited;
        }

        static readonly List<Entry> all = new List<Entry>();
        internal static List<Entry> All { get { return all; } }

        /// <summary>Make a parked vehicle enterable. Returns the entry (fill optional fields).</summary>
        public static Entry Register(ArcadeCar v, string name, PersonPose pose = PersonPose.Drive)
        {
            if (v == null) return null;
            var e = Find(v);
            if (e == null) { e = new Entry { vehicle = v }; all.Add(e); }
            e.name = name;
            e.pose = pose;
            return e;
        }

        public static void Unregister(ArcadeCar v)
        {
            for (int i = all.Count - 1; i >= 0; i--)
                if (all[i].vehicle == null || all[i].vehicle == v) all.RemoveAt(i);
        }

        public static Entry Find(ArcadeCar v)
        {
            if (v == null) return null;
            for (int i = 0; i < all.Count; i++) if (all[i].vehicle == v) return all[i];
            return null;
        }

        public static bool IsModuleOwned(ArcadeCar v)
        {
            var e = Find(v);
            return e != null && e.moduleOwned;
        }

        /// <summary>The garage car the player left when switching to another vehicle (kept parked, one at most).</summary>
        internal static ArcadeCar Leftover { get; private set; }

        internal static void SetLeftover(ArcadeCar v)
        {
            if (Leftover != null && Leftover != v)
            {
                var old = Leftover;
                Unregister(old);
                PlayerState.SetVehicleEmpty(old, false);
                Object.Destroy(old.gameObject);
            }
            Leftover = v;
            if (v != null)
            {
                var e = Register(v, v.spec != null ? v.spec.name : "Car");
                e.moduleOwned = false;
            }
        }

        /// <summary>A new garage car replaces the parked one.</summary>
        internal static void ClearLeftover(ArcadeCar keep)
        {
            if (Leftover == null || Leftover == keep) { if (Leftover == keep && keep != null) { Unregister(keep); Leftover = null; } return; }
            SetLeftover(null);
        }

        internal static void Prune()
        {
            for (int i = all.Count - 1; i >= 0; i--) if (all[i].vehicle == null) all.RemoveAt(i);
        }
    }
}

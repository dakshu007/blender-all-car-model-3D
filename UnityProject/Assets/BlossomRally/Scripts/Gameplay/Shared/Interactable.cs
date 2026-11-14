// Interactable.cs - SHARED CONTRACT: anything the player can use with the Interact key (E / gamepad X):
// shop counters, temple bells, job boards, showroom desks, doors into interiors, vehicles.
// The interaction system (on-foot module) finds the nearest available one in range, shows its prompt
// ("E  Buy tea  ¥12") and calls Use(). Add the component to a GameObject and fill the fields.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        /// <summary>Prompt shown next to the key hint, e.g. "Enter Tea House".</summary>
        public string prompt = "Interact";
        /// <summary>Optional second line, e.g. a price or "Closed at night".</summary>
        public string detail = "";
        /// <summary>Reach in metres from this transform.</summary>
        public float radius = 2.5f;
        /// <summary>Usable on foot (default) and/or from a vehicle (drive-throughs, job pickups).</summary>
        public bool onFoot = true, inVehicle = false;
        /// <summary>Optional gate (e.g. enough money, open hours). Null = always available.</summary>
        public System.Func<bool> available;
        /// <summary>Called when the player presses Interact.</summary>
        public System.Action onUse;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool CanUse(bool playerOnFoot)
        {
            if (playerOnFoot ? !onFoot : !inVehicle) return false;
            return available == null || available();
        }

        public void Use() { if (onUse != null) onUse(); }

        /// <summary>Convenience: add an interactable to a new child object at a world position.</summary>
        public static Interactable Create(Transform parent, Vector3 pos, string prompt, System.Action onUse, float radius = 2.5f)
        {
            var go = new GameObject("Interact_" + prompt);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var i = go.AddComponent<Interactable>();
            i.prompt = prompt; i.onUse = onUse; i.radius = radius;
            return i;
        }
    }
}

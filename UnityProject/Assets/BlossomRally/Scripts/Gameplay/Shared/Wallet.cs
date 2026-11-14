// Wallet.cs - SHARED CONTRACT: the player's money and what they own. Implemented by the economy module
// (which also saves it); everyone else calls these. Currency: yuan (¥).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class Wallet
    {
        static int balance = 2500;
        static readonly HashSet<string> owned = new HashSet<string> { "CAR-009" };

        /// <summary>Raised whenever money or ownership changes: (delta, reason).</summary>
        public static event System.Action<int, string> Changed;

        public static int Balance { get { return balance; } }

        public static void Add(int amount, string reason)
        {
            if (amount == 0) return;
            balance = Mathf.Max(0, balance + amount);
            if (Changed != null) Changed(amount, reason);
        }

        public static bool CanAfford(int amount) { return balance >= amount; }

        public static bool TrySpend(int amount, string reason)
        {
            if (amount < 0 || balance < amount) return false;
            balance -= amount;
            if (Changed != null) Changed(-amount, reason);
            return true;
        }

        /// <summary>Vehicles (car / bike ids) and other unlocks.</summary>
        public static bool Owns(string id) { return owned.Contains(id); }
        public static void Grant(string id) { if (owned.Add(id) && Changed != null) Changed(0, "unlock:" + id); }
        public static IEnumerable<string> Owned { get { return owned; } }

        /// <summary>Economy module: restore saved state (no events).</summary>
        public static void Load(int money, IEnumerable<string> ids)
        {
            balance = Mathf.Max(0, money);
            owned.Clear();
            foreach (var id in ids) owned.Add(id);
        }

        public static string Format(int amount) { return "¥" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
    }
}

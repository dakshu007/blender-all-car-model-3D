// GameplayHooks.cs - SHARED CONTRACT: decoupled entry points between gameplay modules. A module that owns a
// screen assigns the delegate; places / NPCs / interactables call it (always null-check: ?.Invoke).
namespace BlossomRally
{
    public static class GameplayHooks
    {
        /// <summary>Open a vehicle showroom (economy module). id: "city_cars", "coast_bikes", ...</summary>
        public static System.Action<string> OpenShowroom;
        /// <summary>Open a job board / work menu (economy module). id: "teahouse", "harbour", "taxi_rank", ...</summary>
        public static System.Action<string> OpenJobBoard;
        /// <summary>Open a shop menu (economy module) with items defined by the caller.</summary>
        public static System.Action<ShopDef> OpenShop;
        /// <summary>Show a short message on screen (game manager toast).</summary>
        public static System.Action<string> Toast;
        /// <summary>True while any gameplay menu (shop, showroom, job board, interior UI) has the input.</summary>
        public static bool MenuOpen;
    }

    /// <summary>A simple shop: title + items (name, price, effect when bought).</summary>
    public sealed class ShopDef
    {
        public string id, title, subtitle;
        public readonly System.Collections.Generic.List<ShopItem> items = new System.Collections.Generic.List<ShopItem>();
    }

    public sealed class ShopItem
    {
        public string name, description;
        public int price;
        public System.Action onBought;
    }
}

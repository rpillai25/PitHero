using Nez;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Services
{
    /// <summary>
    /// Shared sell-to-vault logic used by manual selling (InventoryGrid.DiscardItem) and
    /// auto-sell of excess items. Sold items go to the Second Chance vault for buyback and
    /// gold is credited via GameStateService with the "sell_item" source.
    /// </summary>
    public static class ItemSellHelper
    {
        /// <summary>Sells the item at the given bag slot (whole stack for consumables) and clears the slot. Returns gold earned (0 if slot empty).</summary>
        public static int SellBagItem(ItemBag bag, int bagIndex, string source)
        {
            var item = bag?.GetSlotItem(bagIndex);
            if (item == null)
                return 0;

            int gold = SellItemDirect(item, source);
            bag.SetSlotItem(bagIndex, null);
            return gold;
        }

        /// <summary>Sells an item that is not in a bag (e.g. an incoming chest item). Returns gold earned.</summary>
        public static int SellItemDirect(IItem item, string source)
        {
            if (item == null)
                return 0;

            int qty = (item is Consumable c) ? c.StackCount : 1;
            int gold = item.GetSellPrice() * qty;

            // Core.Instance is null in headless hosts (unit tests, virtual balance runs); Core.Services would throw there.
            if (Core.Instance != null)
            {
                Core.Services?.GetService<SecondChanceMerchantVault>()?.AddItem(item);
                Core.Services?.GetService<GameStateService>()?.AddFunds(gold, "sell_item");
            }

            Analytics.AnalyticsService.LogItemSold(item, qty, gold, source);
            Debug.Log($"Sold {item.Name} x{qty} for {gold}G ({source})");
            return gold;
        }
    }
}

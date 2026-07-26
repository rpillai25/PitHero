using Microsoft.Xna.Framework;
using Nez;
using PitHero.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Services
{
    /// <summary>Outcome of an auto-sell attempt when the bag is full.</summary>
    public enum AutoSellOutcome
    {
        /// <summary>Nothing was sold (disabled, bag not full, stackable pickup, or everything protected).</summary>
        None,
        /// <summary>A bag item was sold, freeing a slot for the incoming item.</summary>
        SoldBagItem,
        /// <summary>The incoming item was the weakest candidate and was sold directly.</summary>
        SoldIncoming
    }

    /// <summary>
    /// Auto-sells the weakest excess item when the bag is full and a new chest item arrives.
    /// Call-driven (no update loop): OpenChestAction invokes TryMakeRoom before adding the item.
    /// Items in an active synergy or under a placed stencil are never sold; gear rarities can be
    /// excluded via RarityAllowed. Consumables always sell before gear; gear weakness is compared
    /// across all gear types at once.
    /// </summary>
    public class AutoSellExcessItemsService
    {
        private const string SellSource = "auto_excess";

        /// <summary>Master toggle. On by default (unlike other automation toggles) — this guards against loot loss.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether gear of each rarity may be auto-sold, indexed by ItemRarity. All true by default.</summary>
        public bool[] RarityAllowed { get; } = new bool[5];

        public AutoSellExcessItemsService()
        {
            for (int i = 0; i < RarityAllowed.Length; i++)
                RarityAllowed[i] = true;
        }

        /// <summary>True when gear of the given rarity may be auto-sold. Consumables are never rarity-filtered.</summary>
        public bool IsRarityAllowed(ItemRarity rarity)
        {
            int i = (int)rarity;
            return i < 0 || i >= RarityAllowed.Length || RarityAllowed[i];
        }

        /// <summary>
        /// Attempts to free a bag slot for the incoming item by selling the weakest sellable item.
        /// Returns SoldIncoming when the incoming item itself was sold (caller must not add it to the bag).
        /// </summary>
        public AutoSellOutcome TryMakeRoom(ItemBag bag, IItem incoming)
        {
            if (!Enabled || bag == null || incoming == null || !bag.IsFull)
                return AutoSellOutcome.None;

            // A consumable that can absorb into an existing non-full stack needs no empty slot
            if (incoming is Consumable consumable && CanStackInto(bag, consumable))
                return AutoSellOutcome.None;

            // Refresh the grid from the bag so the synergy cache matches current contents before protection checks
            var grid = GetHeroInventoryGrid();
            grid?.UpdateItemsFromBag();
            System.Func<int, bool> isProtected = grid != null ? grid.IsBagIndexProtected : (System.Func<int, bool>)null;

            var selection = ExcessItemSellSelector.Select(bag, incoming, isProtected, IsRarityAllowed);
            if (!selection.HasSelection)
                return AutoSellOutcome.None;

            if (selection.SellIncoming)
            {
                int gold = ItemSellHelper.SellItemDirect(incoming, SellSource);
                EmitConsole(incoming, gold);
                return AutoSellOutcome.SoldIncoming;
            }

            var soldItem = bag.GetSlotItem(selection.BagIndex);
            int earned = ItemSellHelper.SellBagItem(bag, selection.BagIndex, SellSource);
            grid?.UpdateItemsFromBag();
            InventorySelectionManager.OnInventoryChanged?.Invoke();
            EmitConsole(soldItem, earned);
            return AutoSellOutcome.SoldBagItem;
        }

        private static bool CanStackInto(ItemBag bag, Consumable incoming)
        {
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is Consumable existing &&
                    existing.Name == incoming.Name &&
                    existing.StackCount < existing.StackSize)
                    return true;
            }
            return false;
        }

        private static InventoryGrid GetHeroInventoryGrid()
        {
            if (Core.Instance == null)
                return null;
            return Core.Services?.GetService<SettingsUI>()?.HeroUI?.GetInventoryGrid();
        }

        private static void EmitConsole(IItem item, int gold)
        {
            if (Core.Instance == null || item == null)
                return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleAutoSoldItem,
                (item.Name, RarityUtils.GetRarityColor(item.Rarity)),
                (gold.ToString(), Color.White));
        }
    }
}

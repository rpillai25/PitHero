using System;
using System.Collections.Generic;
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
    /// Auto-sells the weakest excess items. Call-driven (no update loop), two entry points:
    /// <see cref="TrySellDownToThreshold"/> runs from the pre-jump pass (issue #411) and sells one item at
    /// a time until the bag drops below <see cref="InventorySellPercent"/> full; <see cref="TryMakeRoom"/>
    /// is the in-pit safety net OpenChestAction invokes before adding a chest item to a full bag.
    /// Items in an active synergy or under a placed stencil are never sold; gear rarities and gear
    /// categories can be excluded via RarityAllowed / GearTypeAllowed (both edited from the
    /// "Gear Sell Options" dialog). Consumables can be excluded or floored per catalog entry via
    /// ConsumableSellAllowed / ConsumableKeepStacks (the "Consumable Sell Options" dialog); the same
    /// Keep Stacks array is what auto-purchase tops up to, so selling never undoes a purchase.
    /// ConsumablesFirst picks whether the weakest consumable or the weakest gear (compared across all
    /// gear types at once) sells first. Gear that is an upgrade for someone in the party is the last
    /// tier — sold only when nothing else can clear the space.
    /// </summary>
    public class AutoSellExcessItemsService
    {
        private const string SellSource = "auto_excess";

        /// <summary>Master toggle. On by default (unlike other automation toggles) — this guards against loot loss.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Sell priority: true sells consumables before gear (default), false sells gear before consumables.</summary>
        public bool ConsumablesFirst { get; set; } = true;

        /// <summary>Whether gear of each rarity may be auto-sold, indexed by ItemRarity. All true by default.</summary>
        public bool[] RarityAllowed { get; } = new bool[5];

        /// <summary>Whether gear of each category may be auto-sold, indexed by GearCategory. All true by default.</summary>
        public bool[] GearTypeAllowed { get; } = new bool[GearCategoryUtils.Count];

        /// <summary>Whether each catalog consumable may be auto-sold, indexed by ConsumableCatalog index. All true by default.</summary>
        public bool[] ConsumableSellAllowed { get; } = new bool[ConsumableCatalog.Count];

        /// <summary>
        /// Keep Stacks per catalog consumable, indexed by ConsumableCatalog index. 1 by default. Auto-sell
        /// never sells below it and auto-purchase buys exactly up to it — <see cref="AutoItemPurchaseService"/>
        /// shares this very array, so the two can never disagree.
        /// </summary>
        public int[] ConsumableKeepStacks { get; } = new int[ConsumableCatalog.Count];

        /// <summary>Lowest and highest Keep Stacks value the options slider offers (0 = keep none).</summary>
        public const int MinKeepStacks = 0;
        public const int MaxKeepStacks = 3;

        private int _inventorySellPercent = GameConfig.AutoSellInventoryPercentDefault;

        /// <summary>
        /// Bag fill percentage at which the pre-jump sweep sells (0-100). 100 means only a completely
        /// full bag, 0 means every jump sells everything eligible.
        /// </summary>
        public int InventorySellPercent
        {
            get => _inventorySellPercent;
            set
            {
                int v = value;
                if (v < GameConfig.AutoSellInventoryPercentMin) v = GameConfig.AutoSellInventoryPercentMin;
                if (v > GameConfig.AutoSellInventoryPercentMax) v = GameConfig.AutoSellInventoryPercentMax;
                _inventorySellPercent = v;
            }
        }

        public AutoSellExcessItemsService()
        {
            for (int i = 0; i < RarityAllowed.Length; i++)
                RarityAllowed[i] = true;
            for (int i = 0; i < GearTypeAllowed.Length; i++)
                GearTypeAllowed[i] = true;
            for (int i = 0; i < ConsumableSellAllowed.Length; i++)
                ConsumableSellAllowed[i] = true;
            for (int i = 0; i < ConsumableKeepStacks.Length; i++)
                ConsumableKeepStacks[i] = 1;
        }

        /// <summary>True when gear of the given rarity may be auto-sold. Consumables are never rarity-filtered.</summary>
        public bool IsRarityAllowed(ItemRarity rarity)
        {
            int i = (int)rarity;
            return i < 0 || i >= RarityAllowed.Length || RarityAllowed[i];
        }

        /// <summary>True when gear of the given kind's category may be auto-sold. Consumables are never type-filtered.</summary>
        public bool IsGearTypeAllowed(ItemKind kind)
        {
            return GearCategoryUtils.IsAllowed(GearTypeAllowed, kind);
        }

        /// <summary>True when the given consumable may be auto-sold. Unknown (non-catalog) consumables are sellable.</summary>
        public bool IsConsumableSellAllowed(Consumable consumable)
        {
            int i = ConsumableCatalog.IndexOfSpriteName(consumable?.SpriteName);
            return i < 0 || ConsumableSellAllowed[i];
        }

        /// <summary>Stacks of the given consumable that must remain in the bag after an auto-sale (0 for non-catalog items).</summary>
        public int GetKeepStacks(Consumable consumable)
        {
            int i = ConsumableCatalog.IndexOfSpriteName(consumable?.SpriteName);
            return i < 0 ? 0 : ConsumableKeepStacks[i];
        }

        /// <summary>True when the bag holds at least <see cref="InventorySellPercent"/> percent of its capacity.</summary>
        public bool IsAtOrAboveSellThreshold(ItemBag bag)
        {
            if (bag == null || bag.Capacity <= 0)
                return false;
            return bag.Count * 100 >= InventorySellPercent * bag.Capacity;
        }

        /// <summary>
        /// Attempts to free a bag slot for the incoming item by selling the weakest sellable item.
        /// Returns SoldIncoming when the incoming item itself was sold (caller must not add it to the bag).
        /// <paramref name="gearIsUpgrade"/> marks gear the party could still use; it sells last.
        /// </summary>
        public AutoSellOutcome TryMakeRoom(ItemBag bag, IItem incoming, Func<IGear, bool> gearIsUpgrade = null)
        {
            if (!Enabled || bag == null || incoming == null || !bag.IsFull)
                return AutoSellOutcome.None;

            // A consumable that can absorb into an existing non-full stack needs no empty slot
            if (incoming is Consumable consumable && CanStackInto(bag, consumable))
                return AutoSellOutcome.None;

            var grid = RefreshGrid();
            Func<int, bool> isProtected = grid != null ? grid.IsBagIndexProtected : (Func<int, bool>)null;

            var selection = SelectNext(bag, incoming, isProtected, gearIsUpgrade);
            if (!selection.HasSelection)
                return AutoSellOutcome.None;

            if (selection.SellIncoming)
            {
                int gold = ItemSellHelper.SellItemDirect(incoming, SellSource);
                EmitConsole(incoming, gold);
                return AutoSellOutcome.SoldIncoming;
            }

            SellBagSelection(bag, selection.BagIndex, grid, null);
            return AutoSellOutcome.SoldBagItem;
        }

        /// <summary>
        /// The pre-jump sweep (issue #411): while the bag is at or above the sell threshold, sells the
        /// weakest eligible item, stopping as soon as the bag drops below it or nothing is sellable.
        /// Each sold item's name is appended to <paramref name="soldNamesOut"/> (may be null) so the
        /// purchase pass that follows never buys the same thing straight back. Returns the number sold.
        /// </summary>
        public int TrySellDownToThreshold(ItemBag bag, Func<IGear, bool> gearIsUpgrade, List<string> soldNamesOut)
        {
            if (!Enabled || bag == null)
                return 0;

            var grid = RefreshGrid();
            Func<int, bool> isProtected = grid != null ? grid.IsBagIndexProtected : (Func<int, bool>)null;

            int sold = 0;
            while (IsAtOrAboveSellThreshold(bag))
            {
                var selection = SelectNext(bag, null, isProtected, gearIsUpgrade);
                if (!selection.HasSelection || selection.BagIndex < 0)
                    break;
                SellBagSelection(bag, selection.BagIndex, grid, soldNamesOut);
                sold++;
            }
            return sold;
        }

        private SellSelection SelectNext(ItemBag bag, IItem incoming, Func<int, bool> isProtected, Func<IGear, bool> gearIsUpgrade)
        {
            return ExcessItemSellSelector.Select(bag, incoming, isProtected, IsRarityAllowed, ConsumablesFirst, IsGearTypeAllowed,
                IsConsumableSellAllowed, GetKeepStacks, gearIsUpgrade);
        }

        /// <summary>Sells the bag slot, refreshes the grid and console, and records the sold name.</summary>
        private static void SellBagSelection(ItemBag bag, int bagIndex, InventoryGrid grid, List<string> soldNamesOut)
        {
            var soldItem = bag.GetSlotItem(bagIndex);
            int earned = ItemSellHelper.SellBagItem(bag, bagIndex, SellSource);
            grid?.UpdateItemsFromBag();
            InventorySelectionManager.OnInventoryChanged?.Invoke();
            EmitConsole(soldItem, earned);
            if (soldNamesOut != null && soldItem != null)
                soldNamesOut.Add(soldItem.Name);
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

        /// <summary>Refreshes the hero grid from the bag so the synergy cache matches current contents before protection checks.</summary>
        private static InventoryGrid RefreshGrid()
        {
            if (Core.Instance == null)
                return null;
            var grid = Core.Services?.GetService<SettingsUI>()?.HeroUI?.GetInventoryGrid();
            grid?.UpdateItemsFromBag();
            return grid;
        }

        private static void EmitConsole(IItem item, int gold)
        {
            if (Core.Instance == null || item == null)
                return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleAutoSoldItem,
                (item.DisplayName, RarityUtils.GetRarityColor(item.Rarity)),
                (gold.ToString(), Color.White));
        }
    }
}

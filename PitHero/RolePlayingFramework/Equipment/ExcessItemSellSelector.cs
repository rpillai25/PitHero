using System;
using RolePlayingFramework.Inventory;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Result of an excess-item sell selection.</summary>
    public struct SellSelection
    {
        /// <summary>Bag slot index of the item to sell, or -1 when nothing (or the incoming item) was chosen.</summary>
        public int BagIndex;
        /// <summary>True when the incoming item itself is the weakest candidate and should be sold directly.</summary>
        public bool SellIncoming;
        /// <summary>True when the pick came from the last-resort tier: gear that is an upgrade for someone in the party.</summary>
        public bool IsUpgrade;
        /// <summary>True when either a bag item or the incoming item was selected.</summary>
        public bool HasSelection => SellIncoming || BagIndex >= 0;

        public static SellSelection None => new SellSelection { BagIndex = -1 };
        public static SellSelection Incoming => new SellSelection { BagIndex = -1, SellIncoming = true };
        public static SellSelection AtIndex(int bagIndex) => new SellSelection { BagIndex = bagIndex };
    }

    /// <summary>
    /// Pure selection logic for auto-selling excess items (issue: auto-sell excess items; issue #411).
    /// The player chooses whether consumables or gear sell first; within the second category items are only
    /// considered when the first category has no sellable candidate. Among gear, weakness is compared across
    /// ALL gear types at once so a lone strong item of one type never sells before a weak item of another.
    /// The incoming item (when given) participates in the comparison so junk loot never displaces better items.
    /// Consumables can additionally be excluded outright or protected by a keep-stacks floor
    /// (the "Consumable Sell Options" dialog) so auto-sell never drains the party's potions to zero.
    /// Gear that is an upgrade for someone in the party (<paramref name="gearIsUpgrade"/>) is the absolute
    /// last tier: it is skipped by both category passes and only picked — weakest upgrade first — when
    /// nothing else can clear the space, because inventory must always be clearable.
    /// </summary>
    public static class ExcessItemSellSelector
    {
        /// <summary>Sentinel index representing the incoming (not yet in bag) item.</summary>
        private const int IncomingIndex = int.MaxValue;

        /// <summary>
        /// Selects the item to sell to free a bag slot for <paramref name="incoming"/> (null for a plain
        /// sweep of the bag). Consumable pass: weakest-effect unprotected consumable stack (HP+MP restore,
        /// then sell price, then stack count), filtered by <paramref name="consumableSellAllowed"/> and
        /// <paramref name="consumableKeepStacks"/> (stacks of that consumable that must remain in the bag;
        /// null means "no floor"). Gear pass: weakest unprotected gear across all types by gear score (then
        /// rarity, then sell price), filtered by <paramref name="rarityAllowed"/> and
        /// <paramref name="gearTypeAllowed"/> (gear only; null means "allow everything") and skipping gear
        /// for which <paramref name="gearIsUpgrade"/> is true. <paramref name="consumablesFirst"/> picks
        /// which pass runs first; the other pass only runs when the first finds no candidate. When both come
        /// up empty the upgrade gear is considered as a last resort. Bag items win ties against the incoming item.
        /// </summary>
        public static SellSelection Select(ItemBag bag, IItem incoming, Func<int, bool> isProtectedBagIndex, Func<ItemRarity, bool> rarityAllowed, bool consumablesFirst = true, Func<ItemKind, bool> gearTypeAllowed = null, Func<Consumable, bool> consumableSellAllowed = null, Func<Consumable, int> consumableKeepStacks = null, Func<IGear, bool> gearIsUpgrade = null)
        {
            if (bag == null)
                return SellSelection.None;

            var first = consumablesFirst
                ? SelectConsumable(bag, incoming, isProtectedBagIndex, consumableSellAllowed, consumableKeepStacks)
                : SelectGear(bag, incoming, isProtectedBagIndex, rarityAllowed, gearTypeAllowed, gearIsUpgrade, UpgradeTier.Exclude);
            if (first.HasSelection)
                return first;

            var second = consumablesFirst
                ? SelectGear(bag, incoming, isProtectedBagIndex, rarityAllowed, gearTypeAllowed, gearIsUpgrade, UpgradeTier.Exclude)
                : SelectConsumable(bag, incoming, isProtectedBagIndex, consumableSellAllowed, consumableKeepStacks);
            if (second.HasSelection)
                return second;

            // Last resort: the weakest piece of gear somebody could still use
            if (gearIsUpgrade == null)
                return SellSelection.None;
            var upgrade = SelectGear(bag, incoming, isProtectedBagIndex, rarityAllowed, gearTypeAllowed, gearIsUpgrade, UpgradeTier.Only);
            upgrade.IsUpgrade = upgrade.HasSelection;
            return upgrade;
        }

        /// <summary>Which gear the gear pass looks at with respect to the upgrade test.</summary>
        private enum UpgradeTier
        {
            /// <summary>Everything the filters allow except upgrades (no test ⇒ everything).</summary>
            Exclude,
            /// <summary>Only upgrades.</summary>
            Only
        }

        /// <summary>Picks the weakest-effect unprotected consumable stack, or none.</summary>
        private static SellSelection SelectConsumable(ItemBag bag, IItem incoming, Func<int, bool> isProtectedBagIndex, Func<Consumable, bool> sellAllowed, Func<Consumable, int> keepStacks)
        {
            int bestIndex = -1;
            long bestKeyA = 0, bestKeyB = 0, bestKeyC = 0;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is Consumable c && !IsProtected(isProtectedBagIndex, i) &&
                    ConsumableOk(bag, c, sellAllowed, keepStacks, inBag: true))
                    ConsiderConsumable(c, i, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);
            }
            if (incoming is Consumable incomingConsumable &&
                ConsumableOk(bag, incomingConsumable, sellAllowed, keepStacks, inBag: false))
                ConsiderConsumable(incomingConsumable, IncomingIndex, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);

            return ToSelection(bestIndex);
        }

        /// <summary>
        /// True when the consumable may be sold: it is allowed, and after the sale the bag still holds
        /// at least the required stack count. A bag stack removes itself from the count; the incoming
        /// item was never in the bag, so the existing stacks must already satisfy the floor.
        /// </summary>
        private static bool ConsumableOk(ItemBag bag, Consumable c, Func<Consumable, bool> sellAllowed, Func<Consumable, int> keepStacks, bool inBag)
        {
            if (sellAllowed != null && !sellAllowed(c))
                return false;
            if (keepStacks == null)
                return true;

            int floor = keepStacks(c);
            if (floor <= 0)
                return true;

            int remaining = CountStacks(bag, c.Name) - (inBag ? 1 : 0);
            return remaining >= floor;
        }

        /// <summary>
        /// Number of bag slots holding the given consumable (a partial stack still counts as one).
        /// Matches by name — the same identity bag stacking uses — not by sprite.
        /// </summary>
        private static int CountStacks(ItemBag bag, string name)
        {
            int count = 0;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is Consumable c && c.Name == name)
                    count++;
            }
            return count;
        }

        /// <summary>Picks the weakest unprotected gear across all gear types (rarity-, type- and tier-filtered), or none.</summary>
        private static SellSelection SelectGear(ItemBag bag, IItem incoming, Func<int, bool> isProtectedBagIndex, Func<ItemRarity, bool> rarityAllowed, Func<ItemKind, bool> gearTypeAllowed, Func<IGear, bool> gearIsUpgrade, UpgradeTier tier)
        {
            int bestIndex = -1;
            long bestKeyA = 0, bestKeyB = 0, bestKeyC = 0;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is IGear g && RarityOk(rarityAllowed, g.Rarity) && GearTypeOk(gearTypeAllowed, g.Kind) &&
                    TierOk(gearIsUpgrade, g, tier) && !IsProtected(isProtectedBagIndex, i))
                    ConsiderGear(g, i, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);
            }
            if (incoming is IGear incomingGear && RarityOk(rarityAllowed, incomingGear.Rarity) && GearTypeOk(gearTypeAllowed, incomingGear.Kind) &&
                TierOk(gearIsUpgrade, incomingGear, tier))
                ConsiderGear(incomingGear, IncomingIndex, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);

            return ToSelection(bestIndex);
        }

        private static SellSelection ToSelection(int bestIndex)
        {
            if (bestIndex < 0)
                return SellSelection.None;
            return bestIndex == IncomingIndex ? SellSelection.Incoming : SellSelection.AtIndex(bestIndex);
        }

        private static bool IsProtected(Func<int, bool> isProtectedBagIndex, int bagIndex)
            => isProtectedBagIndex != null && isProtectedBagIndex(bagIndex);

        private static bool RarityOk(Func<ItemRarity, bool> rarityAllowed, ItemRarity rarity)
            => rarityAllowed == null || rarityAllowed(rarity);

        private static bool GearTypeOk(Func<ItemKind, bool> gearTypeAllowed, ItemKind kind)
            => gearTypeAllowed == null || gearTypeAllowed(kind);

        /// <summary>With no upgrade test every piece is ordinary gear; otherwise the tier decides which side is wanted.</summary>
        private static bool TierOk(Func<IGear, bool> gearIsUpgrade, IGear gear, UpgradeTier tier)
        {
            if (gearIsUpgrade == null)
                return tier == UpgradeTier.Exclude;
            bool isUpgrade = gearIsUpgrade(gear);
            return tier == UpgradeTier.Only ? isUpgrade : !isUpgrade;
        }

        /// <summary>Weakness key: restore effect, then sell price, then stack count. Lower wins; bag index breaks final ties.</summary>
        private static void ConsiderConsumable(Consumable c, int index, ref int bestIndex, ref long keyA, ref long keyB, ref long keyC)
        {
            ItemWeaknessRanking.ConsumableKey(c, c.StackCount, out long a, out long b, out long k);
            if (bestIndex < 0 || ItemWeaknessRanking.IsWeaker(a, b, k, index, keyA, keyB, keyC, bestIndex))
            {
                bestIndex = index; keyA = a; keyB = b; keyC = k;
            }
        }

        /// <summary>Weakness key: gear score, then rarity, then sell price. Lower wins; bag index breaks final ties.</summary>
        private static void ConsiderGear(IGear g, int index, ref int bestIndex, ref long keyA, ref long keyB, ref long keyC)
        {
            ItemWeaknessRanking.GearKey(g, out long a, out long b, out long c);
            if (bestIndex < 0 || ItemWeaknessRanking.IsWeaker(a, b, c, index, keyA, keyB, keyC, bestIndex))
            {
                bestIndex = index; keyA = a; keyB = b; keyC = c;
            }
        }
    }
}

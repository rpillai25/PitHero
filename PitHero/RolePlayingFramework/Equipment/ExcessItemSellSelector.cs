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
        /// <summary>True when either a bag item or the incoming item was selected.</summary>
        public bool HasSelection => SellIncoming || BagIndex >= 0;

        public static SellSelection None => new SellSelection { BagIndex = -1 };
        public static SellSelection Incoming => new SellSelection { BagIndex = -1, SellIncoming = true };
        public static SellSelection AtIndex(int bagIndex) => new SellSelection { BagIndex = bagIndex };
    }

    /// <summary>
    /// Pure selection logic for auto-selling excess items when the bag is full (issue: auto-sell excess items).
    /// Consumables are always sold before gear; among gear, weakness is compared across ALL gear types at once
    /// so a lone strong item of one type never sells before a weak item of another. The incoming item participates
    /// in the comparison so junk loot never displaces better items.
    /// </summary>
    public static class ExcessItemSellSelector
    {
        /// <summary>Sentinel index representing the incoming (not yet in bag) item.</summary>
        private const int IncomingIndex = int.MaxValue;

        /// <summary>
        /// Selects the item to sell to free a bag slot for <paramref name="incoming"/>.
        /// Step 1: weakest-effect unprotected consumable stack (HP+MP restore, then sell price, then stack count).
        /// Step 2 (only if no sellable consumable): weakest unprotected gear across all types by gear score
        /// (then rarity, then sell price), filtered by <paramref name="rarityAllowed"/> (gear only).
        /// Bag items win ties against the incoming item.
        /// </summary>
        public static SellSelection Select(ItemBag bag, IItem incoming, Func<int, bool> isProtectedBagIndex, Func<ItemRarity, bool> rarityAllowed)
        {
            if (bag == null)
                return SellSelection.None;

            // Step 1: consumables
            int bestIndex = -1;
            long bestKeyA = 0, bestKeyB = 0, bestKeyC = 0;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is Consumable c && !IsProtected(isProtectedBagIndex, i))
                    ConsiderConsumable(c, i, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);
            }
            if (incoming is Consumable incomingConsumable)
                ConsiderConsumable(incomingConsumable, IncomingIndex, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);

            if (bestIndex >= 0)
                return bestIndex == IncomingIndex ? SellSelection.Incoming : SellSelection.AtIndex(bestIndex);

            // Step 2: gear across all types
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is IGear g && RarityOk(rarityAllowed, g.Rarity) && !IsProtected(isProtectedBagIndex, i))
                    ConsiderGear(g, i, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);
            }
            if (incoming is IGear incomingGear && RarityOk(rarityAllowed, incomingGear.Rarity))
                ConsiderGear(incomingGear, IncomingIndex, ref bestIndex, ref bestKeyA, ref bestKeyB, ref bestKeyC);

            if (bestIndex >= 0)
                return bestIndex == IncomingIndex ? SellSelection.Incoming : SellSelection.AtIndex(bestIndex);

            return SellSelection.None;
        }

        private static bool IsProtected(Func<int, bool> isProtectedBagIndex, int bagIndex)
            => isProtectedBagIndex != null && isProtectedBagIndex(bagIndex);

        private static bool RarityOk(Func<ItemRarity, bool> rarityAllowed, ItemRarity rarity)
            => rarityAllowed == null || rarityAllowed(rarity);

        /// <summary>Weakness key: restore effect, then sell price, then stack count. Lower wins; bag index breaks final ties.</summary>
        private static void ConsiderConsumable(Consumable c, int index, ref int bestIndex, ref long keyA, ref long keyB, ref long keyC)
        {
            long a = c.HPRestoreAmount + c.MPRestoreAmount;
            long b = ((IItem)c).GetSellPrice();
            long k = c.StackCount;
            if (bestIndex < 0 || IsWeaker(a, b, k, index, keyA, keyB, keyC, bestIndex))
            {
                bestIndex = index; keyA = a; keyB = b; keyC = k;
            }
        }

        /// <summary>Weakness key: gear score, then rarity, then sell price. Lower wins; bag index breaks final ties.</summary>
        private static void ConsiderGear(IGear g, int index, ref int bestIndex, ref long keyA, ref long keyB, ref long keyC)
        {
            long a = GearAutoEquipService.GetGearScore(g);
            long b = (long)g.Rarity;
            long c = ((IItem)g).GetSellPrice();
            if (bestIndex < 0 || IsWeaker(a, b, c, index, keyA, keyB, keyC, bestIndex))
            {
                bestIndex = index; keyA = a; keyB = b; keyC = c;
            }
        }

        private static bool IsWeaker(long a, long b, long c, int index, long bestA, long bestB, long bestC, int bestIndex)
        {
            if (a != bestA) return a < bestA;
            if (b != bestB) return b < bestB;
            if (c != bestC) return c < bestC;
            return index < bestIndex;
        }
    }
}

namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Shared weakness-ranking keys extracted from ExcessItemSellSelector so the vault eviction
    /// logic can reuse the same ordering without duplicating the comparison math.
    /// All methods are pure (no side effects, no service calls).
    /// </summary>
    public static class ItemWeaknessRanking
    {
        /// <summary>
        /// Computes the three-part weakness key for a gear item.
        /// Lower values indicate weaker gear (more suitable for eviction).
        /// Key: (gear score, rarity index, sell price).
        /// </summary>
        public static void GearKey(IGear g, out long a, out long b, out long c)
        {
            a = GearAutoEquipService.GetGearScore(g);
            b = (long)g.Rarity;
            c = ((IItem)g).GetSellPrice();
        }

        /// <summary>
        /// Computes the three-part weakness key for a consumable item.
        /// Lower values indicate weaker consumables (more suitable for eviction).
        /// Key: (HP+MP restore, sell price, stack count).
        /// <paramref name="stackCount"/> is supplied explicitly because in the vault the
        /// authoritative quantity lives on <c>StackedItem.Quantity</c>, not on
        /// <c>Consumable.StackCount</c>.
        /// </summary>
        public static void ConsumableKey(Consumable c, long stackCount, out long a, out long b, out long kc)
        {
            a  = c.HPRestoreAmount + c.MPRestoreAmount;
            b  = ((IItem)c).GetSellPrice();
            kc = stackCount;
        }

        /// <summary>
        /// Returns true when the candidate (a, b, c, index) is weaker than the current best.
        /// Comparison is lexicographic: a first, then b, then c; on a full key tie the lower
        /// index wins (existing bag/vault slot beats a higher-index or sentinel slot).
        /// </summary>
        public static bool IsWeaker(long a, long b, long c, int index,
                                    long bestA, long bestB, long bestC, int bestIndex)
        {
            if (a != bestA) return a < bestA;
            if (b != bestB) return b < bestB;
            if (c != bestC) return c < bestC;
            return index < bestIndex;
        }
    }
}

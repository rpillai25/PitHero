using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Inventory
{
    /// <summary>Utility for consumable stack absorption logic shared by UI and bag.</summary>
    public static class ConsumableStacking
    {
        /// <summary>Calculates absorb amount from source into target if same consumable type and capacity available.</summary>
        public static bool TryCalculateAbsorb(IItem sourceItem, IItem targetItem, out int toAbsorb)
        {
            toAbsorb = 0;
            if (sourceItem is not Consumable src || targetItem is not Consumable dst)
                return false;
            if (!string.Equals(src.Name, dst.Name, System.StringComparison.Ordinal))
                return false;
            if (dst.StackCount >= dst.StackSize)
                return false;
            int space = dst.StackSize - dst.StackCount;
            if (space <= 0 || src.StackCount <= 0)
                return false;
            toAbsorb = System.Math.Min(space, src.StackCount);
            return toAbsorb > 0;
        }

        /// <summary>Applies absorption from source to target and adjusts source if depleted.</summary>
        public static void ApplyAbsorb(Consumable source, Consumable target, int toAbsorb)
        {
            if (toAbsorb <= 0 || source == null || target == null)
                return;
            target.StackCount += toAbsorb;
            source.StackCount -= toAbsorb;
        }
    }
}

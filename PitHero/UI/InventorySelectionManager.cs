using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Shared utilities for inventory stack absorption.
    /// Selection / click-to-swap state has been removed — drag-and-drop is the only movement model.
    /// </summary>
    public static class InventorySelectionManager
    {
        /// <summary>Callback to refresh all components after any inventory mutation.</summary>
        public static System.Action OnInventoryChanged;

        /// <summary>Returns true if two slots can perform stack absorption and outputs amount to absorb.</summary>
        public static bool CanAbsorbStacks(InventorySlot source, InventorySlot target, out int toAbsorb)
        {
            toAbsorb = 0;
            if (source?.SlotData?.Item is not Consumable src || target?.SlotData?.Item is not Consumable dst)
                return false;
            if (!string.Equals(src.Name, dst.Name, System.StringComparison.Ordinal))
                return false;
            if (src.StackCount >= src.StackSize)
                return false;
            if (dst.StackCount >= dst.StackSize)
                return false;
            int space = dst.StackSize - dst.StackCount;
            if (space <= 0 || src.StackCount <= 0)
                return false;
            toAbsorb = System.Math.Min(space, src.StackCount);
            return toAbsorb > 0;
        }

        /// <summary>Applies absorption from source to target using the specified amount. Clears source if empty.</summary>
        public static void PerformAbsorbStacks(InventorySlot source, InventorySlot target, int toAbsorb)
        {
            if (toAbsorb <= 0) return;
            var src = (Consumable)source.SlotData.Item;
            var dst = (Consumable)target.SlotData.Item;
            dst.StackCount += toAbsorb;
            src.StackCount -= toAbsorb;
            if (src.StackCount <= 0)
            {
                source.SlotData.Item = null;
            }
        }

    }
}

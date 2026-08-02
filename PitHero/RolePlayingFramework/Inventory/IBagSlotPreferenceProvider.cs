using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Inventory
{
    /// <summary>Supplies a preferred empty slot index for an incoming item (or -1 for no preference).</summary>
    public interface IBagSlotPreferenceProvider
    {
        int GetPreferredEmptySlot(ItemBag bag, IItem item);
    }
}

using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores MP.</summary>
    public sealed class FullMPPotion : BaseHPMPPotion
    {
        public FullMPPotion() : base(InventoryTextKey.Inv_FullMPPotion_Name, ItemRarity.Epic, InventoryTextKey.Inv_FullMPPotion_Desc, 500, 0, -1) { }
    }
}

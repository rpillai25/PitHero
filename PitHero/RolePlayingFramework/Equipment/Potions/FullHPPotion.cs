using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP.</summary>
    public sealed class FullHPPotion : BaseHPMPPotion
    {
        public FullHPPotion() : base(InventoryTextKey.Inv_FullHPPotion_Name, ItemRarity.Epic, InventoryTextKey.Inv_FullHPPotion_Desc, 500, -1, 0) { }
    }
}

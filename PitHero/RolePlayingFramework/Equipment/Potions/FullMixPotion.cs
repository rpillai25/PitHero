using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and MP.</summary>
    public sealed class FullMixPotion : BaseHPMPPotion
    {
        public FullMixPotion() : base(InventoryTextKey.Inv_FullMixPotion_Name, ItemRarity.Epic, InventoryTextKey.Inv_FullMixPotion_Desc, 900, -1, -1) { }
    }
}

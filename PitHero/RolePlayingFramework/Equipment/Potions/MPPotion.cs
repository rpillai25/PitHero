using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of MP.</summary>
    public sealed class MPPotion : BaseHPMPPotion
    {
        public MPPotion() : base(InventoryTextKey.Inv_MPPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_MPPotion_Desc, 20, 0, 100) { }
    }
}

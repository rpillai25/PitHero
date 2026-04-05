using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of MP.</summary>
    public sealed class MidMPPotion : BaseHPMPPotion
    {
        public MidMPPotion() : base(InventoryTextKey.Inv_MidMPPotion_Name, ItemRarity.Rare, InventoryTextKey.Inv_MidMPPotion_Desc, 100, 0, 500) { }
    }
}

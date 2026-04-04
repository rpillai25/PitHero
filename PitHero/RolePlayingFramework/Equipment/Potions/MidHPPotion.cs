using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP.</summary>
    public sealed class MidHPPotion : BaseHPMPPotion
    {
        public MidHPPotion() : base(InventoryTextKey.Inv_MidHPPotion_Name, ItemRarity.Rare, InventoryTextKey.Inv_MidHPPotion_Desc, 100, 500, 0) { }
    }
}

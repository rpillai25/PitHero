using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of HP.</summary>
    public sealed class HPPotion : BaseHPMPPotion
    {
        public HPPotion() : base(InventoryTextKey.Inv_HPPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_HPPotion_Desc, 20, 100, 0) { }
    }
}

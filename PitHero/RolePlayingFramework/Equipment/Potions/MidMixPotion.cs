using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP and MP.</summary>
    public sealed class MidMixPotion : BaseHPMPPotion
    {
        public MidMixPotion() : base(InventoryTextKey.Inv_MidMixPotion_Name, ItemRarity.Rare, InventoryTextKey.Inv_MidMixPotion_Desc, 180, 500, 500) { }
    }
}

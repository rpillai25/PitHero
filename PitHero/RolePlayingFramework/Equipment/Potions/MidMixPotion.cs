using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP and MP.</summary>
    public sealed class MidMixPotion : BaseHPMPPotion
    {
        public MidMixPotion() : base(InventoryTextKey.Inv_MidMixPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_MidMixPotion_Desc, 180, 500, 500) { }
        /// <summary>Returns a new MidMixPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new MidMixPotion();
    }
}

using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores moderate HP and MP.</summary>
    public sealed class MixPotion : BaseHPMPPotion
    {
        public MixPotion() : base(InventoryTextKey.Inv_MixPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_MixPotion_Desc, 30, 100, 100) { }
        /// <summary>Returns a new MixPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new MixPotion();
    }
}

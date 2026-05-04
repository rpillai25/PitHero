using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of MP.</summary>
    public sealed class MidMPPotion : BaseHPMPPotion
    {
        public MidMPPotion() : base(InventoryTextKey.Inv_MidMPPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_MidMPPotion_Desc, 100, 0, 500) { }
        /// <summary>Returns a new MidMPPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new MidMPPotion();
    }
}

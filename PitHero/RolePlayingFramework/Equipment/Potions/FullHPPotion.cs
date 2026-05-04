using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP.</summary>
    public sealed class FullHPPotion : BaseHPMPPotion
    {
        public FullHPPotion() : base(InventoryTextKey.Inv_FullHPPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_FullHPPotion_Desc, 500, -1, 0) { }
        /// <summary>Returns a new FullHPPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new FullHPPotion();
    }
}

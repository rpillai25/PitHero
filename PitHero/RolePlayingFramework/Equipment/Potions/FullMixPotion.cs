using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and MP.</summary>
    public sealed class FullMixPotion : BaseHPMPPotion
    {
        public FullMixPotion() : base(InventoryTextKey.Inv_FullMixPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_FullMixPotion_Desc, 900, -1, -1) { }
        /// <summary>Returns a new FullMixPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new FullMixPotion();
    }
}

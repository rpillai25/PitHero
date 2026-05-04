using PitHero;
namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP.</summary>
    public sealed class MidHPPotion : BaseHPMPPotion
    {
        public MidHPPotion() : base(InventoryTextKey.Inv_MidHPPotion_Name, ItemRarity.Normal, InventoryTextKey.Inv_MidHPPotion_Desc, 100, 500, 0) { }
        /// <summary>Returns a new MidHPPotion instance with StackCount = 1.</summary>
        public override Consumable CreateFreshInstance() => new MidHPPotion();
    }
}

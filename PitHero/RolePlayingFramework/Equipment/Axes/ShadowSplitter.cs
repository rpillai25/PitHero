using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Shadow Splitter gear.</summary>
    public static class ShadowSplitter
    {
        private const int PitLevel = 16;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_ShadowSplitter_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_ShadowSplitter_Desc,
                525,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

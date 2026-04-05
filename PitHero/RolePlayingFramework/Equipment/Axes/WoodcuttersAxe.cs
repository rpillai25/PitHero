using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Woodcutter's Axe gear.</summary>
    public static class WoodcuttersAxe
    {
        private const int PitLevel = 3;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_WoodcuttersAxe_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_WoodcuttersAxe_Desc,
                100,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

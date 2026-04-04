using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Wooden Spear gear.</summary>
    public static class WoodenSpear
    {
        private const int PitLevel = 2;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_WoodenSpear_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_WoodenSpear_Desc,
                75,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

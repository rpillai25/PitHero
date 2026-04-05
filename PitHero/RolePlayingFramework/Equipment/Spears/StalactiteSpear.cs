using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Stalactite Spear gear.</summary>
    public static class StalactiteSpear
    {
        private const int PitLevel = 19;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_StalactiteSpear_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_StalactiteSpear_Desc,
                600,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Crystal Cleaver gear.</summary>
    public static class CrystalCleaver
    {
        private const int PitLevel = 13;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_CrystalCleaver_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_CrystalCleaver_Desc,
                450,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

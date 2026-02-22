using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

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
                "CrystalCleaver",
                ItemKind.WeaponSword,
                Rarity,
                "Axe with crystalline blade edge.",
                450,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

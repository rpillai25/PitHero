using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Rusty Dagger gear.</summary>
    public static class RustyDagger
    {
        private const int PitLevel = 1;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "RustyDagger",
                ItemKind.WeaponSword,
                Rarity,
                $"Old corroded blade.",
                40,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

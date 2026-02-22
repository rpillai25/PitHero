using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

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
                "StalactiteSpear",
                ItemKind.WeaponSword,
                Rarity,
                "Spear crafted from a massive cave formation.",
                600,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

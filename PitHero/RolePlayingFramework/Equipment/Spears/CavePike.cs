using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Cave Pike gear.</summary>
    public static class CavePike
    {
        private const int PitLevel = 11;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "CavePike",
                ItemKind.WeaponSword,
                Rarity,
                "Long spear designed for cave defense.",
                400,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

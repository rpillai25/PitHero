using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Silent Fang gear.</summary>
    public static class SilentFang
    {
        private const int PitLevel = 8;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "SilentFang",
                ItemKind.WeaponSword,
                Rarity,
                "Slim blade for stealth attacks.",
                225,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

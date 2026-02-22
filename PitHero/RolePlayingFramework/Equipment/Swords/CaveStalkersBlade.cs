using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Cave Stalker's Blade gear.</summary>
    public static class CaveStalkersBlade
    {
        private const int PitLevel = 3;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "CaveStalkersBlade",
                ItemKind.WeaponSword,
                Rarity,
                $"Dark steel blade favored by cave dwellers.",
                100,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

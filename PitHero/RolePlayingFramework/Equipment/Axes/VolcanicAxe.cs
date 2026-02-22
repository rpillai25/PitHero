using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Volcanic Axe gear.</summary>
    public static class VolcanicAxe
    {
        private const int PitLevel = 20;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "VolcanicAxe",
                ItemKind.WeaponSword,
                Rarity,
                "Axe forged in volcanic heat.",
                625,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Serpent's Tooth gear.</summary>
    public static class SerpentsTooth
    {
        private const int PitLevel = 12;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "SerpentsTooth",
                ItemKind.WeaponSword,
                Rarity,
                "Curved dagger shaped like a snake fang.",
                425,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

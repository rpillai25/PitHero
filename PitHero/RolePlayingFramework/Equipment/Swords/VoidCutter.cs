using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Void Cutter gear.</summary>
    public static class VoidCutter
    {
        private const int PitLevel = 14;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "VoidCutter",
                ItemKind.WeaponSword,
                Rarity,
                "Dark blade that seems to drink in light.",
                475,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

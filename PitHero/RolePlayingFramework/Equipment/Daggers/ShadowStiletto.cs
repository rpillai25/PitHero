using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Shadow Stiletto gear.</summary>
    public static class ShadowStiletto
    {
        private const int PitLevel = 17;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "ShadowStiletto",
                ItemKind.WeaponKnife,
                Rarity,
                "Thin piercing blade that vanishes in shadows.",
                550,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

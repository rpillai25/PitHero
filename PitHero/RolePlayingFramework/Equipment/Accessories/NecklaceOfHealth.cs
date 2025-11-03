using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Necklace of Health gear.</summary>
    public static class NecklaceOfHealth
    {
        private const int PitLevel = 20;
        private const ItemRarity Rarity = ItemRarity.Rare;

        public static Gear Create()
        {
            int statBonus = BalanceConfig.CalculateEquipmentStatBonus(PitLevel, Rarity);
            // HP bonus scales with stat bonus for vitality-focused items
            int hpBonus = statBonus * 5;
            return new Gear(
                "NecklaceOfHealth",
                ItemKind.Accessory,
                Rarity,
                $"+{hpBonus} HP, +{statBonus} Vitality",
                150,
                new StatBlock(0, 0, statBonus, 0),
                hp: hpBonus,
                elementalProps: new ElementalProperties(ElementType.Light));
        }
    }
}

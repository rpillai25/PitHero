using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Protect Ring gear.</summary>
    public static class ProtectRing
    {
        private const int PitLevel = 12;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int statBonus = BalanceConfig.CalculateEquipmentStatBonus(PitLevel, Rarity);
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "ProtectRing",
                ItemKind.Accessory,
                Rarity,
                $"+{defenseBonus} Defense, +{statBonus} Vitality",
                120,
                new StatBlock(0, 0, statBonus, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

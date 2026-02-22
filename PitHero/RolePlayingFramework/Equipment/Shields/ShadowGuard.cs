using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Shadow Guard gear.</summary>
    public static class ShadowGuard
    {
        private const int PitLevel = 14;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "ShadowGuard",
                ItemKind.Shield,
                Rarity,
                "Dark shield that bends shadows.",
                550,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

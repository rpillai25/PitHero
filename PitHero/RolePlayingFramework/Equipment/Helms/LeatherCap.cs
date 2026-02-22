using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Leather Cap gear.</summary>
    public static class LeatherCap
    {
        private const int PitLevel = 2;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "LeatherCap",
                ItemKind.HatHeadband,
                Rarity,
                "Basic leather headwear.",
                60,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Winged Helm gear.</summary>
    public static class WingedHelm
    {
        private const int PitLevel = 19;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "WingedHelm",
                ItemKind.HatHelm,
                Rarity,
                "Helmet with decorative wings.",
                800,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

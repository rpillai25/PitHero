using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Padded Coif gear.</summary>
    public static class PaddedCoif
    {
        private const int PitLevel = 4;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "PaddedCoif",
                ItemKind.HatHeadband,
                Rarity,
                "Quilted head protection.",
                120,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

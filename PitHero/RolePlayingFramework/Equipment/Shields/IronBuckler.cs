using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Iron Buckler gear.</summary>
    public static class IronBuckler
    {
        private const int PitLevel = 8;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "IronBuckler",
                ItemKind.Shield,
                Rarity,
                "Small all-metal shield.",
                225,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

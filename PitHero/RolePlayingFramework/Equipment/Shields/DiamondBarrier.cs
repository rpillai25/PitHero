using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Diamond Barrier gear.</summary>
    public static class DiamondBarrier
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "DiamondBarrier",
                ItemKind.Shield,
                Rarity,
                "Ultra-hard diamond shield.",
                1000,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

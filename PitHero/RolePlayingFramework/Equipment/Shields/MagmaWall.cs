using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Magma Wall gear.</summary>
    public static class MagmaWall
    {
        private const int PitLevel = 24;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "MagmaWall",
                ItemKind.Shield,
                Rarity,
                "Molten-core defensive barrier.",
                1050,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

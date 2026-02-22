using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Miner's Axe gear.</summary>
    public static class MinersAxe
    {
        private const int PitLevel = 7;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "MinersAxe",
                ItemKind.WeaponSword,
                Rarity,
                $"Heavy mining tool repurposed for combat.",
                200,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

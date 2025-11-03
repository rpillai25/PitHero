using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Ring of Power gear.</summary>
    public static class RingOfPower
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int statBonus = BalanceConfig.CalculateEquipmentStatBonus(PitLevel, Rarity);
            return new Gear(
                "RingOfPower",
                ItemKind.Accessory,
                Rarity,
                $"+{statBonus} Strength",
                150,
                new StatBlock(statBonus, 0, 0, 0),
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

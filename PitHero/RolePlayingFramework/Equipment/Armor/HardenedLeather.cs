using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Hardened Leather gear.</summary>
    public static class HardenedLeather
    {
        private const int PitLevel = 8;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "HardenedLeather",
                ItemKind.ArmorGi,
                Rarity,
                "Treated leather with increased durability.",
                240,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

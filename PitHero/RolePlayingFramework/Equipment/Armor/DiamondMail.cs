using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Diamond Mail gear.</summary>
    public static class DiamondMail
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "DiamondMail",
                ItemKind.ArmorMail,
                Rarity,
                "Armor reinforced with diamond.",
                1000,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

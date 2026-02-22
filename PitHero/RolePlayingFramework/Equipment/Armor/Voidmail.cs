using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Voidmail gear.</summary>
    public static class Voidmail
    {
        private const int PitLevel = 18;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "Voidmail",
                ItemKind.ArmorMail,
                Rarity,
                "Pitch-black armor that absorbs light.",
                750,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

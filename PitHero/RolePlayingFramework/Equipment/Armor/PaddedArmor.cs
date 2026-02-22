using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Padded Armor gear.</summary>
    public static class PaddedArmor
    {
        private const int PitLevel = 4;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "PaddedArmor",
                ItemKind.ArmorGi,
                Rarity,
                "Quilted protective layers.",
                120,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

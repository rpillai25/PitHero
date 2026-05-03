using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Abyss Plate gear.</summary>
    public static class AbyssPlate
    {
        private const int PitLevel = 22;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_AbyssPlate_Name,
                ItemKind.ArmorMail,
                Rarity,
                InventoryTextKey.Inv_AbyssPlate_Desc,
                950,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

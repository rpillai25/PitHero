using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Steel Cuirass gear.</summary>
    public static class SteelCuirass
    {
        private const int PitLevel = 19;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_SteelCuirass_Name,
                ItemKind.ArmorMail,
                Rarity,
                InventoryTextKey.Inv_SteelCuirass_Desc,
                800,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

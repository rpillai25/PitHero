using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Shadow Cowl gear.</summary>
    public static class ShadowCowl
    {
        private const int PitLevel = 14;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_ShadowCowl_Name,
                ItemKind.HatHeadband,
                Rarity,
                InventoryTextKey.Inv_ShadowCowl_Desc,
                550,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

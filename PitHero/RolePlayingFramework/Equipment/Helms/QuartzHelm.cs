using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Quartz Helm gear.</summary>
    public static class QuartzHelm
    {
        private const int PitLevel = 20;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_QuartzHelm_Name,
                ItemKind.HatHelm,
                Rarity,
                InventoryTextKey.Inv_QuartzHelm_Desc,
                850,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Pit Lord's Aegis gear.</summary>
    public static class PitLordsAegis
    {
        private const int PitLevel = 25;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_PitLordsAegis_Name,
                ItemKind.Shield,
                Rarity,
                InventoryTextKey.Inv_PitLordsAegis_Desc,
                1100,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

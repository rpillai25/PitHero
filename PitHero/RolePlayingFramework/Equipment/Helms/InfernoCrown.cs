using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Inferno Crown gear.</summary>
    public static class InfernoCrown
    {
        private const int PitLevel = 21;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_InfernoCrown_Name,
                ItemKind.HatHelm,
                Rarity,
                InventoryTextKey.Inv_InfernoCrown_Desc,
                900,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Inferno Guard gear.</summary>
    public static class InfernoGuard
    {
        private const int PitLevel = 21;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_InfernoGuard_Name,
                ItemKind.Shield,
                Rarity,
                InventoryTextKey.Inv_InfernoGuard_Desc,
                900,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Void Barrier gear.</summary>
    public static class VoidBarrier
    {
        private const int PitLevel = 18;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_VoidBarrier_Name,
                ItemKind.Shield,
                Rarity,
                InventoryTextKey.Inv_VoidBarrier_Desc,
                750,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

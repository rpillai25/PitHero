using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Ember Shield gear.</summary>
    public static class EmberShield
    {
        private const int PitLevel = 13;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_EmberShield_Name,
                ItemKind.Shield,
                Rarity,
                InventoryTextKey.Inv_EmberShield_Desc,
                500,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

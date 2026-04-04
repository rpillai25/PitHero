using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Lava Shield gear.</summary>
    public static class LavaShield
    {
        private const int PitLevel = 17;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_LavaShield_Name,
                ItemKind.Shield,
                Rarity,
                InventoryTextKey.Inv_LavaShield_Desc,
                700,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

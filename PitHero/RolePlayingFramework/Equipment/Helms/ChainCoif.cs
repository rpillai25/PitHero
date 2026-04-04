using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Chain Coif gear.</summary>
    public static class ChainCoif
    {
        private const int PitLevel = 6;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_ChainCoif_Name,
                ItemKind.HatHelm,
                Rarity,
                InventoryTextKey.Inv_ChainCoif_Desc,
                175,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

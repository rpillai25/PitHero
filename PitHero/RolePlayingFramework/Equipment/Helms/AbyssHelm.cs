using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Abyss Helm gear.</summary>
    public static class AbyssHelm
    {
        private const int PitLevel = 22;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_AbyssHelm_Name,
                ItemKind.HatHelm,
                Rarity,
                InventoryTextKey.Inv_AbyssHelm_Desc,
                950,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

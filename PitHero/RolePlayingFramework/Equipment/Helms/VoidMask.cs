using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Void Mask gear.</summary>
    public static class VoidMask
    {
        private const int PitLevel = 18;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "VoidMask",
                ItemKind.HatHelm,
                Rarity,
                "Featureless dark mask.",
                750,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}

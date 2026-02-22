using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Stone Hatchet gear.</summary>
    public static class StoneHatchet
    {
        private const int PitLevel = 5;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "StoneHatchet",
                ItemKind.WeaponSword,
                Rarity,
                $"Primitive stone-headed axe.",
                150,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

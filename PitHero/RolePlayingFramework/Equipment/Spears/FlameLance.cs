using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Flame Lance gear.</summary>
    public static class FlameLance
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "FlameLance",
                ItemKind.WeaponSword,
                Rarity,
                "Spear with burning tip.",
                500,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

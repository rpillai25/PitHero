using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Staves
{
    /// <summary>Factory for creating Walking Stick gear.</summary>
    public static class WalkingStick
    {
        private const int PitLevel = 2;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "WalkingStick",
                ItemKind.WeaponStaff,
                Rarity,
                "Simple wooden staff.",
                75,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}

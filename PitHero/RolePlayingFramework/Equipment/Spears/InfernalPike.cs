using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Infernal Pike gear.</summary>
    public static class InfernalPike
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "InfernalPike",
                ItemKind.WeaponSword,
                Rarity,
                "Spear wreathed in eternal flames.",
                700,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

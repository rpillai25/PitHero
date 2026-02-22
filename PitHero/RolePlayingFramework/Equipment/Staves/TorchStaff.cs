using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Staves
{
    /// <summary>Factory for creating Torch Staff gear.</summary>
    public static class TorchStaff
    {
        private const int PitLevel = 6;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "TorchStaff",
                ItemKind.WeaponStaff,
                Rarity,
                "Staff with burning crystal tip.",
                175,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}

using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Hammers
{
    /// <summary>Factory for creating Quake Hammer gear.</summary>
    public static class QuakeHammer
    {
        private const int PitLevel = 18;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "QuakeHammer",
                ItemKind.WeaponKnuckle,
                Rarity,
                "Massive hammer that shakes the ground.",
                575,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}

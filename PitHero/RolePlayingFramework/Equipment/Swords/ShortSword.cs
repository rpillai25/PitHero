using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Short Sword gear.</summary>
    public static class ShortSword
    {
        public static Gear Create() => new Gear(
            "ShortSword",
            ItemKind.WeaponSword,
            ItemRarity.Normal,
            "+3 Attack",
            100,
            new StatBlock(0, 0, 0, 0),
            atk: 3,
            elementalProps: new ElementalProperties(ElementType.Neutral));
    }
}

using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Leather Armor gear.</summary>
    public static class LeatherArmor
    {
        public static Gear Create() => new Gear(
            "LeatherArmor",
            ItemKind.ArmorMail,
            ItemRarity.Normal,
            "+3 Defense",
            120,
            new StatBlock(0, 0, 0, 0),
            def: 3,
            elementalProps: new ElementalProperties(ElementType.Neutral));
    }
}

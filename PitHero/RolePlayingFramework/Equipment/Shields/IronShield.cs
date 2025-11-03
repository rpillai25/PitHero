using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Iron Shield gear.</summary>
    public static class IronShield
    {
        public static Gear Create() => new Gear(
            "IronShield",
            ItemKind.Shield,
            ItemRarity.Normal,
            "+3 Defense, Water Resistant",
            120,
            new StatBlock(0, 0, 0, 0),
            def: 3,
            elementalProps: new ElementalProperties(
                ElementType.Water,
                new Dictionary<ElementType, float>
                {
                    { ElementType.Water, 0.30f },   // 30% resistance to Water
                    { ElementType.Fire, -0.15f }    // 15% weakness to Fire (opposing element)
                }));
    }
}

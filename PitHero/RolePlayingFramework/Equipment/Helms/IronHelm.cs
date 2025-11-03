using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Iron Helm gear.</summary>
    public static class IronHelm
    {
        public static Gear Create() => new Gear(
            "IronHelm",
            ItemKind.HatHelm,
            ItemRarity.Normal,
            "+3 Defense, Earth Resistant",
            135,
            new StatBlock(0, 0, 0, 0),
            def: 3,
            element: ElementType.Earth,
            elementalProps: new ElementalProperties(
                ElementType.Earth,
                new Dictionary<ElementType, float>
                {
                    { ElementType.Earth, 0.20f },   // 20% resistance to Earth
                    { ElementType.Wind, -0.10f }    // 10% weakness to Wind (opposing element)
                }));
    }
}

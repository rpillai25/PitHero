using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Martial artist focused on endurance and counters.
    /// Role: Balanced fighter, moderate HP, good agility
    /// Target Stats at L99: HP ~315 (Vit 58), MP ~121 (Mag 37), Str 73, Agi 62, Vit 58, Mag 37
    /// </summary>
    public sealed class Monk : BaseJob
    {
        public Monk() : base(
            name: "Monk",
            // BaseBonus provides balanced initial stats
            // Target: Balanced physical stats with good Strength and Agility
            baseBonus: new StatBlock(strength: 9, agility: 7, vitality: 7, magic: 4),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 9 + (0.653 * 98) ≈ 73, Agi: 7 + (0.561 * 98) ≈ 62
            // Vit: 7 + (0.520 * 98) ≈ 58, Mag: 4 + (0.337 * 98) ≈ 37
            growthPerLevel: new StatBlock(strength: 0.653f, agility: 0.561f, vitality: 0.520f, magic: 0.337f),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new CounterPassive());
            list.Add(new DeflectPassive());
            list.Add(new RoundhouseSkill());
            list.Add(new FlamingFistSkill());
        }
    }
}

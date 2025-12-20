using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Stealthy fighter with trap disarm abilities.
    /// Role: High agility, moderate damage, low defense
    /// Target Stats at L99: HP ~240 (Vit 43), MP ~106 (Mag 32), Str 58, Agi 82, Vit 43, Mag 32
    /// </summary>
    public sealed class Thief : BaseJob
    {
        public Thief() : base(
            name: "Thief",
            // BaseBonus emphasizes Agility
            // Target: Very high Agility, moderate Strength, low Vitality and Magic
            baseBonus: new StatBlock(strength: 7, agility: 10, vitality: 5, magic: 4),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 7 + (0.520 * 98) ≈ 58, Agi: 10 + (0.735 * 98) ≈ 82
            // Vit: 5 + (0.388 * 98) ≈ 43, Mag: 4 + (0.286 * 98) ≈ 32
            growthPerLevel: new StatBlock(strength: 0.520f, agility: 0.735f, vitality: 0.388f, magic: 0.286f),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowstepPassive());
            list.Add(new TrapSensePassive());
            list.Add(new SneakAttackSkill());
            list.Add(new VanishSkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Ki Shot (Monk + Bowman) - Martial archer who channels energy.
    /// Role: Ranged physical DPS with Ki-enhanced attacks
    /// Target Stats at L99 (job contribution): HP ~335 (Vit 62), MP ~220 (Mag 70), Str 70, Agi 90, Vit 62, Mag 70
    /// </summary>
    public sealed class KiShot : BaseJob
    {
        public KiShot() : base(
            name: "Ki Shot",
            // BaseBonus: L1 stats emphasizing Agility and Strength
            // Target: 15-25% stronger than parent jobs (Monk Str 73, Bowman Agi 72)
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 2),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.694 * 98) ≈ 70, Agi: 3 + (0.888 * 98) ≈ 90, Vit: 2 + (0.612 * 98) ≈ 62, Mag: 2 + (0.694 * 98) ≈ 70
            growthPerLevel: new StatBlock(strength: 0.694f, agility: 0.888f, vitality: 0.612f, magic: 0.694f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new KiSightPassive());
            list.Add(new ArrowMeditationPassive());
            list.Add(new KiArrowSkill());
            list.Add(new ArrowFlurrySkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Marksman (Bowman + Thief) - Precision ranged DPS with high evasion.
    /// Role: Elite archer with critical hits and stealth
    /// Target Stats at L99 (job contribution): HP ~315 (Vit 58), MP ~205 (Mag 65), Str 68, Agi 88, Vit 58, Mag 65
    /// </summary>
    public sealed class Marksman : BaseJob
    {
        public Marksman() : base(
            name: "Marksman",
            // BaseBonus: L1 stats emphasizing Agility and Strength
            // Target: 15-25% stronger than parent jobs (Bowman Agi 72, Thief Agi 82)
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 1),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.673 * 98) ≈ 68, Agi: 3 + (0.867 * 98) ≈ 88, Vit: 2 + (0.571 * 98) ≈ 58, Mag: 1 + (0.653 * 98) ≈ 65
            growthPerLevel: new StatBlock(strength: 0.673f, agility: 0.867f, vitality: 0.571f, magic: 0.653f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new EagleReflexesPassive());
            list.Add(new SteadyAimPassive());
            list.Add(new PowerVolleySkill());
            list.Add(new ArmorPiercerSkill());
        }
    }
}

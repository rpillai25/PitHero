using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Stalker (Thief + Bowman) - Stealth archer, critical hits and ambush.
    /// Role: Ranged assassin with stealth and precision
    /// Target Stats at L99 (job contribution): HP ~315 (Vit 58), MP ~205 (Mag 65), Str 68, Agi 90, Vit 58, Mag 65
    /// </summary>
    public sealed class Stalker : BaseJob
    {
        public Stalker() : base(
            name: "Stalker",
            // BaseBonus: L1 stats emphasizing Agility
            // Target: 15-25% stronger than parent jobs (Thief Agi 82, Bowman Agi 72)
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 1),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.673 * 98) ≈ 68, Agi: 3 + (0.888 * 98) ≈ 90, Vit: 2 + (0.571 * 98) ≈ 58, Mag: 1 + (0.653 * 98) ≈ 65
            growthPerLevel: new StatBlock(strength: 0.673f, agility: 0.888f, vitality: 0.571f, magic: 0.653f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new HiddenTrackerPassive());
            list.Add(new QuickEscapePassive());
            list.Add(new PoisonArrowSkill());
            list.Add(new SilentVolleySkill());
        }
    }
}

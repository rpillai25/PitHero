using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Divine Fist (Priest + Monk) - Sacred martial artist combining healing and physical prowess.
    /// Role: Holy melee fighter with healing capabilities
    /// Target Stats at L99 (job contribution): HP ~335 (Vit 62), MP ~265 (Mag 85), Str 72, Agi 78, Vit 62, Mag 85
    /// </summary>
    public sealed class DivineFist : BaseJob
    {
        public DivineFist() : base(
            name: "Divine Fist",
            // BaseBonus: L1 stats emphasizing Strength and Magic
            // Target: 15-25% stronger than parent jobs (Monk Str 73, Priest Mag 78)
            baseBonus: new StatBlock(strength: 3, agility: 2, vitality: 2, magic: 3),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 3 + (0.704 * 98) ≈ 72, Agi: 2 + (0.776 * 98) ≈ 78, Vit: 2 + (0.612 * 98) ≈ 62, Mag: 3 + (0.837 * 98) ≈ 85
            growthPerLevel: new StatBlock(strength: 0.704f, agility: 0.776f, vitality: 0.612f, magic: 0.837f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SpiritGuardPassive());
            list.Add(new EnlightenedPassive());
            list.Add(new SacredStrikeSkill());
            list.Add(new AuraShieldSkill());
        }
    }
}

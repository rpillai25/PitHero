using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Shadowmender (Thief + Priest) - Stealth healer and debuffer.
    /// Role: Support with evasion and healing
    /// Target Stats at L99 (job contribution): HP ~300 (Vit 55), MP ~250 (Mag 80), Str 62, Agi 85, Vit 55, Mag 80
    /// </summary>
    public sealed class Shadowmender : BaseJob
    {
        public Shadowmender() : base(
            name: "Shadowmender",
            // BaseBonus: L1 stats emphasizing Agility and Magic
            // Target: 15-25% stronger than parent jobs (Thief Agi 82, Priest Mag 78)
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 2),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.612 * 98) ≈ 62, Agi: 3 + (0.837 * 98) ≈ 85, Vit: 2 + (0.541 * 98) ≈ 55, Mag: 2 + (0.796 * 98) ≈ 80
            growthPerLevel: new StatBlock(strength: 0.612f, agility: 0.837f, vitality: 0.541f, magic: 0.796f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowMendPassive());
            list.Add(new PurgeTrapPassive());
            list.Add(new LifeLeechSkill());
            list.Add(new VeilOfSilenceSkill());
        }
    }
}

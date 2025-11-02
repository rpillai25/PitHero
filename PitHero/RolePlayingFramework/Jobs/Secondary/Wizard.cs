using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Wizard (Mage + Priest) - High-powered spellcaster, excels in magical damage and support.
    /// Role: Ultimate magic user with high MP and spell power
    /// Target Stats at L99 (job contribution): HP ~275 (Vit 50), MP ~304 (Mag 98), Str 55, Agi 75, Vit 50, Mag 98
    /// </summary>
    public sealed class Wizard : BaseJob
    {
        public Wizard() : base(
            name: "Wizard",
            // BaseBonus: L1 stats emphasizing Magic
            // Target: 15-25% stronger than parent jobs (Mage Mag 88, Priest Mag 78)
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 4),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.541 * 98) ≈ 55, Agi: 2 + (0.745 * 98) ≈ 75, Vit: 2 + (0.490 * 98) ≈ 50, Mag: 4 + (0.959 * 98) ≈ 98
            growthPerLevel: new StatBlock(strength: 0.541f, agility: 0.745f, vitality: 0.490f, magic: 0.959f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ManaSpringPassive());
            list.Add(new BlessingPassive());
            list.Add(new MeteorSkill());
            list.Add(new PurifySkill());
        }
    }
}

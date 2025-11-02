using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// War Mage (Knight + Mage) - Armored spellcaster with physical and magical skills.
    /// Role: Balanced warrior-mage with defense and magic
    /// Target Stats at L99 (job contribution): HP ~375 (Vit 70), MP ~250 (Mag 80), Str 60, Agi 50, Vit 70, Mag 80
    /// </summary>
    public sealed class WarMage : BaseJob
    {
        public WarMage() : base(
            name: "War Mage",
            // BaseBonus: L1 stats emphasizing Magic and Vitality
            // Target: 15-25% stronger than parent jobs (Knight Vit 78, Mage Mag 88)
            baseBonus: new StatBlock(strength: 3, agility: 1, vitality: 2, magic: 3),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 3 + (0.582 * 98) ≈ 60, Agi: 1 + (0.500 * 98) ≈ 50, Vit: 2 + (0.694 * 98) ≈ 70, Mag: 3 + (0.786 * 98) ≈ 80
            growthPerLevel: new StatBlock(strength: 0.582f, agility: 0.500f, vitality: 0.694f, magic: 0.786f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new FocusedMindPassive());
            list.Add(new ArcaneDefensePassive());
            list.Add(new SpellbladeSkill());
            list.Add(new BlitzSkill());
        }
    }
}

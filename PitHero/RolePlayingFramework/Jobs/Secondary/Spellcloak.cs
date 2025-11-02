using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Spellcloak (Mage + Thief) - Magical rogue, excels at evasion and magic.
    /// Role: Stealthy spellcaster with evasion
    /// Target Stats at L99 (job contribution): HP ~285 (Vit 52), MP ~274 (Mag 88), Str 62, Agi 85, Vit 52, Mag 88
    /// </summary>
    public sealed class Spellcloak : BaseJob
    {
        public Spellcloak() : base(
            name: "Spellcloak",
            // BaseBonus: L1 stats emphasizing Magic and Agility
            // Target: 15-25% stronger than parent jobs (Mage Mag 88, Thief Agi 82)
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 3),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.612 * 98) ≈ 62, Agi: 2 + (0.847 * 98) ≈ 85, Vit: 2 + (0.510 * 98) ≈ 52, Mag: 3 + (0.867 * 98) ≈ 88
            growthPerLevel: new StatBlock(strength: 0.612f, agility: 0.847f, vitality: 0.510f, magic: 0.867f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new MiragePassive());
            list.Add(new ArcaneStealthPassive());
            list.Add(new ShadowBoltSkill());
            list.Add(new FadeSkill());
        }
    }
}

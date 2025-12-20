using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Glass cannon caster with strong magic and utility.
    /// Role: High magic, high MP, low physical
    /// Target Stats at L99: HP ~190 (Vit 33), MP ~274 (Mag 88), Str 33, Agi 48, Vit 33, Mag 88
    /// </summary>
    public sealed class Mage : BaseJob
    {
        public Mage() : base(
            name: "Mage",
            // BaseBonus emphasizes Magic
            // Target: Very high Magic, low physical stats
            baseBonus: new StatBlock(strength: 4, agility: 6, vitality: 4, magic: 11),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 4 + (0.296 * 98) ≈ 33, Agi: 6 + (0.429 * 98) ≈ 48
            // Vit: 4 + (0.296 * 98) ≈ 33, Mag: 11 + (0.786 * 98) ≈ 88
            growthPerLevel: new StatBlock(strength: 0.296f, agility: 0.429f, vitality: 0.296f, magic: 0.786f),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new HeartOfFirePassive());
            list.Add(new EconomistPassive());
            list.Add(new FireSkill());
            list.Add(new FireStormSkill());
        }
    }
}

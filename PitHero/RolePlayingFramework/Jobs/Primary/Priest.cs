using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Support caster with healing and modest defenses.
    /// Role: Healing/support, high MP, moderate magic
    /// Target Stats at L99: HP ~240 (Vit 43), MP ~244 (Mag 78), Str 38, Agi 53, Vit 43, Mag 78
    /// </summary>
    public sealed class Priest : BaseJob
    {
        public Priest() : base(
            name: JobTextKey.Job_Priest_Name,
            // BaseBonus emphasizes Magic for healing
            // Target: High Magic for MP and healing, moderate defensive stats
            baseBonus: new StatBlock(strength: 5, agility: 6, vitality: 5, magic: 9),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 5 + (0.337 * 98) ≈ 38, Agi: 6 + (0.480 * 98) ≈ 53
            // Vit: 5 + (0.388 * 98) ≈ 43, Mag: 9 + (0.704 * 98) ≈ 78
            growthPerLevel: new StatBlock(strength: 0.337f, agility: 0.480f, vitality: 0.388f, magic: 0.704f),
            tier: JobTier.Primary,
            jobFlag: JobType.Priest,
            description: JobTextKey.Job_Priest_Desc,
            role: JobTextKey.Job_Priest_Role)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new CalmSpiritPassive());
            list.Add(new MenderPassive());
            list.Add(new HealSkill());
            list.Add(new DefenseUpSkill());
        }
    }
}

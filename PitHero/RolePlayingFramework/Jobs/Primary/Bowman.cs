using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Long range specialist with enhanced sight distance.
    /// Role: Ranged attacker, balanced stats
    /// Target Stats at L99: HP ~265 (Vit 48), MP ~121 (Mag 37), Str 62, Agi 72, Vit 48, Mag 37
    /// </summary>
    public sealed class Bowman : BaseJob
    {
        public Bowman() : base(
            name: "Bowman",
            // BaseBonus provides balanced physical stats
            // Target: Good Agility for ranged attacks, balanced other stats
            baseBonus: new StatBlock(strength: 7, agility: 9, vitality: 6, magic: 4),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 7 + (0.561 * 98) ≈ 62, Agi: 9 + (0.643 * 98) ≈ 72
            // Vit: 6 + (0.429 * 98) ≈ 48, Mag: 4 + (0.337 * 98) ≈ 37
            growthPerLevel: new StatBlock(strength: 0.561f, agility: 0.643f, vitality: 0.429f, magic: 0.337f),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new EagleEyePassive());
            list.Add(new QuickdrawPassive());
            list.Add(new PowerShotSkill());
            list.Add(new VolleySkill());
        }
    }
}

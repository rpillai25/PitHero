using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Ki Shot (Monk + Bowman) - Spiritual archer with enhanced perception and multi-hit attacks.</summary>
    public sealed class KiShot : BaseJob
    {
        public KiShot() : base(
            name: "Ki Shot",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new KiSightPassive());
            list.Add(new ArrowMeditationPassive());
            list.Add(new KiArrowSkill());
            list.Add(new ArrowFlurrySkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>Long range specialist with enhanced sight distance.</summary>
    public sealed class Bowman : BaseJob
    {
        public Bowman() : base(
            name: "Bowman",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 1),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 1),
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

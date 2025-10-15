using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Marksman (Knight + Bowman) - Balanced warrior-archer with powerful ranged attacks.</summary>
    public sealed class Marksman : BaseJob
    {
        public Marksman() : base(
            name: "Marksman",
            baseBonus: new StatBlock(strength: 3, agility: 2, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 1, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new EagleReflexesPassive());
            list.Add(new SteadyAimPassive());
            list.Add(new PowerVolleySkill());
            list.Add(new ArmorPiercerSkill());
        }
    }
}

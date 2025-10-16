using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Holy Shadow (Holy Archer + Shadowmender) - Stealthy holy archer with healing.</summary>
    public sealed class HolyShadow : BaseJob
    {
        public HolyShadow() : base(
            name: "Holy Shadow",
            baseBonus: new StatBlock(strength: 1, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DivineVeilPassive());
            list.Add(new LightAndDarkPassive());
            list.Add(new ShadowShotSkill());
            list.Add(new SacredSilenceSkill());
        }
    }
}

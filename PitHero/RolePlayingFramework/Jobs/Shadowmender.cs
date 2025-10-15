using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Shadowmender (Priest + Thief) - Stealthy healer with life drain and trap disarm abilities.</summary>
    public sealed class Shadowmender : BaseJob
    {
        public Shadowmender() : base(
            name: "Shadowmender",
            baseBonus: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowMendPassive());
            list.Add(new PurgeTrapPassive());
            list.Add(new LifeLeechSkill());
            list.Add(new VeilOfSilenceSkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Shinobi Master (Samurai + Ninja) - Elite warrior with speed and stealth mastery.</summary>
    public sealed class ShinobiMaster : BaseJob
    {
        public ShinobiMaster() : base(
            name: "Shinobi Master",
            baseBonus: new StatBlock(strength: 4, agility: 4, vitality: 3, magic: 2),
            growthPerLevel: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowReflexPassive());
            list.Add(new IronDisciplinePassive());
            list.Add(new FlashStrikeSkill());
            list.Add(new MistEscapeSkill());
        }
    }
}

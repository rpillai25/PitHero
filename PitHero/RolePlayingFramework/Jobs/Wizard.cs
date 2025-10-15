using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Wizard (Mage + Priest) - Supreme magic user with enhanced spells and support abilities.</summary>
    public sealed class Wizard : BaseJob
    {
        public Wizard() : base(
            name: "Wizard",
            baseBonus: new StatBlock(strength: 1, agility: 1, vitality: 1, magic: 6),
            growthPerLevel: new StatBlock(strength: 1, agility: 1, vitality: 1, magic: 3))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ManaSpringPassive());
            list.Add(new BlessingPassive());
            list.Add(new MeteorSkill());
            list.Add(new PurifySkill());
        }
    }
}

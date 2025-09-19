using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Durable frontliner specializing in swords and heavy armor.</summary>
    public sealed class Knight : BaseJob
    {
        public Knight() : base(
            name: "Knight",
            baseBonus: new StatBlock(strength: 4, agility: 0, vitality: 3, magic: 0),
            growthPerLevel: new StatBlock(strength: 2, agility: 0, vitality: 2, magic: 0))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new LightArmorPassive());
            list.Add(new HeavyArmorPassive());
            list.Add(new SpinSlashSkill());
            list.Add(new HeavyStrikeSkill());
        }
    }
}

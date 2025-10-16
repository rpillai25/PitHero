using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Silent Hunter (Stalker + Ninja) - Master of stealth, tracking and evasion.</summary>
    public sealed class SilentHunter : BaseJob
    {
        public SilentHunter() : base(
            name: "Silent Hunter",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 2, magic: 1),
            growthPerLevel: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new StealthTrackerPassive());
            list.Add(new EscapeMasterPassive());
            list.Add(new SilentSlashSkill());
            list.Add(new VenomEscapeSkill());
        }
    }
}

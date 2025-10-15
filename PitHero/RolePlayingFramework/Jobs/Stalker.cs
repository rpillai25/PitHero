using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Stalker (Thief + Bowman) - Stealthy archer with trap detection and poison attacks.</summary>
    public sealed class Stalker : BaseJob
    {
        public Stalker() : base(
            name: "Stalker",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 2),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new HiddenTrackerPassive());
            list.Add(new QuickEscapePassive());
            list.Add(new PoisonArrowSkill());
            list.Add(new SilentVolleySkill());
        }
    }
}

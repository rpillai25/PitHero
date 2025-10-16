using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Holy Archer (Priest + Bowman) - Holy ranged specialist with support abilities.</summary>
    public sealed class HolyArcher : BaseJob
    {
        public HolyArcher() : base(
            name: "Holy Archer",
            baseBonus: new StatBlock(strength: 1, agility: 2, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DivineVisionPassive());
            list.Add(new BlessingArrowPassive());
            list.Add(new LightshotSkill());
            list.Add(new SacredVolleySkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Archer (Divine Fist + Holy Archer) - Holy ranged monk with healing arrows.</summary>
    public sealed class DivineArcher : BaseJob
    {
        public DivineArcher() : base(
            name: "Divine Archer",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 5),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SacredSightPassive());
            list.Add(new AuraBlessingPassive());
            list.Add(new LightStrikeSkill());
            list.Add(new HolyVolleySkill());
        }
    }
}

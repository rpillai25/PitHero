using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>Martial artist focused on endurance and counters.</summary>
    public sealed class Monk : BaseJob
    {
        public Monk() : base(
            name: "Monk",
            baseBonus: new StatBlock(strength: 3, agility: 1, vitality: 3, magic: 0),
            growthPerLevel: new StatBlock(strength: 2, agility: 1, vitality: 2, magic: 0),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new CounterPassive());
            list.Add(new DeflectPassive());
            list.Add(new RoundhouseSkill());
            list.Add(new FlamingFistSkill());
        }
    }
}

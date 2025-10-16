using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>Support caster with healing and modest defenses.</summary>
    public sealed class Priest : BaseJob
    {
        public Priest() : base(
            name: "Priest",
            baseBonus: new StatBlock(strength: 0, agility: 0, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0, agility: 1, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new CalmSpiritPassive());
            list.Add(new MenderPassive());
            list.Add(new HealSkill());
            list.Add(new DefenseUpSkill());
        }
    }
}

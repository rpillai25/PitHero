using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Paladin (Knight + Priest) - Durable holy warrior with healing and defensive abilities.</summary>
    public sealed class Paladin : BaseJob
    {
        public Paladin() : base(
            name: "Paladin",
            baseBonus: new StatBlock(strength: 4, agility: 1, vitality: 3, magic: 2),
            growthPerLevel: new StatBlock(strength: 2, agility: 1, vitality: 2, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new KnightsHonorPassive());
            list.Add(new DivineShieldPassive());
            list.Add(new HolyStrikeSkill());
            list.Add(new AuraHealSkill());
        }
    }
}

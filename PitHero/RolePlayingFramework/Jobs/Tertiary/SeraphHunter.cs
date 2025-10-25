using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Seraph Hunter (Holy Archer + Ki Shot) - Divine archer with meditation and ranged holy attacks.</summary>
    public sealed class SeraphHunter : BaseJob
    {
        public SeraphHunter() : base(
            name: "Seraph Hunter",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 2, magic: 2),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DivineArrowPassive());
            list.Add(new SeraphMeditationPassive());
            list.Add(new SacredFlurrySkill());
            list.Add(new LightBarrierSkill());
        }
    }
}

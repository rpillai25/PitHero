using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Seraph Hunter (Holy Archer + Ki Shot) - Divine archer with meditation and ranged holy attacks.
    /// Target Stats at L99: Str 82, Agi 99, Vit 85, Mag 99</summary>
    public sealed class SeraphHunter : BaseJob
    {
        public SeraphHunter() : base(
            name: "Seraph Hunter",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.816f, agility: 0.980f, vitality: 0.837f, magic: 0.980f),
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

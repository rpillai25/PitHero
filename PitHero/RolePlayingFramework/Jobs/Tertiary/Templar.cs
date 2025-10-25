using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Templar (Paladin + War Mage) - Powerful holy warrior combining defense and magic.</summary>
    public sealed class Templar : BaseJob
    {
        public Templar() : base(
            name: "Templar",
            baseBonus: new StatBlock(strength: 5, agility: 2, vitality: 4, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 2),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new BattleMeditationPassive());
            list.Add(new DivineWardPassive());
            list.Add(new SacredBladeSkill());
            list.Add(new JudgementSkill());
        }
    }
}

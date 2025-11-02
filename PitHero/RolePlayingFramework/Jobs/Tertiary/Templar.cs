using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Templar (Paladin + War Mage) - Powerful holy warrior combining defense and magic.
    /// Target Stats at L99: Str 85, Agi 80, Vit 99, Mag 99</summary>
    public sealed class Templar : BaseJob
    {
        public Templar() : base(
            name: "Templar",
            baseBonus: new StatBlock(strength: 5, agility: 2, vitality: 4, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.816f, agility: 0.796f, vitality: 0.969f, magic: 0.980f),
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

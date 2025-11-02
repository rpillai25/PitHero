using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Samurai (Samurai + Divine Fist) - Holy warrior with spiritual discipline.
    /// Target Stats at L99: Str 84, Agi 70, Vit 88, Mag 99</summary>
    public sealed class DivineSamurai : BaseJob
    {
        public DivineSamurai() : base(
            name: "Divine Samurai",
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.816f, agility: 0.694f, vitality: 0.867f, magic: 0.980f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new BushidoSpiritPassive());
            list.Add(new EnlightenedWillPassive());
            list.Add(new SacredSlashSkill());
            list.Add(new DragonAuraSkill());
        }
    }
}

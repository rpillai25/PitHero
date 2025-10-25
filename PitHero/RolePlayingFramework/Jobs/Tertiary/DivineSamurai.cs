using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Samurai (Samurai + Divine Fist) - Holy warrior with spiritual discipline.</summary>
    public sealed class DivineSamurai : BaseJob
    {
        public DivineSamurai() : base(
            name: "Divine Samurai",
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 2),
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

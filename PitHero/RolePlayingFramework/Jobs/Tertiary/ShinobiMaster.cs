using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Shinobi Master (Samurai + Ninja) - Elite warrior with speed and stealth mastery.
    /// Target Stats at L99: Str 88, Agi 99, Vit 88, Mag 80</summary>
    public sealed class ShinobiMaster : BaseJob
    {
        public ShinobiMaster() : base(
            name: "Shinobi Master",
            baseBonus: new StatBlock(strength: 4, agility: 4, vitality: 3, magic: 2),
            growthPerLevel: new StatBlock(strength: 0.857f, agility: 0.969f, vitality: 0.867f, magic: 0.796f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowReflexPassive());
            list.Add(new IronDisciplinePassive());
            list.Add(new FlashStrikeSkill());
            list.Add(new MistEscapeSkill());
        }
    }
}

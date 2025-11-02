using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Holy Shadow (Holy Archer + Shadowmender) - Stealthy holy archer with healing.
    /// Target Stats at L99: Str 72, Agi 99, Vit 78, Mag 99</summary>
    public sealed class HolyShadow : BaseJob
    {
        public HolyShadow() : base(
            name: "Holy Shadow",
            baseBonus: new StatBlock(strength: 1, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 0.724f, agility: 0.980f, vitality: 0.776f, magic: 0.969f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DivineVeilPassive());
            list.Add(new LightAndDarkPassive());
            list.Add(new ShadowShotSkill());
            list.Add(new SacredSilenceSkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Spell Sniper (Wizard + Arcane Archer) - Long-range magical marksman.</summary>
    public sealed class SpellSniper : BaseJob
    {
        public SpellSniper() : base(
            name: "Spell Sniper",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 6),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 3),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ArcaneFocusPassive());
            list.Add(new SpellPrecisionPassive());
            list.Add(new MeteorArrowSkill());
            list.Add(new ElementalStormSkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Cloak (Divine Fist + Spellcloak) - Holy monk with magical stealth.
    /// Target Stats at L99: Str 72, Agi 88, Vit 72, Mag 99</summary>
    public sealed class DivineCloak : BaseJob
    {
        public DivineCloak() : base(
            name: "Divine Cloak",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 0.714f, agility: 0.857f, vitality: 0.714f, magic: 0.969f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SpiritVeilPassive());
            list.Add(new EnlightenedFadePassive());
            list.Add(new SacredBoltSkill());
            list.Add(new AuraCloakSkill());
        }
    }
}

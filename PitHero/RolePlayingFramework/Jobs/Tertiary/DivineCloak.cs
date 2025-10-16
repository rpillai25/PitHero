using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Cloak (Divine Fist + Spellcloak) - Holy monk with magical stealth.</summary>
    public sealed class DivineCloak : BaseJob
    {
        public DivineCloak() : base(
            name: "Divine Cloak",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
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

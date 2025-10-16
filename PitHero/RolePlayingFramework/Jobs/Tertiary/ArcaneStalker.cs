using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Arcane Stalker (Arcane Archer + Stalker) - Magical tracker with elemental attacks.</summary>
    public sealed class ArcaneStalker : BaseJob
    {
        public ArcaneStalker() : base(
            name: "Arcane Stalker",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new TrackersArcanaPassive());
            list.Add(new QuickArcaneEscapePassive());
            list.Add(new PiercingVenomSkill());
            list.Add(new ArcaneVolleySkill());
        }
    }
}

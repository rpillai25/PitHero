using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Arcane Stalker (Arcane Archer + Stalker) - Magical tracker with elemental attacks.
    /// Target Stats at L99: Str 70, Agi 85, Vit 72, Mag 92</summary>
    public sealed class ArcaneStalker : BaseJob
    {
        public ArcaneStalker() : base(
            name: "Arcane Stalker",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 0.694f, agility: 0.837f, vitality: 0.714f, magic: 0.898f),
            tier: JobTier.Tertiary)
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

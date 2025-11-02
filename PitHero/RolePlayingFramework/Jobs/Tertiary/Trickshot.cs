using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Trickshot (Marksman + Stalker) - Expert marksman with tracking and evasion.
    /// Target Stats at L99: Str 80, Agi 92, Vit 75, Mag 80</summary>
    public sealed class Trickshot : BaseJob
    {
        public Trickshot() : base(
            name: "Trickshot",
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 0.786f, agility: 0.908f, vitality: 0.745f, magic: 0.796f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new TrackersIntuitionPassive());
            list.Add(new ChainShotPassive());
            list.Add(new VenomVolleySkill());
            list.Add(new QuietKillSkill());
        }
    }
}

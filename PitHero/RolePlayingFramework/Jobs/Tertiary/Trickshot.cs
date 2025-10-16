using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Trickshot (Marksman + Stalker) - Expert marksman with tracking and evasion.</summary>
    public sealed class Trickshot : BaseJob
    {
        public Trickshot() : base(
            name: "Trickshot",
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 1))
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

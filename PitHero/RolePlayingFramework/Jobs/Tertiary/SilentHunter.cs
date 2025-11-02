using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Silent Hunter (Stalker + Ninja) - Master of stealth, tracking and evasion.
    /// Target Stats at L99: Str 80, Agi 99, Vit 78, Mag 80</summary>
    public sealed class SilentHunter : BaseJob
    {
        public SilentHunter() : base(
            name: "Silent Hunter",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 2, magic: 1),
            growthPerLevel: new StatBlock(strength: 0.786f, agility: 0.969f, vitality: 0.776f, magic: 0.806f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new StealthTrackerPassive());
            list.Add(new EscapeMasterPassive());
            list.Add(new SilentSlashSkill());
            list.Add(new VenomEscapeSkill());
        }
    }
}

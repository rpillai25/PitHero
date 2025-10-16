using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Mystic Stalker (Stalker + Spellcloak) - Magical tracker with arcane stealth.</summary>
    public sealed class MysticStalker : BaseJob
    {
        public MysticStalker() : base(
            name: "Mystic Stalker",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ArcaneTrackerPassive());
            list.Add(new QuickFadePassive());
            list.Add(new PoisonBoltSkill());
            list.Add(new SilentArcanaSkill());
        }
    }
}

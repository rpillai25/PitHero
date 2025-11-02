using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Mystic Stalker (Stalker + Spellcloak) - Magical tracker with arcane stealth.
    /// Target Stats at L99: Str 72, Agi 99, Vit 72, Mag 99</summary>
    public sealed class MysticStalker : BaseJob
    {
        public MysticStalker() : base(
            name: "Mystic Stalker",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.714f, agility: 0.969f, vitality: 0.714f, magic: 0.980f),
            tier: JobTier.Tertiary)
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

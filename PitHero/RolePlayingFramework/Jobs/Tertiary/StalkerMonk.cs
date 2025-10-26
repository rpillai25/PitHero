using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Stalker Monk (Stalker + Monk) - Fast evasive tracker with escape mastery.</summary>
    public sealed class StalkerMonk : BaseJob
    {
        public StalkerMonk() : base(
            name: "Stalker Monk",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 1),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new FastStalkerPassive());
            list.Add(new SwiftEscapePassive());
            list.Add(new PoisonKiSkill());
            list.Add(new SilentFlurrySkill());
        }
    }
}

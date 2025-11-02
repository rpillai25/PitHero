using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Stalker Monk (Stalker + Monk) - Fast evasive tracker with escape mastery.
    /// Target Stats at L99: Str 80, Agi 99, Vit 75, Mag 80</summary>
    public sealed class StalkerMonk : BaseJob
    {
        public StalkerMonk() : base(
            name: "Stalker Monk",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 0.796f, agility: 0.969f, vitality: 0.745f, magic: 0.796f),
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

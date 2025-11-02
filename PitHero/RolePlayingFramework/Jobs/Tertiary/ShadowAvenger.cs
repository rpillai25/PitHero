using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Shadow Avenger (Shadow Fist + Spellcloak) - Stealthy monk with magical counters.
    /// Target Stats at L99: Str 75, Agi 99, Vit 80, Mag 92</summary>
    public sealed class ShadowAvenger : BaseJob
    {
        public ShadowAvenger() : base(
            name: "Shadow Avenger",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.745f, agility: 0.969f, vitality: 0.796f, magic: 0.908f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new StealthCounterPassive());
            list.Add(new ArcaneEvasionPassive());
            list.Add(new SneakBoltSkill());
            list.Add(new KiFadeSkill());
        }
    }
}

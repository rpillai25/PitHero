using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Shadow Avenger (Shadow Fist + Spellcloak) - Stealthy monk with magical counters.</summary>
    public sealed class ShadowAvenger : BaseJob
    {
        public ShadowAvenger() : base(
            name: "Shadow Avenger",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
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

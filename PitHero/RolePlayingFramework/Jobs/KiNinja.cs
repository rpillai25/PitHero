using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Ki Ninja (Ki Shot + Ninja) - Agile archer-ninja with ki-powered attacks.</summary>
    public sealed class KiNinja : BaseJob
    {
        public KiNinja() : base(
            name: "Ki Ninja",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new KiEvasionPassive());
            list.Add(new ArrowDashPassive());
            list.Add(new KiSlashSkill());
            list.Add(new NinjaFlurrySkill());
        }
    }
}

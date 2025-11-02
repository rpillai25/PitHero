using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Ki Ninja (Ki Shot + Ninja) - Agile archer-ninja with ki-powered attacks.
    /// Target Stats at L99: Str 82, Agi 99, Vit 75, Mag 80</summary>
    public sealed class KiNinja : BaseJob
    {
        public KiNinja() : base(
            name: "Ki Ninja",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 2),
            growthPerLevel: new StatBlock(strength: 0.816f, agility: 0.969f, vitality: 0.745f, magic: 0.796f),
            tier: JobTier.Tertiary)
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

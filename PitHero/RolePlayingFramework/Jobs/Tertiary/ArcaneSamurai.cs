using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Arcane Samurai (Samurai + Spellcloak) - Samurai with magical abilities and stealth.
    /// Target Stats at L99: Str 82, Agi 72, Vit 78, Mag 88</summary>
    public sealed class ArcaneSamurai : BaseJob
    {
        public ArcaneSamurai() : base(
            name: "Arcane Samurai",
            baseBonus: new StatBlock(strength: 4, agility: 3, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.796f, agility: 0.704f, vitality: 0.776f, magic: 0.867f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new MagicBladePassive());
            list.Add(new IronMiragePassive());
            list.Add(new IaidoBoltSkill());
            list.Add(new FadeSlashSkill());
        }
    }
}

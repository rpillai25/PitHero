using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Arcane Samurai (Samurai + Spellcloak) - Samurai with magical abilities and stealth.</summary>
    public sealed class ArcaneSamurai : BaseJob
    {
        public ArcaneSamurai() : base(
            name: "Arcane Samurai",
            baseBonus: new StatBlock(strength: 4, agility: 3, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 1, magic: 2))
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

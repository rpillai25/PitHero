using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>War Mage (Knight + Mage) - Armored spellcaster with physical and magical abilities.</summary>
    public sealed class WarMage : BaseJob
    {
        public WarMage() : base(
            name: "War Mage",
            baseBonus: new StatBlock(strength: 3, agility: 1, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 1, vitality: 1, magic: 2),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new FocusedMindPassive());
            list.Add(new ArcaneDefensePassive());
            list.Add(new SpellbladeSkill());
            list.Add(new BlitzSkill());
        }
    }
}

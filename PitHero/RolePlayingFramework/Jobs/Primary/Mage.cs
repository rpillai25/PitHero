using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>Glass cannon caster with strong magic and utility.</summary>
    public sealed class Mage : BaseJob
    {
        public Mage() : base(
            name: "Mage",
            baseBonus: new StatBlock(strength: 0, agility: 0, vitality: 0, magic: 5),
            growthPerLevel: new StatBlock(strength: 0, agility: 1, vitality: 0, magic: 3),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new HeartOfFirePassive());
            list.Add(new EconomistPassive());
            list.Add(new FireSkill());
            list.Add(new FireStormSkill());
        }
    }
}

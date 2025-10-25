using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>Stealthy fighter with trap disarm abilities.</summary>
    public sealed class Thief : BaseJob
    {
        public Thief() : base(
            name: "Thief",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 0),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 0),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowstepPassive());
            list.Add(new TrapSensePassive());
            list.Add(new SneakAttackSkill());
            list.Add(new VanishSkill());
        }
    }
}

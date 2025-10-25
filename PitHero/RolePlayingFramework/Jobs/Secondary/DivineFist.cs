using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Divine Fist (Priest + Monk) - Holy martial artist with healing and defensive abilities.</summary>
    public sealed class DivineFist : BaseJob
    {
        public DivineFist() : base(
            name: "Divine Fist",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SpiritGuardPassive());
            list.Add(new EnlightenedPassive());
            list.Add(new SacredStrikeSkill());
            list.Add(new AuraShieldSkill());
        }
    }
}

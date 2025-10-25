using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Soul Guardian (Divine Fist + Shadowmender) - Stealth healer with spirit powers.</summary>
    public sealed class SoulGuardian : BaseJob
    {
        public SoulGuardian() : base(
            name: "Soul Guardian",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SoulMendPassive());
            list.Add(new BlessingShadowsPassive());
            list.Add(new SpiritLeechSkill());
            list.Add(new GuardianVeilSkill());
        }
    }
}

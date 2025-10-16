using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Shadow Paladin (Paladin + Shadowmender) - Holy warrior with stealth and shadow healing.</summary>
    public sealed class ShadowPaladin : BaseJob
    {
        public ShadowPaladin() : base(
            name: "Shadow Paladin",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 2, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DarkAegisPassive());
            list.Add(new ShadowBlessingPassive());
            list.Add(new SilenceStrikeSkill());
            list.Add(new SoulWardSkill());
        }
    }
}

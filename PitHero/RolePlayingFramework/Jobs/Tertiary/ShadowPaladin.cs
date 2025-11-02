using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Shadow Paladin (Paladin + Shadowmender) - Holy warrior with stealth and shadow healing.
    /// Target Stats at L99: Str 82, Agi 99, Vit 85, Mag 99</summary>
    public sealed class ShadowPaladin : BaseJob
    {
        public ShadowPaladin() : base(
            name: "Shadow Paladin",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 3, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.806f, agility: 0.969f, vitality: 0.837f, magic: 0.980f),
            tier: JobTier.Tertiary)
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

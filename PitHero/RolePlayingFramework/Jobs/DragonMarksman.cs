using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Dragon Marksman (Dragon Fist + Marksman) - Martial artist with enhanced ranged attacks.</summary>
    public sealed class DragonMarksman : BaseJob
    {
        public DragonMarksman() : base(
            name: "Dragon Marksman",
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DragonSightPassive());
            list.Add(new KiVolleyPassive());
            list.Add(new DragonArrowSkill());
            list.Add(new EnergyShotSkill());
        }
    }
}

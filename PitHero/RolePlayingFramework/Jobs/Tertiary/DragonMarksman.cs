using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Dragon Marksman (Dragon Fist + Marksman) - Martial artist with enhanced ranged attacks.
    /// Target Stats at L99: Str 85, Agi 85, Vit 80, Mag 75</summary>
    public sealed class DragonMarksman : BaseJob
    {
        public DragonMarksman() : base(
            name: "Dragon Marksman",
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0.827f, agility: 0.847f, vitality: 0.796f, magic: 0.735f),
            tier: JobTier.Tertiary)
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

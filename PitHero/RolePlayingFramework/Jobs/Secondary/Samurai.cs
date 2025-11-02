using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Samurai (Knight + Monk) - High counterattack, elite physical warrior.</summary>
    public sealed class Samurai : BaseJob
    {
        public Samurai() : base(
            name: "Samurai",
            // BaseBonus: Emphasizing Strength and Vitality
            // Target Stats at L99: Str 80, Agi 58, Vit 75, Mag 38
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 3, magic: 1),
            // GrowthPerLevel: Str: 4 + (0.776 * 98) ≈ 80, Agi: 2 + (0.571 * 98) ≈ 58
            // Vit: 3 + (0.735 * 98) ≈ 75, Mag: 1 + (0.378 * 98) ≈ 38
            growthPerLevel: new StatBlock(strength: 0.776f, agility: 0.571f, vitality: 0.735f, magic: 0.378f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new BushidoPassive());
            list.Add(new IronWillPassive());
            list.Add(new IaidoSlashSkill());
            list.Add(new DragonKickSkill());
        }
    }
}

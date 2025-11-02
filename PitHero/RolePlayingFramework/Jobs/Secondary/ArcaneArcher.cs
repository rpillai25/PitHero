using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Arcane Archer (Mage + Bowman) - Magic-infused archer with elemental attacks.
    /// Role: Ranged magic DPS with enhanced archery and spellcasting
    /// Target Stats at L99 (job contribution): HP ~300 (Vit 55), MP ~286 (Mag 92), Str 65, Agi 85, Vit 55, Mag 92
    /// </summary>
    public sealed class ArcaneArcher : BaseJob
    {
        public ArcaneArcher() : base(
            name: "Arcane Archer",
            // BaseBonus: L1 stats emphasizing Magic and Agility
            // Target: 15-25% stronger than parent jobs (Mage Mag 88, Bowman Agi 72)
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 4),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (1 * 98) ≈ 100, Agi: 2 + (2 * 98) = 198, Vit: 2 + (1 * 98) = 100, Mag: 4 + (2 * 98) = 200
            // Recalculated: Str: 2 + (0.643 * 98) ≈ 65, Agi: 2 + (0.847 * 98) ≈ 85, Vit: 2 + (0.541 * 98) ≈ 55, Mag: 4 + (0.898 * 98) ≈ 92
            growthPerLevel: new StatBlock(strength: 0.643f, agility: 0.847f, vitality: 0.541f, magic: 0.898f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SnipePassive());
            list.Add(new QuickcastPassive());
            list.Add(new PiercingArrowSkill());
            list.Add(new ElementalVolleySkill());
        }
    }
}

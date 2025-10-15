using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Arcane Archer (Mage + Bowman) - Magic-infused archer with enhanced range and elemental attacks.</summary>
    public sealed class ArcaneArcher : BaseJob
    {
        public ArcaneArcher() : base(
            name: "Arcane Archer",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2))
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

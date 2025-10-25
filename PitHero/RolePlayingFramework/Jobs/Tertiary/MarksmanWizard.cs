using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Marksman Wizard (Marksman + Wizard) - Long-range magical marksman.</summary>
    public sealed class MarksmanWizard : BaseJob
    {
        public MarksmanWizard() : base(
            name: "Marksman Wizard",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 5),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 3),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new EagleFocusPassive());
            list.Add(new QuickcastVolleyPassive());
            list.Add(new MeteorShotSkill());
            list.Add(new PurifyingArrowSkill());
        }
    }
}

using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Divine Archer (Divine Fist + Holy Archer) - Holy ranged monk with healing arrows.
    /// Target Stats at L99: Str 75, Agi 82, Vit 85, Mag 99</summary>
    public sealed class DivineArcher : BaseJob
    {
        public DivineArcher() : base(
            name: "Divine Archer",
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 5),
            growthPerLevel: new StatBlock(strength: 0.745f, agility: 0.816f, vitality: 0.847f, magic: 0.959f),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new SacredSightPassive());
            list.Add(new AuraBlessingPassive());
            list.Add(new LightStrikeSkill());
            list.Add(new HolyVolleySkill());
        }
    }
}

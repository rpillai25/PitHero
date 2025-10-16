using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Mystic Marksman (Marksman + Spellcloak) - Magical sharpshooter with stealth.</summary>
    public sealed class MysticMarksman : BaseJob
    {
        public MysticMarksman() : base(
            name: "Mystic Marksman",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 1, agility: 2, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new MysticAimPassive());
            list.Add(new ArcaneReflexPassive());
            list.Add(new SpellShotSkill());
            list.Add(new FadeVolleySkill());
        }
    }
}

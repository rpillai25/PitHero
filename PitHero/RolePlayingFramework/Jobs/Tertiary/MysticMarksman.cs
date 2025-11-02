using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Mystic Marksman (Marksman + Spellcloak) - Magical sharpshooter with stealth.
    /// Target Stats at L99: Str 78, Agi 99, Vit 72, Mag 99</summary>
    public sealed class MysticMarksman : BaseJob
    {
        public MysticMarksman() : base(
            name: "Mystic Marksman",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 0.776f, agility: 0.980f, vitality: 0.714f, magic: 0.969f),
            tier: JobTier.Tertiary)
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

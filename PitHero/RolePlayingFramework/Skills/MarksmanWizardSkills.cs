using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Marksman Wizard (Marksman + Wizard) Skills
    
    // Passives
    public sealed class EagleFocusPassive : BaseSkill
    {
        public EagleFocusPassive() : base("marksmanwizard.eagle_focus", "Eagle Focus", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            // +3 sight, +10% magic damage (placeholders)
        }
    }

    public sealed class QuickcastVolleyPassive : BaseSkill
    {
        public QuickcastVolleyPassive() : base("marksmanwizard.quickcast_volley", "Quickcast Volley", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            // First attack is magic AoE (placeholder)
        }
    }

    // Active Skills
    public sealed class MeteorShotSkill : BaseSkill
    {
        public MeteorShotSkill() : base("marksmanwizard.meteor_shot", "Meteor Shot", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic AoE on primary
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage);
            // Splash to surrounding
            for (int i = 0; i < surrounding.Count && i < 3; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "MeteorShot";
        }
    }

    public sealed class PurifyingArrowSkill : BaseSkill
    {
        public PurifyingArrowSkill() : base("marksmanwizard.purifying_arrow", "Purifying Arrow", SkillKind.Active, SkillTargetType.Self, 6, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Remove all debuffs from self/ally (placeholder - self only)
            return "PurifyingArrow";
        }
    }
}

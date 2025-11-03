using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Samurai (Knight + Monk) Skills
    
    // Passives
    public sealed class BushidoPassive : BaseSkill
    {
        public BushidoPassive() : base("samurai.bushido", "Bushido", SkillKind.Passive, SkillTargetType.Self, 0, 120, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // Counterattack when surrounded (placeholder)
        }
    }

    public sealed class IronWillPassive : BaseSkill
    {
        public IronWillPassive() : base("samurai.iron_will", "Iron Will", SkillKind.Passive, SkillTargetType.Self, 0, 160, ElementType.Neutral) { }
        public override void ApplyPassive(Hero hero)
        {
            // Resist crowd control effects (placeholder)
        }
    }

    // Active Skills
    public sealed class IaidoSlashSkill : BaseSkill
    {
        public IaidoSlashSkill() : base("samurai.iaido_slash", "Iaido Slash", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 200, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            // Crit chance on first strike (placeholder - just bonus damage)
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f));
            return "IaidoSlash";
        }
    }

    public sealed class DragonKickSkill : BaseSkill
    {
        public DragonKickSkill() : base("samurai.dragon_kick", "Dragon Kick", SkillKind.Active, SkillTargetType.SurroundingEnemies, 7, 220, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Strength);
            }
            return "DragonKick";
        }
    }
}

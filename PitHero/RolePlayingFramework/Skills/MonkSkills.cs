using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    public sealed class RoundhouseSkill : BaseSkill
    {
        public RoundhouseSkill() : base("monk.roundhouse", "Roundhouse", SkillKind.Active, SkillTargetType.SurroundingEnemies, 2, 4, 120) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage);
            }
            return "Roundhouse";
        }
    }

    public sealed class FlamingFistSkill : BaseSkill
    {
        public FlamingFistSkill() : base("monk.flaming_fist", "Flaming Fist", SkillKind.Active, SkillTargetType.SingleEnemy, 3, 5, 170) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 2);
            return "FlamingFist";
        }
    }

    public sealed class CounterPassive : BaseSkill
    {
        public CounterPassive() : base("monk.counter", "Counter", SkillKind.Passive, SkillTargetType.Self, 2, 0, 70) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.EnableCounter = true;
        }
    }

    public sealed class DeflectPassive : BaseSkill
    {
        public DeflectPassive() : base("monk.deflect", "Deflect", SkillKind.Passive, SkillTargetType.Self, 3, 0, 90) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance = 0.15f; // 15%
        }
    }
}

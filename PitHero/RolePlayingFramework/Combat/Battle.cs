using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Combat
{
    /// <summary>Convenience helpers to exchange blows between hero and enemy.</summary>
    public static class Battle
    {
        /// <summary>Hero attacks enemy using resolver.</summary>
        public static AttackResult HeroAttack(IAttackResolver resolver, Hero hero, IEnemy enemy, DamageKind kind)
        {
            var a = hero.GetTotalStats();
            var d = enemy.Stats;
            var res = resolver.Resolve(a, d, kind, hero.Level, enemy.Level);
            if (res.Hit) enemy.TakeDamage(res.Damage);
            return res;
        }

        /// <summary>Enemy attacks hero using resolver.</summary>
        public static AttackResult EnemyAttack(IAttackResolver resolver, IEnemy enemy, Hero hero)
        {
            var res = resolver.Resolve(enemy.Stats, hero.GetTotalStats(), enemy.AttackKind, enemy.Level, hero.Level);
            if (res.Hit) hero.TakeDamage(res.Damage);
            return res;
        }
    }
}

using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Combat
{
    /// <summary>Automates turns based on Agility and resolves attacks until one side falls.</summary>
    public sealed class BattleOrchestrator
    {
        private readonly IAttackResolver _resolver;

        public BattleOrchestrator(IAttackResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>Runs a simple auto-battle and returns true if hero wins.</summary>
        public bool Run(Hero hero, IEnemy enemy)
        {
            var heroStats = hero.GetTotalStats();
            var enemyStats = enemy.Stats;
            float heroGauge = 0f, enemyGauge = 0f;
            const float turnThreshold = 100f;

            while (hero.CurrentHP > 0 && enemy.CurrentHP > 0)
            {
                // accumulate turn gauge by agility
                heroGauge += heroStats.Agility;
                enemyGauge += enemyStats.Agility;

                if (heroGauge >= turnThreshold)
                {
                    heroGauge -= turnThreshold;
                    // Hero uses best (only physical) for now; equipment and stats already included in GetTotalStats
                    var res = _resolver.Resolve(hero.GetTotalStats(), enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                    if (res.Hit) enemy.TakeDamage(res.Damage);
                }

                if (enemy.CurrentHP <= 0) break;

                if (enemyGauge >= turnThreshold)
                {
                    enemyGauge -= turnThreshold;
                    var res = _resolver.Resolve(enemy.Stats, hero.GetTotalStats(), enemy.AttackKind, enemy.Level, hero.Level);
                    if (res.Hit)
                    {
                        // Apply defense gear as flat mitigation
                        var final = res.Damage - hero.GetEquipmentDefenseBonus();
                        if (final < 1) final = 1;
                        hero.TakeDamage(final);
                    }
                }
            }

            var heroWon = hero.CurrentHP > 0;
            if (heroWon)
            {
                hero.AddExperience(enemy.ExperienceYield);
            }
            return heroWon;
        }
    }
}

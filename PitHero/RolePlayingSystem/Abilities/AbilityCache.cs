using PitHero.RolePlayingSystem.Abilities.Enemy;
using PitHero.RolePlayingSystem.Abilities.Magic.Black;
using System.Collections.Generic;

namespace PitHero.RolePlayingSystem.Abilities
{
    public static class AbilityCache
    {
        private static Dictionary<EnemyAbilityCatalog, Ability> enemyAbilities;
        private static Dictionary<BlackAbilityCatalog, Ability> blackAbilities;

        static AbilityCache()
        {
            enemyAbilities = new Dictionary<EnemyAbilityCatalog, Ability>();
            FillEnemyAbilities();

            blackAbilities = new Dictionary<BlackAbilityCatalog, Ability>();
        }

        public static Ability EnemyAbility(EnemyAbilityCatalog enemyAbilityCatalog)
        {
            return enemyAbilities[enemyAbilityCatalog];
        }

        public static Ability BlackAbility(BlackAbilityCatalog blackAbilityCatalog)
        {
            return blackAbilities[blackAbilityCatalog];
        }

        private static void FillEnemyAbilities()
        {
            enemyAbilities[EnemyAbilityCatalog.OozeWhip] = new OozeWhip();
        }
    }
}

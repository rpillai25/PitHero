using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.BattleActors.Enemies
{
    public class EnemySpecialAttack
    {
        /// <summary>
        /// Specialty attack name
        /// </summary>
        public string Name;

        /// <summary>
        /// Effects of specialty attack (Flags)
        /// </summary>
        public EnemySpecialtyEffect EnemySpecialtyEffects;


        /// <summary>
        /// Can this be used with control command?
        /// </summary>
        public bool Controllable;

        public EnemySpecialAttack(string name, EnemySpecialtyEffect enemySpecialtyEffects, bool controllable)
        {
            Name = name;
            EnemySpecialtyEffects = enemySpecialtyEffects;
            Controllable = controllable;
        }
    }
}

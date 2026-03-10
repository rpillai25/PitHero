using System.Collections.Generic;
using Nez;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Enemies;

namespace PitHero.Services
{
    /// <summary>Service that stores and manages allied monsters recruited during battle.</summary>
    public class AlliedMonsterManager
    {
        private readonly List<AlliedMonster> _alliedMonsters;

        /// <summary>Read-only list of all recruited allied monsters.</summary>
        public IReadOnlyList<AlliedMonster> AlliedMonsters => _alliedMonsters;

        /// <summary>Number of allied monsters currently recruited.</summary>
        public int Count => _alliedMonsters.Count;

        /// <summary>Creates a new AlliedMonsterManager with an empty roster.</summary>
        public AlliedMonsterManager()
        {
            _alliedMonsters = new List<AlliedMonster>(16);
        }

        /// <summary>
        /// Rolls recruitment chance for a defeated enemy.
        /// Returns the new AlliedMonster if successful, null otherwise.
        /// </summary>
        public AlliedMonster TryRecruit(IEnemy enemy)
        {
            float joinChance = GameConfig.BaseMonsterJoinChance * enemy.JoinPercentageModifier;
            if (joinChance <= 0f) return null;

            float roll = Nez.Random.NextFloat();
            if (roll > joinChance) return null;

            string firstName = Util.NameGenerator.GenerateFirstName();
            int battle  = Nez.Random.Range(1, 10);
            int cooking = Nez.Random.Range(1, 10);
            int farming = Nez.Random.Range(1, 10);

            var allied = new AlliedMonster(firstName, enemy.Name, battle, cooking, farming);
            _alliedMonsters.Add(allied);
            Debug.Log($"[AlliedMonsterManager] {enemy.Name} joined as '{firstName}'! Battle:{battle} Cooking:{cooking} Farming:{farming}");
            return allied;
        }
    }
}

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
        private readonly Queue<string> _pendingNotifications;

        /// <summary>Read-only list of all recruited allied monsters.</summary>
        public IReadOnlyList<AlliedMonster> AlliedMonsters => _alliedMonsters;

        /// <summary>Number of allied monsters currently recruited.</summary>
        public int Count => _alliedMonsters.Count;

        /// <summary>Whether there are recruitment notifications waiting to be displayed.</summary>
        public bool HasPendingNotification => _pendingNotifications.Count > 0;

        /// <summary>Creates a new AlliedMonsterManager with an empty roster.</summary>
        public AlliedMonsterManager()
        {
            _alliedMonsters = new List<AlliedMonster>(16);
            _pendingNotifications = new Queue<string>(8);
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
            int fishing = Nez.Random.Range(1, 10);
            int cooking = Nez.Random.Range(1, 10);
            int farming = Nez.Random.Range(1, 10);

            var allied = new AlliedMonster(firstName, enemy.Name, fishing, cooking, farming);
            _alliedMonsters.Add(allied);
            var textService = Core.Services.GetService<TextService>();
            var enemyDisplayName = textService?.DisplayText(PitHero.TextType.Monster, enemy.Name) ?? enemy.Name;
            _pendingNotifications.Enqueue($"{enemyDisplayName} {firstName} was recruited!");
            Debug.Log($"[AlliedMonsterManager] {enemy.Name} joined as '{firstName}'! Fishing:{fishing} Cooking:{cooking} Farming:{farming}");
            return allied;
        }

        /// <summary>Removes and returns the next pending notification message, or null if none.</summary>
        public string DequeueNotification()
        {
            return _pendingNotifications.Count > 0 ? _pendingNotifications.Dequeue() : null;
        }

        /// <summary>Directly adds a pre-constructed allied monster (used when restoring saved state).</summary>
        public void AddAlliedMonster(AlliedMonster ally)
        {
            _alliedMonsters.Add(ally);
        }
    }
}

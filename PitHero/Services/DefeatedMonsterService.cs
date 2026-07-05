using System.Collections.Generic;
using RolePlayingFramework.Enemies;

namespace PitHero.Services
{
    /// <summary>
    /// Persistent record of which monster types the player has defeated in battle.
    /// Drives the "Add Monsters" flow in a Monster House (issue #283): a monster type can only be
    /// manually added if it has been defeated at least once. Registered as a game-level service so
    /// the record survives hero death without a reload.
    /// </summary>
    public class DefeatedMonsterService
    {
        private readonly HashSet<EnemyId> _defeated = new HashSet<EnemyId>();

        /// <summary>Records that the given monster type has been defeated.</summary>
        public void MarkDefeated(EnemyId enemyId)
        {
            _defeated.Add(enemyId);
        }

        /// <summary>True if the given monster type has been defeated at least once.</summary>
        public bool IsDefeated(EnemyId enemyId) => _defeated.Contains(enemyId);

        /// <summary>All defeated monster types.</summary>
        public IReadOnlyCollection<EnemyId> GetAll() => _defeated;

        /// <summary>Serializes the defeated set to a list of bare enum names (e.g. "Rat") for saving.</summary>
        public List<string> ToNames()
        {
            var names = new List<string>(_defeated.Count);
            var e = _defeated.GetEnumerator();
            while (e.MoveNext())
                names.Add(e.Current.ToString());
            e.Dispose();
            return names;
        }

        /// <summary>
        /// Replaces the defeated set from a list of saved enum names. Names that no longer map to a
        /// valid <see cref="EnemyId"/> are ignored, so old saves stay loadable if the enum changes.
        /// </summary>
        public void LoadFrom(IEnumerable<string> names)
        {
            _defeated.Clear();
            if (names == null) return;
            var e = names.GetEnumerator();
            while (e.MoveNext())
            {
                if (System.Enum.TryParse(e.Current, out EnemyId id))
                    _defeated.Add(id);
            }
            e.Dispose();
        }
    }
}

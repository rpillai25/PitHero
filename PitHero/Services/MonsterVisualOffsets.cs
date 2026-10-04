using System.Collections.Generic;
using RolePlayingFramework.Enemies;

namespace PitHero.Services
{
    /// <summary>
    /// Per-monster-type visual nudges (worn hat, carried crops/plates), read from the monster's own
    /// definition (<see cref="IEnemy.HatYOffset"/>, <see cref="IEnemy.CarryYOffset"/>) and cached per
    /// type so the definition isn't re-created on every lookup. Purely presentational.
    /// </summary>
    public static class MonsterVisualOffsets
    {
        private struct Offsets
        {
            public float HatY;
            public float CarryY;
        }

        private static readonly Dictionary<EnemyId, Offsets> _cache = new Dictionary<EnemyId, Offsets>();

        /// <summary>Hat nudge (px, negative = raise) for a monster type name (bare "Slime" or "Monster_Slime").</summary>
        public static float HatYOffsetFor(string monsterTypeName) => Lookup(monsterTypeName).HatY;

        /// <summary>Carried-item nudge (px, negative = raise) for a monster type name (bare "Slime" or "Monster_Slime").</summary>
        public static float CarryYOffsetFor(string monsterTypeName) => Lookup(monsterTypeName).CarryY;

        private static Offsets Lookup(string monsterTypeName)
        {
            if (string.IsNullOrEmpty(monsterTypeName))
                return default;
            var bare = monsterTypeName.StartsWith("Monster_")
                ? monsterTypeName.Substring("Monster_".Length)
                : monsterTypeName;
            if (!System.Enum.TryParse(bare, out EnemyId id))
                return default;
            if (!_cache.TryGetValue(id, out var offsets))
            {
                var enemy = EnemyFactory.Create(id);
                offsets.HatY = enemy.HatYOffset;
                offsets.CarryY = enemy.CarryYOffset;
                _cache[id] = offsets;
            }
            return offsets;
        }
    }
}

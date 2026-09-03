using Nez;
using RolePlayingFramework.Equipment;

namespace PitHero.Services
{
    /// <summary>
    /// Session-scoped owner of the treasure loot shuffle bags (issue #382). Registered per
    /// MainGameScene load; transient by design — bags refill fresh each session and are
    /// never saved (chests themselves are not persisted; the pit regenerates on load).
    /// Also owns the epic-chest draw for biome main-boss kills. That draw runs mid-battle
    /// (inside the BattleEngine coroutine via LiveBattleAdapter.OnEnemyDefeated), so it
    /// uses a private System.Random and must NEVER touch Nez.Random — the battle stream's
    /// call sequence is a determinism contract.
    /// </summary>
    public sealed class LootShuffleService
    {
        private System.Random _epicRng = new System.Random();

        /// <summary>
        /// Points the epic draw at a seeded stream (GameRandom.Loot) so replays reproduce boss loot.
        /// Called from MainGameScene.Begin right after the session seed is applied.
        /// </summary>
        public void SetEpicRng(System.Random rng)
        {
            if (rng != null)
                _epicRng = rng;
        }

        /// <summary>The session's loot bags, shared by every chest roll.</summary>
        public LootBagSet Bags { get; } = new LootBagSet();

        /// <summary>
        /// The live layer's bags, or null when no scene/service exists (headless tests,
        /// virtual layer). Callers fall back to the legacy pure-random path on null.
        /// </summary>
        public static LootBagSet LiveBags
        {
            get
            {
                if (Core.Instance == null)
                    return null;
                var service = Core.Services.GetService<LootShuffleService>();
                return service?.Bags;
            }
        }

        /// <summary>
        /// Draws the next epic item for a biome main-boss chest. Cycles all four PitLord
        /// items before any repeat. Consumes zero Nez.Random (safe mid-battle).
        /// </summary>
        public IItem DrawEpicItem()
        {
            return Bags.DrawEpicItem((float)_epicRng.NextDouble());
        }
    }
}

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
        private readonly System.Random _epicRng = new System.Random();

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
            var index = Bags.DrawEpicIndex((float)_epicRng.NextDouble());
            switch (index)
            {
                case 0: return GearItems.PitLordsSword();
                case 1: return GearItems.PitLordsArmor();
                case 2: return GearItems.PitLordsAegis();
                default: return GearItems.PitLordsCrown();
            }
        }
    }
}

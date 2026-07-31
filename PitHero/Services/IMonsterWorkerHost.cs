using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// A coordinator that owns live worker entities for allied monsters (farm, kitchen, future
    /// jobs). Coordinators register each other as peers so a worker is never spawned for a
    /// monster while its previous job's entity still exists in the world — the old worker must
    /// finish walking home and despawn first. Without this gate, a mid-day job change shows two
    /// entities for the same monster at once.
    /// </summary>
    public interface IMonsterWorkerHost
    {
        /// <summary>True while this coordinator has a non-destroyed worker entity for the monster.</summary>
        bool HasLiveWorkerFor(AlliedMonster monster);
    }
}

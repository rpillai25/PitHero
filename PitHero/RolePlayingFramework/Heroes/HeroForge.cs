using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>In-memory implementation of the hero forge.</summary>
    public sealed class HeroForge : IHeroForge
    {
        private HeroCrystal? _queued;

        /// <summary>Queues a crystal to be used when a new hero is created.</summary>
        public void QueueNext(HeroCrystal crystal)
        {
            _queued = crystal;
        }

        /// <summary>Creates a hero from the queued crystal and clears the queue.</summary>
        public Hero InfuseNext(string runtimeName)
        {
            if (_queued == null)
            {
                // default fallback hero if nothing queued
                var baseStats = new StatBlock(2, 2, 2, 2);
                var job = new Jobs.Knight();
                return new Hero(runtimeName, job, 1, baseStats);
            }

            var hero = new Hero(runtimeName, _queued.Job, _queued.Level, _queued.BaseStats);
            _queued = null;
            return hero;
        }
    }
}

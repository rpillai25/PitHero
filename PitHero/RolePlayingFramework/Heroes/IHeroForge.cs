using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Queues and infuses the next hero with a crystal.</summary>
    public interface IHeroForge
    {
        /// <summary>Queues a crystal to be used when a new hero is created.</summary>
        void QueueNext(HeroCrystal crystal);

        /// <summary>Creates a hero from the queued crystal and clears the queue.</summary>
        Hero InfuseNext(string runtimeName);
    }
}

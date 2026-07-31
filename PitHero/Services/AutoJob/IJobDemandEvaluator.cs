using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>Per-job demand provider for the auto job assignment system. Add one per automatable job.</summary>
    public interface IJobDemandEvaluator
    {
        /// <summary>The job this evaluator staffs.</summary>
        MonsterJob Job { get; }

        /// <summary>
        /// Computes how many workers this job wants right now. rosterSize is the full shift roster;
        /// availableWorkers is the roster minus the MinWorkers already reserved by higher-priority
        /// evaluators (registration order = priority), so lower-priority jobs only claim what's left.
        /// </summary>
        JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers);
    }
}

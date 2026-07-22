using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>Per-job demand provider for the auto job assignment system. Add one per automatable job.</summary>
    public interface IJobDemandEvaluator
    {
        /// <summary>The job this evaluator staffs.</summary>
        MonsterJob Job { get; }

        /// <summary>Computes how many workers this job wants right now, given the roster size.</summary>
        JobDemandEntry EvaluateDemand(int rosterSize);
    }
}

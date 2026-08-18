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
        /// nocturnal is true when this call evaluates the nocturnal shift (10 PM–6 AM); evaluators
        /// that have no work on the night shift (e.g. kitchen) should return zero demand.
        /// </summary>
        JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers, bool nocturnal);

        /// <summary>
        /// Samples the live workload into the evaluator's backpressure tracker (issue #375).
        /// Called once per sampling interval (scaled seconds) — never from EvaluateDemand, which
        /// runs twice per reassessment (once per shift) and would double-sample.
        /// </summary>
        void SamplePressure(float nowSeconds);

        /// <summary>Clears smoothing/drain state. Called when the clock rewinds (save load).</summary>
        void ResetPressure(float nowSeconds);
    }
}

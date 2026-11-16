using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Fishing demand: with a Fishing Pond placed, staff every reachable spot, up to
    /// <see cref="GameConfig.FishingMaxWorkersPerShift"/> per shift (fishing runs around the clock, so
    /// both shifts ask for the same crew); with no pond, no one. The demand is constant (the pond never
    /// runs out of fish), so there is no backpressure. Registered last: fishing is the lowest-priority
    /// job, it soaks up monsters farming and the kitchen don't need, and it has no minimum, so their
    /// starvation release can always pull fishers back. Sticky: a fisher is not reshuffled mid-cast
    /// between solves for no reason.
    /// </summary>
    public sealed class FishingJobDemandEvaluator : IJobDemandEvaluator
    {
        private readonly FishingTaskCoordinator _coordinator;

        /// <summary>The coordinator is optional so the evaluator can run headless in tests (no pond, no demand).</summary>
        public FishingJobDemandEvaluator(FishingTaskCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        /// <inheritdoc/>
        public MonsterJob Job => MonsterJob.Fishing;

        /// <inheritdoc/>
        public void SamplePressure(float nowSeconds) { }

        /// <inheritdoc/>
        public void ResetPressure(float nowSeconds) { }

        /// <inheritdoc/>
        public JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers, bool nocturnal)
        {
            bool pondPlaced = _coordinator != null && _coordinator.GetPond() != null;
            int spots = pondPlaced ? _coordinator.UsableSpotCount : 0;
            return ComputeDemand(pondPlaced, spots, availableWorkers);
        }

        /// <summary>
        /// Pure demand math: one fisher per reachable spot, capped per shift and by the workers left
        /// after the higher-priority jobs; nothing without a pond.
        /// </summary>
        public static JobDemandEntry ComputeDemand(bool pondPlaced, int usableSpots, int availableWorkers)
        {
            int desired = 0;
            if (pondPlaced)
            {
                desired = usableSpots < GameConfig.FishingMaxWorkersPerShift ? usableSpots : GameConfig.FishingMaxWorkersPerShift;
                if (desired > availableWorkers)
                    desired = availableWorkers;
                if (desired < 0)
                    desired = 0;
            }
            return new JobDemandEntry
            {
                Job = MonsterJob.Fishing,
                MinWorkers = 0,
                DesiredWorkers = desired,
                Sticky = true,
            };
        }
    }
}

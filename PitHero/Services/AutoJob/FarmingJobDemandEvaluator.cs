using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Farming demand, driven by real outstanding work (issue #375): the burst signal (outstanding
    /// till/plant/water/harvest tasks) staffs up instantly during watering and harvest waves, and
    /// the backpressure tracker holds that headcount briefly so a momentary dip between waves
    /// doesn't send everyone home — then drains it one worker at a time. While crops or plans
    /// exist at all, one caretaker stays on the field for the next wave; idle surplus farmers are
    /// released instead of wandering (the pre-#375 baseline term kept them staffed with nothing
    /// to do).
    /// </summary>
    public sealed class FarmingJobDemandEvaluator : IJobDemandEvaluator
    {
        private readonly FarmTaskCoordinator _coordinator;
        private readonly CropGrowthService _cropGrowth;
        private readonly CropPlantingService _cropPlanting;
        private readonly BackpressureTracker _tracker = new BackpressureTracker();

        /// <summary>All dependencies are optional so the evaluator can run headless in tests.</summary>
        public FarmingJobDemandEvaluator(FarmTaskCoordinator coordinator,
            CropGrowthService cropGrowth, CropPlantingService cropPlanting)
        {
            _coordinator = coordinator;
            _cropGrowth = cropGrowth;
            _cropPlanting = cropPlanting;
        }

        /// <inheritdoc/>
        public MonsterJob Job => MonsterJob.Farming;

        /// <inheritdoc/>
        public void SamplePressure(float nowSeconds)
        {
            int outstanding = _coordinator != null ? _coordinator.OutstandingTaskCount : 0;
            _tracker.Sample(outstanding / (float)GameConfig.AutoJobFarmTasksPerWorker, nowSeconds);
        }

        /// <inheritdoc/>
        public void ResetPressure(float nowSeconds) => _tracker.Reset(nowSeconds);

        /// <inheritdoc/>
        public JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers)
        {
            int outstanding = _coordinator != null ? _coordinator.OutstandingTaskCount : 0;
            int careLoad = (_cropGrowth != null ? _cropGrowth.CropCount : 0)
                + (_cropPlanting != null ? _cropPlanting.PlanCount : 0);
            return ComputeDemand(outstanding, careLoad, _tracker.GrantedWorkers, availableWorkers);
        }

        /// <summary>
        /// Pure demand math. The live burst gives instant scale-up even between samples; the
        /// tracker's granted count (which only falls one worker per drain interval) gives the
        /// slow scale-down. One caretaker minimum while anything is planted or planned.
        /// </summary>
        public static JobDemandEntry ComputeDemand(int outstandingTasks, int careLoad,
            int grantedWorkers, int availableWorkers)
        {
            int burst = CeilDiv(outstandingTasks, GameConfig.AutoJobFarmTasksPerWorker);
            int min = (outstandingTasks > 0 || careLoad > 0) ? 1 : 0;

            int desired = burst > grantedWorkers ? burst : grantedWorkers;
            if (desired < min)
                desired = min;
            if (desired > availableWorkers)
                desired = availableWorkers;

            return new JobDemandEntry
            {
                Job = MonsterJob.Farming,
                MinWorkers = min < desired ? min : desired,
                DesiredWorkers = desired,
                Sticky = false,
            };
        }

        private static int CeilDiv(int value, int per) => (value + per - 1) / per;
    }
}

using PitHero.Config;
using PitHero.Services.Analytics;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Utils;

namespace PitHero.Services
{
    /// <summary>
    /// Fishing catch resolution: one 200-marble bag (<see cref="FishingConfig"/>) shared by every fisher,
    /// drawn without replacement, so every 200 catches hold exactly the configured composition (two
    /// Alligator Gars, 39 pieces of junk...). A fish sells on the spot; junk is worth nothing.
    ///
    /// Simulation state: drawn only in the fixed step, with a <c>Nez.Random</c> roll, only when a
    /// catch happens. Scene-scoped and never saved, like every shuffle bag (docs/ShuffleBagSystem.md):
    /// a load, a new game or a replay seek starts a fresh bag, exactly as the recording's start did.
    /// </summary>
    public class FishingService
    {
        private readonly ShuffleBag<FishCatchType> _bag = BuildBag();

        /// <summary>Builds the 200-marble catch bag in enum order (the order is part of the draw contract).</summary>
        public static ShuffleBag<FishCatchType> BuildBag()
        {
            var bag = new ShuffleBag<FishCatchType>(FishingConfig.BagSize);
            for (int i = 0; i < FishingConfig.CatchCount; i++)
            {
                var type = (FishCatchType)i;
                bag.Add(type, FishingConfig.GetMarbles(type));
            }
            return bag;
        }

        /// <summary>Marbles left in the current cycle (tests and diagnostics).</summary>
        public int MarblesRemaining => _bag.Remaining;

        /// <summary>Draws the next catch from a [0,1) roll without paying for it.</summary>
        public FishCatchType Draw(float roll01) => _bag.NextFromRoll(roll01);

        /// <summary>
        /// Lands one catch for <paramref name="monster"/> at pond spot <paramref name="spot"/>: draws the
        /// marble, sells a fish on the spot (gold source "fish_catch"), counts one Fishing task toward
        /// the monster's job level, and logs the catch. Returns what was caught.
        /// </summary>
        public FishCatchType ResolveCatch(GameStateService gameState, AlliedMonster monster, int spot, float roll01)
        {
            var type = Draw(roll01);
            int gold = FishingConfig.GetGold(type);
            if (gold > 0)
                gameState?.AddFunds(gold, "fish_catch");
            gameState?.Stats.RecordFishCatch(type, gold);
            MonsterJobTaskRecorder.Record(monster, MonsterJob.Fishing);
            AnalyticsService.LogFishCaught(type.ToString(), gold, spot,
                monster?.Name, monster?.MonsterTypeName);
            return type;
        }

        /// <summary>Refills the bag to its starting arrangement.</summary>
        public void Reset() => _bag.Reset();
    }
}

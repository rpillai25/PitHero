using System.Collections.Generic;
using PitHero.Dining;
using PitHero.Farming;

namespace PitHero.VirtualGame.Economy
{
    /// <summary>
    /// Inputs for one <see cref="VirtualEconomySimulation"/> run (issue #417): the farm layout, the
    /// workers, the kitchen crew, the automation settings and the progression state the run starts
    /// from. The factories below are the scenarios the balance report and the EconomySim tests use.
    /// </summary>
    public class EconomyScenario
    {
        /// <summary>Report label.</summary>
        public string Name = "scenario";

        /// <summary>Real hours to simulate (1 real second = 1 in-game minute).</summary>
        public float RealHours = 10f;

        /// <summary>RNG seed for arrivals, tips and dish picks.</summary>
        public int Seed = 417;

        /// <summary>Wallet at the start.</summary>
        public int StartingGold = GameConfig.NewGameStartingGold;

        /// <summary>One entry per farm plot: the crop planned there (plans are permanent, issue #305).</summary>
        public List<CropType> PlotPlans = new List<CropType>(128);

        /// <summary>Seeds in inventory at the start, indexed by (int)CropType.</summary>
        public int[] StartingSeeds = new int[CropTypeInfo.Count];

        /// <summary>Crop Storage buildings (32 slots each).</summary>
        public int StorageBuildings = 1;

        /// <summary>Farm workers (allied monsters on the Farming job).</summary>
        public int FarmWorkers = 1;

        /// <summary>Farming job level 1-9 (speed = 1 - 0.06 x (level - 1)).</summary>
        public int FarmWorkerLevel = 1;

        /// <summary>Crop growth multiplier (1 = none, 2 = Fast Grow Fertilizer, 3 = Lightning Grow Fertilizer; CropGrowthService.GrowthSpeedMultiplier).</summary>
        public float GrowthSpeedMultiplier = 1f;

        /// <summary>Seconds of walking folded into every farm task (no pathing in the sim).</summary>
        public float TravelOverheadSeconds = 4f;

        /// <summary>Kitchen cooks on shift (0 = kitchen closed).</summary>
        public int Cooks = 0;

        /// <summary>Kitchen servers on shift (0 = kitchen closed).</summary>
        public int Servers = 0;

        /// <summary>Cooking proficiency of the crew (cook time and deluxe odds).</summary>
        public int CookProficiency = 1;

        /// <summary>Seconds of walking folded into every kitchen leg (order, plate, deliver).</summary>
        public float KitchenOverheadSeconds = 8f;

        /// <summary>Auto-sell full crop stacks (Settings > Automation).</summary>
        public bool AutoSellEnabled = true;

        /// <summary>Full stacks of each crop kept back from auto-sell.</summary>
        public int KeepStacks = 0;

        /// <summary>Auto-buy seeds for unplanted plans (Settings > Automation).</summary>
        public bool AutoSeedEnabled = true;

        /// <summary>Gold floor auto-purchases never dip below.</summary>
        public int GoldBuffer = 200;

        /// <summary>Whether the hero's party eats three meals a day at the tavern (hero pays).</summary>
        public bool EatAtTavern = false;

        /// <summary>The hero's Food-tab favorite.</summary>
        public DishType FavoriteDish = DishType.ButteredBread;

        /// <summary>Hero job name for the dish fallback ladder.</summary>
        public string HeroJobName = "Knight";

        /// <summary>Hired mercenaries eating free meals (ingredients only).</summary>
        public int Mercenaries = 0;

        /// <summary>Lifetime crop harvest totals at the start (crop unlocks), indexed by (int)CropType.</summary>
        public int[] InitialCropTotals = new int[CropTypeInfo.Count];

        /// <summary>Lifetime dishes served at the start (dish unlocks), indexed by (int)DishType.</summary>
        public int[] InitialDishTotals = new int[DishTypeInfo.Count];

        /// <summary>Adds <paramref name="count"/> plots of a crop.</summary>
        public EconomyScenario AddPlots(CropType crop, int count)
        {
            for (int i = 0; i < count; i++)
                PlotPlans.Add(crop);
            return this;
        }

        /// <summary>Gives the starting seed inventory enough seeds to plant every plot once.</summary>
        public EconomyScenario SeedEveryPlot()
        {
            for (int i = 0; i < PlotPlans.Count; i++)
                StartingSeeds[(int)PlotPlans[i]]++;
            return this;
        }

        /// <summary>Harvest totals that satisfy every crop requirement up to and including <paramref name="tier"/>.</summary>
        public static int[] CropTotalsUnlockingTier(int tier)
        {
            var totals = new int[CropTypeInfo.Count];
            for (int c = 0; c < CropTypeInfo.Count; c++)
            {
                if (CropUnlockConfig.GetTier((CropType)c) > tier)
                    continue;
                var reqs = CropUnlockConfig.GetRequirements((CropType)c);
                for (int r = 0; r < reqs.Length; r++)
                    if (totals[(int)reqs[r].Crop] < reqs[r].Required)
                        totals[(int)reqs[r].Crop] = reqs[r].Required;
            }
            return totals;
        }

        /// <summary>Served totals that satisfy every dish requirement up to and including <paramref name="tier"/>.</summary>
        public static int[] DishTotalsUnlockingTier(int tier)
        {
            var totals = new int[DishTypeInfo.Count];
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                if (DishUnlockConfig.GetTier((DishType)d) > tier)
                    continue;
                var reqs = DishUnlockConfig.GetRequirements((DishType)d);
                for (int r = 0; r < reqs.Length; r++)
                    if (totals[(int)reqs[r].Dish] < reqs[r].Required)
                        totals[(int)reqs[r].Dish] = reqs[r].Required;
            }
            return totals;
        }

        /// <summary>A brand-new game: the scripted starter farm, one level-1 worker, no kitchen, automation on.</summary>
        public static EconomyScenario Starter(float realHours = 4f, int seed = 417)
        {
            var s = new EconomyScenario
            {
                Name = "starter",
                RealHours = realHours,
                Seed = seed,
                StartingGold = GameConfig.NewGameStartingGold,
                FarmWorkers = 1,
                FarmWorkerLevel = 1,
                StorageBuildings = 1,
                GoldBuffer = 200,
            };
            s.AddPlots(CropType.Wheat, GameConfig.NewGameStartingWheatSeeds)
             .AddPlots(CropType.Corn, GameConfig.NewGameStartingCornSeeds);
            s.StartingSeeds[(int)CropType.Wheat] = GameConfig.NewGameStartingWheatSeeds;
            s.StartingSeeds[(int)CropType.Corn] = GameConfig.NewGameStartingCornSeeds;
            return s;
        }

        /// <summary>A mid-game farm: 30 mixed plots through tier 3, two workers, a small kitchen, party eating out.</summary>
        public static EconomyScenario MidGame(float realHours = 6f, int seed = 417)
        {
            var s = new EconomyScenario
            {
                Name = "mid-game",
                RealHours = realHours,
                Seed = seed,
                StartingGold = 2000,
                FarmWorkers = 2,
                FarmWorkerLevel = 4,
                StorageBuildings = 2,
                Cooks = 2,
                Servers = 1,
                CookProficiency = 4,
                EatAtTavern = true,
                FavoriteDish = DishType.TurnipOnionStew,
                Mercenaries = 1,
                InitialCropTotals = CropTotalsUnlockingTier(3),
                InitialDishTotals = DishTotalsUnlockingTier(3),
            };
            s.AddPlots(CropType.Tomato, 8).AddPlots(CropType.Eggplant, 6).AddPlots(CropType.Lettuce, 6)
             .AddPlots(CropType.Turnip, 6).AddPlots(CropType.Onion, 4);
            s.SeedEveryPlot();
            return s;
        }

        /// <summary>The pacing anchor: 100 plots spread across the unlocked crops, weighted to the top tiers, full crew.</summary>
        public static EconomyScenario LateGameDiverse(float realHours = 12f, int seed = 417)
        {
            var s = LateGameBase("late-game diverse", realHours, seed);
            s.AddPlots(CropType.AppleTree, 20).AddPlots(CropType.Pumpkin, 15).AddPlots(CropType.Watermelon, 15)
             .AddPlots(CropType.Potato, 12).AddPlots(CropType.Grapes, 12)
             .AddPlots(CropType.Turnip, 8).AddPlots(CropType.Onion, 8)
             .AddPlots(CropType.Sugarcane, 5).AddPlots(CropType.Lettuce, 5);
            s.SeedEveryPlot();
            return s;
        }

        /// <summary>The same farm as a wall of apple trees — what market saturation is there to discourage.</summary>
        public static EconomyScenario LateGameMonoculture(float realHours = 12f, int seed = 417)
        {
            var s = LateGameBase("late-game monoculture", realHours, seed);
            s.AddPlots(CropType.AppleTree, 100);
            s.SeedEveryPlot();
            return s;
        }

        private static EconomyScenario LateGameBase(string name, float realHours, int seed)
        {
            return new EconomyScenario
            {
                Name = name,
                RealHours = realHours,
                Seed = seed,
                StartingGold = 30000,
                FarmWorkers = 6,
                FarmWorkerLevel = 9,
                StorageBuildings = 4,
                Cooks = 3,
                Servers = 2,
                CookProficiency = 9,
                EatAtTavern = true,
                FavoriteDish = DishType.HarvestFeastPlatter,
                Mercenaries = 2,
                GoldBuffer = 1000,
                InitialCropTotals = CropTotalsUnlockingTier(6),
                InitialDishTotals = DishTotalsUnlockingTier(6),
            };
        }
    }
}

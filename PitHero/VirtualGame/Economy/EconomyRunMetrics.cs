using System.Collections.Generic;
using System.IO;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.VirtualGame.Economy
{
    /// <summary>
    /// Everything one <see cref="VirtualEconomySimulation"/> run measured: the gold curve, income
    /// and spend by source, milestone times, unlock timelines, final market demand and farm
    /// throughput. <see cref="WriteSummary"/> renders the balance-report tables.
    /// </summary>
    public class EconomyRunMetrics
    {
        /// <summary>Milestones reported, in gold.</summary>
        public static readonly int[] Milestones = { 1_000, 10_000, 100_000, 1_000_000 };

        public string ScenarioName;
        public int Seed;
        public float RealHours;
        public int StartingGold;
        public int FinalGold;

        /// <summary>Wallet at the end of every real hour (index 0 = after hour 1).</summary>
        public List<int> GoldByHour = new List<int>(64);

        public long IncomeCrops;
        public long IncomeDishes;
        public long IncomeTips;
        public long SpendSeeds;
        public long SpendMeals;

        /// <summary>Real minute at which each <see cref="Milestones"/> entry was first reached, or -1.</summary>
        public int[] MilestoneMinutes = { -1, -1, -1, -1 };

        /// <summary>Real minute each crop unlocked during the run (-1 = never, 0 = already unlocked at start).</summary>
        public int[] CropUnlockMinutes = new int[CropTypeInfo.Count];

        /// <summary>Real minute each dish fully unlocked during the run (-1 = never, 0 = already unlocked at start).</summary>
        public int[] DishUnlockMinutes = new int[DishTypeInfo.Count];

        public int[] HarvestsByCrop = new int[CropTypeInfo.Count];
        public int[] UnitsSoldByCrop = new int[CropTypeInfo.Count];
        public int[] DishesServedByDish = new int[DishTypeInfo.Count];
        public float[] FinalDemand = new float[CropTypeInfo.Count];

        /// <summary>Base (full-demand) gold value of every unit sold, per crop.</summary>
        public long[] BaseGoldSoldByCrop = new long[CropTypeInfo.Count];

        /// <summary>Gold actually realized from sales, per crop.</summary>
        public long[] GoldRealizedByCrop = new long[CropTypeInfo.Count];

        /// <summary>Realized gold / base gold per crop — the demand the farm actually sold at (1 when nothing sold).</summary>
        public float AverageDemand(CropType crop)
        {
            int c = (int)crop;
            return BaseGoldSoldByCrop[c] > 0 ? (float)GoldRealizedByCrop[c] / BaseGoldSoldByCrop[c] : 1f;
        }

        /// <summary>Net gold gained during real hours [from, to) (1-based, inclusive of both ends).</summary>
        public int GoldGainedBetweenHours(int fromHour, int toHour)
        {
            int start = fromHour <= 1 ? StartingGold : GoldByHour[fromHour - 2];
            return GoldByHour[toHour - 1] - start;
        }

        public int PatronsArrived;
        public int PatronsServed;
        public int PatronsLeftHungry;
        public int HeroMeals;
        public int HeroMealsSkipped;

        /// <summary>Fraction of awake worker-minutes spent on a task.</summary>
        public float WorkerUtilization;

        /// <summary>Fraction of plot-minutes (crops growing) that were wet.</summary>
        public float WetFraction;

        /// <summary>Net gold gained over the run.</summary>
        public int NetGold => FinalGold - StartingGold;

        /// <summary>Average net gold per real hour.</summary>
        public float GoldPerRealHour => RealHours > 0f ? NetGold / RealHours : 0f;

        /// <summary>One CSV row per real hour: scenario,hour,gold.</summary>
        public void WriteCsv(TextWriter writer)
        {
            writer.WriteLine("scenario,hour,gold");
            for (int i = 0; i < GoldByHour.Count; i++)
                writer.WriteLine(ScenarioName + "," + (i + 1) + "," + GoldByHour[i]);
        }

        /// <summary>Markdown summary of the run.</summary>
        public void WriteSummary(TextWriter writer)
        {
            writer.WriteLine("### " + ScenarioName + " (seed " + Seed + ", " + RealHours + " real hours)");
            writer.WriteLine();
            writer.WriteLine("| Metric | Value |");
            writer.WriteLine("|---|---|");
            writer.WriteLine("| Starting gold | " + StartingGold + " |");
            writer.WriteLine("| Final gold | " + FinalGold + " |");
            writer.WriteLine("| Net gold per real hour | " + GoldPerRealHour.ToString("F0") + " |");
            writer.WriteLine("| Crop sales | " + IncomeCrops + " |");
            writer.WriteLine("| Dish sales | " + IncomeDishes + " |");
            writer.WriteLine("| Tips | " + IncomeTips + " |");
            writer.WriteLine("| Seeds bought | " + SpendSeeds + " |");
            writer.WriteLine("| Hero meals | " + SpendMeals + " |");
            writer.WriteLine("| Patrons arrived / served / left hungry | " + PatronsArrived + " / " + PatronsServed + " / " + PatronsLeftHungry + " |");
            writer.WriteLine("| Hero meals eaten / skipped | " + HeroMeals + " / " + HeroMealsSkipped + " |");
            writer.WriteLine("| Worker utilization | " + (WorkerUtilization * 100f).ToString("F0") + "% |");
            writer.WriteLine("| Wet fraction (growing plots) | " + (WetFraction * 100f).ToString("F0") + "% |");
            writer.WriteLine();

            writer.WriteLine("| Milestone | Reached after |");
            writer.WriteLine("|---|---|");
            for (int i = 0; i < Milestones.Length; i++)
                writer.WriteLine("| " + Milestones[i] + " g | " + FormatMinutes(MilestoneMinutes[i]) + " |");
            writer.WriteLine();

            writer.WriteLine("| Hour | Gold |");
            writer.WriteLine("|---|---|");
            for (int i = 0; i < GoldByHour.Count; i++)
                writer.WriteLine("| " + (i + 1) + " | " + GoldByHour[i] + " |");
            writer.WriteLine();

            writer.WriteLine("| Crop | Harvests | Units sold | Gold realized | Avg demand | Final demand | Unlocked at |");
            writer.WriteLine("|---|---|---|---|---|---|---|");
            var order = CropUnlockConfig.ProgressionOrder;
            for (int i = 0; i < order.Length; i++)
            {
                int c = (int)order[i];
                if (HarvestsByCrop[c] == 0 && UnitsSoldByCrop[c] == 0 && CropUnlockMinutes[c] <= 0)
                    continue;
                writer.WriteLine("| " + order[i] + " | " + HarvestsByCrop[c] + " | " + UnitsSoldByCrop[c] + " | "
                    + GoldRealizedByCrop[c] + " | " + (AverageDemand(order[i]) * 100f).ToString("F0") + "% | "
                    + (FinalDemand[c] * 100f).ToString("F0") + "% | " + FormatMinutes(CropUnlockMinutes[c]) + " |");
            }
            writer.WriteLine();

            bool anyDish = false;
            for (int d = 0; d < DishTypeInfo.Count; d++)
                if (DishesServedByDish[d] > 0 || DishUnlockMinutes[d] > 0) anyDish = true;
            if (anyDish)
            {
                writer.WriteLine("| Dish | Price | Served | Unlocked at |");
                writer.WriteLine("|---|---|---|---|");
                var dishOrder = DishUnlockConfig.ProgressionOrder;
                for (int i = 0; i < dishOrder.Length; i++)
                {
                    int d = (int)dishOrder[i];
                    if (DishesServedByDish[d] == 0 && DishUnlockMinutes[d] <= 0)
                        continue;
                    writer.WriteLine("| " + dishOrder[i] + " | " + DishConfig.GetPrice(dishOrder[i]) + " | "
                        + DishesServedByDish[d] + " | " + FormatMinutes(DishUnlockMinutes[d]) + " |");
                }
                writer.WriteLine();
            }
        }

        private static string FormatMinutes(int minutes)
        {
            if (minutes < 0) return "never";
            if (minutes == 0) return "start";
            int h = minutes / 60;
            int m = minutes % 60;
            return h + "h " + m.ToString("D2") + "m";
        }
    }
}

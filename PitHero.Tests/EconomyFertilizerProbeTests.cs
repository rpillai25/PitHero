using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Farming;
using PitHero.VirtualGame.Economy;

namespace PitHero.Tests
{
    /// <summary>Probe: how the fertilizer artifacts (2x / 3x growth) interact with market saturation.</summary>
    [TestClass]
    public class EconomyFertilizerProbeTests
    {
        [TestMethod]
        [TestCategory("EconomyProbe")]
        public void Fertilizer_GrowthMultiplier_Probe()
        {
            var sb = new StringBuilder(4096);
            using var w = new StringWriter(sb);
            w.WriteLine("| Farm | Growth | g / real hour | Crop sales | Kitchen | Seeds | 1M at | Apple avg demand | Pumpkin avg | Turnip avg | Worker util |");
            w.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            float[] mults = { 1f, 2f, 3f };
            for (int i = 0; i < mults.Length; i++)
            {
                var d = EconomyScenario.LateGameDiverse(realHours: 12f);
                d.GrowthSpeedMultiplier = mults[i];
                Row(w, "diverse 100", new VirtualEconomySimulation().Run(d), mults[i]);
                var m = EconomyScenario.LateGameMonoculture(realHours: 12f);
                m.GrowthSpeedMultiplier = mults[i];
                Row(w, "100 apple", new VirtualEconomySimulation().Run(m), mults[i]);
            }
            System.Console.WriteLine(sb.ToString());
        }

        private static void Row(TextWriter w, string farm, EconomyRunMetrics r, float mult)
        {
            string million = r.MilestoneMinutes[3] < 0 ? "never" : (r.MilestoneMinutes[3] / 60) + "h " + (r.MilestoneMinutes[3] % 60) + "m";
            w.WriteLine($"| {farm} | {mult}x | {r.GoldPerRealHour:F0} | {r.IncomeCrops} | {r.IncomeDishes + r.IncomeTips} | {r.SpendSeeds} | {million} | {r.AverageDemand(CropType.AppleTree):P0} | {r.AverageDemand(CropType.Pumpkin):P0} | {r.AverageDemand(CropType.Turnip):P0} | {r.WorkerUtilization:P0} |");
        }
    }
}

using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.VirtualGame;
using PitHero.VirtualGame.Economy;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// Economy pacing (issue #417), measured by the headless farm + kitchen simulation. The bands
    /// are deliberately wide; the exact figures land in
    /// features/reports/feature_economy_417_balance_report.md (printed to the test output here).
    /// </summary>
    [TestClass]
    public class EconomySimulationTests
    {
        private static EconomyRunMetrics Run(EconomyScenario scenario)
        {
            var metrics = new VirtualEconomySimulation().Run(scenario);
            var sb = new StringBuilder(4096);
            using (var writer = new StringWriter(sb))
                metrics.WriteSummary(writer);
            System.Console.WriteLine(sb.ToString());
            return metrics;
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void Starter_FirstHours_PositiveAndModest()
        {
            var m = Run(EconomyScenario.Starter(realHours: 3f));
            Assert.IsTrue(m.GoldByHour.Count == 3);
            int firstHour = m.GoldByHour[0] - m.StartingGold;
            Assert.IsTrue(firstHour > 0, $"a starter farm must earn something in its first real hour (got {firstHour})");
            Assert.IsTrue(m.GoldPerRealHour >= 200f && m.GoldPerRealHour <= 2500f,
                $"starter farm earns {m.GoldPerRealHour:F0} g per real hour — pocket money, not wealth");
            Assert.IsTrue(m.MilestoneMinutes[0] > 0, "1,000 g is reached within three real hours");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void Starter_UnlocksTierOneCrops_WithinTwoRealHours()
        {
            var m = Run(EconomyScenario.Starter(realHours: 2f));
            int tomato = m.CropUnlockMinutes[(int)CropType.Tomato];
            Assert.IsTrue(tomato > 0 && tomato <= 120, $"Tomato unlocked at minute {tomato}");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void MidGame_IncomeBetweenStarterAndLate()
        {
            var starter = Run(EconomyScenario.Starter(realHours: 3f));
            var mid = Run(EconomyScenario.MidGame(realHours: 4f));
            var late = Run(EconomyScenario.LateGameDiverse(realHours: 4f));
            Assert.IsTrue(mid.GoldPerRealHour > starter.GoldPerRealHour, "mid-game out-earns the starter");
            Assert.IsTrue(late.GoldPerRealHour > mid.GoldPerRealHour, "late game out-earns mid-game");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void LateGameDiverse_ReachesOneMillion_InSevenToTwelveRealHours()
        {
            var m = Run(EconomyScenario.LateGameDiverse(realHours: 12f));
            int minute = m.MilestoneMinutes[3];
            Assert.IsTrue(minute > 0, $"1,000,000 g not reached in 12 real hours (final {m.FinalGold})");
            Assert.IsTrue(minute >= 7 * 60, $"1,000,000 g reached too early at minute {minute}");
            Assert.IsTrue(minute <= 12 * 60, $"1,000,000 g reached too late at minute {minute}");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void LateGameMonoculture_EarnsMuchLessThanDiverse()
        {
            var diverse = Run(EconomyScenario.LateGameDiverse(realHours: 12f));
            var mono = Run(EconomyScenario.LateGameMonoculture(realHours: 12f));
            Assert.IsTrue(mono.NetGold <= 0.6f * diverse.NetGold,
                $"100 apple trees earn {mono.NetGold} vs {diverse.NetGold} for a mixed farm");
            // Harvests arrive in synchronized bursts, so judge the price the farm actually sold at
            float avg = mono.AverageDemand(CropType.AppleTree);
            Assert.IsTrue(avg <= 0.35f, $"a monoculture sells near the demand floor (realized {avg:P0})");
            Assert.IsTrue(diverse.AverageDemand(CropType.AppleTree) > avg + 0.3f, "the mixed farm's apples sell far better");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void LateGame_DemandSettles_NoReplanningNeeded()
        {
            var m = Run(EconomyScenario.LateGameDiverse(realHours: 12f));
            // Income over hours 5-8 and 9-12 match within a third: the market has settled and the
            // farm keeps printing without any replanning (individual hours are lumpy: watermelon
            // cycles are 1.8 real hours and every plot was planted at once)
            int early = m.GoldGainedBetweenHours(5, 8);
            int late = m.GoldGainedBetweenHours(9, 12);
            float ratio = early > 0 ? (float)late / early : 0f;
            Assert.IsTrue(ratio > 0.67f && ratio < 1.5f, $"hours 5-8 earned {early}, hours 9-12 earned {late}");
            var order = CropUnlockConfig.ProgressionOrder;
            for (int i = 0; i < order.Length; i++)
                if (m.UnitsSoldByCrop[(int)order[i]] > 0)
                    Assert.IsTrue(m.AverageDemand(order[i]) >= 0.5f, $"{order[i]} sold at {m.AverageDemand(order[i]):P0} on a mixed farm");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void LateGame_KitchenIncome_MaterialButSecondary()
        {
            var m = Run(EconomyScenario.LateGameDiverse(realHours: 6f));
            long kitchen = m.IncomeDishes + m.IncomeTips;
            Assert.IsTrue(kitchen > 0, "a staffed kitchen sells dishes");
            Assert.IsTrue(kitchen <= 0.6f * m.IncomeCrops, $"kitchen {kitchen} vs crops {m.IncomeCrops}: the farm leads");
            Assert.IsTrue(kitchen >= 0.02f * m.IncomeCrops, $"kitchen {kitchen} vs crops {m.IncomeCrops}: the kitchen matters");
        }

        [TestMethod]
        [TestCategory("EconomySim")]
        public void SameSeed_ProducesIdenticalCsv()
        {
            var a = new StringBuilder();
            var b = new StringBuilder();
            using (var w = new StringWriter(a)) new VirtualEconomySimulation().Run(EconomyScenario.MidGame(realHours: 2f)).WriteCsv(w);
            using (var w = new StringWriter(b)) new VirtualEconomySimulation().Run(EconomyScenario.MidGame(realHours: 2f)).WriteCsv(w);
            Assert.AreEqual(a.ToString(), b.ToString());
        }

        /// <summary>Pit gold across four tiers: battle gold and chest pouches per level, for the report.</summary>
        [TestMethod]
        [TestCategory("EconomySim")]
        public void PitGold_Tiers1To4_BattleAndChestGoldPerLevel()
        {
            var sb = new StringBuilder(4096);
            using var writer = new StringWriter(sb);
            writer.WriteLine("| Depth | Tier | Level | Battles | Battle gold | Chest gold | Chests |");
            writer.WriteLine("|---|---|---|---|---|---|---|");
            int[] depths = { 1, 5, 10, 15, 20, 25, 30, 40, 50, 60, 75, 90, 100 };
            long battleTotal = 0, chestTotal = 0;
            for (int i = 0; i < depths.Length; i++)
            {
                var sim = new VirtualGameSimulation(12345);
                int level = RolePlayingFramework.Balance.BalanceConfig.EstimatePlayerLevelForPitLevel(depths[i]);
                var crystal = new HeroCrystal("RefCrystal", new Knight(), level, new StatBlock(10, 8, 10, 4));
                crystal.EarnJP(1_000_000);
                sim.ConfigureHero(new Knight(), level, new StatBlock(10, 8, 10, 4), crystal);
                var hero = sim.Hero.LinkedHero;
                for (int s = 0; s < hero.Job.Skills.Count; s++) hero.TryPurchaseSkill(hero.Job.Skills[s]);
                for (int p = 0; p < 5; p++) sim.Bag.TryAdd(PotionItems.HPPotion());
                var m = sim.RunPitLevel(depths[i]);
                writer.WriteLine($"| {depths[i]} | {m.PitTier} | {m.DisplayedLevel} | {m.BattleCount} | {m.GoldEarned} | {m.ChestGold} | {m.TreasuresOpened} |");
                battleTotal += m.GoldEarned;
                chestTotal += m.ChestGold;
            }
            System.Console.WriteLine(sb.ToString());
            Assert.IsTrue(chestTotal > 0, "chest pouches contribute pit gold");
            Assert.IsTrue(battleTotal > 0);
        }
    }
}

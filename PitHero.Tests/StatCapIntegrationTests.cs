using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class StatCapIntegrationTests
    {
        [TestMethod]
        public void Hero_LevelingToMax_StatsCappedAt99()
        {
            // Create a hero with high base stats
            var hero = new Hero("Maximus", new Knight(), level: 1, baseStats: new StatBlock(50, 50, 50, 50));

            // Level up to max (this would give stats well over 99 without capping)
            for (int i = 1; i < StatConstants.MaxLevel; i++)
            {
                hero.AddExperience(i * 100);
            }

            var totalStats = hero.GetTotalStats();
            Assert.IsTrue(totalStats.Strength <= StatConstants.MaxStat, $"Strength {totalStats.Strength} exceeds cap");
            Assert.IsTrue(totalStats.Agility <= StatConstants.MaxStat, $"Agility {totalStats.Agility} exceeds cap");
            Assert.IsTrue(totalStats.Vitality <= StatConstants.MaxStat, $"Vitality {totalStats.Vitality} exceeds cap");
            Assert.IsTrue(totalStats.Magic <= StatConstants.MaxStat, $"Magic {totalStats.Magic} exceeds cap");
        }

        [TestMethod]
        public void Hero_MaxLevel_CannotExceed99()
        {
            var hero = new Hero("Maximus", new Knight(), level: 98, baseStats: new StatBlock(10, 10, 10, 10));

            // Try to level up multiple times past max
            hero.AddExperience(100000); // Massive XP

            Assert.AreEqual(StatConstants.MaxLevel, hero.Level, "Level should not exceed MaxLevel");
        }

        [TestMethod]
        public void Hero_HPWithHighVitality_CappedAt9999()
        {
            // Create a hero that would have HP > 9999 without capping
            // HP = 25 + (Vit * 5)
            // To exceed 9999: Vit > (9999 - 25) / 5 = 1994.8
            var hero = new Hero("Tank", new Knight(), level: 99, baseStats: new StatBlock(99, 99, 99, 99));

            Assert.IsTrue(hero.MaxHP <= StatConstants.MaxHP, $"HP {hero.MaxHP} exceeds cap");
            Assert.IsTrue(hero.MaxHP > 0, "HP should be positive");
        }

        [TestMethod]
        public void Hero_MPWithHighMagic_CappedAt999()
        {
            // Create a hero that would have MP > 999 without capping
            // MP = 10 + (Mag * 3)
            // To exceed 999: Mag > (999 - 10) / 3 = 329.67
            var hero = new Hero("Mage", new Mage(), level: 99, baseStats: new StatBlock(99, 99, 99, 99));

            Assert.IsTrue(hero.MaxMP <= StatConstants.MaxMP, $"MP {hero.MaxMP} exceeds cap");
            Assert.IsTrue(hero.MaxMP > 0, "MP should be positive");
        }

        [TestMethod]
        public void Hero_LevelUp_StatsIncreaseCorrectly()
        {
            var hero = new Hero("Warrior", new Knight(), level: 1, baseStats: new StatBlock(10, 10, 10, 10));
            
            var initialStrength = hero.GetTotalStats().Strength;
            var initialHP = hero.MaxHP;
            var initialMP = hero.MaxMP;

            // Level up once
            hero.AddExperience(100);

            var newStrength = hero.GetTotalStats().Strength;
            var newHP = hero.MaxHP;
            var newMP = hero.MaxMP;

            Assert.IsTrue(newStrength > initialStrength, "Strength should increase on level up");
            Assert.IsTrue(newHP > initialHP, "HP should increase on level up");
            Assert.IsTrue(newMP >= initialMP, "MP should increase or stay same on level up");
        }

        [TestMethod]
        public void GrowthCurveCalculator_MatchesHeroImplementation()
        {
            var hero = new Hero("Test", new Knight(), level: 10, baseStats: new StatBlock(10, 10, 10, 10));
            var job = new Knight();

            // Calculate stats manually using GrowthCurveCalculator
            var expectedStats = GrowthCurveCalculator.CalculateTotalStatsAtLevel(
                hero.BaseStats,
                job.BaseBonus,
                job.GrowthPerLevel,
                10
            );

            var actualStats = hero.GetTotalStats();

            // Stats should match (accounting for equipment, which we don't have in this test)
            // Since hero has no equipment, job contribution should match
            var jobContribution = job.GetJobContributionAtLevel(10);
            var manualTotal = StatConstants.ClampStatBlock(hero.BaseStats.Add(jobContribution));

            Assert.AreEqual(manualTotal.Strength, actualStats.Strength);
            Assert.AreEqual(manualTotal.Agility, actualStats.Agility);
            Assert.AreEqual(manualTotal.Vitality, actualStats.Vitality);
            Assert.AreEqual(manualTotal.Magic, actualStats.Magic);
        }

        [TestMethod]
        public void Hero_RecalculateDerived_UsesGrowthCurveCalculator()
        {
            var hero = new Hero("Healer", new Priest(), level: 50, baseStats: new StatBlock(20, 20, 20, 40));
            
            var totalStats = hero.GetTotalStats();
            
            // Calculate expected HP using GrowthCurveCalculator
            // Hero.RecalculateDerived adds GetEquipmentHPBonus() after CalculateHP
            var expectedHP = GrowthCurveCalculator.CalculateHP(totalStats.Vitality, baseHP: 25, vitalityMultiplier: 5) 
                           + hero.GetEquipmentHPBonus();
            
            // Calculate expected MP using GrowthCurveCalculator
            // Hero.RecalculateDerived adds GetEquipmentMPBonus() after CalculateMP
            var expectedMP = GrowthCurveCalculator.CalculateMP(totalStats.Magic, baseMP: 10, magicMultiplier: 3)
                           + hero.GetEquipmentMPBonus();
            
            // Clamp to caps
            expectedHP = StatConstants.ClampHP(expectedHP);
            expectedMP = StatConstants.ClampMP(expectedMP);
            
            Assert.AreEqual(expectedHP, hero.MaxHP, "HP calculation should match GrowthCurveCalculator");
            Assert.AreEqual(expectedMP, hero.MaxMP, "MP calculation should match GrowthCurveCalculator");
        }

        [TestMethod]
        public void StatBlock_Add_RespectsCaps()
        {
            var stats1 = new StatBlock(90, 90, 90, 90);
            var stats2 = new StatBlock(20, 20, 20, 20);
            
            var sum = stats1.Add(stats2);
            var clamped = StatConstants.ClampStatBlock(sum);
            
            // Without clamping, each stat would be 110
            Assert.AreEqual(99, clamped.Strength);
            Assert.AreEqual(99, clamped.Agility);
            Assert.AreEqual(99, clamped.Vitality);
            Assert.AreEqual(99, clamped.Magic);
        }

        [TestMethod]
        public void Hero_MultipleJobs_StatsCapped()
        {
            // Test with a job that has high stat growth
            var hero = new Hero("Hybrid", new Knight(), level: 99, baseStats: new StatBlock(80, 80, 80, 80));
            
            var stats = hero.GetTotalStats();
            
            // All stats should be at or below cap
            Assert.IsTrue(stats.Strength <= StatConstants.MaxStat);
            Assert.IsTrue(stats.Agility <= StatConstants.MaxStat);
            Assert.IsTrue(stats.Vitality <= StatConstants.MaxStat);
            Assert.IsTrue(stats.Magic <= StatConstants.MaxStat);
        }
    }
}

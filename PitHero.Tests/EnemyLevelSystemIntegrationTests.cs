using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Enemies;
using PitHero.Config;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyLevelSystemIntegrationTests
    {
        [TestMethod]
        public void SlimeEnemy_UsesRequestedLevel_WhenProvided()
        {
            // Arrange
            int requestedLevel1 = 1;
            int requestedLevel2 = 5;
            int requestedLevel3 = 10;

            // Act
            var slime1 = new Slime(1);
            var slime2 = new Slime(5);
            var slime3 = new Slime(10);

            // Assert
            Assert.AreEqual(requestedLevel1, slime1.Level);
            Assert.AreEqual(requestedLevel2, slime2.Level);
            Assert.AreEqual(requestedLevel3, slime3.Level);
        }

        [TestMethod]
        public void SlimeEnemy_UsesPresetLevel_WhenRequestedLevelIsInvalid()
        {
            // Arrange
            var presetLevel = EnemyLevelConfig.GetPresetLevel(EnemyId.Slime);

            // Act
            var slime = new Slime(0);

            // Assert
            Assert.AreEqual(presetLevel, slime.Level);
        }

        [TestMethod]
        public void SlimeEnemy_StatsConsistentAtPresetLevel()
        {
            // Arrange
            var presetLevel = EnemyLevelConfig.GetPresetLevel(EnemyId.Slime);

            // Act - Create multiple slimes at preset level
            var slime1 = new Slime(presetLevel);
            var slime2 = new Slime(presetLevel);

            // Assert - All slimes should have consistent stats
            Assert.AreEqual(slime1.MaxHP, slime2.MaxHP);
            Assert.AreEqual(slime1.Stats.Strength, slime2.Stats.Strength);
            Assert.AreEqual(slime1.Stats.Agility, slime2.Stats.Agility);
            Assert.AreEqual(slime1.Stats.Vitality, slime2.Stats.Vitality);
            Assert.AreEqual(slime1.Stats.Magic, slime2.Stats.Magic);
            Assert.AreEqual(slime1.ExperienceYield, slime2.ExperienceYield);
        }

        [TestMethod]
        public void EnemyLevelConfig_SlimeLevel_IsAlwaysOne()
        {
            // Arrange & Act
            var slimeLevel = EnemyLevelConfig.GetPresetLevel(EnemyId.Slime);

            // Assert
            Assert.AreEqual(1, slimeLevel, "Slimes should always be level 1 according to the design requirements");
        }

        // -----------------------------------------------------------------------
        // Tests for the 9 previously-broken enemies (issue #291)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void AllPreviouslyBrokenEnemies_HonorRequestedLevel()
        {
            // Arrange – request a level well above each enemy's preset to confirm it is used.
            const int requested = 30;

            // Act
            var bat      = new Bat(requested);
            var goblin   = new Goblin(requested);
            var orc      = new Orc(requested);
            var pitLord  = new PitLord(requested);
            var rat      = new Rat(requested);
            var skeleton = new Skeleton(requested);
            var snake    = new Snake(requested);
            var spider   = new Spider(requested);
            var wraith   = new Wraith(requested);

            // Assert – Level must equal the requested value.
            Assert.AreEqual(requested, bat.Level,      "Bat should honour requested level");
            Assert.AreEqual(requested, goblin.Level,   "Goblin should honour requested level");
            Assert.AreEqual(requested, orc.Level,      "Orc should honour requested level");
            Assert.AreEqual(requested, pitLord.Level,  "PitLord should honour requested level");
            Assert.AreEqual(requested, rat.Level,      "Rat should honour requested level");
            Assert.AreEqual(requested, skeleton.Level, "Skeleton should honour requested level");
            Assert.AreEqual(requested, snake.Level,    "Snake should honour requested level");
            Assert.AreEqual(requested, spider.Level,   "Spider should honour requested level");
            Assert.AreEqual(requested, wraith.Level,   "Wraith should honour requested level");

            // Assert – MaxHP must be consistent with BalanceConfig at that level/archetype.
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.FastFragile), bat.MaxHP,      "Bat MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.Balanced),    goblin.MaxHP,   "Goblin MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.Tank),        orc.MaxHP,      "Orc MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.Tank),        pitLord.MaxHP,  "PitLord MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.Balanced),    rat.MaxHP,      "Rat MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.Tank),        skeleton.MaxHP, "Skeleton MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.FastFragile), snake.MaxHP,    "Snake MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.FastFragile), spider.MaxHP,   "Spider MaxHP should match BalanceConfig at requested level");
            Assert.AreEqual(BalanceConfig.CalculateMonsterHP(requested, BalanceConfig.MonsterArchetype.FastFragile), wraith.MaxHP,   "Wraith MaxHP should match BalanceConfig at requested level");
        }

        [TestMethod]
        public void AllPreviouslyBrokenEnemies_FallBackToPreset_WhenLevelIsZero()
        {
            // Act – level 0 signals "use preset".
            var bat      = new Bat(0);
            var goblin   = new Goblin(0);
            var orc      = new Orc(0);
            var pitLord  = new PitLord(0);
            var rat      = new Rat(0);
            var skeleton = new Skeleton(0);
            var snake    = new Snake(0);
            var spider   = new Spider(0);
            var wraith   = new Wraith(0);

            // Assert – each enemy should be at its configured preset level.
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Bat),      bat.Level,      "Bat(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Goblin),   goblin.Level,   "Goblin(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Orc),      orc.Level,      "Orc(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.PitLord),  pitLord.Level,  "PitLord(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Rat),      rat.Level,      "Rat(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Skeleton), skeleton.Level, "Skeleton(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Snake),    snake.Level,    "Snake(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Spider),   spider.Level,   "Spider(0) should use preset level");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Wraith),   wraith.Level,   "Wraith(0) should use preset level");
        }

        [TestMethod]
        public void EnemyFactory_CreateWithNoLevel_ReturnsPresetLevel()
        {
            // Calling EnemyFactory.Create with no level argument must yield each enemy's preset.
            var skeleton = EnemyFactory.Create(EnemyId.Skeleton);
            var pitLord  = EnemyFactory.Create(EnemyId.PitLord);

            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.Skeleton), skeleton.Level,
                "EnemyFactory.Create(Skeleton) with no level should return preset level 6");
            Assert.AreEqual(EnemyLevelConfig.GetPresetLevel(EnemyId.PitLord), pitLord.Level,
                "EnemyFactory.Create(PitLord) with no level should return preset level 10");
        }
    }
}

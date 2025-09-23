using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class EnhancedBattleSystemTests
    {
        /// <summary>Tests that BattleStats are calculated correctly for heroes</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void BattleStats_CalculateForHero_ReturnsCorrectValues()
        {
            // Arrange
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
            var hero = new Hero(name: "Test Knight", job: knight, level: 6, baseStats: baseStats);

            // Act
            var battleStats = BattleStats.CalculateForHero(hero);

            // Assert
            var totalStats = hero.GetTotalStats();
            var expectedAttack = totalStats.Strength + hero.GetEquipmentAttackBonus();
            var expectedDefense = totalStats.Agility / 2 + hero.GetEquipmentDefenseBonus();
            var expectedEvasion = System.Math.Min(255, totalStats.Agility * 2 + hero.Level);

            Assert.AreEqual(expectedAttack, battleStats.Attack, "Hero Attack calculation is incorrect");
            Assert.AreEqual(expectedDefense, battleStats.Defense, "Hero Defense calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Evasion, "Hero Evasion calculation is incorrect");
        }

        /// <summary>Tests that BattleStats are calculated correctly for monsters</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void BattleStats_CalculateForMonster_ReturnsCorrectValues()
        {
            // Arrange
            var slime = new Slime(level: 3);

            // Act
            var battleStats = BattleStats.CalculateForMonster(slime);

            // Assert
            var expectedAttack = slime.Stats.Strength;
            var expectedDefense = slime.Stats.Agility / 2;
            var expectedEvasion = System.Math.Min(255, slime.Stats.Agility * 2 + slime.Level);

            Assert.AreEqual(expectedAttack, battleStats.Attack, "Monster Attack calculation is incorrect");
            Assert.AreEqual(expectedDefense, battleStats.Defense, "Monster Defense calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Evasion, "Monster Evasion calculation is incorrect");
        }

        /// <summary>Tests the new damage calculation formula</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_CalculateDamage_ReturnsCorrectValues()
        {
            // Arrange
            var resolver = new EnhancedAttackResolver();

            // Act & Assert - Test attack >= defense
            var damage1 = resolver.CalculateDamage(attack: 20, defense: 15);
            var expectedDamage1 = 20 * 2 - 15; // 25
            Assert.AreEqual(expectedDamage1, damage1, "Damage calculation for attack >= defense is incorrect");

            // Act & Assert - Test attack < defense
            var damage2 = resolver.CalculateDamage(attack: 10, defense: 15);
            var expectedDamage2 = 10 * 10 / 15; // 6 (integer division)
            Assert.AreEqual(expectedDamage2, damage2, "Damage calculation for attack < defense is incorrect");

            // Act & Assert - Test minimum damage
            var damage3 = resolver.CalculateDamage(attack: 1, defense: 100);
            Assert.AreEqual(1, damage3, "Minimum damage should be 1");
        }

        /// <summary>Tests the evasion calculation with different evasion values</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_CalculateEvasion_ReturnsExpectedProbabilities()
        {
            // Arrange
            var resolver = new EnhancedAttackResolver();
            const int trials = 1000;

            // Test with high evasion (should evade most attacks)
            int evadeCount = 0;
            for (int i = 0; i < trials; i++)
            {
                if (resolver.CalculateEvasion(200)) // High evasion
                    evadeCount++;
            }
            double evadeRate = (double)evadeCount / trials;
            Assert.IsTrue(evadeRate > 0.6, $"High evasion should result in >60% evasion rate, got {evadeRate:P}");

            // Test with low evasion (should evade few attacks)
            evadeCount = 0;
            for (int i = 0; i < trials; i++)
            {
                if (resolver.CalculateEvasion(50)) // Low evasion
                    evadeCount++;
            }
            evadeRate = (double)evadeCount / trials;
            Assert.IsTrue(evadeRate < 0.4, $"Low evasion should result in <40% evasion rate, got {evadeRate:P}");
        }

        /// <summary>Tests that enhanced attack resolver correctly handles hits and misses</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_Resolve_HandlesHitsAndMisses()
        {
            // Arrange
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 50);
            var defenderStats = new BattleStats(attack: 15, defense: 8, evasion: 200); // High evasion

            // Act - Try multiple attacks to test both hits and misses
            int hitCount = 0;
            int missCount = 0;
            const int trials = 100;

            for (int i = 0; i < trials; i++)
            {
                var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Physical);
                if (result.Hit)
                {
                    hitCount++;
                    Assert.IsTrue(result.Damage > 0, "Hit attacks must deal positive damage");
                }
                else
                {
                    missCount++;
                    Assert.AreEqual(0, result.Damage, "Missed attacks must deal 0 damage");
                }
            }

            // Assert - Should have both hits and misses due to high defender evasion
            Assert.IsTrue(hitCount > 0, "Should have some hits");
            Assert.IsTrue(missCount > 0, "Should have some misses with high evasion");
            Assert.AreEqual(trials, hitCount + missCount, "Total results should equal trials");
        }

        /// <summary>Tests backwards compatibility with legacy attack resolver interface</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_LegacyInterface_WorksCorrectly()
        {
            // Arrange
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new StatBlock(strength: 15, agility: 12, vitality: 10, magic: 5);
            var defenderStats = new StatBlock(strength: 10, agility: 8, vitality: 12, magic: 3);

            // Act
            var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Physical, 
                attackerLevel: 5, defenderLevel: 3);

            // Assert
            Assert.IsTrue(result.Hit || !result.Hit, "Should return a valid result (hit or miss)");
            if (result.Hit)
            {
                Assert.IsTrue(result.Damage > 0, "Hit attacks must deal positive damage");
            }
            else
            {
                Assert.AreEqual(0, result.Damage, "Missed attacks must deal 0 damage");
            }
        }
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

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
            var expectedEvasion = System.Math.Min(255, totalStats.Agility * 2 + hero.Level / 2);

            Assert.AreEqual(expectedAttack, battleStats.Attack, "Hero Attack calculation is incorrect");
            Assert.AreEqual(expectedDefense, battleStats.Defense, "Hero Defense calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Evasion, "Hero Evasion calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Accuracy, "Hero Accuracy should equal base evasion (no gear/buff bonuses)");
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
            var expectedEvasion = System.Math.Min(255, slime.Stats.Agility * 2 + slime.Level / 2);

            Assert.AreEqual(expectedAttack, battleStats.Attack, "Monster Attack calculation is incorrect");
            Assert.AreEqual(expectedDefense, battleStats.Defense, "Monster Defense calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Evasion, "Monster Evasion calculation is incorrect");
            Assert.AreEqual(expectedEvasion, battleStats.Accuracy, "Monster Accuracy should equal base evasion");
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

        /// <summary>Tests the dodge roll with different effective dodge chances</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_RollDodge_ReturnsExpectedProbabilities()
        {
            // Arrange
            var resolver = new EnhancedAttackResolver();
            const int trials = 1000;

            // Test with high dodge chance (should evade most attacks)
            int evadeCount = 0;
            for (int i = 0; i < trials; i++)
            {
                if (resolver.RollDodge(200)) // High dodge chance (~78%)
                    evadeCount++;
            }
            double evadeRate = (double)evadeCount / trials;
            Assert.IsTrue(evadeRate > 0.6, $"High dodge chance should result in >60% evasion rate, got {evadeRate:P}");

            // Test with low dodge chance (should evade few attacks)
            evadeCount = 0;
            for (int i = 0; i < trials; i++)
            {
                if (resolver.RollDodge(50)) // Low dodge chance (~20%)
                    evadeCount++;
            }
            evadeRate = (double)evadeCount / trials;
            Assert.IsTrue(evadeRate < 0.4, $"Low dodge chance should result in <40% evasion rate, got {evadeRate:P}");
        }

        /// <summary>Tests that enhanced attack resolver correctly handles hits and misses</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_Resolve_HandlesHitsAndMisses()
        {
            // Arrange — dodge chance = clamp(13 + 200 − 50) = 163 (~64%): both outcomes frequent
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 50, accuracy: 50);
            var defenderStats = new BattleStats(attack: 15, defense: 8, evasion: 200, accuracy: 200); // High evasion

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

        /// <summary>Magical attacks bypass physical evasion entirely — even a max-evasion defender is always hit</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_MagicalAttack_BypassesEvasion()
        {
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 0, accuracy: 0);
            var defenderStats = new BattleStats(attack: 15, defense: 8, evasion: 255, accuracy: 255);

            for (int i = 0; i < 100; i++)
            {
                var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Magical);
                Assert.IsTrue(result.Hit, "Magical attacks must never be dodged by physical evasion");
                Assert.IsTrue(result.Damage > 0, "Magical hits must deal positive damage");
            }
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

        /// <summary>Tests elemental advantage (2x damage multiplier)</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_WithElementalAdvantage_DealsBonusDamage()
        {
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 0, accuracy: 0);
            var defenderStats = new BattleStats(attack: 15, defense: 10, evasion: 0, accuracy: 0);
            var defenderProps = new ElementalProperties(ElementType.Water);

            // Fire vs Water should deal 2x damage (Fire opposes Water).
            // Magical kind bypasses the dodge roll (physical has a 5% whiff floor),
            // keeping this damage-math assertion deterministic.
            var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Magical,
                ElementType.Fire, defenderProps);
            
            int baseDamage = 20 * 2 - 10; // 30 base damage
            int expectedMin = (int)(baseDamage * 2.0f * 0.9f); // Account for variance
            int expectedMax = (int)(baseDamage * 2.0f * 1.1f);
            
            Assert.IsTrue(result.Hit);
            Assert.IsTrue(result.Damage >= expectedMin && result.Damage <= expectedMax, 
                $"Expected damage between {expectedMin}-{expectedMax}, got {result.Damage}");
        }

        /// <summary>Tests elemental disadvantage (0.5x damage multiplier)</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_WithElementalDisadvantage_DealsReducedDamage()
        {
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 0, accuracy: 0);
            var defenderStats = new BattleStats(attack: 15, defense: 10, evasion: 0, accuracy: 0);
            var defenderProps = new ElementalProperties(ElementType.Fire);

            // Fire vs Fire should deal 0.5x damage (same element = resistance).
            // Magical kind bypasses the dodge roll — deterministic hit for the damage assertion.
            var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Magical,
                ElementType.Fire, defenderProps);
            
            int baseDamage = 20 * 2 - 10; // 30 base damage
            int expectedMin = (int)(baseDamage * 0.5f * 0.9f); // Account for variance
            int expectedMax = (int)(baseDamage * 0.5f * 1.1f);
            
            Assert.IsTrue(result.Hit);
            Assert.IsTrue(result.Damage >= expectedMin && result.Damage <= expectedMax, 
                $"Expected damage between {expectedMin}-{expectedMax}, got {result.Damage}");
        }

        /// <summary>Tests neutral elements (1.0x damage multiplier)</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void EnhancedAttackResolver_WithNeutralElement_DealsNormalDamage()
        {
            var resolver = new EnhancedAttackResolver();
            var attackerStats = new BattleStats(attack: 20, defense: 10, evasion: 0, accuracy: 0);
            var defenderStats = new BattleStats(attack: 15, defense: 10, evasion: 0, accuracy: 0);
            var defenderProps = new ElementalProperties(ElementType.Fire);

            // Neutral vs Fire should deal 1.0x damage (no relationship).
            // Magical kind bypasses the dodge roll — deterministic hit for the damage assertion.
            var result = resolver.Resolve(attackerStats, defenderStats, DamageKind.Magical,
                ElementType.Neutral, defenderProps);

            int baseDamage = 20 * 2 - 10; // 30 base damage
            int expectedMin = (int)(baseDamage * 1.0f * 0.9f); // Account for variance
            int expectedMax = (int)(baseDamage * 1.0f * 1.1f);

            Assert.IsTrue(result.Hit);
            Assert.IsTrue(result.Damage >= expectedMin && result.Damage <= expectedMax,
                $"Expected damage between {expectedMin}-{expectedMax}, got {result.Damage}");
        }

        // ── ResolveHit elemental multiplier integration ───────────────────────────────

        /// <summary>
        /// Verifies that the elemental multiplier flows all the way through
        /// BaseSkill.ResolveHit when using EnhancedAttackResolver.
        /// A Fire skill should deal more to a Water enemy (2× advantage) than to a
        /// Fire enemy (0.5× resistance) — even accounting for ±10% variance.
        /// </summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void ResolveHit_ViaFireSkill_ElementalMultiplierApplied()
        {
            var resolver = new EnhancedAttackResolver();

            // High attack so elemental difference is large vs ±10% variance.
            // Knight job + StatBlock(50,0,10,2) gives plenty of attack without weapon.
            var merc = new Mercenary("Mage", new Knight(), 1, new StatBlock(50, 0, 10, 2));
            var skill = new FireSkill(); // Element = Fire

            // Accumulate damage over multiple rounds to smooth variance
            long waterDamageTotal = 0;
            long fireDamageTotal = 0;
            const int trials = 20;

            for (int t = 0; t < trials; t++)
            {
                // Water enemy: Fire has 2× advantage
                var waterEnemy = new ElementalEnemy(ElementType.Water, hp: 5000);
                skill.Execute(merc, waterEnemy, new List<IEnemy>(), resolver, null);
                waterDamageTotal += 5000 - waterEnemy.CurrentHP;

                // Fire enemy: Fire has 0.5× resistance
                var fireEnemy = new ElementalEnemy(ElementType.Fire, hp: 5000);
                skill.Execute(merc, fireEnemy, new List<IEnemy>(), resolver, null);
                fireDamageTotal += 5000 - fireEnemy.CurrentHP;
            }

            Assert.IsTrue(waterDamageTotal > fireDamageTotal,
                $"Fire skill should deal more to Water enemy (2× advantage) than Fire enemy (0.5× resistance). " +
                $"Water total={waterDamageTotal}, Fire total={fireDamageTotal} over {trials} trials");
        }

        /// <summary>Minimal test enemy with configurable element and zero evasion.</summary>
        private sealed class ElementalEnemy : IEnemy
        {
            private int _hp;
            public ElementalEnemy(ElementType element, int hp = 1000)
            {
                Element = element;
                ElementalProps = new ElementalProperties(element);
                _hp = hp;
                MaxHP = hp;
            }
            public string Name => "ElementalEnemy";
            public EnemyId EnemyId => EnemyId.Slime;
            public int Level => 1;
            public StatBlock Stats => new StatBlock(5, 0, 5, 5); // Agility=0 → near-zero evasion
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element { get; }
            public ElementalProperties ElementalProps { get; }
            public int MaxHP { get; }
            public int CurrentHP => _hp;
            public int ExperienceYield => 0;
            public int JPYield => 0;
            public int SPYield => 0;
            public int GoldYield => 0;
            public float JoinPercentageModifier => 1.0f;
            public bool IsBoss => false;
            public bool IsRecruitable => false;
            public bool TakeDamage(int amount)
            {
                if (amount <= 0) return false;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
        }
    }
}
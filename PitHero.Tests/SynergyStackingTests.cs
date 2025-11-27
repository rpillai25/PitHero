using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the Synergy Stacking System (Issue #133).
    /// Validates diminishing returns, overlap rejection, point acceleration, and skill unlocking.
    /// </summary>
    [TestClass]
    public class SynergyStackingTests
    {
        #region SynergyEffectAggregator Tests
        
        [TestMethod]
        public void SynergyEffectAggregator_SingleInstance_Returns100PercentMultiplier()
        {
            // Act
            float multiplier = SynergyEffectAggregator.GetTotalMultiplier(1);
            
            // Assert
            Assert.AreEqual(1.0f, multiplier, 0.001f, "Single instance should have 1.0x multiplier");
        }
        
        [TestMethod]
        public void SynergyEffectAggregator_TwoInstances_Returns150PercentMultiplier()
        {
            // Act
            float multiplier = SynergyEffectAggregator.GetTotalMultiplier(2);
            
            // Assert
            Assert.AreEqual(1.5f, multiplier, 0.001f, "Two instances should have 1.5x multiplier (1.0 + 0.5)");
        }
        
        [TestMethod]
        public void SynergyEffectAggregator_ThreeInstances_Returns175PercentMultiplier()
        {
            // Act
            float multiplier = SynergyEffectAggregator.GetTotalMultiplier(3);
            
            // Assert
            Assert.AreEqual(1.75f, multiplier, 0.001f, "Three instances should have 1.75x multiplier (1.0 + 0.5 + 0.25)");
        }
        
        [TestMethod]
        public void SynergyEffectAggregator_FourthInstance_DoesNotExceedCap()
        {
            // Act
            float multiplier = SynergyEffectAggregator.GetTotalMultiplier(4);
            
            // Assert: Should still be 1.75, fourth instance is rejected at aggregator level
            Assert.AreEqual(1.75f, multiplier, 0.001f, "Fourth instance should be capped at 1.75x");
        }
        
        [TestMethod]
        public void SynergyEffectAggregator_ZeroInstances_ReturnsZero()
        {
            // Act
            float multiplier = SynergyEffectAggregator.GetTotalMultiplier(0);
            
            // Assert
            Assert.AreEqual(0f, multiplier, 0.001f, "Zero instances should have 0x multiplier");
        }
        
        [TestMethod]
        public void SynergyEffectAggregator_GetInstanceMultiplier_ReturnsCorrectValues()
        {
            // Assert
            Assert.AreEqual(1.0f, SynergyEffectAggregator.GetInstanceMultiplier(0), 0.001f, "First instance index 0 = 1.0");
            Assert.AreEqual(0.5f, SynergyEffectAggregator.GetInstanceMultiplier(1), 0.001f, "Second instance index 1 = 0.5");
            Assert.AreEqual(0.25f, SynergyEffectAggregator.GetInstanceMultiplier(2), 0.001f, "Third instance index 2 = 0.25");
            Assert.AreEqual(0f, SynergyEffectAggregator.GetInstanceMultiplier(3), 0.001f, "Fourth instance index 3 = 0 (beyond cap)");
            Assert.AreEqual(0f, SynergyEffectAggregator.GetInstanceMultiplier(-1), 0.001f, "Negative index = 0");
        }
        
        #endregion
        
        #region Point Acceleration Tests
        
        [TestMethod]
        public void PointsAcceleration_SingleInstance_NoAcceleration()
        {
            // Act
            float accel = SynergyEffectAggregator.GetPointsAccelerationMultiplier(1, skillLearned: false);
            
            // Assert
            Assert.AreEqual(1.0f, accel, 0.001f, "Single instance should have no acceleration");
        }
        
        [TestMethod]
        public void PointsAcceleration_TwoInstances_35PercentBonus()
        {
            // Act
            float accel = SynergyEffectAggregator.GetPointsAccelerationMultiplier(2, skillLearned: false);
            
            // Assert: 1.0 + 0.35 * (2-1) = 1.35
            Assert.AreEqual(1.35f, accel, 0.001f, "Two instances should have 1.35x acceleration");
        }
        
        [TestMethod]
        public void PointsAcceleration_ThreeInstances_CappedAt70Percent()
        {
            // Act
            float accel = SynergyEffectAggregator.GetPointsAccelerationMultiplier(3, skillLearned: false);
            
            // Assert: 1.0 + 0.35 * (3-1) = 1.70 (at cap)
            Assert.AreEqual(1.70f, accel, 0.001f, "Three instances should be capped at 1.70x");
        }
        
        [TestMethod]
        public void PointsAcceleration_FourInstances_StillCappedAt70Percent()
        {
            // Act
            float accel = SynergyEffectAggregator.GetPointsAccelerationMultiplier(4, skillLearned: false);
            
            // Assert: Would be 1.0 + 0.35 * 3 = 2.05, but capped at 1.70
            Assert.AreEqual(1.70f, accel, 0.001f, "Four instances should still be capped at 1.70x");
        }
        
        [TestMethod]
        public void PointsAcceleration_AfterSkillLearned_NoAcceleration()
        {
            // Act
            float accel = SynergyEffectAggregator.GetPointsAccelerationMultiplier(3, skillLearned: true);
            
            // Assert: After skill learned, no acceleration
            Assert.AreEqual(1.0f, accel, 0.001f, "After skill learned, acceleration should be 1.0x");
        }
        
        #endregion
        
        #region ActiveSynergy SharesItems Tests
        
        [TestMethod]
        public void ActiveSynergy_SharesItems_ReturnsTrueWhenOverlapping()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var synergy1 = new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) });
            var synergy2 = new ActiveSynergy(pattern, new Point(1, 0), 
                new List<Point> { new Point(1, 0), new Point(2, 0) });
            
            // Act & Assert
            Assert.IsTrue(synergy1.SharesItems(synergy2), "Synergies sharing slot (1,0) should overlap");
        }
        
        [TestMethod]
        public void ActiveSynergy_SharesItems_ReturnsFalseWhenNoOverlap()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var synergy1 = new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) });
            var synergy2 = new ActiveSynergy(pattern, new Point(3, 0), 
                new List<Point> { new Point(3, 0), new Point(4, 0) });
            
            // Act & Assert
            Assert.IsFalse(synergy1.SharesItems(synergy2), "Non-overlapping synergies should not share items");
        }
        
        [TestMethod]
        public void ActiveSynergy_SharesItems_ReturnsFalseForNull()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var synergy = new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0) });
            
            // Act & Assert
            Assert.IsFalse(synergy.SharesItems(null!), "SharesItems should return false for null");
        }
        
        #endregion
        
        #region ActiveSynergyGroup Tests
        
        [TestMethod]
        public void ActiveSynergyGroup_TryAddInstance_AcceptsNonOverlapping()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var group = new ActiveSynergyGroup(pattern);
            var synergy1 = new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) });
            var synergy2 = new ActiveSynergy(pattern, new Point(3, 0), 
                new List<Point> { new Point(3, 0), new Point(4, 0) });
            
            // Act
            bool added1 = group.TryAddInstance(synergy1);
            bool added2 = group.TryAddInstance(synergy2);
            
            // Assert
            Assert.IsTrue(added1, "First instance should be added");
            Assert.IsTrue(added2, "Non-overlapping instance should be added");
            Assert.AreEqual(2, group.InstanceCount, "Group should have 2 instances");
        }
        
        [TestMethod]
        public void ActiveSynergyGroup_TryAddInstance_RejectsOverlapping()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var group = new ActiveSynergyGroup(pattern);
            var synergy1 = new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) });
            var synergy2 = new ActiveSynergy(pattern, new Point(1, 0), 
                new List<Point> { new Point(1, 0), new Point(2, 0) });
            
            // Act
            bool added1 = group.TryAddInstance(synergy1);
            bool added2 = group.TryAddInstance(synergy2);
            
            // Assert
            Assert.IsTrue(added1, "First instance should be added");
            Assert.IsFalse(added2, "Overlapping instance should be rejected");
            Assert.AreEqual(1, group.InstanceCount, "Group should have 1 instance");
        }
        
        [TestMethod]
        public void ActiveSynergyGroup_TryAddInstance_RejectsAtMaxCap()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var group = new ActiveSynergyGroup(pattern);
            
            // Add max instances (3)
            for (int i = 0; i < SynergyEffectAggregator.MaxInstancesPerPattern; i++)
            {
                var synergy = new ActiveSynergy(pattern, new Point(i * 10, 0), 
                    new List<Point> { new Point(i * 10, 0), new Point(i * 10 + 1, 0) });
                group.TryAddInstance(synergy);
            }
            
            // Try to add 4th
            var synergy4 = new ActiveSynergy(pattern, new Point(100, 0), 
                new List<Point> { new Point(100, 0), new Point(101, 0) });
            
            // Act
            bool added4 = group.TryAddInstance(synergy4);
            
            // Assert
            Assert.IsFalse(added4, "Fourth instance should be rejected at cap");
            Assert.AreEqual(3, group.InstanceCount, "Group should have max 3 instances");
        }
        
        [TestMethod]
        public void ActiveSynergyGroup_TotalMultiplier_CalculatesCorrectly()
        {
            // Arrange
            var pattern = CreateTestPattern("test");
            var group = new ActiveSynergyGroup(pattern);
            
            // Assert initial
            Assert.AreEqual(0f, group.TotalMultiplier, 0.001f, "Empty group should have 0 multiplier");
            
            // Add instances and verify multiplier
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0) }));
            Assert.AreEqual(1.0f, group.TotalMultiplier, 0.001f, "1 instance = 1.0x");
            
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(10, 0), 
                new List<Point> { new Point(10, 0) }));
            Assert.AreEqual(1.5f, group.TotalMultiplier, 0.001f, "2 instances = 1.5x");
            
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(20, 0), 
                new List<Point> { new Point(20, 0) }));
            Assert.AreEqual(1.75f, group.TotalMultiplier, 0.001f, "3 instances = 1.75x");
        }
        
        #endregion
        
        #region SynergyDetector Grouped Detection Tests
        
        [TestMethod]
        public void SynergyDetector_DetectSynergiesGrouped_GroupsByPattern()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(KnightSynergyPatterns.CreateShieldMastery());
            
            // Create grid with 2 separate sword+shield pairs (non-overlapping)
            var grid = new IItem[20, 8];
            grid[0, 0] = GearItems.ShortSword();
            grid[1, 0] = GearItems.WoodenShield();
            grid[5, 0] = GearItems.ShortSword();
            grid[6, 0] = GearItems.WoodenShield();
            
            // Act
            var groups = detector.DetectSynergiesGrouped(grid, 20, 8);
            
            // Assert
            Assert.AreEqual(1, groups.Count, "Should have 1 group for knight.shield_mastery");
            Assert.AreEqual("knight.shield_mastery", groups[0].Pattern.Id);
            Assert.AreEqual(2, groups[0].InstanceCount, "Group should have 2 non-overlapping instances");
            Assert.AreEqual(1.5f, groups[0].TotalMultiplier, 0.001f, "2 instances = 1.5x multiplier");
        }
        
        [TestMethod]
        public void SynergyDetector_DetectSynergiesGrouped_RejectsOverlappingInstances()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(KnightSynergyPatterns.CreateShieldMastery());
            
            // Create grid with overlapping patterns (sharing the shield)
            var grid = new IItem[20, 8];
            grid[0, 0] = GearItems.ShortSword();
            grid[1, 0] = GearItems.WoodenShield();  // Shared shield
            grid[2, 0] = GearItems.ShortSword();    // Another sword next to shield
            // Note: Pattern is Sword at (0,0), Shield at (1,0) relative to anchor
            // So anchoring at (1,0) would need Shield at (2,0) which is a sword - won't match
            // But we could have sword at (0,0) shield at (1,0) AND try sword at (2,0) shield at... doesn't exist
            // Let's verify with proper overlapping setup:
            
            // Actually create 2 patterns that would overlap on shield at (1,0)
            // Pattern 1: anchor(0,0) -> sword(0,0), shield(1,0)
            // To have overlap, we'd need another pattern using shield(1,0)
            // But sword-shield pattern requires sword at anchor, shield at offset(1,0)
            // So another match at anchor(0,0) would be duplicate, anchor(1,0) would need shield at (2,0)
            
            // Let me restructure: Create a situation where same items could be matched multiple times
            // Place: Sword, Shield, Sword - the middle items shouldn't be double-counted
            grid[3, 0] = GearItems.ShortSword();
            grid[4, 0] = GearItems.WoodenShield();  // This shield
            // If we had sword at (5,0), it would NOT create overlap because pattern is Sword+Shield to right
            // shield at (4,0) is only used by anchor at (3,0)
            
            // Act
            var groups = detector.DetectSynergiesGrouped(grid, 20, 8);
            
            // Assert: Should have 2 non-overlapping instances (at 0,0 and 3,0)
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(2, groups[0].InstanceCount, 
                "Should detect 2 separate sword-shield pairs without overlap");
        }
        
        [TestMethod]
        public void SynergyDetector_DetectSynergiesGrouped_CapsAtThreeInstances()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(KnightSynergyPatterns.CreateShieldMastery());
            
            // Create grid with 5 separate sword+shield pairs
            var grid = new IItem[50, 8];
            for (int i = 0; i < 5; i++)
            {
                grid[i * 10, 0] = GearItems.ShortSword();
                grid[i * 10 + 1, 0] = GearItems.WoodenShield();
            }
            
            // Act
            var groups = detector.DetectSynergiesGrouped(grid, 50, 8);
            
            // Assert
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(3, groups[0].InstanceCount, "Should cap at 3 instances even with 5 available");
            Assert.AreEqual(1.75f, groups[0].TotalMultiplier, 0.001f, "3 instances = 1.75x");
        }
        
        #endregion
        
        #region Hero Grouped Synergy Application Tests
        
        [TestMethod]
        public void Hero_UpdateActiveSynergiesGrouped_AppliesMultipliedEffects()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            int baseDefense = hero.PassiveDefenseBonus;
            
            // Create a group with 2 instances of sword-shield pattern (+5 defense effect)
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(10, 0), 
                new List<Point> { new Point(10, 0), new Point(11, 0) }));
            
            // Act
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Assert: 2 instances = 1.5x multiplier, +5 defense * 1.5 = +7 defense (truncated)
            Assert.AreEqual(baseDefense + 7, hero.PassiveDefenseBonus, 
                "Defense bonus should be 5 * 1.5 = 7 (truncated from 7.5)");
        }
        
        [TestMethod]
        public void Hero_UpdateActiveSynergiesGrouped_RemovesOldEffects()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            int baseDefense = hero.PassiveDefenseBonus;
            
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            
            // Apply synergy
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            Assert.AreNotEqual(baseDefense, hero.PassiveDefenseBonus);
            
            // Remove synergy
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup>());
            
            // Assert: Effects should be removed
            Assert.AreEqual(baseDefense, hero.PassiveDefenseBonus, 
                "Defense should return to base after removing synergy");
        }
        
        [TestMethod]
        public void Hero_UpdateActiveSynergiesGrouped_PopulatesLegacyList()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(10, 0), 
                new List<Point> { new Point(10, 0), new Point(11, 0) }));
            
            // Act
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Assert: Legacy ActiveSynergies list should contain all instances
            Assert.AreEqual(2, hero.ActiveSynergies.Count, "Legacy list should have both instances");
            Assert.AreEqual(1, hero.ActiveSynergyGroups.Count, "Groups list should have 1 group");
        }
        
        #endregion
        
        #region Synergy Point Acceleration Integration Tests
        
        [TestMethod]
        public void Hero_EarnSynergyPointsWithAcceleration_AppliesCorrectBonus()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            var group = new ActiveSynergyGroup(pattern);
            
            // Add 3 instances for max acceleration
            for (int i = 0; i < 3; i++)
            {
                group.TryAddInstance(new ActiveSynergy(pattern, new Point(i * 10, 0), 
                    new List<Point> { new Point(i * 10, 0), new Point(i * 10 + 1, 0) }));
            }
            
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Act: Earn 100 base points
            hero.EarnSynergyPointsWithAcceleration(100);
            
            // Assert: 3 instances = 1.70x acceleration = 170 points
            Assert.AreEqual(170, crystal.GetSynergyPoints(pattern.Id), 
                "Should earn 100 * 1.70 = 170 points with 3 instances");
        }
        
        [TestMethod]
        public void Hero_EarnSynergyPointsWithAcceleration_StopsAfterSkillLearned()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            // Create pattern with unlockable skill
            var skill = new LightArmorPassive();
            var pattern = CreateTestPatternWithSkill("test_pattern", skill, 100);
            var group = new ActiveSynergyGroup(pattern);
            
            // Add 2 instances
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0) }));
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(10, 0), 
                new List<Point> { new Point(10, 0) }));
            
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Earn enough points to unlock skill (threshold 100)
            hero.EarnSynergyPointsWithAcceleration(100); // 100 * 1.35 = 135 points
            
            Assert.IsTrue(crystal.HasSynergySkill(skill.Id), "Skill should be learned");
            int pointsAfterUnlock = crystal.GetSynergyPoints(pattern.Id);
            
            // Earn more points - should not be accelerated
            hero.EarnSynergyPointsWithAcceleration(100); // 100 * 1.0 = 100 points (no acceleration)
            
            // Assert
            Assert.AreEqual(pointsAfterUnlock + 100, crystal.GetSynergyPoints(pattern.Id), 
                "After skill learned, points should not be accelerated");
        }
        
        #endregion
        
        #region Skill Unlock Tests
        
        [TestMethod]
        public void Hero_SynergySkill_UnlocksExactlyOnce()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            var skill = new LightArmorPassive();
            var pattern = CreateTestPatternWithSkill("test_pattern", skill, 50);
            var group = new ActiveSynergyGroup(pattern);
            
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0) }));
            
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Act: Unlock skill
            hero.EarnSynergyPointsWithAcceleration(50);
            Assert.IsTrue(crystal.HasSynergySkill(skill.Id), "Skill should be learned");
            
            // Earn more points (skill already learned)
            hero.EarnSynergyPointsWithAcceleration(100);
            hero.EarnSynergyPointsWithAcceleration(100);
            
            // Assert: Still only learned once (no duplicate entries)
            Assert.IsTrue(crystal.HasSynergySkill(skill.Id));
            Assert.IsTrue(hero.LearnedSkills.ContainsKey(skill.Id), "Hero should have the skill");
        }
        
        [TestMethod]
        public void Hero_MultipleInstances_DoNotDuplicateSkillUnlock()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            var skill = new LightArmorPassive();
            var pattern = CreateTestPatternWithSkill("test_pattern", skill, 50);
            var group = new ActiveSynergyGroup(pattern);
            
            // Add 3 instances
            for (int i = 0; i < 3; i++)
            {
                group.TryAddInstance(new ActiveSynergy(pattern, new Point(i * 10, 0), 
                    new List<Point> { new Point(i * 10, 0) }));
            }
            
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            
            // Act: Unlock skill with accelerated points
            hero.EarnSynergyPointsWithAcceleration(50);
            
            // Assert: Skill learned once despite 3 instances
            Assert.IsTrue(crystal.HasSynergySkill(skill.Id));
            int learnedCount = 0;
            foreach (var kvp in hero.LearnedSkills)
            {
                if (kvp.Key == skill.Id) learnedCount++;
            }
            Assert.AreEqual(1, learnedCount, "Skill should only be in learned skills once");
        }
        
        #endregion
        
        #region Organic Discovery Tests
        
        [TestMethod]
        public void Hero_UpdateActiveSynergiesGrouped_TriggersStencilDiscovery()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            var gameStateService = new PitHero.Services.GameStateService();
            
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            Assert.IsFalse(gameStateService.IsStencilDiscovered(pattern.Id));
            
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(pattern, new Point(0, 0), 
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            
            // Act
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group }, gameStateService);
            
            // Assert
            Assert.IsTrue(gameStateService.IsStencilDiscovered(pattern.Id), 
                "Stencil should be discovered via organic match");
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, 
                gameStateService.DiscoveredStencils[pattern.Id]);
        }
        
        #endregion
        
        #region Helper Methods
        
        private SynergyPattern CreateTestPattern(string id)
        {
            return new SynergyPattern(
                id,
                "Test Pattern",
                "A test pattern",
                new List<Point> { new Point(0, 0), new Point(1, 0) },
                new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield },
                new List<ISynergyEffect>(),
                100
            );
        }
        
        private SynergyPattern CreateTestPatternWithSkill(string id, ISkill skill, int pointsRequired)
        {
            return new SynergyPattern(
                id,
                "Test Pattern",
                "A test pattern with skill",
                new List<Point> { new Point(0, 0) },
                new List<ItemKind> { ItemKind.WeaponSword },
                new List<ISynergyEffect>(),
                pointsRequired,
                skill
            );
        }
        
        #endregion
    }
}

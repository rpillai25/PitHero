using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class OrganicSynergyDiscoveryTests
    {
        [TestMethod]
        public void SwordShieldPattern_IsDetected_WhenItemsAreAdjacent()
        {
            // Arrange: Create detector and register sword & shield pattern
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            // Create 8x7 grid (matching InventoryGrid dimensions)
            var grid = new IItem[20, 8];
            
            // Place sword and shield horizontally adjacent
            grid[0, 3] = GearItems.ShortSword();      // Sword at (0,3)
            grid[1, 3] = GearItems.WoodenShield();    // Shield at (1,3) - adjacent to the right
            
            // Act: Detect synergies
            var synergies = detector.DetectSynergies(grid, 20, 8);
            
            // Assert: Pattern should be detected
            Assert.AreEqual(1, synergies.Count, "Should detect exactly one synergy");
            Assert.AreEqual("sword_shield_mastery", synergies[0].Pattern.Id);
            Assert.AreEqual(new Point(0, 3), synergies[0].AnchorSlot);
        }
        
        [TestMethod]
        public void SwordShieldPattern_IsNotDetected_WhenItemsAreNotAdjacent()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            var grid = new IItem[20, 8];
            
            // Place sword and shield with gap
            grid[0, 3] = GearItems.ShortSword();      // Sword at (0,3)
            grid[2, 3] = GearItems.WoodenShield();    // Shield at (2,3) - not adjacent
            
            // Act
            var synergies = detector.DetectSynergies(grid, 20, 8);
            
            // Assert: Pattern should NOT be detected
            Assert.AreEqual(0, synergies.Count, "Should not detect synergy when items are not adjacent");
        }
        
        [TestMethod]
        public void SwordShieldPattern_IsNotDetected_WhenWrongItemTypes()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            var grid = new IItem[20, 8];
            
            // Place sword and armor (not shield)
            grid[0, 3] = GearItems.ShortSword();      // Sword
            grid[1, 3] = GearItems.LeatherArmor();    // Armor, not shield
            
            // Act
            var synergies = detector.DetectSynergies(grid, 20, 8);
            
            // Assert: Pattern should NOT be detected
            Assert.AreEqual(0, synergies.Count, "Should not detect synergy with wrong item types");
        }
        
        [TestMethod]
        public void SwordShieldSynergy_AppliesDefenseBonus_WhenActive()
        {
            // Arrange: Create detector and hero
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            var crystal = new HeroCrystal("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            // Create grid with sword & shield pattern
            var grid = new IItem[20, 8];
            grid[0, 3] = GearItems.ShortSword();
            grid[1, 3] = GearItems.WoodenShield();
            
            // Act: Detect and apply synergies
            var synergies = detector.DetectSynergies(grid, 20, 8);
            hero.UpdateActiveSynergies(synergies);
            
            // Assert: Defense bonus should be applied
            Assert.AreEqual(1, hero.ActiveSynergies.Count);
            Assert.AreEqual(5, hero.PassiveDefenseBonus, "Should have +5 defense from synergy");
        }
        
        [TestMethod]
        public void SynergyDiscovery_MarksPatternAsDiscovered_InCrystal()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            var crystal = new HeroCrystal("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            // Verify synergy not discovered initially
            Assert.IsFalse(crystal.HasDiscoveredSynergy("sword_shield_mastery"));
            
            // Create grid with pattern
            var grid = new IItem[20, 8];
            grid[0, 3] = GearItems.ShortSword();
            grid[1, 3] = GearItems.WoodenShield();
            
            // Act: Detect and apply
            var synergies = detector.DetectSynergies(grid, 20, 8);
            hero.UpdateActiveSynergies(synergies);
            
            // Assert: Crystal should mark synergy as discovered
            Assert.IsTrue(crystal.HasDiscoveredSynergy("sword_shield_mastery"), 
                "Crystal should mark synergy as discovered after first match");
        }
        
        [TestMethod]
        public void StencilDiscovery_IsTriggered_WhenPatternMatched()
        {
            // Arrange
            var detector = new SynergyDetector();
            detector.RegisterPattern(ExampleSynergyPatterns.CreateSwordShieldMastery());
            
            var crystal = new HeroCrystal("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);
            
            // Create GameStateService to track stencil discovery
            var gameStateService = new PitHero.Services.GameStateService();
            Assert.IsFalse(gameStateService.IsStencilDiscovered("sword_shield_mastery"));
            
            // Create grid with pattern
            var grid = new IItem[20, 8];
            grid[0, 3] = GearItems.ShortSword();
            grid[1, 3] = GearItems.WoodenShield();
            
            // Act: Detect and apply with gameStateService
            var synergies = detector.DetectSynergies(grid, 20, 8);
            hero.UpdateActiveSynergies(synergies, gameStateService);
            
            // Assert: Stencil should be discovered
            Assert.IsTrue(gameStateService.IsStencilDiscovered("sword_shield_mastery"), 
                "Stencil should be discovered when pattern is organically matched");
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, 
                gameStateService.DiscoveredStencils["sword_shield_mastery"],
                "Stencil should be marked as discovered via PlayerMatch");
        }
    }
}

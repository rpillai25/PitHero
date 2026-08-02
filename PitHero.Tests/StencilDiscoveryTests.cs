using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Synergies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using PitHero.Services;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class StencilDiscoveryTests
    {
        [TestMethod]
        public void SynergyStencil_Constructor_InitializesCorrectly()
        {
            var stencil = new SynergyStencil("stencil_1", "pattern_1");
            
            Assert.AreEqual("stencil_1", stencil.Id);
            Assert.AreEqual("pattern_1", stencil.SynergyPatternId);
            Assert.IsTrue(stencil.IsPlaceable);
            Assert.IsNull(stencil.OverlayAnchor);
            Assert.AreEqual(StencilDiscoverySource.Unknown, stencil.DiscoverySource);
        }
        
        [TestMethod]
        public void SynergyStencil_MarkDiscovered_SetsSource()
        {
            var stencil = new SynergyStencil("stencil_1", "pattern_1");
            
            stencil.MarkDiscovered(StencilDiscoverySource.PlayerMatch);
            
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, stencil.DiscoverySource);
        }
        
        [TestMethod]
        public void SynergyStencil_MarkDiscovered_OnlyFirstSourceIsRecorded()
        {
            var stencil = new SynergyStencil("stencil_1", "pattern_1");
            
            stencil.MarkDiscovered(StencilDiscoverySource.PlayerMatch);
            stencil.MarkDiscovered(StencilDiscoverySource.LootReward);
            
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, stencil.DiscoverySource);
        }
        
        [TestMethod]
        public void SynergyStencil_OverlayAnchor_CanBeSet()
        {
            var stencil = new SynergyStencil("stencil_1", "pattern_1");
            
            stencil.OverlayAnchor = new Point(5, 3);
            
            Assert.IsTrue(stencil.OverlayAnchor.HasValue);
            Assert.AreEqual(new Point(5, 3), stencil.OverlayAnchor.Value);
        }
        
        [TestMethod]
        public void GameStateService_DiscoverStencil_AddsNewStencil()
        {
            var service = new GameStateService();
            
            service.DiscoverStencil("pattern_1", StencilDiscoverySource.PlayerMatch);
            
            Assert.IsTrue(service.IsStencilDiscovered("pattern_1"));
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, service.DiscoveredStencils["pattern_1"]);
        }
        
        [TestMethod]
        public void GameStateService_DiscoverStencil_DoesNotOverwriteExisting()
        {
            var service = new GameStateService();
            
            service.DiscoverStencil("pattern_1", StencilDiscoverySource.PlayerMatch);
            service.DiscoverStencil("pattern_1", StencilDiscoverySource.LootReward);
            
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, service.DiscoveredStencils["pattern_1"]);
        }
        
        [TestMethod]
        public void GameStateService_IsStencilDiscovered_ReturnsFalseForUndiscovered()
        {
            var service = new GameStateService();
            
            Assert.IsFalse(service.IsStencilDiscovered("pattern_1"));
        }
        
        [TestMethod]
        public void GameStateService_DiscoverStencil_TracksMultipleStencils()
        {
            var service = new GameStateService();
            
            service.DiscoverStencil("pattern_1", StencilDiscoverySource.PlayerMatch);
            service.DiscoverStencil("pattern_2", StencilDiscoverySource.LootReward);
            service.DiscoverStencil("pattern_3", StencilDiscoverySource.EventReward);
            
            Assert.IsTrue(service.IsStencilDiscovered("pattern_1"));
            Assert.IsTrue(service.IsStencilDiscovered("pattern_2"));
            Assert.IsTrue(service.IsStencilDiscovered("pattern_3"));
            Assert.AreEqual(3, service.DiscoveredStencils.Count);
        }
        
        [TestMethod]
        public void SynergyPattern_HasStencil_DefaultsToTrue()
        {
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            
            Assert.IsTrue(pattern.HasStencil);
        }
        
        [TestMethod]
        public void OrganicDiscovery_AddsStencilWhenPatternFirstMatched()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            var gameState = new GameStateService();
            
            // Create a simple pattern
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            var pattern = new SynergyPattern("test_pattern", "Test", "Test", offsets, kinds, effects, 100);
            
            // Create an active synergy
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            var synergy = new ActiveSynergy(pattern, anchor, slots);
            
            // Verify stencil not discovered yet
            Assert.IsFalse(gameState.IsStencilDiscovered(pattern.Id));
            
            // Update synergies with game state service
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy }, gameState);
            
            // Verify stencil was discovered
            Assert.IsTrue(gameState.IsStencilDiscovered(pattern.Id));
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, gameState.DiscoveredStencils[pattern.Id]);
        }
        
        [TestMethod]
        public void OrganicDiscovery_DoesNotDiscoverTwice()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            var gameState = new GameStateService();
            
            // Create a simple pattern
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            var pattern = new SynergyPattern("test_pattern", "Test", "Test", offsets, kinds, effects, 100);
            
            // Create an active synergy
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            var synergy = new ActiveSynergy(pattern, anchor, slots);
            
            // First discovery
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy }, gameState);
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch, gameState.DiscoveredStencils[pattern.Id]);
            
            // Manually change the discovery source to test
            gameState.DiscoveredStencils[pattern.Id] = StencilDiscoverySource.LootReward;
            
            // Second discovery attempt should not change source
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy }, gameState);
            Assert.AreEqual(StencilDiscoverySource.LootReward, gameState.DiscoveredStencils[pattern.Id]);
        }
        
        [TestMethod]
        public void OrganicDiscovery_OnlyDiscoversPatternsWithStencils()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            var gameState = new GameStateService();
            
            // Create a pattern without a stencil
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            var pattern = new SynergyPattern("test_pattern", "Test", "Test", offsets, kinds, effects, 100, hasStencil: false);
            
            // Create an active synergy
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            var synergy = new ActiveSynergy(pattern, anchor, slots);
            
            // Update synergies
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy }, gameState);
            
            // Verify stencil was NOT discovered (pattern has no stencil)
            Assert.IsFalse(gameState.IsStencilDiscovered(pattern.Id));
        }
        
        [TestMethod]
        public void OrganicDiscovery_WorksWithoutGameStateService()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            
            // Create a simple pattern
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            var pattern = new SynergyPattern("test_pattern", "Test", "Test", offsets, kinds, effects, 100);
            
            // Create an active synergy
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            var synergy = new ActiveSynergy(pattern, anchor, slots);
            
            // This should not throw when gameStateService is null
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy }, null);
            
            // Synergy should still be active
            Assert.AreEqual(1, hero.ActiveSynergies.Count);
        }

        /// <summary>
        /// DiscoverStencil with StencilDiscoverySource.LootReward records that source correctly.
        /// </summary>
        [TestMethod]
        public void GameStateService_DiscoverStencil_LootRewardSourceIsRecorded()
        {
            var service = new GameStateService();

            service.DiscoverStencil("knight.shield_mastery", StencilDiscoverySource.LootReward);

            Assert.IsTrue(service.IsStencilDiscovered("knight.shield_mastery"),
                "Stencil must be marked discovered after LootReward discovery");
            Assert.AreEqual(StencilDiscoverySource.LootReward,
                service.DiscoveredStencils["knight.shield_mastery"],
                "Discovery source must be LootReward");
        }

        /// <summary>
        /// LootReward discovery does not overwrite an earlier PlayerMatch source (first-source-wins rule).
        /// </summary>
        [TestMethod]
        public void GameStateService_DiscoverStencil_LootRewardDoesNotOverwritePlayerMatch()
        {
            var service = new GameStateService();

            service.DiscoverStencil("knight.shield_mastery", StencilDiscoverySource.PlayerMatch);
            service.DiscoverStencil("knight.shield_mastery", StencilDiscoverySource.LootReward);

            Assert.AreEqual(StencilDiscoverySource.PlayerMatch,
                service.DiscoveredStencils["knight.shield_mastery"],
                "First-source-wins: PlayerMatch recorded first must not be overwritten by LootReward");
        }

        /// <summary>
        /// PlayerMatch does not overwrite an earlier LootReward source.
        /// </summary>
        [TestMethod]
        public void GameStateService_DiscoverStencil_PlayerMatchDoesNotOverwriteLootReward()
        {
            var service = new GameStateService();

            service.DiscoverStencil("knight.shield_mastery", StencilDiscoverySource.LootReward);
            service.DiscoverStencil("knight.shield_mastery", StencilDiscoverySource.PlayerMatch);

            Assert.AreEqual(StencilDiscoverySource.LootReward,
                service.DiscoveredStencils["knight.shield_mastery"],
                "First-source-wins: LootReward recorded first must not be overwritten by PlayerMatch");
        }

        /// <summary>
        /// LootReward discovery round-trips through SaveData (DiscoveredStencils dict, section persisted pre-v27).
        /// </summary>
        [TestMethod]
        public void LootRewardDiscovery_RoundTripsThroughSaveData()
        {
            // SaveData.DiscoveredStencils stores the source as an int.
            // Verify the int encoding of LootReward survives a round-trip via the in-memory dict.
            var original = new SaveData();
            original.DiscoveredStencils["knight.shield_mastery"] = (int)StencilDiscoverySource.LootReward;
            original.DiscoveredStencils["knight.holy_strike"]    = (int)StencilDiscoverySource.PlayerMatch;

            // Simulate what SaveLoadService does on load: copy into GameStateService.
            var service = new GameStateService();
            var enumerator = original.DiscoveredStencils.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var src = (StencilDiscoverySource)enumerator.Current.Value;
                service.DiscoverStencil(enumerator.Current.Key, src);
            }
            enumerator.Dispose();

            Assert.AreEqual(StencilDiscoverySource.LootReward,
                service.DiscoveredStencils["knight.shield_mastery"],
                "LootReward source must survive the SaveData → GameStateService copy");
            Assert.AreEqual(StencilDiscoverySource.PlayerMatch,
                service.DiscoveredStencils["knight.holy_strike"],
                "PlayerMatch source must survive the SaveData → GameStateService copy");
        }
    }
}

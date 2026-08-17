using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    [TestClass]
    public class AdvancePriorityBugTests
    {
        private HeroComponent _heroComponent;

        [TestInitialize]
        public void TestInitialize()
        {
            _heroComponent = new HeroComponent();

            // Non-boss floor baseline: no living boss gates the orb
            // (in-game this is set during hero initialization and floor regeneration)
            _heroComponent.BossDefeated = true;
        }

        [TestMethod]
        public void HeroComponent_WhenPriority1IsAdvanceAndWizardOrbFound_ShouldSetExploredPitTrue()
        {
            // Arrange - Set Priority1 to Advance
            _heroComponent.Priority1 = HeroPitPriority.Advance;
            _heroComponent.Priority2 = HeroPitPriority.Battle; 
            _heroComponent.Priority3 = HeroPitPriority.Treasure;
            
            // Initially ExploredPit should be false
            _heroComponent.ExploredPit = false;
            
            // Wizard orb is found
            _heroComponent.FoundWizardOrb = true;
            
            // Act - Update explored pit based on priorities
            _heroComponent.UpdateExploredPitBasedOnPriorities();
            
            // Assert - ExploredPit should be true since Priority1 (Advance) is satisfied
            Assert.IsTrue(_heroComponent.ExploredPit, "ExploredPit should be true when Priority1=Advance and wizard orb is found");
        }
        
        [TestMethod]
        public void HeroComponent_WhenPriority2IsAdvanceAndWizardOrbFound_ShouldSetExploredPitTrue()
        {
            // Arrange - Set Priority2 to Advance, Priority1 to something unsatisfied
            _heroComponent.Priority1 = HeroPitPriority.Treasure; // This will be unsatisfied in test
            _heroComponent.Priority2 = HeroPitPriority.Advance;
            _heroComponent.Priority3 = HeroPitPriority.Battle;
            
            // Initially ExploredPit should be false
            _heroComponent.ExploredPit = false;
            
            // Wizard orb is found (satisfies Advance priority)
            _heroComponent.FoundWizardOrb = true;
            
            // Act - Update explored pit based on priorities
            _heroComponent.UpdateExploredPitBasedOnPriorities();
            
            // Assert - ExploredPit should NOT be true yet since Priority1 (Treasure) is not satisfied
            Assert.IsFalse(_heroComponent.ExploredPit, "ExploredPit should be false when Priority1 is not satisfied even if Advance is satisfied as Priority2");
        }
        
        [TestMethod]
        public void HeroComponent_WhenAdvanceIsCurrentPriorityAndWizardOrbFound_ShouldSetExploredPitTrue()
        {
            // Arrange - Set Priority1 to Advance (making it the current priority)
            _heroComponent.Priority1 = HeroPitPriority.Advance;
            _heroComponent.Priority2 = HeroPitPriority.Battle;
            _heroComponent.Priority3 = HeroPitPriority.Treasure;
            
            _heroComponent.ExploredPit = false;
            _heroComponent.FoundWizardOrb = true; // Satisfies Advance
            
            // Act
            _heroComponent.UpdateExploredPitBasedOnPriorities();
            
            // Assert - When current priority is Advance and it's satisfied, ExploredPit should be true immediately
            Assert.IsTrue(_heroComponent.ExploredPit, "When current priority is Advance and wizard orb is found, ExploredPit should be true");
        }

        [TestMethod]
        public void AdvancePriority_OrbFoundButBossAlive_ExploredPitStaysFalse()
        {
            // Arrange - boss floor: orb found but boss still alive
            _heroComponent.Priority1 = HeroPitPriority.Advance;
            _heroComponent.Priority2 = HeroPitPriority.Battle;
            _heroComponent.Priority3 = HeroPitPriority.Treasure;

            _heroComponent.ExploredPit = false;
            _heroComponent.FoundWizardOrb = true;
            _heroComponent.BossDefeated = false;

            // Act
            _heroComponent.UpdateExploredPitBasedOnPriorities();

            // Assert - the red orb alone must not satisfy Advance
            Assert.IsFalse(_heroComponent.ExploredPit, "ExploredPit should stay false while the boss is alive even if the wizard orb is found");
            Assert.IsFalse(_heroComponent.IsPrioritySatisfied(HeroPitPriority.Advance), "Advance priority should not be satisfied while the boss is alive");
        }

        [TestMethod]
        public void AdvancePriority_OrbFoundAndBossDefeated_ExploredPitBecomesTrue()
        {
            // Arrange - boss floor: orb found, boss initially alive
            _heroComponent.Priority1 = HeroPitPriority.Advance;
            _heroComponent.Priority2 = HeroPitPriority.Battle;
            _heroComponent.Priority3 = HeroPitPriority.Treasure;

            _heroComponent.ExploredPit = false;
            _heroComponent.FoundWizardOrb = true;
            _heroComponent.BossDefeated = false;
            _heroComponent.UpdateExploredPitBasedOnPriorities();
            Assert.IsFalse(_heroComponent.ExploredPit, "Sanity: ExploredPit should be false while boss is alive");

            // Act - boss is defeated (orb turns white)
            _heroComponent.BossDefeated = true;
            _heroComponent.UpdateExploredPitBasedOnPriorities();

            // Assert
            Assert.IsTrue(_heroComponent.ExploredPit, "ExploredPit should become true once the boss is defeated and the orb is found");
            Assert.IsTrue(_heroComponent.IsPrioritySatisfied(HeroPitPriority.Advance), "Advance priority should be satisfied once the boss is defeated");
        }
    }
}
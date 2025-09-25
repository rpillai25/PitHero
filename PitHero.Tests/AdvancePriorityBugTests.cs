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
    }
}
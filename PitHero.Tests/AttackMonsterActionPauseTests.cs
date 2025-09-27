using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Heroes;

namespace PitHero.Tests
{
    [TestClass]
    public class AttackMonsterActionPauseTests
    {
        private AttackMonsterAction _action = null!;
        private PauseService _pauseService = null!;
        private VirtualGoapContext _virtualContext = null!;

        [TestInitialize]
        public void Setup()
        {
            _action = new AttackMonsterAction();
            _pauseService = new PauseService();
            
            var virtualWorld = new VirtualWorldState();
            _virtualContext = new VirtualGoapContext(virtualWorld);
        }

        [TestMethod]
        public void AttackMonsterAction_VirtualExecuteNotAffectedByPause()
        {
            // Arrange
            _pauseService.IsPaused = true;
            
            // Act - Virtual execution should not be affected by pause
            bool result = _action.Execute(_virtualContext);
            
            // Assert
            Assert.IsTrue(result, "Virtual attack execution should complete regardless of pause state");
        }

        [TestMethod]
        public void AttackMonsterAction_HasCorrectBasicProperties()
        {
            // Assert
            Assert.AreEqual(GoapConstants.AttackMonster, _action.Name);
            Assert.AreEqual(3, _action.Cost);
            Assert.IsNotNull(_action, "AttackMonsterAction should be instantiable");
        }

        [TestMethod]
        public void AttackMonsterAction_InheritsFromHeroActionBase()
        {
            // Assert
            Assert.IsInstanceOfType(_action, typeof(HeroActionBase), "AttackMonsterAction should inherit from HeroActionBase");
        }

        [TestMethod]
        public void PauseService_CanBeToggled()
        {
            // Arrange
            Assert.IsFalse(_pauseService.IsPaused, "PauseService should start unpaused");
            
            // Act
            _pauseService.Toggle();
            
            // Assert
            Assert.IsTrue(_pauseService.IsPaused, "PauseService should be paused after toggle");
            
            // Act
            _pauseService.Toggle();
            
            // Assert
            Assert.IsFalse(_pauseService.IsPaused, "PauseService should be unpaused after second toggle");
        }
    }
}
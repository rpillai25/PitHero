using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.Services;

namespace PitHero.Tests
{
    [TestClass]
    public class PauseServiceTests
    {
        private PauseService _pauseService = null!;

        [TestInitialize]
        public void Setup()
        {
            _pauseService = new PauseService();
        }

        [TestMethod]
        public void PauseService_InitiallyNotPaused()
        {
            // Arrange & Act - service is created in Setup
            
            // Assert
            Assert.IsFalse(_pauseService.IsPaused);
        }

        [TestMethod]
        public void PauseService_PauseSetsTruе()
        {
            // Act
            _pauseService.Pause();
            
            // Assert
            Assert.IsTrue(_pauseService.IsPaused);
        }

        [TestMethod]
        public void PauseService_UnpauseSetsFalse()
        {
            // Arrange
            _pauseService.Pause();
            
            // Act
            _pauseService.Unpause();
            
            // Assert
            Assert.IsFalse(_pauseService.IsPaused);
        }

        [TestMethod]
        public void PauseService_ToggleChangesPauseState()
        {
            // Arrange
            var initialState = _pauseService.IsPaused;
            
            // Act
            _pauseService.Toggle();
            
            // Assert
            Assert.AreEqual(!initialState, _pauseService.IsPaused);
        }

        [TestMethod]
        public void PauseService_IsPausedPropertySetsState()
        {
            // Act
            _pauseService.IsPaused = true;
            
            // Assert
            Assert.IsTrue(_pauseService.IsPaused);
            
            // Act
            _pauseService.IsPaused = false;
            
            // Assert
            Assert.IsFalse(_pauseService.IsPaused);
        }
    }
}
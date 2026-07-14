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

        // ── Farm-mode pause gate ─────────────────────────────────────────────

        [TestMethod]
        public void FarmModePause_SetTrue_MakesIsPausedTrue_IndependentOfManualFlag()
        {
            // Arrange: manual flag is false
            Assert.IsFalse(_pauseService.IsPaused);

            // Act
            _pauseService.SetFarmModePause(true);

            // Assert: farm flag alone is sufficient to make the service report paused
            Assert.IsTrue(_pauseService.IsPaused);
        }

        [TestMethod]
        public void FarmModePause_BothFlagsOrTogether_IsPausedTrue()
        {
            // Arrange: turn on both flags
            _pauseService.IsPaused = true;
            _pauseService.SetFarmModePause(true);

            Assert.IsTrue(_pauseService.IsPaused, "Both flags set → IsPaused should be true");

            // Clearing only the manual flag still keeps IsPaused true via the farm flag
            _pauseService.IsPaused = false;
            Assert.IsTrue(_pauseService.IsPaused, "Farm flag alone keeps IsPaused true");
        }

        [TestMethod]
        public void FarmModePause_ToggleOnlyAffectsManualFlag_FarmGateStaysTrue()
        {
            // Arrange: farm pause on, manual flag off
            _pauseService.SetFarmModePause(true);
            Assert.IsTrue(_pauseService.IsPaused, "Precondition: paused via farm flag");

            // Act: Toggle should flip the manual flag from false → true, but IsPaused stays true regardless
            _pauseService.Toggle();

            // The service is still paused (both flags are now true)
            Assert.IsTrue(_pauseService.IsPaused, "After Toggle with farm flag on, IsPaused should still be true");

            // Act: Toggle again (manual flag → false), farm flag still keeps IsPaused true
            _pauseService.Toggle();
            Assert.IsTrue(_pauseService.IsPaused, "After second Toggle, farm flag alone keeps IsPaused true");
        }

        [TestMethod]
        public void FarmModePause_SetFalse_RestoresManualFlagControl()
        {
            // Arrange: farm pause on, manual flag off
            _pauseService.SetFarmModePause(true);
            Assert.IsTrue(_pauseService.IsPaused);

            // Act: release farm pause
            _pauseService.SetFarmModePause(false);

            // Now only the manual flag controls IsPaused (which is still false)
            Assert.IsFalse(_pauseService.IsPaused, "After farm flag cleared, manual flag (false) controls IsPaused");
        }

        [TestMethod]
        public void FarmModePause_IsPausedSetterOnlyAffectsManualFlag()
        {
            // Arrange: farm pause on
            _pauseService.SetFarmModePause(true);

            // Act: try to "unpause" via the setter
            _pauseService.IsPaused = false;

            // The farm flag is untouched, so IsPaused is still true
            Assert.IsTrue(_pauseService.IsPaused, "IsPaused setter does not clear the farm-mode flag");
        }

        [TestMethod]
        public void IsManuallyPaused_ReflectsOnlyManualFlag_NotFarmGate()
        {
            // Farm pause alone: IsPaused true, but not manually paused (camera stays interactive)
            _pauseService.SetFarmModePause(true);
            Assert.IsTrue(_pauseService.IsPaused);
            Assert.IsFalse(_pauseService.IsManuallyPaused, "Farm gate alone must not count as manual pause");

            // Manual pause on top: both report true
            _pauseService.IsPaused = true;
            Assert.IsTrue(_pauseService.IsManuallyPaused, "Manual flag set → manually paused");

            // Releasing the farm gate leaves the manual pause in effect
            _pauseService.SetFarmModePause(false);
            Assert.IsTrue(_pauseService.IsManuallyPaused);
            Assert.IsTrue(_pauseService.IsPaused);
        }
    }
}
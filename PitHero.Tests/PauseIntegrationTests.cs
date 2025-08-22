using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>
    /// Test component to verify pause functionality without using Core services
    /// </summary>
    public class MockPausableComponent : IPausableComponent
    {
        private readonly PauseService _pauseService;
        public bool ShouldPause => true;
        public bool UpdateCalled { get; private set; }

        public MockPausableComponent(PauseService pauseService)
        {
            _pauseService = pauseService;
        }

        public void Update()
        {
            // Check if game is paused (using injected service instead of Core.Services)
            if (_pauseService?.IsPaused == true)
                return;

            UpdateCalled = true;
        }

        public void ResetUpdateFlag() => UpdateCalled = false;
    }

    [TestClass]
    public class PauseIntegrationTests
    {
        private MockPausableComponent _component = null!;
        private PauseService _pauseService = null!;

        [TestInitialize]
        public void Setup()
        {
            _pauseService = new PauseService();
            _component = new MockPausableComponent(_pauseService);
        }

        [TestMethod]
        public void PausableComponent_UpdatesWhenNotPaused()
        {
            // Arrange
            _pauseService.IsPaused = false;
            _component.ResetUpdateFlag();

            // Act
            _component.Update();

            // Assert
            Assert.IsTrue(_component.UpdateCalled, "Component should update when not paused");
        }

        [TestMethod]
        public void PausableComponent_DoesNotUpdateWhenPaused()
        {
            // Arrange
            _pauseService.IsPaused = true;
            _component.ResetUpdateFlag();

            // Act
            _component.Update();

            // Assert
            Assert.IsFalse(_component.UpdateCalled, "Component should not update when paused");
        }

        [TestMethod]
        public void IPausableComponent_HasCorrectInterface()
        {
            // Assert that the component implements the interface correctly
            Assert.IsTrue(_component.ShouldPause, "Test component should respect pause state");
            Assert.IsInstanceOfType(_component, typeof(IPausableComponent));
        }
    }
}
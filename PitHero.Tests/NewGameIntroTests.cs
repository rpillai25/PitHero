using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>Pure-math coverage for the new-game intro drop (issue #396).</summary>
    [TestClass]
    public class NewGameIntroTests
    {
        [TestMethod]
        public void ComputeFallHeight_AtStart_ReturnsStartHeight()
        {
            Assert.AreEqual(290f, NewGameIntroService.ComputeFallHeight(290f, 0f), 0.0001f);
        }

        [TestMethod]
        public void ComputeFallHeight_AtEnd_ReturnsZero()
        {
            Assert.AreEqual(0f, NewGameIntroService.ComputeFallHeight(290f, 1f), 0.0001f);
        }

        [TestMethod]
        public void ComputeFallHeight_ClampsTimeOutsideUnitRange()
        {
            Assert.AreEqual(290f, NewGameIntroService.ComputeFallHeight(290f, -0.5f), 0.0001f);
            Assert.AreEqual(0f, NewGameIntroService.ComputeFallHeight(290f, 1.5f), 0.0001f);
        }

        [TestMethod]
        public void ComputeFallHeight_IsNonIncreasingAndGravityEased()
        {
            float previous = NewGameIntroService.ComputeFallHeight(100f, 0f);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                float height = NewGameIntroService.ComputeFallHeight(100f, t);
                Assert.IsTrue(height <= previous, $"height increased at t={t}");
                previous = height;
            }

            // Ease-in: the first half of the drop covers less distance than the second half
            float atHalf = NewGameIntroService.ComputeFallHeight(100f, 0.5f);
            Assert.IsTrue(100f - atHalf < atHalf, "expected a gravity-style ease-in");
        }

        [TestMethod]
        public void ComputeFallStartHeight_StatueTileAtDefaultZoom()
        {
            // Hero at world Y 208 (tile 6), visible top at 12 (camera Y 192, 360px render target, 1x zoom),
            // 46px composite sprite, 48px margin
            Assert.AreEqual(290f, NewGameIntroService.ComputeFallStartHeight(208f, 12f, 46f, 48f), 0.0001f);
        }

        [TestMethod]
        public void ComputeFallStartHeight_NeverBelowSpritePlusMargin()
        {
            // Hero already above the visible top edge: still drop at least sprite + margin
            Assert.AreEqual(94f, NewGameIntroService.ComputeFallStartHeight(0f, 400f, 46f, 48f), 0.0001f);
        }

        [TestMethod]
        public void ComputeVisibleWorldTop_DefaultZoom()
        {
            Assert.AreEqual(12f, CameraControllerComponent.ComputeVisibleWorldTop(192f, 360, 1f), 0.0001f);
        }

        [TestMethod]
        public void ComputeVisibleWorldTop_ZoomedOutShowsMoreWorldAbove()
        {
            Assert.AreEqual(-528f, CameraControllerComponent.ComputeVisibleWorldTop(192f, 360, 0.25f), 0.0001f);
        }

        [TestMethod]
        public void ComputeVisibleWorldTop_ZeroZoomFallsBackToOne()
        {
            Assert.AreEqual(12f, CameraControllerComponent.ComputeVisibleWorldTop(192f, 360, 0f), 0.0001f);
        }
    }
}

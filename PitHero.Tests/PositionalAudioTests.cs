using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class PositionalAudioTests
    {
        // Fixture: camera spans world X [100, 500]; max audible distance = 10 tiles * 32px = 320px
        private const float Left = 100f;
        private const float Right = 500f;
        private const float MaxPx = 320f;
        private const float Delta = 0.0001f;

        #region CalculateVolumeScale Tests

        [TestMethod]
        public void VolumeScale_SourceAtCameraCenter_ReturnsFullVolume()
        {
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(300f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceExactlyAtEdges_ReturnsFullVolume()
        {
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(Left, Left, Right, MaxPx), Delta);
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(Right, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceJustInsideEdges_ReturnsFullVolume()
        {
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(Left + 1f, Left, Right, MaxPx), Delta);
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(Right - 1f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceHalfMaxDistancePastLeftEdge_ReturnsHalfVolume()
        {
            Assert.AreEqual(0.5f, PositionalAudio.CalculateVolumeScale(Left - 160f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceHalfMaxDistancePastRightEdge_ReturnsHalfVolume()
        {
            Assert.AreEqual(0.5f, PositionalAudio.CalculateVolumeScale(Right + 160f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_FalloffIsLinear()
        {
            Assert.AreEqual(0.75f, PositionalAudio.CalculateVolumeScale(Left - 80f, Left, Right, MaxPx), Delta);
            Assert.AreEqual(0.25f, PositionalAudio.CalculateVolumeScale(Right + 240f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceExactlyAtMaxDistance_ReturnsZero()
        {
            Assert.AreEqual(0f, PositionalAudio.CalculateVolumeScale(Left - MaxPx, Left, Right, MaxPx), Delta);
            Assert.AreEqual(0f, PositionalAudio.CalculateVolumeScale(Right + MaxPx, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceBeyondMaxDistance_ReturnsZero()
        {
            Assert.AreEqual(0f, PositionalAudio.CalculateVolumeScale(Left - 1000f, Left, Right, MaxPx), Delta);
            Assert.AreEqual(0f, PositionalAudio.CalculateVolumeScale(Right + 1000f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void VolumeScale_SourceJustInsideMaxDistance_ReturnsSmallPositive()
        {
            float scale = PositionalAudio.CalculateVolumeScale(Left - 319f, Left, Right, MaxPx);
            Assert.IsTrue(scale > 0f && scale < 0.01f, $"Expected small positive scale, got {scale}");
        }

        [TestMethod]
        public void VolumeScale_ZeroMaxDistance_FullInsideZeroOutside()
        {
            Assert.AreEqual(1f, PositionalAudio.CalculateVolumeScale(300f, Left, Right, 0f), Delta);
            Assert.AreEqual(0f, PositionalAudio.CalculateVolumeScale(Left - 1f, Left, Right, 0f), Delta);
        }

        #endregion

        #region CalculatePan Tests

        [TestMethod]
        public void Pan_SourceOnScreen_ReturnsCenter()
        {
            Assert.AreEqual(0f, PositionalAudio.CalculatePan(300f, Left, Right, MaxPx), Delta);
            Assert.AreEqual(0f, PositionalAudio.CalculatePan(Left, Left, Right, MaxPx), Delta);
            Assert.AreEqual(0f, PositionalAudio.CalculatePan(Right, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void Pan_SourceHalfMaxDistancePastLeftEdge_ReturnsHalfLeft()
        {
            Assert.AreEqual(-0.5f, PositionalAudio.CalculatePan(Left - 160f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void Pan_SourceHalfMaxDistancePastRightEdge_ReturnsHalfRight()
        {
            Assert.AreEqual(0.5f, PositionalAudio.CalculatePan(Right + 160f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void Pan_SourceAtOrBeyondMaxDistance_ClampsToFullPan()
        {
            Assert.AreEqual(-1f, PositionalAudio.CalculatePan(Left - MaxPx, Left, Right, MaxPx), Delta);
            Assert.AreEqual(1f, PositionalAudio.CalculatePan(Right + MaxPx, Left, Right, MaxPx), Delta);
            Assert.AreEqual(-1f, PositionalAudio.CalculatePan(Left - 1000f, Left, Right, MaxPx), Delta);
            Assert.AreEqual(1f, PositionalAudio.CalculatePan(Right + 1000f, Left, Right, MaxPx), Delta);
        }

        [TestMethod]
        public void Pan_ZeroMaxDistance_FullPanOutside()
        {
            Assert.AreEqual(-1f, PositionalAudio.CalculatePan(Left - 1f, Left, Right, 0f), Delta);
            Assert.AreEqual(1f, PositionalAudio.CalculatePan(Right + 1f, Left, Right, 0f), Delta);
        }

        #endregion

        [TestMethod]
        public void ConfiguredMaxAudibleDistance_Is10Tiles320Pixels()
        {
            Assert.AreEqual(320, GameConfig.MaxAudibleDistanceTiles * GameConfig.TileSize);
        }
    }
}

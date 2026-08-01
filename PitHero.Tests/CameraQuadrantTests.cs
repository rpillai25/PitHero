using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    [TestClass]
    public class CameraQuadrantTests
    {
        [TestMethod]
        public void GetQuadrantCenter_FullMapBounds_ReturnsScreenWidthCenters()
        {
            // 240x12 tile map at 32px = 7680x384; quadrants are exactly one 1920px screen wide
            var bounds = new Rectangle(0, 0, 7680, 384);

            Assert.AreEqual(new Vector2(960f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 0));
            Assert.AreEqual(new Vector2(2880f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 1));
            Assert.AreEqual(new Vector2(4800f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 2));
            Assert.AreEqual(new Vector2(6720f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 3));
        }

        [TestMethod]
        public void GetQuadrantCenter_FallbackBounds_ReturnsQuarterWidthCenters()
        {
            // Default bounds used when no tilemap is found
            var bounds = new Rectangle(0, 0, 1920, 384);

            Assert.AreEqual(new Vector2(240f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 0));
            Assert.AreEqual(new Vector2(720f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 1));
            Assert.AreEqual(new Vector2(1200f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 2));
            Assert.AreEqual(new Vector2(1680f, 192f), CameraControllerComponent.GetQuadrantCenter(bounds, 3));
        }

        [TestMethod]
        public void GetQuadrantCenter_NonZeroOrigin_RespectsBoundsOffset()
        {
            var bounds = new Rectangle(100, 50, 400, 200);

            Assert.AreEqual(new Vector2(150f, 150f), CameraControllerComponent.GetQuadrantCenter(bounds, 0));
            Assert.AreEqual(new Vector2(450f, 150f), CameraControllerComponent.GetQuadrantCenter(bounds, 3));
        }
    }
}

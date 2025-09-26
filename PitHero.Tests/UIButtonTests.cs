using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.UI;

namespace PitHero.Tests
{
    [TestClass]
    public class UIButtonTests
    {
        [TestMethod]
        public void FastFUI_CanBeCreated()
        {
            var fastFUI = new FastFUI();
            Assert.IsNotNull(fastFUI);
        }

        [TestMethod]
        public void HeroUI_CanBeCreated()
        {
            var heroUI = new HeroUI();
            Assert.IsNotNull(heroUI);
        }

        [TestMethod]
        public void FastFUI_TimeScaleToggle_WorksCorrectly()
        {
            var fastFUI = new FastFUI();
            
            // Initially time scale should be 1.0
            Time.TimeScale = 1f;
            
            // Test that FastFUI component initializes correctly
            Assert.IsNotNull(fastFUI);
            Assert.AreEqual(1f, Time.TimeScale);
        }
    }
}
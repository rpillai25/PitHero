using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PitHero.Tests
{
    [TestClass]
    public class RenderLayerConfigTests
    {
        /// <summary>
        /// #337: fog of war must render BEHIND actors, single-tile objects, and dropped items
        /// (higher layer value = drawn further back) so the party is never partially covered by
        /// adjacent fog. Entities under covered fog are hidden via FogHideableComponent instead.
        /// </summary>
        [TestMethod]
        public void FogOfWar_RendersBehindActorsAndPitObjects()
        {
            Assert.IsTrue(GameConfig.RenderLayerFogOfWar > GameConfig.RenderLayerActors,
                "Fog of war must draw behind actors (heroes, mercenaries, monsters)");
            Assert.IsTrue(GameConfig.RenderLayerFogOfWar > GameConfig.RenderLayerSingleTileObject,
                "Fog of war must draw behind single-tile objects (chests, walls)");
            Assert.IsTrue(GameConfig.RenderLayerFogOfWar > GameConfig.RenderLayerDroppedItems,
                "Fog of war must draw behind dropped items");
            Assert.IsTrue(GameConfig.RenderLayerFogOfWar < GameConfig.RenderLayerDetail,
                "Fog of war must still draw in front of the detail/base tilemap layers");
        }
    }
}

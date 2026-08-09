using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.AI;

namespace PitHero.Tests
{
    [TestClass]
    public class MercenaryPitEdgeOffsetTests
    {
        // Pit interior rows span PitRectY+1 .. PitRectY+PitRectHeight-2; a merc's jump lands at
        // (edgeX - 2, edgeY), so every offset rim tile must keep its row inside that span.
        private const int InteriorRowMin = GameConfig.PitRectY + 1;
        private const int InteriorRowMax = GameConfig.PitRectY + GameConfig.PitRectHeight - 2;

        [TestMethod]
        public void PartyPitEdgeTiles_AreDistinctPerMercAndFromHero()
        {
            const int edgeX = 13;
            var heroTile = new Point(edgeX, GameConfig.PitCenterTileY);
            var merc0Tile = WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, 0);
            var merc1Tile = WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, 1);

            Assert.AreNotEqual(heroTile, merc0Tile, "Merc 0 must not share the hero's pit-edge tile");
            Assert.AreNotEqual(heroTile, merc1Tile, "Merc 1 must not share the hero's pit-edge tile");
            Assert.AreNotEqual(merc0Tile, merc1Tile, "Mercs must not share a pit-edge tile");
        }

        [TestMethod]
        public void PartyPitEdgeTiles_StayOnEdgeColumn()
        {
            const int edgeX = 13;
            Assert.AreEqual(edgeX, WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, 0).X);
            Assert.AreEqual(edgeX, WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, 1).X);
        }

        [TestMethod]
        public void PartyPitEdgeTiles_KeepJumpLandingRowsInsidePitInterior()
        {
            const int edgeX = 13;
            for (int mercIndex = 0; mercIndex < 2; mercIndex++)
            {
                var tile = WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, mercIndex);
                Assert.IsTrue(tile.Y >= InteriorRowMin && tile.Y <= InteriorRowMax,
                    $"Merc {mercIndex} edge row {tile.Y} must be within interior rows {InteriorRowMin}..{InteriorRowMax}");
            }
        }

        [TestMethod]
        public void PartyPitEdgeTiles_AvoidBlockedRimRows()
        {
            // The map's rim column pattern (PitHero.tmx Collision layer, stamped at every pit
            // width by PitWidthManager) has collision tiles at rows 5 and 7 — only rows 4, 6
            // and 8 are open within the interior span. Offsets that target 5 or 7 dead-end the
            // merc's walk-to-edge plan and strand the party (issue #371 regression).
            const int edgeX = 13;
            for (int mercIndex = 0; mercIndex < 2; mercIndex++)
            {
                var tile = WalkToPitEdgeAction.CalculatePitEdgeTileForPartyIndex(edgeX, mercIndex);
                Assert.AreNotEqual(GameConfig.PitCenterTileY - 1, tile.Y,
                    $"Merc {mercIndex} must not target blocked rim row {GameConfig.PitCenterTileY - 1}");
                Assert.AreNotEqual(GameConfig.PitCenterTileY + 1, tile.Y,
                    $"Merc {mercIndex} must not target blocked rim row {GameConfig.PitCenterTileY + 1}");
            }
        }
    }
}

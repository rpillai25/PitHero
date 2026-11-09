using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Artifacts;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>The DigBot artifact (issue #452): catalog facts, shop gating, its route and its digs.</summary>
    [TestClass]
    public class DigBotTests
    {
        [TestMethod]
        public void Catalog_DigBotIsALocal100kArtifactAfterTheMap()
        {
            Assert.AreEqual(10, (int)ArtifactType.DigBot, "Artifact ordinals are saved: append only");
            Assert.IsTrue(ArtifactCatalog.IsLocal(ArtifactType.DigBot));
            Assert.AreEqual(100000, ArtifactCatalog.GetPrice(ArtifactType.DigBot));
            Assert.AreEqual("DigBot", ArtifactCatalog.GetSpriteName(ArtifactType.DigBot));
            Assert.AreEqual(UITextKey.ArtifactDigBotName, ArtifactCatalog.GetNameKey(ArtifactType.DigBot));
            Assert.AreEqual(UITextKey.ArtifactDigBotEffect, ArtifactCatalog.GetEffectKey(ArtifactType.DigBot));
            CollectionAssert.AreEqual(new[] { ArtifactType.ProspectorsMap }, ArtifactCatalog.GetPrerequisites(ArtifactType.DigBot));
            Assert.IsTrue(ArtifactCatalog.RequiresPlacedDiggingField(ArtifactType.DigBot));
            Assert.IsFalse(ArtifactCatalog.RequiresPlacedDiggingField(ArtifactType.ProspectorsMap));
        }

        [TestMethod]
        public void Shop_OffersTheDigBotOnlyWithTheMapAndAPlacedField()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_digbot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                service.AttachLocalStore(new GameStateService());
                bool fieldPlaced = false;
                service.DiggingFieldPlacedProbe = () => fieldPlaced;

                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.DigBot), "Needs the map");
                service.Grant(ArtifactType.ProspectorsMap);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.DigBot), "Needs a placed field");
                fieldPlaced = true;
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.DigBot));
                service.Grant(ArtifactType.DigBot);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.DigBot), "Owned artifacts leave the shop");
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void HasDigBot_NeedsTheBotAndTheMap()
        {
            var state = new GameStateService();
            Assert.IsFalse(LocalArtifactEffects.HasDigBot(null));
            state.GrantLocalArtifact(ArtifactType.DigBot);
            Assert.IsFalse(LocalArtifactEffects.HasDigBot(state), "A dig needs the map too");
            state.GrantLocalArtifact(ArtifactType.ProspectorsMap);
            Assert.IsTrue(LocalArtifactEffects.HasDigBot(state));
        }

        [TestMethod]
        public void Route_ZigzagsDownThenBackUp_OneTilePerStep()
        {
            Assert.AreEqual(48, DigBotComponent.LapLength, "Rows 0,1,2,3 down, then 2,1 back up");
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(0, 0), DigBotComponent.GetScanTile(0));
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(7, 0), DigBotComponent.GetScanTile(7));
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(7, 1), DigBotComponent.GetScanTile(8), "Drops down, then runs right to left");
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(0, 3), DigBotComponent.GetScanTile(31), "Reaches the bottom left");
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(0, 2), DigBotComponent.GetScanTile(32), "Climbs one row");
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(7, 2), DigBotComponent.GetScanTile(39), "...and runs right");
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(0, 1), DigBotComponent.GetScanTile(47), "Ends next to the top left");
            Assert.AreEqual(0, DigBotComponent.NextScanIndex(47), "...and steps up into it");

            // Every field tile is visited each lap, and every step (the wrap included) moves one tile
            var seen = new bool[DiggingFieldService.TileCount];
            for (int i = 0; i < DigBotComponent.LapLength; i++)
            {
                var tile = DigBotComponent.GetWorldTile(130, 6, i);
                int index = BuildingConfig.GetDiggingFieldTileIndex(130, 6, tile.X, tile.Y);
                Assert.IsTrue(index >= 0, "Inside the field");
                seen[index] = true;
                var here = DigBotComponent.GetScanTile(i);
                var next = DigBotComponent.GetScanTile(DigBotComponent.NextScanIndex(i));
                Assert.AreEqual(1, System.Math.Abs(next.X - here.X) + System.Math.Abs(next.Y - here.Y), "One tile per step at " + i);
            }
            for (int i = 0; i < seen.Length; i++)
                Assert.IsTrue(seen[i], "Tile " + i + " is visited");
        }

        [TestMethod]
        public void TryDig_RestsTheTile_AndRefusesWithoutTheMapOrWhileResting()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding { Type = BuildingType.DiggingField, TileX = 130, TileY = 6, UniqueId = 1 });
            float now = 0f;
            var field = new DiggingFieldService(buildings, () => now);
            var state = new GameStateService();

            Assert.IsFalse(field.TryDig(state, 126, 4, 0, "digbot_dig", out _), "No map, no dig");
            state.GrantLocalArtifact(ArtifactType.ProspectorsMap);
            Assert.IsFalse(field.TryDig(state, 125, 4, 0, "digbot_dig", out _), "Outside the field");

            Assert.IsTrue(field.TryDig(state, 126, 4, 0, "digbot_dig", out _));
            Assert.IsTrue(field.IsCooling(0));
            Assert.IsFalse(field.TryDig(state, 126, 4, 0, "digbot_dig", out _), "Resting tile");

            now = GameConfig.DiggingFieldCooldownSeconds;
            Assert.IsTrue(field.TryDig(state, 126, 4, 0, "digbot_dig", out _), "Ready again");
        }
    }
}

using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Config;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Issue #473 — pit biomes: the PitHeroDungeons.tmx biome blocks, the biome wall tiles and tint,
    /// and the v47 → v48 save conversion of the 25-level loop to the 100-level cycle.
    /// </summary>
    [TestClass]
    public class PitBiomeTests
    {
        private static int[][] LoadBlocks()
        {
            using (var stream = File.OpenRead(FindContentFile("PitHeroDungeons.tmx")))
                return PitBiomeTileSet.ParseBlocks(XDocument.Load(stream));
        }

        private static string FindContentFile(string name)
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "PitHero", "Content", "Tilemaps", name);
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("Could not locate " + name + " above " + AppContext.BaseDirectory);
        }

        [TestMethod]
        public void DungeonMap_HasThreeFullBlocks()
        {
            var blocks = LoadBlocks();
            Assert.AreEqual(PitBiomeTileSet.SourceBlockCount, blocks.Length);
            for (int b = 0; b < blocks.Length; b++)
            {
                Assert.AreEqual(PitBiomeTileSet.BlockWidth * PitBiomeTileSet.BlockHeight, blocks[b].Length);
                for (int i = 0; i < blocks[b].Length; i++)
                    Assert.AreNotEqual(0, blocks[b][i], $"block {b} cell {i} is empty — every pit tile must be stamped");
            }
        }

        [TestMethod]
        public void CastleBlock_MatchesTheAuthoredPitInPitHeroTmx()
        {
            // PitHero.tmx was authored with the castle pit at (0,1); stamping the Castle block must be a no-op there
            var castle = LoadBlocks()[PitBiomeTileSet.GetSourceBlockIndex(PitBiome.Castle)];
            var map = XDocument.Load(FindContentFile("PitHero.tmx"));
            XElement baseLayer = null;
            foreach (var layer in map.Root.Elements("layer"))
                if ((string)layer.Attribute("name") == "Base") baseLayer = layer;
            Assert.IsNotNull(baseLayer);
            int width = (int)baseLayer.Attribute("width");
            var cells = ((string)baseLayer.Element("data")).Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int y = 0; y < PitBiomeTileSet.BlockHeight; y++)
                for (int x = 0; x < PitBiomeTileSet.BlockWidth; x++)
                {
                    int mapGid = int.Parse(cells[(y + PitBiomeTileSet.StampTileY) * width + x + PitBiomeTileSet.StampTileX]);
                    Assert.AreEqual(mapGid, castle[y * PitBiomeTileSet.BlockWidth + x], $"tile ({x},{y + 1})");
                }
        }

        [TestMethod]
        public void PitEntryTile_IsOpenFloorInEveryBlock()
        {
            // Row 6 (block row 5) column 13 is the single pit entry tile; each biome draws its own opening
            var blocks = LoadBlocks();
            int entry = (GameConfig.PitCenterTileY - PitBiomeTileSet.StampTileY) * PitBiomeTileSet.BlockWidth + 13;
            int rimAbove = entry - PitBiomeTileSet.BlockWidth;
            for (int b = 0; b < blocks.Length; b++)
                Assert.AreNotEqual(blocks[b][rimAbove], blocks[b][entry], $"block {b}: entry tile must differ from the rim above it");
        }

        [TestMethod]
        public void UnderworldReusesCave_TintedPurple()
        {
            Assert.AreEqual(PitBiomeTileSet.GetSourceBlockIndex(PitBiome.Cave), PitBiomeTileSet.GetSourceBlockIndex(PitBiome.Underworld));
            Assert.AreEqual(PitBiomeTileSet.GetWallTileId(PitBiome.Cave), PitBiomeTileSet.GetWallTileId(PitBiome.Underworld));
            Assert.AreNotEqual(Microsoft.Xna.Framework.Color.White, PitBiomeTileSet.GetTint(PitBiome.Underworld));
            Assert.AreEqual(Microsoft.Xna.Framework.Color.White, PitBiomeTileSet.GetTint(PitBiome.Cave));
            Assert.AreEqual(Microsoft.Xna.Framework.Color.White, PitBiomeTileSet.GetTint(PitBiome.Forest));
            Assert.AreEqual(Microsoft.Xna.Framework.Color.White, PitBiomeTileSet.GetTint(PitBiome.Castle));
        }

        [TestMethod]
        public void WallTiles_MatchTheIssue()
        {
            Assert.AreEqual(191, PitBiomeTileSet.GetWallTileId(PitBiome.Cave));
            Assert.AreEqual(211, PitBiomeTileSet.GetWallTileId(PitBiome.Forest));
            Assert.AreEqual(34, PitBiomeTileSet.GetWallTileId(PitBiome.Castle));
        }

        // ── Save conversion ───────────────────────────────────────────────────────

        private static SaveData ReadBytes(byte[] bytes)
        {
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }

        private static byte[] WriteBytes(SaveData data)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(data);
            return ms.ToArray();
        }

        [TestMethod]
        public void SaveV47_PitPosition_ConvertsByEffectiveDepth()
        {
            var bytes = WriteBytes(new SaveData { PitLevel = 3, PitTier = 2, TierBaseLevel = 14 });
            bytes[0] = 47; // v48 added no bytes, only the meaning of (PitLevel, PitTier)
            var loaded = ReadBytes(bytes);
            Assert.AreEqual(28, loaded.PitLevel, "legacy tier 2 level 3 is depth 28: Forest level 28");
            Assert.AreEqual(1, loaded.PitTier);
            Assert.AreEqual(14, loaded.TierBaseLevel);

            bytes = WriteBytes(new SaveData { PitLevel = 2, PitTier = 5 });
            bytes[0] = 47;
            loaded = ReadBytes(bytes);
            Assert.AreEqual(2, loaded.PitLevel, "legacy tier 5 level 2 is depth 102: tier 2 level 2");
            Assert.AreEqual(2, loaded.PitTier);
        }

        [TestMethod]
        public void SaveV48_PitPosition_RoundTripsUnchanged()
        {
            var loaded = ReadBytes(WriteBytes(new SaveData { PitLevel = 77, PitTier = 3 }));
            Assert.AreEqual(77, loaded.PitLevel);
            Assert.AreEqual(3, loaded.PitTier);
        }
    }
}

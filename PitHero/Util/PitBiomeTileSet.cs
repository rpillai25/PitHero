using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using Nez.Tiled;
using PitHero.Config;

namespace PitHero.Util
{
    /// <summary>
    /// Pit biome tiles (issue #473). <c>PitHeroDungeons.tmx</c> holds one 14x11 block of Base tiles per
    /// biome, side by side: Cave, Forest, Castle. The Underworld reuses the Cave block, tinted purple
    /// (<see cref="GetTint"/>). <see cref="PitWidthManager"/> stamps the block for the current biome into
    /// the main map's Base layer at tile (0,1), so pit expansion copies the biome's columns. The
    /// dungeon map shares the main map's tileset (firstgid 1), so its gids are written unchanged.
    ///
    /// The blocks are parsed straight from the TMX XML, without loading its tileset texture through the
    /// content manager, so loading them can never unload or replace the texture the main map draws with.
    /// The parsed blocks are immutable data and are cached for the process.
    /// </summary>
    public static class PitBiomeTileSet
    {
        /// <summary>The biome tile source map.</summary>
        public const string DungeonMapPath = "Content/Tilemaps/PitHeroDungeons.tmx";

        /// <summary>Width of one biome block in tiles (the pit's base area, x = 0..13).</summary>
        public const int BlockWidth = 14;

        /// <summary>Height of one biome block in tiles (y = 1..11 of the main map).</summary>
        public const int BlockHeight = 11;

        /// <summary>Main-map tile where a block's top-left tile is stamped.</summary>
        public const int StampTileX = 0, StampTileY = 1;

        /// <summary>Number of blocks the dungeon map holds (Cave, Forest, Castle).</summary>
        public const int SourceBlockCount = 3;

        // 0-based tileset ids of the random pit wall (obstacle) tile per biome, from the issue
        private const int CaveWallTileId = 191;
        private const int ForestWallTileId = 211;
        private const int CastleWallTileId = 34;

        /// <summary>Purple grade of the Underworld (multiplied over the Cave tiles and walls).</summary>
        public static readonly Color UnderworldTint = new Color(176, 112, 232);

        private static int[][] _blocks;
        private static bool _loadAttempted;
        private static readonly Dictionary<int, Sprite> _wallSprites = new Dictionary<int, Sprite>(4);

        /// <summary>Index of the dungeon-map block a biome stamps (the Underworld reuses the Cave block).</summary>
        public static int GetSourceBlockIndex(PitBiome biome)
        {
            switch (biome)
            {
                case PitBiome.Forest: return 1;
                case PitBiome.Castle: return 2;
                default: return 0; // Cave, Underworld
            }
        }

        /// <summary>0-based tileset id of the biome's pit wall (obstacle) tile.</summary>
        public static int GetWallTileId(PitBiome biome)
        {
            switch (biome)
            {
                case PitBiome.Forest: return ForestWallTileId;
                case PitBiome.Castle: return CastleWallTileId;
                default: return CaveWallTileId; // Cave, Underworld
            }
        }

        /// <summary>Color multiplied over the biome's tiles and walls: white except for the purple Underworld.</summary>
        public static Color GetTint(PitBiome biome) => biome == PitBiome.Underworld ? UnderworldTint : Color.White;

        /// <summary>
        /// The biome's block as Base-layer gids, row-major (<c>[y * BlockWidth + x]</c>, 0 = empty), or null
        /// when the dungeon map could not be read. Do not modify the returned array.
        /// </summary>
        public static int[] GetBaseBlock(PitBiome biome)
        {
            if (!_loadAttempted)
            {
                _loadAttempted = true;
                try
                {
                    using (var stream = TitleContainer.OpenStream(DungeonMapPath))
                        _blocks = ParseBlocks(XDocument.Load(stream));
                }
                catch (Exception ex)
                {
                    _blocks = null;
                    Nez.Debug.Error("[PitBiomeTileSet] Could not read " + DungeonMapPath + ": " + ex.Message);
                }
            }
            if (_blocks == null)
                return null;
            int index = GetSourceBlockIndex(biome);
            return index < _blocks.Length ? _blocks[index] : null;
        }

        /// <summary>
        /// Splits the dungeon map's Base layer (CSV) into its 14x11 biome blocks, left to right. Throws
        /// <see cref="InvalidDataException"/> when the layer is missing or too small for the three blocks.
        /// </summary>
        public static int[][] ParseBlocks(XDocument tmx)
        {
            XElement baseLayer = null;
            foreach (var layer in tmx.Root.Elements("layer"))
            {
                if ((string)layer.Attribute("name") == "Base")
                {
                    baseLayer = layer;
                    break;
                }
            }
            if (baseLayer == null)
                throw new InvalidDataException("no Base layer");

            int width = (int)baseLayer.Attribute("width");
            int height = (int)baseLayer.Attribute("height");
            if (width < BlockWidth * SourceBlockCount || height < BlockHeight)
                throw new InvalidDataException($"Base layer is {width}x{height}, needs {BlockWidth * SourceBlockCount}x{BlockHeight}");

            var data = baseLayer.Element("data");
            if (data == null || (string)data.Attribute("encoding") != "csv")
                throw new InvalidDataException("Base layer data must be CSV");

            var cells = ((string)data).Split(new[] { ',', '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length < width * height)
                throw new InvalidDataException($"Base layer has {cells.Length} cells, expected {width * height}");

            var blocks = new int[SourceBlockCount][];
            for (int b = 0; b < SourceBlockCount; b++)
            {
                var block = new int[BlockWidth * BlockHeight];
                for (int y = 0; y < BlockHeight; y++)
                {
                    for (int x = 0; x < BlockWidth; x++)
                    {
                        // Strip Tiled's flip flags (top 3 bits) — the biome blocks are never flipped
                        uint raw = uint.Parse(cells[y * width + b * BlockWidth + x]);
                        block[y * BlockWidth + x] = (int)(raw & 0x1FFFFFFF);
                    }
                }
                blocks[b] = block;
            }
            return blocks;
        }

        /// <summary>
        /// The biome's pit wall sprite, cut from the main map's tileset (centre origin like the old atlas
        /// wall). The tileset texture is loaded through the content manager, so it carries its asset name
        /// and the replay frame stream can record it. Null when the map or tile is unavailable.
        /// </summary>
        public static Sprite GetWallSprite(TmxMap map, PitBiome biome)
        {
            if (map?.Tilesets == null || map.Tilesets.Count == 0)
                return null;
            var tileset = map.Tilesets[0];
            var texture = tileset.Image?.Texture;
            if (texture == null || texture.IsDisposed)
                return null;

            int gid = tileset.FirstGid + GetWallTileId(biome);
            if (_wallSprites.TryGetValue(gid, out var cached) && ReferenceEquals(cached.Texture2D, texture))
                return cached;

            if (tileset.TileRegions == null || !tileset.TileRegions.TryGetValue(gid, out var region))
                return null;
            var sprite = new Sprite(texture, new Rectangle((int)region.X, (int)region.Y, (int)region.Width, (int)region.Height));
            _wallSprites[gid] = sprite;
            return sprite;
        }
    }
}

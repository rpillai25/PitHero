using Microsoft.Xna.Framework;
using Nez.Textures;
using PitHero.ECS.Components;
using PitHero.Rendering;

namespace PitHero.Tests
{
    /// <summary>Pins the baked character frame layout (issue #459) and the headless fallback contract.</summary>
    [TestClass]
    public class CharacterFrameBakeTests
    {
        [TestMethod]
        public void Cell_MatchesCompositeSize()
        {
            Assert.AreEqual(MultiSpriteAnimator.RT_WIDTH, CharacterFrameAtlas.CellWidth);
            Assert.AreEqual(MultiSpriteAnimator.RT_HEIGHT, CharacterFrameAtlas.CellHeight);
        }

        [TestMethod]
        public void Page_HoldsAFullPartyAndTavernCrowd()
        {
            // About 45 poses a character (Down, Right, Left, Up, Jump, sleep); a page should hold at least 12
            Assert.IsTrue(CharacterFrameAtlas.CellsPerPage >= 12 * 45, "cells per page: " + CharacterFrameAtlas.CellsPerPage);
        }

        [TestMethod]
        public void Resolve_WithoutGraphicsDevice_ReturnsNullSoCallerFallsBack()
        {
            var cache = new CharacterPoseCache(new Vector2(16f, 39f));
            var sprites = new Sprite[1];
            var offsets = new Vector2[1];
            var flips = new bool[1];
            var colors = new[] { Color.White };

            Assert.IsNull(cache.Resolve(null, null, sprites, offsets, flips, colors, 0, 1));
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void Resolve_TooManyLayers_ReturnsNull()
        {
            int n = CharacterPoseCache.MaxLayers + 1;
            var cache = new CharacterPoseCache(new Vector2(16f, 39f));
            Assert.IsNull(cache.Resolve(null, null, new Sprite[n], new Vector2[n], new bool[n], new Color[n], 0, n));
            Assert.IsNull(cache.Resolve(null, null, new Sprite[1], new Vector2[1], new bool[1], new Color[1], 0, 0));
        }
    }
}

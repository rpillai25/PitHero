using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>The Fishing Pond building: a free, unique, Move-only ground building like the Digging Field.</summary>
    [TestClass]
    public class FishingPondTests
    {
        [TestMethod]
        public void Config_PondIsAFreeUniqueMoveOnlyEightBySix()
        {
            Assert.AreEqual(3, (int)BuildingType.FishingPond, "Building ordinals are saved: append only");
            Assert.AreEqual(48, BuildingConfig.GetFootprint(BuildingType.FishingPond).Length);
            var b = BuildingConfig.GetFootprintBounds(BuildingType.FishingPond);
            Assert.AreEqual(8, b.dxMax - b.dxMin + 1);
            Assert.AreEqual(6, b.dyMax - b.dyMin + 1);
            Assert.AreEqual(0, BuildingConfig.GetCost(BuildingType.FishingPond, 0));
            Assert.AreEqual(1, BuildingConfig.GetMaxCount(BuildingType.FishingPond));
            Assert.IsFalse(BuildingConfig.IsSellable(BuildingType.FishingPond));
            Assert.IsTrue(BuildingConfig.IsGroundBuilding(BuildingType.FishingPond));
            Assert.IsFalse(BuildingConfig.IsWalkable(BuildingType.FishingPond), "water: nobody walks across it");
            Assert.IsTrue(BuildingConfig.BlocksWholeFootprint(BuildingType.FishingPond));
            Assert.AreEqual("FishingPond_1", BuildingConfig.GetSpriteName(BuildingType.FishingPond));
            Assert.AreEqual(UITextKey.BuildingNameFishingPond, BuildingConfig.GetDisplayNameKey(BuildingType.FishingPond));
            Assert.AreEqual(UITextKey.BuildingDescFishingPond, BuildingConfig.GetDescriptionKey(BuildingType.FishingPond));

            // The Digging Field is still a ground building; the others are untouched
            Assert.IsTrue(BuildingConfig.IsGroundBuilding(BuildingType.DiggingField));
            Assert.IsFalse(BuildingConfig.BlocksWholeFootprint(BuildingType.DiggingField));
            Assert.IsFalse(BuildingConfig.IsGroundBuilding(BuildingType.MonsterHouse));
            Assert.IsFalse(BuildingConfig.BlocksWholeFootprint(BuildingType.CropStorage));
            Assert.AreEqual(int.MaxValue, BuildingConfig.GetMaxCount(BuildingType.MonsterHouse));
        }

        [TestMethod]
        public void Config_SpriteSitsCenteredOnWholePixels()
        {
            // Entity at the footprint's top-left corner (anchor 130,6 -> columns 126..133, rows 3..8)
            var pos = BuildingConfig.GetWorldPos(130, 6, BuildingType.FishingPond);
            Assert.AreEqual(126 * 32f, pos.X);
            Assert.AreEqual(3 * 32f, pos.Y);

            // The 233x192 sprite is drawn centered on entity + offset; its edges must land on whole pixels
            var offset = BuildingConfig.GetSpriteOffset(BuildingType.FishingPond);
            float left = offset.X - BuildingConfig.FishingPondSpriteWidth / 2f;
            float top = offset.Y - BuildingConfig.FishingPondSpriteHeight / 2f;
            Assert.AreEqual(11f, left, "11 px in from the footprint's left edge (256 - 233 = 23)");
            Assert.AreEqual(0f, top, "192 px fills the 6-tile height exactly");
            Assert.IsTrue(left + BuildingConfig.FishingPondSpriteWidth <= BuildingConfig.FishingPondWidth * 32);
        }
    }
}

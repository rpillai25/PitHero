using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>The Digging Field building (issue #452): its config facts and the per-tile dig cooldowns.</summary>
    [TestClass]
    public class DiggingFieldTests
    {
        [TestMethod]
        public void Config_FieldIsAFreeUniqueWalkableEightByFour()
        {
            Assert.AreEqual(2, (int)BuildingType.DiggingField, "Building ordinals are saved: append only");
            Assert.AreEqual(32, BuildingConfig.GetFootprint(BuildingType.DiggingField).Length);
            var b = BuildingConfig.GetFootprintBounds(BuildingType.DiggingField);
            Assert.AreEqual(8, b.dxMax - b.dxMin + 1);
            Assert.AreEqual(4, b.dyMax - b.dyMin + 1);
            Assert.AreEqual(0, BuildingConfig.GetCost(BuildingType.DiggingField, 0));
            Assert.AreEqual(1, BuildingConfig.GetMaxCount(BuildingType.DiggingField));
            Assert.IsFalse(BuildingConfig.IsSellable(BuildingType.DiggingField));
            Assert.IsTrue(BuildingConfig.IsWalkable(BuildingType.DiggingField));
            Assert.AreEqual("DiggingField", BuildingConfig.GetSpriteName(BuildingType.DiggingField));
            Assert.AreEqual(UITextKey.BuildingNameDiggingField, BuildingConfig.GetDisplayNameKey(BuildingType.DiggingField));

            // The others keep their old behaviour
            Assert.IsTrue(BuildingConfig.IsSellable(BuildingType.MonsterHouse));
            Assert.IsFalse(BuildingConfig.IsWalkable(BuildingType.CropStorage));
            Assert.AreEqual(int.MaxValue, BuildingConfig.GetMaxCount(BuildingType.CropStorage));
        }

        [TestMethod]
        public void Config_SpriteCoversTheFootprint()
        {
            // Entity at the footprint's top-left corner; the centered 256x128 sprite is offset by half its size
            var pos = BuildingConfig.GetWorldPos(130, 6, BuildingType.DiggingField);
            var b = BuildingConfig.GetFootprintBounds(BuildingType.DiggingField);
            var offset = BuildingConfig.GetSpriteOffset(BuildingType.DiggingField);
            Assert.AreEqual((130 + b.dxMin) * 32f, pos.X, "entity at the footprint's left edge");
            Assert.AreEqual((6 + b.dyMin) * 32f, pos.Y, "entity at the footprint's top edge");
            Assert.AreEqual(128f, offset.X, "sprite center half its 256 px width in");
            Assert.AreEqual(64f, offset.Y, "sprite center half its 128 px height down");
        }

        [TestMethod]
        public void TileIndex_IsRowMajorInsideTheFootprint()
        {
            // Anchor (130, 6): columns 126..133, rows 4..7
            Assert.AreEqual(0, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 126, 4));
            Assert.AreEqual(7, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 133, 4));
            Assert.AreEqual(8, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 126, 5));
            Assert.AreEqual(31, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 133, 7));
            Assert.AreEqual(-1, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 125, 4));
            Assert.AreEqual(-1, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 134, 7));
            Assert.AreEqual(-1, BuildingConfig.GetDiggingFieldTileIndex(130, 6, 126, 8));
        }

        [TestMethod]
        public void Service_FindsTheFieldAndItsTiles()
        {
            var buildings = new BuildingService();
            float now = 0f;
            var svc = new DiggingFieldService(buildings, () => now);
            Assert.IsNull(svc.GetField());
            Assert.AreEqual(-1, svc.GetTileIndex(126, 4), "No field placed");

            buildings.AddBuilding(new PlacedBuilding { Type = BuildingType.CropStorage, TileX = 140, TileY = 4, UniqueId = 1 });
            buildings.AddBuilding(new PlacedBuilding { Type = BuildingType.DiggingField, TileX = 130, TileY = 6, UniqueId = 2 });
            Assert.AreEqual(2, svc.GetField().UniqueId);
            Assert.AreEqual(9, svc.GetTileIndex(127, 5));
        }

        [TestMethod]
        public void Cooldown_LastsItsSecondsPerTile_ThenClears()
        {
            float now = 10f;
            var svc = new DiggingFieldService(new BuildingService(), () => now);
            Assert.IsFalse(svc.IsCooling(3));

            svc.StartCooldown(3);
            Assert.IsTrue(svc.IsCooling(3));
            Assert.IsFalse(svc.IsCooling(4), "Only the dug tile rests");
            Assert.AreEqual(1f, svc.GetCooldownFraction(3), 1e-5f);

            now += GameConfig.DiggingFieldCooldownSeconds / 2f;
            Assert.IsTrue(svc.IsCooling(3));
            Assert.AreEqual(0.5f, svc.GetCooldownFraction(3), 1e-4f);

            now = 10f + GameConfig.DiggingFieldCooldownSeconds;
            Assert.IsFalse(svc.IsCooling(3), "Ready again once the cooldown has passed");
            Assert.AreEqual(0f, svc.GetCooldownFraction(3));

            svc.StartCooldown(3);
            svc.Reset();
            Assert.IsFalse(svc.IsCooling(3));
            Assert.IsFalse(svc.IsCooling(-1));
            Assert.IsFalse(svc.IsCooling(DiggingFieldService.TileCount));
        }

        [TestMethod]
        public void GrowIn_StartsAtATenth_EasesSmoothlyToFullSize()
        {
            float start = PitHero.GameConfig.DiggingFieldGrowStartScale;
            Assert.AreEqual(0.1f, start, 1e-6f);
            Assert.AreEqual(start, PitHero.ECS.Components.BuildingGrowAnimator.ScaleAt(start, 0f), 1e-6f);
            Assert.AreEqual(1f, PitHero.ECS.Components.BuildingGrowAnimator.ScaleAt(start, 1f), 1e-6f);

            float previous = start;
            for (int i = 1; i <= 20; i++)
            {
                float s = PitHero.ECS.Components.BuildingGrowAnimator.ScaleAt(start, i / 20f);
                Assert.IsTrue(s >= previous, "the field never shrinks while growing in");
                Assert.IsTrue(s <= 1f, "the field never overshoots full size");
                previous = s;
            }
        }
    }
}

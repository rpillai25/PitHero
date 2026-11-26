using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>The wizard-orb warp choreography (issue #469): timeline, swirl curve and replay record.</summary>
    [TestClass]
    public class PitWarpServiceTests
    {
        [TestMethod]
        public void Timeline_HoldsAtRegenerationUntilRegenerated_ThenFinishes()
        {
            var warp = new PitWarpService();
            warp.Begin(null, null, null, oldRightEdge: 12, newRightEdge: 14, seed: 1);
            Assert.IsTrue(warp.IsActive);

            for (int i = 0; i < PitWarpService.RegenerationTick; i++)
            {
                Assert.IsFalse(warp.RegenerationDue, $"not due before tick {PitWarpService.RegenerationTick} (at {warp.Tick})");
                warp.Step();
            }
            Assert.IsTrue(warp.RegenerationDue);

            // The caller is late: the warp holds fully drained instead of running on
            for (int i = 0; i < 5; i++)
                warp.Step();
            Assert.AreEqual(PitWarpService.RegenerationTick, warp.Tick);
            Assert.AreEqual(1f, warp.Pull, 1e-5f, "fully drained while held");
            Assert.IsFalse(warp.IsFinished);

            warp.OnPitRegenerated();
            Assert.IsFalse(warp.RegenerationDue);
            int steps = 0;
            while (!warp.IsFinished && steps < 10000)
            {
                warp.Step();
                steps++;
            }
            Assert.AreEqual(PitWarpService.TotalTicks - PitWarpService.RegenerationTick, steps);

            warp.End();
            Assert.IsFalse(warp.IsActive);
            Assert.AreEqual(0f, warp.Twist);
            Assert.AreEqual(0f, warp.Pull);
        }

        [TestMethod]
        public void Region_CoversEveryTileOfTheOldAndNewPits()
        {
            var warp = new PitWarpService();
            warp.Begin(null, null, null, oldRightEdge: 16, newRightEdge: 12, seed: 3);
            int ts = GameConfig.TileSize;
            Assert.AreEqual((GameConfig.PitRectX - 1) * ts, warp.RegionX, "outer left wall column included");
            Assert.AreEqual((GameConfig.PitRectY - 1) * ts, warp.RegionY, "outer top wall row included");
            Assert.AreEqual((16 + 1) * ts, warp.RegionX + warp.RegionWidth, "a shrinking pit still swirls the old, wider pit through its right edge column");
            Assert.AreEqual((GameConfig.PitRectY + GameConfig.PitRectHeight + 1) * ts, warp.RegionY + warp.RegionHeight, "outer bottom wall row included");

            warp.End();
            warp.Begin(null, null, null, oldRightEdge: 12, newRightEdge: 14, seed: 3);
            Assert.AreEqual((14 + 1) * ts, warp.RegionX + warp.RegionWidth, "a growing pit swirls the new, wider pit");
        }

        [TestMethod]
        public void SwirlCurve_StartsAndEndsUntouched_FullyDrainedAroundRegeneration()
        {
            Assert.AreEqual(0f, PitWarpService.TwistAt(0));
            Assert.AreEqual(0f, PitWarpService.PullAt(GameConfig.PitWarpRiseTicks));
            Assert.AreEqual(1f, PitWarpService.PullAt(PitWarpService.RegenerationTick), 1e-5f);
            Assert.AreEqual(1f, PitWarpService.PullAt(PitWarpService.RegenerationTick - 2), 1e-5f, "drained a little before the swap");
            Assert.AreEqual(1f, PitWarpService.PullAt(PitWarpService.RegenerationTick + 2), 1e-5f, "and a little after it");
            Assert.AreEqual(0f, PitWarpService.TwistAt(PitWarpService.GlideStartTick));
            Assert.AreEqual(0f, PitWarpService.PullAt(PitWarpService.TotalTicks));

            float previous = -1f;
            for (int t = GameConfig.PitWarpRiseTicks; t <= PitWarpService.RegenerationTick; t++)
            {
                float twist = PitWarpService.TwistAt(t);
                Assert.IsTrue(twist >= previous, $"twist winds up monotonically (tick {t})");
                previous = twist;
            }
        }

        [TestMethod]
        public void Float_RisesHoldsAndLands()
        {
            Assert.AreEqual(0f, PitWarpService.BaseHeightAt(0));
            Assert.AreEqual(GameConfig.PitWarpRiseHeightPx, PitWarpService.BaseHeightAt(GameConfig.PitWarpRiseTicks));
            Assert.AreEqual(GameConfig.PitWarpRiseHeightPx, PitWarpService.BaseHeightAt(PitWarpService.DropStartTick - 1));
            Assert.AreEqual(0f, PitWarpService.BaseHeightAt(PitWarpService.TotalTicks));
            Assert.AreEqual(1f, PitWarpService.CarryWeightAt(PitWarpService.GlideStartTick));
            Assert.AreEqual(0f, PitWarpService.CarryWeightAt(PitWarpService.DropStartTick));
            Assert.AreEqual(0f, PitWarpService.BobWeightAt(PitWarpService.TotalTicks));
        }

        [TestMethod]
        public void HudRecord_CarriesThePitWarp()
        {
            var hud = new HudRecord
            {
                PitWarpTwist = 3.5f,
                PitWarpPull = 0.75f,
                PitWarpSpin = 12f,
                PitWarpX = 64,
                PitWarpY = 96,
                PitWarpWidth = 352,
                PitWarpHeight = 224,
                OrbRingX = 240,
                OrbRingY = 200,
                OrbRingRadius = 180.5f,
                OrbRingAlpha = 0.6f,
            };
            var w = new FrameWriter(new byte[HudRecord.Size]);
            hud.Write(ref w);
            Assert.AreEqual(HudRecord.Size, w.Length);
            var r = new FrameReader(w.Buffer, 0, w.Length);
            var back = HudRecord.Read(ref r);
            Assert.AreEqual(hud, back);

            var changed = hud;
            changed.PitWarpPull = 0.5f;
            Assert.AreNotEqual(hud, changed, "a changed warp re-emits the record");
            changed = hud;
            changed.OrbRingRadius = 181f;
            Assert.AreNotEqual(hud, changed, "a spreading ring re-emits the record");
        }

        [TestMethod]
        public void OrbRing_FadesWhileSpreading_GoneAtThePitWidth_WithinTheWarp()
        {
            Assert.IsTrue(PitWarpService.RingTotalTicks < PitWarpService.RegenerationTick + GameConfig.PitWarpSwirlOutTicks,
                "the ring is done well before the party lands");
            Assert.AreEqual(0f, PitWarpService.RingRadiusAt(0, 500f));
            Assert.AreEqual(500f, PitWarpService.RingRadiusAt(GameConfig.PitWarpRingExpandTicks, 500f), 1e-3f);

            // Full brightness for the first half of the way out, then fading while it keeps moving
            float previousAlpha = 2f, previousRadius = -1f;
            bool sawFadingWhileMoving = false;
            for (int t = 0; t <= PitWarpService.RingTotalTicks; t++)
            {
                float radius = PitWarpService.RingRadiusAt(t, 500f);
                float alpha = PitWarpService.RingAlphaAt(t);
                if (radius <= 250f)
                    Assert.AreEqual(1f, alpha, $"bright until halfway out (tick {t})");
                Assert.IsTrue(alpha <= previousAlpha, $"fades monotonically (tick {t})");
                if (alpha > 0f && alpha < 1f && radius > previousRadius)
                    sawFadingWhileMoving = true;
                previousAlpha = alpha;
                previousRadius = radius;
            }
            Assert.IsTrue(sawFadingWhileMoving);
            Assert.AreEqual(0f, PitWarpService.RingAlphaAt(PitWarpService.RingTotalTicks), "gone once it reaches full size");

            var warp = new PitWarpService();
            warp.Begin(null, null, null, oldRightEdge: 12, newRightEdge: 14, seed: 1, orbPosition: new Vector2(208f, 176f));
            Assert.AreEqual(208, warp.RingX);
            Assert.AreEqual(176, warp.RingY);
            for (int i = 0; i < GameConfig.PitWarpRingExpandTicks - 1; i++)
                warp.Step();
            Assert.IsTrue(warp.RingRadius > warp.RegionWidth * 0.99f, "max radius is the width of the pit");
            Assert.IsTrue(warp.RingAlpha > 0f && warp.RingAlpha < 0.1f, "nearly faded as it arrives");
            warp.Step();
            Assert.AreEqual(0f, warp.RingAlpha);
            Assert.IsTrue(warp.IsActive, "the swirl carries on after the ring is gone");
            warp.End();

            warp.Begin(null, null, null, oldRightEdge: 12, newRightEdge: 14, seed: 1);
            warp.Step();
            Assert.AreEqual(0f, warp.RingAlpha, "no orb, no ring");
        }

        [TestMethod]
        public void OrbMotes_OnlyFromAShownOrbThatIsNotRed()
        {
            Assert.IsTrue(PitHero.ECS.Components.WizardOrbMotesComponent.ShouldEmit(true, Color.White), "uncovered blue orb");
            Assert.IsFalse(PitHero.ECS.Components.WizardOrbMotesComponent.ShouldEmit(false, Color.White), "still under the fog");
            Assert.IsFalse(PitHero.ECS.Components.WizardOrbMotesComponent.ShouldEmit(true, Color.Red), "boss alive, or already touched");
        }
    }
}

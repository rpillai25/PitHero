using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.Services;
using PitHero.Services.AutoJob;
using PitHero.Services.Replay.Frames;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests
{
    /// <summary>Monster fishing: the catch table, the marble bag, pond spots, job gating, auto demand and the Line frame op.</summary>
    [TestClass]
    public class FishingJobTests
    {
        [TestMethod]
        public void CatchTable_Is200Marbles_RarestFishTwo_JunkWorthNothing()
        {
            Assert.AreEqual(FishingConfig.CatchCount, System.Enum.GetValues(typeof(FishCatchType)).Length);
            int total = 0, fishMarbles = 0;
            for (int i = 0; i < FishingConfig.CatchCount; i++)
            {
                var type = (FishCatchType)i;
                total += FishingConfig.GetMarbles(type);
                if (!FishingConfig.IsJunk(type))
                    fishMarbles += FishingConfig.GetMarbles(type);
                else
                    Assert.AreEqual(0, FishingConfig.GetGold(type), type + " is junk");
            }
            Assert.AreEqual(200, total);
            Assert.AreEqual(161, fishMarbles);
            Assert.AreEqual(2, FishingConfig.GetMarbles(FishCatchType.AlligatorGar));
            Assert.AreEqual(1000, FishingConfig.GetGold(FishCatchType.AlligatorGar));
            Assert.AreEqual(50, FishingConfig.GetGold(FishCatchType.Minnow));
            Assert.AreEqual(9, (int)FishCatchType.AlligatorGar, "ordinals are logged: append only");
            Assert.AreEqual(12, (int)FishCatchType.JunkShoe);

            // Pricier fish are never more common than cheaper ones
            for (int i = 1; i < 10; i++)
            {
                Assert.IsTrue(FishingConfig.GetGold((FishCatchType)i) > FishingConfig.GetGold((FishCatchType)(i - 1)));
                Assert.IsTrue(FishingConfig.GetMarbles((FishCatchType)i) < FishingConfig.GetMarbles((FishCatchType)(i - 1)));
            }
        }

        [TestMethod]
        public void Bag_OneFullCycle_HasTheExactComposition()
        {
            var service = new FishingService();
            var counts = new int[FishingConfig.CatchCount];
            var rng = new System.Random(1234);
            for (int i = 0; i < FishingConfig.BagSize; i++)
                counts[(int)service.Draw((float)rng.NextDouble())]++;
            for (int i = 0; i < FishingConfig.CatchCount; i++)
                Assert.AreEqual(FishingConfig.GetMarbles((FishCatchType)i), counts[i], ((FishCatchType)i).ToString());
            Assert.AreEqual(0, service.MarblesRemaining);
            service.Draw(0.5f); // refills
            Assert.AreEqual(FishingConfig.BagSize - 1, service.MarblesRemaining);
        }

        [TestMethod]
        public void ResolveCatch_SellsFishOnTheSpot_JunkPaysNothing()
        {
            var state = new GameStateService();
            var service = new FishingService();
            var rng = new System.Random(99);
            int expected = 0;
            for (int i = 0; i < FishingConfig.BagSize; i++)
            {
                var type = service.ResolveCatch(state, null, 0, (float)rng.NextDouble());
                expected += FishingConfig.GetGold(type);
            }
            Assert.AreEqual(expected, state.Funds);
            Assert.AreEqual(24900, state.Funds, "one full bag is worth 24,900 G (124.5 G per catch)");
        }

        [TestMethod]
        public void Spots_SitJustOutsideTheFootprint_CenteredOnEachEdge()
        {
            // Anchor (130, 6): footprint columns 126..133, rows 3..8; pond centre (4160, 192)
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(125, 6), FishingPondSpots.GetStandTile(FishingPondSpots.Left, 130, 6));
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(134, 6), FishingPondSpots.GetStandTile(FishingPondSpots.Right, 130, 6));
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(130, 2), FishingPondSpots.GetStandTile(FishingPondSpots.Top, 130, 6));
            Assert.AreEqual(new Microsoft.Xna.Framework.Point(130, 9), FishingPondSpots.GetStandTile(FishingPondSpots.Bottom, 130, 6));

            float centerX = 130 * 32f, centerY = 6 * 32f;
            Assert.AreEqual(centerY, FishingPondSpots.GetSitPosition(FishingPondSpots.Left, 130, 6).Y);
            Assert.AreEqual(centerY, FishingPondSpots.GetSitPosition(FishingPondSpots.Right, 130, 6).Y);
            Assert.AreEqual(centerX, FishingPondSpots.GetSitPosition(FishingPondSpots.Top, 130, 6).X);
            Assert.AreEqual(centerX, FishingPondSpots.GetSitPosition(FishingPondSpots.Bottom, 130, 6).X);

            // The sitting point stays in its stand tile's column/row; the top fisher sits down on the top
            // rocks (the pond's first footprint row), every other one inside its stand tile.
            for (int spot = 0; spot < FishingPondSpots.Count; spot++)
            {
                var tile = FishingPondSpots.GetStandTile(spot, 130, 6);
                var sit = FishingPondSpots.GetSitPosition(spot, 130, 6);
                Assert.AreEqual(tile.X, (int)(sit.X / 32f), "spot " + spot);
                int expectedRow = spot == FishingPondSpots.Top ? tile.Y + 1 : tile.Y;
                Assert.AreEqual(expectedRow, (int)(sit.Y / 32f), "spot " + spot);

                // Each bobber rests in the pond's open water: sprite pixels x 40..200, y 60..165 of the
                // 233x192 art, i.e. (-77..83, -36..69) from the pond centre — clear of the rock ring
                var bobber = FishingPondSpots.GetBobberPosition(spot, 130, 6);
                float bx = bobber.X - centerX, by = bobber.Y - centerY;
                Assert.IsTrue(bx > -77f && bx < 83f, "bobber x in open water, spot " + spot);
                Assert.IsTrue(by > -36f && by < 69f, "bobber y in open water, spot " + spot);
            }

            // The top fisher sits closer to the water than its stand tile: on the top rocks
            Assert.IsTrue(FishingPondSpots.GetSitPosition(FishingPondSpots.Top, 130, 6).Y > (6 - 3) * 32f,
                "top fisher sits inside the pond's top row (on the rocks)");
            Assert.IsFalse(FishingPondSpots.FacesRight(FishingPondSpots.Right));
            Assert.IsTrue(FishingPondSpots.FacesRight(FishingPondSpots.Left));
        }

        [TestMethod]
        public void Rules_NeedAPond_AndCapEachShiftAtFour()
        {
            var roster = new List<AlliedMonster>();
            for (int i = 0; i < 4; i++)
                roster.Add(new AlliedMonster("Day" + i, "Slime", 1, 1, 1) { Job = MonsterJob.Fishing });
            var dayNewcomer = new AlliedMonster("Day4", "Slime", 1, 1, 1);
            var nightNewcomer = new AlliedMonster("Night0", "Orc", 1, 1, 1);
            roster.Add(dayNewcomer);
            roster.Add(nightNewcomer);

            Assert.AreEqual(FishingAssignResult.NeedsPond, FishingJobRules.CanAssign(roster, nightNewcomer, false));
            Assert.AreEqual(FishingAssignResult.CrewFull, FishingJobRules.CanAssign(roster, dayNewcomer, true));
            Assert.AreEqual(FishingAssignResult.Allowed, FishingJobRules.CanAssign(roster, nightNewcomer, true), "the night shift has its own 4");
            Assert.AreEqual(FishingAssignResult.Allowed, FishingJobRules.CanAssign(roster, roster[0], true), "a fisher may keep fishing");
            Assert.AreEqual(4, FishingJobRules.CountFishers(roster, false));
            Assert.AreEqual(0, FishingJobRules.CountFishers(roster, true));
        }

        [TestMethod]
        public void Demand_NoPondNoFishers_OtherwiseOnePerSpotCappedAtFour()
        {
            var none = FishingJobDemandEvaluator.ComputeDemand(false, 4, 10);
            Assert.AreEqual(0, none.DesiredWorkers);
            Assert.AreEqual(0, none.MinWorkers);

            var full = FishingJobDemandEvaluator.ComputeDemand(true, 4, 10);
            Assert.AreEqual(4, full.DesiredWorkers);
            Assert.AreEqual(0, full.MinWorkers, "fishers yield to farm and kitchen");
            Assert.IsTrue(full.Sticky);
            Assert.AreEqual(MonsterJob.Fishing, full.Job);

            Assert.AreEqual(2, FishingJobDemandEvaluator.ComputeDemand(true, 2, 10).DesiredWorkers, "blocked spots");
            Assert.AreEqual(1, FishingJobDemandEvaluator.ComputeDemand(true, 4, 1).DesiredWorkers, "few workers left");
            Assert.AreEqual(0, new FishingJobDemandEvaluator(null).EvaluateDemand(5, 5, false).DesiredWorkers);
        }

        [TestMethod]
        public void LineOp_RoundTrip_AndSkip()
        {
            var w = new FrameWriter(new byte[8]);
            w.WriteLine(10.4f, -3.6f, 300f, 41f, 2f, 0x8F563BFFu, 0.25f, 41);
            w.WriteRect(1f, 2f, 3f, 4f, 0x11223344u, FrameOpFlags.None);
            Assert.AreEqual(1 + FrameOpCode.LinePayload + 1 + FrameOpCode.RectPayload, w.Length);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.Line, r.ReadOpCode());
            r.ReadLine(out var op);
            Assert.AreEqual((short)10, op.X1);
            Assert.AreEqual((short)-4, op.Y1);
            Assert.AreEqual((short)300, op.X2);
            Assert.AreEqual((short)41, op.Y2);
            Assert.AreEqual((byte)2, op.Thickness);
            Assert.AreEqual(0x8F563BFFu, op.Color);
            Assert.AreEqual(0.25f, op.LayerDepth);
            Assert.AreEqual((short)41, op.RenderLayer);

            var skip = new FrameReader(w.Buffer, 0, w.Length);
            skip.SkipOp();
            Assert.AreEqual(FrameOpCode.Rect, skip.ReadOpCode());
        }
    }
}

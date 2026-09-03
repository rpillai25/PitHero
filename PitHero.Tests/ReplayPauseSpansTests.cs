using System.Collections.Generic;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>Pins how recorded pause stretches are derived from pause commands for playback skipping.</summary>
    [TestClass]
    public class ReplayPauseSpansTests
    {
        private static ReplayCommandRecord Pause(long tick, bool on)
            => new ReplayCommandRecord(tick, PlayerCommand.Flag(PlayerCommandType.SetManualPause, on));

        private static ReplayCommandRecord FarmPause(long tick, bool on)
            => new ReplayCommandRecord(tick, PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, on));

        [TestMethod]
        public void Build_PauseThenRelease_YieldsOneSpanAfterTheCommandTick()
        {
            var cmds = new List<ReplayCommandRecord> { Pause(100, true), Pause(700, false) };
            var spans = ReplayPauseSpans.Build(cmds, 5000, 120);
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(101, spans[0].Start);
            Assert.AreEqual(701, spans[0].End);
        }

        [TestMethod]
        public void Build_ShortPauses_AreIgnored()
        {
            var cmds = new List<ReplayCommandRecord> { Pause(100, true), Pause(150, false) };
            Assert.AreEqual(0, ReplayPauseSpans.Build(cmds, 5000, 120).Count);
        }

        [TestMethod]
        public void Build_OverlappingManualAndFarmPauses_MergeIntoOneSpan()
        {
            var cmds = new List<ReplayCommandRecord>
            {
                Pause(100, true), FarmPause(200, true), Pause(300, false), FarmPause(900, false)
            };
            var spans = ReplayPauseSpans.Build(cmds, 5000, 120);
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(101, spans[0].Start);
            Assert.AreEqual(901, spans[0].End);
        }

        [TestMethod]
        public void Build_UnreleasedPause_RunsToTheEnd()
        {
            var cmds = new List<ReplayCommandRecord> { Pause(100, true) };
            var spans = ReplayPauseSpans.Build(cmds, 5000, 120);
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(5000, spans[0].End);
        }

        [TestMethod]
        public void FindSkipTarget_InsideAndOutside()
        {
            var spans = ReplayPauseSpans.Build(new List<ReplayCommandRecord> { Pause(100, true), Pause(700, false) }, 5000, 120);
            Assert.AreEqual(701, ReplayPauseSpans.FindSkipTarget(spans, 101));
            Assert.AreEqual(701, ReplayPauseSpans.FindSkipTarget(spans, 400));
            Assert.AreEqual(-1, ReplayPauseSpans.FindSkipTarget(spans, 100));
            Assert.AreEqual(-1, ReplayPauseSpans.FindSkipTarget(spans, 701));
        }
    }
}

using System.Collections.Generic;
using PitHero.Services.Replay;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The frame viewer's playhead (issue #428): wall time × speed × 60 with fractional carry, reverse
    /// play, recorded pause spans skipped, clamping to the range, and the completed-ticks → frame mapping.
    /// </summary>
    [TestClass]
    public class ReplayFrameViewerTests
    {
        private const float Frame = 1f / 60f;

        [TestMethod]
        public void Play_AdvancesSixtyTicksPerSecondAtOneX()
        {
            var cursor = new ReplayFrameCursor(10_000);
            for (int i = 0; i < 60; i++)
                cursor.Advance(Frame, 1f, null);
            Assert.AreEqual(60, cursor.Cursor);
        }

        [TestMethod]
        public void Play_CarriesFractionsAcrossFrames()
        {
            // 2.5x at 60 Hz = 2.5 ticks per frame: two frames must give exactly 5 ticks, not 4
            var cursor = new ReplayFrameCursor(10_000);
            cursor.Advance(Frame, 2.5f, null);
            cursor.Advance(Frame, 2.5f, null);
            Assert.AreEqual(5, cursor.Cursor);
            // a long frame at 8x: 0.5 s = 240 ticks
            cursor.Advance(0.5f, 8f, null);
            Assert.AreEqual(245, cursor.Cursor);
        }

        [TestMethod]
        public void Reverse_MovesBackwardAndStopsAtZero()
        {
            var cursor = new ReplayFrameCursor(10_000);
            cursor.Seek(100);
            cursor.Direction = -1;
            Assert.IsFalse(cursor.Advance(1f, 1f, null)); // 60 back
            Assert.AreEqual(40, cursor.Cursor);
            Assert.IsTrue(cursor.Advance(1f, 1f, null));  // would pass zero: clamped, end reported
            Assert.AreEqual(0, cursor.Cursor);
        }

        [TestMethod]
        public void Play_ClampsAtMaxAndReportsTheEnd()
        {
            var cursor = new ReplayFrameCursor(100);
            cursor.Seek(90);
            Assert.IsTrue(cursor.Advance(1f, 1f, null));
            Assert.AreEqual(100, cursor.Cursor);
            // a smaller max pulls a seek back into range
            cursor.Max = 50;
            cursor.Seek(80);
            Assert.AreEqual(50, cursor.Cursor);
            cursor.Seek(-5);
            Assert.AreEqual(0, cursor.Cursor);
        }

        [TestMethod]
        public void Play_SkipsRecordedPauseSpansForwardOnly()
        {
            var spans = new List<ReplayPauseSpan> { new ReplayPauseSpan { Start = 100, End = 700 } };
            var cursor = new ReplayFrameCursor(10_000);
            cursor.Seek(98);
            cursor.Advance(Frame * 3, 1f, spans); // 98 + 3 = 101, inside the span: jump to its end
            Assert.AreEqual(700, cursor.Cursor);

            // reverse play walks back through the span tick by tick
            cursor.Direction = -1;
            cursor.Advance(Frame * 10, 1f, spans);
            Assert.AreEqual(690, cursor.Cursor);
        }

        [TestMethod]
        public void FrameTick_IsTheCompletedTickBeforeTheCursor()
        {
            var cursor = new ReplayFrameCursor(500);
            Assert.AreEqual(0, cursor.FrameTick); // before the first tick there is only frame 0 to show
            cursor.Seek(1);
            Assert.AreEqual(0, cursor.FrameTick);
            cursor.Seek(500);
            Assert.AreEqual(499, cursor.FrameTick);
        }

        [TestMethod]
        public void Seek_DropsTheFractionalCarry()
        {
            var cursor = new ReplayFrameCursor(10_000);
            cursor.Advance(Frame * 0.9f, 1f, null); // 0.9 of a tick pending
            cursor.Seek(10);
            cursor.Advance(Frame * 0.5f, 1f, null); // 0.5 alone must not complete a tick
            Assert.AreEqual(10, cursor.Cursor);
        }
    }
}

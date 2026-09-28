using Microsoft.Xna.Framework;
using PitHero.Services;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>The viewer's console feed (issue #428): tick-ordered lines with a binary-searched count and Time Travel truncation.</summary>
    [TestClass]
    public class RecordedConsoleLogTests
    {
        private static ConsoleSegment[] Line(string text) => new[] { new ConsoleSegment(text, Color.White) };

        [TestMethod]
        public void CountAtOrBefore_IsExactAtBoundaries()
        {
            var log = new RecordedConsoleLog();
            log.Add(10, Line("a"));
            log.Add(10, Line("b"));
            log.Add(25, Line("c"));
            log.Add(90, Line("d"));
            Assert.AreEqual(0, log.CountAtOrBefore(9));
            Assert.AreEqual(2, log.CountAtOrBefore(10));
            Assert.AreEqual(2, log.CountAtOrBefore(24));
            Assert.AreEqual(3, log.CountAtOrBefore(25));
            Assert.AreEqual(4, log.CountAtOrBefore(1000));
            Assert.AreEqual("c", log[2].Segments[0].Text);
        }

        [TestMethod]
        public void TruncateAfter_DropsLaterLinesOnly()
        {
            var log = new RecordedConsoleLog();
            for (int i = 0; i < 10; i++)
                log.Add(i * 5, Line(i.ToString()));
            log.TruncateAfter(22);
            Assert.AreEqual(5, log.Count); // ticks 0,5,10,15,20
            Assert.AreEqual(20, log[4].Tick);
            log.TruncateAfter(100);
            Assert.AreEqual(5, log.Count);
            log.TruncateAfter(-1);
            Assert.AreEqual(0, log.Count);
        }

        [TestMethod]
        public void Add_ClampsALateTickToTheLastOne()
        {
            var log = new RecordedConsoleLog();
            log.Add(50, Line("a"));
            log.Add(40, Line("late"));
            Assert.AreEqual(50, log[1].Tick);
            log.Add(50, null);
            Assert.AreEqual(2, log.Count);
        }
    }
}

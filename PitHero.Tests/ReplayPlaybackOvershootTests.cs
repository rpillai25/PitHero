using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>
    /// A fast Simulated playback may step a few ticks past the recorded end inside one rendered frame.
    /// Those ticks have nothing to verify against, so the recorder takes over and the recording stays
    /// gap-free. Issue #438 removed the simulated future but kept this overshoot rule, and dropped the
    /// pause-release commands the future used to inject on its first tick.
    /// </summary>
    [TestClass]
    public class ReplayPlaybackOvershootTests
    {
        [TestMethod]
        public void InjectDue_PastRecordedEnd_TurnsTheRecorderOn_AndInjectsNothing()
        {
            var recorder = new ReplayRecorder();
            var commands = new PlayerCommandService();
            var playback = new ReplayPlaybackService();
            try
            {
                recorder.Initialize(ReplayKind.NewGame, 1, null, null);
                recorder.IsRecording = false; // playback: the recorder's lists are the recording
                var data = new ReplayData { Kind = ReplayKind.NewGame, TotalTicks = 100 };
                playback.AttachRecordingForTest(data);

                playback.InjectDue(100, commands);
                Assert.IsFalse(recorder.IsRecording, "The last recorded tick is verified, not recorded");
                Assert.AreEqual(0, commands.PendingCount);

                playback.InjectDue(101, commands);
                Assert.IsTrue(recorder.IsRecording, "The first tick past the end flips the recorder on");
                Assert.AreEqual(0, commands.PendingCount, "Nothing is injected past the end: the future's pause release is gone");
            }
            finally
            {
                playback.Detach();
                commands.Detach();
                recorder.Detach();
            }
        }
    }
}

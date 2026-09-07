using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>Continuing live play from the middle of a replay must drop everything recorded after that tick.</summary>
    [TestClass]
    public class ReplayRecorderTruncateTests
    {
        [TestMethod]
        public void TruncateAfter_DropsLaterCommandsAndSamples_KeepsEarlierOnes()
        {
            var recorder = new ReplayRecorder();
            try
            {
                recorder.Initialize(ReplayKind.NewGame, 1, null, null);
                for (int i = 0; i < 10; i++)
                    recorder.RecordCommand(i * 100, new PlayerCommand(PlayerCommandType.Replenish));
                for (int i = 0; i < 10; i++)
                {
                    recorder.RecordDecision(i * 100 + 5, (ulong)i);
                    recorder.RecordStateHash(new ReplayHashSample(i * 100 + 50, (ulong)i));
                }

                recorder.TruncateAfter(450);

                var snap = recorder.Snapshot(450);
                Assert.AreEqual(5, snap.Commands.Count, "ticks 0..400 stay");
                Assert.AreEqual(400, snap.Commands[4].Tick);
                Assert.AreEqual(5, snap.Decisions.Count, "decisions at 5..405 stay");
                Assert.AreEqual(5, snap.StateHashes.Count, "state samples at 50..450 stay (450 is not after the cut)");

                // Recording continues cleanly after the cut
                recorder.RecordCommand(451, new PlayerCommand(PlayerCommandType.Replenish));
                Assert.AreEqual(6, recorder.Snapshot(451).Commands.Count);
            }
            finally
            {
                recorder.Detach();
            }
        }

        [TestMethod]
        public void TruncateAfter_BeyondEnd_ChangesNothing()
        {
            var recorder = new ReplayRecorder();
            try
            {
                recorder.Initialize(ReplayKind.NewGame, 1, null, null);
                recorder.RecordCommand(10, new PlayerCommand(PlayerCommandType.Replenish));
                recorder.TruncateAfter(1000);
                Assert.AreEqual(1, recorder.Snapshot(1000).Commands.Count);
            }
            finally
            {
                recorder.Detach();
            }
        }
    }
}

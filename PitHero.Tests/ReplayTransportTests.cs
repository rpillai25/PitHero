using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>
    /// The replay transport: Play runs at 1X, the speed button only picks the rate, and the
    /// fast-forward and rewind buttons are what play at it (Simulated mode, headless).
    /// </summary>
    [TestClass]
    public class ReplayTransportTests
    {
        private static ReplayPlaybackService MakePlaying()
        {
            var playback = new ReplayPlaybackService();
            playback.AttachRecordingForTest(new ReplayData { Kind = ReplayKind.NewGame, TotalTicks = 1000000000L }); // past any tick the shared SimulationClock reaches in other tests
            playback.SetStateForTest(ReplayPlaybackState.Playing);
            return playback;
        }

        [TestMethod]
        public void Play_RunsAt1X_UntilFastForwardIsPressed()
        {
            var playback = MakePlaying();
            try
            {
                Assert.AreEqual("2X", playback.SpeedLabel, "The picker starts at the first fast rung");
                Assert.AreEqual(1f, playback.PlaybackSpeed);
                Assert.IsFalse(playback.IsFastForwarding);

                playback.ToggleFastForward();
                Assert.IsTrue(playback.IsFastForwarding);
                Assert.AreEqual(playback.Speed, playback.PlaybackSpeed);

                playback.ToggleFastForward();
                Assert.IsFalse(playback.IsFastForwarding, "A second press drops back to plain play");
                Assert.AreEqual(ReplayPlaybackState.Playing, playback.State);
                Assert.AreEqual(1f, playback.PlaybackSpeed);
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void SpeedButton_OnlyPicksTheRate_AndSkips1X()
        {
            var playback = MakePlaying();
            try
            {
                playback.CycleSpeed();
                Assert.AreEqual("4X", playback.SpeedLabel);
                Assert.AreEqual(1f, playback.PlaybackSpeed, "Picking a rate does not start a fast-forward");
                playback.CycleSpeed();
                Assert.AreEqual("8X", playback.SpeedLabel);
                playback.CycleSpeed();
                Assert.AreEqual("2X", playback.SpeedLabel, "Wraps back to 2X, never 1X");

                playback.ToggleFastForward();
                playback.CycleSpeed();
                Assert.AreEqual(GameConfig.SpeedSteps[2], playback.PlaybackSpeed, "A running fast-forward changes rate at once");
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void FastForward_FromPause_Plays_AndPauseEndsIt()
        {
            var playback = MakePlaying();
            try
            {
                playback.TogglePause();
                playback.ToggleFastForward();
                Assert.AreEqual(ReplayPlaybackState.Playing, playback.State, "Fast-forward resumes a paused replay");
                Assert.IsTrue(playback.IsFastForwarding);

                playback.TogglePause();
                Assert.IsFalse(playback.IsFastForwarding, "Pausing ends the fast-forward");
                playback.TogglePause();
                Assert.AreEqual(1f, playback.PlaybackSpeed, "Resuming plays at 1X");
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void HoldFastForward_FromPlay_ReturnsTo1XPlayOnRelease()
        {
            var playback = MakePlaying();
            try
            {
                playback.BeginHoldFastForward();
                Assert.IsTrue(playback.IsFastForwarding);
                Assert.AreEqual(playback.Speed, playback.PlaybackSpeed);

                playback.EndHoldFastForward();
                Assert.IsFalse(playback.IsFastForwarding);
                Assert.AreEqual(ReplayPlaybackState.Playing, playback.State);
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void HoldFastForward_FromPause_PausesAgainOnRelease()
        {
            var playback = MakePlaying();
            try
            {
                playback.TogglePause();
                playback.BeginHoldFastForward();
                Assert.AreEqual(ReplayPlaybackState.Playing, playback.State);
                Assert.IsTrue(playback.IsFastForwarding);

                playback.EndHoldFastForward();
                Assert.AreEqual(ReplayPlaybackState.Paused, playback.State);
                Assert.IsFalse(playback.IsFastForwarding);
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void HoldFastForward_DuringFastForward_KeepsItOnRelease()
        {
            var playback = MakePlaying();
            try
            {
                playback.ToggleFastForward();
                playback.BeginHoldFastForward();
                playback.EndHoldFastForward();
                Assert.IsTrue(playback.IsFastForwarding, "The button's fast-forward outlives the key hold");
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void HoldFastForward_PausedMidHold_StaysPaused()
        {
            var playback = MakePlaying();
            try
            {
                playback.BeginHoldFastForward();
                playback.TogglePause();
                playback.EndHoldFastForward();
                Assert.AreEqual(ReplayPlaybackState.Paused, playback.State, "A pause during the hold is not undone by the release");
            }
            finally
            {
                playback.Detach();
            }
        }

        [TestMethod]
        public void FastForward_AtEnd_DoesNothing()
        {
            var playback = MakePlaying();
            try
            {
                playback.SetStateForTest(ReplayPlaybackState.AtEnd);
                playback.ToggleFastForward();
                Assert.AreEqual(ReplayPlaybackState.AtEnd, playback.State);
                Assert.IsFalse(playback.IsFastForwarding);
            }
            finally
            {
                playback.Detach();
            }
        }
    }
}

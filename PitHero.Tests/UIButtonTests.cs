using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Artifacts;
using PitHero.Services;
using PitHero.UI;
using System;
using System.IO;

namespace PitHero.Tests
{
    [TestClass]
    public class UIButtonTests
    {
        private string _artifactDir;
        private ArtifactService _artifacts;

        /// <summary>
        /// Most fast-forward tests exercise the full speed ladder, so they need the Kairos Metronome
        /// owned. The gating test replaces this service with its own empty one.
        /// </summary>
        [TestInitialize]
        public void UnlockHighSpeedRungs()
        {
            _artifactDir = Path.Combine(Path.GetTempPath(), "pithero_ff_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_artifactDir);
            _artifacts = new ArtifactService(_artifactDir, "system.bin");
            _artifacts.Grant(ArtifactType.KairosMetronome);
        }

        [TestCleanup]
        public void ResetSpeedState()
        {
            _artifacts?.Detach();
            Core.SimulationSpeed = 1f;
            Core.MaxStepsPerFrame = GameConfig.SimulationMaxStepsPerFrame;
            if (Directory.Exists(_artifactDir))
                Directory.Delete(_artifactDir, true);
        }

        [TestMethod]
        public void FastFUI_CanBeCreated()
        {
            var fastFUI = new FastFUI();
            Assert.IsNotNull(fastFUI);
        }

        [TestMethod]
        public void HeroUI_CanBeCreated()
        {
            var heroUI = new HeroUI();
            Assert.IsNotNull(heroUI);
        }

        [TestMethod]
        public void FastFUI_TimeScaleToggle_WorksCorrectly()
        {
            var fastFUI = new FastFUI();
            
            // Initially time scale should be 1.0
            Time.TimeScale = 1f;
            
            // Test that FastFUI component initializes correctly
            Assert.IsNotNull(fastFUI);
            Assert.AreEqual(1f, Time.TimeScale);
        }

        /// <summary>
        /// From the 1X start rung a plain toggle engages the default rung rather than latching at
        /// normal speed, and toggling back restores normal speed and the normal per-frame step cap.
        /// Speed is never Time.TimeScale.
        /// </summary>
        [TestMethod]
        public void FastFUI_Toggle_EngagesDefaultRungAndRestores()
        {
            var fastFUI = new FastFUI();
            Assert.AreEqual(0, fastFUI.SpeedIndex);

            fastFUI.TriggerToggle();
            Assert.IsTrue(fastFUI.IsSpeedUp);
            Assert.AreEqual(GameConfig.SpeedSteps[GameConfig.SimulationDefaultSpeedIndex], Core.SimulationSpeed);
            Assert.AreEqual(GameConfig.HighSpeedMaxStepsPerFrame, Core.MaxStepsPerFrame);
            Assert.AreEqual("2X", fastFUI.SpeedLabel);

            fastFUI.TriggerToggle();
            Assert.IsFalse(fastFUI.IsSpeedUp);
            Assert.AreEqual(1f, Core.SimulationSpeed);
            Assert.AreEqual(GameConfig.SimulationMaxStepsPerFrame, Core.MaxStepsPerFrame);
            // Back at normal speed the label is dropped, even though the rung is remembered
            Assert.IsNull(fastFUI.SpeedLabel);
            Assert.AreEqual(GameConfig.SimulationDefaultSpeedIndex, fastFUI.SpeedIndex);
            Assert.AreEqual(1f, Time.TimeScale);

            // Re-engaging brings the remembered rung's label back
            fastFUI.TriggerToggle();
            Assert.AreEqual("2X", fastFUI.SpeedLabel);
            fastFUI.SetSpeedUp(false);
        }

        /// <summary>
        /// Cycling only picks the rung — it never engages or disengages fast forward. The label
        /// updates immediately so the player sees the speed they just selected.
        /// </summary>
        [TestMethod]
        public void FastFUI_CycleSpeed_ChangesRungWithoutEngaging()
        {
            var fastFUI = new FastFUI();
            Core.SimulationSpeed = 1f;

            // Starts on rung 0 (1X): disengaged, and the button face carries no label
            Assert.AreEqual(0, fastFUI.SpeedIndex);
            Assert.IsFalse(fastFUI.IsSpeedUp);
            Assert.IsNull(fastFUI.SpeedLabel);

            fastFUI.CycleSpeed();
            Assert.AreEqual(1, fastFUI.SpeedIndex);
            Assert.AreEqual("2X", fastFUI.SpeedLabel);
            Assert.IsFalse(fastFUI.IsSpeedUp, "SHIFT+click must not engage fast forward");
            Assert.AreEqual(1f, Core.SimulationSpeed, "picking a rung while disengaged must not speed the game up");

            fastFUI.CycleSpeed();
            Assert.AreEqual("4X", fastFUI.SpeedLabel);
            Assert.IsFalse(fastFUI.IsSpeedUp, "SHIFT+click must not engage fast forward");
            Assert.AreEqual(1f, Core.SimulationSpeed, "picking a rung while disengaged must not speed the game up");

            fastFUI.CycleSpeed();
            Assert.AreEqual("8X", fastFUI.SpeedLabel);

            // Wrapping past the top rung returns to 2X, never to the 1X start rung
            fastFUI.CycleSpeed();
            Assert.AreEqual(GameConfig.SimulationDefaultSpeedIndex, fastFUI.SpeedIndex);
            Assert.AreEqual("2X", fastFUI.SpeedLabel);
            Assert.IsFalse(fastFUI.IsSpeedUp);
        }

        /// <summary>
        /// Dropping back to normal speed hides the label; SHIFT+click brings it back even though the
        /// game is still running at normal speed.
        /// </summary>
        [TestMethod]
        public void FastFUI_Label_HiddenAtNormalSpeed_ReturnsOnCycle()
        {
            var fastFUI = new FastFUI();

            fastFUI.TriggerToggle();            // engage at 2X
            Assert.AreEqual("2X", fastFUI.SpeedLabel);

            fastFUI.TriggerToggle();            // back to normal speed
            Assert.IsNull(fastFUI.SpeedLabel);
            Assert.AreEqual(1f, Core.SimulationSpeed);

            fastFUI.CycleSpeed();               // picking a rung shows it again while still disengaged
            Assert.AreEqual("4X", fastFUI.SpeedLabel);
            Assert.IsFalse(fastFUI.IsSpeedUp);
            Assert.AreEqual(1f, Core.SimulationSpeed);
        }

        /// <summary>A plain toggle engages at whatever rung is currently selected.</summary>
        [TestMethod]
        public void FastFUI_Toggle_EngagesAtSelectedRung()
        {
            var fastFUI = new FastFUI();

            fastFUI.CycleSpeed();               // 1X -> 2X
            fastFUI.CycleSpeed();               // 2X -> 4X
            fastFUI.CycleSpeed();               // 4X -> 8X
            fastFUI.TriggerToggle();
            Assert.IsTrue(fastFUI.IsSpeedUp);
            Assert.AreEqual(8f, Core.SimulationSpeed);
            Assert.AreEqual(GameConfig.HighSpeedMaxStepsPerFrame, Core.MaxStepsPerFrame);

            // Changing the rung while engaged takes effect immediately and stays a real speed-up
            fastFUI.CycleSpeed();               // 8X -> 2X
            Assert.IsTrue(fastFUI.IsSpeedUp, "the rung cycle must not disengage fast forward");
            Assert.AreEqual(GameConfig.SpeedSteps[GameConfig.SimulationDefaultSpeedIndex], Core.SimulationSpeed);
            Assert.AreEqual(GameConfig.HighSpeedMaxStepsPerFrame, Core.MaxStepsPerFrame);

            fastFUI.SetSpeedUp(false);
            Assert.AreEqual(GameConfig.SimulationMaxStepsPerFrame, Core.MaxStepsPerFrame);
        }

        /// <summary>
        /// The 4X and 8X rungs are gated behind the Kairos Metronome: without it SHIFT+click parks on
        /// 2X, with it the full ladder cycles.
        /// </summary>
        [TestMethod]
        public void FastFUI_HighRungs_GatedOnKairosMetronome()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_ff_gate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var service = new ArtifactService(dir, "system.bin"); // replaces the unlocked setup service
            try
            {
                Assert.IsFalse(FastFUI.HighSpeedRungsUnlocked);

                var locked = new FastFUI();
                locked.CycleSpeed();
                Assert.AreEqual("2X", locked.SpeedLabel);
                locked.CycleSpeed();
                Assert.AreEqual("2X", locked.SpeedLabel, "4X stays locked without the metronome");

                service.Grant(ArtifactType.KairosMetronome);
                Assert.IsTrue(FastFUI.HighSpeedRungsUnlocked);

                var unlocked = new FastFUI();
                unlocked.CycleSpeed();
                Assert.AreEqual("2X", unlocked.SpeedLabel);
                unlocked.CycleSpeed();
                Assert.AreEqual("4X", unlocked.SpeedLabel);
                unlocked.CycleSpeed();
                Assert.AreEqual("8X", unlocked.SpeedLabel);
                unlocked.CycleSpeed();
                Assert.AreEqual("2X", unlocked.SpeedLabel);
            }
            finally
            {
                service.Detach();
                Core.SimulationSpeed = 1f;
                Core.MaxStepsPerFrame = GameConfig.SimulationMaxStepsPerFrame;
                Directory.Delete(dir, true);
            }
        }

        /// <summary>The speed ladder and its player-facing labels stay index-aligned.</summary>
        [TestMethod]
        public void SpeedSteps_AndLabels_AreAligned()
        {
            Assert.AreEqual(GameConfig.SpeedSteps.Length, GameConfig.SpeedStepLabels.Length);
            Assert.AreEqual(1f, GameConfig.SpeedSteps[0]);
            CollectionAssert.AreEqual(new[] { "1X", "2X", "4X", "8X" }, GameConfig.SpeedStepLabels);
            Assert.IsTrue(GameConfig.HighSpeedMaxStepsPerFrame >= GameConfig.SpeedSteps[GameConfig.SpeedSteps.Length - 1]);
        }
    }
}
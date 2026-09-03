using System.Collections.Generic;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>
    /// The command queue is the only doorway from player input into the simulation. These tests pin
    /// its ordering, tick stamping, replay-time rejection and the direct-apply fallback. Handlers run
    /// headlessly as no-ops (no Nez core), which is exactly the re-validation contract they promise.
    /// </summary>
    [TestClass]
    public class PlayerCommandServiceTests
    {
        private PlayerCommandService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new PlayerCommandService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service.Detach();
        }

        /// <summary>Commands drain in enqueue order, each stamped with the drain tick.</summary>
        [TestMethod]
        public void Drain_AppliesInOrderWithTick()
        {
            var seen = new List<(long tick, PlayerCommandType type, int a)>();
            _service.OnCommandApplied += (tick, cmd) => seen.Add((tick, cmd.Type, cmd.A));

            Assert.IsTrue(_service.Enqueue(new PlayerCommand(PlayerCommandType.UseShortcut, 3)));
            Assert.IsTrue(_service.Enqueue(PlayerCommand.Flag(PlayerCommandType.SetManualPause, true)));
            Assert.IsTrue(_service.Enqueue(new PlayerCommand(PlayerCommandType.Replenish)));
            Assert.AreEqual(3, _service.PendingCount);

            _service.Drain(42);

            Assert.AreEqual(0, _service.PendingCount);
            Assert.AreEqual(3, seen.Count);
            Assert.AreEqual((42L, PlayerCommandType.UseShortcut, 3), seen[0]);
            Assert.AreEqual((42L, PlayerCommandType.SetManualPause, 1), seen[1]);
            Assert.AreEqual((42L, PlayerCommandType.Replenish, 0), seen[2]);
        }

        /// <summary>Live enqueues are dropped while a replay is playing; injected commands still drain.</summary>
        [TestMethod]
        public void RejectLiveEnqueues_DropsLiveButKeepsInjected()
        {
            int applied = 0;
            _service.OnCommandApplied += (tick, cmd) => applied++;
            _service.RejectLiveEnqueues = true;

            Assert.IsFalse(_service.Enqueue(new PlayerCommand(PlayerCommandType.Replenish)));
            Assert.AreEqual(0, _service.PendingCount);

            _service.Inject(new PlayerCommand(PlayerCommandType.Replenish));
            Assert.AreEqual(1, _service.PendingCount);
            _service.Drain(7);
            Assert.AreEqual(1, applied);
        }

        /// <summary>A None command is never queued.</summary>
        [TestMethod]
        public void Enqueue_NoneIsIgnored()
        {
            Assert.IsFalse(_service.Enqueue(new PlayerCommand(PlayerCommandType.None)));
            Assert.AreEqual(0, _service.PendingCount);
        }

        /// <summary>The queue grows past its initial capacity without losing order.</summary>
        [TestMethod]
        public void Enqueue_GrowsBeyondInitialCapacity()
        {
            var seen = new List<int>();
            _service.OnCommandApplied += (tick, cmd) => seen.Add(cmd.A);
            for (int i = 0; i < 300; i++)
                _service.Enqueue(new PlayerCommand(PlayerCommandType.UseShortcut, i));
            _service.Drain(1);
            Assert.AreEqual(300, seen.Count);
            for (int i = 0; i < 300; i++)
                Assert.AreEqual(i, seen[i]);
        }

        /// <summary>With a service present, Dispatch queues; while a handler runs, callers apply directly.</summary>
        [TestMethod]
        public void Dispatch_QueuesWhenServiceExists()
        {
            Assert.IsFalse(PlayerCommandService.ShouldApplyDirectly);
            Assert.IsTrue(PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.Replenish)));
            Assert.AreEqual(1, _service.PendingCount);
        }

        /// <summary>Without a service (title screen, tests) Dispatch applies immediately and reports success.</summary>
        [TestMethod]
        public void Dispatch_AppliesDirectlyWithoutService()
        {
            _service.Detach();
            Assert.IsTrue(PlayerCommandService.ShouldApplyDirectly);
            Assert.IsTrue(PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.Replenish)));
        }

        /// <summary>PauseService routes through the queue during a session and applies directly otherwise.</summary>
        [TestMethod]
        public void PauseService_RoutesThroughQueueDuringSession()
        {
            var pause = new PauseService();
            pause.Pause();
            Assert.IsFalse(pause.IsPaused, "queued, not yet applied");
            Assert.IsTrue(pause.IsManualPauseRequested);
            Assert.AreEqual(1, _service.PendingCount);

            pause.ApplyManualPause(true);
            Assert.IsTrue(pause.IsPaused);

            pause.Toggle();
            Assert.IsFalse(pause.IsManualPauseRequested, "toggle works off the requested value");

            _service.Detach();
            var direct = new PauseService();
            direct.Pause();
            Assert.IsTrue(direct.IsPaused, "no session: applied immediately");
            direct.SetFarmModePause(true);
            Assert.IsTrue(direct.IsPaused);
            direct.ResetImmediate();
            Assert.IsFalse(direct.IsPaused);
        }
    }
}

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

        /// <summary>
        /// A command enqueued while a drain is running (a dialog closing itself after the command it
        /// executed, e.g. AddMonsterDialog after a purchase fills the house) is recorded and applied on
        /// the NEXT drain — never applied directly and never lost. The 2026-09-16 divergence: an
        /// unpause raised inside the PurchaseMonster handler was applied directly, so the live game
        /// resumed while the replay (dialog never open) stayed paused.
        /// </summary>
        [TestMethod]
        public void Enqueue_DuringDrain_RunsOnTheNextDrain()
        {
            var pause = new PauseService();
            var applied = new List<PlayerCommandType>();
            bool requested = false;
            _service.OnCommandApplied += (tick, cmd) =>
            {
                applied.Add(cmd.Type);
                if (!requested)
                {
                    requested = true;
                    pause.Unpause(); // presentation feedback from inside a drain
                }
            };
            _service.Enqueue(PlayerCommand.Flag(PlayerCommandType.SetManualPause, true));

            _service.Drain(10);
            Assert.AreEqual(1, applied.Count, "the unpause must not run in the same drain");
            Assert.AreEqual(1, _service.PendingCount, "the unpause was queued (and will be recorded), not applied directly");
            Assert.IsFalse(pause.IsManualPauseRequested);

            _service.Drain(11);
            Assert.AreEqual(2, applied.Count);
            Assert.AreEqual(PlayerCommandType.SetManualPause, applied[1]);
            Assert.AreEqual(0, _service.PendingCount);
        }

        /// <summary>
        /// Settings that used to be written straight from UI (Food tab, fridge slider, sell/purchase
        /// priority lists) are commands now. Their numbers are stored in replay files: pin them.
        /// </summary>
        [TestMethod]
        public void FormerlyDirectUiSettings_HaveStableCommandNumbers()
        {
            Assert.AreEqual(120, (int)PlayerCommandType.SetFavoriteDish);
            Assert.AreEqual(121, (int)PlayerCommandType.SetEatAtTavern);
            Assert.AreEqual(122, (int)PlayerCommandType.SetPreStockStackSize);
            Assert.AreEqual(123, (int)PlayerCommandType.SetConsumablesFirst);
        }

        /// <summary>The new handlers re-validate and no-op headlessly instead of throwing.</summary>
        [TestMethod]
        public void FormerlyDirectUiSettings_DrainHeadlessWithoutThrowing()
        {
            var applied = new List<PlayerCommandType>();
            _service.OnCommandApplied += (tick, cmd) => applied.Add(cmd.Type);

            _service.Enqueue(new PlayerCommand(PlayerCommandType.SetFavoriteDish, 999));
            _service.Enqueue(PlayerCommand.Flag(PlayerCommandType.SetEatAtTavern, false));
            _service.Enqueue(new PlayerCommand(PlayerCommandType.SetPreStockStackSize, 5));
            _service.Enqueue(new PlayerCommand(PlayerCommandType.SetConsumablesFirst, 1, 1));
            _service.Drain(7);

            Assert.AreEqual(4, applied.Count);
        }

        /// <summary>Farm priority command numbers are stored in replay files: pin them.</summary>
        [TestMethod]
        public void FarmPriority_HasStableCommandNumbers()
        {
            Assert.AreEqual(124, (int)PlayerCommandType.SetFarmMonstersDecide);
            Assert.AreEqual(125, (int)PlayerCommandType.SetFarmPriorityOrder);
        }

        /// <summary>A junk order payload is ignored by the handler rather than throwing or applying.</summary>
        [TestMethod]
        public void FarmPriority_DrainsHeadlessWithoutThrowing()
        {
            var applied = new List<PlayerCommandType>();
            _service.OnCommandApplied += (tick, cmd) => applied.Add(cmd.Type);

            _service.Enqueue(PlayerCommand.Flag(PlayerCommandType.SetFarmMonstersDecide, false));
            _service.Enqueue(new PlayerCommand(PlayerCommandType.SetFarmPriorityOrder, 9, 9, 9, 9));
            _service.Drain(7);

            Assert.AreEqual(2, applied.Count);
        }
    }
}

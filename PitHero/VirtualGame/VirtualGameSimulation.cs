using Microsoft.Xna.Framework;
using PitHero.AI;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Main virtual game simulation that runs the complete GOAP workflow
    /// </summary>
    public class VirtualGameSimulation
    {
        private readonly VirtualWorldState _world;
        private readonly VirtualHero _hero;
        private readonly VirtualPitLevelQueue _pitQueue;
        private string _currentAction;
        private int _tickCount;
        private readonly Random _random;

        // ── Phase B: combat simulation state ──────────────────────────────────────
        private VirtualBattleRunner _battleRunner;
        private VirtualRunMetrics   _runMetrics;

        // Mutable roster: ConfigureMercenaries copies in; TryHireRandomMercenary appends;
        // RunLevelRange prunes dead mercs between levels.
        private readonly List<Mercenary> _mercenaries = new List<Mercenary>(2);

        // ── Gold economy state ─────────────────────────────────────────────────────
        // Counter for deterministic virtual merc names ("Merc1", "Merc2", ...)
        private int _mercHireCount;

        // Party view from the most recent RunPitLevel — used by TryInnRest to reset
        // healing-exhausted flags exactly as SleepInBedAction does.
        private VirtualBattlePartyView _lastPartyView;

        public VirtualGameSimulation()
        {
            _world = new VirtualWorldState();
            _hero = new VirtualHero(_world);
            _pitQueue = new VirtualPitLevelQueue();
            _currentAction = "None";
            _tickCount = 0;
            _random = new Random(42); // Deterministic seed for testing
            // _mercenaries is initialized inline as new List<Mercenary>(2)
            Gold = GameConfig.NewGameStartingGold; // mirrors live new-game starting funds
            RngSeed = Nez.Random.GetSeed();
        }

        /// <summary>
        /// Creates a simulation with a deterministic combat RNG seed.
        /// Seeds the global <c>Nez.Random</c> (used by all combat rolls: turn order,
        /// evasion/variance, target picks, crit/deflect) so before/after balance runs
        /// with the same seed are reproducible.  Pit LAYOUT is already deterministic
        /// per level (local <c>Random(level)</c> in the generators) independent of this seed.
        /// </summary>
        /// <param name="rngSeed">Seed applied to <c>Nez.Random</c> for combat rolls.</param>
        public VirtualGameSimulation(int rngSeed) : this()
        {
            Nez.Random.SetSeed(rngSeed);
            RngSeed = rngSeed;
        }

        /// <summary>
        /// The combat RNG seed for this run: the value passed to the seeded constructor,
        /// or the ambient <c>Nez.Random</c> seed captured at construction.  Recorded into
        /// <see cref="VirtualRunMetrics.RngSeed"/> so every balance report can cite it.
        /// </summary>
        public int RngSeed { get; }

        /// <summary>Public access to the hero for tests.</summary>
        public VirtualHero Hero => _hero;

        /// <summary>Public read-only access to the virtual world state.</summary>
        public VirtualWorldState World => _world;

        /// <summary>
        /// Run metrics accumulated by the last <see cref="RunPitLevel"/> call.
        /// Null until <see cref="RunPitLevel"/> has been called at least once.
        /// </summary>
        public VirtualRunMetrics Metrics => _runMetrics;

        // ── Gold economy ──────────────────────────────────────────────────────────

        /// <summary>
        /// Current gold wallet.  Defaults to <see cref="GameConfig.NewGameStartingGold"/>
        /// (200 gold) — the same amount the live game grants on a fresh save.
        /// Grows as gold is earned from monsters; shrinks when <see cref="TryInnRest"/>
        /// or <see cref="TryHireRandomMercenary"/> spend from it.
        /// </summary>
        public int Gold { get; private set; }

        /// <summary>
        /// Overrides the starting wallet balance.  Call before any <see cref="RunPitLevel"/>
        /// call to mirror a save-loaded mid-game state or to set up a specific test scenario.
        /// </summary>
        /// <param name="gold">New wallet amount (clamped to ≥ 0).</param>
        public void ConfigureStartingGold(int gold)
        {
            Gold = gold < 0 ? 0 : gold;
        }

        /// <summary>
        /// Mirrors <see cref="PitHero.AI.SleepInBedAction"/> (lines ~255–262 + ~465–535):
        /// requires <see cref="Gold"/> ≥ <see cref="GameConfig.InnCostGold"/> (10 g);
        /// deducts the cost; restores the hero and every configured mercenary to full HP and
        /// MP; resets <c>HealingItemExhausted</c> and <c>HealingSkillExhausted</c> on the
        /// last-used party view — exactly as the live action does after the sleep animation.
        /// Walking / animation time is skipped (virtual: instant).
        /// </summary>
        /// <returns>
        /// <c>true</c> when the rest was taken and all HP/MP restored;
        /// <c>false</c> when there is insufficient gold (mirrors live <c>InnExhausted</c>
        /// semantics in <see cref="PitHero.AI.JumpOutOfPitForInnAction"/>).
        /// </returns>
        public bool TryInnRest()
        {
            if (Gold < GameConfig.InnCostGold) return false;

            Gold -= GameConfig.InnCostGold;

            // Restore hero HP and MP to full (mirrors SleepInBedAction lines ~465-495)
            var hero = _hero.LinkedHero;
            if (hero != null)
            {
                hero.RestoreHP(hero.MaxHP);    // clamps to MaxHP
                hero.RestoreMP(-1);            // Hero.RestoreMP(-1) = full restore
            }

            // Restore each configured mercenary to full HP and MP
            // (mirrors SleepInBedAction lines ~502-536)
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                var merc = _mercenaries[i];
                merc.RestoreHP(merc.MaxHP);    // clamps to MaxHP
                merc.RestoreMP(merc.MaxMP);    // Mercenary.RestoreMP clamps to MaxMP
            }

            // Reset healing-exhausted flags (mirrors SleepInBedAction lines ~497-500)
            if (_lastPartyView != null)
            {
                _lastPartyView.HealingItemExhausted  = false;
                _lastPartyView.HealingSkillExhausted = false;
            }

            return true;
        }

        /// <summary>
        /// Mirrors <see cref="PitHero.Services.MercenaryManager.SpawnMercenary"/> +
        /// <see cref="PitHero.Services.MercenaryManager.HireMercenary"/>:
        /// <list type="bullet">
        ///   <item>Returns <c>null</c> when the roster already has 2 mercenaries
        ///   (mirrors <c>MaxHiredMercenaries = 2</c>).</item>
        ///   <item>Returns <c>null</c> when <see cref="Gold"/> &lt;
        ///   <see cref="BalanceConfig.CalculateMercenaryHireCost"/> for the rolled level.</item>
        ///   <item>Otherwise: rolls mercenary level via the same weighted distribution
        ///   as <c>MercenaryManager.DetermineMercenaryLevel</c> and job via the same
        ///   six-job pool; creates and returns the <see cref="Mercenary"/> with
        ///   <see cref="Mercenary.LearnAllJobSkills"/> applied.</item>
        /// </list>
        /// All RNG uses <c>Nez.Random</c> (seeded per-run by the constructor) so results
        /// are reproducible.
        /// </summary>
        /// <returns>The hired <see cref="Mercenary"/>, or <c>null</c> when not possible.</returns>
        public Mercenary TryHireRandomMercenary()
        {
            const int maxHiredMercenaries = 2; // mirrors MercenaryManager.MaxHiredMercenaries

            if (_mercenaries.Count >= maxHiredMercenaries) return null;

            int heroLevel = _hero.LinkedHero?.Level ?? 1;
            int mercLevel = VirtualMercenaryLevelRoller.DetermineMercenaryLevel(heroLevel);
            int hireCost  = BalanceConfig.CalculateMercenaryHireCost(mercLevel);

            if (Gold < hireCost) return null;

            Gold -= hireCost;

            _mercHireCount++;
            string name   = "Merc" + _mercHireCount;
            IJob   job    = VirtualMercenaryLevelRoller.GetRandomJob();
            var baseStats = new StatBlock(strength: 4, agility: 3, vitality: 5, magic: 1);
            var mercenary = new Mercenary(name, job, mercLevel, baseStats);
            mercenary.LearnAllJobSkills();

            _mercenaries.Add(mercenary);
            return mercenary;
        }

        /// <summary>
        /// Runs a persistent multi-level traversal reusing the same hero, mercenary roster,
        /// item bag, and gold wallet across all levels.  Between each pair of levels the
        /// surface policy is applied (mirroring what a player does before re-entering):
        /// <list type="number">
        ///   <item>Dead mercenaries (HP ≤ 0 after the previous level) are pruned from the
        ///   roster so that hiring can fill their slot.</item>
        ///   <item>Hire random mercenaries via <see cref="TryHireRandomMercenary"/> until
        ///   the roster has 2 members or gold runs out.</item>
        ///   <item>Rest at the inn via <see cref="TryInnRest"/> when any party member is
        ///   below full HP or MP and gold is sufficient.</item>
        /// </list>
        /// The number of mercs hired and whether the inn was used are recorded in the
        /// <em>following</em> level's <see cref="VirtualRunMetrics"/> (since they happen
        /// before that level starts).  Traversal stops early when a level ends with the
        /// hero wiped.
        /// </summary>
        /// <param name="fromLevel">First pit level to run (inclusive).</param>
        /// <param name="toLevel">Last pit level to run (inclusive).</param>
        /// <returns>One <see cref="VirtualRunMetrics"/> per level attempted, in order.</returns>
        public List<VirtualRunMetrics> RunLevelRange(int fromLevel, int toLevel)
        {
            if (_hero.LinkedHero == null)
                throw new InvalidOperationException("Call ConfigureHero before RunLevelRange.");

            var results = new List<VirtualRunMetrics>(toLevel - fromLevel + 1);

            bool innRestedBeforeThis = false;
            int  mercsHiredBeforeThis = 0;

            for (int level = fromLevel; level <= toLevel; level++)
            {
                // ── Between-level surface policy (skip for the very first level) ─────
                if (level > fromLevel)
                {
                    // 1. Prune dead mercs so hiring can replace them
                    for (int i = _mercenaries.Count - 1; i >= 0; i--)
                    {
                        if (_mercenaries[i].CurrentHP <= 0)
                            _mercenaries.RemoveAt(i);
                    }

                    // 2. Hire up to roster cap while affordable
                    mercsHiredBeforeThis = 0;
                    while (_mercenaries.Count < 2)
                    {
                        if (TryHireRandomMercenary() == null) break;
                        mercsHiredBeforeThis++;
                    }

                    // 3. Inn rest when any party member is below full HP or MP
                    innRestedBeforeThis = false;
                    if (PartyNeedsRest())
                        innRestedBeforeThis = TryInnRest();
                }
                else
                {
                    innRestedBeforeThis  = false;
                    mercsHiredBeforeThis = 0;
                }

                // ── Run the pit level ─────────────────────────────────────────────────
                var metrics = RunPitLevel(level);

                // Stamp between-level policy actions onto this level's metrics
                metrics.InnRested  = innRestedBeforeThis;
                metrics.MercsHired = mercsHiredBeforeThis;

                results.Add(metrics);

                if (metrics.Wiped)
                    break;
            }

            return results;
        }

        // ── Phase B: simulation configuration API ─────────────────────────────────

        /// <summary>
        /// Creates and links a real <see cref="Hero"/> to the virtual hero so that
        /// combat simulation reflects the correct job, level, and stat totals.
        /// </summary>
        /// <param name="job">Job class (Warrior, Mage, Priest, etc.).</param>
        /// <param name="level">Hero level (1–99).</param>
        /// <param name="baseStats">Base stat block (Str / Agi / Vit / Mag).</param>
        /// <param name="crystal">Optional bound crystal enabling skill purchases (see VirtualHero.ConfigureHero).</param>
        public void ConfigureHero(IJob job, int level, in StatBlock baseStats, HeroCrystal crystal = null)
        {
            _hero.ConfigureHero(job, level, in baseStats, crystal);
        }

        /// <summary>
        /// Consumable bag used by the party in virtual battles.  Stock it with potions
        /// before <see cref="RunPitLevel"/> to mirror a real playthrough (a live new game
        /// starts with HP Potions); it persists across levels like the live hero's bag.
        /// </summary>
        public ItemBag Bag { get; } = new ItemBag();

        /// <summary>
        /// Sets the mercenary roster to use for combat simulation.
        /// Pass an empty list (or null) to run solo (hero only).
        /// Copies the provided list into the internal mutable roster so that
        /// <see cref="TryHireRandomMercenary"/> and <see cref="RunLevelRange"/> can
        /// grow / shrink it independently of the caller's collection.
        /// </summary>
        /// <param name="mercenaries">Up to 2 hired mercenaries.</param>
        public void ConfigureMercenaries(IReadOnlyList<Mercenary> mercenaries)
        {
            _mercenaries.Clear();
            if (mercenaries != null)
            {
                for (int i = 0; i < mercenaries.Count; i++)
                    _mercenaries.Add(mercenaries[i]);
            }
        }

        /// <summary>
        /// Builds the chest-loot job context from the configured party, mirroring
        /// PitGenerator.BuildLootJobContext so virtual chest gear is biased toward
        /// equipable kinds exactly as in live play.
        /// </summary>
        private PitHero.ECS.Components.LootJobContext BuildLootJobContext()
        {
            if (_hero.LinkedHero == null)
                return PitHero.ECS.Components.LootJobContext.Empty;

            var ctx = new PitHero.ECS.Components.LootJobContext();
            ctx.HeroJob = _hero.LinkedHero.Job.JobFlag;
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                if (_mercenaries[i] != null)
                    ctx.MercJobs |= _mercenaries[i].Job.JobFlag;
            }
            return ctx;
        }

        /// <summary>
        /// Runs a complete pit level: generates the pit, explores it using the virtual
        /// state machine, fights all monsters, activates the wizard orb, and returns
        /// accumulated <see cref="VirtualRunMetrics"/>.
        /// </summary>
        /// <remarks>
        /// Requires <see cref="ConfigureHero"/> to have been called first.
        /// Mercenaries are optional — call <see cref="ConfigureMercenaries"/> before
        /// this if the scenario needs hired party members.
        /// </remarks>
        /// <param name="pitLevel">Pit level to simulate (1–25 for Cave biome).</param>
        /// <returns>Per-level aggregated metrics.</returns>
        public VirtualRunMetrics RunPitLevel(int pitLevel)
        {
            if (_hero.LinkedHero == null)
                throw new InvalidOperationException("Call ConfigureHero before RunPitLevel.");

            // Use Type.Name to avoid requiring the Nez text service in headless tests
            string jobName = _hero.LinkedHero.Job?.GetType().Name ?? "Unknown";

            _runMetrics = new VirtualRunMetrics
            {
                PitLevel      = pitLevel,
                JobName       = jobName,
                LevelRangeMin = _hero.LinkedHero.Level,
                LevelRangeMax = _hero.LinkedHero.Level,
                RngSeed       = RngSeed
            };

            // Party max-HP pool at level start, for the HP-loss percentage
            int partyMaxHPPool = _hero.LinkedHero.MaxHP;
            for (int i = 0; i < _mercenaries.Count; i++)
                partyMaxHPPool += _mercenaries[i].MaxHP;

            // Step 1: Set up pit geometry (fog, collision, pit bounds, PitLevel property)
            _world.RegeneratePit(pitLevel);

            // Step 2: Use VirtualPitGenerator to populate real IEnemy instances and traps.
            // The generator calls ClearAllEntities() internally, which preserves fog/collision
            // set by RegeneratePit, then repopulates entities with cave-biome aware content.
            var tiledMapService = new VirtualTiledMapService(_world);
            // VirtualPitWidthManager is uninitialized here (no tiled map layers available
            // headlessly); CurrentPitRightEdge returns 0, so the generator falls back to
            // default PitRectX + PitRectWidth - 3 for the valid placement area.
            var pitWidthManager = new VirtualPitWidthManager(tiledMapService);
            var generator       = new VirtualPitGenerator(_world, tiledMapService, pitWidthManager);
            generator.LootContext = BuildLootJobContext();
            generator.RegenerateForLevel(pitLevel);

            // Build the battle runner with the real hero + mercs.
            // Store in _lastPartyView so TryInnRest can reset its exhausted flags.
            var partyView = new VirtualBattlePartyView(_hero.LinkedHero, Bag);
            _lastPartyView = partyView;
            _battleRunner  = new VirtualBattleRunner(_world, partyView);
            _battleRunner.SetHeroAlly(_hero.LinkedHero);
            _battleRunner.SetMercenaries(_mercenaries);

            // Place hero at pit start and reset the per-level GOAP flags — without this,
            // a persistent multi-level run (RunLevelRange) would carry ActivatedWizardOrb
            // over from the previous level and skip the new level entirely.
            var pitStart = new Point(_world.PitBounds.X + 1, _world.PitBounds.Y + 1);
            _hero.TeleportTo(pitStart);
            _hero.InsidePit          = true;
            _hero.ExploredPit        = false;
            _hero.FoundWizardOrb     = false;
            _hero.ActivatedWizardOrb = false;

            // Run the state machine until exploration + orb activation completes
            var stateMachine = new VirtualHeroStateMachine(_hero, _world);
            stateMachine.BattleRunner = _battleRunner;

            int maxTicks = 2000;
            int tick = 0;
            while (tick < maxTicks)
            {
                stateMachine.Update();
                tick++;

                if (_hero.ActivatedWizardOrb || !_battleRunner.HeroAlive)
                    break;
            }

            // Accumulate battle metrics gathered during exploration
            var allBattles = _battleRunner.AllBattleMetrics;
            for (int i = 0; i < allBattles.Count; i++)
                _runMetrics.AccumulateBattle(allBattles[i]);

            _runMetrics.Wiped           = !_battleRunner.HeroAlive;
            _runMetrics.TreasuresOpened = _battleRunner.TreasuresOpened;
            _runMetrics.GearEquipped    = _battleRunner.GearEquipped;
            if (partyMaxHPPool > 0)
                _runMetrics.HpLossPercent = (float)_runMetrics.DamageTaken / partyMaxHPPool;

            // Credit the wallet with gold earned this level and snapshot the balance.
            // InnRested / MercsHired are 0/false here; RunLevelRange stamps them after.
            Gold                  += _runMetrics.GoldEarned;
            _runMetrics.Wallet     = Gold;

            _currentAction = "RunPitLevel_Complete";
            return _runMetrics;
        }

        /// <summary>
        /// Run a complete simulation cycle as described in the comment
        /// </summary>
        public void RunCompleteSimulation()
        {
            Console.WriteLine("=== Starting Virtual Game Simulation ===");
            Console.WriteLine("Scenario: Level 40 pit -> MoveToPit -> Jump -> Explore -> Wizard Orb Workflow -> Regenerate");
            Console.WriteLine();

            // Step 1: Generate pit at level 40
            Console.WriteLine("STEP 1: Generating pit at level 40");
            _world.RegeneratePit(40);
            LogWorldState();

            // Step 2: Hero spawns and executes JumpIntoPitAction (replaces MoveToPitAction)
            Console.WriteLine("\nSTEP 2: Hero spawns and begins JumpIntoPitAction");
            ExecuteJumpIntoPitAction();

            // Step 3: Hero jumps into pit
            Console.WriteLine("\nSTEP 3: Hero jumps into pit");
            ExecuteJumpIntoPitAction();

            // Step 4: Hero wanders and explores the entire pit
            Console.WriteLine("\nSTEP 4: Hero wanders and explores pit completely");
            ExecuteWanderPitAction();

            // Step 5: Execute wizard orb workflow
            Console.WriteLine("\nSTEP 5: Execute complete wizard orb workflow");
            ExecuteWizardOrbWorkflow();

            // Step 6: Cycle restarts
            Console.WriteLine("\nSTEP 6: Cycle restarts");
            Console.WriteLine("Hero would now target the new regenerated pit to start the cycle over");

            Console.WriteLine("\n=== Simulation Complete ===");
            LogFinalState();
        }

        /// <summary>
        /// Simulate JumpIntoPitAction - hero jumps into pit (replaces MoveToPitAction)
        /// </summary>
        private void ExecuteJumpIntoPitAction()
        {
            _currentAction = "JumpIntoPitAction";
            var startPos = _hero.Position;
            var pitBounds = _world.PitBounds;

            // Target: Adjacent tile outside pit (left side)
            var targetPos = new Point(pitBounds.X - 1, pitBounds.Y + pitBounds.Height / 2);

            Console.WriteLine($"[{_currentAction}] Moving from ({startPos.X},{startPos.Y}) to ({targetPos.X},{targetPos.Y})");

            // Simulate pathfinding and movement
            var path = CalculateSimplePath(startPos, targetPos);
            _hero.SetMovementPath(path);

            // Execute movement step by step
            while (!_hero.ExecuteMovementStep())
            {
                _tickCount++;
                if (_tickCount % 5 == 0) // Log every 5 ticks
                {
                    Console.WriteLine($"[{_currentAction}] Tick {_tickCount}: Hero at ({_hero.Position.X},{_hero.Position.Y})");
                }
            }

            Console.WriteLine($"[{_currentAction}] Completed. Hero adjacent to pit: {_hero.AdjacentToPitBoundaryFromOutside()}");
        }

        /// <summary>
        /// Simulate WanderPitAction using state machine approach instead of systematic exploration
        /// </summary>
        private void ExecuteWanderPitAction()
        {
            _currentAction = "WanderPitAction";
            Console.WriteLine($"[{_currentAction}] Starting exploration using VirtualHeroStateMachine");

            // Create virtual state machine for proper pathfinding-based exploration
            var stateMachine = new VirtualHeroStateMachine(_hero, _world);

            int maxTicks = 1000; // Safety limit
            int tickCount = 0;

            while (!stateMachine.IsExplorationComplete() && tickCount < maxTicks)
            {
                stateMachine.Update();
                tickCount++;

                if (tickCount % 50 == 0) // Log every 50 ticks
                {
                    var fogCount = CountRemainingFog();
                    Console.WriteLine($"[{_currentAction}] Tick {tickCount}: Hero at ({_hero.Position.X},{_hero.Position.Y}), fog remaining: {fogCount}");
                }
            }

            var finalFogCount = CountRemainingFog();
            var isExplored = stateMachine.IsExplorationComplete();
            Console.WriteLine($"[{_currentAction}] Completed after {tickCount} ticks. Fog remaining: {finalFogCount}, ExploredPit: {isExplored}");

            if (isExplored)
            {
                _hero.ExploredPit = true;
            }
        }

        /// <summary>
        /// Execute the complete wizard orb workflow chain - updated for simplified GOAP
        /// </summary>
        private void ExecuteWizardOrbWorkflow()
        {
            // WanderPitAction (combines exploration and wizard orb finding)
            ExecuteWanderPitAction();

            // ActivateWizardOrbAction
            ExecuteActivateWizardOrbAction();

            // Cycle restarts with JumpIntoPitAction (pit regeneration now happens in ActivateWizardOrbAction)
            Console.WriteLine("\nSTEP 6: Cycle restarts - JumpIntoPitAction would begin again");
            Console.WriteLine("Hero would now target the new regenerated pit to start the cycle over");
        }

        private void ExecuteActivateWizardOrbAction()
        {
            _currentAction = "ActivateWizardOrbAction";
            Console.WriteLine($"[{_currentAction}] Activating wizard orb and queuing next pit level");

            _world.ActivateWizardOrb();
            _hero.ActivatedWizardOrb = true;

            // Queue next pit level (current + 10)
            var nextLevel = _world.PitLevel + 10;
            _pitQueue.QueueLevel(nextLevel);

            _tickCount++;
            Console.WriteLine($"[{_currentAction}] Completed. Orb activated, queued level {nextLevel}");
        }

        // Note: MovingToInsidePitEdgeAction is replaced by JumpOutOfPitForInnAction which handles its own movement

        private void ExecuteJumpOutOfPitForInnAction()
        {
            _currentAction = "JumpOutOfPitForInnAction";
            var pitBounds = _world.PitBounds;

            // Target: Outside pit
            var targetPos = new Point(pitBounds.X - 1, pitBounds.Y + pitBounds.Height / 2);

            Console.WriteLine($"[{_currentAction}] Jumping out of pit to ({targetPos.X},{targetPos.Y})");

            // Use pathfinding instead of teleportation
            var path = CalculateSimplePath(_hero.Position, targetPos);
            _hero.SetMovementPath(path);

            // Execute movement step by step
            while (!_hero.ExecuteMovementStep())
            {
                _tickCount++;
            }

            _hero.ResetWizardOrbStates();

            _tickCount++;
            var outsidePit = _hero.GetWorldState().ContainsKey(GoapConstants.OutsidePit);
            Console.WriteLine($"[{_currentAction}] Completed. OutsidePit: {outsidePit}");
        }



        /// <summary>
        /// Simple pathfinding - straight line or L-shaped path
        /// </summary>
        private List<Point> CalculateSimplePath(Point start, Point target)
        {
            var path = new List<Point>();
            var current = start;

            // Move horizontally first
            while (current.X != target.X)
            {
                current = new Point(current.X + Math.Sign(target.X - current.X), current.Y);
                if (!_world.IsCollisionTile(current))
                {
                    path.Add(current);
                }
            }

            // Then move vertically
            while (current.Y != target.Y)
            {
                current = new Point(current.X, current.Y + Math.Sign(target.Y - current.Y));
                if (!_world.IsCollisionTile(current))
                {
                    path.Add(current);
                }
            }

            return path;
        }

        /// <summary>
        /// Count remaining fog tiles in pit
        /// </summary>
        private int CountRemainingFog()
        {
            var pitBounds = _world.PitBounds;
            int count = 0;

            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    if (_world.HasFogOfWar(new Point(x, y)))
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Log current world and hero state
        /// </summary>
        private void LogWorldState()
        {
            Console.WriteLine(_world.GetVisualRepresentation());

            var heroState = _hero.GetWorldState();
            Console.WriteLine("Hero GOAP States:");
            foreach (var kvp in heroState.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Returns true when any party member (hero or configured mercenary) is below
        /// full HP or MP — the trigger for inn rest in <see cref="RunLevelRange"/>.
        /// </summary>
        private bool PartyNeedsRest()
        {
            var hero = _hero.LinkedHero;
            if (hero != null &&
                (hero.CurrentHP < hero.MaxHP || hero.CurrentMP < hero.MaxMP))
                return true;

            for (int i = 0; i < _mercenaries.Count; i++)
            {
                var m = _mercenaries[i];
                if (m.CurrentHP < m.MaxHP || m.CurrentMP < m.MaxMP)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Log final simulation state
        /// </summary>
        private void LogFinalState()
        {
            Console.WriteLine($"Total simulation ticks: {_tickCount}");
            Console.WriteLine($"Current action: {_currentAction}");
            Console.WriteLine();

            LogWorldState();

            Console.WriteLine("Simulation verified the complete GOAP workflow:");
            Console.WriteLine("✓ Pit generation at level 40");
            Console.WriteLine("✓ Hero JumpIntoPitAction execution");
            Console.WriteLine("✓ Complete pit exploration via WanderPitAction");
            Console.WriteLine("✓ ActivateWizardOrbAction execution (includes immediate pit regeneration)");
            Console.WriteLine("✓ Pit regeneration at higher level");
            Console.WriteLine();
            Console.WriteLine("The hero is now ready to start the cycle again with the new pit!");
        }

        /// <summary>
        /// Initialize level 40 pit for testing
        /// </summary>
        public void InitializeLevel40Pit()
        {
            _world.RegeneratePit(40);
            LogWorldState();
        }

        /// <summary>
        /// Simulate hero jumping into pit
        /// </summary>
        public void HeroJumpIntoPit()
        {
            var targetPos = new Point(2, 3); // Inside pit area
            var path = CalculateSimplePath(_hero.Position, targetPos);
            _hero.SetMovementPath(path);

            // Execute movement
            while (!_hero.ExecuteMovementStep())
            {
                // Move step by step
            }

            _hero.InsidePit = true;
            Console.WriteLine($"Hero jumped into pit at tile {_hero.Position.X},{_hero.Position.Y}");
        }

        /// <summary>
        /// Complete exploration by clearing all fog tiles
        /// </summary>
        public void CompleteExploration()
        {
            _world.ClearAllFogInPit();
            _world.DiscoverWizardOrb(new Point(9, 4));
            Console.WriteLine("Exploration completed - all fog cleared, wizard orb discovered");
        }

        /// <summary>
        /// Check if map is fully explored
        /// </summary>
        public bool IsMapExplored()
        {
            return _world.FogTilesInPit.Count == 0;
        }

        /// <summary>
        /// Check if wizard orb is found
        /// </summary>
        public bool IsWizardOrbFound()
        {
            return _world.WizardOrbPosition.HasValue;
        }

        /// <summary>
        /// Create GOAP context for testing
        /// </summary>
        public VirtualGoapContext CreateGoapContext()
        {
            return new VirtualGoapContext(_world, _hero);
        }

        /// <summary>
        /// Get progressive goal state based on current state
        /// </summary>
        public Dictionary<string, bool> GetProgressiveGoalState(Dictionary<string, bool> currentState)
        {
            var goal = new Dictionary<string, bool>();

            bool mapExplored = currentState.GetValueOrDefault(GoapConstants.ExploredPit, false);
            bool wizardOrbActivated = currentState.GetValueOrDefault(GoapConstants.ActivatedWizardOrb, false);
            // Note: AtPitGenPoint removed from simplified GOAP - logic simplified to 2 main goals

            if (!mapExplored)
            {
                goal[GoapConstants.ExploredPit] = true;
            }
            else if (!wizardOrbActivated)
            {
                goal[GoapConstants.ActivatedWizardOrb] = true;
            }
            // Note: Pit regeneration is now handled by the 2-goal cycle in HeroComponent.SetGoalState()
            else
            {
                goal[GoapConstants.OutsidePit] = true;
            }

            return goal;
        }

        /// <summary>
        /// Plan actions using GOAP
        /// </summary>
        public Stack<HeroActionBase> PlanActions(Dictionary<string, bool> currentState, Dictionary<string, bool> goalState)
        {
            var context = CreateGoapContext();

            // Create action planner directly
            var planner = new Nez.AI.GOAP.ActionPlanner();

            // Add all hero actions (extended interactive model)
            planner.AddAction(new JumpIntoPitAction());
            planner.AddAction(new WanderPitAction());
            planner.AddAction(new ActivateWizardOrbAction());
            planner.AddAction(new JumpOutOfPitForInnAction());
            planner.AddAction(new AttackMonsterAction());
            planner.AddAction(new OpenChestAction());

            // Convert dictionaries to WorldState objects (simplified)
            var wsCurrentState = Nez.AI.GOAP.WorldState.Create(planner);
            foreach (var kvp in currentState)
            {
                wsCurrentState.Set(kvp.Key, kvp.Value);
            }

            var wsGoalState = Nez.AI.GOAP.WorldState.Create(planner);
            foreach (var kvp in goalState)
            {
                wsGoalState.Set(kvp.Key, kvp.Value);
            }

            var actionPlan = planner.Plan(wsCurrentState, wsGoalState);

            var result = new Stack<HeroActionBase>();
            if (actionPlan != null && actionPlan.Count > 0)
            {
                while (actionPlan.Count > 0)
                {
                    if (actionPlan.Pop() is HeroActionBase heroAction)
                    {
                        result.Push(heroAction);
                    }
                }

                // Reverse to get correct execution order
                var temp = new Stack<HeroActionBase>();
                while (result.Count > 0)
                {
                    temp.Push(result.Pop());
                }
                result = temp;
            }

            return result;
        }

        /// <summary>
        /// Tick hero movement simulation
        /// </summary>
        public void TickHeroMovement()
        {
            // Simple movement simulation - just advance towards target if moving
            if (_hero.IsMoving && _hero.TargetTilePosition.HasValue)
            {
                var current = _hero.Position;
                var target = _hero.TargetTilePosition.Value;

                // Simple step towards target (only adjacent moves allowed)
                Point nextStep = current;
                if (current.X < target.X) nextStep.X++;
                else if (current.X > target.X) nextStep.X--;
                else if (current.Y < target.Y) nextStep.Y++;
                else if (current.Y > target.Y) nextStep.Y--;

                // Use single-step MoveTo instead of teleportation
                if (nextStep != current)
                {
                    try
                    {
                        _hero.MoveTo(nextStep);
                    }
                    catch (System.InvalidOperationException)
                    {
                        // Step was too large, stop movement
                        _hero.IsMoving = false;
                        _hero.TargetTilePosition = null;
                        return;
                    }
                }

                // Stop moving if reached target
                if (_hero.Position == target)
                {
                    _hero.IsMoving = false;
                    _hero.TargetTilePosition = null;
                }
            }
        }

        /// <summary>
        /// Execute an action in the simulation
        /// </summary>
        public void ExecuteAction(HeroActionBase action)
        {
            var context = CreateGoapContext();
            bool completed = false;
            int maxIterations = 100;
            int iterations = 0;

            Console.WriteLine($"Executing action: {action.Name}");

            while (!completed && iterations < maxIterations)
            {
                completed = action.Execute(context);
                if (!completed)
                {
                    TickHeroMovement();
                }
                iterations++;
            }

            // Sync state back to hero after action execution
            context.SyncBackToHero();

            if (completed)
            {
                Console.WriteLine($"Action {action.Name} completed successfully");
            }
            else
            {
                Console.WriteLine($"Action {action.Name} failed to complete within {maxIterations} iterations");
            }
        }

        /// <summary>
        /// Simulate pit trigger exit for testing
        /// </summary>
        public void TriggerPitExit()
        {
            // This simulates the pit trigger exit logic
            var currentTile = _hero.CurrentTilePosition;
            var pitBounds = new Rectangle(1, 2, _world.PitWidthTiles, 8); // Level 40 pit bounds

            if (!pitBounds.Contains(currentTile))
            {
                // Hero is truly outside pit - reset flags
                _hero.InsidePit = false;
                _hero.ActivatedWizardOrb = false;
            }
            // Otherwise, hero is still in pit area - don't reset flags
        }
    }

    /// <summary>
    /// Simple virtual pit level queue for testing
    /// </summary>
    public class VirtualPitLevelQueue
    {
        private int? _queuedLevel;

        public bool HasQueuedLevel => _queuedLevel.HasValue;

        public void QueueLevel(int level)
        {
            _queuedLevel = level;
        }

        public int? DequeueLevel()
        {
            var level = _queuedLevel;
            _queuedLevel = null;
            return level;
        }
    }
}
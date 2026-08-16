using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Config;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Services.Analytics;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Central coordinator for kitchen/tavern work. Owns the worker lifecycle (cooks, servers,
    /// runners), the ticket board, the fridge inventory, cooking stations, serving table slots,
    /// and patron notifications.
    ///
    /// Flow: a server takes up to 3 orders from patrons at its tables, posts them on the ticket
    /// board (82,2); the shortfall of any recipe not covered by the fridge (87,2) is fetched
    /// proactively by a runner from Crop Storage; a cook reads one ticket at a time from the
    /// board, gathers ingredients at the fridge (waiting for the runner if short), cooks at a
    /// free station (83-85,2), and places the dish on a serving table (87,3-5) — holding it if
    /// all three are full. The server whose zone owns the table picks up (up to 2 dishes) and
    /// delivers; dishes whose patron left go to the sink (86,2).
    /// </summary>
    public class KitchenTaskCoordinator : IMonsterWorkerHost
    {
        // ── Internal types ──────────────────────────────────────────────────────

        private struct ActiveWorker
        {
            public AlliedMonster Monster;
            public Entity Entity;
            public KitchenMonsterStateMachine Fsm;
            public KitchenRole Role;
        }

        public struct BusJob
        {
            public Entity DishEntity;  // plate entity on the table to be bussed
            public Vector2 WorldPos;   // where to pick it up from
            public float EnqueuedTime; // Time.TotalTime when queued — drives anti-starvation priority
        }

        private struct OrphanDish
        {
            public int Slot;          // serving slot the dish sits on
            public Entity DishEntity; // dish entity on the serving table (patron left)
        }

        // ── Services ────────────────────────────────────────────────────────────
        private readonly AlliedMonsterManager _alliedMonsters;
        private readonly BuildingService _buildingService;
        private CropStorageInventoryService _cropStorage;
        private DroppedCropService _droppedCrops;
        private DishEntityService _dishService;
        private GameStateService _gameState;
        private MercenaryManager _mercenaryManager;

        // ── Role-mix dwell (issue #375) ─────────────────────────────────────────
        // A role change sends the worker home to respawn, so the demand-weighted mix is
        // recomputed at most once per dwell period (head-count changes recompute immediately —
        // spawns/despawns are happening anyway).
        private readonly List<KitchenRole> _cachedRoles = new List<KitchenRole>(8);
        private readonly List<int> _currentRoleByPost = new List<int>(8);
        private int _cachedPostCount = -1;
        private float _roleMixElapsed = GameConfig.KitchenRoleMixDwellSeconds;

        // Per-role pressures seesaw within a single service cycle (orders just taken → runner
        // work spikes; dishes plated → server work spikes), so an instantaneous reading at
        // recompute time hands the marginal post to whichever side of the seesaw got sampled —
        // and back again at the next dwell. EMA-smoothed pressures see the cycle's AVERAGE, so
        // the marginal post only moves on a sustained shift in where the bottleneck really is.
        private float _rolePressureSampleElapsed = GameConfig.KitchenRolePressureSampleIntervalSeconds;
        private float _smoothedCookPressure;
        private float _smoothedServerPressure;
        private float _smoothedRunnerPressure;

        // ── Workers ─────────────────────────────────────────────────────────────
        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(8);
        private readonly List<IMonsterWorkerHost> _peers = new List<IMonsterWorkerHost>(2);
        private Scene _scene;
        private float _hatCheckElapsed;

        // Scratch arrays for role assignment (pre-allocated, reset each reconcile)
        private readonly List<AlliedMonster> _wantedAssignments = new List<AlliedMonster>(8);
        private readonly List<KitchenRole> _wantedRoles = new List<KitchenRole>(8);

        // ── Pathfinder ──────────────────────────────────────────────────────────
        /// <summary>Shared A* grid for all kitchen monsters.</summary>
        public FarmPathfinder Pathfinder { get; }

        // ── Tickets / board ─────────────────────────────────────────────────────
        private const int MaxOpenTickets = 16;
        private readonly List<KitchenTicket> _tickets = new List<KitchenTicket>(MaxOpenTickets);
        private int _nextTicketId;

        // ── Fridge inventory (kitchen-local crop stock, slot-based — issue #386) ──
        private FridgeInventoryService _fridgeInv;

        // ── Pre-stock jobs (runners keep N stacks of each available crop stocked) ──
        private readonly List<CropType> _preStockQueue = new List<CropType>(16);
        private readonly bool[] _preStockBusy = new bool[CropTypeInfo.Count]; // queued OR claimed
        private static bool[] _recipeCropMask; // lazy: crops used by at least one dish recipe
        private float _preStockCheckElapsed;

        // ── Runner fetch queue (tickets whose storage-taken share needs transport) ──
        private readonly List<KitchenTicket> _fetchQueue = new List<KitchenTicket>(8);

        // ── Cooking stations ────────────────────────────────────────────────────
        private readonly KitchenTicket[] _stationTicket = new KitchenTicket[GameConfig.MaxKitchenCooks];

        // ── Serving table orphans (cooked dishes whose patron left) ─────────────
        private readonly List<OrphanDish> _orphanServing = new List<OrphanDish>(4);

        // ── Bus queue ───────────────────────────────────────────────────────────
        private readonly List<BusJob> _busJobs = new List<BusJob>(8);

        // ── Party order source ──────────────────────────────────────────────────
        private IPartyOrderSource _partyOrderSource;

        // ── Kitchen open/closed ─────────────────────────────────────────────────
        private int _cook1WorkerIdx = -1;
        private int _server1WorkerIdx = -1;
        private int _runner1WorkerIdx = -1;

        /// <summary>True when ≥1 cook and ≥1 server are assigned and awake.</summary>
        public bool IsKitchenOpen => _cook1WorkerIdx >= 0 && _server1WorkerIdx >= 0;

        /// <summary>
        /// True when ≥1 runner is on shift. Runners own plate bussing; servers only fall back to
        /// it while this is false, so a cook+server-only kitchen still clears its tables.
        /// </summary>
        public bool HasActiveRunner => _runner1WorkerIdx >= 0;

        /// <summary>Total kitchen role posts — the cap on simultaneous kitchen workers.</summary>
        public static int MaxWorkerPosts =>
            GameConfig.MaxKitchenCooks + GameConfig.MaxKitchenServers + GameConfig.MaxKitchenRunners;

        /// <summary>
        /// Neutral role mix (no pressure signals): cycles Cook → Server → Runner, yielding
        /// cook1, server1, runner1, cook2, server2, runner2, cook3, runner3.
        /// </summary>
        public static void FillRoleMix(int postCount, List<KitchenRole> into)
            => FillRoleMix(postCount, 0, 0, 0, into);

        /// <summary>
        /// Fills <paramref name="into"/> with the role for each of the first postCount posts.
        /// Posts 0–2 are always Cook, Server, Runner — the base crew: a two-monster kitchen is a
        /// cook and a server (the pair that opens it), the third is the runner that keeps the
        /// fridge stocked and the tables clear. Posts beyond the base crew go to the role with
        /// the highest pressure per worker already assigned to it (D'Hondt greedy, issue #375),
        /// honoring the per-role caps; roles with zero pressure fall back to the neutral
        /// Cook → Server → Runner cycle. Stations and zones are then claimed dynamically by
        /// the FSMs. This is the from-scratch reference mix; live recomputes go through
        /// ReconcileRoleMix instead so occupied posts never chase pressure noise.
        /// </summary>
        public static void FillRoleMix(int postCount, int cookPressure, int serverPressure,
            int runnerPressure, List<KitchenRole> into)
        {
            if (postCount > MaxWorkerPosts)
                postCount = MaxWorkerPosts;
            int cooks = 0, servers = 0, runners = 0;

            if (postCount >= 1) { cooks++; into.Add(KitchenRole.Cook); }
            if (postCount >= 2) { servers++; into.Add(KitchenRole.Server); }
            if (postCount >= 3) { runners++; into.Add(KitchenRole.Runner); }

            int cursor = 0;
            for (int i = 3; i < postCount; i++)
            {
                // Highest pressure/(count+1) among roles under cap, compared by integer
                // cross-multiplication; strict > breaks ties toward Cook, then Server, then Runner.
                int bestRole = -1, bestNum = 0, bestDen = 1;
                if (cooks < GameConfig.MaxKitchenCooks)
                {
                    bestRole = 0; bestNum = cookPressure; bestDen = cooks + 1;
                }
                if (servers < GameConfig.MaxKitchenServers
                    && (bestRole < 0 || serverPressure * bestDen > bestNum * (servers + 1)))
                {
                    bestRole = 1; bestNum = serverPressure; bestDen = servers + 1;
                }
                if (runners < GameConfig.MaxKitchenRunners
                    && (bestRole < 0 || runnerPressure * bestDen > bestNum * (runners + 1)))
                {
                    bestRole = 2; bestNum = runnerPressure; bestDen = runners + 1;
                }

                if (bestRole >= 0 && bestNum > 0)
                {
                    if (bestRole == 0) { cooks++; into.Add(KitchenRole.Cook); }
                    else if (bestRole == 1) { servers++; into.Add(KitchenRole.Server); }
                    else { runners++; into.Add(KitchenRole.Runner); }
                    continue;
                }

                // No pressured role can take the post — continue the neutral cycle. postCount
                // never exceeds the sum of the per-role caps, so this always resolves.
                while (true)
                {
                    int slot = cursor % 3;
                    cursor++;
                    if (slot == 0 && cooks < GameConfig.MaxKitchenCooks)
                    {
                        cooks++; into.Add(KitchenRole.Cook); break;
                    }
                    if (slot == 1 && servers < GameConfig.MaxKitchenServers)
                    {
                        servers++; into.Add(KitchenRole.Server); break;
                    }
                    if (slot == 2 && runners < GameConfig.MaxKitchenRunners)
                    {
                        runners++; into.Add(KitchenRole.Runner); break;
                    }
                }
            }
        }

        /// <summary>
        /// Incremental role-mix update (issue #375 follow-up): starts from the previous mix's
        /// role COUNTS and only (a) adds posts when the crew grew — base-crew floors first, then
        /// D'Hondt greedy on the smoothed pressures, least-staffed role when nothing is
        /// pressured — (b) removes the lowest pressure-per-worker role above its floor when the
        /// crew shrank, and (c) moves at most ONE occupied post per recompute between roles, and
        /// only when the gaining role's smoothed pressure exceeds the losing role's by
        /// GameConfig.KitchenRoleMixSwitchMargin AND the move strictly improves the D'Hondt
        /// balance. Early service the entire kitchen signal is a single ticket pulsing
        /// runner → cook → server pressure as it moves through the pipeline; recomputing the mix
        /// from scratch (FillRoleMix) made the extra posts chase that 0↔1 pulse, and every chase
        /// is a walk-home/respawn round trip. With the margin, pulse noise can never flip an
        /// occupied post, while a sustained multi-ticket imbalance still re-skews the crew one
        /// post per dwell period. Output order: posts 0–2 are Cook, Server, Runner (the
        /// base-crew invariant), then extras grouped by role — AssignRolesWithRetention only
        /// consumes the counts, so extra ordering is cosmetic.
        /// </summary>
        public static void ReconcileRoleMix(int postCount, float cookPressure,
            float serverPressure, float runnerPressure, int prevCooks, int prevServers,
            int prevRunners, List<KitchenRole> into)
        {
            if (postCount > MaxWorkerPosts)
                postCount = MaxWorkerPosts;
            if (postCount < 0)
                postCount = 0;

            Span<int> caps = stackalloc int[3]
            {
                GameConfig.MaxKitchenCooks, GameConfig.MaxKitchenServers, GameConfig.MaxKitchenRunners
            };
            Span<float> pressure = stackalloc float[3] { cookPressure, serverPressure, runnerPressure };
            Span<int> counts = stackalloc int[3] { prevCooks, prevServers, prevRunners };
            Span<int> floors = stackalloc int[3]
            {
                postCount >= 1 ? 1 : 0, postCount >= 2 ? 1 : 0, postCount >= 3 ? 1 : 0
            };

            for (int r = 0; r < 3; r++)
            {
                if (counts[r] > caps[r]) counts[r] = caps[r];
                if (counts[r] < 0) counts[r] = 0;
            }
            int total = counts[0] + counts[1] + counts[2];

            // Grow — new posts are fresh spawns, so assigning them by pressure is churn-free.
            while (total < postCount)
            {
                int pick = -1;
                for (int r = 0; r < 3; r++)
                    if (counts[r] < floors[r]) { pick = r; break; }
                if (pick < 0)
                {
                    float bestScore = 0f;
                    for (int r = 0; r < 3; r++)
                    {
                        if (counts[r] >= caps[r])
                            continue;
                        float score = pressure[r] / (counts[r] + 1);
                        if (score > bestScore) { bestScore = score; pick = r; }
                    }
                }
                if (pick < 0)
                {
                    for (int r = 0; r < 3; r++)
                        if (counts[r] < caps[r] && (pick < 0 || counts[r] < counts[pick]))
                            pick = r;
                }
                counts[pick]++;
                total++;
            }

            // Shrink — drop the lowest pressure-per-worker role above its floor; ties drop the
            // most-staffed role (undoing neutral extras symmetrically), then Runner, Server, Cook.
            while (total > postCount)
            {
                int pick = -1;
                for (int r = 2; r >= 0; r--)
                {
                    if (counts[r] <= floors[r])
                        continue;
                    if (pick < 0) { pick = r; continue; }
                    float cur = pressure[r] / counts[r];
                    float best = pressure[pick] / counts[pick];
                    if (cur < best || (cur == best && counts[r] > counts[pick]))
                        pick = r;
                }
                counts[pick]--;
                total--;
            }

            // Repair — a stale mix can under-fill a base-crew role after roster churn; refill it
            // from the lowest-pressure role above its floor (always feasible: Σfloors ≤ postCount).
            for (int r = 0; r < 3; r++)
            {
                while (counts[r] < floors[r])
                {
                    int from = -1;
                    for (int o = 2; o >= 0; o--)
                    {
                        if (o == r || counts[o] <= floors[o])
                            continue;
                        if (from < 0 || pressure[o] / counts[o] < pressure[from] / counts[from])
                            from = o;
                    }
                    counts[from]--;
                    counts[r]++;
                }
            }

            // Rebalance — the only path that reassigns an occupied post, margin-gated and
            // limited to one move per recompute.
            int gain = -1;
            float gainScore = 0f;
            for (int r = 0; r < 3; r++)
            {
                if (counts[r] >= caps[r])
                    continue;
                float score = pressure[r] / (counts[r] + 1);
                if (score > gainScore) { gainScore = score; gain = r; }
            }
            int lose = -1;
            for (int r = 2; r >= 0; r--)
            {
                if (r == gain || counts[r] <= floors[r])
                    continue;
                if (lose < 0) { lose = r; continue; }
                float cur = pressure[r] / counts[r];
                float best = pressure[lose] / counts[lose];
                if (cur < best || (cur == best && counts[r] > counts[lose]))
                    lose = r;
            }
            if (gain >= 0 && lose >= 0
                && pressure[gain] - pressure[lose] >= GameConfig.KitchenRoleMixSwitchMargin
                && pressure[gain] * counts[lose] > pressure[lose] * (counts[gain] + 1))
            {
                counts[gain]++;
                counts[lose]--;
            }

            if (postCount >= 1) into.Add(KitchenRole.Cook);
            if (postCount >= 2) into.Add(KitchenRole.Server);
            if (postCount >= 3) into.Add(KitchenRole.Runner);
            for (int i = 1; i < counts[0]; i++) into.Add(KitchenRole.Cook);
            for (int i = 1; i < counts[1]; i++) into.Add(KitchenRole.Server);
            for (int i = 1; i < counts[2]; i++) into.Add(KitchenRole.Runner);
        }

        /// <summary>
        /// Maps the role mix onto the actual posts with minimum churn (issue #375 follow-up): the
        /// mix is treated as a multiset of role COUNTS, and a live worker keeps its current role
        /// as long as that role still has quota — quota is consumed in post order (= proficiency
        /// order), so when a role shrinks, the best holders keep it and only the worst is
        /// reassigned. Remaining quota goes to unmatched posts in Cook → Server → Runner order.
        /// Without this, roles were tied to sorted-list positions, and any recompute could flip
        /// two positions' roles — sending BOTH workers home to respawn in each other's role, a
        /// pure-waste "cook leaves, different cook walks in" shuffle.
        /// currentRoleByPost[j] = (int)KitchenRole of post j's live worker, or -1 if none.
        /// </summary>
        public static void AssignRolesWithRetention(List<KitchenRole> mix,
            List<int> currentRoleByPost, List<KitchenRole> into)
        {
            int cooks = 0, servers = 0, runners = 0;
            for (int j = 0; j < mix.Count; j++)
            {
                if (mix[j] == KitchenRole.Cook) cooks++;
                else if (mix[j] == KitchenRole.Server) servers++;
                else runners++;
            }

            // Pass 1: retention. keptMask bit j = post j kept its current role (mix.Count ≤ 8).
            int keptMask = 0;
            for (int j = 0; j < mix.Count; j++)
            {
                into.Add(KitchenRole.Cook);   // placeholder; every post is overwritten below
                int current = currentRoleByPost[j];
                if (current == (int)KitchenRole.Cook && cooks > 0)
                {
                    cooks--; into[j] = KitchenRole.Cook; keptMask |= 1 << j;
                }
                else if (current == (int)KitchenRole.Server && servers > 0)
                {
                    servers--; into[j] = KitchenRole.Server; keptMask |= 1 << j;
                }
                else if (current == (int)KitchenRole.Runner && runners > 0)
                {
                    runners--; into[j] = KitchenRole.Runner; keptMask |= 1 << j;
                }
            }

            // Pass 2: remaining quota to unmatched posts, best proficiency toward Cook first.
            for (int j = 0; j < mix.Count; j++)
            {
                if ((keptMask & (1 << j)) != 0)
                    continue;
                if (cooks > 0) { cooks--; into[j] = KitchenRole.Cook; }
                else if (servers > 0) { servers--; into[j] = KitchenRole.Server; }
                else { runners--; into[j] = KitchenRole.Runner; }
            }
        }

        /// <summary>Number of open kitchen tickets (any state) — the order backlog.</summary>
        public int ActiveTicketCount => _tickets.Count;

        // ── Per-role backpressure signals (issue #375) ──────────────────────────
        // Each counter isolates the pipeline stage one role is responsible for, so the role mix
        // can put extra posts where the actual bottleneck is.

        /// <summary>Tickets stalled until a runner delivers their storage shortfall — runner pressure.</summary>
        public int AwaitingIngredientsTicketCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _tickets.Count; i++)
                    if (_tickets[i].State == TicketState.AwaitingIngredients)
                        count++;
                return count;
            }
        }

        /// <summary>Posted tickets no cook has picked up yet — cook pressure.</summary>
        public int ReadyToCookUnclaimedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _tickets.Count; i++)
                {
                    var t = _tickets[i];
                    if (t.PostedToBoard && !t.CookClaimed
                        && (t.State == TicketState.ReadyToCook || t.State == TicketState.AwaitingIngredients))
                        count++;
                }
                return count;
            }
        }

        /// <summary>Cooked dishes sitting on serving tables waiting for a server — server pressure.</summary>
        public int PlatedAwaitingPickupCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _tickets.Count; i++)
                    if (_tickets[i].State == TicketState.Plated)
                        count++;
                return count;
            }
        }

        /// <summary>Tickets queued for a runner's storage→fridge trip — runner pressure.</summary>
        public int FetchQueueDepth => _fetchQueue.Count;

        /// <summary>Empty plates queued for bussing — runner pressure.</summary>
        public int BusJobCount => _busJobs.Count;

        /// <summary>Age in seconds of the oldest open ticket, or 0 with an empty board.</summary>
        public float OldestOpenTicketAgeSeconds(float nowTotalTime)
        {
            float oldest = 0f;
            for (int i = 0; i < _tickets.Count; i++)
            {
                float age = nowTotalTime - _tickets[i].CreatedTime;
                if (age > oldest)
                    oldest = age;
            }
            return oldest;
        }

        /// <summary>True while the ticket board has room for another order.</summary>
        public bool HasTicketCapacity => _tickets.Count < MaxOpenTickets;

        /// <summary>
        /// True when at least one dish is fully coverable from fridge + storage. Servers must
        /// check this (and HasTicketCapacity) before walking to a waiting patron: when no order
        /// can possibly be created, targeting the patron anyway livelocks the server standing at
        /// the seat, re-selecting the same patron every decide pass.
        /// </summary>
        public bool HasAnyOrderableDish()
        {
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                if (CanCoverRecipe((DishType)d))
                    return true;
            }
            return false;
        }

        // ── Constructor ─────────────────────────────────────────────────────────

        public KitchenTaskCoordinator(AlliedMonsterManager alliedMonsters,
            BuildingService buildingService,
            int mapWidthTiles, int mapHeightTiles,
            Nez.Tiled.TmxLayer collisionLayer = null)
        {
            _alliedMonsters = alliedMonsters;
            _buildingService = buildingService;
            Pathfinder = new FarmPathfinder(mapWidthTiles, mapHeightTiles);
            Pathfinder.SeedStaticWalls(collisionLayer);
            if (buildingService != null)
            {
                Pathfinder.RebuildWalls(buildingService);
                buildingService.BuildingsChanged += HandleBuildingsChanged;
            }
        }

        /// <summary>Provides the scene used to spawn kitchen worker entities.</summary>
        public void Initialize(Scene scene)
        {
            _scene = scene;
            EnsureServices();
        }

        /// <summary>Unsubscribes from service events. Call when the scene is torn down.</summary>
        public void Detach()
        {
            if (_buildingService != null)
                _buildingService.BuildingsChanged -= HandleBuildingsChanged;
        }

        /// <summary>
        /// Registers a coordinator for another job. A kitchen worker is only spawned once no peer
        /// still has a live entity for the monster (its old-job worker walked home and despawned).
        /// </summary>
        public void AddPeer(IMonsterWorkerHost peer)
        {
            if (peer != null && !ReferenceEquals(peer, this))
                _peers.Add(peer);
        }

        /// <inheritdoc/>
        public bool HasLiveWorkerFor(AlliedMonster monster)
        {
            for (int i = 0; i < _workers.Count; i++)
                if (ReferenceEquals(_workers[i].Monster, monster) && !_workers[i].Entity.IsDestroyed)
                    return true;
            return false;
        }

        private bool AnyPeerHasLiveWorkerFor(AlliedMonster monster)
        {
            for (int i = 0; i < _peers.Count; i++)
                if (_peers[i].HasLiveWorkerFor(monster))
                    return true;
            return false;
        }

        // ── Per-frame tick ───────────────────────────────────────────────────────

        /// <summary>Per-frame tick: reconciles worker assignments and reaps destroyed entities.</summary>
        public void Update()
        {
            if (_alliedMonsters == null || _scene == null)
                return;

            var timeService = Core.Services.GetService<InGameTimeService>();

            // Build the wanted assignment list (sorted by CookingProficiency descending).
            // Insertion sort — allocation-free, small list.
            _wantedAssignments.Clear();
            _wantedRoles.Clear();

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m.Job != MonsterJob.Cooking)
                    continue;
                if (MonsterScheduleConfig.IsAsleep(m.MonsterTypeName, timeService))
                    continue;

                int insertPos = _wantedAssignments.Count;
                for (int j = 0; j < _wantedAssignments.Count; j++)
                {
                    if (m.CookingProficiency > _wantedAssignments[j].CookingProficiency)
                    {
                        insertPos = j;
                        break;
                    }
                }
                _wantedAssignments.Insert(insertPos, m);
            }

            int postCount = _wantedAssignments.Count < MaxWorkerPosts
                ? _wantedAssignments.Count : MaxWorkerPosts;

            _preStockCheckElapsed += Time.DeltaTime;
            if (_preStockCheckElapsed >= GameConfig.KitchenPreStockCheckIntervalSeconds)
            {
                _preStockCheckElapsed = 0f;
                RecomputePreStockDeficits();
            }

            _rolePressureSampleElapsed += Time.DeltaTime;
            if (_rolePressureSampleElapsed >= GameConfig.KitchenRolePressureSampleIntervalSeconds)
            {
                _rolePressureSampleElapsed = 0f;
                EnsureServices();
                // AwaitingIngredients tickets already cover the queued fetch jobs, so the fetch
                // queue isn't added again on top of them.
                int rawRunner = AwaitingIngredientsTicketCount + BusJobCount + PreStockQueueDepth;
                int rawCook = ReadyToCookUnclaimedCount;
                int rawServer = PlatedAwaitingPickupCount
                    + (_mercenaryManager != null ? _mercenaryManager.CountPatronsWaitingToOrder() : 0);
                float alpha = GameConfig.KitchenRolePressureEmaAlpha;
                _smoothedCookPressure += (rawCook - _smoothedCookPressure) * alpha;
                _smoothedServerPressure += (rawServer - _smoothedServerPressure) * alpha;
                _smoothedRunnerPressure += (rawRunner - _smoothedRunnerPressure) * alpha;
            }

            _roleMixElapsed += Time.DeltaTime;
            if (postCount != _cachedPostCount || _roleMixElapsed >= GameConfig.KitchenRoleMixDwellSeconds)
            {
                _roleMixElapsed = 0f;
                _cachedPostCount = postCount;
                // Recompute incrementally from the previous mix's counts — never from scratch.
                // A from-scratch FillRoleMix here chased single-ticket pressure pulses early in
                // service, flipping occupied posts (walk-home/respawn round trips) every dwell.
                int prevCooks = 0, prevServers = 0, prevRunners = 0;
                for (int j = 0; j < _cachedRoles.Count; j++)
                {
                    if (_cachedRoles[j] == KitchenRole.Cook) prevCooks++;
                    else if (_cachedRoles[j] == KitchenRole.Server) prevServers++;
                    else prevRunners++;
                }
                _cachedRoles.Clear();
                ReconcileRoleMix(postCount, _smoothedCookPressure, _smoothedServerPressure,
                    _smoothedRunnerPressure, prevCooks, prevServers, prevRunners, _cachedRoles);
            }

            // Map the mix's role COUNTS onto posts so live workers keep their roles wherever the
            // quota allows — only genuine count changes cause a walk-home/respawn.
            _currentRoleByPost.Clear();
            for (int j = 0; j < postCount; j++)
            {
                int currentRole = -1;
                for (int wi = 0; wi < _workers.Count; wi++)
                {
                    if (ReferenceEquals(_workers[wi].Monster, _wantedAssignments[j])
                        && !_workers[wi].Entity.IsDestroyed)
                    {
                        currentRole = (int)_workers[wi].Role;
                        break;
                    }
                }
                _currentRoleByPost.Add(currentRole);
            }
            AssignRolesWithRetention(_cachedRoles, _currentRoleByPost, _wantedRoles);

            // SpawnWorker appends to _workers mid-pass, so snapshot the count and never index past it.
            int existingWorkerCount = _workers.Count;

            for (int wi = 0; wi < existingWorkerCount; wi++)
            {
                var w = _workers[wi];
                int wantedIdx = -1;
                for (int j = 0; j < _wantedAssignments.Count; j++)
                {
                    if (ReferenceEquals(_wantedAssignments[j], w.Monster))
                    {
                        wantedIdx = j;
                        break;
                    }
                }

                if (wantedIdx < 0 || wantedIdx >= postCount)
                {
                    w.Fsm.RequestReturnHome();
                }
                else if (w.Role == _wantedRoles[wantedIdx])
                {
                    w.Fsm.CancelReturnHome();
                }
                else
                {
                    // Role changed — send home; will be respawned next reconcile. Log only on the
                    // transition (this branch repeats every frame until the worker despawns).
                    if (!w.Fsm.IsReturningHome)
                    {
                        AnalyticsService.LogKitchenRoleChanged(w.Monster.Name, w.Monster.MonsterTypeName,
                            w.Role.ToString(), _wantedRoles[wantedIdx].ToString());
                    }
                    w.Fsm.RequestReturnHome();
                }
            }

            for (int j = 0; j < postCount; j++)
            {
                var monster = _wantedAssignments[j];
                // A live worker for this monster is either the matched one keeping its role, or
                // one still walking home (role change here, or a farm worker after a job change —
                // peers). Either way, never spawn until it's gone: one entity per monster, ever.
                if (!HasLiveWorkerFor(monster) && !AnyPeerHasLiveWorkerFor(monster))
                    SpawnWorker(monster, _wantedRoles[j]);
            }

            // Reap workers whose entities finished despawning
            for (int wi = _workers.Count - 1; wi >= 0; wi--)
            {
                if (_workers[wi].Entity.IsDestroyed)
                    _workers.RemoveAt(wi);
            }

            // Update kitchen-open post indices (first cook, first server, first runner)
            _cook1WorkerIdx = -1;
            _server1WorkerIdx = -1;
            _runner1WorkerIdx = -1;
            for (int wi = 0; wi < _workers.Count; wi++)
            {
                if (_workers[wi].Fsm.IsReturningHome)
                    continue;
                if (_cook1WorkerIdx < 0 && _workers[wi].Role == KitchenRole.Cook)
                    _cook1WorkerIdx = wi;
                if (_server1WorkerIdx < 0 && _workers[wi].Role == KitchenRole.Server)
                    _server1WorkerIdx = wi;
                if (_runner1WorkerIdx < 0 && _workers[wi].Role == KitchenRole.Runner)
                    _runner1WorkerIdx = wi;
            }

            // Periodic hat sweep: shift overlaps can leave a worker hatless at spawn
            _hatCheckElapsed += Time.DeltaTime;
            if (_hatCheckElapsed >= GameConfig.KitchenHatCheckIntervalSeconds)
            {
                _hatCheckElapsed = 0f;
                for (int wi = 0; wi < _workers.Count; wi++)
                    _workers[wi].Fsm.EnsureHat();
            }
        }

        // ── Worker spawning ──────────────────────────────────────────────────────

        private void SpawnWorker(AlliedMonster monster, KitchenRole role)
        {
            var house = FindMonsterHouse(monster.MonsterHouseId);
            if (house == null)
                house = FindMonsterHouseWithCapacity();
            if (house == null)
                return;

            var doorTile = new Point(house.TileX, house.TileY + 2);
            var position = new Vector2(
                doorTile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                doorTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);

            string typeName = monster.MonsterTypeName.StartsWith("Monster_")
                ? monster.MonsterTypeName.Substring("Monster_".Length)
                : monster.MonsterTypeName;

            var entity = _scene.CreateEntity("kitchen-monster-" + monster.Name);
            entity.SetPosition(position);

            // No collider, no TAG_MONSTER — kitchen workers must never trigger battles
            var bodyAnimator = entity.AddComponent(new NamedMonsterAnimationComponent(typeName, Color.White));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            entity.AddComponent(new ActorFacingComponent());
            entity.AddComponent(new FarmMonsterMover());

            // Carry renderers: center dish/crop plus left/right side crops for runner hauls
            var carryRenderer = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryRenderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryRenderer.SetEnabled(false);
            var carryLeft = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryLeft.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryLeft.SetEnabled(false);
            var carryRight = entity.AddComponent(new Nez.Sprites.SpriteRenderer());
            carryRight.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
            carryRight.SetEnabled(false);

            // Speech bubble — AnchorRenderer drives bubble height from the monster sprite
            var kitchenBubble = entity.AddComponent(new SpeechBubbleComponent());
            kitchenBubble.AnchorRenderer = bodyAnimator;

            var fsm = entity.AddComponent(new KitchenMonsterStateMachine(
                monster, this, new Point(house.TileX, house.TileY), role));
            fsm.BodyAnimator = bodyAnimator;
            fsm.CarryRenderer = carryRenderer;
            fsm.CarryLeftRenderer = carryLeft;
            fsm.CarryRightRenderer = carryRight;

            var worker = new ActiveWorker
            {
                Monster = monster,
                Entity = entity,
                Fsm = fsm,
                Role = role,
            };
            _workers.Add(worker);

            Debug.Log($"[KitchenTaskCoordinator] Spawned {role} monster '{monster.Name}' ({typeName})");
        }

        private PlacedBuilding FindMonsterHouse(int uniqueId)
        {
            if (uniqueId < 0 || _buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].UniqueId == uniqueId && all[i].Type == BuildingType.MonsterHouse)
                    return all[i];
            return null;
        }

        private PlacedBuilding FindMonsterHouseWithCapacity()
        {
            if (_buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.MonsterHouse)
                    continue;
                if (!_alliedMonsters.IsHouseFull(all[i].UniqueId))
                    return all[i];
            }
            return null;
        }

        // ── Server zones ─────────────────────────────────────────────────────────

        /// <summary>
        /// The zone a server currently works: one active server works all 4 tables; with two,
        /// the first (staffing order) works the top tables and the second the bottom tables.
        /// Recomputed on demand, so zone handoffs on staffing changes are automatic — the
        /// current zone owner finishes whatever the previous owner started there.
        /// </summary>
        public ServerZone GetServerZone(KitchenMonsterStateMachine fsm)
        {
            int myOrder = -1;
            int activeServers = 0;
            for (int i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Role != KitchenRole.Server || _workers[i].Fsm.IsReturningHome)
                    continue;
                if (ReferenceEquals(_workers[i].Fsm, fsm))
                    myOrder = activeServers;
                activeServers++;
            }
            if (activeServers <= 1)
                return ServerZone.AllTables;
            return myOrder == 0 ? ServerZone.TopTables : ServerZone.BottomTables;
        }

        /// <summary>True when the zone covers the given table tile.</summary>
        public static bool ZoneContainsTable(ServerZone zone, Point tableTile)
        {
            switch (zone)
            {
                case ServerZone.TopTables:    return tableTile.Y <= GameConfig.TavernTopZoneMaxTileY;
                case ServerZone.BottomTables: return tableTile.Y >= GameConfig.TavernBottomZoneMinTileY;
                default:                      return true;
            }
        }

        // ── Fridge inventory ─────────────────────────────────────────────────────

        /// <summary>Units of the crop currently in the kitchen fridge.</summary>
        public int FridgeCount(CropType crop)
        {
            EnsureServices();
            return _fridgeInv?.Count(crop) ?? 0;
        }

        /// <summary>
        /// Adds crops to the fridge; any overflow that doesn't fit the bounded fridge (e.g. a
        /// ticket-cancel refund into a full fridge) spills back to crop storage so crops are
        /// never destroyed.
        /// </summary>
        private void FridgeAdd(CropType crop, int amount)
        {
            if (amount <= 0) return;
            EnsureServices();
            int stored = _fridgeInv?.Deposit(crop, amount) ?? 0;
            int overflow = amount - stored;
            if (overflow > 0)
                _cropStorage?.DepositAcrossBuildings(crop, overflow);
        }

        private int FridgeTake(CropType crop, int amount)
        {
            if (amount <= 0) return 0;
            EnsureServices();
            return _fridgeInv?.Withdraw(crop, amount) ?? 0;
        }

        /// <summary>Fridge units targeted per available crop: slider stacks × flat stack size.</summary>
        private int PreStockTargetUnits()
            => (_fridgeInv?.PreStockStackSize ?? 1) * GameConfig.KitchenFridgeStackSize;

        // ── Ticket API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a ticket for an order taken at <paramref name="seatTile"/>, reserving the
        /// ingredients (fridge stock first, storage shortfall withdrawn all-or-nothing). If any
        /// shortfall was withdrawn from storage, the ticket enters the runner fetch queue
        /// immediately (proactive fetch — the runner starts as soon as the order is taken).
        /// The ticket is NOT visible to cooks until the server posts it at the board.
        /// Returns null if ingredients cannot be covered or the queue is full.
        /// </summary>
        public KitchenTicket CreateTicket(DishType dish, bool isParty, int partySlot,
            Entity patronEntity, Point seatTile)
        {
            if (_tickets.Count >= MaxOpenTickets)
                return null;

            EnsureServices();
            var def = DishConfig.GetDefinition(dish);

            // All-or-nothing availability check (fridge + storage; dairy is free and not in Recipe)
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int available = FridgeCount(def.Recipe[i].Crop)
                    + (_cropStorage?.CountTotal(def.Recipe[i].Crop) ?? 0);
                if (available < def.Recipe[i].Qty)
                    return null;
            }

            var fridgeTaken = new int[def.Recipe.Length];
            var storageTaken = new int[def.Recipe.Length];
            int storageTotal = 0;
            List<int> sourceBuildings = null;

            for (int i = 0; i < def.Recipe.Length; i++)
            {
                var crop = def.Recipe[i].Crop;
                int need = def.Recipe[i].Qty;
                fridgeTaken[i] = FridgeTake(crop, need);
                int shortfall = need - fridgeTaken[i];
                if (shortfall > 0)
                {
                    if (sourceBuildings == null)
                        sourceBuildings = new List<int>(2);
                    if (!(_cropStorage?.TryWithdrawAcrossBuildings(crop, shortfall, sourceBuildings) ?? false))
                    {
                        // Availability changed mid-withdraw — roll everything back
                        for (int r = 0; r <= i; r++)
                        {
                            FridgeAdd(def.Recipe[r].Crop, fridgeTaken[r]);
                            if (storageTaken[r] > 0)
                                _cropStorage?.DepositAcrossBuildings(def.Recipe[r].Crop, storageTaken[r]);
                        }
                        return null;
                    }
                    storageTaken[i] = shortfall;
                    storageTotal += shortfall;
                }
            }

            var ticket = new KitchenTicket
            {
                TicketId = ++_nextTicketId,
                CreatedTime = Time.TotalTime,
                Dish = dish,
                IsPartyTicket = isParty,
                PartySlot = partySlot,
                PatronEntity = patronEntity,
                SeatTile = seatTile,
                TableTile = TavernSeatConfig.GetTableTile(seatTile),
                FridgeTakenQty = fridgeTaken,
                StorageTakenQty = storageTaken,
                SourceBuildingIds = sourceBuildings,
                IngredientsFetched = storageTotal == 0,
            };
            ticket.State = ticket.IngredientsFetched
                ? TicketState.ReadyToCook
                : TicketState.AwaitingIngredients;
            _tickets.Add(ticket);

            // Proactive runner: queue the transport as soon as the order exists
            if (!ticket.IngredientsFetched)
                _fetchQueue.Add(ticket);

            // Fridge stock just dropped — queue pre-stock refills immediately (issue #386)
            RecomputePreStockDeficits();

            return ticket;
        }

        /// <summary>
        /// Creates a ticket WITHOUT reserving ingredients (save-reload path — crops were already
        /// deducted before the save). Enters the board immediately as ReadyToCook.
        /// </summary>
        public KitchenTicket CreateTicketPreReserved(DishType dish, int partySlot)
        {
            if (_tickets.Count >= MaxOpenTickets)
                return null;

            var def = DishConfig.GetDefinition(dish);
            var storageTaken = new int[def.Recipe.Length];
            for (int i = 0; i < def.Recipe.Length; i++)
                storageTaken[i] = def.Recipe[i].Qty; // cancel refunds the full recipe to storage

            var seat = GetPartySeatTile(partySlot);
            var ticket = new KitchenTicket
            {
                TicketId = ++_nextTicketId,
                CreatedTime = Time.TotalTime,
                Dish = dish,
                IsPartyTicket = true,
                PartySlot = partySlot,
                SeatTile = seat,
                TableTile = TavernSeatConfig.GetTableTile(seat),
                FridgeTakenQty = new int[def.Recipe.Length],
                StorageTakenQty = storageTaken,
                IngredientsFetched = true,
                PostedToBoard = true,
                State = TicketState.ReadyToCook,
            };
            _tickets.Add(ticket);
            RecomputePreStockDeficits();
            return ticket;
        }

        /// <summary>Server posts a taken order on the ticket board — cooks can now read it.</summary>
        public void PostTicket(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;
            t.PostedToBoard = true;
        }

        /// <summary>
        /// Cancels a ticket at any stage. Pre-cooking: refunds fridge-taken units to the fridge
        /// and storage-taken units to storage. After cooking started: the patron still pays.
        /// A plated dish becomes an orphan the zone server carries to the sink; a delivered dish
        /// becomes a bus job.
        /// </summary>
        public void CancelTicket(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;

            if (t.CropsRefundable)
            {
                EnsureServices();
                var def = DishConfig.GetDefinition(t.Dish);
                for (int i = 0; i < def.Recipe.Length; i++)
                {
                    if (t.FridgeTakenQty != null && t.FridgeTakenQty[i] > 0)
                        FridgeAdd(def.Recipe[i].Crop, t.FridgeTakenQty[i]);
                    if (t.StorageTakenQty != null && t.StorageTakenQty[i] > 0)
                        _cropStorage?.DepositAcrossBuildings(def.Recipe[i].Crop, t.StorageTakenQty[i]);
                }
            }
            else if (!t.IsPartyTicket)
            {
                // Patron left after cooking started (patience expired or hired mid-dining):
                // the ingredients are spent, the dish is made — payment is still collected (no tip)
                EnsureServices();
                _gameState?.AddFunds(DishConfig.GetPrice(t.Dish), "dish_sale");
            }

            // Dish sitting on a serving table → orphan for the servers to sink (the entity may
            // already be gone; the orphan entry still frees the slot once a server "collects" it)
            if (t.State == TicketState.Plated && t.ServingSlot >= 0)
            {
                var dishEntity = t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed
                    ? t.PlatedDishEntity : null;
                _orphanServing.Add(new OrphanDish { Slot = t.ServingSlot, DishEntity = dishEntity });
                t.PlatedDishEntity = null;
            }
            // Dish on the patron's table → bus job
            else if (t.State == TicketState.Delivered
                && t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
            {
                _busJobs.Add(new BusJob
                {
                    DishEntity = t.PlatedDishEntity,
                    WorldPos = t.PlatedDishEntity.Transform.Position,
                    EnqueuedTime = Time.TotalTime,
                });
                t.PlatedDishEntity = null;
            }
            // Delivering / carried-by-cook: the carrying FSM sees Canceled and diverts to the sink.

            // Release claims
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            _fetchQueue.Remove(t);

            t.State = TicketState.Canceled;
            _tickets.Remove(t);

            // Refund may have changed fridge stock either way — refresh pre-stock deficits
            RecomputePreStockDeficits();
        }

        /// <summary>Finds and cancels the ticket belonging to the given patron entity.</summary>
        public void CancelTicketForPatron(Entity patronEntity)
        {
            if (patronEntity == null)
                return;
            for (int i = _tickets.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_tickets[i].PatronEntity, patronEntity))
                {
                    CancelTicket(_tickets[i]);
                    return;
                }
            }
        }

        /// <summary>True if fridge + storage can cover every recipe entry for the dish.</summary>
        public bool CanCoverRecipe(DishType dish)
        {
            EnsureServices();
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                int available = FridgeCount(def.Recipe[i].Crop)
                    + (_cropStorage?.CountTotal(def.Recipe[i].Crop) ?? 0);
                if (available < def.Recipe[i].Qty)
                    return false;
            }
            return true;
        }

        /// <summary>Clears <paramref name="results"/> and fills it with dishes whose recipe is coverable.</summary>
        public void GetOrderableDishes(List<DishType> results)
        {
            results.Clear();
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                if (CanCoverRecipe(dish))
                    results.Add(dish);
            }
        }

        // Patron dish shuffle bag (#382): every dish weighted inversely to its price, so
        // cheap dishes are ordered more often but even the priciest cycles through
        // predictably. Lazily built — prices are static per session (DishConfig cache).
        private RolePlayingFramework.Utils.ShuffleBag<DishType> _dishBag;

        /// <summary>
        /// Picks the dish a walk-in patron orders from the currently orderable set.
        /// Bounded draw-and-skip over the persistent full-menu bag: unorderable draws are
        /// skipped (their marbles restore on the next bag cycle when stock returns), so
        /// pricey-dish pity persists across stock fluctuations. Falls back to a uniform
        /// pick if a full cycle yields nothing orderable.
        /// </summary>
        public DishType PickPatronDish(List<DishType> orderable)
        {
            if (_dishBag == null)
                BuildDishBag();

            int limit = _dishBag.Count;
            for (int i = 0; i < limit; i++)
            {
                var dish = _dishBag.Next();
                for (int j = 0; j < orderable.Count; j++)
                {
                    if (orderable[j] == dish)
                        return dish;
                }
            }
            return orderable[Nez.Random.Range(0, orderable.Count)];
        }

        /// <summary>Builds the inverse-price dish bag: marbles(d) = max(1, round(maxPrice / price(d))).</summary>
        private void BuildDishBag()
        {
            int maxPrice = 0;
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                int price = DishConfig.GetPrice((DishType)d);
                if (price > maxPrice) maxPrice = price;
            }

            _dishBag = new RolePlayingFramework.Utils.ShuffleBag<DishType>(DishTypeInfo.Count * 4);
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                int marbles = Math.Max(1, (int)Math.Round((float)maxPrice / DishConfig.GetPrice(dish)));
                _dishBag.Add(dish, marbles);
            }
        }

        /// <summary>Registers the party order source.</summary>
        public void SetPartyOrderSource(IPartyOrderSource source) => _partyOrderSource = source;

        /// <summary>Returns the live (non-canceled) party ticket for the given slot, or null.</summary>
        public KitchenTicket GetPartyTicket(int partySlot)
        {
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.IsPartyTicket && t.PartySlot == partySlot && t.State != TicketState.Canceled)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Called when a patron finishes eating. Despawns the table dish entity, spawns an
        /// EmptyPlate, and enqueues a bus job for the zone server.
        /// </summary>
        public void NotifyPatronFinishedEating(KitchenTicket t)
        {
            if (t == null) return;

            Vector2 platePos = Vector2.Zero;
            bool hasPos = false;
            if (t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
            {
                platePos = t.PlatedDishEntity.Transform.Position;
                hasPos = true;
                _dishService?.Despawn(t.PlatedDishEntity);
                t.PlatedDishEntity = null;
            }

            if (hasPos)
            {
                var emptyPlate = _dishService?.SpawnEmptyPlateAtWorldPos(platePos);
                if (emptyPlate != null)
                {
                    _busJobs.Add(new BusJob
                    {
                        DishEntity = emptyPlate,
                        WorldPos = platePos,
                        EnqueuedTime = Time.TotalTime,
                    });
                }
            }

            _tickets.Remove(t);
        }

        /// <summary>Called when a party member finishes eating. Same as patron, different caller.</summary>
        public void NotifyPartyMemberFinishedEating(KitchenTicket t) => NotifyPatronFinishedEating(t);

        // ── Cook API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Cook reads the next unclaimed posted ticket from the board (party tickets first, then
        /// FIFO). Only one cook holds any given ticket. Returns null when the board is empty.
        /// </summary>
        /// <summary>
        /// True when a posted, unclaimed ticket is waiting at the board. Non-claiming peek — a
        /// cook wandering between tickets uses it to decide when to walk back and read one, so it
        /// must mirror TryReadNextTicket's filter exactly.
        /// </summary>
        public bool HasReadableTicket()
        {
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (!t.PostedToBoard || t.CookClaimed || t.State == TicketState.Canceled)
                    continue;
                if (t.State == TicketState.AwaitingIngredients || t.State == TicketState.ReadyToCook)
                    return true;
            }
            return false;
        }

        public KitchenTicket TryReadNextTicket()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantParty = pass == 0;
                for (int i = 0; i < _tickets.Count; i++)
                {
                    var t = _tickets[i];
                    if (t.IsPartyTicket != wantParty)
                        continue;
                    if (!t.PostedToBoard || t.CookClaimed || t.State == TicketState.Canceled)
                        continue;
                    if (t.State != TicketState.AwaitingIngredients && t.State != TicketState.ReadyToCook)
                        continue;
                    t.CookClaimed = true;
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Claims the first free cooking station for the ticket. Stations are free when no other
        /// cook is using them. Always succeeds while cooks ≤ stations; returns false otherwise.
        /// </summary>
        public bool TryClaimStation(KitchenTicket t, out int station)
        {
            for (int i = 0; i < _stationTicket.Length; i++)
            {
                if (_stationTicket[i] == null)
                {
                    _stationTicket[i] = t;
                    t.StationIndex = i;
                    station = i;
                    return true;
                }
            }
            station = -1;
            return false;
        }

        /// <summary>
        /// Cook abandons its claimed ticket (shift end / interruption). The ticket returns to the
        /// board for the next cook; a mid-cook abandon resets it to ReadyToCook.
        /// </summary>
        public void ReleaseCookTicket(KitchenTicket t)
        {
            if (t == null) return;
            t.CookClaimed = false;
            if (t.State == TicketState.Cooking)
                t.State = TicketState.ReadyToCook;
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            t.StationIndex = -1;
            t.ServingSlot = -1;
        }

        /// <summary>Cook starts cooking at its station: rolls deluxe, locks the reservation.</summary>
        public void BeginCookingAtStation(KitchenTicket t, int cookProficiency)
        {
            if (t == null) return;
            t.State = TicketState.Cooking;
            t.CropsRefundable = false;
            t.IsDeluxe = Nez.Random.Chance(DishConfig.GetDeluxeChance(cookProficiency));
        }

        /// <summary>Cook finished cooking: frees the station (the cook now holds the dish).</summary>
        public void FinishCooking(KitchenTicket t)
        {
            if (t == null) return;
            if (t.StationIndex >= 0 && t.StationIndex < _stationTicket.Length
                && ReferenceEquals(_stationTicket[t.StationIndex], t))
            {
                _stationTicket[t.StationIndex] = null;
            }
            t.StationIndex = -1;
        }

        /// <summary>
        /// Reserves a free serving table slot for the ticket. A slot is occupied while any
        /// ticket or orphaned dish sits on (or is headed to) it.
        /// </summary>
        public bool TryReserveServingSlot(KitchenTicket t, out int slot)
        {
            for (int i = 0; i < GameConfig.KitchenServingSlotCount; i++)
            {
                if (!IsServingSlotOccupied(i))
                {
                    t.ServingSlot = i;
                    slot = i;
                    return true;
                }
            }
            slot = -1;
            return false;
        }

        /// <summary>
        /// Last-resort placement when a cook must go home while every slot is full: reuse the
        /// least-loaded slot. Pickups scan tickets, not slots, so this self-heals.
        /// </summary>
        public int ForceReserveServingSlot(KitchenTicket t)
        {
            t.ServingSlot = 0;
            return 0;
        }

        private bool IsServingSlotOccupied(int slot)
        {
            for (int i = 0; i < _tickets.Count; i++)
                if (_tickets[i].ServingSlot == slot)
                    return true;
            for (int i = 0; i < _orphanServing.Count; i++)
                if (_orphanServing[i].Slot == slot)
                    return true;
            return false;
        }

        /// <summary>Cook placed the dish entity on its reserved serving slot.</summary>
        public void PlaceDishOnServing(KitchenTicket t, Entity dishEntity)
        {
            if (t == null) return;
            t.PlatedDishEntity = dishEntity;
            t.State = TicketState.Plated;
        }

        // ── Server API ───────────────────────────────────────────────────────────

        /// <summary>True when a plated dish (or orphan) is waiting that this zone's server should handle.</summary>
        public bool HasReadyDishForZone(ServerZone zone)
        {
            if (_orphanServing.Count > 0)
                return true;
            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State == TicketState.Plated && ZoneContainsTable(zone, t.TableTile))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Server picks up one item from the serving tables: orphaned dishes first (to the sink),
        /// then plated dishes for tables in the server's zone. The dish entity is despawned —
        /// the server is now carrying it. Returns false when nothing is available.
        /// </summary>
        public bool TryPickupReadyDish(ServerZone zone, out KitchenTicket ticket,
            out DishType dish, out bool toSink)
        {
            if (_orphanServing.Count > 0)
            {
                var orphan = _orphanServing[0];
                _orphanServing.RemoveAt(0);
                if (orphan.DishEntity != null && !orphan.DishEntity.IsDestroyed)
                    _dishService?.Despawn(orphan.DishEntity);
                ticket = null;
                dish = default;
                toSink = true;
                return true;
            }

            for (int i = 0; i < _tickets.Count; i++)
            {
                var t = _tickets[i];
                if (t.State != TicketState.Plated || !ZoneContainsTable(zone, t.TableTile))
                    continue;
                if (t.PlatedDishEntity != null && !t.PlatedDishEntity.IsDestroyed)
                    _dishService?.Despawn(t.PlatedDishEntity);
                t.PlatedDishEntity = null;
                t.ServingSlot = -1;
                t.State = TicketState.Delivering;
                ticket = t;
                dish = t.Dish;
                toSink = false;
                return true;
            }

            ticket = null;
            dish = default;
            toSink = false;
            return false;
        }

        /// <summary>Marks the ticket Delivered and notifies party or patron.</summary>
        public void OnTicketDelivered(KitchenTicket ticket, Entity dishEntity)
        {
            if (ticket == null) return;
            ticket.State = TicketState.Delivered;
            ticket.PlatedDishEntity = dishEntity;

            if (ticket.IsPartyTicket)
            {
                NotifyPartyDishDelivered(ticket.PartySlot, ticket);
            }
            else if (ticket.PatronEntity != null && !ticket.PatronEntity.IsDestroyed)
            {
                var patron = ticket.PatronEntity.GetComponent<ECS.Components.TavernPatronComponent>();
                patron?.OnDishDelivered();
            }
        }

        /// <summary>Next pending bus job. Removes it from the queue.</summary>
        public bool TryClaimBusJob(out BusJob job)
            => TryClaimBusJob(0f, out job);

        /// <summary>
        /// Claims the oldest bus job that has waited at least minAgeSeconds (0 = any). Removes it
        /// from the queue. Bussing is deliberately zone-free — any server may clear any table —
        /// and the age gate lets servers bump long-waiting plates ahead of order-taking so a busy
        /// tavern can't starve bussing forever. (Order-taking and delivery stay zone-restricted.)
        /// </summary>
        public bool TryClaimBusJob(float minAgeSeconds, out BusJob job)
        {
            int oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _busJobs.Count; i++)
            {
                if (Time.TotalTime - _busJobs[i].EnqueuedTime < minAgeSeconds)
                    continue;
                if (_busJobs[i].EnqueuedTime < oldestTime)
                {
                    oldestTime = _busJobs[i].EnqueuedTime;
                    oldest = i;
                }
            }
            if (oldest < 0)
            {
                job = default;
                return false;
            }
            job = _busJobs[oldest];
            _busJobs.RemoveAt(oldest);
            return true;
        }

        /// <summary>
        /// Returns a claimed bus job to the queue — the worker was sent home (or died) before it
        /// picked the plate up. Keeps the original enqueue time so the plate stays at the head of
        /// the queue. No-ops for a plate that was already picked up (its entity is gone) or one
        /// that is somehow queued twice, so calling this defensively is always safe.
        /// </summary>
        public void ReleaseBusJob(BusJob job)
        {
            if (job.DishEntity == null || job.DishEntity.IsDestroyed)
                return;
            for (int i = 0; i < _busJobs.Count; i++)
            {
                if (ReferenceEquals(_busJobs[i].DishEntity, job.DishEntity))
                    return;
            }
            _busJobs.Add(job);
        }

        /// <summary>True while any plate is waiting to be bussed.</summary>
        public bool HasPendingBusJob => _busJobs.Count > 0;

        /// <summary>
        /// True while a pending (unclaimed) bus job's plate sits at the given world position
        /// (within half a tile). Arriving patrons wait at the tavern door while this holds so
        /// they never sit down at a table with a dirty plate; once a server claims the job the
        /// plate is moments from being cleared, so the patron may start walking in.
        /// </summary>
        public bool HasPendingBusJobAt(Vector2 platePos)
        {
            const float maxDistSq = 16f * 16f;
            for (int i = 0; i < _busJobs.Count; i++)
            {
                if (Vector2.DistanceSquared(_busJobs[i].WorldPos, platePos) <= maxDistSq)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Claims the pending bus job whose plate sits at the given world position (within half a
        /// tile), if any. Called by a delivering server right before it sets a new dish down, so a
        /// new meal is never stacked on top of an un-bussed empty plate.
        /// </summary>
        public bool TryClaimBusJobAtPosition(Vector2 platePos, out BusJob job)
        {
            const float maxDistSq = 16f * 16f;
            for (int i = 0; i < _busJobs.Count; i++)
            {
                if (Vector2.DistanceSquared(_busJobs[i].WorldPos, platePos) > maxDistSq)
                    continue;
                job = _busJobs[i];
                _busJobs.RemoveAt(i);
                return true;
            }
            job = default;
            return false;
        }

        // ── Runner API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Runner claims the next transport job (FIFO). The claimed ticket leaves the queue so
        /// two runners never fetch the same ingredients. Returns null when nothing is queued.
        /// </summary>
        public KitchenTicket TryClaimFetchJob()
        {
            while (_fetchQueue.Count > 0)
            {
                var t = _fetchQueue[0];
                _fetchQueue.RemoveAt(0);
                if (t.State == TicketState.Canceled || t.IngredientsFetched)
                    continue; // stale entry
                return t;
            }
            return null;
        }

        /// <summary>Runner abandons a claimed fetch (shift end) — the job re-enters the queue.</summary>
        public void ReleaseFetchJob(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled || t.IngredientsFetched)
                return;
            if (!_fetchQueue.Contains(t))
                _fetchQueue.Add(t);
        }

        /// <summary>One leg of a runner's ingredient trip: the storage building and its door tile.</summary>
        public struct FetchStop
        {
            public int BuildingId;
            public Point DoorTile;
        }

        // Scratch for route planning (allocation-free; the planner runs once per fetch trip)
        private readonly Dictionary<CropType, int> _routeNeed = new Dictionary<CropType, int>(16);
        private readonly List<PlacedBuilding> _routeCandidates = new List<PlacedBuilding>(8);

        /// <summary>
        /// Plans the runner's tour of Crop Storage buildings for a fetch job, nearest-first from
        /// <paramref name="fromTile"/>, capped at <see cref="GameConfig.RunnerMaxStorageStops"/>.
        ///
        /// A building earns a stop when it either supplied this ticket's shortfall
        /// (<see cref="KitchenTicket.SourceBuildingIds"/> — the crops are already withdrawn, so
        /// this is the only record of where they came from) or still holds a crop the fridge needs
        /// to reach its pre-stock target. Stops that no longer contribute once nearer ones are
        /// visited are dropped,
        /// so a multi-crop recipe visits several storages but never one it has no reason to enter.
        ///
        /// Best-effort by design: stock can change between planning and arrival, and the ticket's
        /// own ingredients were reserved at order time, so a short route never blocks a cook.
        /// </summary>
        public void PlanFetchRoute(KitchenTicket t, Point fromTile, List<FetchStop> route)
        {
            route.Clear();
            if (t == null || _buildingService == null)
                return;
            EnsureServices();
            if (_cropStorage == null)
                return;

            // Remaining fridge top-up per recipe crop
            _routeNeed.Clear();
            var def = DishConfig.GetDefinition(t.Dish);
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                var crop = def.Recipe[i].Crop;
                if (_routeNeed.ContainsKey(crop))
                    continue;
                // A stop is only worth planning for what the runner's hands can carry this trip
                int want = FridgeTopUpWant(crop);
                int carry = RunnerCarryUnits;
                if (want > carry) want = carry;
                if (want > 0)
                    _routeNeed[crop] = want;
            }

            _routeCandidates.Clear();
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type == BuildingType.CropStorage)
                    _routeCandidates.Add(all[i]);
            }

            var cursor = fromTile;
            while (_routeCandidates.Count > 0 && route.Count < GameConfig.RunnerMaxStorageStops)
            {
                int nearest = -1;
                long best = long.MaxValue;
                Point bestDoor = default;
                for (int i = 0; i < _routeCandidates.Count; i++)
                {
                    var door = Util.BuildingConfig.GetDoorTile(_routeCandidates[i].Type,
                        new Point(_routeCandidates[i].TileX, _routeCandidates[i].TileY));
                    long dx = door.X - cursor.X;
                    long dy = door.Y - cursor.Y;
                    long distSq = dx * dx + dy * dy;
                    if (distSq < best)
                    {
                        best = distSq;
                        nearest = i;
                        bestDoor = door;
                    }
                }

                var building = _routeCandidates[nearest];
                _routeCandidates.RemoveAt(nearest);

                bool worthStopping = t.SourceBuildingIds != null
                    && t.SourceBuildingIds.Contains(building.UniqueId);
                if (!worthStopping)
                {
                    foreach (var kvp in _routeNeed)
                    {
                        if (kvp.Value > 0 && _cropStorage.CountIn(building.UniqueId, kvp.Key) > 0)
                        {
                            worthStopping = true;
                            break;
                        }
                    }
                }
                if (!worthStopping)
                    continue;

                route.Add(new FetchStop { BuildingId = building.UniqueId, DoorTile = bestDoor });
                cursor = bestDoor;

                // Simulate this stop's supply so a later storage isn't toured for a crop
                // this one already covers
                _routeScratchCrops.Clear();
                foreach (var kvp in _routeNeed)
                    _routeScratchCrops.Add(kvp.Key);
                for (int i = 0; i < _routeScratchCrops.Count; i++)
                {
                    var crop = _routeScratchCrops[i];
                    int have = _cropStorage.CountIn(building.UniqueId, crop);
                    int left = _routeNeed[crop] - have;
                    _routeNeed[crop] = left > 0 ? left : 0;
                }
            }
        }

        private readonly List<CropType> _routeScratchCrops = new List<CropType>(16);

        /// <summary>
        /// Runner is at a storage door: opportunistically withdraws top-up crops for the
        /// ticket's recipe into <paramref name="carriedTopUp"/> (indexed by CropType), drawing
        /// only on the building it is standing in front of. The fridge does NOT change here —
        /// the cargo lands via <see cref="DeliverCarriedTopUp"/> when the runner unloads at the
        /// fridge. Pass a negative id to draw from every storage at once — the fallback when no
        /// route could be planned. Amounts already in hand from earlier stops count against both
        /// the target and the carry cap, so a multi-stop tour never over-collects.
        /// </summary>
        public void RunnerCollectAtStorage(KitchenTicket t, int[] carriedTopUp, int buildingId = -1)
        {
            if (t == null || carriedTopUp == null) return;
            EnsureServices();
            if (_cropStorage == null) return;

            var def = DishConfig.GetDefinition(t.Dish);
            int carry = RunnerCarryUnits;
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                var crop = def.Recipe[i].Crop;
                int idx = (int)crop;
                if (idx >= carriedTopUp.Length)
                    continue;

                // Opportunistic top-up rides in the runner's hands, so it is capped by the
                // carry level (the ticket's own reserved shortfall moved at order time and is
                // not subject to the cap)
                int already = carriedTopUp[idx];
                int want = FridgeTopUpWant(crop) - already;
                int room = carry - already;
                if (want > room) want = room;
                if (want <= 0)
                    continue;

                int taken;
                if (buildingId >= 0)
                {
                    taken = _cropStorage.WithdrawUpTo(buildingId, crop, want);
                }
                else
                {
                    int available = _cropStorage.CountTotal(crop);
                    int take = want < available ? want : available;
                    taken = take > 0 && _cropStorage.TryWithdrawAcrossBuildings(crop, take) ? take : 0;
                }

                if (taken > 0)
                    carriedTopUp[idx] += taken;
            }
        }

        /// <summary>
        /// Runner arrived at the fridge: the ticket's ingredients are now complete and the
        /// ticket becomes cookable (if posted, a cook can start immediately).
        /// </summary>
        public void CompleteFetch(KitchenTicket t)
        {
            if (t == null || t.State == TicketState.Canceled)
                return;
            t.IngredientsFetched = true;
            if (t.State == TicketState.AwaitingIngredients)
                t.State = TicketState.ReadyToCook;
        }

        // ── Pre-stock jobs (issue #386) ──────────────────────────────────────────

        /// <summary>
        /// One pre-stock trip: fetch up to <see cref="GameConfig.KitchenRunnerCarryCropTypes"/>
        /// different crops from one storage, each capped at the runner carry level's per-crop
        /// units. The first <see cref="CropCount"/> entries of the arrays are valid.
        /// </summary>
        public struct PreStockJob
        {
            public CropType[] Crops;
            public int[] Units;
            public int CropCount;
            public int BuildingId;
            public Point DoorTile;
        }

        /// <summary>
        /// Units of each crop type a runner can hold per trip: runners carry crops by hand, so
        /// the global carry level (raised by one-of-a-kind items) gates trip size — 1, 5, or 10
        /// units per crop type at levels 1/2/3.
        /// </summary>
        private int RunnerCarryUnits
            => GameConfig.GetRunnerCarryUnits(_gameState?.RunnerCarryLevel ?? GameConfig.KitchenRunnerCarryLevelMin);

        /// <summary>Queued pre-stock trips — feeds runner backpressure alongside ticket fetches.</summary>
        public int PreStockQueueDepth => _preStockQueue.Count;

        /// <summary>
        /// Remaining fridge top-up for the crop toward the pre-stock target, clamped to what the
        /// bounded fridge can still hold.
        /// </summary>
        private int FridgeTopUpWant(CropType crop)
        {
            int want = PreStockTargetUnits() - FridgeCount(crop);
            if (want <= 0)
                return 0;
            int capacity = _fridgeInv?.CapacityFor(crop) ?? 0;
            return want < capacity ? want : capacity;
        }

        private static bool IsRecipeCrop(CropType crop)
        {
            if (_recipeCropMask == null)
            {
                var mask = new bool[CropTypeInfo.Count];
                for (int d = 0; d < DishTypeInfo.Count; d++)
                {
                    var def = DishConfig.GetDefinition((DishType)d);
                    for (int i = 0; i < def.Recipe.Length; i++)
                        mask[(int)def.Recipe[i].Crop] = true;
                }
                _recipeCropMask = mask;
            }
            return _recipeCropMask[(int)crop];
        }

        /// <summary>
        /// Scans every recipe crop and queues a pre-stock trip for each one whose fridge stock has
        /// fallen below the target stack count and that is available in crop storage. Called on a
        /// throttle from <see cref="Update"/> and directly whenever a ticket takes from the fridge,
        /// so a depleted crop triggers a refetch immediately.
        /// </summary>
        public void RecomputePreStockDeficits()
        {
            EnsureServices();
            if (_cropStorage == null || _fridgeInv == null)
                return;

            for (int c = 0; c < CropTypeInfo.Count; c++)
            {
                if (_preStockBusy[c])
                    continue;
                var crop = (CropType)c;
                if (!IsRecipeCrop(crop))
                    continue;

                // Unit-based target (stacks × stack size), so a partially consumed stack still
                // counts as a deficit and gets topped back up
                if (FridgeTopUpWant(crop) <= 0)
                    continue;
                if (_cropStorage.CountTotal(crop) <= 0)
                    continue;

                _preStockQueue.Add(crop);
                _preStockBusy[c] = true;
            }
        }

        /// <summary>
        /// Runner claims the next pre-stock trip (FIFO): one storage stop carrying up to
        /// <see cref="GameConfig.KitchenRunnerCarryCropTypes"/> queued crops that storage holds,
        /// each capped at the carry level's per-crop units. Stale entries (stock or capacity
        /// changed since queueing) are skipped. Returns false when nothing claimable is queued.
        /// </summary>
        public bool TryClaimPreStockJob(Point fromTile, out PreStockJob job)
        {
            job = default;
            EnsureServices();
            if (_cropStorage == null || _fridgeInv == null || _buildingService == null)
                return false;

            int carryUnits = RunnerCarryUnits;

            while (_preStockQueue.Count > 0)
            {
                var anchor = _preStockQueue[0];
                _preStockQueue.RemoveAt(0);

                int anchorWant = FridgeTopUpWant(anchor);
                if (anchorWant <= 0)
                {
                    _preStockBusy[(int)anchor] = false;
                    continue; // stale — fridge topped up or full since queueing
                }

                // Nearest storage that still holds the anchor crop
                var all = _buildingService.GetAll();
                long best = long.MaxValue;
                int bestId = -1;
                Point bestDoor = default;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Type != BuildingType.CropStorage)
                        continue;
                    if (_cropStorage.CountIn(all[i].UniqueId, anchor) <= 0)
                        continue;
                    var door = Util.BuildingConfig.GetDoorTile(all[i].Type,
                        new Point(all[i].TileX, all[i].TileY));
                    long dx = door.X - fromTile.X;
                    long dy = door.Y - fromTile.Y;
                    long distSq = dx * dx + dy * dy;
                    if (distSq < best)
                    {
                        best = distSq;
                        bestId = all[i].UniqueId;
                        bestDoor = door;
                    }
                }
                if (bestId < 0)
                {
                    _preStockBusy[(int)anchor] = false;
                    continue; // stale — storage emptied since queueing
                }

                var crops = new CropType[GameConfig.KitchenRunnerCarryCropTypes];
                var units = new int[GameConfig.KitchenRunnerCarryCropTypes];
                crops[0] = anchor;
                units[0] = ClampTripUnits(carryUnits, anchorWant, _cropStorage.CountIn(bestId, anchor));
                int count = 1;

                // Fill the remaining hand slots with other queued crops this same storage holds;
                // crops it doesn't hold stay queued for their own trip
                for (int i = 0; i < _preStockQueue.Count && count < GameConfig.KitchenRunnerCarryCropTypes;)
                {
                    var crop = _preStockQueue[i];
                    int want = FridgeTopUpWant(crop);
                    int inStorage = _cropStorage.CountIn(bestId, crop);
                    if (want > 0 && inStorage > 0)
                    {
                        _preStockQueue.RemoveAt(i); // stays busy until delivery/release
                        crops[count] = crop;
                        units[count] = ClampTripUnits(carryUnits, want, inStorage);
                        count++;
                    }
                    else
                        i++;
                }

                job = new PreStockJob
                {
                    Crops = crops,
                    Units = units,
                    CropCount = count,
                    BuildingId = bestId,
                    DoorTile = bestDoor,
                };
                return true;
            }
            return false;
        }

        private static int ClampTripUnits(int carryUnits, int want, int inStorage)
        {
            int units = carryUnits;
            if (want < units) units = want;
            if (inStorage < units) units = inStorage;
            return units;
        }

        /// <summary>Runner abandons a claimed pre-stock trip (shift end / no path) — the deficit recompute re-queues it.</summary>
        public void ReleasePreStockJob(in PreStockJob job)
        {
            for (int i = 0; i < job.CropCount; i++)
                _preStockBusy[(int)job.Crops[i]] = false;
        }

        /// <summary>
        /// Runner is at the storage door for a pre-stock trip: withdraws the crops from storage
        /// into the runner's hands — the fridge does NOT change here; stock only rises when the
        /// runner unloads at the fridge via <see cref="PreStockDeliver"/>. Each crop is
        /// re-clamped against the LIVE target — another runner's trip or a ticket top-up may
        /// have filled it during the walk out, and overshooting the target would drain storage
        /// for nothing. Fills <paramref name="takenPerCrop"/> (parallel to the job's crops) and
        /// returns the total units picked up. The job's crops STAY busy while carried so the
        /// deficit recompute cannot dispatch a second runner for cargo already in transit;
        /// a zero-total pickup releases them immediately (the trip aborts with empty hands).
        /// </summary>
        public int PreStockCollect(in PreStockJob job, int[] takenPerCrop = null)
        {
            EnsureServices();
            if (_cropStorage == null || _fridgeInv == null)
            {
                for (int i = 0; i < job.CropCount; i++)
                    _preStockBusy[(int)job.Crops[i]] = false;
                return 0;
            }

            int total = 0;
            for (int i = 0; i < job.CropCount; i++)
            {
                var crop = job.Crops[i];
                int want = job.Units[i];
                int liveWant = FridgeTopUpWant(crop);
                if (liveWant < want) want = liveWant;

                int taken = want > 0 ? _cropStorage.WithdrawUpTo(job.BuildingId, crop, want) : 0;
                if (takenPerCrop != null && i < takenPerCrop.Length)
                    takenPerCrop[i] = taken;
                total += taken;
            }

            if (total <= 0)
            {
                for (int i = 0; i < job.CropCount; i++)
                    _preStockBusy[(int)job.Crops[i]] = false;
            }
            return total;
        }

        /// <summary>
        /// Runner arrived at the fridge with pre-stock cargo (or despawned carrying it — cargo
        /// teleports in rather than being lost): deposits the collected crops, which is the ONLY
        /// point fridge stock increases for a pre-stock trip. Overflow that no longer fits the
        /// fridge falls back to crop storage so crops are never destroyed. Clears the busy mask
        /// and re-queues any crops still below target, so low carry levels turn straight around
        /// for the next armful.
        /// </summary>
        public void PreStockDeliver(in PreStockJob job, int[] takenPerCrop,
            string monster = null, string monsterType = null)
        {
            EnsureServices();
            for (int i = 0; i < job.CropCount; i++)
            {
                _preStockBusy[(int)job.Crops[i]] = false;

                int taken = takenPerCrop != null && i < takenPerCrop.Length ? takenPerCrop[i] : 0;
                if (taken <= 0)
                    continue;
                var crop = job.Crops[i];
                int stored = _fridgeInv?.Deposit(crop, taken) ?? 0;
                if (stored < taken)
                    _cropStorage?.DepositAcrossBuildings(crop, taken - stored);
                if (stored > 0)
                    Analytics.AnalyticsService.LogCropFridgeStocked(crop.ToString(), stored,
                        job.BuildingId, "prestock", monster, monsterType);
            }

            RecomputePreStockDeficits();
        }

        /// <summary>
        /// Runner arrived at the fridge after a ticket fetch (or despawned mid-carry): deposits
        /// the opportunistic top-up crops accumulated across the trip's storage stops and zeroes
        /// the accumulator. Overflow that no longer fits the fridge falls back to crop storage.
        /// </summary>
        public void DeliverCarriedTopUp(int[] carriedTopUp, string monster = null, string monsterType = null)
        {
            if (carriedTopUp == null)
                return;
            EnsureServices();

            for (int c = 0; c < carriedTopUp.Length; c++)
            {
                int qty = carriedTopUp[c];
                if (qty <= 0)
                    continue;
                carriedTopUp[c] = 0;
                var crop = (CropType)c;
                int stored = _fridgeInv?.Deposit(crop, qty) ?? 0;
                if (stored < qty)
                    _cropStorage?.DepositAcrossBuildings(crop, qty - stored);
                if (stored > 0)
                    Analytics.AnalyticsService.LogCropFridgeStocked(crop.ToString(), stored,
                        -1, "ticket_topup", monster, monsterType);
            }
        }

        // ── Static tile helpers ──────────────────────────────────────────────────

        /// <summary>Ticket board tile (servers post, cooks read).</summary>
        public static Point TicketBoardTile
            => new Point(GameConfig.KitchenTicketBoardTileX, GameConfig.KitchenTicketBoardTileY);

        /// <summary>Fridge tile (cooks gather here; runners restock it).</summary>
        public static Point FridgeTile
            => new Point(GameConfig.KitchenFridgeTileX, GameConfig.KitchenFridgeTileY);

        /// <summary>A tile inside the runners' wander area (kitchen south corridor).</summary>
        public static Point RunnerWanderAnchorTile
            => new Point(GameConfig.KitchenRunnerWanderMinTileX, GameConfig.KitchenRunnerWanderMinTileY + 1);

        /// <summary>Sink tile (dirty plates and orphaned dishes go here).</summary>
        public static Point SinkTile => new Point(GameConfig.KitchenSinkTileX, GameConfig.KitchenSinkTileY);

        /// <summary>World center of the sink tile.</summary>
        public static Vector2 SinkWorldPos => new Vector2(
            GameConfig.KitchenSinkTileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
            GameConfig.KitchenSinkTileY * GameConfig.TileSize + GameConfig.TileSize / 2f);

        /// <summary>Returns the cooking station tile for the given station index (cook stands here).</summary>
        public static Point GetStationTile(int stationIndex)
        {
            int x;
            switch (stationIndex)
            {
                case 0: x = GameConfig.KitchenStove1TileX; break;
                case 1: x = GameConfig.KitchenStove2TileX; break;
                default: x = GameConfig.KitchenStove3TileX; break;
            }
            return new Point(x, GameConfig.KitchenStoveTileY);
        }

        /// <summary>Returns the serving table tile for the given slot index.</summary>
        public static Point GetServingTile(int slot)
            => new Point(GameConfig.KitchenServingTableTileX, GameConfig.KitchenServingTableFirstTileY + slot);

        /// <summary>
        /// Tile a worker stands on to place/take a dish at the given serving slot — one tile
        /// left of the table so they work beside it instead of on top of it.
        /// </summary>
        public static Point GetServingApproachTile(int slot)
            => new Point(GameConfig.KitchenServingTableTileX - 1, GameConfig.KitchenServingTableFirstTileY + slot);

        /// <summary>Seat tile for a party slot (0 = hero, 1/2 = hired mercs).</summary>
        public static Point GetPartySeatTile(int partySlot)
        {
            switch (partySlot)
            {
                case 0:  return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
                case 1:  return new Point(GameConfig.TavernMercenary1SeatTileX, GameConfig.TavernMercenary1SeatTileY);
                case 2:  return new Point(GameConfig.TavernMercenary2SeatTileX, GameConfig.TavernMercenary2SeatTileY);
                default: return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
            }
        }

        /// <summary>Nearest CropStorage door tile from the given origin. Returns false if none exists.</summary>
        public bool TryFindNearestStorageDoor(Point fromTile, out Point doorTile)
        {
            doorTile = default;
            if (_buildingService == null)
                return false;

            var all = _buildingService.GetAll();
            long best = long.MaxValue;
            bool found = false;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.CropStorage)
                    continue;
                var door = Util.BuildingConfig.GetDoorTile(all[i].Type, new Point(all[i].TileX, all[i].TileY));
                long dx = door.X - fromTile.X;
                long dy = door.Y - fromTile.Y;
                long distSq = dx * dx + dy * dy;
                if (distSq < best)
                {
                    best = distSq;
                    doorTile = door;
                    found = true;
                }
            }
            return found;
        }

        // ── Party order source pass-through ──────────────────────────────────────

        /// <summary>True if the party order source is set and has a pending order.</summary>
        public bool TryGetNextPartyOrder(out int partySlot, out DishType dish)
        {
            if (_partyOrderSource != null)
                return _partyOrderSource.TryGetNextPartyOrder(out partySlot, out dish);
            partySlot = -1;
            dish = default;
            return false;
        }

        /// <summary>Notifies party order source that a server took the order.</summary>
        public void NotifyPartyOrderTaken(int partySlot, KitchenTicket ticket)
            => _partyOrderSource?.OnPartyOrderTaken(partySlot, ticket);

        /// <summary>Notifies party order source that a dish was delivered to the table.</summary>
        public void NotifyPartyDishDelivered(int partySlot, KitchenTicket ticket)
            => _partyOrderSource?.OnPartyDishDelivered(partySlot, ticket);

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void EnsureServices()
        {
            // Core.Services requires a running game instance; headless tests inject via SetHeadlessServices.
            if (Core.Instance == null)
                return;
            if (_cropStorage == null)
                _cropStorage = Core.Services.GetService<CropStorageInventoryService>();
            if (_droppedCrops == null)
                _droppedCrops = Core.Services.GetService<DroppedCropService>();
            if (_dishService == null)
                _dishService = Core.Services.GetService<DishEntityService>();
            if (_gameState == null)
                _gameState = Core.Services.GetService<GameStateService>();
            if (_mercenaryManager == null)
                _mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (_fridgeInv == null)
                _fridgeInv = Core.Services.GetService<FridgeInventoryService>();
        }

        /// <summary>
        /// Injects service instances directly for headless tests (no running game instance).
        /// The live path resolves these through Core.Services in EnsureServices.
        /// </summary>
        public void SetHeadlessServices(CropStorageInventoryService cropStorage, GameStateService gameState,
            FridgeInventoryService fridge = null)
        {
            _cropStorage = cropStorage;
            _gameState = gameState;
            _fridgeInv = fridge ?? new FridgeInventoryService();
        }

        private void HandleBuildingsChanged()
        {
            Pathfinder.RebuildWalls(_buildingService);
        }

        /// <summary>Exposes the DishEntityService for use by the FSM when spawning dishes.</summary>
        public DishEntityService DishService => _dishService;
    }
}

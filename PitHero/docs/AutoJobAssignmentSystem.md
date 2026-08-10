# Auto Job Assignment System (issue #321; backpressure scaling issue #375)

Automates allied-monster job assignment. When the player enables **Settings → Automation →
"Automate monster jobs"**, `AutoJobAssignmentService` continuously samples per-job backpressure,
periodically measures per-job workload ("demand"), and rewrites each monster's
`AlliedMonster.Job`. That field write is the *entire* assignment action:
`FarmTaskCoordinator.Update()` and `KitchenTaskCoordinator.Update()` reconcile worker entities
against `Job` + awake state every frame, so no entity or FSM code is involved in assignment.

Scaling is asymmetric by design (issue #375): jobs staff **up instantly** when they fall behind
and drain **down slowly** (one worker per drain interval per job) when pressure subsides, so a
dinner rush is met immediately but its crew isn't dumped the moment the last plate lands.

The coordinators are peered via `IMonsterWorkerHost` (`MainGameScene` wires `AddPeer` both ways):
on a mid-shift job change the monster's old-job worker entity must walk home and despawn before
the new job's coordinator spawns its worker — never two entities for the same monster at once.
The same gate covers kitchen role changes (the replacement waits for the old worker to despawn).

## Components

| Piece | File | Role |
|---|---|---|
| `AutoJobAssignmentService` | `PitHero/Services/AutoJobAssignmentService.cs` | Cadence + snapshot + apply loop. Registered/ticked/unloaded in `MainGameScene` (`Begin`, `Update` unpaused block, `Unload`). |
| `JobAssignmentSolver` | `PitHero/Services/AutoJob/JobAssignmentSolver.cs` | Pure static solver. No service or ECS dependencies — fully unit-testable. |
| `IJobDemandEvaluator` | `PitHero/Services/AutoJob/IJobDemandEvaluator.cs` | One per automatable job: reports how many workers the job wants right now (`EvaluateDemand`) and feeds its backpressure tracker (`SamplePressure`/`ResetPressure`). |
| `BackpressureTracker` | `PitHero/Services/AutoJob/BackpressureTracker.cs` | Per-job smoother: instant attack, EMA decay, one-worker-per-interval drain. Owned by each evaluator. |
| `FarmingJobDemandEvaluator` | `PitHero/Services/AutoJob/FarmingJobDemandEvaluator.cs` | Farming demand (non-sticky). |
| `KitchenJobDemandEvaluator` | `PitHero/Services/AutoJob/KitchenJobDemandEvaluator.cs` | Kitchen demand (sticky). |
| UI gating | `PitHero/UI/SettingsUI.cs` (checkbox), `PitHero/UI/MonsterUI.cs` (job buttons non-clickable while enabled) | |
| Persistence | `SaveData.AutomateMonsterJobs` (v19, section 34) → `AutoJobAssignmentService.Enabled` | Loading never forces a reshuffle; persisted jobs stand until the next cadence tick. |

Tunables live in `GameConfig` under the `AutoJob*` prefix.

## When sampling and reassessment run

`Update()` (per unpaused frame) calls `TickCadence(nowSeconds, isNighttime)`, measured on
`InGameTimeService.AccumulatedSeconds` so pausing never advances either timer. Two cadences:

- **Pressure sampling** — every `GameConfig.AutoJobPressureSampleIntervalSeconds` (5 scaled
  seconds) each evaluator's `SamplePressure` feeds live workload into its `BackpressureTracker`.
  Sampling always runs before any reassess in the same tick, so a solve works from fresh grants.
- **Reassessment** — `ReassessNow()` fires:
  1. Every `GameConfig.AutoJobReassessIntervalSeconds` (15 scaled seconds = 15 in-game minutes).
     A `now < last` guard restarts the interval after a load rewinds time — and also calls
     `ResetPressure` on every evaluator, since tracker timestamps came from the old clock.
  2. Immediately when `IsNighttime` flips (6AM / 10PM shift change), restarting the interval.
  3. Immediately when the player first checks the checkbox (`SettingsUI` calls `ReassessNow()`).

### The backpressure tracker

`BackpressureTracker.Sample(raw, now)` holds pressure in workers-worth units:

- **Attack is instant**: raw ≥ smoothed replaces smoothed outright, and the granted worker count
  jumps straight to `ceil(raw)` — a rush staffs up on the very next solve. (Scale-up keys off the
  raw signal, never the smoothed one, so the EMA tail can't re-grant workers.)
- **Decay is smoothed**: falling raw decays through an EMA (`AutoJobPressureDecayAlpha`), so
  pressure must stay low across several samples before a grant becomes releasable.
- **Drain is rate-limited**: a grant level is released only when smoothed pressure sits below
  `granted − 0.5` (half-worker release hysteresis) AND a full
  `AutoJobScaleDownDrainIntervalSeconds` (60 scaled seconds) has passed since the last change —
  one worker per interval, never more.

Tracker state is transient (never persisted); it rebuilds within a few samples after a load.

## Day/night shifts are solved independently

Day monsters (work 6AM–10PM) and nocturnal monsters (10PM–6AM; see
`MonsterScheduleConfig.IsNocturnal`) are **disjoint workforces that never work at the same time**.
`ReassessNow()` partitions the roster by `IsNocturnal(MonsterTypeName)` and runs demand + solve
separately per shift, so every job gets both a day crew and a night crew. Demand clamps
(`rosterSize`) always refer to the *shift's* size, not the whole roster. Asleep monsters are
assigned normally — the coordinators keep them home until their work window.

## The solver

`JobAssignmentSolver.Solve(monsters, demands, resultJobs)` — deterministic (ties break on lowest
`RosterIndex`), allocation-free, all `for` loops:

1. **Sticky pass** — monsters whose `CurrentJob` matches a demand entry with `Sticky = true` keep
   that job (stickiness = "not reshuffled arbitrarily between solves"; workers leave only through
   the next pass or the starvation release).
2. **Sticky trim pass** (issue #375, deliberately replacing the original "never demoted, even
   above desired" rule) — a sticky demand holding more workers than
   `max(MinWorkers, DesiredWorkers)` releases its **lowest-proficiency** worker, at most **one per
   solve per demand**. This is the sanctioned scale-down path: even if a demand gate suddenly
   zeroes desired (larder ran dry), the crew drains one worker per reassessment, never in one
   layoff. Running before the fill passes means the freed monster is claimable by whichever job
   needs it in the *same* solve.
3. **Min pass** — each demand, in list order, fills up to `MinWorkers` from unassigned monsters by
   highest proficiency for that job.
4. **Desired round-robin pass** (issue #375, replacing the whole-demand-at-a-time desired fill) —
   repeated cycles over the demand list; each demand still under `DesiredWorkers` claims exactly
   **one** free monster per cycle. List order decides who gets each cycle's first spare, making
   priority a tiebreak instead of a monopoly. (The old pass let farming's desired extras absorb
   every spare monster before the kitchen's desired pass ran — the "kitchen frozen at 3 during a
   packed dinner rush" bug.)
5. **Starvation release pass** — a demand with `MinWorkers > 0`
   that ends the fill passes with **zero** workers (proof that every candidate is sticky-locked
   elsewhere — fill exhausts free monsters first) pulls sticky-held workers from **lower-priority**
   demands only, up to its `DesiredWorkers`, never dropping a raided job below its own
   `MinWorkers`. In practice: farm tasks with no farmer at all release kitchen workers to the farm
   (kitchen work is pointless with nothing being grown), but the kitchen is never raided below its
   base crew when the shift is big enough to field one. (The trim pass never drops a job below its
   own `MinWorkers`, so a trimmed kitchen can't trigger its own starvation.)
6. **Swap pass** — a sticky worker swaps with a non-sticky assignee only when the other job
   **strictly** gains proficiency and the sticky job **doesn't lose** any. Every swap strictly
   raises total proficiency, so the reverse swap can never qualify and assignments never
   oscillate between reassessments. (The zero-loss case matters after a trim: equal cooks, but
   one is the far better farmer — the farm gets that one.)
7. Everyone unassigned gets `MonsterJob.None` → the coordinators send them home.

A monster holding a job with **no demand entry** is non-sticky by definition and gets pooled and
reassigned — this is the extensibility guarantee (a Fishing-assigned monster before a fishing
evaluator exists just returns to the pool).

## Current demand models

Evaluators run in priority order (**farming first** — the kitchen depends on farm output, so farm
demand is covered before the kitchen staffs). Each evaluator receives the shift's `rosterSize` and
`availableWorkers` = roster minus the `MinWorkers` already reserved by higher-priority evaluators,
so a lower-priority job only claims workers its betters don't need.

- **Farming** (`Sticky = false`, listed first): honest, work-driven demand (issue #375). Desired =
  `max(burst, granted, min)` clamped to available workers, where burst =
  `OutstandingTaskCount / AutoJobFarmTasksPerWorker` ceil-divided (instant scale-up for
  watering/harvest waves), granted = the tracker's drain-limited memory of recent waves (so
  staffing ramps down between waves instead of collapsing), and `MinWorkers` = 1 whenever any
  crops or plans exist — one caretaker for the next wave. The pre-#375 baseline term
  (`careLoad / 12`) is gone: it staffed farmers who had literally nothing to claim, and they
  ping-ponged Idle↔Wander around the field. Surplus farmers now drain off to the kitchen or home.
- **Kitchen** (`Sticky = true`): base crew `AutoJobKitchenBaseStaff` = 3 — **cook + server +
  runner; never less, a runner-less kitchen runs the fridge dry and leaves dirty plates on the
  tables** — plus extra workers `max(liveExtras, grantedExtras)`, capped at
  `AutoJobKitchenMaxWorkers` (must mirror `KitchenTaskCoordinator.MaxWorkerPosts` = 3 cooks +
  2 servers + 3 runners = 8; asserted by
  `KitchenServiceLoopTests.RoleMix_RespectsPerRoleCapsAndNeverExceedsMaxPosts`). Live extras =
  one worker per `AutoJobKitchenBacklogPerExtraWorker` backlog items (open tickets + seated
  patrons + pending party diners), **plus one more when any patron has waited
  `AutoJobKitchenHighWaitSeconds` (an in-game hour) to order or be served** — a long wait proves
  the pipeline is behind even when raw counts look modest. Granted extras come from the tracker
  (slow drain after the rush).
  `MinWorkers` = the base crew (firm, so it fills ahead of farming's desired extras and doubles as
  the floor neither the trim pass nor the starvation release pass drops below). **All-or-nothing
  when competing with farming:** if farming's reservation leaves fewer than the base crew
  available (`availableWorkers < baseStaff && availableWorkers < rosterSize`), the kitchen fields
  no one — a partial kitchen has no runner, and with no farm workers there'd be no ingredients
  anyway. On tiny rosters with no farm workload, the base crew still clamps down to the roster as
  before.
  **Empty-larder gate:** demand is also zero while no dish is coverable from fridge + storage
  (`KitchenTaskCoordinator.HasAnyOrderableDish`) — servers refuse orders they can't cover, so
  staff would be dead weight (a new game's lone monster stays home instead of taking the chef
  hat). Workers already in the kitchen drain out one per solve (trim pass) if the larder runs dry
  mid-day; the gate blocks fresh staffing until the next cadence tick after crops land in storage.

  The demand model's granularity is `MonsterJob`, not `KitchenRole` — it asks for *N kitchen
  workers* and the coordinator decides the cook/server/runner split (`FillRoleMix`, below).

Worked examples (farm plans present, no outstanding tasks): 2 monsters → 1 caretaker farms, 1
home (kitchen can't field a functional crew of 2). Exactly 4 → 1 farmer + 3 kitchen. 12 with a
9-ticket dinner rush → 1 farmer + 6 kitchen + 5 home, draining back to 1 + 3 + 8 after the rush.

## Kitchen role mix under pressure

`KitchenTaskCoordinator.FillRoleMix(postCount, cookPressure, serverPressure, runnerPressure,
into)` decides the cook/server/runner split. Posts 0–2 are always Cook, Server, Runner (the crew
that opens the kitchen and keeps it fed and cleared — `IsKitchenOpen` needs posts 0 and 1). Posts
beyond the base crew go to the role with the highest **pressure per already-assigned worker**
(D'Hondt greedy, integer cross-multiplication, ties toward Cook → Server → Runner), honoring the
per-role caps (3 cooks / 2 servers / 3 runners; 2 servers is a hard `ServerZone` design limit).
Zero-pressure roles fall back to the legacy Cook → Server → Runner cycle, and the zero-pressure
overload *is* the legacy cycle.

Per-role pressure signals, computed in the coordinator's `Update()`:

- **Runner**: `AwaitingIngredientsTicketCount` (tickets stalled on a storage fetch — this already
  covers the queued fetch jobs) + `BusJobCount` (dirty plates).
- **Cook**: `ReadyToCookUnclaimedCount` (posted tickets no cook has picked up).
- **Server**: `PlatedAwaitingPickupCount` (dishes cooling on serving tables) +
  `MercenaryManager.CountPatronsWaitingToOrder()`.

**Anti-thrash dwell:** a role change sends the worker home to despawn and respawn, so the
weighted mix is recomputed at most once per `GameConfig.KitchenRoleMixDwellSeconds` (45 scaled
seconds) — except when the post count itself changes, which recomputes immediately (spawns and
despawns are happening anyway).

Workload getters: `FarmTaskCoordinator.OutstandingTaskCount`,
`KitchenTaskCoordinator.ActiveTicketCount` / `AwaitingIngredientsTicketCount` /
`ReadyToCookUnclaimedCount` / `PlatedAwaitingPickupCount` / `FetchQueueDepth` / `BusJobCount` /
`OldestOpenTicketAgeSeconds`, `CropGrowthService.CropCount`, `CropPlantingService.PlanCount`,
`MercenaryManager.CountSeatedPatrons()` / `CountPatronsWaitingToOrder()` /
`MaxPatronWaitSeconds()`, `TavernPatronComponent.StateElapsedSeconds`,
`PartyDiningService.CountPendingPartyDiners()`. `KitchenTicket.CreatedTime` stamps ticket
creation (`Time.TotalTime`, mirroring `BusJob.EnqueuedTime`; not persisted).

## Adding a new job (e.g. Fishing)

The solver never changes. Steps:

1. The job must already exist in `MonsterJob` and have a coordinator/FSM work loop that
   spawns/despawns workers off `AlliedMonster.Job` (fishing has the enum value but no work loop
   yet — build that first, mirroring `FarmTaskCoordinator`).
2. Create `PitHero/Services/AutoJob/FishingJobDemandEvaluator.cs` implementing
   `IJobDemandEvaluator`: report `Job = MonsterJob.Fishing` and a `JobDemandEntry` computed from
   whatever workload signal fits (keep the arithmetic in a `public static ComputeDemand(...)` so
   it's unit-testable headless, like the existing evaluators; take service dependencies as
   nullable constructor params). `EvaluateDemand(rosterSize, availableWorkers)` — clamp to
   `availableWorkers`, the workers left after higher-priority evaluators' minimums. Own a
   `BackpressureTracker`, feed it in `SamplePressure` (never in `EvaluateDemand`, which runs
   twice per reassess — once per shift), clear it in `ResetPressure`, and fold
   `tracker.GrantedWorkers` into desired via `max(liveSignal, granted)` for slow scale-down.
3. Decide `Sticky`: true only if pulling workers off the job mid-shift is harmful (kitchen-style
   continuity); false if workers can be freely rebalanced (farming-style).
4. Register it in `MainGameScene.Begin()` via `autoJobAssignmentService.AddEvaluator(...)` —
   or add a constructor parameter if it should always exist. **List order = priority order** for
   the min/desired fill passes, the `availableWorkers` reservation, and the starvation release
   pass (which only raids lower-priority jobs).
5. Add `GameConfig` constants for its tunables (`AutoJob*` prefix).
6. Proficiency: `JobAssignmentSolver.GetProficiency` already maps `MonsterJob.Fishing` →
   `FishingProficiency`. A brand-new job beyond the existing three needs a case added there.
7. Tests: evaluator math cases in `AutoJobAssignmentServiceTests` (pattern:
   `FarmingDemand_*` / `KitchenDemand_*`), plus a solver staffing case if the job introduces a
   new demand shape.

## Testing

- `PitHero.Tests/JobAssignmentSolverTests.cs` — solver passes, tie-breaks, stickiness, swaps,
  extensibility.
- `PitHero.Tests/AutoJobAssignmentServiceTests.cs` — service wiring, cadence/shift-boundary
  triggers (`TickCadence` is public precisely so tests can drive it without `Core.Services`),
  per-shift segregation, evaluator math. Construct everything directly (no `Core.Services`);
  evaluators accept null dependencies for headless runs.

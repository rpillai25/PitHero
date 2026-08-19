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
| `IJobDemandEvaluator` | `PitHero/Services/AutoJob/IJobDemandEvaluator.cs` | One per automatable job: reports how many workers the job wants right now (`EvaluateDemand(rosterSize, availableWorkers, nocturnal)`) and feeds its backpressure tracker (`SamplePressure`/`ResetPressure`). |
| `BackpressureTracker` | `PitHero/Services/AutoJob/BackpressureTracker.cs` | Per-job smoother: instant attack, EMA decay, one-worker-per-interval drain. Owned by each evaluator. |
| `FarmingJobDemandEvaluator` | `PitHero/Services/AutoJob/FarmingJobDemandEvaluator.cs` | Farming demand (non-sticky). |
| `KitchenJobDemandEvaluator` | `PitHero/Services/AutoJob/KitchenJobDemandEvaluator.cs` | Kitchen demand (sticky). |
| UI gating | `PitHero/UI/SettingsUI.cs` (checkbox), `PitHero/UI/MonsterUI.cs` (job buttons non-clickable while enabled) | |
| Persistence | `SaveData.AutomateMonsterJobs` (v19, section 34) → `AutoJobAssignmentService.Enabled` | Loading never forces a reshuffle; persisted jobs stand until the next cadence tick. |

## Tunables

All in `GameConfig` (`AutoJob*` prefix for headcount scaling, `KitchenRole*` for the role mix).
Times are scaled seconds (1 scaled second = 1 in-game minute; pause freezes them all).

| Constant | Value | What it does / when to change it |
|---|---|---|
| `AutoJobReassessIntervalSeconds` | 15 | Solve/apply cadence. Raise if assignments visibly change too often overall; lower for snappier reaction to demand. |
| `AutoJobPressureSampleIntervalSeconds` | 5 | Headcount backpressure sampling cadence. Rarely needs touching. |
| `AutoJobScaleDownDrainIntervalSeconds` | 60 | Min gap between releasing successive FARM workers. Raise if farmers leave/return too often between waves. |
| `AutoJobKitchenScaleDownDrainIntervalSeconds` | 180 | Min gap between releasing successive KITCHEN workers. Raise if kitchen departures look churny; lower if surplus staff linger too long. |
| `AutoJobPressureDecayAlpha` | 0.15 | EMA decay on falling headcount pressure. Lower = grants hold longer after a rush. |
| `AutoJobKitchenHighWaitSeconds` | 60 | Patron wait (to order or be served) that adds +1 worker of kitchen pressure. Lower to react to unhappy patrons sooner. |
| `AutoJobFarmTasksPerWorker` | 6 | Outstanding farm tasks each farmer absorbs. Lower = more farmers per wave. |
| `AutoJobKitchenBaseStaff` | 3 | Cook + server + runner floor. Do not lower — a runner-less kitchen runs the fridge dry. |
| `AutoJobKitchenBacklogPerExtraWorker` | 3 | Backlog items per extra kitchen worker. Lower = kitchen scales up harder during rushes. |
| `AutoJobKitchenMaxWorkers` | 8 | Kitchen headcount cap. Must equal `KitchenTaskCoordinator.MaxWorkerPosts` (test-asserted). |
| `KitchenRoleMixDwellSeconds` | 45 | Min gap between role-mix recomputes. Raise if `kitchen_role_changed` events stream (see Diagnosing below). |
| `KitchenRolePressureSampleIntervalSeconds` | 5 | Per-role pressure sampling cadence. |
| `KitchenRolePressureEmaAlpha` | 0.1 | Per-role pressure smoothing (~50s time constant). |
| `KitchenRoleMixSwitchMargin` | 1.5 | Smoothed-pressure gap before an occupied post switches role (`ReconcileRoleMix`). Raise if role flips still chase noise; lower (min ~1.1) if the crew re-skews too slowly in rushes. |

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
  `granted − 0.5` (half-worker release hysteresis) AND a full drain interval has passed since the
  last change — one worker per interval, never more. The interval is per-job (tracker constructor
  param): farming uses `AutoJobScaleDownDrainIntervalSeconds` (60 scaled seconds); the kitchen
  uses `AutoJobKitchenScaleDownDrainIntervalSeconds` (180) because a worker walking out of a
  service area mid-rush is far more noticeable than a farmer leaving a field.

Tracker state is transient (never persisted); it rebuilds within a few samples after a load.

## Day/night shifts are solved independently

Day monsters (work 6AM–10PM) and nocturnal monsters (10PM–6AM; see
`MonsterScheduleConfig.IsNocturnal`) are **disjoint workforces that never work at the same time**.
`ReassessNow()` partitions the roster by `IsNocturnal(MonsterTypeName)` and runs demand + solve
separately per shift, passing `nocturnal` to each `IJobDemandEvaluator.EvaluateDemand(rosterSize,
availableWorkers, nocturnal)`. Demand clamps (`rosterSize`) always refer to the *shift's* size,
not the whole roster. Asleep monsters are assigned normally — the coordinators keep them home
until their work window.

**Kitchen night-shift demand (issue #392)**: `KitchenJobDemandEvaluator.EvaluateDemand` returns
Min=0 / Desired=0 / Sticky=true when `nocturnal=true`. The kitchen only operates 6 AM–10 PM;
nocturnal monsters are never assigned there by automation. `FarmingJobDemandEvaluator` accepts
and ignores the `nocturnal` param — farming is shift-agnostic.

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
overload *is* the legacy cycle. `FillRoleMix` is the **from-scratch reference** only — live
recomputes go through `ReconcileRoleMix` (below) so occupied posts never chase pressure noise.

Per-role pressure signals, sampled in the coordinator's `Update()`:

- **Runner**: `AwaitingIngredientsTicketCount` (tickets stalled on a storage fetch — this already
  covers the queued fetch jobs) + `BusJobCount` (dirty plates).
- **Cook**: `ReadyToCookUnclaimedCount` (posted tickets no cook has picked up).
- **Server**: `PlatedAwaitingPickupCount` (dishes cooling on serving tables) +
  `MercenaryManager.CountPatronsWaitingToOrder()`.

**Pressure smoothing:** these counts seesaw within a single service cycle — orders just taken
spike runner work, plated dishes spike server work — so an instantaneous reading at recompute
time would hand the marginal post to whichever side of the seesaw got sampled, then hand it
back at the next dwell (observed in playtesting as a worker ping-ponging Runner↔Server at
exactly the dwell cadence). The mix therefore reads EMA-smoothed pressures, sampled every
`KitchenRolePressureSampleIntervalSeconds` (5 scaled seconds) with weight
`KitchenRolePressureEmaAlpha` (0.1 ≈ 50 scaled-second time constant, spanning a full cycle) —
the marginal post only moves on a sustained bottleneck shift.

**Incremental reconcile + switch margin (`ReconcileRoleMix`):** smoothing alone was not enough.
Early in service the entire kitchen signal is a *single ticket* pulsing runner → cook → server
pressure as it moves through the pipeline; the EMA turns over ~60% of its weight within one
dwell period, so from-scratch recomputes still chased the 0↔1 pulse (observed 2026-08-11:
Cook→Runner, Runner→Cook, Cook→Runner flips at exactly the dwell cadence with the tavern nearly
empty). The mix is therefore updated **incrementally from the previous mix's counts**:

- **Growth** (crew got bigger) only *adds* posts — base-crew floors first, then D'Hondt on the
  smoothed pressures, least-staffed role when nothing is pressured. Existing posts are never
  reshuffled; the new worker is spawning anyway, so this is churn-free.
- **Shrink** removes the lowest pressure-per-worker role above its base-crew floor.
- **Rebalance** — the *only* path that reassigns an occupied post — moves at most **one** post
  per recompute, and only when the gaining role's smoothed pressure exceeds the losing role's by
  `GameConfig.KitchenRoleMixSwitchMargin` (1.5 workers-worth) *and* the move strictly improves
  the D'Hondt balance. A lone ticket pulses each signal by 1, so noise can never clear the
  margin; a sustained multi-ticket imbalance re-skews the crew one post per dwell period.

**Anti-thrash dwell:** a role change sends the worker home to despawn and respawn, so the mix is
reconciled at most once per `GameConfig.KitchenRoleMixDwellSeconds` (45 scaled seconds) — except
when the post count itself changes, which reconciles immediately (spawns and despawns are
happening anyway, and reconcile growth/shrink can't touch unrelated posts).

**Role retention:** the mix is applied as a multiset of role *counts*
(`AssignRolesWithRetention`), not position-by-position: a live worker keeps its current role as
long as that role still has quota, with quota consumed in proficiency order so a shrinking role
sheds its worst holder first; only leftover quota is assigned to unmatched posts (Cook → Server →
Runner). A recompute whose counts don't change therefore causes **zero** churn. Before this,
roles were tied to sorted-list positions and a recompute could flip two positions — sending both
workers home to respawn in each other's roles (the visible "one cook leaves and another cook
immediately replaces it" shuffle).

Workload getters: `FarmTaskCoordinator.OutstandingTaskCount`,
`KitchenTaskCoordinator.ActiveTicketCount` / `AwaitingIngredientsTicketCount` /
`ReadyToCookUnclaimedCount` / `PlatedAwaitingPickupCount` / `FetchQueueDepth` / `BusJobCount` /
`OldestOpenTicketAgeSeconds`, `CropGrowthService.CropCount`, `CropPlantingService.PlanCount`,
`MercenaryManager.CountSeatedPatrons()` / `CountPatronsWaitingToOrder()` /
`MaxPatronWaitSeconds()`, `TavernPatronComponent.StateElapsedSeconds`,
`PartyDiningService.CountPendingPartyDiners()`. `KitchenTicket.CreatedTime` stamps ticket
creation (`Time.TotalTime`, mirroring `BusJob.EnqueuedTime`; not persisted).

## Diagnosing staffing behavior

Staffing decisions are observable in the debug-build analytics JSONL
(`%LOCALAPPDATA%\<exeName>\analytics\session_*.jsonl`; schema in
`PitHero/docs/AnalyticsSchema.md` → "Monster job staffing"). Grep for `monster_job_changed`
(headcount) and `kitchen_role_changed` (role mix), read `gt` for in-game time, and compare
against these signatures:

- **Healthy**: rush scale-ups arrive as a burst of `toJob:"Cooking"` lines at one timestamp;
  scale-downs release exactly one worker per drain interval (e.g. one farmer `toJob:"None"` per
  hour through the afternoon); `kitchen_role_changed` is rare and coincides with crew-size
  changes.
- **Role-mix thrash**: the same monster flips A→B→A→B in `kitchen_role_changed` at intervals
  matching `KitchenRoleMixDwellSeconds`. Every role change is a walk-home/despawn/respawn round
  trip, so this is highly visible in-game. The switch margin should make this impossible for
  single-ticket noise — if it appears, first check `KitchenRoleMixSwitchMargin` (a signal with
  pulse amplitude ≥ the margin defeats it), then `KitchenRolePressureEmaAlpha`, then the dwell.
- **Headcount churn**: a monster leaves a job and returns within an hour or two
  (`monster_job_changed` pairs). Distinguish real demand cycles (see Known behaviors) from
  boundary oscillation — a backlog hovering at a multiple of
  `AutoJobKitchenBacklogPerExtraWorker` flips desired by ±1; the drain interval bounds the cycle
  to one departure per interval.

This exact workflow (previous session vs. new session event counts + per-monster timelines) is
how the role-retention and pressure-smoothing fixes were validated.

## Known behaviors (intended — do not "fix" without a design decision)

- **The noon bounce**: the backlog dips between breakfast and lunch, so the kitchen trims a
  worker around midday and re-staffs them about an hour later when the lunch rush hits. That's
  demand-following working as designed; the cost is one walk-home/return round trip per day for
  the marginal worker. Softening it further means longer drain intervals (slower response
  everywhere), not a bug fix.
- **Sticky role skew after a rush**: the incremental reconcile deliberately keeps the current
  role counts until a pressure gap clears `KitchenRoleMixSwitchMargin`, so after a runner-heavy
  rush the crew can stay runner-skewed for a while even though a fresh `FillRoleMix` would pick
  a different split. Stability beats the optimal split; sustained imbalances still correct one
  post per dwell period.
- **Suboptimal-but-stable role holders**: role retention deliberately keeps a worker in its
  current role even when a higher-proficiency colleague would ideally hold it — stability beats
  marginal cook-speed. Fresh optimal assignments happen naturally at shift changes and respawns.
- **05:37 pre-dawn send-home**: outstanding farm work often hits zero just before dawn, so the
  whole day crew drains right before the 6AM shift-change reassess re-staffs it. Harmless — the
  monsters are asleep/home anyway.

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

- `PitHero.Tests/JobAssignmentSolverTests.cs` — solver passes, tie-breaks, round-robin desired
  fairness, one-per-solve sticky trim, starvation release, swaps, extensibility.
- `PitHero.Tests/BackpressureTrackerTests.cs` — instant attack, EMA decay, one drain per
  interval, rebound regrant, reset.
- `PitHero.Tests/AutoJobAssignmentServiceTests.cs` — service wiring, cadence/shift-boundary
  triggers (`TickCadence` is public precisely so tests can drive it without `Core.Services`),
  per-shift segregation, evaluator math, and the end-to-end
  `EndToEnd_KitchenScalesPastBaseCrewUnderBacklog_ThenDrainsStepwise` (a real ticket rush staffs
  the kitchen past 3 while farming keeps its caretaker, then drains back one worker at a time —
  note ticket creation withdraws crops, so the test stocks backlog+1 servings to keep
  `HasAnyOrderableDish` true). Construct everything directly (no `Core.Services`); evaluators
  accept null dependencies for headless runs.
- `PitHero.Tests/KitchenServiceLoopTests.cs` — `RoleMix_*` (neutral cycle, pinned base-crew
  posts, caps), `WeightedRoleMix_*` (D'Hondt splits), `Retention_*` (no churn on count-neutral
  recomputes, worst-holder-first shrink, count preservation).

# Auto Job Assignment System (issue #321)

Automates allied-monster job assignment. When the player enables **Settings → Automation →
"Automate monster jobs"**, `AutoJobAssignmentService` periodically measures per-job workload
("demand") and rewrites each monster's `AlliedMonster.Job`. That field write is the *entire*
assignment action: `FarmTaskCoordinator.Update()` and `KitchenTaskCoordinator.Update()` reconcile
worker entities against `Job` + awake state every frame, so no entity or FSM code is involved in
assignment.

## Components

| Piece | File | Role |
|---|---|---|
| `AutoJobAssignmentService` | `PitHero/Services/AutoJobAssignmentService.cs` | Cadence + snapshot + apply loop. Registered/ticked/unloaded in `MainGameScene` (`Begin`, `Update` unpaused block, `Unload`). |
| `JobAssignmentSolver` | `PitHero/Services/AutoJob/JobAssignmentSolver.cs` | Pure static solver. No service or ECS dependencies — fully unit-testable. |
| `IJobDemandEvaluator` | `PitHero/Services/AutoJob/IJobDemandEvaluator.cs` | One per automatable job: reports how many workers the job wants right now. |
| `FarmingJobDemandEvaluator` | `PitHero/Services/AutoJob/FarmingJobDemandEvaluator.cs` | Farming demand (non-sticky). |
| `KitchenJobDemandEvaluator` | `PitHero/Services/AutoJob/KitchenJobDemandEvaluator.cs` | Kitchen demand (sticky). |
| UI gating | `PitHero/UI/SettingsUI.cs` (checkbox), `PitHero/UI/MonsterUI.cs` (job buttons non-clickable while enabled) | |
| Persistence | `SaveData.AutomateMonsterJobs` (v19, section 34) → `AutoJobAssignmentService.Enabled` | Loading never forces a reshuffle; persisted jobs stand until the next cadence tick. |

Tunables live in `GameConfig` under the `AutoJob*` prefix.

## When reassessment runs

`Update()` (per unpaused frame) calls `TickCadence(nowSeconds, isNighttime)`, which fires
`ReassessNow()`:

1. Every `GameConfig.AutoJobReassessIntervalSeconds` (60 scaled seconds = 60 in-game minutes),
   measured on `InGameTimeService.AccumulatedSeconds` so pausing never advances the timer. A
   `now < last` guard restarts the interval after a load rewinds time.
2. Immediately when `IsNighttime` flips (6AM / 10PM shift change), restarting the interval.
3. Immediately when the player first checks the checkbox (`SettingsUI` calls `ReassessNow()`).

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
   that job unconditionally, even above `DesiredWorkers` (kitchen workers are never demoted).
2. **Min pass** — each demand, in list order, fills up to `MinWorkers` from unassigned monsters by
   highest proficiency for that job.
3. **Desired pass** — same, up to `DesiredWorkers`.
4. **Swap pass** — a sticky worker swaps with a non-sticky assignee only when **both** jobs
   strictly gain proficiency (prevents oscillation between reassessments).
5. Everyone unassigned gets `MonsterJob.None` → the coordinators send them home.

A monster holding a job with **no demand entry** is non-sticky by definition and gets pooled and
reassigned — this is the extensibility guarantee (a Fishing-assigned monster before a fishing
evaluator exists just returns to the pool).

## Current demand models

- **Kitchen** (`Sticky = true`, listed first so its small crew staffs before farming absorbs the
  rest): base crew `AutoJobKitchenBaseStaff` = 3 — **cook + server + runner; never less, a
  runner-less kitchen runs the fridge dry** — plus one worker per
  `AutoJobKitchenBacklogPerExtraWorker` backlog items (open tickets + seated patrons + pending
  party diners), capped at `AutoJobKitchenMaxWorkers` (mirrors the coordinator's role-post cap).
  `MinWorkers` = base crew only when backlog > 0.
- **Farming** (`Sticky = false`): `max(burst, baseline)` where burst =
  `FarmTaskCoordinator.OutstandingTaskCount / AutoJobFarmTasksPerWorker` (catches watering/harvest
  waves) and baseline = `(CropCount + PlanCount) / AutoJobFarmCropsPerWorkerBaseline` (quiet
  growth periods), ceil-divided, clamped to shift size. Released farmers become the pool that
  staffs the kitchen or goes home.

Workload getters added for this system: `FarmTaskCoordinator.OutstandingTaskCount`,
`KitchenTaskCoordinator.ActiveTicketCount`, `CropGrowthService.CropCount`,
`CropPlantingService.PlanCount`, `MercenaryManager.CountSeatedPatrons()`,
`PartyDiningService.CountPendingPartyDiners()`.

## Adding a new job (e.g. Fishing)

The solver never changes. Steps:

1. The job must already exist in `MonsterJob` and have a coordinator/FSM work loop that
   spawns/despawns workers off `AlliedMonster.Job` (fishing has the enum value but no work loop
   yet — build that first, mirroring `FarmTaskCoordinator`).
2. Create `PitHero/Services/AutoJob/FishingJobDemandEvaluator.cs` implementing
   `IJobDemandEvaluator`: report `Job = MonsterJob.Fishing` and a `JobDemandEntry` computed from
   whatever workload signal fits (keep the arithmetic in a `public static ComputeDemand(...)` so
   it's unit-testable headless, like the existing evaluators; take service dependencies as
   nullable constructor params).
3. Decide `Sticky`: true only if pulling workers off the job mid-shift is harmful (kitchen-style
   continuity); false if workers can be freely rebalanced (farming-style).
4. Register it in `MainGameScene.Begin()` via `autoJobAssignmentService.AddEvaluator(...)` —
   or add a constructor parameter if it should always exist. **List order = priority order** for
   the min/desired fill passes.
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

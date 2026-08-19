# Tavern Cooking & Dining System

Issue #319 (PR #320). Monster-staffed kitchen serves cooked dishes to walk-in tavern patrons
(unhired mercenaries) and to the player's party (hero + hired mercs). Party meals grant battle
buffs re-injected at every battle start. This doc is the map — read it before diving into the
two big files (`KitchenTaskCoordinator.cs` ~1,100 lines, `KitchenMonsterStateMachine.cs` ~1,050
lines).

## Overview — three flows share one kitchen

1. **Walk-in patrons** — unhired mercs seated in the tavern order random affordable dishes, eat,
   pay + maybe tip, linger, then walk off. Pure economy flavor; no buffs.
2. **Party dining** — while the hero is in Stop mode and seated, `PartyDiningService` feeds
   orders to servers via `IPartyOrderSource`. Members pay at order time and receive a meal buff
   that lasts the in-game day.
3. **Kitchen workers** — allied monsters with `Job == MonsterJob.Cooking` are drafted into
   Cook / Server / Runner roles by `KitchenTaskCoordinator` and driven by
   `KitchenMonsterStateMachine` (a `SimpleStateMachine`, live-only — no virtual-layer
   counterpart; walk routes are instead verified headlessly by `KitchenFlowPathTests`).

## File map

| Area | Files |
|---|---|
| Coordinator (tickets, staffing, fridge, slots) | `Services/KitchenTaskCoordinator.cs` |
| Worker FSM (walk loops per role) | `ECS/Components/KitchenMonsterStateMachine.cs`, `Dining/KitchenMonsterState.cs` |
| Dish data (recipes, buffs, pricing, durations) | `Dining/DishType.cs`, `Dining/DishDefinition.cs`, `Dining/DishConfig.cs` |
| Ticket | `Dining/KitchenTicket.cs` |
| Party dining | `Services/PartyDiningService.cs`, `Dining/IPartyOrderSource.cs`, `UI/FoodTab.cs`, `UI/StopAdventuringUI.cs` |
| Meal buffs | `Services/MealBuffService.cs`, injection in `Combat/BattleEngine.cs` |
| Patrons | `ECS/Components/TavernPatronComponent.cs`, spawning/churn in `Services/MercenaryManager.cs` |
| Seats/tables/plates | `Config/TavernSeatConfig.cs` |
| Dish world sprites | `Services/DishEntityService.cs` |
| Job hats | `Services/KitchenHatService.cs` |
| Fridge inventory (issue #386) | `Services/FridgeInventoryService.cs` (32 slots, flat 10-unit stacks) |
| Refrigerator window | `UI/RefrigeratorDialog.cs`; open/close pause + window-size gate in `MainGameScene.UpdateFridgeDialogGate` (sets `SettingsUI.ExternalUIWindowOpen`) |
| Storage holds (held-for-transfer) | reservation ledger inside `Services/CropStorageInventoryService.cs` |
| Runner carry level | `GameStateService.RunnerCarryLevel` + `GameConfig.GetRunnerCarryUnits` |
| Constants | `GameConfig.cs` ("Kitchen / Tavern Dining" block) |
| Save | `Services/SaveData.cs` + `SaveLoadService.cs` (v18 section 33; fridge §45 v28, carry level §46 v29) |
| Tests | `PitHero.Tests/KitchenServiceLoopTests.cs` (logic), `KitchenFlowPathTests.cs` (map routes), `DishPricingTests.cs`, `FridgePreStockTests.cs` + `CropStorageReservationTests` (pre-stock + holds), `FridgeInventoryServiceTests.cs` |

## Tile geography

All coordinates are tiles on the surface map (`PitHero.tmx`). Static helpers live on
`KitchenTaskCoordinator`.

**Kitchen (x 82–88, north wall y=2):**

| Post | Tile(s) | Helper |
|---|---|---|
| Ticket board (servers post, cooks read) | (82,2) | `TicketBoardTile` |
| Stoves 1–3 (cook stands here) | (83,2) (84,2) (85,2) | `GetStationTile(i)` |
| Sink (orphan dishes, bussed plates) | (86,2) | `SinkTile` |
| Fridge (cooks gather, runners restock) | (87,2) | `FridgeTile` |
| Serving tables (dish sits here) | (87,3) (87,4) (87,5) | `GetServingTile(slot)` |
| Serving **approach** (worker stands here) | (86,3) (86,4) (86,5) | `GetServingApproachTile(slot)` |
| Runner wander box | x 83–88, y 6–8 | `RunnerWanderAnchorTile` |
| Cook wander box | x 82–84, y 2–3 | `GameConfig.KitchenCookWander*` |

Workers never stand on a serving table: cooks/servers path to the approach tile (one tile left)
and the dish entity spawns on the table tile.

**Tavern (x 91–99):** four 4-seat tables — left-upper (93,3), right-upper (97,3), left-lower
(93,7) = the **party table**, right-lower (97,7). `TavernSeatConfig` registers the 12 seats
around them (9 patron + 3 party) plus per-seat facing and plate pixel offsets (`TryGetPlateWorldPosition`
returns the dish-sprite center on the table). Party seats: hero (93,6), merc1 (92,7),
merc2 (94,7) (`GetPartySeatTile`). Server zone split: top zone tables have Y ≤ 4, bottom zone
Y ≥ 5. Patrons spawn at (104,11) and exit via (103,6).

## Ticket lifecycle

`KitchenTicket` is a bag of public fields (including `ServerEntity` — the worker who took the
order, set in `TakeOrderAtTarget` and cached on `TavernPatronComponent` for the server's
farewell speech bubble; null for party tickets); `TicketState`:

```
AwaitingIngredients → ReadyToCook → Cooking → Plated → Delivering → Delivered
        (any state) → Canceled
```

| Transition | Method (KitchenTaskCoordinator) | Notes |
|---|---|---|
| create | `CreateTicket(dish, isParty, partySlot, patron, seatTile)` | Reserves ingredients (below). Starts `ReadyToCook` if fridge covered everything, else `AwaitingIngredients` + fetch job enqueued. **Not yet visible to cooks.** Cap: 16 live tickets. |
| post | `PostTicket(t)` | Sets `PostedToBoard = true`; idempotent. Servers do this at the board. |
| cook claims | `TryReadNextTicket()` | Party tickets first, then FIFO. Sets `CookClaimed`. |
| ingredients arrive | `CompleteFetch(t)` | `IngredientsFetched = true`; `AwaitingIngredients → ReadyToCook`. |
| cooking starts | `BeginCookingAtStation(t, proficiency)` | `→ Cooking`; `CropsRefundable = false`; rolls `IsDeluxe`. |
| cooking ends | `FinishCooking(t)` | Frees the station; cook now carries the dish. |
| plated | `PlaceDishOnServing(t, entity)` | `→ Plated`; dish entity sits on a serving table. |
| server picks up | `TryPickupReadyDish(zone, …)` | `→ Delivering`; frees serving slot. Orphans first (returns `toSink=true`). |
| delivered | `OnTicketDelivered(t, entity)` | `→ Delivered`; notifies patron component or party source. |
| eaten | `NotifyPatronFinishedEating` / `NotifyPartyMemberFinishedEating` | Removes ticket; patrons get an EmptyPlate + bus job. |
| canceled | `CancelTicket(t)` / `CancelTicketForPatron(entity)` | See refund rules below. |

Cook interruption paths: `ReleaseCookTicket(t)` (shift end / despawn) un-claims and resets
`Cooking → ReadyToCook` so another cook resumes from the board.

## Ingredient reservation — the key contract

**Crops are physically withdrawn at ticket creation, not when the runner walks.**
`CreateTicket` does an all-or-nothing availability check (fridge + all CropStorage buildings),
takes from the fridge first, then withdraws the shortfall from storage (rolling everything back
if a mid-loop withdraw fails). `FridgeTakenQty[]` / `StorageTakenQty[]` remember the split for
refunds.

- If everything came from the fridge → `IngredientsFetched = true`, ticket starts `ReadyToCook`.
- Any storage shortfall → ticket starts `AwaitingIngredients` and is enqueued as a **fetch job**,
  and the buildings drawn from are recorded in `SourceBuildingIds` so the runner can retrace the
  route. The runner's trip is **cosmetic for this ticket** — the crops are already committed. At
  each storage door, `RunnerCollectAtStorage` additionally HOLDS top-up crops toward the
  pre-stock target (`PreStockStackSize` 1–4 stacks × `KitchenFridgeStackSize = 10` units per
  recipe crop, carry-capped) via the storage reservation ledger + the runner's carry queue;
  at the fridge, `DeliverCarriedTopUp` consumes the holds (units physically leave storage NOW)
  and `CompleteFetch` flips `IngredientsFetched`. If storage vanishes mid-run the ticket still
  proceeds.

## Fridge pre-stock (issue #386)

The fridge is a slot-based inventory (`FridgeInventoryService`, one 8×4 page, every stack a
flat 10 units) that runners proactively keep stocked: for each crop that appears in ≥1 dish
recipe and has stock in some CropStorage, the coordinator queues a **pre-stock job** whenever
fridge units fall below `PreStockStackSize × 10` (throttled in `Update`, and immediately at the
end of `CreateTicket` / `CreateTicketPreReserved` / `CancelTicket`, plus after every
`PreStockDeliver` so under-target crops turn the runner straight around). An idle runner (after
bus and ticket-fetch jobs) claims a trip (`TryClaimPreStockJob` — nearest storage holding the
front-of-queue crop, batching up to 3 queued crops that same storage holds, one per hand slot),
sprints out, and the trip is **two-phase held cargo**: `PreStockCollect` RESERVES the crops at
the door via `CropStorageInventoryService.Reserve` (**re-clamping each crop against the live
target** so a trip that raced another top-up never overshoots) — the units stay physically in
their storage slots, so a save or quit at ANY moment loses nothing — and `PreStockDeliver`
consumes the holds at the fridge (`WithdrawReserved` + fridge deposit): **units physically
leave storage and fridge stock rises only at the unload**. Held units are invisible everywhere:
`AvailableIn`/`AvailableTotal` exclude them, `WithdrawUpTo`/`TryWithdrawAcrossBuildings` can't
touch them (order reservations included), auto-sell skips crops with holds, and the crop
storage viewer shows/sells only available units (`CopyDisplaySlots`/`TakeFromSlot`). The job's
crops also stay busy-masked while carried so the deficit recompute never dispatches a second
runner for cargo in transit. Abandoning a trip (despawn, go-home, canceled tour) just releases
the holds (`ReleaseCarried`) — nothing ever moved, so nothing can be lost. If physical units
vanish under a hold anyway (player force-sells, building sold/moved) the ledger clamps and the
unload shorts gracefully. `PreStockQueueDepth` feeds runner backpressure. Clicking the fridge
in the kitchen opens the Refrigerator window (`RefrigeratorDialog`): the stack grid, the
persisted Pre-Stock Stack Size slider, and per-stack Send to Crop Storage / Sell actions.

**Runner carry level** (`GameStateService.RunnerCarryLevel`, persisted save v29 section 46):
runners carry crops by hand, so every hand-carried amount — pre-stock trips AND the
opportunistic ticket-trip top-up — is capped at `GameConfig.GetRunnerCarryUnits(level)` units
per crop type (level 1 → 1, level 2 → 5, level 3 → 10; up to
`KitchenRunnerCarryCropTypes = 3` crop types per trip). The level is global, starts at 1
(constant storage runs early on), and will be raised by one-of-a-kind items the hero finds
(future feature). The ticket's own reserved shortfall moved at order time and is exempt.
Carry visuals show only crops actually held from storage: pre-stock trips show the carry
queue's distinct crops, ticket trips show recipe entries with `StorageTakenQty > 0` (a crop
fully covered by the fridge never appears in hand).

**Save-anytime lossless**: held cargo is a transient reservation over units that never left
their storage slots, so the save gather always sees the full physical inventory. On load the
ledger starts empty and runners simply re-fetch.

**Staff exits (runner-only routing)**: runners path with
`KitchenTaskCoordinator.RunnerPathfinder` (selected per role via the FSM's `ActivePathfinder`),
a second `FarmPathfinder` where the collision tiles at (91,10) and (101,10)
(`GameConfig.KitchenRunnerStaffExit*`) are opened with `RemoveStaticWall` (survives
`RebuildWalls`) AND the tavern dining floor (x91–99, y2–8) is `AddWeightedTile` at 5× step
cost. The weighting is what makes crop runs *favor* the side corridor — the staff route is
physically longer, so plain shortest-path ignored it. Jobs whose destination is inside the
tavern (plate bussing) still enter, since all in-tavern routes carry the same weight. Both
tiles stay solid on the shared `Pathfinder` used by every other worker and patron. Guarded by
`KitchenFlowPathTests.RunnerCropRun_FavorsTheStaffExitOverTheMainEntryway`.

**Refrigerator window**: clicking the fridge (white hover outline, statue-style tile hit test
in `MainGameScene`) opens `UI/RefrigeratorDialog.cs` — the stack grid, the persisted Pre-Stock
Stack Size slider, and per-stack Send to Crop Storage / Sell actions. While it is open the
game pauses and a half-size window temporarily restores to normal
(`MainGameScene.UpdateFridgeDialogGate`, a visibility edge-watcher covering both the Close
button and outside-click dismissal). Any scene-owned dialog like this must also set
`SettingsUI.ExternalUIWindowOpen` while visible, or the top bar can auto-hide at the wrong
button scale and return parked half off-screen after the half-window restore.
- Milk/cheese (`UsesMilk`/`UsesCheese`) are display-only — never in recipes, prices, or checks.

**Cancellation refund rules** (`CancelTicket`): while `CropsRefundable` (pre-cooking) both
fridge and storage takes are refunded, and a paid party order refunds the gold. Once cooking
started, ingredients are spent; a non-party ticket still collects the dish price (no tip).
A `Plated` cancel turns the dish into an **orphan** (`_orphanServing`) that keeps its slot until
a server sinks it; a `Delivered` cancel enqueues a bus job; a dish being carried is diverted to
the sink by the carrier's FSM when it sees `Canceled`.

## Kitchen workers

**Staffing** (coordinator `Update`, per frame): candidates = allied monsters with
`Job == Cooking` that are awake per `MonsterScheduleConfig.IsAsleep` (in-game time = the shift
system), sorted by `CookingProficiency` descending. The role mix fixes posts 0–2 as Cook,
Server, Runner, then gives posts 3+ to the role with the highest backpressure per assigned
worker (issue #375: stalled-fetch tickets + dirty plates → runners, unclaimed board tickets →
cooks, plated dishes + patrons waiting to order → servers; D'Hondt greedy over EMA-smoothed
pressures). With no pressure it falls back to the legacy Cook → Server → Runner cycle —
**cook1, server1, runner1, cook2, server2, runner2, cook3, runner3** (`MaxWorkerPosts` = 8 =
3 cooks + 2 servers + 3 runners). Live recomputes are **incremental** (`ReconcileRoleMix`):
crew growth only adds posts, shrink drops the lowest-pressure role, and an occupied post only
switches role when the smoothed-pressure gap clears `GameConfig.KitchenRoleMixSwitchMargin`
(one move max per recompute) — a lone early-morning ticket pulsing through the pipeline can
never flip a post (`FillRoleMix` is the from-scratch reference used at spin-up and in tests).
The mix is reconciled at most once per `KitchenRoleMixDwellSeconds` (role changes are expensive
walk-home/respawn round trips), except immediately on head-count changes. The mix is
applied as role *counts* with retention (`AssignRolesWithRetention`): live workers keep their
current role while quota remains, so a recompute with unchanged counts never reshuffles anyone.
Change a `GameConfig.MaxKitchen*` constant and the order re-derives — but keep
`AutoJobKitchenMaxWorkers` in sync (a test asserts it). Workers whose role/slot disappears get `RequestReturnHome()` (finish current task,
walk into the house, despawn); a restored assignment calls `CancelReturnHome()`. Spawn is at
their Monster House door (anchor +2 south); no collider/TAG_MONSTER, so workers never trigger
battles. A 5s sweep calls `EnsureHat()` — `KitchenHatService` pools 7 hat entities
(ChefHat/ServerHat/CourierHat sprites, parented above the head) and grows the pool if a shift
overlap exhausts it.

**Server loop** (`ServerDecide`): priority is (1) deliver plated dishes for its zone,
(2) take orders — party members first, then nearest waiting patron, batching up to
`ServerOrderMemoryLimit = 3` before a single trip to the board, (3) wander its table area
(interruptible). **Bussing belongs to the runners** — it was the server bottleneck (issue #327).
A server only falls back to bussing while `HasActiveRunner` is false, so a cook+server-only
kitchen still clears its tables; in that mode the old two-tier priority applies (a plate older
than `ServerBusPlateMaxWaitSeconds = 90` bumps ahead of deliveries and orders, otherwise plates
come after orders). Pickup: walks to the middle serving approach tile,
grabs up to `ServerCarryDishLimit = 2` dishes for its zone, delivers each to the seat's plate
position. One server on shift = `ServerZone.AllTables`; two = first works `TopTables`, second
`BottomTables` (recomputed live, so zones re-shard when staffing changes).

**Cook loop**: read ticket at board (1s pause) → claim station → fridge (wait there until
`IngredientsFetched`) → station, cook for `DishConfig.GetCookDuration(dish, proficiency)`
(5/7/10s base by class, −6%/proficiency point, floor 5s; seconds = in-game minutes) → carry
dish to its reserved serving slot's approach tile, place it facing right. If all 3 slots are
full the cook holds the dish (`CookWaitServingSlot`); at shift end `ForceReserveServingSlot`
overflows onto slot 0 rather than stranding the dish (pickup scans tickets, not slots, so this
self-heals). Deluxe roll happens at cook start: `proficiency × 5%` capped 45%.

Between tickets the cook potters around the board and the first two stoves (`CookWander`,
x 82–84 / y 2–3) instead of standing frozen at the board. `HasReadableTicket()` — a non-claiming
peek that must mirror `TryReadNextTicket`'s filter — pulls it straight back. Claiming still
happens only at the board, with the read pause. A cook holding a ticket while every station is
busy waits at the board rather than wandering.

**Runner loop** (`RunnerIdle`): **dirty plates first**, then ingredients — a plate left on a
table keeps that seat out of service and parks arriving patrons at the door, which costs more
than a slow order. Backing orders up a little is the accepted trade.

- *Bus* (`RunnerBusPlate` → `RunnerWalkToSink`): claim a bus job (zone-free, oldest first) →
  sprint to the plate → pick it up → keep claiming and collecting while under
  `RunnerCarryPlateLimit = 3` → sprint the stack to the sink. The plates show on the runner's
  three carry renderers (center / left / right), the same rig as a crop haul.
- *Fetch*: `PlanFetchRoute` builds a tour of the storages that **actually hold the crops** —
  the buildings this ticket's shortfall was withdrawn from (`KitchenTicket.SourceBuildingIds`,
  the only record of that, since the crops left storage at order time) plus any that can still
  top the fridge up to par. Nearest-first, dropping stops that later become redundant, capped at
  `RunnerMaxStorageStops = 3`. Then: sprint (3×) to each door → 1s collect (`RunnerCollectAtStorage`
  draws **only on that building** via `WithdrawUpTo`) → next stop → carry crop sprites back to
  the fridge → `CompleteFetch`. A multi-crop recipe spread over two storages visits both; the
  longer trip is the point. Never interrupted mid-trip by a plate; prioritization happens at
  claim time only.

  Route planning is best-effort: stock can shift between planning and arrival, and the ticket's
  own ingredients were already reserved at order time, so a short or stale route never blocks a
  cook. With no storage left standing the runner completes the fetch on the spot; with none
  worth entering it still makes one trip to the nearest door (`BuildingId = -1` = draw from all).

A claimed-but-not-yet-picked-up plate goes back to the queue via `ReleaseBusJob` when the runner
despawns (it keeps its original `EnqueuedTime`, so it stays at the head of the line). Plates
already in hand had their entities destroyed at pickup, so they're simply gone — the tables are
clear either way, which is all the seat gate cares about.

## Walk-in patrons

`MercenaryManager` spawns unhired mercs into 9 fixed seats. The arrival interval is
**time-of-day dependent** (issue #392, `TavernScheduleConfig.GetArrivalIntervalMultiplier`):

| Window | Hours | Multiplier | Effective interval |
|---|---|---|---|
| Morning rush | 6–8 AM | 0.5× | 30–60s rolled per arrival |
| Lunch rush | 12–2 PM | 0.5× | 30–60s |
| Dinner rush | 6–9 PM | 0.5× | 30–60s |
| Off-hours / overnight | all other hours | 2× | 120–240s |

The multiplier is applied **at compare-time** (not reset-time), so a mid-wait hour flip adapts
immediately. The empty-tavern 5s fast path also scales (2.5s at rush, 10s off-hours) but is
**suppressed entirely while the kitchen is closed** — overnight the tavern may sit empty between
trickle arrivals. Patrons keep arriving overnight at the 2× slow-trickle rate — a night phase
with stage/bar is planned later.

**Closed-hours arrivals are purely timer-driven.** While the kitchen is open, a departing patron
is replaced almost immediately (the walk-off coroutine calls `TrySpawnMercenary` directly after a
2s beat, and a full tavern evicts the oldest `FinishedEating` patron for a fresh face). While the
kitchen is **closed**, both paths are disabled: no fresh-face evictions, and each departure
resets `_timeSinceLastSpawn` instead of spawning a replacement — so the closing exodus empties
the tavern and the next patron trickles in a full overnight interval later.

At the seat a `TavernPatronComponent` is added. **A patron never sits down at a table that still
has an un-bussed plate.** `GetAvailableTavernPosition` prefers a free seat that is already
cleared, and `TryReseatToClearedSeat` re-checks (plates appear and get bussed during the walk
in) — only when *every* free seat is dirty does the patron wait at the tavern door (100,6),
retrying the reseat every 0.25s until a plate is cleared. Waiting patrons are unseated, so they
add no ordering pressure while the backlog drains. When full, the oldest patron in
`FinishedEating` is walked off to free a seat — never one still waiting or eating.

`PatronState`: `WaitingToOrder → Ordered → FoodDelivered → Eating → FinishedEating`.
Patience: 10 min pre-order and post-order (expiry cancels the ticket and leaves immediately);
after eating they linger 5 min. **Closed-kitchen patience**: while the kitchen is closed
(10 PM–6 AM) and the patron is still `WaitingToOrder`, the effective pre-order patience
threshold drops to `PatronPatiencePreOrderSeconds × PatronClosedKitchenPatienceFactor`
(600s × 0.25 = 150s ≈ 2.5 in-game hours, tunable via `GameConfig`). A patron seated before
10 PM with accrued wait above that threshold leaves promptly at close; overnight arrivals sit
for ambiance and then walk off via the normal `LeaveOnPatienceExpiry` path. Patrons in
`Ordered` / `FoodDelivered` / `Eating` / `FinishedEating` keep their normal timers — the
kitchen finishes serving them.

On delivery the patron faces their table (`TavernSeatConfig.GetFacing`). On finishing: pays
`DishConfig.GetPrice`, 50% chance of a 5–15% tip (rounded up), logs `dish_served`. Hiring a
patron mid-order calls `CancelTicketForPatron` before removing the component. Patrons order a
random dish from `GetOrderableDishes` (= every dish whose recipe fridge+storage can cover).

## Kitchen hours (10 PM – 6 AM closure)

Issue #392. `TavernScheduleConfig.IsKitchenClosed(hour)` returns true for hours ≥ 22 or < 6.

- **No new orders**: `KitchenMonsterStateMachine.TryPickNextOrderTarget` and `HasOrderWork` both
  return false when closed. `KitchenTaskCoordinator.CreateTicket` also returns null as a
  belt-and-braces guard. (`CreateTicketPreReserved` is exempted — it is the save-reload path.)
- **Crew wind-down**: `KitchenTaskCoordinator.Update()` computes `closed` (from `timeService`,
  null → open) and `HasClosingWork(hasUndeliveredTickets, busJobCount, diningPatrons)` —
  undelivered tickets (any state not `Delivered`/`Canceled`), plates queued for bussing, and
  seated guests still at their food (`MercenaryManager.CountPatronsDining()`:
  `FoodDelivered`/`Eating`/`FinishedEating`). While closed and no closing work remains, workers
  are not added to `_wantedAssignments`, so the existing reconcile sends everyone home via
  `RequestReturnHome`. The crew therefore delivers every in-flight dish, waits out the last
  eaters, busses the final plates, and only then drains — leaving every table clean for
  overnight arrivals (a crew that left at the last delivery stranded dirty tables all night and
  door-waiters piled up). This is independent of `IsAsleep`, which means nocturnal workers
  (Orc, Skeleton, GhostMiner — awake 10 PM–6 AM) are also excluded from kitchen duty overnight.
- **Shift-end speech bubble**: `ReturnHome_Enter` now says the shift-end bubble when
  `IsAsleep(...) || IsKitchenClosed(hour)` (so nocturnal workers' closing-time departure looks
  like a real shift end, not a role change).
- **Overnight leftovers**: rare — the crew now outlasts the last eater and busses their plates
  before draining. A plate can still be orphaned by an edge case (e.g. a bus claim released by a
  despawning worker after the drain decision); it sits until the 6 AM crew clears it.
- **Auto job assignment**: `KitchenJobDemandEvaluator.EvaluateDemand` returns Min=0 / Desired=0 /
  Sticky=true when `nocturnal=true` — kitchen only operates on the day shift.
- **Manual job window**: `MonsterUI.RefreshMonsterList` disables the Kitchen job button 10 PM–6 AM
  in manual mode: dimmed, unclickable, hover text "Kitchen closed" (`UITextKey.JobKitchenClosed`).
  A monster already holding Cooking keeps the job; the coordinator drains it and refields at 6 AM.

Patrons, servers, cooks, and runners emit random speech bubbles at key moments (order taken,
payment — tip-gated variants, patron walk-off farewell via `KitchenTicket.ServerEntity`, dish
plated, fetch trip start) — see [SpeechBubbleSystem.md](SpeechBubbleSystem.md) for the catalog
and hook rules (patience-expiry leavers get no farewell; these paths run headless in tests).

## Party dining

Rides **Stop mode** — no new GOAP surface. Entry paths:

- **Manual**: player hits Stop; hero (and hired mercs) walk to the tavern and sit
  (`WalkToTavernForStopAction`, `HeroComponent.StoppedAdventure && SeatedInTavern`). The party
  sits regardless of the "Eat at tavern" checkbox; when it's off, servers ignore them entirely
  and focus on walk-in patrons.
- **Three auto-dine meals** (issue #392): `BeginAutoDine(MealPeriod meal)` fires three times
  per in-game day:
  - **Breakfast** (6 AM): `SleepInBedAction` calls `BeginAutoDine(MealPeriod.Breakfast)` after
    the party wakes from night sleep.
  - **Lunch** (12 PM): `MainGameScene` fires at the 12 PM hour-edge (`ResetForNewMealPeriod()`,
    then `BeginAutoDine(MealPeriod.Lunch)`).
  - **Dinner** (6 PM): same pattern with `MealPeriod.Dinner`.

  Each call checks: "Eat at tavern" on → party not already player-stopped → kitchen open →
  `HasEatenThisMeal` false → **some dish the hero can order** exists (`TryPickHeroDish`:
  Food-tab favorite first, then the hero's two job fallbacks via
  `DishConfig.GetFallbackForJob`, each gated on `CanCoverRecipe` AND price — one missing crop
  no longer starves the whole party). A full shortfall skips the trip with a session-console
  line (console keys: `ConsoleLunchSkipped` / `ConsoleDinnerSkipped`) **and a hero skip
  bubble** (`HeroLunchSkipped` / `HeroDinnerSkipped`, breakfast keeps
  `HeroBreakfastNoIngredients`) on the no-ingredients branch only. Every `BeginAutoDine`
  outcome logs a `party_meal_trip` analytics event (`started`, or the skip reason:
  `hero_not_present` / `already_stopped` / `kitchen_closed` / `kitchen_unstaffed` /
  `already_ate` / `no_ingredients` / `no_gold` / `no_stop_ui`) so a silently skipped meal is
  diagnosable from the session log.
  `BeginAutoDine` no-ops if the party is already player-stopped. `ResetForNewMealPeriod()` runs
  **before** `BeginAutoDine` at every edge so `HasEatenThisMeal` is cleared for the new period.
  At 10 PM a stopped party leaves the table for night sleep; `StoppedAdventure` stays true.

`PartyDiningService` implements `IPartyOrderSource`; the coordinator polls
`TryGetNextPartyOrder`. **The hero leads the meal**: he orders the Food-tab favorite
(`FavoriteDishId`), falling back through his job's two cheap dishes when the favorite can't
be made or afforded (`TryPickHeroDish` — the same ladder `BeginAutoDine` pre-checks), and
pays for it; only when nothing he can order is available do the servers skip the whole
party — mercenaries never eat unless the hero eats. Mercenary meals are **free** (no
gold in or out): job favorite via `DishConfig.GetFavoriteForJob(job)`, falling back through
two job-specific cheap dishes, ingredient-gated only. Skips log `party_dine_skipped`
analytics once (reason `already_ate` / `no_ingredients` / `no_gold`) but are **re-evaluated
every poll**, so a seated party is still served when ingredients/gold appear later. One meal
per member per **meal period** (`HasEatenThisMeal`, reset by `ResetForNewMealPeriod()` at each
6/12/18 AM/PM edge). `MealBuffService.ClearAll()` runs at the 6 AM edge as belt-and-braces
(the last dinner buff expires ~4 AM; the 6 AM clear removes any stragglers).

**The hero pays at order time** (`OnPartyOrderTaken`), unlike patrons who pay after eating.
Eating runs on `GetEatSeconds` (5/7/10s by class); `FinishMember` applies the meal buff, logs
`dish_served` (party=true, tip=0), and notifies the coordinator. Resuming play mid-meal
cancels outstanding tickets (refunding gold only while `CropsRefundable` and `HasPaid`), but
a `Delivered` dish is fast-tracked — buffs still granted. `CheckAllDone` holds the trip open
for un-fed members only while they can actually be served (EatAtTavern on + kitchen staffed);
otherwise in-flight meals finish and the party resumes — no endless sitting.

## Meal buffs

`MealBuffService` keeps one `(combatant, dish, deluxe, expiresAtSeconds)` record per active
meal per member. Buffs last **6 in-game hours** (`GameConfig.MealBuffDurationSeconds = 360f` ×
1 real-second-per-in-game-minute time scale). Each `MealRecord` stores an absolute
`ExpiresAtSeconds` stamp; `Prune(nowSeconds)` removes expired records every frame (reverse
iteration, no allocation). Three meals per day means up to three active buff slots around
dinner time if the player eats them close together.

Buffs are **not persistent battle state** — `BattleEngine` clears battle state at battle start
and then calls `InjectBuffsAtBattleStart` for the hero and each merc (and for late-joining
mercs), adding each non-expired dish buff as `BattleBuff(type, magnitude, -1, "meal")` — turns
= -1 means "until battle end". Injection is pure list writes: **it consumes no battle RNG**
(RNG call order is a contract — see `VirtualGameLogicLayer.md`). Deluxe meals scale magnitude
×1.5 rounded up. MagicUp feeds skill formulas via `ICombatant.GetSkillStats()`; HP/MP regen
ticks at end of round. Food never restores HP/MP directly — that's the inn's job.

## Dish data

16 dishes (`DishType` 0–15, persisted as int — **values must stay stable**). Each
`DishDefinition`: recipe (crop×qty), milk/cheese flags, buffs, `CookTimeClass`
(Simple/Standard/Complex), `EatTimeClass` (Snack/Meal/Feast), `BaseSpriteName` (CropsProps
atlas; `_Large` for UI, `_Small` for world), `NameKey`. See `DishConfig`'s static ctor for the
full table — from Onion-skewer starters up to `HarvestFeastPlatter` (11-crop recipe, 6 buffs).

**Pricing is derived, not hardcoded** (`DishConfig.ComputePrice`, cached):
`ingredientSellValue × 1.25 + effect premium (15g/stat point, 10g/MAG, 3g/EVA, 30g/regen
point)`, rounded to 5g, min 10g. A monotonicity pass then guarantees that among single-buff
dishes of the same buff type, more magnitude costs strictly more. Rebalancing crop sell prices
reprices the whole menu automatically (`DishPricingTests` guards this).

## Persistence (section 33)

Save version **30** (issue #392). Persisted per party slot (`SavedDiningRecord`):
`OrderedDishId`, `HasPaid`, `HasEatenThisMeal` (renamed from `HasEatenToday`),
`MealDishId`, `MealDeluxe`, `MealExpiresAtSeconds`; plus `FavoriteDishId` and `EatAtTavern`.
v29 files still load (backwards compatibility is mandatory — see AGENTS.md "Save Format"):
`MealExpiresAtSeconds` is read only when `fileVersion >= 30` and defaults to 0 for v29, so a
pre-#392 meal buff is dropped cleanly and the party simply re-eats at the next meal period.
On load, meal buffs are rebuilt via `MealBuffService.RestoreRecord` only when
`MealExpiresAtSeconds > InGameTimeService.AccumulatedSeconds` at load time (expired records
are discarded and the slot mirrors cleared). An open order forces Stop mode back on so the
party returns to the table — crops were deducted pre-save and `HasPaid` prevents double
payment (`CreateTicketPreReserved` recreates the ticket as `ReadyToCook` with full-recipe
refund data).

Fridge contents and the Pre-Stock Stack Size slider persist separately (save v28, section 45:
`FridgeSlots` + `FridgePreStockStackSize`, restored into `FridgeInventoryService` on load), as
does the runner carry level (v29, section 46: `RunnerCarryLevel` → `GameStateService`).

**Not persisted**: live tickets, workers/shift state, serving-slot/plate entities, patron
state. All of that is transient and reconciled live after load.

## Fault-tolerance invariants (keep these true)

- A worker despawning for any reason must never strand work:
  `KitchenMonsterStateMachine.OnRemovedFromEntity` re-posts held orders, re-plates carried
  dishes (force-reserving a slot if needed), releases cook tickets and fetch jobs, and releases
  any carry-queue holds (`AbandonPreStockTrip` → `ReleaseCarried` — the crops never left
  storage, so releasing the hold is lossless).
- Ticket reservations are physical-at-creation, so crashes/despawns never lose reserved crops;
  the ticket-fetch walk is presentation only FOR THE RESERVED SHORTFALL. Pre-stock and top-up
  cargo is held-in-place (reservation ledger), so every exit path from a carrying state must
  either consume the holds at the fridge (`PreStockDeliver`/`DeliverCarriedTopUp`) or release
  them (`ReleaseCarried`) — a leaked hold permanently hides crops from every consumer.
- `PostTicket`, `ReleaseFetchJob`, `ReleaseBusJob`, `ForceReserveServingSlot` are idempotent /
  self-healing — a canceled or stale ticket is skipped everywhere.
- A bus job that leaves the queue must end with its plate entity destroyed. A claimed job whose
  plate is unreachable is cleared in place (`ClearUnreachablePlate`) rather than dropped: the
  alternative is a seat that blocks arriving patrons forever.
- Kitchen workers must not carry colliders or `TAG_MONSTER`.
- Meal-buff injection must stay RNG-free.

## Tests & analytics

- `KitchenServiceLoopTests` — headless end-to-end ticket logic (order → fetch → cook → plate →
  deliver → eat), cancellation/orphans, slot exhaustion, role mix, bus queue. No tiles or FSM.
  Note `Entity.Destroy()` no-ops without a scene, so headless tests can't fake a picked-up plate.
- `KitchenFlowPathTests` — parses the real TMX collision layer and asserts every walk leg
  (house exit → posts, station → serving approach, pickup → all 12 seat tables → sink,
  storage door ↔ fridge) is passable, and that every tile in the cook wander box is walkable.
  Update this when moving any kitchen/tavern tile.
- `DishPricingTests` — pricing formula and monotonicity.
- Analytics (see `AnalyticsSchema.md`): `dish_served` (price, tip, party, deluxe),
  `party_dine_skipped` (reason).

## Gotchas

- The kitchen FSM is live-only; don't look for a `VirtualGame` counterpart
  (`VirtualGameLogicLayer.md` lists it as intentionally uncovered).
- `TicketState` default is `AwaitingIngredients`; `CreateTicket` may immediately promote it.
- `GetServerZone` is recomputed per call from live worker order — don't cache zones.
- Serving-table tiles are *passable* floor on the collision map even though workers no longer
  stand on them (dish entities and path fallbacks rely on this).
- `ForceReserveServingSlot` intentionally double-books slot 0; downstream code scans tickets,
  not slots, so never "fix" pickups to key off slot occupancy alone.
- Coordinator caps live tickets at 16; `CreateTicket` returning null is normal backpressure.

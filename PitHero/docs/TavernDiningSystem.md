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
| Constants | `GameConfig.cs` ("Kitchen / Tavern Dining" block) |
| Save | `Services/SaveData.cs` + `SaveLoadService.cs` (v18 section 33) |
| Tests | `PitHero.Tests/KitchenServiceLoopTests.cs` (logic), `KitchenFlowPathTests.cs` (map routes), `DishPricingTests.cs` |

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

`KitchenTicket` is a bag of public fields; `TicketState`:

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
  each storage door, `RunnerCollectAtStorage` additionally tops the fridge up to par
  (`KitchenFridgeParPerCrop = 4` per recipe crop) with a withdraw+add against that one building;
  at the fridge, `CompleteFetch` flips `IngredientsFetched`. If storage vanishes mid-run the
  ticket still proceeds.
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
system), sorted by `CookingProficiency` descending. `FillRoleMix` cycles Cook → Server → Runner
skipping full roles, giving **cook1, server1, runner1, cook2, server2, runner2, cook3, runner3**
(`MaxWorkerPosts` = 8 = 3 cooks + 2 servers + 3 runners). Change a `GameConfig.MaxKitchen*`
constant and the order re-derives — but keep `AutoJobKitchenMaxWorkers` in sync (a test asserts
it). Workers whose role/slot disappears get `RequestReturnHome()` (finish current task,
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

`MercenaryManager` spawns unhired mercs on a 60–120s rolled interval (5s when the tavern is
empty) into 9 fixed seats; at the seat a `TavernPatronComponent` is added. **A patron never sits
down at a table that still has an un-bussed plate.** `GetAvailableTavernPosition` prefers a free
seat that is already cleared, and `TryReseatToClearedSeat` re-checks (plates appear and get
bussed during the walk in) — only when *every* free seat is dirty does the patron wait at the
tavern door (100,6), retrying the reseat every 0.25s until a plate is cleared. Waiting patrons
are unseated, so they add no ordering pressure while the backlog drains. When full, the
oldest patron in `FinishedEating` is walked off to free a seat — never one still waiting or
eating. `PatronState`: `WaitingToOrder → Ordered → FoodDelivered → Eating → FinishedEating`.
Patience: 10 min pre-order and post-order (expiry cancels the ticket and leaves immediately);
after eating they linger 5 min. On delivery the patron faces their table
(`TavernSeatConfig.GetFacing`). On finishing: pays `DishConfig.GetPrice`, 50% chance of a
5–15% tip (rounded up), logs `dish_served`. Hiring a patron mid-order calls
`CancelTicketForPatron` before removing the component. Patrons order a random dish from
`GetOrderableDishes` (= every dish whose recipe fridge+storage can cover).

## Party dining

Rides **Stop mode** — no new GOAP surface. Entry paths:

- **Manual**: player hits Stop; hero (and hired mercs) walk to the tavern and sit
  (`WalkToTavernForStopAction`, `HeroComponent.StoppedAdventure && SeatedInTavern`).
- **Morning auto-dine**: `SleepInBedAction` calls `BeginAutoDine()` after waking if the
  Food-tab "Eat at tavern" checkbox is on, the kitchen is open, and at least one member can
  dine; it force-stops, and `CheckAllDone()` auto-resumes when everyone finishes.

`PartyDiningService` implements `IPartyOrderSource`; the coordinator polls
`TryGetNextPartyOrder` (hero first — slot 0, then hired mercs 1/2). Dish choice: hero uses the
Food-tab favorite (`FavoriteDishId`); mercs use `DishConfig.GetFavoriteForJob(job)`, falling
back through two job-specific cheap dishes. A member whose candidates are all uncoverable or
unaffordable is **skipped for that seating, with no substitution** (`party_dine_skipped`
analytics with reason `already_ate` / `no_ingredients` / `no_gold`). One meal per member per
day (`HasEatenToday`, reset at the 6 AM daily tick along with `MealBuffService.ClearAll()`).

**Party pays at order time** (`OnPartyOrderTaken`), unlike patrons who pay after eating.
Eating runs on `GetEatSeconds` (5/7/10s by class); `FinishMember` applies the meal buff, logs
`dish_served` (party=true, tip=0), and notifies the coordinator. Resuming play mid-meal
cancels outstanding tickets (refunding gold only while `CropsRefundable`), but a `Delivered`
dish is fast-tracked — buffs still granted.

## Meal buffs

`MealBuffService` keeps one `(combatant, dish, deluxe)` record per member per day. Buffs are
**not persistent battle state** — `BattleEngine` clears battle state at battle start and then
calls `InjectBuffsAtBattleStart` for the hero and each merc (and for late-joining mercs),
adding each dish buff as `BattleBuff(type, magnitude, -1, "meal")` — turns = -1 means "until
battle end". Injection is pure list writes: **it consumes no battle RNG** (RNG call order is a
contract — see `VirtualGameLogicLayer.md`). Deluxe meals scale magnitude ×1.5 rounded up.
MagicUp feeds skill formulas via `ICombatant.GetSkillStats()`; HP/MP regen ticks at end of
round. Food never restores HP/MP directly — that's the inn's job.

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

## Persistence (save v18, section 33)

Persisted per party slot (`SavedDiningRecord`): `OrderedDishId`, `HasPaid`, `HasEatenToday`,
`MealDishId`, `MealDeluxe`; plus `FavoriteDishId` and `EatAtTavern`. v17 saves load with
defaults. On load, meal buffs are rebuilt via `MealBuffService.RestoreRecord` (no HP/MP
re-grant), and an open order forces Stop mode back on so the party returns to the table —
crops were deducted pre-save and `HasPaid` prevents double payment
(`CreateTicketPreReserved` recreates the ticket as `ReadyToCook` with full-recipe refund data).

**Not persisted**: live tickets, workers/shift state, fridge contents, serving-slot/plate
entities, patron state. All of that is transient and reconciled live after load.

## Fault-tolerance invariants (keep these true)

- A worker despawning for any reason must never strand work:
  `KitchenMonsterStateMachine.OnRemovedFromEntity` re-posts held orders, re-plates carried
  dishes (force-reserving a slot if needed), releases cook tickets and fetch jobs.
- Reservations are physical-at-creation, so crashes/despawns never lose crops; runner and
  server walks are presentation only.
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

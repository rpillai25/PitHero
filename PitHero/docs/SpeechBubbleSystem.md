# Speech Bubble Dialogue System

## Overview

Short dialogue lines rendered in a nine-patch bubble above a speaker's head (issue #377).
Bubbles are **screen-space** visuals (constant screen-px size at any camera zoom — 128 wide,
height sized to the mode's visible-line count — on `GameConfig.RenderLayerSpeechBubble` via
the scene's `ScreenSpaceRenderer`): each frame the tail-tip world anchor is converted with the
world camera's `WorldToScreenPoint`, so the bubble tracks its speaker but never scales with
zoom. In half-size window mode everything doubles (pre-scaled `Express2x` font, 256 px wide
bubble) so bubbles read at the same physical size as the normal window. Text reveals
typewriter-style via a pause-aware coroutine — lines beyond the bubble's capacity (normal 3,
half-window 2) scroll the block up one line at a time — then lingers ~2s and hides. Speakers:
the hero, mercenaries/tavern patrons, kitchen workers, and farm workers.

Three pieces:

| Piece | File | Role |
|---|---|---|
| `SpeechBubbleComponent` | `PitHero/ECS/Components/SpeechBubbleComponent.cs` | The view. `Say(localizedText)` / `Hide()`. One per speaker entity. |
| `SpeechBubbleDialogue` | `PitHero/SpeechBubbleDialogue.cs` | Static helper — one `Say*` method per game event; owns the random-variant tables and all headless guards. **All triggers call this, never the component directly.** |
| `Dialogue.txt` | `PitHero/Content/Localization/en-us/Dialogue.txt` | Localization table (`TextType.Dialogue`, keys in `DialogueTextKey`). |

## SpeechBubbleComponent

- Nine-patch `NinePatchSpeechBubble` (4/4/4/4 splits supplied **in code** — the atlas format has
  no split metadata) + `SpeechBubbleTail` whose top 2 rows overlap the bubble's bottom border.
  Renders on `GameConfig.RenderLayerSpeechBubble` (1000, screen space — back-most of the
  `ScreenSpaceRenderer` group, so UI windows and the pause dim draw over bubbles); text is
  `FontPathSpeechBubble` (Express — Skullboy and SkullboySmall were rejected in playtesting;
  never use `GetHudFontForCurrentMode()` here, the half-window Skullboy2x variant overflows the
  120 px-wide text area).
- **Half-size window mode:** `Say()` checks `WindowManager.IsHalfHeightMode()` and picks the
  bubble's scale (1 or 2) and font (`Express` / `FontPathSpeechBubble2x` = pre-scaled
  `Express2x`, lineHeight 18) for that bubble's lifetime; wrap width doubles with the font so
  line breaks match. A mode toggle mid-bubble keeps the Say-time scale until the next line.
  The component draws the `NinePatchSprite` patches individually with a per-patch scale
  (design layouts generated once) because `NinePatchDrawable.Draw` always renders borders at
  their 4 px source size — at 2x the corners/outline must double too. The half-window bubble
  is also shorter than normal's: both heights derive from their visible-line counts
  (`padding*2 + lines * lineHeight` design px: normal 3 lines = 35, half-window 2 lines = 26),
  each with its own pre-generated nine-patch layout.
- **Line scrolling (both modes):** when wrapped text exceeds the bubble's line capacity
  (`GameConfig.SpeechBubbleVisibleLinesNormal` = 3, `SpeechBubbleVisibleLinesHalfWindow` = 2),
  the visible text block scrolls up one line each time a line finishes its typewriter reveal —
  the oldest line drops and the next line types into the freed bottom row
  (`ScrollVisibleTextIfNeeded`, triggered only on `'\n'` appends). After the reveal, the
  bubble lingers showing the last N lines.
- **Culling:** `IsVisibleFromCamera` ignores the screen-space camera it is handed and instead
  checks the speaker's world position against the **scene camera's** bounds (expanded by one
  tile). A speaker fully outside the camera view hides its bubble; it reappears when the
  speaker scrolls back in.
- `Say()` **interrupts** any in-flight bubble (stops the coroutine, restarts). There is no queue.
- Word-wrap via `BitmapFont.WrapText` (never splits a word that fits a line); reveal and linger
  are pause-aware (`PauseService` spin). `OnRemovedFromEntity` stops the coroutine — a bubble is
  silently truncated if its entity is destroyed (e.g. a patron despawning off-screen).
- **Height anchoring:** by default the tail tip sits at `entity.Y + GameConfig.SpeechBubbleTailTipOffsetY`
  (−36, tuned for the 32px hero/mercenary paperdoll whose head top is at `Y − 32`). Worker
  monsters vary 28–90px and use a tile-anchor offset (sprite top = `Y + 16 − spriteHeight`), so
  for them set the public `AnchorRenderer` field to the body `SpriteRenderer` — the tail offset
  is then derived per-frame as `16 − spriteHeight − 4` (4px clearance above the head).
- Tunables live in the `GameConfig` "Speech bubbles" block: size, padding, tail overlap/offset,
  `SpeechBubbleCharsPerSecond` (20), `SpeechBubbleLingerSeconds` (2). Bubble duration ≈
  `chars / 20 + 2` seconds — relevant when a scripted sequence must wait for a line (see the
  crystal ceremony below).

### Where the component is attached

| Speaker | Attach site |
|---|---|
| Hero | `MainGameScene.CreateHeroEntity` (covers respawns) |
| Mercenaries / patrons | `MercenaryManager.SpawnMercenary` **and** the save-restore entity build in `MercenaryManager` (~line 1100) — there are **two** merc entity factories; a new component must go in both |
| Kitchen workers | `KitchenTaskCoordinator.SpawnWorker` (+ `AnchorRenderer = bodyAnimator`) |
| Farm workers | `FarmTaskCoordinator.SpawnWorker` (+ `AnchorRenderer = bodyAnimator`) |

**Trap:** Nez defers `OnAddedToEntity` to the next scene update, and `Say()` no-ops until the
component has loaded its drawables there. A `Say()` on the same frame the entity was created
silently does nothing — fire from the first AI action instead (this is why the hero's respawn
line lives in `WalkToStatueForCrystalAction`, not `RespawnHero()`).

## SpeechBubbleDialogue — option sets, gates, formatting

Multi-variant events are `OptionBag` tables — an `Option[]` plus a **shared
`ShuffleBag<int>` of option indices** (issue #385). Selection (`SelectKey`, public and
pure for tests) draws indices from the bag with **bounded draw-and-skip**: gate-ineligible
marbles are consumed and skipped (they return next cycle), bounded at `Count * 2` draws;
an eligibility pre-count returns early — without advancing the bag — when nothing can
match. Because the tables are static, one bag per event is automatically the shared
per-job pool: two cooks draw from the same `CookServedOptions` bag, so no line repeats
within a cycle no matter which worker speaks.

- `new Option(null)` is the **silent variant** — drawing it shows no bubble; it's a real
  marble, consumed like any other, so silence also cycles fairly.
- `Option.Gate` restricts eligibility (ineligible draws are skipped):
  `Gate.Merc` = at least one hired mercenary; `Gate.Tip` / `Gate.NoTip` = filtered by the
  `tipPaid` argument (only `SayPatronPaid` supplies it). Design-notation mapping: `""` → silent,
  `[G]` → `Gate.Merc`, `[T]`/`[!T]` → `Gate.Tip`/`Gate.NoTip`.
- Lines with a `[dish]` placeholder store `{0}` in Dialogue.txt; the `Say*` method resolves the
  localized dish name (`TextType.UI`, `DishConfig.GetDefinition(dish).NameKey`) and passes it as
  `formatArg` → `string.Format` at emit time. `TextService` itself has no placeholder support.
- Every public `Say*` funnels through `SaySingle`, which guards `Core.Instance == null` and
  `entity == null` — this is what keeps triggers headless-safe (patron order/payment paths run
  without `Core` in `KitchenServiceLoopTests`). `SayFromOptions` repeats the guard **before**
  any bag draw, so headless calls never advance a bag (keeps those tests deterministic).

### ⚠ RNG rule (do not break)

Variant picks use the class's **private `System.Random`** — NEVER `Nez.Random`. The
boss-defeated trigger fires inside `BattleEngine.Run` (via `IBattleEventSink.OnEnemyDefeated`),
and the global `Nez.Random` stream is the seeded battle-determinism contract shared with the
virtual sim. One `Nez.Random` call from a dialogue pick breaks `BattleEngineTests` and
virtual/live run parity. See the comment block at the top of `SpeechBubbleDialogue.cs`.

## Event catalog

### Hero

| Trigger | Emit site | Notes |
|---|---|---|
| Commits to a pit trip | `HeroStateMachine.EmitPitIntentBubbles` | One-shot per trip via `_pitBubbleEmitted` latch (mirrors `_innRestEmitted`); re-arms when a non-pit plan is adopted |
| Auto-purchase before pit jump | `AutoItemPurchaseService.TryPurchasePass` | Only when items were actually bought |
| Lands in the pit | `JumpIntoPitAction` after `InsidePit = true` | [G] variant + silent |
| Exits pit for night sleep / rest | `EmitPitIntentBubbles` | Discriminated at plan formation: `IsNighttime` → bedtime, else `HPCritical‖MPCritical` → rest set; **player-Stop exits never bubble**; `_pitExitBubbleEmitted` latch |
| Breakfast commit / skipped (no ingredients) | `PartyDiningService` | Skip line only on the `CanCoverRecipe` failure branch, not no-gold |
| Boss defeated | `LiveBattleAdapter.OnEnemyDefeated` | Live layer only; `VirtualBattleSink` untouched |
| Respawn after defeat | `WalkToStatueForCrystalAction.WalkToStatue` start | See the deferral trap above |
| Crystal ceremony prayer | `HeroPromotionService.ExecuteHeroCrystalCeremony` top | The pre-lightning dwell was extended to 4.0s (0.5 + 3.5) specifically to fit this line's reveal+linger — don't shorten one without the other |

### Tavern & workers

| Speaker | Trigger | Emit site | Notes |
|---|---|---|---|
| Patron | Places an order | `TavernPatronComponent.OnOrderTaken` | `{0}` dish name |
| Patron | Pays after eating | `TavernPatronComponent.FinishEating` | `tipPaid = tip > 0` gates [T]/[!T] |
| Server | Their patron leaves after eating | `TavernPatronComponent.Update` walk-off | Via `KitchenTicket.ServerEntity` (set in `TakeOrderAtTarget`, cached on the patron — the ticket is gone from the board by leave time). Angry patience-expiry leavers get no farewell |
| Cook | Plates a dish on serving | `KitchenMonsterStateMachine.CookWalkToServing_Tick` | Capture `_cookTicket.Dish` before it's nulled; the carry-to-sink branch never bubbles |
| Runner | Claims a fetch job | `KitchenMonsterStateMachine.RunnerIdle_Tick` | Not `RunnerWalkToStorage_Enter` — that re-enters per storage stop |
| Farmer | Arrives at crop storage carrying a harvest | `FarmingMonsterStateMachine.CarryHarvestToStorage_Tick` | Before `DepositAndFinish()` (which hides the body); can fire twice on a split delivery |
| Any worker | Shift end, heading home | `ReturnHome_Enter` (both FSMs) | Gated on `MonsterScheduleConfig.IsAsleep` so mid-shift role-change send-homes stay silent |
| Any worker | Emerges for a new shift/job | `EmergeFromHouse_Tick` (both FSMs) | At the transition out of the emerge state (worker is outside the house sprite) |

**Worker-FSM hook rule:** the job coordinators call `RequestReturnHome()` **every frame** while
a worker is unwanted — never emit from coordinator `Update()`. The FSM `_Enter` callbacks and
one-shot tick transitions fire exactly once and are the correct hook points.

### Innkeeper (issue #385)

The innkeeper (entity name `"innkeeper"`, tile 69,3) gets its `SpeechBubbleComponent` in
`MainGameScene.SpawnInnkeeper`.

| Trigger | Emit site | Notes |
|---|---|---|
| Party pays for an inn nap | `SleepInBedAction.SleepCoroutine` paid branch | "Have a good rest" — **paid stays only**; the free night-sleep branch stays silent |
| Hero crosses (63,6) heading to the pit after an inn stay | `HeroStateMachine.CheckInnFarewell` (GoTo move-complete edge) | 3-variant farewell bag. Armed by `HeroComponent.JustLeftInn` (set at sleep completion, transient/not saved); the first post-inn trip whose `PitIntent != EnteringPit` (e.g. the auto-dine tavern detour) disarms it, so tavern-origin pit trips that also pass (63,6) stay silent. Tile constant: `GameConfig.InnFarewellTileX/Y` |

### Second Chance merchant (issue #385)

The shop owner is a UI `Image`, not a world entity, so it uses its own view:
`PitHero/UI/MerchantSpeechBubble.cs` — a stage-space `Nez.UI.Element` reusing the same
`NinePatchSpeechBubble`/`SpeechBubbleTail` sprites and Express font. Differences from
`SpeechBubbleComponent`:

- **Persists after reveal**: no linger/auto-hide — the full text stays until the shop closes
  (`RemoveMerchantBubble()` runs in both `ToggleShopWindow`'s close branch and
  `ForceCloseWindow`).
- **Sized to the full wrapped text** (no scroll-and-drop), so every line stays visible.
- **Ticked with `Time.UnscaledDeltaTime`** from `SecondChanceShopUI.Update()` — the shop sets
  `PauseService.IsPaused` while open (a pure flag; `Time.DeltaTime` still ticks) and
  fast-forward scales `Time.TimeScale`; unscaled time keeps the reveal speed constant.
- Greeting text comes from `SpeechBubbleDialogue.GetSecondChanceGreeting()` (3-variant bag,
  re-drawn on every shop open; returns the localized string since there's no entity).
- Tail anchor = merchant sprite position + `GameConfig.SecondChanceMerchantBubbleAnchorX/Y`
  (plus the shop's stage-centering `xOffset`). The bubble is in `GetWindowBoundsElements()`
  so clicking it doesn't dismiss the shop, and it's `Touchable.Disabled`.

## Localization

Dialogue is its **own** table: `TextType.Dialogue`, `DialogueTextKey` consts,
`Content/Localization/en-us/Dialogue.txt` (CSV `Key,Text`, split on the **first** comma so
commas inside lines are fine). **Never add dialogue lines to UI.txt** — explicit project
decision. The csproj `Content/**/*.*` glob copies the file; no csproj edit needed for new lines.
Dish names are the exception: they already live in the UI table (`UITextKey.Dish*`) and are
injected via `formatArg`.

## Recipe: adding a new dialogue event

1. Add key consts to `DialogueTextKey.cs` + lines to `Dialogue.txt` (use `{0}` if parameterized).
2. In `SpeechBubbleDialogue`: add an `OptionBag` table (`new OptionBag(new Option[] { … })`;
   include `new Option(null)` if a silent draw is wanted, gates as needed) and a public
   `SayXxx(Entity, …)` that calls `SayFromOptions` (or `SaySingle` for a fixed line). The bag
   is static, so it's automatically the shared pool across all speakers of the event.
3. Call `SpeechBubbleDialogue.SayXxx(entity)` from the trigger. Pick a spot that fires **once**
   per logical event (FSM `_Enter`, a latched plan-formation block, a state transition) — never
   a per-frame branch. Check whether the code path also runs headless (tests / virtual layer);
   the helper's guards make the call safe, but the trigger itself must not need `Core`.
4. New speaker type? Attach `SpeechBubbleComponent` at the entity factory (all of them — see the
   mercenary two-factory note) and set `AnchorRenderer` if the sprite isn't the 32px paperdoll.
5. Verify: `dotnet build`, `dotnet test` (kitchen/patron tests exercise several triggers
   headless), and confirm no new `Nez.Random` usage in the diff.

## Related docs

- `TavernDiningSystem.md` — patron/ticket lifecycle the tavern bubbles hang off
- `CrystalCeremony.md` — respawn + ceremony timeline (bubble-driven dwell)
- `RenderingSystem.md` — render layer stack (`RenderLayerTop`)

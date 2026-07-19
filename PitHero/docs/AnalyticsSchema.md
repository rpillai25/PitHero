# Analytics Schema (Game Balancing)

Reference for the debug-only analytics layer added in issue #289. Read this before analyzing
analytics logs for balance work — it defines the file format, every event type, and the
interpretation caveats that are not obvious from the raw data.

## Where the data lives

- **Path:** `%LOCALAPPDATA%\PitHero\analytics\session_yyyyMMdd_HHmmss.jsonl`
- **Format:** JSONL — one JSON object per line, one line per event.
- **Rotation:** a new file per session. Returning to the title screen and starting/loading
  another game rotates to a new file within the same process.
- **Flush cadence:** events are buffered and written every 15 seconds
  (`GameConfig.AnalyticsFlushIntervalSeconds`), on 64 KB buffer overflow, and on clean exit.
  **After a crash, up to the last flush interval of events is missing** — treat abrupt file
  ends as truncation, not as "nothing happened".

## Availability

- Compiled into **Debug builds only**. Every `AnalyticsService` log method is
  `[Conditional("DEBUG")]`; Release builds contain no analytics code or call sites.
- Master switch: `GameConfig.AnalyticsEnabled` (const bool). `false` disables logging even
  in Debug builds.
- Implementation: `PitHero/Services/Analytics/` (`AnalyticsService`, `AnalyticsManager`,
  `JsonLineBuilder`).

## Envelope (every line)

| Field | Meaning |
|---|---|
| `t` | Wall-clock timestamp, ISO-8601 local time with UTC offset, ms precision. Lines are written in event order; `t` is monotonically non-decreasing within a file. |
| `gt` | In-game clock as `"HH:MM"` (24-hour). 1 real second = 1 in-game minute. Restored from save on load, so it can jump between adjacent lines during load. |
| `e` | Event type (see below). |

`session_start` is guaranteed to be the **first event** of each file.

## Event types

### Session
| `e` | Fields | Notes |
|---|---|---|
| `session_start` | `mode` (`"new_game"`/`"load"`), `gold` | `gold` is the player's funds at session start. |
| `session_end` | `goldGainedTotal`, `monstersDefeated`, `durationSec` | Written on clean exit only — absent after a crash. |

### Pit
| `e` | Fields | Notes |
|---|---|---|
| `pit_generated` | `pitLevel`, `pitTier`, `isBossFloor`, `monsterCount`, `chestCount` | Fires on every pit (re)generation, including the initial load-time generation. `isBossFloor` comes from `CaveBiomeConfig.IsBossFloor` — see caveat below. `pitTier` is 1 until the player completes pit 25, after which it increments and `pitLevel` resets to 1 for the new tier. |
| `chest_spawned` | `pitLevel`, `pitTier`, `x`, `y`, `chestLevel` (1–5), `item`, `kind`, `rarity`, optional `seedType` + `seedCount` | Contents decided at spawn time. `item` is `null` for seed chests. |
| `monster_spawned` | `pitLevel`, `pitTier`, `x`, `y`, `name`, `enemyId`, `level`, `maxHP`, `isBoss` | `level` is the enemy's own level (often a per-type preset — see caveats). |
| `trap_triggered` | `pitLevel`, `x`, `y`, `damage` | Hero stepped on a hidden trap. `damage` is the clamped value actually applied (hero's HP is never reduced below 1 out-of-battle). Formula: `5 + (tier-1)*25*2 + pitLevel * 2` (effective depth scaling) before clamping. |
| `trap_disarmed` | `pitLevel`, `x`, `y` | A party member with the TrapSense passive auto-disarmed a trap when fog was cleared over it. No damage dealt. |
| `orb_activated` | `fromPitLevel`, `toPitLevel`, `pitTier`, `heroLevel` | Wizard orb → next pit level. `pitTier` is the tier **at the moment of activation** (i.e. the tier about to be left when the orb advances into a new tier). Followed by a `party_snapshot` with `reason:"orb"`. |
| `pit_jump` | `pitLevel`, `heroLevel`, `heroHP`, `heroMaxHP` | Hero jumped into the pit. Followed by a `party_snapshot` with `reason:"pit_jump"`. |
| `party_snapshot` | `reason`, `members[]` | Each member: `name`, `type` (`"hero"`/`"merc"`), `job`, `level`, `str/agi/vit/mag` (total stats incl. gear/synergy), `maxHP`, `curHP`, `maxMP`, `skills[]` (skill ids), `gear{}` (slot→item name; keys `weapon`, `armor`, `hat`, `shield`, `acc1`, `acc2`; empty slots omitted). |

### Town
| `e` | Fields | Notes |
|---|---|---|
| `inn_sleep` | `cost`, `goldAfter` | `cost` is 0 for the free night-sleep branch. |
| `merc_arrived` | `name`, `job`, `level`, `str/agi/vit/mag`, `maxHP`, `maxMP`, `hireCost` | New tavern mercenary. Also fires for the immediate first spawn at scene start (that is a genuinely new mercenary, not save noise). |
| `merc_left` | `name`, `reason` | `reason`: `"tavern_left"` (rotation or tavern dismissal) or `"dismissed"` (hired party member dismissed). |

### Gear / gold
| `e` | Fields | Notes |
|---|---|---|
| `item_acquired` | `item`, `kind`, `rarity`, `pitLevel`, `chestLevel` | Item actually collected from a chest. Pair with `chest_spawned` (availability) — they answer different questions. |
| `gear_equipped` | `character`, `charType`, `slot`, `item`, `rarity`, `str/agi/vit/mag`, `maxHP`, `maxMP` | Stats are the character's **resulting totals after** the equip. Save-restore re-equips are deliberately not logged. |
| `gold_gained` | `amount`, `source`, `sessionTotal`, `currentGold` | `source`: `"battle"`, `"sell_item"`, `"sell_building"`, `"sell_crops"`, `"refund"`. `sessionTotal` resets each `session_start`. Spends are mostly not logged; infer from `currentGold` deltas (e.g. `inn_sleep.goldAfter`). Exceptions: `seed_purchased` and `building_created` (below) record their own spends. `source:"sell_crops"` lines pair with per-stack `crop_sold` detail lines. |

### Battle
| `e` | Fields | Notes |
|---|---|---|
| `attack` | `actor`, `actorType` (`"hero"`/`"merc"`/`"monster"`), `action`, `target`, `targetType`, `dmg`, `hpBefore`, `hpAfter`, `killed`, `missed` (only present when `true`) | `action` is `"physical"`, a skill id (e.g. `knight.heavy_strike`), `"counter"` (hero/merc retaliation after taking a hit — requires monk.counter passive), or `"<skillId>.dot"` (end-of-round damage-over-time tick from a skill such as `synergy.poison_arrow.dot`). Critical hits append `".crit"` (`"physical.crit"`, `"knight.heavy_strike.crit"`) — filter with `action ENDS WITH ".crit"`. `hpBefore`/`hpAfter` are the **target's** HP; `hpBefore − dmg = hpAfter` (floored at 0). AoE skills emit one line per target hit. Dodged attacks log `missed: true` with `dmg: 0` and `hpBefore == hpAfter` (skill misses log only against the primary target). Monsters only have physical attacks. DoT ticks never crit or miss; counters can miss but never crit. |
| `heal` | `actor`, `source` (skill id or item name), `target`, `amount`, `hpAfter` | Battle heals (skills and consumables). |
| `buff` | `actor`, `source` (skill id), `target`, `buffType`, `magnitude`, `durationTurns` | One row per granted buff actually applied (a multi-buff skill like `synergy.fade` logs one row per buff). Buffs skipped by the MaxStacks at-cap guard are not logged. `durationTurns: -1` = until battle end. `buffType` is the `BuffType` enum name (`DefenseUp`, `EvasionUp`, `MPRegen`, `Untargetable`, `AttackUp`, `MagicUp`, `AgilityUp`, `HPRegen`). Meal buffs (issue #319) are re-injected silently at every battle start and are NOT logged here — correlate with `dish_served` instead. |
| `monster_defeated` | `name`, `enemyId`, `level`, `isBoss`, `pitLevel`, `pitTier`, `xp`, `jp`, `gold` | One per kill regardless of killer; the matching `gold_gained` (`source:"battle"`) follows. |
| `char_killed` | full victim snapshot (same shape as a `party_snapshot` member) + `killer{name, enemyId, level, str/agi/vit/mag, maxHP}` | Hero or mercenary killed by a monster. |

### Farming (issue #312)

All farming work is performed by allied monsters; `monster` is the worker's display name and
`monsterType` its species name. `crop` is always the `CropType` enum name (`AppleTree`,
`Turnip`, …), **not** the localized display name. `x`/`y` are tile coordinates.

| `e` | Fields | Notes |
|---|---|---|
| `crop_planted` | `crop`, `x`, `y`, `monster`, `monsterType` | Seed consumed and crop entity spawned on a planned tile. |
| `crop_grown` | `crop`, `x`, `y` | Crop reached full size (CropGrown set) and is ready to harvest. No monster — growth is time/water driven. Re-fires on every regrowth cycle of repeat-harvest crops; not replayed on save load. |
| `crop_harvested` | `crop`, `x`, `y`, `qty`, `monster`, `monsterType` | `qty` is the harvest yield picked up. Fires when the worker commits to carrying (storage with room was found). Picking a dropped stack back up is **not** a harvest. |
| `crop_stored` | `crop`, `qty`, `monster`, `monsterType` | `qty` is the units actually accepted by the Crop Storage. Can appear **twice for one harvest** when a deposit is split across two storages (destination filled mid-walk). |
| `crop_dropped` | `crop`, `qty`, `x`, `y` | Carried crops dropped on the ground because no storage had room (or the target storage was sold mid-carry). `x`/`y` is the **final placement tile**, which may be relocated to the nearest drop-free tile if another drop occupied the intended one. Save-restore of existing drops is not logged. |
| `crop_destroyed` | `crop`, `x`, `y`, `monster`, `monsterType` | Worker removed a crop whose plan was swapped/removed (issue #305 destroy flow). |
| `crop_watered` | `crop`, `x`, `y`, `monster`, `monsterType`, `waterLeft` | `waterLeft` is the watering-can charges remaining **after** this pour. `crop` is `null` when the crop vanished before the pour landed. |
| `watering_can_filled` | `monster`, `monsterType`, `x`, `y` | Worker refilled its can to `GameConfig.WateringCanMaxCharges` at a pond tile. |
| `seed_purchased` | `crop`, `qty`, `goldSpent`, `source`, `currentGold` | `source`: `"manual"` (shop dialog) or `"auto"` (AutoSeedPurchaseService; one aggregate line per crop per pass). `currentGold` is funds **after** the spend. First instrumented gold spend. |
| `crop_sold` | `crop`, `qty`, `gold`, `source` | One line **per stack sold**; `source`: `"manual"` or `"auto"`. The gold itself arrives via the paired `gold_gained (source:"sell_crops")`. In bulk manual sells the per-stack `gold` sum can deviate from the paired `gold_gained.amount` if auto-sell emptied slots while the confirm dialog was open (pre-existing quirk; the detail lines reflect what was actually cleared). |
| `building_created` | `buildingType`, `x`, `y`, `cost` | `buildingType`: `"MonsterHouse"` or `"CropStorage"`. Player placements only — never fired on save restore. `cost` is the gold spent. |
| `building_moved` | `buildingType`, `fromX`, `fromY`, `toX`, `toY` | Player moved an existing building. |

### Tavern dining (issue #319)
| `e` | Fields | Notes |
|---|---|---|
| `dish_served` | `dish`, `price`, `tip`, `isParty`, `deluxe` | One line per finished meal. `dish` is the `DishType` enum name. Patron meals (`isParty:false`) pair with `gold_gained (source:"dish_sale")` and, when `tip > 0`, `gold_gained (source:"dish_tip")`. Party meals (`isParty:true`) never tip and paid at order time (spend not logged — infer from `currentGold` deltas). A patron who leaves after cooking started (patience expiry or hired mid-dining) still pays via `gold_gained (source:"dish_sale")` but logs no `dish_served`. |
| `party_dine_skipped` | `slot`, `member`, `dish`, `reason` | A party member was passed over during a tavern seating — no order, no charge. `slot`: 0 = hero, 1/2 = hired mercs. `dish` is the favorite that would have been ordered. `reason`: `no_ingredients` (fridge + storage can't cover the favorite's recipe — no substitution by design), `no_gold` (funds below the dish price), or `already_ate` (member already dined today; normal after breakfast if the party stops again). At most one line per member per seating. |

## Interpretation caveats

- **Names are text keys, not display names.** Monsters log localization keys
  (`Monster_Skeleton`), jobs log `IJob.NameKey` (`Job_Knight_Name`). Item names are the
  localized display names (English by default). Strip the prefixes for readability.
- **Damage is post-mitigation.** `dmg` is the applied damage (includes the debug
  `DEBUG_DAMAGE_MULT` multiplier in `AttackMonsterAction`, normally 1).
- **Enemy `level` tracks effective depth inside the Cave biome.** As of issue #291, enemy
  constructors honour `pitLevel` passed by `CaveBiomeConfig`; the per-type preset path
  (`EnemyLevelConfig.GetPresetLevel`) is still used for non-Cave content. Use `pitTier` and
  `pitLevel` together to recover effective depth: `effectiveDepth = (pitTier-1)*25 + pitLevel`.
  Trap damage scales with this same depth formula: `5 + effectiveDepth * 2` before clamping.
- **Load-time replay:** on `mode:"load"`, the pit is regenerated, so the first
  `pit_generated`/`chest_spawned`/`monster_spawned` burst right after `session_start`
  reflects the restored pit, and one `merc_arrived` fires for the initial tavern spawn.
- **Not instrumented:** gold spends other than `seed_purchased` and `building_created`
  (infer the rest via `goldAfter`/`currentGold`), out-of-battle healing GOAP actions,
  the virtual game layer (`PitHero.VirtualGame` has no combat simulation or farming).

## Typical analysis joins

- **Difficulty curve:** `attack` lines grouped by `pitLevel` (from surrounding
  `pit_generated`) — hero dmg vs monster `maxHP`, monster dmg vs party `maxHP`.
- **Progression vs performance:** `party_snapshot` (stats/gear per pit entry) joined to
  battle outcomes at the same pit level.
- **Gear access:** `chest_spawned` vs `item_acquired` vs `gear_equipped` per pit level.
- **Economy:** `gold_gained` by `source` over `t`, against `inn_sleep`/`merc_arrived`
  costs.

# Virtual-Layer Coverage Checklist

For each subsystem, verify the listed items. If any are missing **and the current feature exercises that subsystem**, implement them.

**Baseline status (post issue #296 / PR #297):** all six core systems below are mirrored.
The authoritative current-state reference is `PitHero/docs/VirtualGameLogicLayer.md` —
read it first; this checklist is for verifying NEW features against that baseline.

## 1. Pit Generation — ✅ mirrored

- [x] `VirtualPitGenerator` produces layouts with the live count formulas and `CaveBiomeConfig` pools
- [x] Tile grid, fog-of-war, treasure positions reproducible (local `Random(level)` per level)
- [x] Boss floors use the LIVE mapping (5=StoneGuardian, 10=EarthElemental, 15=AncientWyrm, 20=PitLord, 25=MoltenTitan)
- [ ] New biome boundaries / generation rules added to live `PitGenerator` are mirrored

## 2. Monsters — ✅ mirrored

- [x] Real `IEnemy` instances via `EnemyFactory.Create` with `CaveBiomeConfig.GetScaledEnemyLevelForPitLevel` (no `VirtualEnemy` stand-in exists or is needed)
- [x] Stored in `VirtualWorldState`'s `Dictionary<Point, IEnemy>`; adjacency triggers battles during wander + a monster-sweep phase
- [ ] New enemy types/archetypes spawn correctly in the virtual pool (usually automatic via `EnemyFactory` — verify the pool config)

## 3. Equipment & Loot — ✅ mirrored

- [x] Chest items are real `IItem`s using live treasure formulas + party-job loot weighting (`TreasureComponent.*Deterministic` overloads + `LootJobContext`)
- [x] Auto-equip mirrors `OpenChestAction` (hero → mercs, hand-me-down cascade via `GearAutoEquipService`)
- [ ] New gear kinds/rarity tiers reachable through the deterministic generation path (extend the `Get*ItemAtIndex` pools in `TreasureComponent` for BOTH live and deterministic paths)

## 4. Items (Consumables) — ✅ mirrored

- [x] `VirtualGameSimulation.Bag` (real `ItemBag`); battle consumable use through the decision engine; `HealingItemExhausted` tracked in `VirtualBattlePartyView`
- [ ] New consumable effects work through `Consumable.Consume` headlessly (no `Core.Services` dependencies — see the `GetTextService` guard pattern)

## 5. Mercenaries — ✅ mirrored (battle + economy; no per-tile state machine)

- [x] Real `Mercenary` objects fight in battles as `IBattleAlly`s; death prunes the roster; XP awarded
- [x] Hiring mirrors `MercenaryManager` (level distribution, job pool, cost) via `VirtualMercenaryLevelRoller`; inn rest restores them
- [x] Note: there is deliberately NO `VirtualMercenaryStateMachine` — mercs don't walk in the virtual layer; they are roster members present in every battle
- [ ] New mercenary battle behaviors flow through the shared `BattleEngine` (they will, unless Nez components are touched)

## 6. Battle — ✅ mirrored (shared engine)

- [x] The SAME `PitHero.Combat.BattleEngine` runs live (via `LiveBattleAdapter` coroutine) and virtual (via `HeadlessCoroutineRunner`) — damage formulas, elements, buffs, DoT, counter/deflect/crit are identical by construction
- [x] Death propagates: monsters removed from `VirtualWorldState`, hero death ends the level run (`Wiped`)
- [ ] **New battle mechanics MUST go into `BattleEngine` (or skills/`BattleReactionHelper`), never into `LiveBattleAdapter` display code** — engine changes are automatically mirrored; adapter changes are live-only
- [ ] ⚠ Respect the RNG-call-order contract documented in `VirtualGameLogicLayer.md` — do not add/remove/reorder `Nez.Random` calls in the engine without accepting a live-behavior change

## 7. GOAP Action Dual Execution

For each GOAP action exercised by the feature:

- [ ] Action implements both `Execute(HeroComponent)` and `Execute(IGoapContext)`
- [ ] `IGoapContext` exposes everything the action needs (no hidden live-layer dependencies)
- [ ] Any new per-level hero flag is reset in `VirtualGameSimulation.RunPitLevel` (or persistent runs will skip levels — see Known Gotchas in `VirtualGameLogicLayer.md`)

## Known Remaining Gaps (record additions here / in the doc's "Still Unmirrored" table)

Walking/travel time, seed-crop chest contents, shop purchases, night sleep,
`SecondChanceMerchantVault` recovery, analytics JSONL emission (metrics are aggregated
instead), tavern seat management.

## Priority Order When Filling Gaps

1. **Pit Generation** (everything else depends on a valid pit)
2. **Monsters** (combat needs enemies)
3. **Equipment & Items** (combat needs gear; items influence GOAP)
4. **Battle** (combat resolution — usually free via the shared engine)
5. **Mercenaries** (multi-actor coordination)
6. **GOAP dual execution** (last — actions wire everything together)

## Scope Discipline

**Don't** add virtual counterparts for subsystems the current feature doesn't touch — record them in the Delta Plan section of your output instead. Future features will pick them up.

## Verification Smoke Test

For each new virtual component added:

1. Write a focused unit test under `PitHero.Tests/` that exercises only the new component.
2. Run `dotnet test PitHero.Tests/` and confirm the new test passes.
3. Confirm existing virtual-layer tests still pass — the baseline is **12 known pre-existing failures**; anything above that is a regression.
4. For combat-adjacent changes, also run `--filter "TestCategory=BalanceTraversal"` and confirm the same-seed reproducibility test still passes.

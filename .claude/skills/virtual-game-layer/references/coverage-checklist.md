# Virtual-Layer Coverage Checklist

For each subsystem, verify the listed items. If any are missing **and the current feature exercises that subsystem**, implement them.

## 1. Pit Generation

- [ ] `VirtualPitGenerator` (or equivalent) produces a layout matching `PitGenerator`
- [ ] Tile grid size, fog-of-war placement, treasure positions all reproducible from a seed
- [ ] Boss-floor flag propagates correctly (every 5 levels)
- [ ] Biome boundary recognized at levels 25/50/75/100+

## 2. Monsters

- [ ] Virtual spawn pool mirrors the live spawn pool (sliding window of 10)
- [ ] Boss vs non-boss flag set correctly
- [ ] `VirtualEnemy` reproduces `IEnemy` stats from `BalanceConfig.MonsterArchetype`
- [ ] Elemental properties (`ElementalProperties`) propagated
- [ ] Encounter trigger (adjacency) translates to `AdjacentToMonster` GOAP flag in virtual context

## 3. Equipment

- [ ] Virtual chest drops produce the same `Gear` items as live (formula-driven)
- [ ] Rarity distribution matches live (Cave: 1-10 → tier 1; 11+ weighted)
- [ ] Stat bonuses applied to virtual hero on equip
- [ ] Elemental properties propagated through `ElementalProperties`

## 4. Items (Consumables)

- [ ] Virtual hero inventory supports add/remove
- [ ] Healing item use updates virtual HP/MP
- [ ] `HealingItemExhausted` GOAP flag tracked correctly

## 5. Mercenaries

- [ ] `VirtualMercenaryStateMachine` parity with `MercenaryStateMachine`
- [ ] All 5 mercenary actions executable on virtual layer
- [ ] Pit-status sync (`_expectedTargetInPit`) drives replanning in virtual context too

## 6. Battle

- [ ] `EnhancedAttackResolver` paths reachable in virtual context
- [ ] Damage formula identical (no shortcuts)
- [ ] Elemental multipliers computed via `BalanceConfig.GetElementalDamageMultiplier`
- [ ] Death detection (`HP == 0`) propagates to GOAP world state

## 7. GOAP Action Dual Execution

For each GOAP action exercised by the feature:

- [ ] Action implements both `Execute(HeroComponent)` and `Execute(IGoapContext)`
- [ ] `IGoapContext` exposes everything the action needs (no hidden live-layer dependencies)

## Priority Order When Filling Gaps

When the feature touches multiple subsystems, implement in this order to unblock dependent work:

1. **Pit Generation** (everything else depends on a valid pit)
2. **Monsters** (combat needs enemies)
3. **Equipment & Items** (combat needs gear; items influence GOAP)
4. **Battle** (combat resolution)
5. **Mercenaries** (multi-actor coordination)
6. **GOAP dual execution** (last — actions wire everything together)

## Scope Discipline

**Don't** add virtual counterparts for subsystems the current feature doesn't touch — record them in the Delta Plan section of your output instead. Future features will pick them up.

## Verification Smoke Test

For each new virtual component added:

1. Write a focused unit test under `PitHero.Tests/` that exercises only the new component.
2. Run `dotnet test PitHero.Tests/` and confirm the new test passes.
3. Confirm existing virtual-layer tests still pass (regression check).

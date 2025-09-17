# Pit Hero — RolePlayingFramework Overview

This document describes the RolePlayingFramework, a self-contained layer that powers single-hero progression, jobs, enemies, and a simple, original combat model. It is designed to be data-light, extensible, and independent of any external RPG systems.

## Goals
- Single active hero at a time
- Linear, predictable stat growth
- Job (vocation) augments on top of base stats
- Hero crystals as templates that can be combined
- Simple, readable combat math (hit + damage) for both sides
- Small, composable interfaces for testing and expansion

---

## Core Stats
Namespace: `RolePlayingFramework.Stats`

- `StatBlock`
  - Immutable Strength, Agility, Vitality, Magic
  - Helpers: `Add`, `Scale`
  - Used for heroes, jobs, and enemies as the shared currency of power

### Derived resources (in Hero)
- `MaxHP = 50 + 10 × Vitality`
- `MaxMP = 10 + 5 × Magic`

These are recomputed whenever level or stats change.

---

## Jobs (Vocations)
Namespace: `RolePlayingFramework.Jobs`

- `IJob`
  - `Name`: Display name
  - `BaseBonus`: Stats applied at level 1
  - `GrowthPerLevel`: Additional stats per level past level 1
  - `Abilities`: Array of job abilities
  - `GetJobContributionAtLevel(level)`: Base + growth up to the provided level

- `BaseJob`: Minimal base class that implements growth accumulation

- `JobAbility` (enum): A small, extendable set of passive hooks
  - Examples: `SwordMastery`, `HeavyArmorTraining`, `Guard`, `FistMastery`, `Counter`, `ChannelMagic`, `Heal`, `StaffMastery`

- Concrete jobs provided:
  - `Knight`: Frontliner; strong Strength/Vitality growth
  - `Mage`: High Magic; light Agility growth
  - `Monk`: Balanced Strength/Vitality with counters
  - `Priest`: Support caster; Magic/Vitality growth

The hero is “stuck” in a job for their life. Job stats layer on top of base stats.

---

## Hero Crystals
Namespace: `RolePlayingFramework.Heroes`

- `HeroCrystal`
  - Stores `Name`, `Job`, `Level`, and `BaseStats`
  - Represents a hero template that can be saved, traded, or purchased later

- `HeroCrystal.Combine(name, a, b)`
  - Produces a new crystal with a combined job and stats
  - Level is averaged; base stats are summed; abilities are unioned via the composite job

- `CompositeJob`
  - Wraps two `IJob` instances and sums their contributions, unions abilities

- `IHeroForge` / `HeroForge`
  - Queue a crystal for the next hero spawn (`QueueNext`)
  - Infuse the queued crystal into a new runtime hero (`InfuseNext`)
  - If none queued, creates a simple default hero

---

## Hero Runtime
Namespace: `RolePlayingFramework.Heroes`

- `Hero`
  - `Name`, `Job`, `Level`, `Experience`, `BaseStats`
  - `CurrentHP/MP`, `MaxHP/MP`
  - Growth:
    - `AddExperience(amount)`: linear XP curve, `required = level × 100`
    - On level-up: base stats +1 each; derived HP/MP recalculated
  - `GetTotalStats()`: Base + Job contribution at current level
  - `TakeDamage(amount)`, `Heal(amount)`, `SpendMP(amount)`

This keeps the growth model simple, predictable, and easy to tune.

---

## Enemies
Namespace: `RolePlayingFramework.Enemies`

- `IEnemy`
  - `Name`, `Level`, `Stats`
  - `AttackKind`: Physical or Magical
  - `MaxHP`, `CurrentHP`, `TakeDamage(amount)`

- `Slime`
  - Intro-level enemy with low Strength, moderate Vitality
  - HP scales off Vitality

This interface allows building more complex enemies (special attacks, resistances) incrementally.

---

## Combat
Namespace: `RolePlayingFramework.Combat`

- `DamageKind`
  - `Physical` | `Magical`

- `IAttackResolver`
  - Single entry point for resolving hit and damage

- `SimpleAttackResolver` (original, streamlined model)
  - To-hit chance:
    - `acc = clamp(5..95, 75 + (attackerLevel ? defenderLevel) + (AgilityDiff)/2)`
  - Damage:
    - Physical: `max(1, Strength×2 + Level×2 ? defender.Vitality)`
    - Magical: `max(1, Magic×3 + Level ? defender.Magic/2)`
    - ±10% variance for natural feel

- `AttackResult`: `{ Hit: bool, Damage: int }`

- `Battle` helpers
  - `HeroAttack(resolver, hero, enemy, kind)`
  - `EnemyAttack(resolver, enemy, hero)`

This model is intentionally simple and readable so you can evolve mechanics without hidden coupling.

---

## Typical Flow
1. Create a `HeroCrystal` with a chosen job and base stats
2. Queue it in `HeroForge`
3. Call `InfuseNext` to spawn the runtime `Hero`
4. Use `Battle` with `SimpleAttackResolver` to exchange attacks vs. enemies
5. On hero death, save their crystal and allow purchase/combination later

---

## Extensibility Ideas
- Equipment layer that augments `StatBlock` and grants more `JobAbility` hooks
- Resistances/weaknesses by damage kind or tags
- Status effects with a lightweight state machine
- EXP rewards and loot tables on `IEnemy`
- Ability/skill activations that consume MP and call the resolver with different formulas

---

## Design Rationale
- Linear formulas are transparent and fast to tune
- Jobs are additive and composable (via `CompositeJob`) without complex inheritance
- Small interfaces make testing and data-driven expansion straightforward

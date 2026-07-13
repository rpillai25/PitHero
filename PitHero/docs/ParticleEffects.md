# Particle Effects

How PitHero uses Nez's built-in particle system, and how to add a new effect. Introduced in issue #301 / PR #302.

## Architecture

- **`PitHero/Util/ParticleEffectManager.cs`** — a Nez `GlobalManager` (registered in `Game1.Initialize`, fetched via `Core.GetGlobalManager<ParticleEffectManager>()`). Holds an enum-keyed registry (`ParticleEffectType` → `ParticleEmitterConfig`) loaded once from `.pex` files in `PitHero/Content/Particles/`.
- **`.pex` files** are ParticleDesigner XML loaded at startup by `Core.Content.LoadParticleEmitterConfig` — raw file loading, no content pipeline. `PitHero.csproj` already copies `Content/**/*` to output; dropping a file in the folder is enough.
- **Battle visuals** flow through `IBattleEventSink`. Live effects are triggered only from `LiveBattleAdapter`; `VirtualBattleSink` and headless tests inherit no-ops from `BattleEventSinkBase`. Never touch particles from `BattleEngine` logic or the virtual layer, and never change RNG call order (it is a save/replay contract).
- **Cleanup is automatic**: spawn methods subscribe static handlers to `ParticleEmitter.OnAllParticlesExpired` that remove the component (or destroy the throwaway entity). Static handlers only — no captures (AOT safety).

## Adding a new effect (the recipe)

1. Copy an existing `.pex` in `PitHero/Content/Particles/` (keep the embedded `<texture data="...">` — it's a 64×64 soft radial glow) and edit the fields.
2. Add a `ParticleEffectType` enum member with a doc comment describing the look and pattern.
3. Add one load line in `ParticleEffectManager.Init`.
4. Call it at the trigger site:
   - `SpawnEffect(type, entity)` — attach to an actor (heals, buffs).
   - `SpawnEffectAtPosition(type, worldPos, scene)` — free-standing (dust, impacts, projectiles).
   - For battle skills, add a `case` in `LiveBattleAdapter.ShowSkillEffectOnMonsters` (see patterns below).

## Rules (violating these caused real bugs)

- **Never mutate a registry config at spawn time** — configs are shared instances; an edit bleeds into every later spawn. Per-spawn variation clones the config: see `SpawnEffect`'s `densityScale` (scales `MaxParticles` + `EmissionRate` via `CloneConfig`). New knobs should follow the same clone pattern.
- **Sizing: particle sizes are world pixels.** Tiles are 32px; the shared texture is a soft glow whose bright core is a fraction of the sprite. **Anything under ~5px is effectively invisible** — the first potion effect shipped at 3px and read as "no effect at all". Working references: heal burst 5–8px ×59 particles, potion rise 8–12px ×36, firestorm rain 12–16px ×56.
- **Fire-and-forget spawns need a finite `duration`** — a `-1` (infinite) config never fires `OnAllParticlesExpired` and leaks the emitter. Manually-driven effects (projectiles) still use a finite duration as a leak-safe cap in case the driving coroutine is torn down mid-effect.

## .pex quirks (what Nez actually reads)

- `sourcePosition` and `yCoordFlipped` are **ignored**. Particles spawn at the emitter entity's position ± `sourcePositionVariance`.
- Angles are **screen-space**: `angle=270` moves up, `90` down, `0` right. Negative `gravity y` accelerates upward.
- `emitterType 0` (Gravity) = ballistic motion from `speed`/`angle`/`gravity`. `emitterType 1` (Radial) = particles orbit the emitter from `maxRadius` toward `minRadius` at `rotatePerSecond` deg/s — this is how the fireball's "spinning ball" look works; `speed`/`gravity` are ignored.
- `EmissionRate` is not a `.pex` field — the loader computes `MaxParticles / ParticleLifespan`. If you ever build a `ParticleEmitterConfig` in code, you must set it yourself or nothing emits.
- Keep `particleLifespanVariance` **below** `particleLifeSpan`, or some particles roll a negative lifetime and die instantly (thins the effect).
- `blendFuncSource=770, blendFuncDestination=1` = additive glow (used by all current effects).
- All effects render at `GameConfig.RenderLayerLowest` (0) by default — above actors and fog, same as battle digits.

## Effect patterns in use

| Pattern | Example | How |
|---|---|---|
| Attached burst | Heal spell | `SpawnEffect(Heal, targetEntity)` fire-and-forget alongside the digit coroutine |
| Attached, intensity-scaled | Heal potion | `SpawnPotionHealEffect` → `densityScale` 1×/2×/3× by `HPRestoreAmount` (base / ≥500 / negative=full) |
| Projectile | Fire (`mage.fire`) | `SpawnEffectAtPosition` at caster, lerp entity to target (`SkillProjectileSpeed`, 0.15–0.6s clamp), `PauseEmission()` on impact; engine yields the coroutine so damage digits appear on landing |
| Area rain | Firestorm (`mage.firestorm`) | Average affected enemies' positions, spawn `AreaRainSpawnHeight` above, `WaitForSecondsRespectingPause(AreaRainImpactDelay)` before damage shows |

Skill visuals hook `ShowSkillEffectOnMonsters(caster, skill, primaryTarget, surroundingTargets)`, which `BattleEngine.ExecuteCombatantAttackSkill` yields **before** damage digits. Caster entity resolution works for hero and mercenaries via `GetEntityForCombatant`.

## Wiring map — battle sink is NOT enough

Heals and item use have paths that bypass `BattleEngine` entirely. If an effect should play "whenever X happens", check all of:

| Trigger | In battle | Out of battle |
|---|---|---|
| Skill heal | `LiveBattleAdapter.ShowHealOnAlly` | `UseHealingSkillAction.UseHealingSkillOnTarget` (GOAP) |
| Potion heal | `LiveBattleAdapter.ShowItemHealOnAlly` | `InventoryGrid.UseConsumable`, `ShortcutBar.UseConsumable`, `UseHealingItemAction.UseHealingItemFromBag` |
| Attack skill | `LiveBattleAdapter.ShowSkillEffectOnMonsters` | n/a (skills can't be cast outside battle) |

Out-of-battle potion/heal paths guard on "HP actually restored" so MP-only potions and full-HP sips show nothing.

## Caveats

- The embedded-texture decode path uses `System.Drawing` — **Windows-only** under .NET 7+. If cross-platform ever ships, delete the `data` attribute and place a `texture.png` next to the `.pex` (the loader falls back to `Texture2D.FromStream`).
- Multiple emitters on one entity are fine; each cleans up independently.
- Headless tests can construct `ParticleEffectManager` and exercise the null-safe pre-`Init` paths (`PitHero.Tests/ParticleEffectManagerTests.cs`), but `Init` needs a GraphicsDevice — don't call it in tests.

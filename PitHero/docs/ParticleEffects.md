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
   - Battle skills no longer use particles: they play SkillsStencils sprite animations (see "Battle skill visuals" below). Particles remain for potions and out-of-battle effects.

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
| ~~Attached burst~~ | ~~Heal spell~~ | Replaced (#458) by the `SkillHeal` sprite effect; `heal.pex` stays registered so old replay frames still resolve |
| Attached, intensity-scaled | Heal potion | `SpawnPotionHealEffect` → `densityScale` 1×/2×/3× by `HPRestoreAmount` (base / ≥500 / negative=full) |
| ~~Projectile~~ | ~~Fire (`mage.fire`)~~ | Replaced (#458) by `SkillFireball` + `SkillFireBlast`; `fireball.pex` stays registered for old replays |
| ~~Area rain~~ | ~~Firestorm (`mage.firestorm`)~~ | Replaced (#458) by `SkillMeteorShower`; `firestorm.pex` stays registered for old replays |
| Positioned burst, intensity-scaled | Gem dig (#452) | `SpawnEffectAtPosition(GemSparkle, tileCenter, scene, layer, densityScale)` from `MainGameScene.SpawnDigEffect`, density 0.5× / 1× / 1.6× / 2.4× / 3.5× by gem rarity (Common..Legendary); only gems sparkle, never junk or empty digs |
| Positioned burst | Fishing catch | `SpawnEffectAtPosition(Splash, bobber, scene, RenderLayerActorPropOverlay)` from `FishingTaskCoordinator.SpawnCatchEffect` when a fisher reels in a catch (skipped while `CosmeticUpdatesSuspended`); `splash.pex` is derived from `gem_sparkle.pex`: angle 270±40 with positive gravity so droplets jump up and fall back, blue-white, alpha blend (770/771), 0.12 s duration |

Skill visuals hook `ShowSkillEffectOnMonsters(caster, skill, primaryTarget, surroundingTargets)`, which `BattleEngine.ExecuteCombatantAttackSkill` yields **before** damage digits. Caster entity resolution works for hero and mercenaries via `GetEntityForCombatant`.

## Wiring map — battle sink is NOT enough

Heals and item use have paths that bypass `BattleEngine` entirely. If an effect should play "whenever X happens", check all of:

| Trigger | In battle | Out of battle |
|---|---|---|
| Skill heal | `LiveBattleAdapter.ShowSkillCast` (`SkillHeal` sprite, at the cast, healed or not) | `UseHealingSkillAction.UseHealingSkillOnTarget` (GOAP, same sprite) |
| Potion heal | `LiveBattleAdapter.ShowItemHealOnAlly` | `InventoryGrid.UseConsumable`, `ShortcutBar.UseConsumable`, `UseHealingItemAction.UseHealingItemFromBag` |
| Attack skill | `LiveBattleAdapter.ShowSkillEffectOnMonsters` (sprite effects) | n/a (skills can't be cast outside battle) |

Out-of-battle potion/heal paths guard on "HP actually restored" so MP-only potions and full-HP sips show nothing.

## Caveats

- The embedded-texture decode path uses `System.Drawing` — **Windows-only** under .NET 7+. If cross-platform ever ships, delete the `data` attribute and place a `texture.png` next to the `.pex` (the loader falls back to `Texture2D.FromStream`).
- Multiple emitters on one entity are fine; each cleans up independently.
- Headless tests can construct `ParticleEffectManager` and exercise the null-safe pre-`Init` paths (`PitHero.Tests/ParticleEffectManagerTests.cs`), but `Init` needs a GraphicsDevice — don't call it in tests.

## Physical attack visuals (issue #458)

Physical attacks are not particles: they are SkillsStencils.atlas animations drawn by `AttackEffectRenderer` (an `IFrameCapturable` that records the `RotSprite` frame op, so rotated effects replay).

- **Which visual:** `AttackVisuals.For(weapon)` in `RolePlayingFramework/Combat/AttackVisual.cs`. Sword → `AttackSlash`, knife → `AttackStab`, hammer → `AttackHammer`, bow → `AttackArrow` (flies), unarmed/knuckle → `AttackPunch` (fist + gust), staff → `AttackStaff` (a wooden swing smear), rod → `AttackRod` (see below). Spears and axes are tagged `WeaponSword`, so their factories set `Gear.WithAttackVisual(Thrust / AxeSwing)`; tier-scaled copies keep it.
- **Wiring:** `BattleEngine` yields `IBattleEventSink.ShowPhysicalAttack` after every roll and before the digits, and mercenaries yield `EndPhysicalAttack` after them. `LiveBattleAdapter` makes a melee mercenary (anything but a bow or a rod) jump half a tile beside the hero first and back afterwards. This is render-only: `HeroJumpComponent.SetAirborneOffset` moves the `MultiSpriteAnimator` offset, and the entity never moves. `CleanupBattleUI` resets a mercenary left displaced.
- **Rod:** `AttackEffects.PlayRod` (ranged, so mercenaries stay put). The attacker holds `AttackRod` up beside its head (hand at `AttackRodHandX/Y`), and a glow charges at the orb over `AttackRodRaiseDuration`. Then `AttackRodBolt` flies from the tip (`AttackRodTipX/Y` from the hand) to the target, and a detached `AttackRodBurst` plays there. The rod art is upright, not rotated; toward a target on the left, `AttackEffectRenderer.SetupMirrored` flips it left-right about its anchor. `AttackEffectRenderer.DrawOrigin` mirrors the origin live, and the replay renderer does the same for a RotSprite op with FlipX.
- **Timing is sim time from `GameConfig`** (`AttackEffectDuration`, arrow speed/clamps, jump speed/clamps) and holds while paused, never from the art. A missing animation logs once and draws nothing, but waits just as long, and a replay seek skips spawning but not the wait.
- **Art** is drawn pointing right. The renderer rotates it with `Atan2(target - attacker)` and flips it vertically when pointing left. It's centered `AttackEffectReach` px in front of the attacker and drawn at `RenderLayerLowest`.
- **Art source:** `C:\Users\rpill\Devroot\Art\PitHero\AttackEffects\<Tag>.aseprite`, one file per animation. Each is cropped to its frames' content, and an `anchor` slice's pivot marks where the game places the effect. Attack effects are anchored `AttackEffectReach` (16 px) in front of the attacker, with the target one tile (32 px) out. The arrow's anchor is its tip, which flies to the target. In the atlas, each `Attack*_N` sprite's **origin is that anchor** (pivot ÷ size), not 0.5,0.5. The art came from hun0fx Pixel Studio in DB32: Slash_normal (mirrored), Slash_Hit_thin (60%, rotated), Pierce_normal, spr_arrow_bolt, spr_slash_heavy (mirrored, gray gradient), spr_impact_ring (gray gradient), and spr_smoke_gust (every other frame) under spr_hit_streak.
- **Repacking SkillsStencils keeps origins only with `-originsMap`.** Export each file's frames to `Content/Textures/SkillsStencils/<Tag>/<Tag>_N.png`. Copy `Content/Atlases/SkillsStencils.atlas` into `Content/Textures` as `SkillsStencils.origins.atlas`, and from there run `SpriteAtlasPacker.exe -image:SkillsStencils.png -map:SkillsStencils.atlas -originsMap:SkillsStencils.origins.atlas -fps:8 SkillsStencils -pow2`. Then move both outputs to `Content/Atlases` and delete the copy. Without `-originsMap` every origin resets to the center and the effects shift, and `AttackVisualTests.SkillsStencilsAtlas_AttackEffects_ExistWithAnchorOrigins` fails. A new or changed anchor (pivot of the file's `anchor` slice) is written into the map's origin lines with `C:\Users\rpill\Devroot\Art\PitHero\work\attack_fx\set_origins.awk`. The effects live in SkillsStencils, not Actors, because Actors is for entities (heroes, mercenaries, monsters).

## Attack gestures

Characters move their bodies with their attacks. This is render-only: it moves the `MultiSpriteAnimator` offset, and the entity, its tile and its AI never move.
- **Lunge:** every melee physical attack and melee skill (everything but the spinning ones, Iaido and Heavy Strike) pushes the attacker `AttackLungeDistance` px toward the target and snaps it back over `AttackLungeDuration`, while the effect plays. `AttackGestureComponent.Lunge` runs by itself, holds while paused and adds no time. Bows and rods don't lunge.
- **Iaido Slash** (`MeleeGesture.SlideDash`): the caster slides back `IaidoSlideDistance`, holds, then dashes `IaidoDashDistance` past where it stood, facing the target the whole time. The slice plays from the dash while the sprite eases home (`AttackGestureComponent.Return`). The slide, hold and dash add to the skill's time.
- **Heavy Strike** (`MeleeGesture.LeapStrike`): a leap toward the target in the jump pose with its shadow, rising for `HeavyStrikeLeapPeak` of `HeavyStrikeLeapDuration` and slamming down faster. The effect starts at `HeavyStrikeEffectAt` of the leap, so it lands with the blow, and the sprite eases home while it finishes. A mercenary standing beside the hero leaps from there.
- **Roundhouse** (`MeleeGesture.HalfTurn`): the monk turns a half circle through the target while the kick plays: a quarter turn counter-clockwise of it, the target, a quarter turn clockwise (facing up: left, up, right), one every `RoundhouseFacingStep`, then faces the target. Dragon Kick keeps the Spin Slash four-way spin.
- A jump (`JumpArc`) cancels any running gesture first, because the jump owns the offset from then on.

### Lessons learned (gestures and battle body states)

- **Sim time is the contract.** A gesture that runs *inside* the effect (Lunge, HalfTurn, the Spin Slash spin: they hand-step the effect enumerator with `UnpausedDelta()` and yield its `Current`) takes exactly as long as the effect, so `SimulationVersion` stays. A gesture that adds a phase *before* the effect (Iaido's slide and hold, Heavy Strike's leap) lengthens the battle in sim time and needs a `SimulationVersion` bump.
- **Flips and offsets.** Nez's `SpriteRenderer.SetFlipXAndAdjustLocalOffset` negates `LocalOffset.X`, so a monster turned while displaced jumps to the mirrored spot. Turn monsters (`UpdateAnimationForDirection`) while their offset is at rest. `MultiSpriteAnimator.LocalOffset` is not negated by facing changes, so party gestures can turn freely.
- **Nez flips the texture, not the origin.** A mirrored effect with an off-centre anchor needs `AttackEffectRenderer.DrawOrigin`, live and in `RecordedFrameRenderer`.
- **Persistent looks tied to a buff** (Vanish's see-through, `GameConfig.VanishAlpha` on `MultiSpriteAnimator.Color`) read the buff, never a timer: buffs tick at the end of the round, so `RefreshSeeThrough` runs at each round and turn start, and `CleanupBattleUI` restores anything left. The tint is set even during replay seeks, so the state is right wherever playback lands.
- **Translucent characters replay slightly differently.** Live, a character is one composite render texture, so 50% alpha fades the whole figure evenly. The replay Composite op multiplies the tint into each layer, so overlapping layers (body under shirt, hair over head) show through each other a little. Per-layer ghosts (the afterimage trail) have the same look live. Pre-baked character frames ([rpillai25/PitHero#459](https://github.com/rpillai25/PitHero/issues/459)) would remove both.
- **Vanish alone does nothing.** `BattleEngine` ignores Untargetable when every living ally has it (anti-stall), so a solo hero's Vanish wastes the turn and the AI doesn't know that yet; the see-through still shows.
- **Afterimage trail:** Iaido's slide and dash and Heavy Strike's leap leave fading blue copies of the character behind them, in the style of Symphony of the Night (`AfterimageTrailComponent.Begin` / `End`). Nez's `SpriteTrail` only copies a single `SpriteRenderer`, and characters are layered composites. So the component snapshots every layer's sprite, offset and flip every `AfterimageSpacing` px the body moves, then draws the copies tinted `AfterimageColor` at `AfterimageStartAlpha`, fading over `AfterimageFadeDuration`. A -0.5 Y-sort offset keeps them just behind the character. Each copy is recorded as a Composite frame op, so replays show the trail. The component removes itself once the last copy fades.

## Monster attacks on the party

- **Leap in:** a monster attacking physically (`enemy.AttackKind == Physical`) that isn't already beside its target leaps in front of it first, in `LiveBattleAdapter.OnMonsterWindup`. "Beside" means within `MonsterAttackAdjacentRange` on both axes, diagonals included. It lands on the free tile next to the target nearest the monster, not under another party member or monster, then faces the target and plays its attack animation. `BattleEngine` yields `IBattleEventSink.EndMonsterAttack` at the end of the monster's turn, after digits, deaths and counters, and the monster leaps home and faces the hero again. The leap is render-only, through `EnemyAnimationComponent.LocalOffset`, so the entity, tile and AI never move. Timing uses the mercenary jump's speed and clamps, with `MonsterAttackLeapHeight`. Facing is applied at once with `UpdateAnimationForDirection`, because a monster's flip negates `LocalOffset.X`. A monster killed by a counter fades where it stands, and `CleanupBattleUI` puts a displaced monster back home.
- **Magic attackers** (`AttackKind == Magical`: Magma Ooze, Lava Drake, Ghost Miner) stay put. They turn to the target, play their attack animation, and cast a bolt of their element at it, timed like the Fire skill's fireball (blocking). On a hit, the burst plays on the target instead of `AttackMonsterHit`. `LiveBattleAdapter.MonsterMagicLook` picks the art by element: fire gets SkillFireball + SkillFireBlast, dark gets SkillShadowBolt + SkillSneakAttack, light gets AttackRodBolt + SkillHolyBurst, and anything else AttackRodBolt + AttackRodBurst. `EndMonsterAttack` turns the caster back to the hero.
- **Hit effect:** on a physical hit, `IBattleEventSink.ShowMonsterHitOnAlly` plays `AttackMonsterHit` (hun0fx Hit_02_1) on the target before the damage digits. It's detached and follows the target (`AttackEffects.PlayAnimationOn`), and adds no time. The art sprays right, so it's mirrored (`SetupMirrored`) when the monster is on the target's right, which makes the spray go away from the blow. Art source: `Art\PitHero\AttackEffects\AttackMonsterHit.aseprite`, built by `work\attack_fx\build_monsterhit.lua`.

## Battle skill visuals (issue #458)

Every class active skill plays a SkillsStencils sprite animation (`Skill<Name>`, a `SkillEffect` member, `AttackEffects.AnimationName`). They come from hun0fx and are cropped with anchor origins, the same way as the attack effects. All timings are `GameConfig.Skill*` sim times.

| Skill | Effect | How |
|---|---|---|
| `mage.fire` | SkillFireball, then SkillFireBlast | Projectile flies from the caster to the target (rotated), then the blast plays there; blocking |
| `mage.firestorm` | SkillMeteorShower | At the enemy group's center; blocking |
| `archer.power_shot` | AttackArrow, then SkillShotImpact | Arrow flight, then the impact streak at the target; blocking |
| `archer.volley` | AttackArrow ×N | One arrow per hit enemy, all at once (`AttackEffects.FlyVolley`); blocking |
| `knight.spin_slash` | SkillSpinSlash | Melee: a merc jumps beside the hero, then the arcs play centered on the knight while the knight turns through down, left, up and right every `SpinSlashFacingStep`, then faces the target again; blocking |
| `monk.roundhouse` | SkillRoundhouse | Melee: the merc jumps in, then the effect plays at the enemy group's center while the monk turns a half circle through the target (`MeleeGesture.HalfTurn`: a quarter turn counter-clockwise of the target, the target, a quarter turn clockwise, one every `RoundhouseFacingStep`; facing up that is left, up, right), then faces the target; blocking |
| `knight.heavy_strike`, `monk.flaming_fist`, `thief.sneak_attack` | SkillHeavyStrike, SkillFlamingFist, SkillSneakAttack | Melee: the merc jumps in, then the effect plays on the target; blocking |
| Skill heals (Priest Heal, synergy heals) | SkillHeal | Detached on the healed ally (`ShowSkillCast` at the cast, even at full HP; and out of battle in `UseHealingSkillAction`) |
| `priest.defup`, `thief.vanish` | SkillDefenseUp, SkillVanish | Detached, from `IBattleEventSink.ShowSkillCast` (called at the top of `ApplyHealingSkillEffectsAndDisplay`). Vanish also draws the thief's body (`MultiSpriteAnimator.Color`) at `GameConfig.VanishAlpha` while its Untargetable buff lasts (checked at each round and turn start, after the end-of-round buff tick; `CleanupBattleUI` restores any left at battle end); the tint is in the Composite op, so replays show it |
| `knight.provoke` | SkillProvoke | Detached on the knight, from `OnProvoke` |

- **Blocking** effects are yielded by `ShowSkillEffectOnMonsters` before the digits. They hold the battle for a fixed sim time and are skipped (but still waited) during seeks.
- **Detached** effects use `AttackEffectRenderer.AutoPlay`. The renderer advances itself on the fixed step, holds while paused and destroys itself when done. Nothing waits on it, so battle timing is unchanged.
- A mercenary that jumped for a melee skill jumps back in `IBattleEventSink.EndSkillEffect`, at the end of `BattleEngine.ExecuteCombatantAttackSkill`.
- Adding a skill visual takes an animation, a `SkillEffect` member, a name in `AttackEffects.SkillNames` and a case in `ShowSkillEffectOnMonsters` (most are one `Melee(...)`, `Magic(...)`, `ArrowRoutine` or `BoltRoutine` line). `AttackVisualTests` fails if a `SkillEffect` has no packed animation.

### Synergy skills and per-target hits

Every active synergy skill has a visual. Melee synergy skills make a mercenary jump in like the class melee skills.

| Skill | Effect |
|---|---|
| Holy Strike, Iaido Slash, Flash Strike, Shadow Slash, Sacred Blade, Spellblade | SkillHolyStrike / Iaido / FlashStrike / ShadowSlash / SacredBlade / Spellblade, played in front of the caster (`EffectPlace.Reach`) and rotated toward the target |
| Dragon Claw, Sacred Strike, Sneak Punch | SkillDragonClaw / SkillHolyBurst / SkillSneakAttack on the target |
| Dragon Kick | SkillRoundhouse on the target, with the Spin Slash facing spin |
| Energy Burst (area) | SkillKiBurst at the group center, plus a ki burst on each target |
| Meteor, Elemental Storm (area) | SkillMeteor / SkillElementalStorm at the group center, plus SkillBurn flames on each target |
| Shadow Bolt, Dragon Bolt | SkillShadowBolt / SkillFireball flight, then SkillSneakAttack / SkillFireBlast at the target |
| Blitz, Life Leech | SkillBlitz / SkillLifeLeech on the target |
| Poison Arrow, Piercing Arrow, Lightshot, Ki Arrow | Arrow flight, then SkillPoisonHit / SkillShotImpact / SkillHolyBurst / SkillKiBurst |
| Arrow Flurry, Elemental Volley | Volleys (two for Arrow Flurry) with SkillShotImpact / SkillFireBlast as each arrow lands |
| Smoke Bomb, Fade, Ki Cloak | SkillVanish / SkillFade / SkillKiCloak on the caster (detached, `ShowSkillCast`) |
| Aura Heal, Soul Ward, Purify | The heal swirl (`ShowSkillCast`, played even at full HP so a Purify that only cleanses still shows); Aura Heal and Soul Ward add the SkillDefenseUp shield (`ShowSkillCast`) |

**Per-target hits on area skills.** Area skills strike every hit monster with a detached hit at `SkillAreaHitDelayFraction` (0.4) of the area effect, so it looks like the area effect lands on each one. This is `AttackEffects.PlayAreaWithHits`, and the hits add no battle time.
- Firestorm, Meteor and Elemental Storm put SkillBurn flames on each monster (`SkillBurnDuration`).
- Spin Slash puts a FlashStrike slash on each monster, pointing out from the knight.
- Roundhouse puts a ShotImpact streak on each monster, and Energy Burst a KiBurst.
- Volley, Arrow Flurry and Elemental Volley play their impact on each monster as its arrow lands (`FlyVolley`'s impact argument).

**Effects on a character follow them.** The heal swirl (in and out of battle), the Defense Up shield, Vanish, Smoke Bomb, Fade, Ki Cloak and Provoke use `AttackEffects.PlaySkillOn(target, …)`. That sets `AttackEffectRenderer.Follow`, so the effect stays anchored to the character while it plays, even if they walk away mid-effect. Draw and capture read the target's position directly, so it never lags a step behind. If the target is destroyed, the effect stays where it last was.

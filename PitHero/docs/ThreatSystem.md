# Threat System

Battle-scoped "aggro" that decides which ally a monster attacks. Tank jobs (Knight) exist to
out-threat the Priests and Mages behind them; the player sees who currently has the monsters'
attention because that character's HUD panel is tinted red.

## Where it lives

| Piece | File |
|---|---|
| Ledger + formulas | `PitHero/Combat/ThreatTable.cs` |
| Per-battle owner | `PitHero/AI/BattleContext.cs` (`Threat` property, `IBattleContext.AddThreat`) |
| Engine hooks | `PitHero/Combat/BattleEngine.cs` (`AddThreat`, `ExecuteMonsterTurn`) |
| Event payload | `PitHero/Combat/BattleThreatEvent.cs` |
| Sink callbacks | `IBattleEventSink.OnThreatGenerated` / `OnThreatTargetChanged` |
| Live UI | `LiveBattleAdapter` → `HeroStateMachine.CurrentThreatTarget` → `MainGameScene.UpdateHeroHUD` → `GraphicalHUD.SetThreatTarget` |
| Tunables | `GameConfig.cs` — `// Threat System` block |
| Analytics | `threat` event (`AnalyticsSchema.md`) |
| Virtual metrics | `VirtualBattleMetrics.MonsterAttacks` / `AttacksOnThreatTarget` |

## Units

Raw damage grows ~100× between pit 1 and pit 100, so threat is measured in
**percent-of-max-HP units** and stays comparable at any depth:

| Source | Threat (before job multiplier) |
|---|---|
| Damage dealt | `min(100, dmg / enemy.MaxHP × 100) × ThreatPerDamagePercent (0.5)` per enemy hit — AoE sums across targets; crits count (post-crit pass) |
| Attack skill | `skill.ThreatValue` flat + the damage threat above |
| Healing / support skill | `skill.ThreatValue` flat + `restored / target.MaxHP × 100 × ThreatPerHealPercent (1.0)` (only HP actually restored — overheal is free) |
| Evasion (monster misses) | `ThreatEvasionBase (15) × evasionsThisBattle` → 15, 30, 45 … (any job; Thieves dodge most). Deflect is not an evasion |
| Basic attack | damage threat only |

**Job multiplier** on everything a combatant generates: any job whose `JobFlag` includes
`Knight` × `ThreatKnightMultiplier (2.0)`; all others × 1. A Knight/Mage composite hero keeps it.

### Per-skill flat values (`ISkill.ThreatValue`)

| Job | Skills | Flat |
|---|---|---|
| Knight | Spin Slash, Heavy Strike | 55 (×2 job) |
| Knight | Provoke (reaction, see below) | 100 (×2 job = 200) |
| Mage | Fire, Firestorm | 45 |
| Priest | Heal, Defense Up | 30 (+ heal %) |
| Archer | Power Shot, Volley | 20 |
| Monk | Roundhouse, Flaming Fist | 20 |
| Thief | Sneak Attack / Vanish | 10 / 0 |
| Synergy (unset = −1) | attack-type default `ThreatSkillAttackDefault` (25); Self/ally default `ThreatSkillSupportDefault` (30) |

Set `ThreatValue` in the skill ctor; leave it unset to take the target-type default. A skill's
`Execute` can add extra threat via `IBattleContext.AddThreat(actor, raw, source)`.

## Decay and target selection

- **Decay**: at the end of every round `threat × ThreatDecayPerRound (0.7)`; anything under
  `ThreatFloor (1)` snaps to 0. A Knight who stops using skills loses the lead in ~3 rounds.
- **Death** removes the ally from the ledger.
- **Threat target**: evaluated at **each monster turn**, after the Untargetable filter, over the
  living present allies only. Highest threat `> 0` wins; ties go to party order (hero first).
  Nobody with threat → no target → plain uniform random (pre-feature behaviour).
- **Pull**: the monster attacks the threat target with `ThreatTargetHitChance (0.7)`, otherwise
  a uniform random living ally (which may still be the target).

### RNG contract

The monster target pick consumes exactly **one** `Nez.Random.NextFloat()`, always, in the same
sequence position where the original `Random.Range(0, count)` was. The one float drives both the
pull check (`roll < ThreatTargetHitChance`) and the fallback index (`(int)(roll × count)`).
Ledger writes never touch RNG. Switching from `Range` to `NextFloat` shifted seeded balance
baselines **once** when the feature landed; determinism per seed is unchanged.

## Player feedback

- `OnThreatTargetChanged` fires only when the computed target differs from the last announced one
  (and once with `null` at battle end so the tint clears).
- Live: `HeroStateMachine.CurrentThreatTarget` is polled per frame by `MainGameScene`; the
  matching `GraphicalHUD` panel background is drawn with `GameConfig.ThreatTargetHudTint`
  (bars and text stay white). It is also cleared in `CleanupBattleUI` and on quit-to-title.
- Console: one `ConsoleThreatTarget` line per change ("X has the monsters' attention!").

## Tuning

All `GameConfig` threat fields are `static` (not `const`) so balance runs can override them.
`ThreatSystemTests` covers ledger math, the Knight pull share (≥60% of monster attacks with a
Mage hero + Knight merc), guaranteed pull at `ThreatTargetHitChance = 1`, event firing, and
per-seed determinism. Use `VirtualBattleMetrics.AttacksOnThreatTarget / MonsterAttacks` in
balance traversals to measure tank share at depth.

## Tank aggro control (AI)

Generation is action-based, but tanks also *act on* the ledger. `BattleTacticDecisionEngine.TryThreatRescue`
runs in every tactic branch (hero and merc) **after the heal check and before buffs/openers**, for
Knight-flagged combatants only — pure Knight or any composite containing Knight (Legend, Champion …),
hero or merc (`CompositeJob.JobFlag` ORs its halves):

- **Hold aggro** (`ThreatTankHoldAggro`, default on): whenever the tank is not the current threat
  target and no other tank holds it — including the empty ledger on round 1 — it fires its
  highest-`ThreatValue` affordable attack skill instead of buffs/openers/MP-efficient picks.
  Tanks want the monsters' attention by default; this is what makes a Legend actually out-threat
  its Mage merc (a playtest without it saw the Mage take 53% of hits, the Legend 33%).
- **Maintain** (`ThreatHoldMargin` 1.5, part of hold-aggro): even while the tank *is* the target,
  it re-fires the pull skill when its threat is below `highest rival × margin`. Without this a
  tank grabs aggro once, goes back to basic attacks, and a caster's steady ~50/turn plus ×0.7 decay
  overtakes it two rounds later (seen in a playtest: Knight 5 hits vs Mage 8 after learning Spin Slash).
- **Rescue** (`ThreatRescueHpPercent` 0.6): the same action, triggered by a non-tank target at or
  below that HP fraction. Still fires when hold-aggro is switched off.
- **Turn-start re-evaluation** (hero only): the hero's action is queued at round start, but the
  ledger moves before their turn. `BattleEngine.ReEvaluateHeroQueuedAction` now upgrades a queued
  *basic attack* to the hold-aggro/rescue skill pick for Knight-flagged heroes (mercs already
  decide on their own turn). Non-tank heroes are unaffected.
- Action: highest-`ThreatValue` affordable attack skill (AoE wins ties when several monsters
  live), targeted via the usual elemental preference. Nothing affordable → normal path.
- Healing still wins; neither tier overrides a needed heal. Consumes no RNG.
- The pick is job-agnostic: a Knight/Mage composite pulls with whichever skill carries the most
  threat, which is why Knight skills sit at 55 — above Mage's 45 — so the Knight kit stays the
  natural pull on composites.

## Provoke — the out-of-turn rescue

Playtests showed the tank AI above works once the Knight has acted, but ~30% of monster swings
land **before the slow Knight's first turn** (empty ledger, or the Mage's Fire already on top).
No turn-based skill can reach those. Provoke (`knight.provoke`, `ProvokeSkill`) is the answer:
a Knight active skill that the **engine fires as a reaction, outside turn order**.

- **Skill**: Active, `Self`, 5 MP, 50 JP (replaced the Light Armor passive — a Knight in a robe
  is a downgrade — and reuses its icon for now). `ThreatValue` 100 (→ 200 after the Knight ×2),
  above any single caster action. `ISkill.ReactionOnly = true`: `BattleTacticDecisionEngine`
  never picks it as a turn action.
- **Trigger** (`BattleEngine.TryProvokeReaction`, right after every monster attack): the victim is
  a living **non-tank** ally now at or below `ThreatRescueHpPercent` (60%). The first tank able to
  cast — hero first, then party order — must be alive, present, Knight-flagged, have Provoke
  learned, not have provoked this battle, and afford the MP.
- **Effect** (`ExecuteProvoke`): spend MP, mark the once-per-battle use, add the flat threat, set
  `_forcedThreatTarget` so the **next monster attack is a guaranteed pull** onto the tank (the
  target roll is still consumed → RNG contract intact), and announce the new threat target
  immediately so the HUD turns red before the monster swings. The Knight keeps their real turn.
- **Player cast**: queued from the shortcut bar it fires on the caster's turn with nobody
  "protected"; a second cast in the same battle is skipped without cost.
- **Feedback**: speech bubble over the tank ("Over here!" / "Focus on me!" via
  `SpeechBubbleDialogue.SayProvoke`, `Dialogue.txt`), floating "Provoke!" label, console
  `ConsoleBattleProvoke` line, `provoke` analytics row (plus the `threat` row with
  `source: "knight.provoke"`), `VirtualBattleMetrics.Provokes`.
- **Save compatibility**: `SkillIdMigration.Resolve` maps a learned `knight.light_armor` to
  `knight.provoke` when a crystal reloads, so no JP slot is lost.

## Not yet

- No debuff-style taunt (forcing *all* monsters for a whole round); Provoke covers one swing.

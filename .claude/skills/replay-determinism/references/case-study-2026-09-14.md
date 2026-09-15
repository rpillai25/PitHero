# Case study: the 69-minute divergence (2026-09-14)

One afternoon's recording, three root causes, all the same rule broken in different places:
**presentation state was feeding the simulation.** Read this when a divergence looks inexplicable;
the method matters more than the specific bugs.

## Symptom

`replay_divergence.log`: tick 248700, `rng` + `hero` differ, `party` + `world` match, hero mid-battle
on the pit 20 boss floor. Reproduced byte-identically on a fresh launch, so not a process leak and not
run-to-run randomness. Code sweeps for the usual suspects (wall clock, `Input`, stray `Nez.Random`,
unstable sorts, static state, cosmetic-skip, quiet-log) found nothing.

## Method that worked

1. **Snapshot the last in-sync sample.** Everything the hash covers is, at that sample, the recorded
   state. Printing the full hero/party/enemy description at every matching sample and again at the
   divergence gave a one-second window with named actors (`ReplayStateDescriber`, now permanent).
2. **Trace the replay's own decisions.** Playback suppresses analytics, so the replay had no battle
   log. A 48-entry ring buffer of target picks (with roll and candidate list), Provoke, attacks, buffs
   and threat (`ReplayBattleTrace`, now permanent) showed the boss picking the hero with roll 0.676.
3. **Get the live side from analytics.** `analytics\session_*.jsonl` of the live run, indexed by wall
   time (session start + tick/60 s), showed the same fight action for action — except the boss swing
   the trace showed had **no live row**. Only a deflect writes no row.
4. **Ask why the deflect chance differed.** Deflect comes only from synergy passives. The live
   `party_snapshot` listed a synergy-granted skill; the replay hero had `deflect=0.18`, live had more.
5. **Find who computes it.** grep showed synergy detection existed only in `InventoryGrid` (UI),
   fed by every grid cell including mercenary equipment slots that only fill when the Party window
   opens. Live had opened the window; the replay never does.

## The three bugs and their fixes

| Bug | Why live and replay differed | Fix |
|---|---|---|
| Synergy passives (deflect, defense, stat bonuses, granted skills) computed by the Party grid on window refresh | The replay opens no windows; live passives depended on window history | `HeroSynergyResolver` recomputes in the sim every tick a bag slot changes; the grid only mirrors `hero.ActiveSynergyGroups` |
| Pattern matching covered equipment cells, so mercenary gear shown in the window completed patterns | Design says bag only; the old grid matched every cell | `PartyGridLayout.FillSynergyGrid` fills bag rows only |
| A `SwapSlots`/`BuyVaultItem` on the shop grid no-oped in playback | The UI overlay is built before the hero entity exists; the shop grid only binds to the hero when the shop opens, and live it had been opened | `InventoryGrid.SyncFromSimulation()` rebinds to the current hero and refreshes before a handler applies; `GetGrid` calls it |

The second recording (step-4 test) exposed the third bug at tick 1920 with `hero` only and RNG equal:
the replay's post-swap hash equalled the recorded pre-swap hash, which is the fingerprint of a
command that did nothing.

## What to carry forward

- If a value the sim reads is produced by a UI refresh, it is already a determinism bug even if no
  replay has caught it yet. Move the computation into the sim on the change itself.
- Any UI object used to execute a command must be treated as unconnected and stale until resynced.
- A divergence report is only as good as the state it prints. Keep the snapshots and the trace on;
  they cost nothing outside playback.
- Cross-check the trace against the live analytics before theorising. Two runs of the replay cost a
  minute; a wrong theory cost an hour.

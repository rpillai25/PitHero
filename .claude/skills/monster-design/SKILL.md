---
name: monster-design
description: "**DOMAIN SKILL** — Designing balanced monsters for PitHero pit levels and biomes. USE FOR: creating new enemies, bosses, mini-bosses, big bosses, creature archetypes; balancing monster stats by pit level/biome; defining elemental affinities, resistances, and weaknesses; populating spawn pools; deciding monster sprite size (small/medium/large); updating MONSTER_LIBRARY.md. DO NOT USE FOR: implementing monster C# code (that is implementer work — see `principal-game-engineer.md` and the Monster Creation Pattern there), monster art/animations, non-combat NPCs, mercenaries."
---

# Monster Design — PitHero

You are designing monsters — not implementing them. Output is design data in `MONSTER_LIBRARY.md` at the repo root, which the implementer reads when coding the monster classes.

## Core Constraints

- **No source code.** Design only.
- **MONSTER_LIBRARY.md** is the deliverable. Create it if missing.
- A feature planning artifact under `features/` is also fine to create/update if you're tracking a multi-monster batch.

## Biome Structure (every 25 levels)

| Pit Levels | Biome |
|---|---|
| 1–25 | Cave |
| 26–50 | Forest |
| 51–75 | Castle |
| 76–100+ | Underworld |

## Boss Cadence (every 5 levels)

- Every multiple of 5 → **small boss**
- Every 25th level → **big boss** (end-of-biome encounter)
- After every block of 5 levels, **5 new monsters** are added to the spawn pool

## Spawn Pool

The spawn pool is a **sliding window of 10 monsters**: the 5 from the previous block + the 5 for the current block. When designing a new block, design 5 monsters that work with the previous 5 still in the pool.

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Stat formulas, archetype tables, HP/MP/XP scaling, level-specific examples | `references/balance-formulas.md` and the repo's `MONSTER_BALANCE_GUIDE.md` |
| Biome-by-biome design considerations (Cave themes, Forest themes, etc.), boss cadence specifics | `references/biome-progression.md` |
| Format of MONSTER_LIBRARY.md entries — fields, sprite sizes, common header structure | `references/library-format.md` |

## Element Assignment Guidelines

| Archetype | Recommended Elements |
|---|---|
| Balanced | Earth, Water, Neutral |
| Tank | Earth (defensive), Fire (aggressive) |
| FastFragile | Wind (speed), Dark (evasive) |
| MagicUser | Fire (offensive), Water (support), Light (holy), Dark (shadow) |

**Standard resistance pattern:** 25–30% resistance to own element, 10–15% weakness to opposing element.

Not all monsters need elemental attributes — keep early-Cave monsters mostly neutral, introduce elements as the player progresses.

## Design Approach

1. Read `MONSTER_BALANCE_GUIDE.md` for the formulas and archetype definitions.
2. Identify the **biome** and **pit-level range** being designed.
3. Pick **5 monsters** for the new block (this is the spawn-pool expansion size). At every multiple of 5, one of those should be a **small boss**. At every 25th level it's a **big boss**.
4. For each monster, decide: name, archetype (Balanced/Tank/FastFragile/MagicUser), element (or Neutral), resistance/weakness, sprite size, visual description.
5. Verify the new block plays well alongside the previous 5 monsters (sliding-pool overlap).
6. Append all 5 monsters to `MONSTER_LIBRARY.md` using the common format.

## Output

Update or create `MONSTER_LIBRARY.md` at the repo root. When the file is first created, define the common entry format at the top so future additions stay consistent. Each entry must include:

- Name
- Biome(s)
- Pit level (or pit-level range)
- Archetype
- Element + resistances/weaknesses
- Stats (per `MONSTER_BALANCE_GUIDE.md` formulas)
- Sprite size — small (32×32), medium (48×48), or large (64×64)
- Visual description (used by the implementer for placeholder art)
- Boss flag (none / small boss / big boss)

After designing, summarize the monsters added in this iteration in a short list.

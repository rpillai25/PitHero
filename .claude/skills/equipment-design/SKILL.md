---
name: equipment-design
description: "**DOMAIN SKILL** — Designing balanced equipment (weapons, armor, helms, shields, accessories) for PitHero pit levels and biomes. USE FOR: creating new gear, weapons, armor, helms, shields, accessories, item rarity tiers (Normal/Uncommon/Rare/Epic/Legendary); balancing attack/defense/stat bonuses by pit level and biome; assigning elemental properties; populating treasure-chest spawn pools; updating EQUIPMENT_LIBRARY.md. DO NOT USE FOR: implementing equipment C# code (see Equipment Creation Pattern in `principal-game-engineer.md`), item art, consumable items, skills/abilities."
---

# Equipment Design — PitHero

You are designing equipment — not implementing it. Output is design data in `EQUIPMENT_LIBRARY.md` at the repo root, which the implementer reads when coding the equipment factories.

## Core Constraints

- **No source code.** Design only.
- **EQUIPMENT_LIBRARY.md** is the deliverable. Create it if missing.
- A feature planning artifact under `features/` is also fine to create/update if you're tracking a multi-item batch.

## Equipment Categories

- **Weapons** — primary offensive gear (swords, axes, staves, bows, daggers, …)
- **Armor** — body protection (defense + VIT bonuses common)
- **Helms** — head protection (smaller defense + sometimes MAG bonuses)
- **Shields** — defense + occasionally resistance bonuses
- **Accessories** — flat stat bonuses, sometimes elemental affinity, no defense

## Biome Structure (every 25 levels)

| Pit Levels | Biome |
|---|---|
| 1–25 | Cave |
| 26–50 | Forest |
| 51–75 | Castle |
| 76–100+ | Underworld |

## Boss / Spawn Pool Cadence

- Every 5 levels, the **equipment spawn pool** expands by 5 new pieces (one of each type — weapon, armor, helm, shield, accessory).
- The pool is a sliding window of 10 sets per equipment type: the 5 from the previous block + the 5 for the current block.
- Boss floors (every 5th level) and biome-end floors (every 25th level) typically carry higher-rarity drops.

## Rarity Tiers

| Rarity | Power Level | Spawn Frequency |
|---|---|---|
| Normal | Baseline for pit level | Most common |
| Uncommon | Slight bonus | Common |
| Rare | Strong advantage (not OP for level) | Uncommon |
| Epic | Overpowered for level (low drop chance) | Rare |
| Legendary | Very overpowered (very rare) | Very rare |

**Balance rule:** Normal and Uncommon should NOT make the hero overpowered for their pit level. Rare gives a real advantage. Epic and Legendary may exceed the level because their drop chance is low.

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Attack/defense/stat-bonus formulas, rarity multipliers, scaling by pit level | `references/balance-formulas.md` and the repo's `EQUIPMENT_BALANCE_GUIDE.md` |
| Biome-by-biome design themes, elemental tilt, boss-tier drops | `references/biome-progression.md` |
| Format of EQUIPMENT_LIBRARY.md entries — fields, header structure, examples | `references/library-format.md` |

## Element Assignment Guidelines

| Equipment Type | Element Pattern |
|---|---|
| Weapons | Usually pure element (no resistances) |
| Armor / Shields / Helms | Can have resistances to own element |
| Accessories | Typically pure element (no resistances) |
| Neutral equipment | No resistances or weaknesses |

**Standard resistance pattern:** 25–30% resistance to own element, 10–15% weakness to opposing element.

**Stat-bonus scaling:**
- HP bonus = `stat * 5` (for vitality-focused items)
- MP bonus = `stat * 3` (for magic-focused items)

## Design Approach

1. Read `EQUIPMENT_BALANCE_GUIDE.md` for formulas and rarity multipliers.
2. Identify the **biome** and **pit-level range** being designed.
3. Design **one piece of each type** (weapon, armor, helm, shield, accessory) for the new 5-level block — this expands the spawn pool by 5.
4. For each piece, decide: name, rarity, pit level it spawns at, stat bonus, attack/defense value, element, visual description.
5. Verify the hero's progression — Normal/Uncommon shouldn't make a same-level hero feel overpowered; Rare may give a real edge; Epic/Legendary are deliberately exceptional.
6. Append entries to `EQUIPMENT_LIBRARY.md` using the common format.

## Output

Update or create `EQUIPMENT_LIBRARY.md` at the repo root. When first created, define the common entry format at the top. Each entry must include:

- Name
- Type (weapon/armor/helm/shield/accessory)
- Biome(s) and pit-level range
- Rarity
- Attack or defense value (per formula)
- Stat bonuses (STR/AGI/VIT/MAG)
- Element and resistances/weaknesses (if any)
- Visual description (for placeholder art)
- Notes (flavor, special-case mechanics, unique synergy potential)

After designing, summarize what was added.

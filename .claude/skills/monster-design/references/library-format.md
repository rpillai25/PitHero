# PitHero/docs/MonsterLibrary.md Format

When `PitHero/docs/MonsterLibrary.md` is first created, write the format header at the top so future additions stay consistent.

## Header Template (top of file)

````markdown
# MonsterLibrary.md

Catalog of designed monsters for PitHero. The implementer reads this file when coding monster classes — see Principal Game Engineer for the C# Monster Creation Pattern.

## Entry Format

Each monster entry uses the following structure:

### <Name>

- **Biome:** Cave / Forest / Castle / Underworld
- **Pit level:** N (or range)
- **Boss role:** none / small boss / big boss
- **Archetype:** Balanced / Tank / FastFragile / MagicUser
- **Element:** Neutral / Fire / Water / Earth / Wind / Light / Dark
- **Resistances:** `{ Element: ±value, ... }`
- **Sprite size:** small 32×32 / medium 48×48 / large 64×64
- **Visual description:** 1–2 sentences for placeholder art
- **Stats (at L=<level>):** HP, STR, AGI, VIT, MAG, XP yield
- **Notes:** Any unique mechanics, lore, spawn-pool rationale

---
````

## Example Entry

```markdown
### Cave Slime

- **Biome:** Cave
- **Pit level:** 1
- **Boss role:** none
- **Archetype:** Balanced
- **Element:** Water
- **Resistances:** `{ Water: +0.30, Fire: -0.15 }`
- **Sprite size:** small 32×32
- **Visual description:** A small, translucent blue blob with two beady eyes and a slowly pulsing core.
- **Stats (at L=1):**
  - HP: 15
  - STR: 1, AGI: 1, VIT: 2, MAG: 1
  - XP yield: 18
- **Notes:** Standard intro enemy. Slow, low damage, low XP — establishes baseline combat.

---
```

## Rules

- One blank line between entries; `---` separator after every entry for readability.
- Group entries by biome → pit-level block.
- Don't duplicate names. If a monster reappears at higher levels with scaled stats, call out the level explicitly.
- Boss entries include the `+2 level shift` already applied in the stat block.
- Stats listed should match the formulas in `references/balance-formulas.md` so the implementer can compute archetype multipliers directly.

## After Adding Entries

Summarize the additions in your handoff message:

```
Added 5 monsters for Cave pit levels 11–15:
- Cave Brute (small boss, level 15)
- Cave Bat
- Cave Kobold
- Cave Drake (Fire)
- Cave Sentinel (Tank)
```

The implementer then uses each entry plus `PitHero/docs/MonsterBalanceGuide.md` to write the C# class.

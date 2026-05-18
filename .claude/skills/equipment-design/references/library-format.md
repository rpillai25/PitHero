# EQUIPMENT_LIBRARY.md Format

When `EQUIPMENT_LIBRARY.md` is first created, write the format header at the top.

## Header Template (top of file)

````markdown
# EQUIPMENT_LIBRARY.md

Catalog of designed equipment for PitHero. The implementer reads this when coding equipment factory methods — see Principal Game Engineer for the C# Equipment Creation Pattern.

## Entry Format

Each entry uses the following structure:

### <Name>

- **Type:** Weapon / Armor / Helm / Shield / Accessory
- **Subtype:** Sword / Axe / Staff / Plate / Robe / Ring / Amulet / …
- **Biome:** Cave / Forest / Castle / Underworld
- **Pit level:** N
- **Rarity:** Normal / Uncommon / Rare / Epic / Legendary
- **Attack / Defense / Stat bonus:** (per formula)
- **Stat bonuses (STR/AGI/VIT/MAG):**
- **Element:** Neutral / Fire / Water / Earth / Wind / Light / Dark
- **Resistances:** `{ Element: ±value, ... }`
- **Visual description:** 1–2 sentences for placeholder art
- **Notes:** Flavor, special-case mechanics

---
````

## Example Entry

```markdown
### Bronze Shortsword

- **Type:** Weapon
- **Subtype:** Sword
- **Biome:** Cave
- **Pit level:** 3
- **Rarity:** Normal
- **Attack:** 2
- **Stat bonuses:** 0 STR / 0 AGI / 0 VIT / 0 MAG
- **Element:** Neutral
- **Resistances:** —
- **Visual description:** A crude bronze blade with a leather-wrapped hilt; chipped along the edge.
- **Notes:** Entry-level weapon. Sets the baseline for Cave biome physical offense.

---
```

## Rules

- One blank line between entries; `---` separator after each.
- Group entries by biome → pit-level block → equipment type.
- Don't duplicate names. If a higher-rarity variant exists, suffix or rename (e.g. "Bronze Shortsword (Rare)" is fine — or use a distinct flavorful name).
- Stat values listed must match the formulas in `references/balance-formulas.md`.

## After Adding Entries

Summarize the additions in the handoff message:

```
Added equipment for Cave pit levels 11–15:
- Weapon: Iron Greatsword (Pit 15, Uncommon)
- Armor: Reinforced Chainmail (Pit 13, Normal)
- Helm: Bronze Cap (Pit 11, Normal)
- Shield: Spiked Buckler (Pit 14, Uncommon)
- Accessory: Stone Pendant (Pit 12, Normal, Earth)
```

The implementer then uses each entry plus `EQUIPMENT_BALANCE_GUIDE.md` to write the C# factory method.

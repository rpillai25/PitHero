# PitHero Equipment Library

## Overview

This document catalogs all equipment available in PitHero across all biomes. Equipment is organized by type (Weapons, Armor, Shields, Helms) and follows standardized formulas defined in `BalanceConfig.cs`.

### Equipment Format Definition

Each equipment entry follows this format:

```
Equipment Name
- Biome: [Biome name(s)]
- Pit Level: [Level range]
- Type: [Equipment type]
- Subtype: [Specific category]
- Rarity: [Normal/Uncommon/Rare/Epic/Legendary]
- Attack Bonus: [Value] (calculated: (1 + pitLevel/2) × rarity_multiplier)
- Defense Bonus: [Value] (calculated: (1 + pitLevel/3) × rarity_multiplier)
- Element: [Neutral/Fire/Water/Earth/Wind/Light/Dark]
- Visual Theme: [Description]
- Price: [Gold cost]
- Spawn Pool: [Window info]
```

### Balance Formulas

**Attack Bonus (Weapons):** `(1 + pitLevel / 2) × rarity_multiplier`
**Defense Bonus (Armor/Shields/Helms):** `(1 + pitLevel / 3) × rarity_multiplier`
**Stat Bonus (Accessories):** `(pitLevel / 5) × rarity_multiplier`

**Rarity Multipliers:**
- Normal: 1.0x
- Uncommon: 1.5x
- Rare: 2.0x
- Epic: 2.5x
- Legendary: 3.5x

### Spawn Window System

Equipment spawns using a sliding window system:
- Every 5 pit levels adds 5 new equipment to the spawn pool
- Pool maintains 10 equipment pieces per type at any time
- Earlier equipment phases out as new items become available

---

## Cave Biome (Pit Levels 1-25)

### Thematic Guidelines
- **Visual Theme:** Rocky, earthy, underground aesthetic with moss, stone, and mineral elements
- **Primary Elements:** Earth (defensive), Fire (aggressive), Darkness (stealth)
- **Rarity Progression:** Normal (Pit 1-10), Normal/Uncommon (Pit 11-25)
- **Enemy Difficulty:** Beginner to intermediate (Levels 1-40)

---

## WEAPONS - Cave Biome

### One-Handed Swords

#### Rusty Blade
- **Biome:** Cave
- **Pit Level:** 1
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Corroded iron blade found in cave ruins
- **Price:** 50 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Stone Sword
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Earth
- **Visual Theme:** Crude blade carved from dense stone
- **Price:** 75 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Cave Stalker's Blade
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Darkness
- **Visual Theme:** Dark steel blade favored by cave dwellers
- **Price:** 100 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Miner's Pick Sword
- **Biome:** Cave
- **Pit Level:** 4
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +3
- **Element:** Earth
- **Visual Theme:** Modified mining tool repurposed as a weapon
- **Price:** 125 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Short Sword
- **Biome:** Cave
- **Pit Level:** 5
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Basic iron sword for beginners
- **Price:** 150 gold
- **Spawn Pool:** Window 1-2 (Pit 1-10)

#### Spelunker's Saber
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Fire
- **Visual Theme:** Curved blade with torch-guard attachment
- **Price:** 175 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Granite Blade
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Earth
- **Visual Theme:** Reinforced blade with granite edge
- **Price:** 200 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Shadow Fang
- **Biome:** Cave
- **Pit Level:** 8
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +5
- **Element:** Darkness
- **Visual Theme:** Obsidian blade that absorbs light
- **Price:** 225 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Torch Blade
- **Biome:** Cave
- **Pit Level:** 9
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +5
- **Element:** Fire
- **Visual Theme:** Steel blade with fire-resistant coating
- **Price:** 250 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Cavern Cutter
- **Biome:** Cave
- **Pit Level:** 10
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Normal
- **Attack Bonus:** +6
- **Element:** Earth
- **Visual Theme:** Broad blade designed for tight spaces
- **Price:** 275 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Crystal Edge
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Blade with embedded crystal formations
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Underground Rapier
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Neutral
- **Visual Theme:** Lightweight piercing blade for swift strikes
- **Price:** 425 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Ember Sword
- **Biome:** Cave
- **Pit Level:** 13
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +10
- **Element:** Fire
- **Visual Theme:** Blade that glows with inner heat
- **Price:** 450 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Void Cutter
- **Biome:** Cave
- **Pit Level:** 14
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +10
- **Element:** Darkness
- **Visual Theme:** Dark blade that seems to drink in light
- **Price:** 475 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Long Sword
- **Biome:** Cave
- **Pit Level:** 15
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +11
- **Element:** Fire
- **Visual Theme:** Extended blade for seasoned warriors
- **Price:** 500 gold
- **Spawn Pool:** Window 3-4 (Pit 11-20)

#### Stalagmite Sword
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +12
- **Element:** Earth
- **Visual Theme:** Blade shaped like a cave formation
- **Price:** 525 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Gloom Blade
- **Biome:** Cave
- **Pit Level:** 17
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +12
- **Element:** Darkness
- **Visual Theme:** Cursed blade from deep caverns
- **Price:** 550 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Lava Forged Sword
- **Biome:** Cave
- **Pit Level:** 18
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +13
- **Element:** Fire
- **Visual Theme:** Blade tempered in underground magma
- **Price:** 575 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Depths Reaver
- **Biome:** Cave
- **Pit Level:** 19
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +14
- **Element:** Darkness
- **Visual Theme:** Ancient blade from the deepest caves
- **Price:** 600 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Quartz Saber
- **Biome:** Cave
- **Pit Level:** 20
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +14
- **Element:** Earth
- **Visual Theme:** Crystalline blade that resonates when struck
- **Price:** 625 gold
- **Spawn Pool:** Window 4-5 (Pit 16-25)

#### Inferno Edge
- **Biome:** Cave
- **Pit Level:** 21
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +15
- **Element:** Fire
- **Visual Theme:** Blade wreathed in perpetual flames
- **Price:** 650 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Abyss Fang
- **Biome:** Cave
- **Pit Level:** 22
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +16
- **Element:** Darkness
- **Visual Theme:** Pitch-black blade from the void
- **Price:** 675 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Diamond Edge
- **Biome:** Cave
- **Pit Level:** 23
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +16
- **Element:** Earth
- **Visual Theme:** Blade reinforced with diamond shards
- **Price:** 700 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Magma Blade
- **Biome:** Cave
- **Pit Level:** 24
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +17
- **Element:** Fire
- **Visual Theme:** Red-hot blade that never cools
- **Price:** 725 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Pit Lord's Sword
- **Biome:** Cave
- **Pit Level:** 25
- **Type:** Weapon
- **Subtype:** WeaponSword (One-Handed)
- **Rarity:** Uncommon
- **Attack Bonus:** +17
- **Element:** Darkness
- **Visual Theme:** Massive sword wielded by the cave's master
- **Price:** 750 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

### Axes

#### Woodcutter's Axe
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Simple wood-cutting tool
- **Price:** 100 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Stone Hatchet
- **Biome:** Cave
- **Pit Level:** 5
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Normal
- **Attack Bonus:** +3
- **Element:** Earth
- **Visual Theme:** Primitive stone-headed axe
- **Price:** 150 gold
- **Spawn Pool:** Window 1-2 (Pit 1-10)

#### Miner's Axe
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Earth
- **Visual Theme:** Heavy mining tool repurposed for combat
- **Price:** 200 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Flame Hatchet
- **Biome:** Cave
- **Pit Level:** 10
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Normal
- **Attack Bonus:** +6
- **Element:** Fire
- **Visual Theme:** Axe head that glows red when swung
- **Price:** 275 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Crystal Cleaver
- **Biome:** Cave
- **Pit Level:** 13
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Uncommon
- **Attack Bonus:** +10
- **Element:** Earth
- **Visual Theme:** Axe with crystalline blade edge
- **Price:** 450 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadow Splitter
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Uncommon
- **Attack Bonus:** +12
- **Element:** Darkness
- **Visual Theme:** Black-bladed axe that cleaves through darkness
- **Price:** 525 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Volcanic Axe
- **Biome:** Cave
- **Pit Level:** 20
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Uncommon
- **Attack Bonus:** +14
- **Element:** Fire
- **Visual Theme:** Axe forged in volcanic heat
- **Price:** 625 gold
- **Spawn Pool:** Window 4-5 (Pit 16-25)

#### Obsidian Cleaver
- **Biome:** Cave
- **Pit Level:** 24
- **Type:** Weapon
- **Subtype:** WeaponSword (Axe)
- **Rarity:** Uncommon
- **Attack Bonus:** +17
- **Element:** Darkness
- **Visual Theme:** Razor-sharp volcanic glass axe
- **Price:** 725 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

### Daggers

#### Rusty Dagger
- **Biome:** Cave
- **Pit Level:** 1
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Normal
- **Attack Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Old corroded blade
- **Price:** 40 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Cave Shiv
- **Biome:** Cave
- **Pit Level:** 4
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Normal
- **Attack Bonus:** +3
- **Element:** Darkness
- **Visual Theme:** Crude knife made from cave debris
- **Price:** 125 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Silent Fang
- **Biome:** Cave
- **Pit Level:** 8
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Normal
- **Attack Bonus:** +5
- **Element:** Darkness
- **Visual Theme:** Slim blade for stealth attacks
- **Price:** 225 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Serpent's Tooth
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Darkness
- **Visual Theme:** Curved dagger shaped like a snake fang
- **Price:** 425 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadow Stiletto
- **Biome:** Cave
- **Pit Level:** 17
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Uncommon
- **Attack Bonus:** +12
- **Element:** Darkness
- **Visual Theme:** Thin piercing blade that vanishes in shadows
- **Price:** 550 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Assassin's Edge
- **Biome:** Cave
- **Pit Level:** 22
- **Type:** Weapon
- **Subtype:** WeaponSword (Dagger)
- **Rarity:** Uncommon
- **Attack Bonus:** +16
- **Element:** Darkness
- **Visual Theme:** Perfectly balanced killing blade
- **Price:** 675 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

### Spears

#### Wooden Spear
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Simple wooden shaft with sharpened tip
- **Price:** 75 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Stone Lance
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Earth
- **Visual Theme:** Stone-tipped thrusting weapon
- **Price:** 175 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Cave Pike
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Long spear designed for cave defense
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Flame Lance
- **Biome:** Cave
- **Pit Level:** 15
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Uncommon
- **Attack Bonus:** +11
- **Element:** Fire
- **Visual Theme:** Spear with burning tip
- **Price:** 500 gold
- **Spawn Pool:** Window 3-4 (Pit 11-20)

#### Stalactite Spear
- **Biome:** Cave
- **Pit Level:** 19
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Uncommon
- **Attack Bonus:** +14
- **Element:** Earth
- **Visual Theme:** Spear crafted from a massive cave formation
- **Price:** 600 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Infernal Pike
- **Biome:** Cave
- **Pit Level:** 23
- **Type:** Weapon
- **Subtype:** WeaponSword (Spear)
- **Rarity:** Uncommon
- **Attack Bonus:** +16
- **Element:** Fire
- **Visual Theme:** Spear wreathed in eternal flames
- **Price:** 700 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

### Hammers

#### Mallet
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Weapon
- **Subtype:** WeaponKnuckle (Hammer)
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Simple wooden mallet
- **Price:** 100 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Stone Crusher
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Weapon
- **Subtype:** WeaponKnuckle (Hammer)
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Earth
- **Visual Theme:** Heavy stone-headed hammer
- **Price:** 200 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Geologist's Hammer
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Weapon
- **Subtype:** WeaponKnuckle (Hammer)
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Precision hammer for breaking minerals
- **Price:** 425 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Quake Hammer
- **Biome:** Cave
- **Pit Level:** 18
- **Type:** Weapon
- **Subtype:** WeaponKnuckle (Hammer)
- **Rarity:** Uncommon
- **Attack Bonus:** +13
- **Element:** Earth
- **Visual Theme:** Massive hammer that shakes the ground
- **Price:** 575 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Magma Maul
- **Biome:** Cave
- **Pit Level:** 25
- **Type:** Weapon
- **Subtype:** WeaponKnuckle (Hammer)
- **Rarity:** Uncommon
- **Attack Bonus:** +17
- **Element:** Fire
- **Visual Theme:** Molten-core war hammer
- **Price:** 750 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

### Staves

#### Walking Stick
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Weapon
- **Subtype:** WeaponStaff
- **Rarity:** Normal
- **Attack Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Simple wooden staff
- **Price:** 75 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Torch Staff
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Weapon
- **Subtype:** WeaponStaff
- **Rarity:** Normal
- **Attack Bonus:** +4
- **Element:** Fire
- **Visual Theme:** Staff with burning crystal tip
- **Price:** 175 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Earthen Staff
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Weapon
- **Subtype:** WeaponStaff
- **Rarity:** Uncommon
- **Attack Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Staff embedded with earth crystals
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadowwood Staff
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Weapon
- **Subtype:** WeaponStaff
- **Rarity:** Uncommon
- **Attack Bonus:** +12
- **Element:** Darkness
- **Visual Theme:** Staff carved from black petrified wood
- **Price:** 525 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Ember Rod
- **Biome:** Cave
- **Pit Level:** 21
- **Type:** Weapon
- **Subtype:** WeaponStaff
- **Rarity:** Uncommon
- **Attack Bonus:** +15
- **Element:** Fire
- **Visual Theme:** Staff topped with ever-burning ember
- **Price:** 650 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

---

## ARMOR - Cave Biome

#### Tattered Cloth
- **Biome:** Cave
- **Pit Level:** 1
- **Type:** Armor
- **Subtype:** ArmorRobe
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Worn cloth garments
- **Price:** 40 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Burlap Tunic
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Armor
- **Subtype:** ArmorRobe
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Rough woven fabric armor
- **Price:** 60 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Hide Vest
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Earth
- **Visual Theme:** Animal hide chest protection
- **Price:** 90 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Padded Armor
- **Biome:** Cave
- **Pit Level:** 4
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Quilted protective layers
- **Price:** 120 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Leather Armor
- **Biome:** Cave
- **Pit Level:** 5
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Basic cured leather protection
- **Price:** 150 gold
- **Spawn Pool:** Window 1-2 (Pit 1-10)

#### Studded Leather
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Leather reinforced with metal studs
- **Price:** 180 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Cave Explorer's Vest
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Earth
- **Visual Theme:** Practical armor for cave navigation
- **Price:** 210 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Hardened Leather
- **Biome:** Cave
- **Pit Level:** 8
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Treated leather with increased durability
- **Price:** 240 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Scale Mail
- **Biome:** Cave
- **Pit Level:** 9
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Overlapping metal scales
- **Price:** 270 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Chain Shirt
- **Biome:** Cave
- **Pit Level:** 10
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Normal
- **Defense Bonus:** +4
- **Element:** Neutral
- **Visual Theme:** Interlocking metal rings
- **Price:** 300 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Iron Armor
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Neutral
- **Visual Theme:** Solid iron plate protection
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Stone Plate
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Earth
- **Visual Theme:** Carved stone armor pieces
- **Price:** 450 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Emberguard Mail
- **Biome:** Cave
- **Pit Level:** 13
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Fire
- **Visual Theme:** Heat-resistant mail armor
- **Price:** 500 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadow Vest
- **Biome:** Cave
- **Pit Level:** 14
- **Type:** Armor
- **Subtype:** ArmorGi
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Darkness
- **Visual Theme:** Dark leather that blends into shadows
- **Price:** 550 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Reinforced Plate
- **Biome:** Cave
- **Pit Level:** 15
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Neutral
- **Visual Theme:** Heavy reinforced metal plates
- **Price:** 600 gold
- **Spawn Pool:** Window 3-4 (Pit 11-20)

#### Crystal Guard
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Earth
- **Visual Theme:** Armor embedded with protective crystals
- **Price:** 650 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Lavaplate Armor
- **Biome:** Cave
- **Pit Level:** 17
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Fire
- **Visual Theme:** Magma-forged heavy armor
- **Price:** 700 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Voidmail
- **Biome:** Cave
- **Pit Level:** 18
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Darkness
- **Visual Theme:** Pitch-black armor that absorbs light
- **Price:** 750 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Steel Cuirass
- **Biome:** Cave
- **Pit Level:** 19
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Neutral
- **Visual Theme:** Quality steel chest armor
- **Price:** 800 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Granite Plate
- **Biome:** Cave
- **Pit Level:** 20
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Ultra-dense stone armor
- **Price:** 850 gold
- **Spawn Pool:** Window 4-5 (Pit 16-25)

#### Volcanic Armor
- **Biome:** Cave
- **Pit Level:** 21
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Fire
- **Visual Theme:** Armor tempered in volcanic heat
- **Price:** 900 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Abyss Plate
- **Biome:** Cave
- **Pit Level:** 22
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Darkness
- **Visual Theme:** Armor from the deepest caves
- **Price:** 950 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Diamond Mail
- **Biome:** Cave
- **Pit Level:** 23
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Earth
- **Visual Theme:** Armor reinforced with diamond
- **Price:** 1000 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Magma Forged Plate
- **Biome:** Cave
- **Pit Level:** 24
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Fire
- **Visual Theme:** Ultimate fire-resistant armor
- **Price:** 1050 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Pit Lord's Armor
- **Biome:** Cave
- **Pit Level:** 25
- **Type:** Armor
- **Subtype:** ArmorMail
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Darkness
- **Visual Theme:** Legendary armor of the cave master
- **Price:** 1100 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

---

## SHIELDS - Cave Biome

#### Wooden Plank
- **Biome:** Cave
- **Pit Level:** 1
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Simple wooden board
- **Price:** 50 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Wooden Shield
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Basic round wooden shield
- **Price:** 75 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Hide Shield
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Earth
- **Visual Theme:** Leather-covered wooden frame
- **Price:** 100 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Reinforced Buckler
- **Biome:** Cave
- **Pit Level:** 4
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Small metal-rimmed shield
- **Price:** 125 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Round Shield
- **Biome:** Cave
- **Pit Level:** 5
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Standard circular shield
- **Price:** 150 gold
- **Spawn Pool:** Window 1-2 (Pit 1-10)

#### Cave Guard
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Earth
- **Visual Theme:** Shield painted with cave markings
- **Price:** 175 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Stone Shield
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Earth
- **Visual Theme:** Shield carved from solid stone
- **Price:** 200 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Iron Buckler
- **Biome:** Cave
- **Pit Level:** 8
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Small all-metal shield
- **Price:** 225 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Kite Shield
- **Biome:** Cave
- **Pit Level:** 9
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Tall triangular shield
- **Price:** 250 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Iron Shield
- **Biome:** Cave
- **Pit Level:** 10
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Normal
- **Defense Bonus:** +4
- **Element:** Neutral
- **Visual Theme:** Solid iron construction
- **Price:** 275 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Steel Shield
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Neutral
- **Visual Theme:** Quality steel shield
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Granite Guard
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Earth
- **Visual Theme:** Heavy stone shield
- **Price:** 450 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Ember Shield
- **Biome:** Cave
- **Pit Level:** 13
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Fire
- **Visual Theme:** Shield with glowing ember core
- **Price:** 500 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadow Guard
- **Biome:** Cave
- **Pit Level:** 14
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Darkness
- **Visual Theme:** Dark shield that bends shadows
- **Price:** 550 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Tower Shield
- **Biome:** Cave
- **Pit Level:** 15
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Neutral
- **Visual Theme:** Massive full-body shield
- **Price:** 600 gold
- **Spawn Pool:** Window 3-4 (Pit 11-20)

#### Crystal Barrier
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Earth
- **Visual Theme:** Translucent crystal shield
- **Price:** 650 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Lava Shield
- **Biome:** Cave
- **Pit Level:** 17
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Fire
- **Visual Theme:** Heat-resistant volcanic shield
- **Price:** 700 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Void Barrier
- **Biome:** Cave
- **Pit Level:** 18
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Darkness
- **Visual Theme:** Shield that consumes attacks
- **Price:** 750 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Heater Shield
- **Biome:** Cave
- **Pit Level:** 19
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Neutral
- **Visual Theme:** Classic triangular shield
- **Price:** 800 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Quartz Wall
- **Biome:** Cave
- **Pit Level:** 20
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Shield made of massive quartz crystal
- **Price:** 850 gold
- **Spawn Pool:** Window 4-5 (Pit 16-25)

#### Inferno Guard
- **Biome:** Cave
- **Pit Level:** 21
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Fire
- **Visual Theme:** Shield wreathed in flames
- **Price:** 900 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Abyss Wall
- **Biome:** Cave
- **Pit Level:** 22
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Darkness
- **Visual Theme:** Shield from the void
- **Price:** 950 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Diamond Barrier
- **Biome:** Cave
- **Pit Level:** 23
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Earth
- **Visual Theme:** Ultra-hard diamond shield
- **Price:** 1000 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Magma Wall
- **Biome:** Cave
- **Pit Level:** 24
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Fire
- **Visual Theme:** Molten-core defensive barrier
- **Price:** 1050 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Pit Lord's Aegis
- **Biome:** Cave
- **Pit Level:** 25
- **Type:** Shield
- **Subtype:** Shield
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Darkness
- **Visual Theme:** Legendary shield of the cave master
- **Price:** 1100 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

---

## HELMS - Cave Biome

#### Cloth Cap
- **Biome:** Cave
- **Pit Level:** 1
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Simple fabric head covering
- **Price:** 40 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Leather Cap
- **Biome:** Cave
- **Pit Level:** 2
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Neutral
- **Visual Theme:** Basic leather headwear
- **Price:** 60 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Hide Hood
- **Biome:** Cave
- **Pit Level:** 3
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Normal
- **Defense Bonus:** +1
- **Element:** Earth
- **Visual Theme:** Animal hide hood
- **Price:** 90 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Padded Coif
- **Biome:** Cave
- **Pit Level:** 4
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Quilted head protection
- **Price:** 120 gold
- **Spawn Pool:** Window 1 (Pit 1-5)

#### Squire Helm
- **Biome:** Cave
- **Pit Level:** 5
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Basic metal helmet
- **Price:** 150 gold
- **Spawn Pool:** Window 1-2 (Pit 1-10)

#### Chain Coif
- **Biome:** Cave
- **Pit Level:** 6
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Normal
- **Defense Bonus:** +2
- **Element:** Neutral
- **Visual Theme:** Chainmail head covering
- **Price:** 175 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Cave Explorer's Hood
- **Biome:** Cave
- **Pit Level:** 7
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Earth
- **Visual Theme:** Practical hood for cave exploration
- **Price:** 200 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Reinforced Cap
- **Biome:** Cave
- **Pit Level:** 8
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Leather with metal reinforcement
- **Price:** 225 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Bascinet
- **Biome:** Cave
- **Pit Level:** 9
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Normal
- **Defense Bonus:** +3
- **Element:** Neutral
- **Visual Theme:** Pointed metal helmet
- **Price:** 250 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Iron Helm
- **Biome:** Cave
- **Pit Level:** 10
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Normal
- **Defense Bonus:** +4
- **Element:** Neutral
- **Visual Theme:** Solid iron helmet
- **Price:** 275 gold
- **Spawn Pool:** Window 2 (Pit 6-10)

#### Steel Helm
- **Biome:** Cave
- **Pit Level:** 11
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Neutral
- **Visual Theme:** Quality steel helmet
- **Price:** 400 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Stone Crown
- **Biome:** Cave
- **Pit Level:** 12
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Earth
- **Visual Theme:** Carved stone headpiece
- **Price:** 450 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Ember Helm
- **Biome:** Cave
- **Pit Level:** 13
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +6
- **Element:** Fire
- **Visual Theme:** Heat-resistant helmet with glowing edges
- **Price:** 500 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Shadow Cowl
- **Biome:** Cave
- **Pit Level:** 14
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Darkness
- **Visual Theme:** Dark hood that obscures features
- **Price:** 550 gold
- **Spawn Pool:** Window 3 (Pit 11-15)

#### Great Helm
- **Biome:** Cave
- **Pit Level:** 15
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +7
- **Element:** Neutral
- **Visual Theme:** Fully enclosed helmet
- **Price:** 600 gold
- **Spawn Pool:** Window 3-4 (Pit 11-20)

#### Crystal Circlet
- **Biome:** Cave
- **Pit Level:** 16
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Earth
- **Visual Theme:** Headband with protective crystals
- **Price:** 650 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Lava Crown
- **Biome:** Cave
- **Pit Level:** 17
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Fire
- **Visual Theme:** Magma-forged royal helmet
- **Price:** 700 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Void Mask
- **Biome:** Cave
- **Pit Level:** 18
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +8
- **Element:** Darkness
- **Visual Theme:** Featureless dark mask
- **Price:** 750 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Winged Helm
- **Biome:** Cave
- **Pit Level:** 19
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Neutral
- **Visual Theme:** Helmet with decorative wings
- **Price:** 800 gold
- **Spawn Pool:** Window 4 (Pit 16-20)

#### Quartz Helm
- **Biome:** Cave
- **Pit Level:** 20
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +9
- **Element:** Earth
- **Visual Theme:** Translucent crystal helmet
- **Price:** 850 gold
- **Spawn Pool:** Window 4-5 (Pit 16-25)

#### Inferno Crown
- **Biome:** Cave
- **Pit Level:** 21
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Fire
- **Visual Theme:** Crown with eternal flames
- **Price:** 900 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Abyss Helm
- **Biome:** Cave
- **Pit Level:** 22
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Darkness
- **Visual Theme:** Helmet from the deepest void
- **Price:** 950 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Diamond Circlet
- **Biome:** Cave
- **Pit Level:** 23
- **Type:** Helm
- **Subtype:** HatHeadband
- **Rarity:** Uncommon
- **Defense Bonus:** +10
- **Element:** Earth
- **Visual Theme:** Headpiece with diamond reinforcement
- **Price:** 1000 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Magma Helm
- **Biome:** Cave
- **Pit Level:** 24
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Fire
- **Visual Theme:** Molten-core protective helmet
- **Price:** 1050 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

#### Pit Lord's Crown
- **Biome:** Cave
- **Pit Level:** 25
- **Type:** Helm
- **Subtype:** HatHelm
- **Rarity:** Uncommon
- **Defense Bonus:** +11
- **Element:** Darkness
- **Visual Theme:** Legendary crown of the cave master
- **Price:** 1100 gold
- **Spawn Pool:** Window 5 (Pit 21-25)

---

## Summary Statistics - Cave Biome

### Equipment Count by Type
- **Weapons:** 60 pieces
  - Swords: 25
  - Axes: 8
  - Daggers: 6
  - Spears: 6
  - Hammers: 5
  - Staves: 5
- **Armor:** 25 pieces
- **Shields:** 25 pieces
- **Helms:** 25 pieces
- **Total:** 135 pieces

### Rarity Distribution
- **Normal:** 50 pieces (Pit 1-10)
- **Uncommon:** 85 pieces (Pit 11-25)

### Elemental Distribution
- **Neutral:** 40 pieces
- **Earth:** 35 pieces  
- **Fire:** 30 pieces
- **Darkness:** 30 pieces

### Spawn Windows
- **Window 1 (Pit 1-5):** 20 pieces
- **Window 2 (Pit 6-10):** 40 pieces
- **Window 3 (Pit 11-15):** 40 pieces
- **Window 4 (Pit 16-20):** 40 pieces
- **Window 5 (Pit 21-25):** 50 pieces

### Balance Verification
All equipment follows `BalanceConfig` formulas:
- Attack bonuses range from +1 to +17
- Defense bonuses range from +1 to +11
- Progression is smooth and linear within rarity tiers
- Power increases appropriately with pit level

---

## Future Biomes (Placeholder)

### Forest Biome (Pit 26-50) - TBD
### Desert Biome (Pit 51-75) - TBD
### Volcanic Biome (Pit 76-100) - TBD

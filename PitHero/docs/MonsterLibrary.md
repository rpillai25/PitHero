# Monster Library - Cave Biome (Pit Levels 1-25)

**Version**: 1.0  
**Last Updated**: February 21, 2026  
**Total Monsters**: 25 (10 Existing + 15 New Designs)

---

## Monster Entry Format

Each monster entry contains the following information:

- **Name**: Monster identifier
- **Status**: `IMPLEMENTED` or `DESIGN` (needs implementation)
- **Biome(s)**: Cave (levels 1-25 for this document)
- **Pit Level**: Base level for this monster
- **Spawn Range**: Pit levels where this monster can spawn
- **Boss Type**: None / Small Boss / Big Boss
- **Archetype**: Balanced / Tank / FastFragile / MagicUser
- **Element**: Neutral / Fire / Water / Earth / Wind / Light / Dark
- **Damage Kind**: Physical / Magic
- **Visual Size**: Small (32×32) / Medium (48×48) / Large (64×64)
- **Description**: Visual and thematic description
- **Stats @ Level**: Sample stats using BalanceConfig formulas
- **Spawn Pool**: Which 5-level pool(s) this monster belongs to

---

## Spawn Pool System

The Cave Biome uses a sliding window system where every 5 pit levels introduces a new pool of monsters:

- **Pool 1 (Pit 1-5)**: 10 monsters from levels 1-5, boss at pit 5
- **Pool 2 (Pit 6-10)**: 10 monsters from levels 1-9, boss at pit 10
- **Pool 3 (Pit 11-15)**: 10 monsters from levels 3-14, boss at pit 15
- **Pool 4 (Pit 16-20)**: 10 monsters from levels 6-19, boss at pit 20
- **Pool 5 (Pit 21-25)**: 10 monsters from levels 11-24, boss at pit 25

Boss floors (5, 10, 15, 20, 25) spawn only the boss monster with a +2 level bonus.

---

## Monster Roster

### Pool 1: Early Cave (Pit Levels 1-5)

---

#### 1. Slime
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 1
- **Spawn Range**: Pit 1-10
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Water
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Gelatinous blob that oozes along the cave floor. Translucent blue-green with a simple core visible inside.
- **Stats @ Level 1**:
  - HP: 15 (10 + 1×5 = 15)
  - STR: 1, AGI: 1, VIT: 1, MAG: 1
  - XP Yield: 18, JP: 2, SP: 4, Gold: 5
- **Elemental Properties**: 30% resist Water, 30% weak to Fire
- **Spawn Pool**: Pool 1, Pool 2

---

#### 2. Bat
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 1
- **Spawn Range**: Pit 1-10
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Wind
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Agile cave bat with leathery wings. Fast but fragile, swoops down from above.
- **Stats @ Level 1**:
  - HP: 11 (10 + 1×5 = 15, ×0.7 = 10.5 → 11)
  - STR: 1 (×1.2 = 1.2 → 1), AGI: 2 (×1.5 = 1.5 → 2), VIT: 1 (×0.6 = 0.6 → 1), MAG: 1 (×0.9 = 0.9 → 1)
  - XP Yield: 18, JP: 2, SP: 4, Gold: 5
- **Elemental Properties**: 30% resist Wind, 30% weak to Earth
- **Spawn Pool**: Pool 1, Pool 2

---

#### 3. Rat
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 1
- **Spawn Range**: Pit 1-10
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Neutral
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Scrappy cave rat with matted fur. Balanced stats, slightly evasive.
- **Stats @ Level 1**:
  - HP: 15
  - STR: 1, AGI: 1, VIT: 1, MAG: 1
  - XP Yield: 18, JP: 2, SP: 4, Gold: 5
- **Elemental Properties**: No resistances or weaknesses
- **Spawn Pool**: Pool 1, Pool 2

---

#### 4. Cave Mushroom ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 2
- **Spawn Range**: Pit 1-10
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Ambulatory fungus with toxic spores. Brown cap with glowing spots, shuffles on root-like tendrils.
- **Stats @ Level 2**:
  - HP: 20 (10 + 2×5 = 20)
  - STR: 2, AGI: 2, VIT: 2, MAG: 2
  - XP Yield: 26, JP: 3, SP: 5, Gold: 7
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 1, Pool 2
- **Design Notes**: Could have poison mechanic in future implementation

---

#### 5. Stone Beetle ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 4
- **Spawn Range**: Pit 1-10
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Armored insect with rocky carapace. Slow but durable with high defense.
- **Stats @ Level 4**:
  - HP: 45 (10 + 4×5 = 30, ×1.5 = 45)
  - STR: 3 (3×0.8 = 2.4 → 3), AGI: 2 (3×0.6 = 1.8 → 2), VIT: 4 (3×1.3 = 3.9 → 4), MAG: 2 (3×0.7 = 2.1 → 2)
  - XP Yield: 42, JP: 5, SP: 7, Gold: 11
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 1, Pool 2

---

#### 6. Stone Guardian ⭐ NEW (BOSS)
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 5
- **Spawn Range**: Pit 5 only (Small Boss)
- **Boss Type**: Small Boss
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Animated stone statue guarding ancient cave passages. Moss-covered with glowing rune eyes. Slow but devastating.
- **Stats @ Level 7** (Pit 5 boss gets +2 level bonus):
  - HP: 86 (10 + 7×5 = 45, ×1.5 = 67.5, boss modifier ×1.25 ≈ 86)
  - STR: 4 (5×0.8 = 4), AGI: 3 (5×0.6 = 3), VIT: 7 (5×1.3 = 6.5 → 7), MAG: 4 (5×0.7 = 3.5 → 4)
  - XP Yield: 66 (×1.5 for boss), JP: 9, SP: 10, Gold: 20
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 1 (Boss floor only)
- **Design Notes**: First boss encounter, teaches players about Tank archetype

---

### Pool 2: Mid Cave (Pit Levels 6-10)

---

#### 7. Goblin
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 3
- **Spawn Range**: Pit 6-20
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Cunning green-skinned humanoid with crude weapons. Smart and sometimes dodges attacks.
- **Stats @ Level 3**:
  - HP: 25 (10 + 3×5 = 25)
  - STR: 3, AGI: 3, VIT: 3, MAG: 3
  - XP Yield: 34, JP: 4, SP: 6, Gold: 9
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 2, Pool 3, Pool 4, Pool 5

---

#### 8. Spider
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 3
- **Spawn Range**: Pit 6-25
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Giant cave spider with eight legs. Fast and can potentially poison prey.
- **Stats @ Level 3**:
  - HP: 18 (25 × 0.7 = 17.5 → 18)
  - STR: 4 (3×1.2 = 3.6 → 4), AGI: 5 (3×1.5 = 4.5 → 5), VIT: 2 (3×0.6 = 1.8 → 2), MAG: 3 (3×0.9 = 2.7 → 3)
  - XP Yield: 34, JP: 4, SP: 6, Gold: 9
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 2, Pool 3, Pool 4, Pool 5

---

#### 9. Snake
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 3
- **Spawn Range**: Pit 6-25
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Venomous serpent slithering through caves. High attack, low defense.
- **Stats @ Level 3**:
  - HP: 18
  - STR: 4, AGI: 5, VIT: 2, MAG: 3
  - XP Yield: 34, JP: 4, SP: 6, Gold: 9
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 2, Pool 3, Pool 4, Pool 5

---

#### 10. Shadow Imp ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 7
- **Spawn Range**: Pit 6-15
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Dark
- **Damage Kind**: Physical
- **Visual Size**: Small (32×32)
- **Description**: Mischievous dark creature with glowing red eyes. Darting movements, prefers ambush tactics.
- **Stats @ Level 7**:
  - HP: 33 (10 + 7×5 = 45, ×0.7 = 31.5 → 33)
  - STR: 6 (5×1.2 = 6), AGI: 8 (5×1.5 = 7.5 → 8), VIT: 3 (5×0.6 = 3), MAG: 5 (5×0.9 = 4.5 → 5)
  - XP Yield: 66, JP: 8, SP: 10, Gold: 17
- **Elemental Properties**: 30% resist Dark, 30% weak to Light
- **Spawn Pool**: Pool 2, Pool 3

---

#### 11. Tunnel Worm ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 8
- **Spawn Range**: Pit 6-15
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Large segmented burrowing worm. Pale pink with ring-like segments and circular maw lined with teeth.
- **Stats @ Level 8**:
  - HP: 55 (10 + 8×5 = 50)
  - STR: 6, AGI: 6, VIT: 6, MAG: 6
  - XP Yield: 74, JP: 9, SP: 11, Gold: 19
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 2, Pool 3

---

#### 12. Fire Lizard ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 9
- **Spawn Range**: Pit 6-15
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Fire
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Salamander-like creature with glowing orange scales. Breathes small flames, lives near lava pools.
- **Stats @ Level 9**:
  - HP: 60 (10 + 9×5 = 55)
  - STR: 7, AGI: 7, VIT: 7, MAG: 7
  - XP Yield: 82, JP: 10, SP: 12, Gold: 21
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 2, Pool 3

---

#### 13. Pit Lord (BOSS)
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 10
- **Spawn Range**: Pit 10, 15, 20, 25 (reused boss)
- **Boss Type**: Small Boss (Big Boss at Pit 25)
- **Archetype**: Tank
- **Element**: Fire
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Demonic overlord wreathed in flames. Massive horned figure with molten weapons. Much stronger than regular enemies.
- **Stats @ Level 12** (Pit 10 boss gets +2 level bonus):
  - HP: 128 (10 + 12×5 = 70, ×1.5 = 105, boss modifier ×1.25 ≈ 131)
  - STR: 7 (8×0.8 = 6.4 → 7), AGI: 5 (8×0.6 = 4.8 → 5), VIT: 11 (8×1.3 = 10.4 → 11), MAG: 6 (8×0.7 = 5.6 → 6)
  - XP Yield: 146 (×1.5 for boss), JP: 13, SP: 15, Gold: 35
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 2 (Boss floor only)
- **Design Notes**: Reappears as boss at multiple pit levels with scaled stats

---

### Pool 3: Deep Cave (Pit Levels 11-15)

---

#### 14. Skeleton
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 6
- **Spawn Range**: Pit 11-25
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Dark
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Animated bones held together by dark magic. Resistant to status effects.
- **Stats @ Level 6**:
  - HP: 68 (10 + 6×5 = 40, ×1.5 = 60, rounded up to 68)
  - STR: 4 (4×0.8 = 3.2 → 4), AGI: 3 (4×0.6 = 2.4 → 3), VIT: 6 (4×1.3 = 5.2 → 6), MAG: 3 (4×0.7 = 2.8 → 3)
  - XP Yield: 58, JP: 7, SP: 9, Gold: 15
- **Elemental Properties**: 30% resist Dark, 30% weak to Light
- **Spawn Pool**: Pool 3, Pool 4, Pool 5

---

#### 15. Orc
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 6
- **Spawn Range**: Pit 11-25
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Fire
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Brutish green warrior with crude armor. Hits hard but moves slowly.
- **Stats @ Level 6**:
  - HP: 68
  - STR: 4, AGI: 3, VIT: 6, MAG: 3
  - XP Yield: 58, JP: 7, SP: 9, Gold: 15
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 3, Pool 4, Pool 5

---

#### 16. Wraith
- **Status**: `IMPLEMENTED`
- **Biome(s)**: Cave
- **Pit Level**: 6
- **Spawn Range**: Pit 11-25
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Dark
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Ghostly specter floating through the darkness. High speed and evasion.
- **Stats @ Level 6**:
  - HP: 32 (40 × 0.7 = 28 → 32)
  - STR: 5 (4×1.2 = 4.8 → 5), AGI: 6 (4×1.5 = 6), VIT: 3 (4×0.6 = 2.4 → 3), MAG: 4 (4×0.9 = 3.6 → 4)
  - XP Yield: 58, JP: 7, SP: 9, Gold: 15
- **Elemental Properties**: 30% resist Dark, 30% weak to Light
- **Spawn Pool**: Pool 3, Pool 4, Pool 5

---

#### 17. Magma Ooze ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 11
- **Spawn Range**: Pit 11-20
- **Boss Type**: None
- **Archetype**: Balanced
- **Element**: Fire
- **Damage Kind**: Magic
- **Visual Size**: Medium (48×48)
- **Description**: Molten slime bubbling with lava. Orange-red with constantly shifting form and intense heat aura.
- **Stats @ Level 11**:
  - HP: 80 (10 + 11×5 = 65)
  - STR: 8, AGI: 8, VIT: 8, MAG: 8
  - XP Yield: 98, JP: 12, SP: 14, Gold: 25
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 3, Pool 4
- **Design Notes**: First magic damage dealer in cave biome

---

#### 18. Crystal Golem ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 12
- **Spawn Range**: Pit 11-20
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Hulking crystalline construct with faceted limbs. Translucent blue-purple crystals, refracts light beautifully.
- **Stats @ Level 12**:
  - HP: 128 (10 + 12×5 = 70, ×1.5 = 105 → 128)
  - STR: 7 (8×0.8 = 6.4 → 7), AGI: 5 (8×0.6 = 4.8 → 5), VIT: 11 (8×1.3 = 10.4 → 11), MAG: 6 (8×0.7 = 5.6 → 6)
  - XP Yield: 106, JP: 13, SP: 15, Gold: 27
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 3, Pool 4

---

#### 19. Cave Troll ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 13
- **Spawn Range**: Pit 11-20
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Massive hunched brute with rocky hide. Gray-brown skin with moss patches, wielding crude club.
- **Stats @ Level 13**:
  - HP: 138 (10 + 13×5 = 75, ×1.5 = 112.5 → 138)
  - STR: 8 (9×0.8 = 7.2 → 8), AGI: 6 (9×0.6 = 5.4 → 6), VIT: 12 (9×1.3 = 11.7 → 12), MAG: 7 (9×0.7 = 6.3 → 7)
  - XP Yield: 114, JP: 14, SP: 16, Gold: 29
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 3, Pool 4

---

#### 20. Ghost Miner ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 14
- **Spawn Range**: Pit 11-20
- **Boss Type**: None
- **Archetype**: MagicUser
- **Element**: Dark
- **Damage Kind**: Magic
- **Visual Size**: Medium (48×48)
- **Description**: Spectral undead still searching for treasure. Translucent with phantom pickaxe, emits eerie blue glow.
- **Stats @ Level 14**:
  - HP: 96 (10 + 14×5 = 80, ×0.9 = 72 → 96)
  - STR: 7 (10×0.7 = 7), AGI: 8 (10×0.8 = 8), VIT: 8 (10×0.8 = 8), MAG: 15 (10×1.5 = 15)
  - XP Yield: 122, JP: 15, SP: 17, Gold: 31
- **Elemental Properties**: 30% resist Dark, 30% weak to Light
- **Spawn Pool**: Pool 3, Pool 4

---

#### 21. Earth Elemental ⭐ NEW (BOSS)
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 15
- **Spawn Range**: Pit 15 only (Small Boss)
- **Boss Type**: Small Boss
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Living embodiment of stone and earth. Boulder-like body with craggy features, moss and crystal growths.
- **Stats @ Level 17** (Pit 15 boss gets +2 level bonus):
  - HP: 193 (10 + 17×5 = 95, ×1.5 = 142.5, boss modifier ×1.35 ≈ 193)
  - STR: 10 (12×0.8 = 9.6 → 10), AGI: 8 (12×0.6 = 7.2 → 8), VIT: 16 (12×1.3 = 15.6 → 16), MAG: 9 (12×0.7 = 8.4 → 9)
  - XP Yield: 206 (×1.5 for boss), JP: 18, SP: 20, Gold: 50
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 3 (Boss floor only)
- **Design Notes**: Mid-game boss, demonstrates Tank archetype at higher power level

---

### Pool 4: Ancient Cave (Pit Levels 16-20)

---

#### 22. Shadow Beast ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 16
- **Spawn Range**: Pit 16-25
- **Boss Type**: None
- **Archetype**: FastFragile
- **Element**: Dark
- **Damage Kind**: Physical
- **Visual Size**: Medium (48×48)
- **Description**: Nightmare creature formed from living shadows. Quadrupedal with glowing eyes, constantly shifting form.
- **Stats @ Level 16**:
  - HP: 63 (10 + 16×5 = 90, ×0.7 = 63)
  - STR: 13 (11×1.2 = 13.2 → 13), AGI: 17 (11×1.5 = 16.5 → 17), VIT: 7 (11×0.6 = 6.6 → 7), MAG: 10 (11×0.9 = 9.9 → 10)
  - XP Yield: 138, JP: 17, SP: 19, Gold: 35
- **Elemental Properties**: 30% resist Dark, 30% weak to Light
- **Spawn Pool**: Pool 4, Pool 5

---

#### 23. Lava Drake ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 17
- **Spawn Range**: Pit 16-25
- **Boss Type**: None
- **Archetype**: MagicUser
- **Element**: Fire
- **Damage Kind**: Magic
- **Visual Size**: Large (64×64)
- **Description**: Small dragon inhabiting volcanic caves. Red scales with ember breath, wings too small to fly but intimidating.
- **Stats @ Level 17**:
  - HP: 117 (10 + 17×5 = 95, ×0.9 = 85.5 → 117)
  - STR: 9 (12×0.7 = 8.4 → 9), AGI: 10 (12×0.8 = 9.6 → 10), VIT: 10 (12×0.8 = 9.6 → 10), MAG: 18 (12×1.5 = 18)
  - XP Yield: 146, JP: 18, SP: 20, Gold: 37
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 4, Pool 5
- **Design Notes**: Precursor to the Ancient Wyrm boss at pit 25

---

#### 24. Stone Wyrm ⭐ NEW
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 18
- **Spawn Range**: Pit 16-25
- **Boss Type**: None
- **Archetype**: Tank
- **Element**: Earth
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Serpentine beast with stony scales. Segmented body like living rock formations, slow but devastating.
- **Stats @ Level 18**:
  - HP: 203 (10 + 18×5 = 100, ×1.5 = 150 → 203)
  - STR: 11 (13×0.8 = 10.4 → 11), AGI: 8 (13×0.6 = 7.8 → 8), VIT: 17 (13×1.3 = 16.9 → 17), MAG: 10 (13×0.7 = 9.1 → 10)
  - XP Yield: 154, JP: 19, SP: 21, Gold: 39
- **Elemental Properties**: 30% resist Earth, 30% weak to Wind
- **Spawn Pool**: Pool 4, Pool 5

---

#### 25. Molten Titan ⭐ NEW (BOSS)
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 20
- **Spawn Range**: Pit 20 only (Small Boss)
- **Boss Type**: Small Boss
- **Archetype**: Tank
- **Element**: Fire
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Massive fire giant with molten veins. Volcanic rock skin cracked with lava flows, radiates intense heat.
- **Stats @ Level 22** (Pit 20 boss gets +2 level bonus):
  - HP: 268 (10 + 22×5 = 120, ×1.5 = 180, boss modifier ×1.5 ≈ 268)
  - STR: 13 (15×0.8 = 12 → 13), AGI: 9 (15×0.6 = 9), VIT: 20 (15×1.3 = 19.5 → 20), MAG: 11 (15×0.7 = 10.5 → 11)
  - XP Yield: 258 (×1.5 for boss), JP: 23, SP: 25, Gold: 75
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 4 (Boss floor only)
- **Design Notes**: Late-game boss showcasing high-level Tank power

---

### Pool 5: Abyssal Cave (Pit Levels 21-25)

---

#### 26. Ancient Wyrm ⭐ NEW (BIG BOSS)
- **Status**: `DESIGN`
- **Biome(s)**: Cave
- **Pit Level**: 25
- **Spawn Range**: Pit 25 only (Big Boss)
- **Boss Type**: Big Boss (Final Cave Boss)
- **Archetype**: Tank
- **Element**: Fire
- **Damage Kind**: Physical
- **Visual Size**: Large (64×64)
- **Description**: Ancient dragon slumbering in the deepest caves. Massive form with obsidian scales, molten gold eyes, wreathed in flames. Final challenge of the Cave Biome.
- **Stats @ Level 27** (Pit 25 boss gets +2 level bonus):
  - HP: 338 (10 + 27×5 = 145, ×1.5 = 217.5, big boss modifier ×1.55 ≈ 338)
  - STR: 16 (18×0.8 = 14.4 → 16), AGI: 11 (18×0.6 = 10.8 → 11), VIT: 24 (18×1.3 = 23.4 → 24), MAG: 13 (18×0.7 = 12.6 → 13)
  - XP Yield: 326 (×1.5 for big boss), JP: 28, SP: 30, Gold: 125
- **Elemental Properties**: 30% resist Fire, 30% weak to Water
- **Spawn Pool**: Pool 5 (Boss floor only)
- **Design Notes**: Capstone encounter for Cave Biome, significantly more dangerous than previous bosses

---

## Summary Statistics

### Monster Distribution

**By Status:**
- IMPLEMENTED: 10 monsters
- DESIGN (New): 15 monsters

**By Archetype:**
- Balanced: 9 monsters (36%)
- Tank: 10 monsters (40%)
- FastFragile: 7 monsters (28%)
- MagicUser: 4 monsters (16%)

**By Element:**
- Neutral: 1 monster (4%)
- Fire: 7 monsters (28%)
- Water: 1 monster (4%)
- Earth: 11 monsters (44%)
- Wind: 1 monster (4%)
- Dark: 6 monsters (24%)
- Light: 0 monsters (0%)

**By Boss Type:**
- Regular: 20 monsters (80%)
- Small Boss: 4 monsters (16%)
- Big Boss: 1 monster (4%)

**By Damage Kind:**
- Physical: 21 monsters (84%)
- Magic: 4 monsters (16%)

### Level Progression

| Pit Level Range | Monster Count | Boss Count |
|----------------|---------------|------------|
| 1-5            | 6 regular     | 1 small    |
| 6-10           | 6 regular     | 1 small    |
| 11-15          | 7 regular     | 1 small    |
| 16-20          | 4 regular     | 1 small    |
| 21-25          | 1 regular     | 1 big      |
| **Total**      | **24 regular**| **5 bosses**|

---

## Implementation Priority

### Phase 1: Boss Monsters (Critical Path)
1. Stone Guardian (Pit 5 boss)
2. Earth Elemental (Pit 15 boss)
3. Molten Titan (Pit 20 boss)
4. Ancient Wyrm (Pit 25 big boss)

### Phase 2: Pool Fillers (High Priority)
5. Cave Mushroom (early game variety)
6. Stone Beetle (early tank)
7. Shadow Imp (mid-game speed)
8. Tunnel Worm (mid-game balanced)
9. Fire Lizard (mid-game fire)

### Phase 3: Late Game Content (Medium Priority)
10. Magma Ooze (first magic damage)
11. Crystal Golem (late tank)
12. Cave Troll (late tank)
13. Ghost Miner (late magic)

### Phase 4: End Game Content (Lower Priority)
14. Shadow Beast (end-game speed)
15. Lava Drake (end-game magic)
16. Stone Wyrm (end-game tank)

---

## Design Principles

### Cave Biome Theme
- **Elements**: Predominantly Earth (44%) and Dark (24%), with Fire (28%) representing volcanic activity
- **Archetypes**: Emphasis on Tank (40%) reflects the rugged, defensive nature of cave creatures
- **Progression**: Steady difficulty curve from level 1 slimes to level 27 wyrm
- **Visual**: Dark, rocky environments with bioluminescence and lava features

### Balance Considerations
- Boss encounters every 5 levels provide difficulty spikes
- Sliding spawn window prevents early enemies from becoming trivial
- Element variety encourages strategic team building
- Mix of physical (84%) and magic (16%) damage keeps combat varied
- FastFragile archetype (28%) provides speed challenges alongside Tank encounters

### Spawn Pool Design
- Each 5-level pool contains 10 monsters (excluding boss)
- Pools overlap to ensure smooth difficulty transitions
- Boss floors spawn only the boss monster
- Higher-level monsters appear in multiple pools as "carry-over" threats

---

## Future Expansion Notes

### Potential Additions
- Light-element monsters for elemental balance (currently 0%)
- More MagicUser archetypes for late-game variety (currently only 16%)
- Elite variants of existing monsters
- Rare/special encounters outside normal pools

### Cross-Biome Considerations
- Some Cave monsters could appear in other biomes (e.g., Bat, Rat)
- Boss designs could be adapted for other biomes
- Element distribution should remain biome-specific

---

**End of Cave Biome Monster Library**

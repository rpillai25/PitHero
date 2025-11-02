# Monster Balance Guide

## Introduction

This guide provides comprehensive guidelines for creating new monsters in PitHero with proper balance. All formulas and configurations are centralized in `BalanceConfig.cs`, making it easy to adjust balance during playtesting.

**Balance Philosophy:**
- Linear progression in early levels (1-10) with exponential scaling later
- Monster stats should challenge players but remain beatable at appropriate levels
- Elemental matchups should encourage strategic team composition
- Different archetypes provide varied combat encounters

---

## Monster Stat Calculation Formulas

All monster stats are calculated using formulas in `BalanceConfig.cs` that take into account the monster's level and archetype.

### HP Calculation

**Formula:** `(10 + level * 5) * archetype_hp_multiplier`

**Purpose:** Determines how much damage a monster can take before being defeated.

**Example Values:**

| Level | Balanced | Tank (1.5x) | FastFragile (0.7x) | MagicUser (0.9x) |
|-------|----------|-------------|-------------------|------------------|
| 1     | 15       | 23          | 11                | 14               |
| 10    | 60       | 90          | 42                | 54               |
| 25    | 135      | 203         | 95                | 122              |
| 50    | 260      | 390         | 182               | 234              |
| 75    | 385      | 578         | 270               | 347              |
| 99    | 505      | 758         | 354               | 455              |

**Tuning Advice:** If monsters die too quickly, increase the base value (10) or the level multiplier (5).

---

### Primary Stats (STR, AGI, VIT, MAG)

**Formula:** `(1 + level * 2/3) * archetype_stat_multiplier`

**Purpose:** Determines combat effectiveness - attack power, speed, defense, and magical ability.

**Base Stat Values (Balanced Archetype):**

| Level | Stat Value |
|-------|------------|
| 1     | 1          |
| 10    | 7          |
| 20    | 14         |
| 30    | 21         |
| 40    | 27         |
| 50    | 34         |
| 60    | 41         |
| 75    | 51         |
| 99    | 67         |

**Archetype Stat Modifiers:**

| Archetype   | STR  | AGI  | VIT  | MAG  |
|-------------|------|------|------|------|
| Balanced    | 1.0x | 1.0x | 1.0x | 1.0x |
| Tank        | 0.8x | 0.6x | 1.3x | 0.7x |
| FastFragile | 1.2x | 1.5x | 0.6x | 0.9x |
| MagicUser   | 0.7x | 0.8x | 0.8x | 1.5x |

**Example: Level 50 Monster Stats**

| Archetype   | STR | AGI | VIT | MAG |
|-------------|-----|-----|-----|-----|
| Balanced    | 34  | 34  | 34  | 34  |
| Tank        | 27  | 20  | 44  | 24  |
| FastFragile | 41  | 51  | 20  | 31  |
| MagicUser   | 24  | 27  | 27  | 51  |

**Tuning Advice:** If monster damage/defense feels off, adjust the level multiplier (2/3).

---

### Experience Yield

**Formula:** `10 + level * 8`

**Purpose:** Determines how much experience players gain from defeating the monster.

**Example Values:**

| Level | Experience Yield |
|-------|------------------|
| 1     | 18               |
| 5     | 50               |
| 10    | 90               |
| 25    | 210              |
| 50    | 410              |
| 75    | 610              |
| 99    | 802              |

**Tuning Advice:** Players should need to fight 5-8 monsters per level in early game. Adjust the multiplier (8) if leveling feels too fast/slow.

---

## Monster Archetype System

### Overview

Monsters are classified into four primary archetypes that define their stat distribution and combat role. Each archetype has distinct strengths and weaknesses.

### 1. Balanced Archetype

**Multipliers:** HP 1.0x, STR 1.0x, AGI 1.0x, VIT 1.0x, MAG 1.0x

**Description:** All-around monster with no particular strength or weakness. Good for general encounters.

**Combat Style:** Moderate damage, moderate durability, moderate speed.

**When to Use:**
- Generic enemies for mixed combat encounters
- Early game enemies to introduce mechanics
- Encounters where no specific strategy is required

**Example Monsters:**
- Goblin (Level 3, Earth element)
- Slime (Level 1, Water element)

---

### 2. Tank Archetype

**Multipliers:** HP 1.5x, STR 0.8x, AGI 0.6x, VIT 1.3x, MAG 0.7x

**Description:** High HP and defense, but slow and deals moderate damage. Forces longer battles.

**Combat Style:** Damage sponge, outlasts opponents, low evasion.

**Strengths:**
- High survivability (50% more HP, 30% more VIT)
- Difficult to burst down quickly
- Forces resource management

**Weaknesses:**
- Low speed (40% less AGI) - attacks last, easy to outspeed
- Moderate damage output (20% less STR)
- Vulnerable to percentage-based damage

**When to Use:**
- Boss encounters or mini-bosses
- Encounters designed to test endurance
- Mixed groups to protect faster/fragile enemies
- Late-game encounters where players have high burst damage

**Example Monsters:**
- Orc (Level 6, Fire element, Tank)

---

### 3. FastFragile Archetype

**Multipliers:** HP 0.7x, STR 1.2x, AGI 1.5x, VIT 0.6x, MAG 0.9x

**Description:** High speed and attack power, but low HP and defense. Hit-and-run specialist.

**Combat Style:** Strikes first, hits hard, but dies quickly if hit.

**Strengths:**
- Very high speed (50% more AGI) - almost always attacks first
- High damage output (20% more STR)
- High evasion chance

**Weaknesses:**
- Low survivability (30% less HP, 40% less VIT)
- Dies quickly to AoE attacks
- Poor at sustained combat

**When to Use:**
- Swarm encounters with multiple enemies
- Speed-based challenges
- Encounters that reward tactical CC/crowd control
- Mixed groups to add unpredictability
- Ambush scenarios

**Example Monsters:**
- Bat (Level 1, Wind element, FastFragile)
- Wraith (Level 6, Dark element, FastFragile)

---

### 4. MagicUser Archetype

**Multipliers:** HP 0.9x, STR 0.7x, AGI 0.8x, VIT 0.8x, MAG 1.5x

**Description:** High magical power with moderate durability. Specializes in magical attacks.

**Combat Style:** Magic-based damage dealer with some survivability.

**Strengths:**
- Very high magic power (50% more MAG)
- Can exploit elemental weaknesses
- Balanced survivability (only 10% less HP)

**Weaknesses:**
- Low physical attack (30% less STR)
- Below-average speed (20% less AGI)
- Vulnerable to magic-resistant enemies

**When to Use:**
- Encounters emphasizing elemental strategy
- Mixed physical/magical combat
- Boss battles with elemental phases
- Encounters with specific elemental themes

**Example Monsters:**
- Wizard/Sorcerer types
- Elemental creatures
- Magical beasts

---

## Elemental Distribution Guidelines

### Elemental System Overview

The game uses seven element types: **Neutral, Fire, Water, Earth, Wind, Light, Dark**

**Core Matchup Rules:**
- **Same element:** 0.5x damage (resistance)
- **Opposing element:** 2.0x damage (weakness)
- **No relationship:** 1.0x damage (neutral)

**Opposing Pairs:**
- Fire ↔ Water
- Earth ↔ Wind
- Light ↔ Dark
- Neutral (no opposites)

---

### Standard Elemental Resistance Pattern

Most monsters follow this pattern:
- **30% resistance** to their own element
- **30% weakness** to their opposing element

**Implementation Example:**
```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 0.3f },    // 30% resistance to Fire
    { ElementType.Water, -0.3f }   // 30% weakness to Water
};
ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
```

---

### Elemental Distribution by Archetype

**Balanced Archetype:**
- Any element works well
- Most common: Earth, Water, Neutral
- Good for learning encounters

**Tank Archetype:**
- Prefer: Earth (thematic fit with high defense)
- Also good: Fire (aggressive tank), Water (defensive tank)
- Avoid: Wind (conflicts with low speed concept)

**FastFragile Archetype:**
- Prefer: Wind (thematic fit with high speed), Dark (evasive)
- Also good: Light (swift striker)
- Avoid: Earth (conflicts with high speed concept)

**MagicUser Archetype:**
- Any element works, but these are thematic:
- Fire: offensive caster
- Water: healing/support caster
- Light: holy caster
- Dark: necromancer/shadow caster

---

### Advanced Elemental Customization

You can create custom resistance patterns for unique monsters:

**Heavy Fire Resistance Example:**
```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 0.7f },    // 70% resistance to Fire
    { ElementType.Water, -0.5f }   // 50% weakness to Water
};
```

**Multi-Element Resistance Example:**
```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 0.3f },
    { ElementType.Water, 0.3f },
    { ElementType.Light, -0.4f }   // 40% weakness to Light only
};
```

---

## Monster Examples by Level Range

### Low-Level Monsters (Levels 1-10)

**Purpose:** Introduce players to combat mechanics, elemental system, and archetypes.

#### Example 1: Slime (Level 1, Balanced, Water)

```csharp
public Slime(int level = 1)
{
    Level = 1;
    var archetype = BalanceConfig.MonsterArchetype.Balanced;
    
    // Stats: HP=15, STR=1, AGI=1, VIT=1, MAG=1
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 1
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 1
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 1
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 1
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 15
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 18
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Water, 0.3f },
        { ElementType.Fire, -0.3f }
    };
    ElementalProps = new ElementalProperties(ElementType.Water, resistances);
}
```

**Design Notes:**
- Weakest monster, perfect for tutorial
- Low damage output, easy to defeat
- Teaches elemental weakness (Fire attacks are strong)

---

#### Example 2: Bat (Level 1, FastFragile, Wind)

```csharp
public Bat(int level = 1)
{
    Level = 1;
    var archetype = BalanceConfig.MonsterArchetype.FastFragile;
    
    // Stats: HP=11, STR=1, AGI=2, VIT=1, MAG=1
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 1 (1*1.2=1.2→1)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 2 (1*1.5=1.5→2)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 1 (1*0.6=0.6→1)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 1 (1*0.9=0.9→1)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 11
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 18
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Wind, 0.3f },
        { ElementType.Earth, -0.3f }
    };
    ElementalProps = new ElementalProperties(ElementType.Wind, resistances);
}
```

**Design Notes:**
- Introduces speed mechanic (attacks first)
- Dies in 1-2 hits but can dodge
- Teaches importance of accuracy and speed

---

#### Example 3: Goblin (Level 3, Balanced, Earth)

```csharp
public Goblin(int level = 3)
{
    Level = 3;
    var archetype = BalanceConfig.MonsterArchetype.Balanced;
    
    // Stats: HP=25, STR=3, AGI=3, VIT=3, MAG=3
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 3
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 3
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 3
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 3
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 25
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 34
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Earth, 0.3f },
        { ElementType.Wind, -0.3f }
    };
    ElementalProps = new ElementalProperties(ElementType.Earth, resistances);
}
```

**Design Notes:**
- Standard early-game enemy
- Balanced stats provide fair challenge
- Good for teaching basic combat flow

---

### Mid-Level Monsters (Levels 20-40)

**Purpose:** Provide meaningful challenge, require strategic element usage, test team composition.

#### Example 4: Iron Golem (Level 25, Tank, Earth)

```csharp
public IronGolem(int level = 25)
{
    Level = 25;
    var archetype = BalanceConfig.MonsterArchetype.Tank;
    
    // Stats: HP=203, STR=11, AGI=8, VIT=18, MAG=10
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 11 (14*0.8)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 8  (14*0.6)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 18 (14*1.3)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 10 (14*0.7)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 203
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 210
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Earth, 0.5f },    // 50% resistance (very tanky)
        { ElementType.Wind, -0.3f },
        { ElementType.Physical, 0.2f }  // Extra physical resistance
    };
    ElementalProps = new ElementalProperties(ElementType.Earth, resistances);
}
```

**Design Notes:**
- Mini-boss encounter, tests player endurance
- High defense requires magic or elemental advantage
- Slow speed allows multiple player attacks

---

#### Example 5: Shadow Assassin (Level 30, FastFragile, Dark)

```csharp
public ShadowAssassin(int level = 30)
{
    Level = 30;
    var archetype = BalanceConfig.MonsterArchetype.FastFragile;
    
    // Stats: HP=95, STR=26, AGI=32, VIT=13, MAG=19
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 26 (21*1.2)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 32 (21*1.5)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 13 (21*0.6)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 19 (21*0.9)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 95
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 250
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Dark, 0.4f },
        { ElementType.Light, -0.4f }
    };
    ElementalProps = new ElementalProperties(ElementType.Dark, resistances);
}
```

**Design Notes:**
- Dangerous if not focused down quickly
- High evasion makes landing hits challenging
- Rewards AoE attacks or guaranteed-hit abilities

---

#### Example 6: Fire Mage (Level 35, MagicUser, Fire)

```csharp
public FireMage(int level = 35)
{
    Level = 35;
    var archetype = BalanceConfig.MonsterArchetype.MagicUser;
    
    // Stats: HP=171, STR=17, AGI=19, VIT=19, MAG=36
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 17 (24*0.7)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 19 (24*0.8)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 19 (24*0.8)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 36 (24*1.5)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 171
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 290
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Fire, 0.4f },
        { ElementType.Water, -0.4f }
    };
    ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
}
```

**Design Notes:**
- Heavy magic damage dealer
- Requires Water-element heroes or magic defense
- Moderate survivability makes it a priority target

---

### High-Level Monsters (Levels 60-99)

**Purpose:** Challenge experienced players, require optimized builds, endgame content.

#### Example 7: Ancient Dragon (Level 75, Tank, Fire)

```csharp
public AncientDragon(int level = 75)
{
    Level = 75;
    var archetype = BalanceConfig.MonsterArchetype.Tank;
    
    // Stats: HP=578, STR=41, AGI=31, VIT=66, MAG=36
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 41 (51*0.8)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 31 (51*0.6)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 66 (51*1.3)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 36 (51*0.7)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 578
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 610
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Fire, 0.6f },      // 60% Fire resistance
        { ElementType.Water, -0.3f },
        { ElementType.Physical, 0.3f }   // 30% Physical resistance
    };
    ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
}
```

**Design Notes:**
- Epic boss encounter
- Requires full team coordination
- Long battle tests resource management
- Multiple damage types needed for efficiency

---

#### Example 8: Void Reaper (Level 85, FastFragile, Dark)

```csharp
public VoidReaper(int level = 85)
{
    Level = 85;
    var archetype = BalanceConfig.MonsterArchetype.FastFragile;
    
    // Stats: HP=304, STR=69, AGI=86, VIT=34, MAG=52
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 69 (57*1.2)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 86 (57*1.5)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 34 (57*0.6)
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 52 (57*0.9)
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 304
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 690
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Dark, 0.5f },
        { ElementType.Light, -0.5f }
    };
    ElementalProps = new ElementalProperties(ElementType.Dark, resistances);
}
```

**Design Notes:**
- Glass cannon at high level
- Can one-shot unprepared players
- High evasion makes it frustrating without accuracy buffs
- Rewards CC and burst damage

---

#### Example 9: Pit Lord (Level 99, Balanced, Dark)

```csharp
public PitLord(int level = 99)
{
    Level = 99;
    var archetype = BalanceConfig.MonsterArchetype.Balanced;
    
    // Stats: HP=505, STR=67, AGI=67, VIT=67, MAG=67
    Stats = new StatBlock(
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength),   // 67
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility),    // 67
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality),   // 67
        BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic)       // 67
    );
    
    MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);  // 505
    ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);  // 802
    
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Dark, 0.5f },
        { ElementType.Light, -0.4f },
        { ElementType.Fire, 0.2f },
        { ElementType.Water, 0.2f },
        { ElementType.Earth, 0.2f },
        { ElementType.Wind, 0.2f }
    };
    ElementalProps = new ElementalProperties(ElementType.Dark, resistances);
}
```

**Design Notes:**
- Final boss or ultimate challenge
- Well-rounded stats make no approach overpowered
- Multiple resistances require diverse team
- Light element is the key to victory

---

## Creating New Monsters: Step-by-Step Guide

### Step 1: Determine Level and Role

**Questions to ask:**
- What pit level/depth should this monster appear in?
- Is this a common enemy, elite, or boss?
- What challenge should this monster present?

**Level Guidelines:**
- Pit 1-10: Levels 1-15 (beginner)
- Pit 11-20: Levels 16-35 (intermediate)
- Pit 21-40: Levels 36-70 (advanced)
- Pit 41+: Levels 71-99 (expert)

Use `BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel)` for guidance.

---

### Step 2: Choose Archetype

**Selection Criteria:**

**Choose Balanced if:**
- General-purpose enemy
- No specific combat gimmick
- Flexible encounter design

**Choose Tank if:**
- Boss or mini-boss
- Need to increase encounter length
- Want to test player endurance

**Choose FastFragile if:**
- Speed-based challenge
- Swarm encounter
- Wants to create urgency (must kill quickly)

**Choose MagicUser if:**
- Elemental-themed encounter
- Magic damage challenge
- Want to emphasize elemental strategy

---

### Step 3: Select Element

**Element Selection Guidelines:**

**Consider:**
- Thematic fit (Fire for dragons, Water for sea creatures)
- Dungeon/zone theme (Fire zone = Fire enemies)
- Balance in enemy roster (spread elements evenly)
- Strategic depth (create interesting team-building choices)

**Best Practices:**
- Don't use the same element for consecutive encounters
- Mix elements in multi-enemy battles
- Neutral element is rarely interesting (use sparingly)

---

### Step 4: Implement the Monster Class

**Code Template:**

```csharp
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Short description of the monster's role.</summary>
    public sealed class YourMonster : IEnemy
    {
        private int _hp;

        public string Name => "YourMonster";
        public int Level { get; }
        public StatBlock Stats { get; }
        public DamageKind AttackKind => DamageKind.Physical; // or DamageKind.Magic
        public ElementType Element => ElementType.Fire; // Choose appropriate element
        public ElementalProperties ElementalProps { get; }
        public int MaxHP { get; }
        public int CurrentHP => _hp;
        public int ExperienceYield { get; }

        public YourMonster(int level = 25) // Set default level
        {
            // Use preset level if configured, or use passed level
            Level = level;
            
            // Choose archetype
            var archetype = BalanceConfig.MonsterArchetype.Balanced;
            
            // Calculate stats using BalanceConfig
            var strength = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Strength);
            var agility = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Agility);
            var vitality = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Vitality);
            var magic = BalanceConfig.CalculateMonsterStat(Level, archetype, BalanceConfig.StatType.Magic);
            
            Stats = new StatBlock(strength, agility, vitality, magic);
            MaxHP = BalanceConfig.CalculateMonsterHP(Level, archetype);
            _hp = MaxHP;
            ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);
            
            // Set up elemental resistances
            var resistances = new Dictionary<ElementType, float>
            {
                { ElementType.Fire, 0.3f },   // Resist own element
                { ElementType.Water, -0.3f }  // Weak to opposing element
            };
            ElementalProps = new ElementalProperties(ElementType.Fire, resistances);
        }

        /// <summary>Inflicts damage, returns true if died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            _hp -= amount;
            if (_hp < 0) _hp = 0;
            return _hp == 0;
        }
    }
}
```

---

### Step 5: Test and Tune

**Testing Checklist:**
- [ ] Monster spawns at correct level
- [ ] Stats feel appropriate for level
- [ ] Combat length is reasonable (not too quick/slow)
- [ ] Elemental weaknesses work correctly
- [ ] Experience reward feels fair
- [ ] Monster fits thematically in its area

**Common Issues:**

**Monster dies too quickly:**
- Switch to Tank archetype
- Increase level
- Add physical resistance

**Monster takes too long to defeat:**
- Switch to FastFragile archetype
- Decrease level
- Reduce resistances

**Combat is boring:**
- Change archetype for variety
- Add unique elemental resistances
- Adjust AGI for speed differences

---

## Best Practices and Guidelines

### 1. Archetype Distribution

**In a typical dungeon floor:**
- 50-60% Balanced enemies (common encounters)
- 20-30% FastFragile enemies (speed challenges)
- 10-15% Tank enemies (mini-bosses, tough encounters)
- 5-10% MagicUser enemies (special encounters)

### 2. Elemental Variety

**Aim for:**
- At least 2-3 different elements per dungeon floor
- Mix elements in multi-enemy encounters
- Save rare elements (Light/Dark) for special encounters

**Avoid:**
- Single-element dungeons (boring, one strategy wins)
- Random element distribution (feels incoherent)

### 3. Level Progression

**Smooth Difficulty Curve:**
- Early pit levels: Level ~= Pit Level * 1.5
- Mid pit levels: Level ~= Pit Level * 2
- Late pit levels: Level increases more slowly

**Example:**
- Pit 5 → Level 7-8 enemies
- Pit 10 → Level 15-16 enemies
- Pit 20 → Level 35-40 enemies
- Pit 50 → Level 75-80 enemies

### 4. Boss Design

**Boss Characteristics:**
- Usually Tank or Balanced archetype
- Level = Estimated Player Level + 2-5
- Multiple elemental resistances
- Higher-than-normal experience yield (consider 1.5-2x)

**Example Boss:**
```csharp
public FloorBoss(int level = 30)
{
    Level = level;
    var archetype = BalanceConfig.MonsterArchetype.Tank;
    // ... stats calculation ...
    
    // Boss has broader resistances
    var resistances = new Dictionary<ElementType, float>
    {
        { ElementType.Fire, 0.3f },
        { ElementType.Water, 0.3f },
        { ElementType.Light, -0.4f }  // Clear weakness
    };
    
    // Bonus experience (150% of normal)
    ExperienceYield = (int)(BalanceConfig.CalculateMonsterExperience(Level) * 1.5f);
}
```

### 5. Encounter Design

**Single Enemy:**
- Use Balanced or Tank archetype
- Level should match or slightly exceed player level

**Small Group (2-3 enemies):**
- Mix archetypes (e.g., 1 Tank + 2 FastFragile)
- Mix elements for strategic depth
- Individual enemies 1-2 levels below player

**Large Group (4+ enemies):**
- Primarily FastFragile or Balanced
- Individual enemies 2-3 levels below player
- Same element is OK for swarms

---

## Advanced Topics

### Custom Archetype Combinations

You can manually set stats to create hybrid archetypes:

```csharp
// Bruiser: High HP and Strength, low other stats
var strength = (int)(BalanceConfig.CalculateMonsterStat(Level, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Strength) * 1.3f);
var hp = (int)(BalanceConfig.CalculateMonsterHP(Level, BalanceConfig.MonsterArchetype.Balanced) * 1.2f);
var agility = (int)(BalanceConfig.CalculateMonsterStat(Level, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Agility) * 0.7f);
```

### Elemental Immunity

For special monsters immune to their element:

```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Fire, 1.0f }  // 100% resistance = immunity (0 damage)
};
```

### Elemental Absorption (Healing)

For monsters that heal from their element:

```csharp
var resistances = new Dictionary<ElementType, float>
{
    { ElementType.Dark, 1.5f }  // Absorb = takes negative damage (heals)
};
```

*Note: May require custom implementation in damage calculation.*

---

## Formula Reference

### Quick Reference Table

| Formula | Purpose | Example (Level 50) |
|---------|---------|-------------------|
| HP = (10 + L * 5) * HP_mult | Monster HP | 260 (Balanced) |
| Stat = (1 + L * 2/3) * Stat_mult | Monster stats | 34 (Balanced) |
| XP = 10 + L * 8 | Experience yield | 410 |
| Dmg = A >= D ? A*2 - D : A*A/D | Attack damage | Varies |
| Evasion = min(255, AGI*2 + L) | Evasion value | 130 (AGI=40) |

### Code Reference

All formulas implemented in: `/PitHero/RolePlayingFramework/Balance/BalanceConfig.cs`

Key methods:
- `BalanceConfig.CalculateMonsterHP(level, archetype)`
- `BalanceConfig.CalculateMonsterStat(level, archetype, statType)`
- `BalanceConfig.CalculateMonsterExperience(level)`
- `BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps)`

---

## Conclusion

This guide provides a comprehensive framework for creating balanced monsters in PitHero. By following these formulas and guidelines, you can create diverse, challenging, and fair combat encounters.

**Key Takeaways:**
- Use `BalanceConfig` methods for all stat calculations
- Choose archetypes based on desired combat style
- Distribute elements strategically across encounters
- Test and iterate based on actual gameplay
- Refer to existing monsters for examples

**Remember:** Balance is iterative. Use the formulas as a starting point, then adjust based on playtesting feedback. The centralized `BalanceConfig` makes tuning easy!

---

**Related Documentation:**
- `BalanceConfig.cs` - All balance formulas and constants
- `IEnemy.cs` - Enemy interface definition
- Existing enemy implementations in `/PitHero/RolePlayingFramework/Enemies/`

**For Questions or Balance Feedback:**
- File an issue on GitHub
- Reference this guide when discussing balance changes
- Suggest formula adjustments based on playtesting data

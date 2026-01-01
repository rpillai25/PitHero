# Job Point (JP) System Implementation

## Overview
This document describes the implementation of the Job Point (JP) system for Primary Jobs in PitHero, enabling skill learning and job progression.

## Core Components

### 1. Skill JP Costs
All skills now have a `JPCost` property that defines how much JP is required to learn them:

- **ISkill Interface**: Added `int JPCost { get; }` property
- **BaseSkill**: Constructor now accepts `jpCost` parameter
- All existing and new skills updated with JP costs per specification

### 2. HeroCrystal JP System
The `HeroCrystal` class has been enhanced with JP tracking:

#### Properties
- `TotalJP`: Total JP earned across all time (never decreases)
- `CurrentJP`: Available JP to spend (decreases when skills are purchased)
- `JobLevel`: Computed property based on highest learn level of purchased skills

#### Methods
- `EarnJP(int amount)`: Adds JP from battles, chests, events, or quests
- `TryPurchaseSkill(ISkill skill)`: Attempts to purchase a skill, validating:
  - Sufficient CurrentJP
  - Hero level meets skill's LearnLevel requirement
  - Skill not already learned
- `IsJobMastered()`: Returns true when all job skills are learned
- `CalculateJobLevel()`: Internal method computing job level (1-3 based on purchased skills)

### 3. Hero JP Integration
The `Hero` class now supports JP mechanics through its bound crystal:

#### Methods
- `EarnJP(int amount)`: Proxy to crystal's EarnJP
- `TryPurchaseSkill(ISkill skill)`: Purchases skill via crystal and applies to hero
- `GetCurrentJP()`: Returns current JP from crystal
- `GetTotalJP()`: Returns total JP earned
- `GetJobLevel()`: Returns computed job level
- `IsJobMastered()`: Returns mastery status

## Primary Jobs

### Knight
- **Base Stats**: STR 4, AGI 0, VIT 3, MAG 0
- **Growth/Level**: STR +2, AGI +0, VIT +2, MAG +0
- **Skills**:
  1. Light Armor (Passive, JP: 50, Level: 1) - Can equip robes
  2. Heavy Armor (Passive, JP: 100, Level: 2) - +2 passive defense
  3. Spin Slash (Active, JP: 120, Level: 2) - AP: 4, 80% damage to surrounding enemies
  4. Heavy Strike (Active, JP: 180, Level: 3) - AP: 5, bonus STR damage

### Mage
- **Base Stats**: STR 0, AGI 0, VIT 0, MAG 5
- **Growth/Level**: STR +0, AGI +1, VIT +0, MAG +3
- **Skills**:
  1. Heart of Fire (Passive, JP: 60, Level: 1) - +25% fire damage
  2. Economist (Passive, JP: 80, Level: 2) - -15% AP costs
  3. Fire (Active, JP: 120, Level: 2) - AP: 3, magic damage + fire bonus
  4. Firestorm (Active, JP: 200, Level: 3) - AP: 6, fire AoE

### Priest
- **Base Stats**: STR 0, AGI 0, VIT 2, MAG 3
- **Growth/Level**: STR +0, AGI +1, VIT +1, MAG +2
- **Skills**:
  1. Calm Spirit (Passive, JP: 50, Level: 1) - +1 AP/tick regen
  2. Mender (Passive, JP: 80, Level: 2) - +25% healing
  3. Heal (Active, JP: 100, Level: 2) - AP: 3, restores HP
  4. Defense Up (Active, JP: 160, Level: 3) - AP: 4, +1 defense buff

### Monk
- **Base Stats**: STR 3, AGI 1, VIT 3, MAG 0
- **Growth/Level**: STR +2, AGI +1, VIT +2, MAG +0
- **Skills**:
  1. Counter (Passive, JP: 70, Level: 2) - Counterattack when hit
  2. Deflect (Passive, JP: 90, Level: 3) - 15% deflect chance
  3. Roundhouse (Active, JP: 120, Level: 2) - AP: 4, physical damage to surrounding
  4. Flaming Fist (Active, JP: 170, Level: 3) - AP: 5, physical + magic damage

### Thief (New)
- **Base Stats**: STR 2, AGI 3, VIT 1, MAG 0
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +0
- **Skills**:
  1. Shadowstep (Passive, JP: 70, Level: 1) - Evasion chance (placeholder)
  2. Trap Sense (Passive, JP: 90, Level: 2) - Detect/disarm traps (placeholder)
  3. Sneak Attack (Active, JP: 130, Level: 2) - AP: 3, bonus AGI damage
  4. Vanish (Active, JP: 180, Level: 3) - AP: 6, untargetable for 1 turn (placeholder)

### Archer (New)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 1
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +1
- **Skills**:
  1. Eagle Eye (Passive, JP: 70, Level: 1) - +1 sight distance (placeholder)
  2. Quickdraw (Passive, JP: 100, Level: 2) - First attack crits (placeholder)
  3. Power Shot (Active, JP: 130, Level: 2) - AP: 4, 1.5x damage
  4. Volley (Active, JP: 200, Level: 3) - AP: 7, ranged AoE

## JP Progression Flow

1. **Earn JP**: Heroes earn JP through battles, chests, events, or quests
2. **Check Requirements**: To purchase a skill, hero must:
   - Have sufficient CurrentJP to pay the skill's JPCost
   - Meet the hero level requirement (Level >= skill.LearnLevel)
   - Not already own the skill
3. **Purchase Skill**: When purchased:
   - CurrentJP is reduced by skill's JPCost
   - TotalJP remains unchanged (lifetime tracking)
   - Skill is added to learned skills
   - Passive skills are immediately applied
4. **Job Level**: Automatically computed as the highest LearnLevel of any purchased skill
5. **Job Mastery**: Achieved when all 4 skills of a job are purchased (star indicator can be shown in UI)

## Testing

Comprehensive test suites have been created:

- **JobPointSystemTests.cs**: Core JP system functionality (29 tests)
  - JP earning and tracking
  - Skill purchasing validation
  - Job level calculation
  - Job mastery detection
  - Skill JP cost verification for all jobs
  - Crystal combination with JP

- **HeroJPIntegrationTests.cs**: Hero-Crystal integration (13 tests)
  - Hero JP operations through crystal
  - Skill purchase through hero
  - Passive skill application
  - Crystal sharing between heroes
  - New jobs (Thief/Bowman) integration

- **CompleteJPWorkflowTests.cs**: End-to-end workflows (5 tests)
  - Complete Knight progression
  - Combined crystal multi-job progression
  - Thief complete progression
  - Bowman complete progression
  - All primary jobs verification

## Job System Architecture

The system is designed around primary jobs with the following architecture:
- CompositeJob exists for combining job stats and skills (used internally)
- HeroCrystal.Combine() merges JP pools and skill sets
- Hero can preload skills from composite jobs via crystal
- Job level is per-crystal, enabling independent progression tracking

**Note**: Secondary and tertiary jobs have been removed as part of the job-to-synergy system transition. The CompositeJob infrastructure remains for potential future use with the synergy system.

## Notes on Placeholders

Some skills have placeholder implementations:
- Thief: Shadowstep (evasion), Trap Sense (trap mechanics), Vanish (untargetable status)
- Bowman: Eagle Eye (sight distance), Quickdraw (crit mechanics)

These are intentionally minimal to focus on the JP system infrastructure. Full implementations can be added as game mechanics evolve.

## Files Modified/Created

### Modified
- `RolePlayingFramework/Skills/ISkill.cs` - Added JPCost property
- `RolePlayingFramework/Skills/BaseSkill.cs` - Added JPCost to constructor
- `RolePlayingFramework/Skills/KnightSkills.cs` - Added JP costs
- `RolePlayingFramework/Skills/MageSkills.cs` - Added JP costs
- `RolePlayingFramework/Skills/PriestSkills.cs` - Added JP costs
- `RolePlayingFramework/Skills/MonkSkills.cs` - Added JP costs
- `RolePlayingFramework/Heroes/HeroCrystal.cs` - Added JP system
- `RolePlayingFramework/Heroes/Hero.cs` - Added JP integration methods

### Created
- `RolePlayingFramework/Jobs/Thief.cs` - New Thief job
- `RolePlayingFramework/Jobs/Bowman.cs` - New Bowman job
- `RolePlayingFramework/Skills/ThiefSkills.cs` - Thief skill implementations
- `RolePlayingFramework/Skills/BowmanSkills.cs` - Bowman skill implementations
- `PitHero.Tests/JobPointSystemTests.cs` - Core JP tests
- `PitHero.Tests/HeroJPIntegrationTests.cs` - Integration tests
- `PitHero.Tests/CompleteJPWorkflowTests.cs` - Workflow tests

# Secondary Job System Implementation

## Overview
This document describes the implementation of the Secondary Job System for PitHero, which allows combining two primary jobs to create hybrid classes with unique skills and stat combinations.

## Secondary Jobs Implemented

All 15 secondary jobs have been implemented with their specific stat combinations and 4 skills each (2 passive + 2 active):

### Knight Combinations

#### 1. Paladin (Knight + Priest)
- **Base Stats**: STR 4, AGI 1, VIT 3, MAG 2
- **Growth/Level**: STR +2, AGI +1, VIT +2, MAG +1
- **Skills**:
  - Knight's Honor (Passive, JP: 120, Level: 1) – Reduces damage when surrounded
  - Divine Shield (Passive, JP: 160, Level: 2) – +2 defense, resist debuffs
  - Holy Strike (Active, JP: 200, Level: 2, AP: 5) – Physical + holy damage
  - Aura Heal (Active, JP: 220, Level: 3, AP: 6) – Heals and removes debuffs

#### 2. War Mage (Knight + Mage)
- **Base Stats**: STR 3, AGI 1, VIT 2, MAG 3
- **Growth/Level**: STR +2, AGI +1, VIT +1, MAG +2
- **Skills**:
  - Focused Mind (Passive, JP: 100, Level: 1) – Lower AP cost
  - Arcane Defense (Passive, JP: 140, Level: 2) – +10% magic resist
  - Spellblade (Active, JP: 180, Level: 2, AP: 6) – Physical + magic damage
  - Blitz (Active, JP: 220, Level: 3, AP: 8) – AoE physical/magical

#### 3. Samurai (Knight + Monk)
- **Base Stats**: STR 4, AGI 2, VIT 3, MAG 1
- **Growth/Level**: STR +2, AGI +2, VIT +2, MAG +1
- **Skills**:
  - Bushido (Passive, JP: 120, Level: 1) – Counterattack when surrounded
  - Iron Will (Passive, JP: 160, Level: 2) – Resist crowd control
  - Iaido Slash (Active, JP: 200, Level: 2, AP: 5) – High crit chance
  - Dragon Kick (Active, JP: 220, Level: 3, AP: 7) – Physical AoE

#### 4. Ninja (Knight + Thief)
- **Base Stats**: STR 3, AGI 3, VIT 2, MAG 1
- **Growth/Level**: STR +1, AGI +3, VIT +1, MAG +1
- **Skills**:
  - Evasion Mastery (Passive, JP: 120, Level: 1) – +10% evasion
  - Trap Master (Passive, JP: 160, Level: 2) – Immunity to traps
  - Shadow Slash (Active, JP: 200, Level: 2, AP: 4) – Bonus stealth damage
  - Smoke Bomb (Active, JP: 220, Level: 3, AP: 6) – Untargetable for 2 turns

#### 5. Marksman (Knight + Bowman)
- **Base Stats**: STR 3, AGI 2, VIT 2, MAG 2
- **Growth/Level**: STR +2, AGI +2, VIT +1, MAG +1
- **Skills**:
  - Eagle Reflexes (Passive, JP: 110, Level: 1) – +1 sight, +5% crit
  - Steady Aim (Passive, JP: 150, Level: 2) – Bonus damage at long range
  - Power Volley (Active, JP: 190, Level: 2, AP: 8) – High damage AoE
  - Armor Piercer (Active, JP: 210, Level: 3, AP: 5) – Ignores 50% defense

### Mage Combinations

#### 6. Wizard (Mage + Priest)
- **Base Stats**: STR 1, AGI 1, VIT 1, MAG 6
- **Growth/Level**: STR +1, AGI +1, VIT +1, MAG +3
- **Skills**:
  - Mana Spring (Passive, JP: 120, Level: 1) – +2 AP/tick regen
  - Blessing (Passive, JP: 160, Level: 2) – Resist status effects
  - Meteor (Active, JP: 200, Level: 2, AP: 8) – Magical AoE
  - Purify (Active, JP: 220, Level: 3, AP: 4) – Remove all debuffs

#### 7. Dragon Fist (Mage + Monk)
- **Base Stats**: STR 3, AGI 2, VIT 2, MAG 3
- **Growth/Level**: STR +2, AGI +2, VIT +1, MAG +2
- **Skills**:
  - Arcane Fury (Passive, JP: 110, Level: 1) – +20% magic damage
  - Ki Barrier (Passive, JP: 140, Level: 2) – +10% physical resist
  - Dragon Claw (Active, JP: 190, Level: 2, AP: 5) – Physical + magic
  - Energy Burst (Active, JP: 210, Level: 3, AP: 7) – AoE magic+physical

#### 8. Spellcloak (Mage + Thief)
- **Base Stats**: STR 2, AGI 3, VIT 1, MAG 3
- **Growth/Level**: STR +1, AGI +3, VIT +1, MAG +2
- **Skills**:
  - Mirage (Passive, JP: 120, Level: 1) – +15% evasion
  - Arcane Stealth (Passive, JP: 160, Level: 2) – Magic attacks don't break stealth
  - Shadow Bolt (Active, JP: 200, Level: 2, AP: 5) – Magic + sneak damage
  - Fade (Active, JP: 220, Level: 3, AP: 6) – Untargetable + AP regen

#### 9. Arcane Archer (Mage + Bowman)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 4
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +2
- **Skills**:
  - Snipe (Passive, JP: 120, Level: 1) – +2 sight, bonus magic damage at range
  - Quickcast (Passive, JP: 160, Level: 2) – First spell free
  - Piercing Arrow (Active, JP: 200, Level: 2, AP: 6) – Magic AoE on hit
  - Elemental Volley (Active, JP: 220, Level: 3, AP: 8) – Elemental AoE

### Priest Combinations

#### 10. Divine Fist (Priest + Monk)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 3
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +2
- **Skills**:
  - Spirit Guard (Passive, JP: 120, Level: 1) – +10% resist debuffs
  - Enlightened (Passive, JP: 160, Level: 2) – +15% AP gain
  - Sacred Strike (Active, JP: 200, Level: 2, AP: 4) – Magic + physical
  - Aura Shield (Active, JP: 220, Level: 3, AP: 7) – Absorb next attack

#### 11. Shadowmender (Priest + Thief)
- **Base Stats**: STR 1, AGI 3, VIT 1, MAG 3
- **Growth/Level**: STR +1, AGI +3, VIT +1, MAG +2
- **Skills**:
  - Shadow Mend (Passive, JP: 120, Level: 1) – Heals while stealthed
  - Purge Trap (Passive, JP: 160, Level: 2) – Remove traps on move
  - Life Leech (Active, JP: 200, Level: 2, AP: 5) – Deal & heal
  - Veil of Silence (Active, JP: 220, Level: 3, AP: 6) – Silence enemies in area

#### 12. Holy Archer (Priest + Bowman)
- **Base Stats**: STR 1, AGI 2, VIT 2, MAG 3
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +2
- **Skills**:
  - Divine Vision (Passive, JP: 120, Level: 1) – +2 sight, see hidden enemies
  - Blessing Arrow (Passive, JP: 160, Level: 2) – Arrows heal allies in line
  - Lightshot (Active, JP: 200, Level: 2, AP: 5) – Holy damage
  - Sacred Volley (Active, JP: 220, Level: 3, AP: 8) – Holy AoE

### Other Combinations

#### 13. Shadow Fist (Monk + Thief)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 1
- **Growth/Level**: STR +1, AGI +3, VIT +1, MAG +1
- **Skills**:
  - Shadow Counter (Passive, JP: 120, Level: 1) – Counterattack with stealth
  - Fast Hands (Passive, JP: 160, Level: 2) – Bonus JP from traps
  - Sneak Punch (Active, JP: 200, Level: 2, AP: 4) – Bonus AGI damage
  - Ki Cloak (Active, JP: 220, Level: 3, AP: 6) – Gain stealth after attack

#### 14. Ki Shot (Monk + Bowman)
- **Base Stats**: STR 2, AGI 3, VIT 2, MAG 2
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +2
- **Skills**:
  - Ki Sight (Passive, JP: 120, Level: 1) – +1 sight, see traps
  - Arrow Meditation (Passive, JP: 160, Level: 2) – +5% crit after meditation
  - Ki Arrow (Active, JP: 200, Level: 2, AP: 5) – Magic+physical damage
  - Arrow Flurry (Active, JP: 220, Level: 3, AP: 7) – Multi-hit AoE

#### 15. Stalker (Thief + Bowman)
- **Base Stats**: STR 2, AGI 3, VIT 1, MAG 2
- **Growth/Level**: STR +1, AGI +2, VIT +1, MAG +1
- **Skills**:
  - Hidden Tracker (Passive, JP: 120, Level: 1) – See hidden enemies/traps
  - Quick Escape (Passive, JP: 160, Level: 2) – Escape battles easier
  - Poison Arrow (Active, JP: 200, Level: 2, AP: 5) – Poison damage
  - Silent Volley (Active, JP: 220, Level: 3, AP: 8) – Ranged AoE with silence

## Crystal Combination System

Secondary jobs can be created through the existing `HeroCrystal.Combine()` method, which:
- Averages levels from both parent crystals
- Sums base stats from both parents
- Creates a `CompositeJob` that unions skills from both parent jobs
- Merges JP pools (both total and current)
- Unions learned skill sets

The system was already designed to support secondary jobs through:
- `CompositeJob` class for combining job stats and skills
- `HeroCrystal.Combine()` for merging crystals
- Per-crystal job level tracking for independent progression

## JP and Skill Learning

Each secondary job follows the same JP/skill learning pattern:
- **Level 1 Passive**: 100-120 JP
- **Level 2 Passive**: 140-160 JP
- **Level 2 Active**: 180-220 JP
- **Level 3 Active**: 210-220 JP

Total JP to master a secondary job: ~600-700 JP

Skills are learned by:
1. Earning JP with the secondary job equipped
2. Purchasing skills using `Hero.TryPurchaseSkill()` when requirements are met
3. Job level increases as more skills are learned
4. Job mastery is achieved when all 4 skills are unlocked

## Testing

Comprehensive test suite in `SecondaryJobSystemTests.cs` includes:
- **Instantiation Tests**: Verify all 15 secondary jobs can be created with correct stats
- **JP Cost Tests**: Verify all skills have correct JP costs
- **Crystal Combination Tests**: Verify primary jobs can be combined into secondary jobs
- **Hero Integration Tests**: Verify heroes can use secondary jobs, earn JP, and purchase skills
- **Learn Level Tests**: Verify all skills follow the correct level progression pattern
- **Stat Growth Tests**: Verify stat growth rates are correct for each job
- **Complete Workflow Tests**: End-to-end progression through a secondary job

## Implementation Notes

### Placeholder Mechanics
Some skill effects are implemented as placeholders for future game mechanics:
- Evasion bonuses
- Trap detection/immunity
- Sight distance modifications
- Status effect resistance
- Counterattack mechanics
- Untargetable status
- Stealth mechanics

These placeholders maintain the skill system structure while focusing on the core JP and job progression functionality.

### Design Principles
1. **Minimal Changes**: Uses existing `BaseJob` and `BaseSkill` infrastructure
2. **Consistency**: All secondary jobs follow the same 4-skill pattern (2 passive + 2 active)
3. **Balance**: Stat distributions reflect the hybrid nature of parent jobs
4. **Extensibility**: Easy to add full implementations for placeholder mechanics later

## Files Created

### Job Classes (15 files)
- `PitHero/RolePlayingFramework/Jobs/Paladin.cs`
- `PitHero/RolePlayingFramework/Jobs/WarMage.cs`
- `PitHero/RolePlayingFramework/Jobs/Samurai.cs`
- `PitHero/RolePlayingFramework/Jobs/Ninja.cs`
- `PitHero/RolePlayingFramework/Jobs/Marksman.cs`
- `PitHero/RolePlayingFramework/Jobs/Wizard.cs`
- `PitHero/RolePlayingFramework/Jobs/DragonFist.cs`
- `PitHero/RolePlayingFramework/Jobs/Spellcloak.cs`
- `PitHero/RolePlayingFramework/Jobs/ArcaneArcher.cs`
- `PitHero/RolePlayingFramework/Jobs/DivineFist.cs`
- `PitHero/RolePlayingFramework/Jobs/Shadowmender.cs`
- `PitHero/RolePlayingFramework/Jobs/HolyArcher.cs`
- `PitHero/RolePlayingFramework/Jobs/ShadowFist.cs`
- `PitHero/RolePlayingFramework/Jobs/KiShot.cs`
- `PitHero/RolePlayingFramework/Jobs/Stalker.cs`

### Skill Implementation Files (15 files)
- `PitHero/RolePlayingFramework/Skills/PaladinSkills.cs`
- `PitHero/RolePlayingFramework/Skills/WarMageSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SamuraiSkills.cs`
- `PitHero/RolePlayingFramework/Skills/NinjaSkills.cs`
- `PitHero/RolePlayingFramework/Skills/MarksmanSkills.cs`
- `PitHero/RolePlayingFramework/Skills/WizardSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DragonFistSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SpellcloakSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ArcaneArcherSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DivineFistSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ShadowmenderSkills.cs`
- `PitHero/RolePlayingFramework/Skills/HolyArcherSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ShadowFistSkills.cs`
- `PitHero/RolePlayingFramework/Skills/KiShotSkills.cs`
- `PitHero/RolePlayingFramework/Skills/StalkerSkills.cs`

### Test Files
- `PitHero.Tests/SecondaryJobSystemTests.cs` - Comprehensive test suite with 20+ tests

## Summary

The secondary job system is now fully implemented with:
- ✅ All 15 secondary job classes
- ✅ 60 total skills (4 per job)
- ✅ Proper stat distributions matching specifications
- ✅ JP costs and learn levels as specified
- ✅ Integration with existing crystal combination system
- ✅ Comprehensive test coverage
- ✅ Documentation

The system leverages the existing JP framework and crystal combination mechanics, requiring no changes to core infrastructure while adding substantial gameplay depth through hybrid job classes.

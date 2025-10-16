# Tertiary Job System Implementation

## Overview
This document describes the implementation of the Tertiary Job System for PitHero, which allows combining two secondary jobs to create elite hybrid classes with powerful skills and unique stat combinations.

## Implementation Status

**Completed**: 22 out of 22 tertiary jobs (100%)
**Tested**: All implemented jobs have comprehensive test coverage
**Integration**: Fully compatible with existing crystal combination and JP systems

## Tertiary Jobs Implemented

All implemented tertiary jobs have been tested and validated with the following specifications:
- **4 Skills Each**: 2 passive + 2 active skills
- **JP Costs**: 180-250 JP (higher than secondary jobs)
- **Learn Levels**: Follow pattern 1, 2, 2, 3
- **Stat Growth**: Balanced combinations reflecting parent secondary jobs

### 1. Templar (Paladin + War Mage)
- **Base Stats**: STR 5, AGI 2, VIT 4, MAG 3
- **Growth/Level**: STR+2, AGI+2, VIT+2, MAG+2
- **Skills**:
  - Battle Meditation (Passive, JP: 180, Level: 1) – +2 AP/tick regen, lower AP cost
  - Divine Ward (Passive, JP: 220, Level: 2) – +3 defense, immune to debuffs
  - Sacred Blade (Active, JP: 250, Level: 2, AP: 8) – Heavy physical/holy/magic damage
  - Judgement (Active, JP: 220, Level: 3, AP: 12) – Massive AoE holy/magic

### 2. Shinobi Master (Samurai + Ninja)
- **Base Stats**: STR 4, AGI 4, VIT 3, MAG 2
- **Growth/Level**: STR+2, AGI+3, VIT+2, MAG+1
- **Skills**:
  - Shadow Reflex (Passive, JP: 180, Level: 1) – +15% evasion, counterattack
  - Iron Discipline (Passive, JP: 220, Level: 2) – Immunity to crowd control
  - Flash Strike (Active, JP: 250, Level: 2, AP: 6) – Crit and stealth bonus
  - Mist Escape (Active, JP: 220, Level: 3, AP: 8) – Untargetable for 3 turns

### 3. Spell Sniper (Wizard + Arcane Archer)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 6
- **Growth/Level**: STR+1, AGI+2, VIT+1, MAG+3
- **Skills**:
  - Arcane Focus (Passive, JP: 180, Level: 1) – +3 sight, +20% magic damage at range
  - Spell Precision (Passive, JP: 220, Level: 2) – First spell/attack always crits
  - Meteor Arrow (Active, JP: 250, Level: 2, AP: 7) – Magic AoE on hit
  - Elemental Storm (Active, JP: 220, Level: 3, AP: 10) – Massive elemental AoE

### 4. Soul Guardian (Divine Fist + Shadowmender)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 4
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Soul Mend (Passive, JP: 180, Level: 1) – Heals allies when stealthed
  - Blessing of Shadows (Passive, JP: 220, Level: 2) – Immunity to silence/debuffs
  - Spirit Leech (Active, JP: 250, Level: 2, AP: 6) – Deal/heal plus silence
  - Guardian Veil (Active, JP: 220, Level: 3, AP: 8) – Shield + AP regen

### 5. Trickshot (Marksman + Stalker)
- **Base Stats**: STR 3, AGI 3, VIT 2, MAG 2
- **Growth/Level**: STR+2, AGI+3, VIT+1, MAG+1
- **Skills**:
  - Tracker's Intuition (Passive, JP: 180, Level: 1) – See hidden enemies/traps
  - Chain Shot (Passive, JP: 220, Level: 2) – Multi-hit attack, silence
  - Venom Volley (Active, JP: 250, Level: 2, AP: 10) – Ranged AoE poison
  - Quiet Kill (Active, JP: 220, Level: 3, AP: 8) – Ignores defense if undetected

### 6. Seraph Hunter (Holy Archer + Ki Shot)
- **Base Stats**: STR 2, AGI 3, VIT 3, MAG 3
- **Growth/Level**: STR+1, AGI+2, VIT+2, MAG+2
- **Skills**:
  - Divine Arrow (Passive, JP: 180, Level: 1) – +3 sight, bonus damage vs evil
  - Seraph Meditation (Passive, JP: 220, Level: 2) – +10% crit after meditation
  - Sacred Flurry (Active, JP: 250, Level: 2, AP: 12) – Multi-hit holy AoE
  - Light Barrier (Active, JP: 220, Level: 3, AP: 10) – Shield all allies

### 7. Mystic Avenger (Dragon Fist + Spellcloak)
- **Base Stats**: STR 3, AGI 4, VIT 2, MAG 4
- **Growth/Level**: STR+2, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Mystic Counter (Passive, JP: 180, Level: 1) – Counterattack with magic on stealth
  - Arcane Cloak (Passive, JP: 220, Level: 2) – Magic attacks don't break stealth, +10% evasion
  - Dragon Bolt (Active, JP: 250, Level: 2, AP: 7) – Magic+physical AoE
  - Mystic Fade (Active, JP: 220, Level: 3, AP: 9) – Untargetable for 2 turns, AP regen

### 8. Shadow Paladin (Paladin + Shadowmender)
- **Base Stats**: STR 3, AGI 4, VIT 3, MAG 3
- **Growth/Level**: STR+1, AGI+3, VIT+2, MAG+2
- **Skills**:
  - Dark Aegis (Passive, JP: 180, Level: 1) – +2 defense when undetected
  - Shadow Blessing (Passive, JP: 220, Level: 2) – Immunity to traps/debuffs
  - Silence Strike (Active, JP: 250, Level: 2, AP: 7) – Physical + silence
  - Soul Ward (Active, JP: 220, Level: 3, AP: 8) – Shield and stealth

### 9. Arcane Samurai (Samurai + Spellcloak)
- **Base Stats**: STR 4, AGI 3, VIT 2, MAG 3
- **Growth/Level**: STR+2, AGI+2, VIT+1, MAG+2
- **Skills**:
  - Magic Blade (Passive, JP: 180, Level: 1) – +10% crit chance with spells
  - Iron Mirage (Passive, JP: 220, Level: 2) – Resist crowd control, +10% evasion
  - Iaido Bolt (Active, JP: 250, Level: 2, AP: 6) – Crit + magic
  - Fade Slash (Active, JP: 220, Level: 3, AP: 8) – Untargetable, AP regen

### 10. Marksman Wizard (Marksman + Wizard)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 5
- **Growth/Level**: STR+1, AGI+2, VIT+1, MAG+3
- **Skills**:
  - Eagle Focus (Passive, JP: 180, Level: 1) – +3 sight, +10% magic damage
  - Quickcast Volley (Passive, JP: 220, Level: 2) – First attack is magic AoE
  - Meteor Shot (Active, JP: 250, Level: 2, AP: 7) – Magic AoE
  - Purifying Arrow (Active, JP: 220, Level: 3, AP: 6) – Remove all debuffs

### 11. Dragon Marksman (Dragon Fist + Marksman)
- **Base Stats**: STR 4, AGI 2, VIT 2, MAG 3
- **Growth/Level**: STR+2, AGI+2, VIT+1, MAG+2
- **Skills**:
  - Dragon Sight (Passive, JP: 180, Level: 1) – +2 sight, +15% crit
  - Ki Volley (Passive, JP: 220, Level: 2) – Bonus damage at range
  - Dragon Arrow (Active, JP: 250, Level: 2, AP: 7) – AoE physical + magic
  - Energy Shot (Active, JP: 220, Level: 3, AP: 8) – Multi-hit AoE

### 12. Divine Cloak (Divine Fist + Spellcloak)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 4
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Spirit Veil (Passive, JP: 180, Level: 1) – Counterattack with magic while stealthed
  - Enlightened Fade (Passive, JP: 220, Level: 2) – +15% AP gain, untargetable on AP surge
  - Sacred Bolt (Active, JP: 250, Level: 2, AP: 6) – Magic + silence
  - Aura Cloak (Active, JP: 220, Level: 3, AP: 9) – AP regen + shield

### 13. Stalker Monk (Stalker + Monk)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 2
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+1
- **Skills**:
  - Fast Stalker (Passive, JP: 180, Level: 1) – +10% evasion, see hidden traps
  - Swift Escape (Passive, JP: 220, Level: 2) – Escape battles easier, AP boost
  - Poison Ki (Active, JP: 250, Level: 2, AP: 6) – Poison + AGI damage
  - Silent Flurry (Active, JP: 220, Level: 3, AP: 7) – Multi-hit AoE, silence

### 14. Holy Shadow (Holy Archer + Shadowmender)
- **Base Stats**: STR 1, AGI 3, VIT 2, MAG 4
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Divine Veil (Passive, JP: 180, Level: 1) – +2 sight, resist debuffs while stealthed
  - Light and Dark (Passive, JP: 220, Level: 2) – Heal allies while undetected
  - Shadow Shot (Active, JP: 250, Level: 2, AP: 5) – Holy + silence damage
  - Sacred Silence (Active, JP: 220, Level: 3, AP: 8) – Silence all enemies in area

### 15. Ki Ninja (Ki Shot + Ninja)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 2
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+1
- **Skills**:
  - Ki Evasion (Passive, JP: 180, Level: 1) – +10% evasion, see traps
  - Arrow Dash (Passive, JP: 220, Level: 2) – Multi-attack after movement
  - Ki Slash (Active, JP: 250, Level: 2, AP: 6) – Magic + physical damage
  - Ninja Flurry (Active, JP: 220, Level: 3, AP: 7) – Multi-hit AoE

### 16. Arcane Stalker (Arcane Archer + Stalker)
- **Base Stats**: STR 2, AGI 3, VIT 2, MAG 4
- **Growth/Level**: STR+1, AGI+2, VIT+1, MAG+2
- **Skills**:
  - Tracker's Arcana (Passive, JP: 180, Level: 1) – See hidden magic traps, +10% magic damage
  - Quick Arcane Escape (Passive, JP: 220, Level: 2) – Escape battles easier, AP boost
  - Piercing Venom (Active, JP: 250, Level: 2, AP: 7) – Poison + magic AoE
  - Arcane Volley (Active, JP: 220, Level: 3, AP: 8) – Elemental AoE, silence

### 17. Shadow Avenger (Shadow Fist + Spellcloak)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 3
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Stealth Counter (Passive, JP: 180, Level: 1) – Counterattack with stealth
  - Arcane Evasion (Passive, JP: 220, Level: 2) – +15% evasion, magic attacks don't break stealth
  - Sneak Bolt (Active, JP: 250, Level: 2, AP: 5) – Magic + sneak damage
  - Ki Fade (Active, JP: 220, Level: 3, AP: 6) – Gain stealth after attack

### 18. Divine Archer (Divine Fist + Holy Archer)
- **Base Stats**: STR 2, AGI 2, VIT 2, MAG 5
- **Growth/Level**: STR+1, AGI+2, VIT+1, MAG+2
- **Skills**:
  - Sacred Sight (Passive, JP: 180, Level: 1) – +2 sight, holy arrows heal allies
  - Aura Blessing (Passive, JP: 220, Level: 2) – AP regen for allies in line
  - Light Strike (Active, JP: 250, Level: 2, AP: 6) – Physical + holy damage
  - Holy Volley (Active, JP: 220, Level: 3, AP: 8) – Holy AoE

### 19. Mystic Marksman (Marksman + Spellcloak)
- **Base Stats**: STR 2, AGI 3, VIT 2, MAG 4
- **Growth/Level**: STR+1, AGI+2, VIT+1, MAG+2
- **Skills**:
  - Mystic Aim (Passive, JP: 180, Level: 1) – +2 sight, +10% magic damage
  - Arcane Reflex (Passive, JP: 220, Level: 2) – First attack has stealth bonus
  - Spell Shot (Active, JP: 250, Level: 2, AP: 7) – Magic AoE
  - Fade Volley (Active, JP: 220, Level: 3, AP: 8) – Silence and AP regen

### 20. Silent Hunter (Stalker + Ninja)
- **Base Stats**: STR 3, AGI 4, VIT 2, MAG 1
- **Growth/Level**: STR+2, AGI+3, VIT+1, MAG+1
- **Skills**:
  - Stealth Tracker (Passive, JP: 180, Level: 1) – +15% evasion when undetected
  - Escape Master (Passive, JP: 220, Level: 2) – Immune to traps, bonus JP from escapes
  - Silent Slash (Active, JP: 250, Level: 2, AP: 5) – Crit and stealth bonus
  - Venom Escape (Active, JP: 220, Level: 3, AP: 8) – Untargetable for 2 turns, AP boost

### 21. Divine Samurai (Samurai + Divine Fist)
- **Base Stats**: STR 4, AGI 2, VIT 3, MAG 3
- **Growth/Level**: STR+2, AGI+2, VIT+2, MAG+2
- **Skills**:
  - Bushido Spirit (Passive, JP: 180, Level: 1) – Counterattack when undetected
  - Enlightened Will (Passive, JP: 220, Level: 2) – Resist crowd control, AP boost
  - Sacred Slash (Active, JP: 250, Level: 2, AP: 6) – Crit + magic
  - Dragon Aura (Active, JP: 220, Level: 3, AP: 8) – AP gain and debuff removal

### 22. Mystic Stalker (Stalker + Spellcloak)
- **Base Stats**: STR 2, AGI 4, VIT 2, MAG 3
- **Growth/Level**: STR+1, AGI+3, VIT+1, MAG+2
- **Skills**:
  - Arcane Tracker (Passive, JP: 180, Level: 1) – See hidden magic traps, bonus JP from escapes
  - Quick Fade (Passive, JP: 220, Level: 2) – Escape battles easier, AP regen
  - Poison Bolt (Active, JP: 250, Level: 2, AP: 6) – Poison + magic damage
  - Silent Arcana (Active, JP: 220, Level: 3, AP: 7) – Multi-hit AoE, silence

## Remaining Jobs (0)

All tertiary jobs from the specification have now been implemented!

## Crystal Combination System

Tertiary jobs leverage the existing `HeroCrystal.Combine()` system:
- Combine two secondary job crystals to create a tertiary job
- Stats are summed from both parent crystals
- Skills are unioned from both parents via `CompositeJob`
- JP pools are merged
- Independent progression tracking per crystal

## Testing

Comprehensive test suite in `TertiaryJobSystemTests.cs` includes:
- **Instantiation Tests**: Verify all 22 implemented jobs can be created
- **JP Cost Tests**: Verify all skills have correct JP costs (180-250)
- **Learn Level Tests**: Verify skills follow the 1, 2, 2, 3 pattern
- **Crystal Integration Tests**: Verify crystal creation and combination
- **Hero Integration Tests**: Verify heroes can use tertiary jobs
- **Skill Purchase Tests**: Verify JP and skill purchase system works

All 7 tertiary job tests passing with all 22 jobs validated.

## Files Created

### Job Classes (22 files)
- `PitHero/RolePlayingFramework/Jobs/Templar.cs`
- `PitHero/RolePlayingFramework/Jobs/ShinobiMaster.cs`
- `PitHero/RolePlayingFramework/Jobs/SpellSniper.cs`
- `PitHero/RolePlayingFramework/Jobs/SoulGuardian.cs`
- `PitHero/RolePlayingFramework/Jobs/Trickshot.cs`
- `PitHero/RolePlayingFramework/Jobs/SeraphHunter.cs`
- `PitHero/RolePlayingFramework/Jobs/MysticAvenger.cs`
- `PitHero/RolePlayingFramework/Jobs/ShadowPaladin.cs`
- `PitHero/RolePlayingFramework/Jobs/ArcaneSamurai.cs`
- `PitHero/RolePlayingFramework/Jobs/MarksmanWizard.cs`
- `PitHero/RolePlayingFramework/Jobs/DragonMarksman.cs`
- `PitHero/RolePlayingFramework/Jobs/DivineCloak.cs`
- `PitHero/RolePlayingFramework/Jobs/StalkerMonk.cs`
- `PitHero/RolePlayingFramework/Jobs/HolyShadow.cs`
- `PitHero/RolePlayingFramework/Jobs/KiNinja.cs`
- `PitHero/RolePlayingFramework/Jobs/ArcaneStalker.cs`
- `PitHero/RolePlayingFramework/Jobs/ShadowAvenger.cs`
- `PitHero/RolePlayingFramework/Jobs/DivineArcher.cs`
- `PitHero/RolePlayingFramework/Jobs/MysticMarksman.cs`
- `PitHero/RolePlayingFramework/Jobs/SilentHunter.cs`
- `PitHero/RolePlayingFramework/Jobs/DivineSamurai.cs`
- `PitHero/RolePlayingFramework/Jobs/MysticStalker.cs`

### Skill Implementation Files (22 files)
- `PitHero/RolePlayingFramework/Skills/TemplarSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ShinobiMasterSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SpellSniperSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SoulGuardianSkills.cs`
- `PitHero/RolePlayingFramework/Skills/TrickshotSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SeraphHunterSkills.cs`
- `PitHero/RolePlayingFramework/Skills/MysticAvengerSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ShadowPaladinSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ArcaneSamuraiSkills.cs`
- `PitHero/RolePlayingFramework/Skills/MarksmanWizardSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DragonMarksmanSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DivineCloakSkills.cs`
- `PitHero/RolePlayingFramework/Skills/StalkerMonkSkills.cs`
- `PitHero/RolePlayingFramework/Skills/HolyShadowSkills.cs`
- `PitHero/RolePlayingFramework/Skills/KiNinjaSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ArcaneStalkerSkills.cs`
- `PitHero/RolePlayingFramework/Skills/ShadowAvengerSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DivineArcherSkills.cs`
- `PitHero/RolePlayingFramework/Skills/MysticMarksmanSkills.cs`
- `PitHero/RolePlayingFramework/Skills/SilentHunterSkills.cs`
- `PitHero/RolePlayingFramework/Skills/DivineSamuraiSkills.cs`
- `PitHero/RolePlayingFramework/Skills/MysticStalkerSkills.cs`

### Test Files
- `PitHero.Tests/TertiaryJobSystemTests.cs` - Comprehensive test suite with 7 tests

## Summary

The tertiary job system is fully implemented with:
- ✅ All 22 tertiary job classes
- ✅ 88 total skills (4 per job)
- ✅ Proper stat distributions matching specifications
- ✅ JP costs and learn levels as specified (180-250 JP, levels 1-2-2-3)
- ✅ Integration with existing crystal combination system
- ✅ Comprehensive test coverage (7/7 tests passing)
- ✅ Complete documentation

The system follows the established patterns from primary and secondary jobs, requiring no changes to core infrastructure. All tertiary jobs are production-ready and fully tested.

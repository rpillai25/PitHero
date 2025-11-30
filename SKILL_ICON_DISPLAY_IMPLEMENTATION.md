# Skill Icon Display Implementation

## Overview
This implementation displays proper skill icons from the `SkillsStencils.atlas` in the HeroCrystalTab for all learned skills, including both Job skills and Synergy skills.

## Changes Made

### 1. HeroCrystalTab.cs Updates

#### SkillButton Icon Loading
- **Changed from**: Generic placeholder icons based on hash code (SkillIcon1-10 from UI.atlas)
- **Changed to**: Skill-specific icons loaded from `SkillsStencils.atlas` using the skill's ID
- **Fallback**: If a sprite is not found for a skill ID, falls back to a default icon

```csharp
// Load skill icon from SkillsStencils atlas using skill ID
var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
var iconSprite = skillsAtlas.GetSprite(_skill.Id);
```

#### Skill Grid Display
- **Changed from**: Only showing Job skills
- **Changed to**: Showing both Job skills AND learned Synergy skills
- **New Helper**: Added `SkillDisplayInfo` struct to track whether a skill is a synergy skill

```csharp
private struct SkillDisplayInfo
{
    public ISkill Skill;
    public bool IsLearned;
    public bool IsSynergySkill;
}
```

#### Synergy Skill Integration
The `RebuildSkillGrid` method now:
1. Collects all job skills from the hero's Job
2. Collects all learned synergy skills from the HeroCrystal's `LearnedSynergySkillIds`
3. Displays them together in a 4-column grid

#### Click Behavior for Synergy Skills
- Synergy skills cannot be purchased with JP (they're auto-learned when synergy points reach threshold)
- Clicking on an unlearned synergy skill shows a debug message explaining they unlock automatically
- Job skills maintain the existing purchase flow with JP

## How It Works

### Skill Icon Mapping
Each skill in the game has a unique `Id` property:
- **Job Skills**: e.g., `"knight.spin_slash"`, `"mage.fire"`, `"priest.heal"`
- **Synergy Skills**: e.g., `"synergy.holy_strike"`, `"synergy.meteor"`, `"synergy.dragon_claw"`

The `SkillsStencils.atlas` should contain sprites with these exact IDs as their names.

### Synergy Skill Unlocking Flow
1. Player arranges items in inventory to form a synergy pattern
2. System detects active synergies and applies effects
3. Player earns synergy points through combat: `hero.EarnSynergyPoints(amount)`
4. When points reach the threshold (`SynergyPattern.SynergyPointsRequired`):
   - `HeroCrystal.LearnSynergySkill(skillId)` is called automatically
   - Skill is added to hero's `LearnedSkills` dictionary
   - HeroCrystalTab now displays the skill with full color (learned state)

### Visual States
- **Learned Skills**: Full color icon
- **Unlearned Job Skills**: Grayscale icon (can be purchased with JP)
- **Learned Synergy Skills**: Full color icon (already unlocked)

## Example Sprite Names in SkillsStencils.atlas

### Job Skills
```
knight.light_armor
knight.heavy_armor
knight.spin_slash
knight.heavy_strike
mage.heart_fire
mage.economist
mage.fire
mage.firestorm
priest.calm_spirit
priest.mender
priest.heal
priest.defup
monk.counter
monk.deflect
monk.roundhouse
monk.flaming_fist
thief.shadowstep
thief.trap_sense
thief.sneak_attack
thief.vanish
bowman.eagle_eye
bowman.quickdraw
bowman.power_shot
bowman.volley
```

### Synergy Skills
```
synergy.holy_strike
synergy.iaido_slash
synergy.shadow_slash
synergy.spellblade
synergy.meteor
synergy.shadow_bolt
synergy.elemental_volley
synergy.blitz
synergy.aura_heal
synergy.purify
synergy.sacred_strike
synergy.life_leech
synergy.dragon_claw
synergy.energy_burst
synergy.dragon_kick
synergy.sneak_punch
synergy.smoke_bomb
synergy.poison_arrow
synergy.fade
synergy.ki_cloak
synergy.piercing_arrow
synergy.lightshot
synergy.ki_arrow
synergy.arrow_flurry
synergy.sacred_blade
synergy.flash_strike
synergy.soul_ward
synergy.dragon_bolt
synergy.elemental_storm
```

### Synergy Pattern IDs (for stencil icons)
```
knight.paladin
knight.samurai
knight.ninja
knight.war_mage
mage.wizard
mage.spellcloak
mage.arcane_archer
mage.war_mage_alt
priest.paladin_alt
priest.wizard_alt
priest.divine_fist
priest.shadowmender
monk.dragon_fist
monk.dragon_fist_alt
monk.samurai_alt
monk.shadow_fist
thief.ninja_alt
thief.stalker
thief.spellcloak_alt
thief.shadow_fist_alt
bowman.arcane_archer_alt
bowman.holy_archer
bowman.ki_shot
bowman.ki_shot_alt
cross.templar
cross.shinobi_master
cross.soul_guardian
cross.mystic_avenger
cross.spell_sniper
```

## Testing

To test the implementation:

1. **Create a hero with a crystal**
```csharp
var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, baseStats);
var hero = new Hero("TestHero", new Knight(), 1, baseStats, crystal);
```

2. **Earn JP and purchase job skills**
```csharp
crystal.EarnJP(500);
hero.TryPurchaseSkill(someSkill);
```

3. **Arrange items to form synergy patterns**
```csharp
// Place items in inventory grid to match a synergy pattern
```

4. **Earn synergy points to unlock synergy skills**
```csharp
hero.EarnSynergyPoints(10); // After battles
// Skills auto-unlock when threshold is reached
```

5. **Open HeroCrystalTab to view all skills**
- Job skills should show with proper icons
- Learned synergy skills should appear below job skills
- All icons should load from SkillsStencils.atlas

## Benefits

1. **Clear Visual Identity**: Each skill has its own unique icon
2. **Unified Display**: Job and synergy skills shown together
3. **Proper State Management**: Learned vs unlearned states clearly visible
4. **Automatic Updates**: UI updates when synergy skills are unlocked
5. **Fallback Handling**: Graceful degradation if sprites are missing

## Future Enhancements

1. **Skill Categories**: Add headers to separate Job Skills from Synergy Skills
2. **Skill Sorting**: Sort by JP cost, element type, or unlock order
3. **Progress Indicators**: Show synergy point progress toward next unlock
4. **Skill Details**: Expand tooltip to show synergy pattern requirements
5. **Animation**: Add glow effect when synergy skill is newly unlocked

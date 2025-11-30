# Stencil Library Sprite Implementation

## Overview
This implementation updates the StencilLibraryPanel to display proper sprites from the `SkillsStencils.atlas` for each discovered synergy pattern stencil.

## Changes Made

### StencilLibraryPanel.cs - StencilSlot.DrawStencilPreview()

#### Sprite Loading Logic
The sprite name is determined by the following rules:

1. **If the SynergyPattern has an UnlockedSkill**: Use the skill's ID as the sprite name
   ```csharp
   if (_pattern.UnlockedSkill != null)
   {
       spriteName = _pattern.UnlockedSkill.Id;
   }
   ```

2. **If the SynergyPattern does NOT have an UnlockedSkill**: Use the pattern's ID as the sprite name
   ```csharp
   else
   {
       spriteName = _pattern.Id;
   }
   ```

#### Implementation Details

```csharp
private void DrawStencilPreview(Batcher batcher)
{
    try
    {
        var skillsStencilsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
        
        // Determine sprite name based on whether pattern has an unlocked skill
        string spriteName;
        if (_pattern.UnlockedSkill != null)
        {
            // Use the skill's ID as the sprite name
            spriteName = _pattern.UnlockedSkill.Id;
        }
        else
        {
            // Use the pattern's ID as the sprite name
            spriteName = _pattern.Id;
        }
        
        var sprite = skillsStencilsAtlas.GetSprite(spriteName);
        
        if (sprite != null)
        {
            var drawable = new SpriteDrawable(sprite);
            drawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
        }
        else
        {
            // Fallback if sprite not found
            DrawFallbackIcon(batcher);
        }
    }
    catch
    {
        // Silently fail and draw fallback if sprite atlas not found
        DrawFallbackIcon(batcher);
    }
}
```

### Fallback Icon System

Added a robust fallback system in case sprites are missing:

1. **DrawFallbackIcon()**: Attempts to display the first item kind from the pattern
2. **GetItemSpriteName()**: Maps ItemKind enum values to actual sprite names in the Items atlas

This provides a graceful degradation if:
- The SkillsStencils atlas is not found
- A specific sprite is missing from the atlas
- Any other loading error occurs

## Example Sprite Names Required in SkillsStencils.atlas

### For Patterns WITH UnlockedSkill (Use Skill ID)

**Knight Synergies:**
```
synergy.holy_strike       // Paladin pattern
synergy.iaido_slash       // Samurai pattern
synergy.shadow_slash      // Ninja pattern
synergy.spellblade        // War Mage pattern
```

**Mage Synergies:**
```
synergy.meteor            // Wizard pattern
synergy.shadow_bolt       // Spellcloak pattern
synergy.elemental_volley  // Arcane Archer pattern
synergy.blitz             // War Mage alt pattern
```

**Priest Synergies:**
```
synergy.aura_heal         // Paladin alt pattern
synergy.purify            // Wizard alt pattern
synergy.sacred_strike     // Divine Fist pattern
synergy.life_leech        // Shadowmender pattern
```

**Monk Synergies:**
```
synergy.dragon_claw       // Dragon Fist pattern
synergy.energy_burst      // Dragon Fist alt pattern
synergy.dragon_kick       // Samurai alt pattern
synergy.sneak_punch       // Shadow Fist pattern
```

**Thief Synergies:**
```
synergy.smoke_bomb        // Ninja alt pattern
synergy.poison_arrow      // Stalker pattern
synergy.fade              // Spellcloak alt pattern
synergy.ki_cloak          // Shadow Fist alt pattern
```

**Bowman Synergies:**
```
synergy.piercing_arrow    // Arcane Archer alt pattern
synergy.lightshot         // Holy Archer pattern
synergy.ki_arrow          // Ki Shot pattern
synergy.arrow_flurry      // Ki Shot alt pattern
```

**Cross-Class Synergies:**
```
synergy.sacred_blade      // Templar pattern
synergy.flash_strike      // Shinobi Master pattern
synergy.soul_ward         // Soul Guardian pattern
synergy.dragon_bolt       // Mystic Avenger pattern
synergy.elemental_storm   // Spell Sniper pattern
```

### For Patterns WITHOUT UnlockedSkill (Use Pattern ID)

If you have any synergy patterns that provide only passive effects without unlocking a skill, use the pattern's ID:

```
knight.defensive_stance
mage.mana_efficiency
priest.healing_aura
monk.combat_flow
thief.stealth_mastery
bowman.precision_stance
```

## Pattern Examples

### Pattern with UnlockedSkill (Most Common)
```csharp
var paladinPattern = new SynergyPattern(
    "knight.paladin",                    // Pattern ID (not used for sprite)
    "Paladin",
    "Holy warrior synergy",
    offsets,
    kinds,
    effects,
    100,
    new HolyStrikeSkill()               // Sprite name: "synergy.holy_strike"
);
```

### Pattern without UnlockedSkill (Rare)
```csharp
var defensivePattern = new SynergyPattern(
    "knight.defensive_stance",          // Pattern ID (used for sprite)
    "Defensive Stance",
    "Pure defense boost",
    offsets,
    kinds,
    effects,
    0,                                  // No skill unlock
    null                                // Sprite name: "knight.defensive_stance"
);
```

## Visual Behavior

### In the Stencil Library:
1. **Discovered Stencils**: Display full-color sprite from SkillsStencils atlas
2. **Undiscovered Stencils**: Display dark background with no icon
3. **Missing Sprites**: Display fallback icon (first item from pattern's required items)

### Hover States:
- **Hover**: Shows select box overlay
- **Selected**: Shows highlight box overlay
- **Tooltip**: Displays pattern name when hovering over discovered stencil

## Testing

To verify the implementation:

1. **Discover a stencil** (via organic play or debug):
   ```csharp
   gameStateService.DiscoverStencil("knight.paladin", StencilDiscoverySource.PlayerMatch);
   ```

2. **Open the Stencil Library Panel** and check:
   - Does the sprite load from SkillsStencils atlas?
   - Does it use the correct sprite name (skill ID or pattern ID)?
   - Does the fallback work if sprite is missing?

3. **Test with different pattern types**:
   - Patterns with UnlockedSkill (should use skill ID)
   - Patterns without UnlockedSkill (should use pattern ID)

## Benefits

1. **Consistent Visual Identity**: Each synergy has its own unique icon matching the unlocked skill
2. **Smart Naming**: Uses the most relevant ID (skill if available, pattern otherwise)
3. **Graceful Degradation**: Falls back to item icons if sprites are missing
4. **Easy Asset Management**: Asset creators just name sprites after skill/pattern IDs
5. **Error Handling**: Silently handles missing atlases or sprites without crashing

## Asset Creation Guidelines

When creating sprites for the SkillsStencils atlas:

1. **Size**: Match the slot size (32x32 pixels recommended)
2. **Style**: Should visually represent the synergy or skill
3. **Naming**: 
   - For synergy skills: Use the skill ID (e.g., `synergy.holy_strike`)
   - For pattern-only bonuses: Use the pattern ID (e.g., `knight.defensive_stance`)
4. **Format**: PNG with transparency support
5. **Consistency**: Maintain visual consistency with other UI elements

## Future Enhancements

1. **Animated Icons**: Add subtle animations for legendary synergies
2. **Rarity Borders**: Color-code borders based on synergy rarity
3. **Preview Overlay**: Show the actual pattern shape on hover
4. **Completion Indicators**: Visual indicator if synergy skill has been unlocked
5. **Sort/Filter Options**: Filter by class, discovered/undiscovered, or skill type

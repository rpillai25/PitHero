# Fix for StencilLibraryPanel Showing Wrong Sprite

## Issue
When organically discovering `knight.heavy_fortification`, the StencilLibraryPanel was incorrectly showing the sprite for `knight.shield_mastery`, although the text correctly said "Heavy Fortification".

## Root Cause
The `_allSynergyPatterns` list in `HeroUI.cs` only contained **5 test patterns** instead of **all synergy patterns** from the game. This caused a mismatch between:

1. The **pattern ID** stored in `GameStateService.DiscoveredStencils` when a pattern is discovered organically
2. The **index** of the pattern in the `_allSynergyPatterns` list used by `StencilLibraryPanel`

### Why This Happened
- `knight.shield_mastery` was at index **0** in the old list
- `knight.heavy_fortification` was at index **3** in the old list
- When displaying stencils, the `StencilLibraryPanel.RefreshSlots()` iterates through `_allPatterns` by index and checks if each pattern is discovered
- The sprite lookup uses the pattern/skill ID, but the slot position was determined by the index in `_allPatterns`
- This caused discovered patterns to show in the wrong slots with wrong sprites

## Solution
Updated `HeroUI` constructor to include **ALL** synergy patterns from all classes:

### Pattern Count by Class
- **Knight**: 10 patterns
- **Mage**: 9 patterns  
- **Priest**: 9 patterns
- **Monk**: 9 patterns
- **Thief**: 8 patterns
- **Bowman**: 8 patterns
- **Cross-Class**: 10 patterns

**Total**: 63 synergy patterns

### Complete Pattern List
```csharp
// Knight patterns (10)
CreateHolyStrike()
CreateIaidoSlash()
CreateShadowSlash()
CreateSpellblade()
CreateArmorMastery()
CreateSwordProficiency()
CreateGuardiansResolve()
CreateBerserkerRage()
CreateShieldMastery()
CreateHeavyFortification()

// Mage patterns (9)
CreateMeteor()
CreateShadowBolt()
CreateElementalVolley()
CreateBlitz()
CreateArcaneFocus()
CreateElementalMastery()
CreateSpellWeaving()
CreateManaConvergence()
CreateRodFocus()

// Priest patterns (9)
CreateAuraHeal()
CreatePurify()
CreateSacredStrike()
CreateLifeLeech()
CreateDivineProtection()
CreateHealingAmplification()
CreateHolyAura()
CreateSanctifiedMind()
CreateDivineVestments()

// Monk patterns (9)
CreateDragonClaw()
CreateEnergyBurst()
CreateDragonKick()
CreateSneakPunch()
CreateIronFist()
CreateMartialFocus()
CreateKiMastery()
CreateEvasionTraining()
CreateBalanceTraining()

// Thief patterns (8)
CreateSmokeBomb()
CreatePoisonArrow()
CreateFade()
CreateKiCloak()
CreateShadowStep()
CreateLockpicking()
CreateTrapMastery()
CreateAssassinsEdge()

// Bowman patterns (8)
CreatePiercingArrow()
CreateLightshot()
CreateKiArrow()
CreateArrowFlurry()
CreateMarksman()
CreateSharpAim()
CreateRangersPath()
CreateWindArcher()

// Cross-class patterns (10)
CreateSacredBlade()
CreateFlashStrike()
CreateSoulWard()
CreateDragonBolt()
CreateElementalStorm()
CreateBattleMage()
CreateHolyWarrior()
CreateShadowMaster()
CreateArcaneProtector()
CreateElementalChampion()
```

## How It Works Now

1. **Discovery**: When you organically discover `knight.heavy_fortification` by arranging items:
   - `GameStateService.DiscoveredStencils["knight.heavy_fortification"] = PlayerMatch`

2. **Display**: When you open the StencilLibraryPanel:
   - Panel refreshes and iterates through ALL 63 patterns in `_allPatterns`
   - For each pattern, checks `gameStateService.IsStencilDiscovered(pattern.Id)`
   - If discovered, displays the pattern in its correct slot (slot index = pattern index in list)
   - Loads sprite using either:
     - `pattern.UnlockedSkill.Id` if the pattern has an unlocked skill
     - `pattern.Id` if the pattern has no unlocked skill

3. **Result**: 
   - `knight.heavy_fortification` now shows at its correct index (9) in the Knight section
   - The correct sprite is loaded using the skill ID `synergy.holy_strike` (or pattern ID if no skill)
   - The text and sprite now match correctly

## Benefits

1. **Correct Sprite Display**: Each discovered stencil shows its own sprite
2. **Consistent Ordering**: Stencils appear in a logical order (by class, then by creation order)
3. **Future-Proof**: When new patterns are added, they just need to be added to this list
4. **Complete Library**: Players can see all 63 synergy patterns as they discover them

## Testing

To verify the fix:

1. Organically discover any Knight pattern (e.g., `knight.heavy_fortification`)
2. Open the Stencil Library Panel
3. Verify the correct sprite and name are displayed
4. Try discovering other patterns and verify they all show correctly

## Notes

- The order of patterns in `_allSynergyPatterns` determines their display order in the library
- Currently ordered by class: Knight ? Mage ? Priest ? Monk ? Thief ? Bowman ? Cross-Class
- Each class's patterns are in the order they were created
- If a pattern doesn't have a sprite in `SkillsStencils.atlas`, it will fall back to showing the first item kind from the pattern

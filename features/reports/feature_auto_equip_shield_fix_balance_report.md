# Balance Report: Auto Equip Shield Fix

**Feature:** Auto Equip Shield Fix  
**Tester:** Pit Balance Tester  
**Date:** 2025  
**Verdict:** ✅ PASS

---

## 1. Scope

This is a targeted bug-fix report. The fix separates `ItemKind.Shield` from weapon `case` blocks in `Mercenary.Equip()` so that shields always route to `WeaponShield2` (slot index 1 / right-most slot), and weapons always route to `WeaponShield1` (slot index 0). No broader balance rebalancing was in scope.

---

## 2. Build Verification

| Step | Result |
|------|--------|
| `dotnet build` | ✅ **PASS** — 0 errors, 100 warnings (all pre-existing, none related to this fix) |

Build time: ~15 s. All four projects compiled cleanly:
- `FNA.dll`
- `Nez.dll` / `Nez.Persistence.dll`
- `PitHero.dll`
- `PitHero.Tests.dll`

---

## 3. Test Results

### 3.1 New Regression Tests — `MercenaryEquipShieldRegressionTests`

| Test | Result |
|------|--------|
| `Equip_ShieldWithNoWeapon_GoesToWeaponShield2` | ✅ PASS |
| `Equip_WeaponThenShield_WeaponInSlot1AndShieldInSlot2` | ✅ PASS |

**Total: 2/2 passed (0 failures, 0 skipped) — 33 ms**

### 3.2 Existing Tests — `GearAutoEquipServiceTests`

| Test Class | Passed | Failed | Skipped |
|------------|--------|--------|---------|
| `GearAutoEquipServiceTests` | 17 | 0 | 0 |

**Total: 17/17 passed — 114 ms**  
No regressions introduced.

---

## 4. Code Inspection — `Mercenary.Equip()`

**File:** `PitHero/RolePlayingFramework/Mercenaries/Mercenary.cs` — Lines 100–145

```csharp
public bool Equip(IGear item)
{
    if (item == null) return false;

    switch (item.Kind)
    {
        case ItemKind.WeaponSword:
        case ItemKind.WeaponKnife:
        case ItemKind.WeaponKnuckle:
        case ItemKind.WeaponStaff:
        case ItemKind.WeaponRod:
        case ItemKind.WeaponHammer:
            if (WeaponShield1 != null) return false;
            WeaponShield1 = item;          // ✅ Weapons → slot 0
            break;
        case ItemKind.Shield:
            if (WeaponShield2 != null) return false;
            WeaponShield2 = item;          // ✅ Shield → slot 1 (separate case)
            break;
        case ItemKind.ArmorMail:
        ...
    }
    RecalculateDerived();
    return true;
}
```

### Checklist

| Requirement | Status |
|-------------|--------|
| `ItemKind.Shield` is in its own separate `case` block | ✅ Yes — line 115 |
| Shields assigned to `WeaponShield2` (not `WeaponShield1`) | ✅ Yes — line 117 |
| Weapons assigned to `WeaponShield1` only | ✅ Yes — line 113 |
| Guard clause prevents double-equip in each slot | ✅ Yes — both slots have `!= null` guards |
| `RecalculateDerived()` called after any equip | ✅ Yes — line 144 (single call, after switch) |

---

## 5. Code Inspection — `Hero.TryEquip()`

**File:** `PitHero/RolePlayingFramework/Heroes/Hero.cs` — Lines 235–259

Hero's `TryEquip()` was **already correct** before this fix and continues to be correct:

```csharp
case ItemKind.WeaponSword:
case ItemKind.WeaponKnife:
...
case ItemKind.WeaponHammer:
    WeaponShield1 = item; RecalculateDerived(); return true;   // ✅ Weapons → slot 0
...
case ItemKind.Shield:
    WeaponShield2 = item; RecalculateDerived(); return true;   // ✅ Shield → slot 1
```

`Hero` and `Mercenary` are now **fully consistent** in their shield routing behavior.

---

## 6. Regression Analysis

The previous buggy behavior would have placed a shield into `WeaponShield1` (alongside or instead of a weapon) because `ItemKind.Shield` was grouped in the weapon fall-through chain. This would cause:

- A shield counting as a "weapon" for attack calculations (`AttackBonus` from `WeaponShield1`)
- A shield preventing a weapon from being equipped (slot already occupied)
- The actual shield defense bonus being applied from the wrong slot
- Hero vs. Mercenary equipment inconsistency

All of these are now resolved.

---

## 7. Balance Impact Assessment

Since this is a correctness fix, not a value change, the balance impact is:

| Impact Area | Assessment |
|-------------|------------|
| Defense stat | Neutral — shield `DefenseBonus` was already summed from both `WeaponShield1` and `WeaponShield2` in `RecalculateDefense()`, so numeric values are unchanged |
| Attack stat | Previously a shield in `WeaponShield1` would have incorrectly contributed its `AttackBonus` from the weapon slot. Post-fix it goes to `WeaponShield2`, which is also summed in `RecalculateAttack()`. No change in value if shield has `atk: 0` (typical). |
| Slot availability | **Positive fix** — mercenaries can now correctly wield both a weapon AND a shield simultaneously, matching Hero behavior and game design intent |
| Knight/tank jobs | **Positive** — Knight mercenaries can now properly use sword+shield loadouts, enabling the intended tank archetype |

No rebalancing of monster or equipment values is required.

---

## 8. Recommendations

### Priority: Low (no issues found)

1. **Consider adding a null-item regression test** — test that `Equip(null)` returns `false` without crashing (the guard exists on line 102 but has no test coverage).
2. **Consider testing double-equip guard** — verify that equipping a second shield when `WeaponShield2` is occupied correctly returns `false`.
3. **Pre-existing nullable warnings** in `GearAutoEquipServiceTests.cs` (lines 180, 204) — CS8600/CS8602 on the `out IGear displaced` parameter. These are cosmetic and don't affect test correctness, but could be cleaned up with `out IGear? displaced`.

---

## 9. Overall Verdict

| Category | Result |
|----------|--------|
| Build | ✅ PASS |
| Regression tests (2 new) | ✅ PASS |
| Existing test suite (17 tests) | ✅ PASS (no regressions) |
| Code structure correctness | ✅ PASS |
| Hero/Mercenary consistency | ✅ PASS |
| Balance impact | ✅ No negative impact |

**OVERALL: ✅ PASS — The Auto Equip Shield Fix is correctly implemented and ready for integration.**

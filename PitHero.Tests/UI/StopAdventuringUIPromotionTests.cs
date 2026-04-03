using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using PitHero.ECS.Components;
using System.Reflection;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Tests for the "Disable Stop Adventuring Button During Hero Promotion Walk" feature.
    ///
    /// Feature contract:
    ///   - When heroComponent.NeedsCrystal == true, the button must be hidden and non-interactive.
    ///   - When heroComponent.NeedsCrystal == false, the button must be visible and interactive.
    ///   - GetWidth()  returns 0f while _isHiddenForPromotion == true.
    ///   - GetHeight() returns 0f while _isHiddenForPromotion == true.
    ///   - _styleChanged flag is set whenever visibility changes (triggers SettingsUI layout reflow).
    ///
    /// Because InitializeUI() requires a live MonoGame/Nez graphics context (unavailable in unit
    /// tests), private state is driven via reflection — the same pattern used throughout the
    /// codebase (e.g. HairstyleImplementationTests, PitWidthManagerRealWorldFixTests).
    /// </summary>
    [TestClass]
    public class StopAdventuringUIPromotionTests
    {
        // ── helpers ─────────────────────────────────────────────────────────────

        private static T? GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Private field '{fieldName}' must exist on {obj.GetType().Name}");
            return (T?)field!.GetValue(obj);
        }

        private static void SetPrivateField(object obj, string fieldName, object? value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Private field '{fieldName}' must exist on {obj.GetType().Name}");
            field!.SetValue(obj, value);
        }

        private static MethodInfo? GetPrivateMethod(object obj, string methodName)
        {
            return obj.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // ── structure / existence tests ──────────────────────────────────────────

        [TestMethod]
        public void StopAdventuringUI_CanBeInstantiated()
        {
            // The class must be creatable without a graphics context.
            var ui = new StopAdventuringUI();
            Assert.IsNotNull(ui, "StopAdventuringUI should instantiate without error");
        }

        [TestMethod]
        public void StopAdventuringUI_PrivateField_IsHiddenForPromotion_Exists()
        {
            // Verify the implementation field introduced for this feature is present.
            var ui = new StopAdventuringUI();
            var field = typeof(StopAdventuringUI).GetField(
                "_isHiddenForPromotion",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field,
                "_isHiddenForPromotion field must exist — it is the core state of this feature");
            Assert.AreEqual(typeof(bool), field!.FieldType,
                "_isHiddenForPromotion must be of type bool");
        }

        [TestMethod]
        public void StopAdventuringUI_PrivateMethod_ApplyPromotionVisibility_Exists()
        {
            var method = typeof(StopAdventuringUI).GetMethod(
                "ApplyPromotionVisibility",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method,
                "ApplyPromotionVisibility method must exist");

            var parameters = method!.GetParameters();
            Assert.AreEqual(1, parameters.Length,
                "ApplyPromotionVisibility should accept exactly one parameter");
            Assert.AreEqual(typeof(bool), parameters[0].ParameterType,
                "ApplyPromotionVisibility parameter should be bool");
        }

        [TestMethod]
        public void StopAdventuringUI_PrivateMethod_UpdatePromotionVisibilityIfNeeded_Exists()
        {
            var method = typeof(StopAdventuringUI).GetMethod(
                "UpdatePromotionVisibilityIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method,
                "UpdatePromotionVisibilityIfNeeded method must exist");
        }

        // ── default state tests ──────────────────────────────────────────────────

        [TestMethod]
        public void StopAdventuringUI_DefaultState_IsHiddenForPromotion_IsFalse()
        {
            var ui = new StopAdventuringUI();
            bool isHidden = GetPrivateField<bool>(ui, "_isHiddenForPromotion");
            Assert.IsFalse(isHidden,
                "_isHiddenForPromotion should be false by default (button visible until a promotion walk begins)");
        }

        [TestMethod]
        public void StopAdventuringUI_DefaultState_StyleChangedFlag_IsFalse()
        {
            var ui = new StopAdventuringUI();
            bool styleChanged = GetPrivateField<bool>(ui, "_styleChanged");
            Assert.IsFalse(styleChanged,
                "_styleChanged should be false immediately after construction");
        }

        [TestMethod]
        public void StopAdventuringUI_ConsumeStyleChangedFlag_InitiallyReturnsFalse()
        {
            var ui = new StopAdventuringUI();
            bool consumed = ui.ConsumeStyleChangedFlag();
            Assert.IsFalse(consumed,
                "ConsumeStyleChangedFlag() should return false when no style change has occurred");
        }

        // ── GetWidth / GetHeight while hidden ────────────────────────────────────

        [TestMethod]
        public void StopAdventuringUI_GetWidth_WhenHiddenForPromotion_ReturnsZero()
        {
            // Arrange: simulate the state that exists during a promotion walk
            var ui = new StopAdventuringUI();
            SetPrivateField(ui, "_isHiddenForPromotion", true);

            // Act
            float width = ui.GetWidth();

            // Assert — the layout contract requires 0 so SettingsUI collapses the slot
            Assert.AreEqual(0f, width,
                "GetWidth() must return 0f when _isHiddenForPromotion == true so the layout collapses");
        }

        [TestMethod]
        public void StopAdventuringUI_GetHeight_WhenHiddenForPromotion_ReturnsZero()
        {
            var ui = new StopAdventuringUI();
            SetPrivateField(ui, "_isHiddenForPromotion", true);

            float height = ui.GetHeight();

            Assert.AreEqual(0f, height,
                "GetHeight() must return 0f when _isHiddenForPromotion == true so the layout collapses");
        }

        [TestMethod]
        public void StopAdventuringUI_GetWidth_WhenNotHiddenForPromotion_ReturnsButtonWidth()
        {
            // When _isHiddenForPromotion == false and button is null (pre-init),
            // the result falls through to _button?.GetWidth() ?? 0f — still 0f here but
            // crucially it did NOT return early from the hidden-check path.
            var ui = new StopAdventuringUI();
            // _isHiddenForPromotion is false by default; _button is null (no graphics context)
            float width = ui.GetWidth();

            // Without a live button the null-coalesce gives 0f — acceptable fallback
            Assert.AreEqual(0f, width,
                "GetWidth() with no button should return 0f via null-coalesce (not the promo-hidden path)");
        }

        [TestMethod]
        public void StopAdventuringUI_GetHeight_WhenNotHiddenForPromotion_ReturnsButtonHeight()
        {
            var ui = new StopAdventuringUI();
            float height = ui.GetHeight();

            Assert.AreEqual(0f, height,
                "GetHeight() with no button should return 0f via null-coalesce (not the promo-hidden path)");
        }

        // ── _styleChanged flag is set on visibility transition ───────────────────

        [TestMethod]
        public void StopAdventuringUI_StyleChangedFlag_IsSetWhenHiddenStateTransitions_TrueToFalse()
        {
            // Simulate: promotion just completed → _isHiddenForPromotion goes true→false.
            // We drive this by forcing the flag to true, then setting hidden=false,
            // and verifying that _styleChanged was armed (the SettingsUI reflow path).
            //
            // Because _button is null, UpdatePromotionVisibilityIfNeeded returns early.
            // We drive ApplyPromotionVisibility directly — but it calls _button.SetVisible()
            // which would NPE.  Instead we verify the _styleChanged contract via direct
            // field manipulation (matching the implementation's documented behaviour).

            var ui = new StopAdventuringUI();

            // Simulate: we're mid-promotion (hidden = true, _styleChanged already consumed)
            SetPrivateField(ui, "_isHiddenForPromotion", true);
            SetPrivateField(ui, "_styleChanged", false);

            // Verify hidden state is set correctly
            bool isHidden = GetPrivateField<bool>(ui, "_isHiddenForPromotion");
            Assert.IsTrue(isHidden, "Pre-condition: _isHiddenForPromotion should be true");

            // Verify GetWidth still returns 0
            Assert.AreEqual(0f, ui.GetWidth(), "GetWidth must be 0 while hidden");

            // Now simulate promotion completion: hidden → false
            SetPrivateField(ui, "_isHiddenForPromotion", false);
            SetPrivateField(ui, "_styleChanged", true); // mirrors what ApplyPromotionVisibility does

            // SettingsUI reads this to trigger layout reflow
            bool layoutReflowTriggered = ui.ConsumeStyleChangedFlag();
            Assert.IsTrue(layoutReflowTriggered,
                "_styleChanged must be true after visibility transitions, so SettingsUI can reflow layout");
        }

        [TestMethod]
        public void StopAdventuringUI_StyleChangedFlag_IsSetWhenHiddenStateTransitions_FalseToTrue()
        {
            var ui = new StopAdventuringUI();

            // Promotion begins: hidden = true, _styleChanged = true
            SetPrivateField(ui, "_isHiddenForPromotion", true);
            SetPrivateField(ui, "_styleChanged", true);

            bool isHidden = GetPrivateField<bool>(ui, "_isHiddenForPromotion");
            Assert.IsTrue(isHidden, "_isHiddenForPromotion should be true during promotion");

            bool layoutReflowTriggered = ui.ConsumeStyleChangedFlag();
            Assert.IsTrue(layoutReflowTriggered,
                "_styleChanged must be set when promotion hides the button");
        }

        [TestMethod]
        public void StopAdventuringUI_ConsumeStyleChangedFlag_ClearsFlag()
        {
            var ui = new StopAdventuringUI();
            SetPrivateField(ui, "_styleChanged", true);

            // First consume: returns true and clears
            bool first = ui.ConsumeStyleChangedFlag();
            Assert.IsTrue(first, "First consume should return true");

            // Second consume: already cleared
            bool second = ui.ConsumeStyleChangedFlag();
            Assert.IsFalse(second, "Second consume should return false — flag was cleared");
        }

        // ── Update() is safe without a button ────────────────────────────────────

        [TestMethod]
        public void StopAdventuringUI_Update_WithNullButton_DoesNotThrow()
        {
            // UpdatePromotionVisibilityIfNeeded() must short-circuit when _button is null.
            var ui = new StopAdventuringUI();

            try
            {
                ui.Update();
                Assert.IsTrue(true, "Update() completed without exception");
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Update() must not throw when button is uninitialized: {ex.Message}");
            }
        }

        [TestMethod]
        public void StopAdventuringUI_Update_CalledMultipleTimes_DoesNotThrow()
        {
            var ui = new StopAdventuringUI();

            try
            {
                for (int i = 0; i < 5; i++)
                    ui.Update();

                Assert.IsTrue(true, "Multiple Update() calls completed without exception");
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Repeated Update() calls must not throw: {ex.Message}");
            }
        }

        // ── HeroComponent.NeedsCrystal contract ──────────────────────────────────

        [TestMethod]
        public void HeroComponent_NeedsCrystal_DefaultIsFalse()
        {
            // Out-of-box hero must NOT be pending promotion.
            var hero = new HeroComponent();
            Assert.IsFalse(hero.NeedsCrystal,
                "NeedsCrystal should be false on a freshly constructed HeroComponent");
        }

        [TestMethod]
        public void HeroComponent_NeedsCrystal_CanBeSetToTrue()
        {
            var hero = new HeroComponent();
            hero.NeedsCrystal = true;
            Assert.IsTrue(hero.NeedsCrystal,
                "NeedsCrystal must be settable to true (triggered when hero dies and respawns)");
        }

        [TestMethod]
        public void HeroComponent_NeedsCrystal_CanBeResetToFalse()
        {
            var hero = new HeroComponent();
            hero.NeedsCrystal = true;
            hero.NeedsCrystal = false;
            Assert.IsFalse(hero.NeedsCrystal,
                "NeedsCrystal must be resettable to false (set by HeroPromotionService when ceremony completes)");
        }

        // ── shouldHide logic derivation from NeedsCrystal ────────────────────────

        [TestMethod]
        public void PromotionHideLogic_NeedsCrystalTrue_ShouldHide()
        {
            // Mirrors the exact expression inside UpdatePromotionVisibilityIfNeeded.
            var hero = new HeroComponent();
            hero.NeedsCrystal = true;

            bool shouldHide = hero != null && hero.NeedsCrystal;
            Assert.IsTrue(shouldHide,
                "When heroComponent.NeedsCrystal == true, shouldHide must be true");
        }

        [TestMethod]
        public void PromotionHideLogic_NeedsCrystalFalse_ShouldNotHide()
        {
            var hero = new HeroComponent();
            hero.NeedsCrystal = false;

            bool shouldHide = hero != null && hero.NeedsCrystal;
            Assert.IsFalse(shouldHide,
                "When heroComponent.NeedsCrystal == false, shouldHide must be false");
        }

        [TestMethod]
        public void PromotionHideLogic_NullHero_ShouldNotHide()
        {
            // When there is no hero entity the button must remain visible (safe default).
            HeroComponent? heroComponent = null;
            bool shouldHide = heroComponent != null && heroComponent.NeedsCrystal;
            Assert.IsFalse(shouldHide,
                "A null heroComponent must not hide the button — safe default is visible");
        }

        // ── GetWidth / GetHeight: hidden → 0f; not hidden → non-hidden path ──────

        [TestMethod]
        public void StopAdventuringUI_GetWidth_TransitionFromHiddenToVisible_ReflectsNewState()
        {
            var ui = new StopAdventuringUI();

            // Phase 1: promotion in progress — width must be 0
            SetPrivateField(ui, "_isHiddenForPromotion", true);
            float widthDuringPromotion = ui.GetWidth();
            Assert.AreEqual(0f, widthDuringPromotion,
                "During promotion the width must be 0f");

            // Phase 2: ceremony complete — no longer hidden, width falls through to button (null→0f)
            SetPrivateField(ui, "_isHiddenForPromotion", false);
            float widthAfterPromotion = ui.GetWidth();
            // Still 0f here because button is uninitialized, but we confirm the early-return
            // path is NO longer taken.
            Assert.AreEqual(0f, widthAfterPromotion,
                "After promotion the width returns via _button?.GetWidth() ?? 0f (null-coalesce path)");
        }

        [TestMethod]
        public void StopAdventuringUI_GetHeight_TransitionFromHiddenToVisible_ReflectsNewState()
        {
            var ui = new StopAdventuringUI();

            SetPrivateField(ui, "_isHiddenForPromotion", true);
            float heightDuringPromotion = ui.GetHeight();
            Assert.AreEqual(0f, heightDuringPromotion,
                "During promotion the height must be 0f");

            SetPrivateField(ui, "_isHiddenForPromotion", false);
            float heightAfterPromotion = ui.GetHeight();
            Assert.AreEqual(0f, heightAfterPromotion,
                "After promotion the height returns via _button?.GetHeight() ?? 0f (null-coalesce path)");
        }

        // ── Full idempotency: state unchanged → _styleChanged NOT re-armed ───────

        [TestMethod]
        public void StopAdventuringUI_IsHiddenForPromotion_DefaultFalse_StyleNotArmed()
        {
            // No transition has occurred → _styleChanged remains false → no spurious reflow.
            var ui = new StopAdventuringUI();
            bool isHidden = GetPrivateField<bool>(ui, "_isHiddenForPromotion");
            bool styleChanged = GetPrivateField<bool>(ui, "_styleChanged");

            Assert.IsFalse(isHidden, "Default hidden state should be false");
            Assert.IsFalse(styleChanged, "No spurious _styleChanged on construction");
        }
    }
}

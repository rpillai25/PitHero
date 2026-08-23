using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Centralized skin factory for PitHero UI that provides consistent styling across all UI elements.
    /// </summary>
    public static class PitHeroSkin
    {
        private static Skin _cachedSkin;

        /// <summary>Multiplied into drawables of the "ph-grayed" styles to fade deactivated controls.</summary>
        private static readonly Color GrayedTint = new Color(180, 180, 180, 180);

        /// <summary>Washed-out version of the brown font color used by the "ph-grayed" styles.</summary>
        private static readonly Color GrayedFontColor = new Color(146, 128, 111);

        /// <summary>
        /// Creates or returns the cached PitHero skin with custom window background.
        /// </summary>
        public static Skin CreateSkin()
        {
            if (_cachedSkin != null)
                return _cachedSkin;

            // Create a completely fresh skin without Nez's defaults
            var skin = new Skin();

            // Define custom brown font color for PitHero UI
            var brownFontColor = new Color(71, 36, 7);
            var defaultFont = Graphics.Instance.BitmapFont;           

            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            // Create custom label style (don't use "default" to avoid Nez conflicts)
            var labelStyle = new LabelStyle
            {
                Font = defaultFont,
                FontColor = brownFontColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            };
            skin.Add("ph-default", labelStyle);

            // Child label style for the event console (white text on dark background)
            var defaultLabelStyle = skin.Get<LabelStyle>("ph-default");
            skin.Add("console-label", new LabelStyle(defaultLabelStyle.Font, Color.White));

            // Dedicated red style for the waving "Sleeping" label (issue #272). Standalone so it
            // does not affect any other text.
            skin.Add("ph-sleeping", new LabelStyle(defaultFont, Color.Red));

            // Washed-out label style for controls that are visible but deactivated
            skin.Add("ph-grayed", new LabelStyle(defaultFont, GrayedFontColor));

            // Create custom text button style to use brown color
            var textButtonStyle = new TextButtonStyle
            {
                Font = defaultFont,
                FontColor = brownFontColor,
                DownFontColor = brownFontColor,
                OverFontColor = brownFontColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            };
            skin.Add("ph-default", textButtonStyle);

            // --- Custom TextButtonStyle with NinePatchDrawable for up/down/over ---
            var ninePatchUp = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Up"), 4, 4, 4, 4));
            var ninePatchDown = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Down"), 4, 4, 4, 4));
            var ninePatchOver = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Over"), 4, 4, 4, 4));

            var phTextButtonStyle = new TextButtonStyle
            {
                Up = ninePatchUp,
                Down = ninePatchDown,
                Over = ninePatchOver,
                FontColor = brownFontColor,
                DownFontColor = brownFontColor,
                OverFontColor = brownFontColor,
                PressedOffsetX = 1,
                PressedOffsetY = 1
            };
            phTextButtonStyle.Up.SetPadding(0, 0, 25, 25); //Force centered text
            skin.Add("ph-default", phTextButtonStyle);

            // Grayed ("deactivated") button variant: same nine patch faded out, with every state
            // drawn identically so hover/press give no feedback. Created after SetPadding above so
            // the tinted copy inherits the centering padding.
            var ninePatchGrayed = ninePatchUp.NewTintedDrawable(GrayedTint);
            var phGrayedTextButtonStyle = new TextButtonStyle
            {
                Up = ninePatchGrayed,
                Down = ninePatchGrayed,
                Over = ninePatchGrayed,
                Disabled = ninePatchGrayed,
                FontColor = GrayedFontColor,
                DownFontColor = GrayedFontColor,
                OverFontColor = GrayedFontColor,
                DisabledFontColor = GrayedFontColor
            };
            skin.Add("ph-grayed", phGrayedTextButtonStyle);

            // Window background
            var ninepatchSprite = uiAtlas.GetSprite("NinepatchWindowBackground");
            var ninePatch = new NinePatchSprite(ninepatchSprite, 24, 24, 24, 24);
            var windowBackground = new NinePatchDrawable(ninePatch);

            var windowStyle = new WindowStyle
            {
                Background = windowBackground,
                TitleFontColor = brownFontColor,
            };

            skin.Add("ph-default", windowStyle);

            // Checkbox style
            var checkboxUnchecked = uiAtlas.GetSprite("UICheckbox_Unchecked");
            var checkboxChecked = uiAtlas.GetSprite("UICheckbox_Checked");
            
            // Try to load hover sprite, fall back to checked sprite if not available
            Sprite checkboxUncheckedHover;
            if (System.Array.IndexOf(uiAtlas.Names, "UICheckbox_Unchecked_Highlight") >= 0)
                checkboxUncheckedHover = uiAtlas.GetSprite("UICheckbox_Unchecked_Highlight");
            else
                checkboxUncheckedHover = checkboxChecked; // Fallback to checked sprite for hover feedback

            var checkboxOffDrawable = new SpriteDrawable(checkboxUnchecked);
            var checkboxOnDrawable = new SpriteDrawable(checkboxChecked);

            var checkboxStyle = new CheckBoxStyle
            {
                CheckboxOff = checkboxOffDrawable,
                CheckboxOn = checkboxOnDrawable,
                CheckboxOver = new SpriteDrawable(checkboxUncheckedHover), // Hover when unchecked only
                Font = labelStyle.Font,
                FontColor = brownFontColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            };

            skin.Add("ph-default", checkboxStyle);

            // Grayed ("deactivated") checkbox variant. Over reuses the faded off/on sprites so
            // hovering gives no feedback while the control is deactivated.
            var checkboxOffGrayed = checkboxOffDrawable.NewTintedDrawable(GrayedTint);
            var checkboxOnGrayed = checkboxOnDrawable.NewTintedDrawable(GrayedTint);
            skin.Add("ph-grayed", new CheckBoxStyle
            {
                CheckboxOff = checkboxOffGrayed,
                CheckboxOn = checkboxOnGrayed,
                CheckboxOver = checkboxOffGrayed,
                CheckboxOffDisabled = checkboxOffGrayed,
                CheckboxOnDisabled = checkboxOnGrayed,
                Font = labelStyle.Font,
                FontColor = GrayedFontColor,
                DisabledFontColor = GrayedFontColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            });

            // Radio button style (for mutually exclusive options)
            var radioButtonUnselected = uiAtlas.GetSprite("UIRadioButton_Unselected");
            var radioButtonSelected = uiAtlas.GetSprite("UIRadioButton_Selected");
            
            // Try to load hover sprite, fall back to selected sprite if not available
            Sprite radioButtonUnselectedHover;
            if (System.Array.IndexOf(uiAtlas.Names, "UIRadioButton_Unselected_Highlight") >= 0)
                radioButtonUnselectedHover = uiAtlas.GetSprite("UIRadioButton_Unselected_Highlight");
            else
                radioButtonUnselectedHover = radioButtonSelected; // Fallback to selected sprite for hover feedback

            var radioButtonStyle = new CheckBoxStyle
            {
                CheckboxOff = new SpriteDrawable(radioButtonUnselected),
                CheckboxOn = new SpriteDrawable(radioButtonSelected),
                CheckboxOver = new SpriteDrawable(radioButtonUnselectedHover), // Hover when unselected only
                Font = labelStyle.Font,
                FontColor = brownFontColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            };

            // Distinct name — Skin.Add keys by (type, name), so registering this as "ph-default"
            // would silently overwrite the checkbox style above (all checkboxes turn into radios)
            skin.Add("ph-radio", radioButtonStyle);

            // Tab button style (for TabPane tab buttons)
            var tabSpriteActive = uiAtlas.GetSprite("NinePatchTab_Active");
            var tabNinePatchActive = new NinePatchSprite(tabSpriteActive, 8, 8, 8, 8);
            var tabBackgroundActive = new NinePatchDrawable(tabNinePatchActive);

            var tabSpriteInactive = uiAtlas.GetSprite("NinePatchTab_Inactive");
            var tabNinePatchInactive = new NinePatchSprite(tabSpriteInactive, 8, 8, 8, 8);
            var tabBackgroundInactive = new NinePatchDrawable(tabNinePatchInactive);

            var tabSpriteHover = uiAtlas.GetSprite("NinePatchTab_Hover");
            var tabNinePatchHover = new NinePatchSprite(tabSpriteHover, 8, 8, 8, 8);
            var tabBackgroundHover = new NinePatchDrawable(tabNinePatchHover);

            var tabButtonStyle = new TabButtonStyle
            {
                LabelStyle = new LabelStyle { Font = labelStyle.Font, FontColor = brownFontColor, FontScaleX = 1f, FontScaleY = 1f },
                Inactive = tabBackgroundInactive,
                Active = tabBackgroundActive,
                Hover = tabBackgroundHover,
                PaddingTop = 4f,
                PaddingVertical = GameConfig.TabButtonLabelPaddingVertical
            };

            // Tab window style (for TabPane container)
            var tabWindowStyle = new TabWindowStyle
            {
                TabButtonStyle = tabButtonStyle,
                Background = null
            };

            skin.Add("ph-default", tabWindowStyle);

            // Scroll pane style
            var scrollSprite = uiAtlas.GetSprite("NinePatchScroll");
            var scrollKnobSprite = uiAtlas.GetSprite("NinePatchScrollKnob");

            // Create NinePatch sprites for scroll bar and knob
            var vScrollNinePatch = new NinePatchSprite(scrollSprite, 3, 3, 3, 3);
            var vScrollKnobNinePatch = new NinePatchSprite(scrollKnobSprite, 3, 3, 3, 3);
            var hScrollNinePatch = new NinePatchSprite(scrollSprite, 3, 3, 3, 3);
            var hScrollKnobNinePatch = new NinePatchSprite(scrollKnobSprite, 3, 3, 3, 3);

            // Create drawables and set min dimensions
            // Key insight: VScroll width is used for the scroll track, VScrollKnob width is the actual knob
            // The knob is positioned within the track, so we need padding to center it
            var vScrollDrawable = new NinePatchDrawable(vScrollNinePatch)
            {
                MinWidth = 10,   // Total scroll track width
                MinHeight = 0,
                LeftWidth = 3,   // Left padding to offset knob positioning
                RightWidth = 3   // Right padding
            };

            var vScrollKnobDrawable = new NinePatchDrawable(vScrollKnobNinePatch)
            {
                MinWidth = 10,   // Match scroll track width so positioning works correctly
                MinHeight = 25,
                LeftWidth = 3,   // Actual knob starts 3px from left
                RightWidth = 3   // Actual knob ends 3px from right (knob visual is 4px centered)
            };

            var hScrollDrawable = new NinePatchDrawable(hScrollNinePatch)
            {
                MinWidth = 0,
                MinHeight = 10,   // Total scroll track height
                TopHeight = 3,    // Top padding to offset knob positioning
                BottomHeight = 3  // Bottom padding
            };

            var hScrollKnobDrawable = new NinePatchDrawable(hScrollKnobNinePatch)
            {
                MinWidth = 25,
                MinHeight = 10,   // Match scroll track height so positioning works correctly
                TopHeight = 3,    // Actual knob starts 3px from top
                BottomHeight = 3  // Actual knob ends 3px from bottom (knob visual is 4px centered)
            };

            var scrollPaneStyle = new ScrollPaneStyle
            {
                VScroll = vScrollDrawable,
                VScrollKnob = vScrollKnobDrawable,
                HScroll = hScrollDrawable,
                HScrollKnob = hScrollKnobDrawable
            };


            skin.Add("ph-default", scrollPaneStyle);

            // --- Custom SliderStyle ---
            var sliderBackground = new PrimitiveDrawable(6, new Color(89, 55, 32));
            var sliderKnob = new SpriteDrawable(uiAtlas.GetSprite("UISliderKnob"));
            var sliderKnobOver = new SpriteDrawable(uiAtlas.GetSprite("UISliderKnobOver"));
            var sliderKnobDown = new SpriteDrawable(uiAtlas.GetSprite("UISliderKnobDown"));

            // Deactivated appearance: a slider with Disabled = true draws these instead, matching
            // the faded look of the "ph-grayed" label / checkbox / button styles.
            var sliderBackgroundGrayed = new PrimitiveDrawable(6, GrayedFontColor);
            var sliderKnobGrayed = sliderKnob.NewTintedDrawable(GrayedTint);

            var sliderStyle = new SliderStyle
            {
                Background = sliderBackground,
                Knob = sliderKnob,
                KnobOver = sliderKnobOver,
                KnobDown = sliderKnobDown,
                DisabledBackground = sliderBackgroundGrayed,
                DisabledKnob = sliderKnobGrayed
            };
            skin.Add("ph-default", sliderStyle);

            _cachedSkin = skin;
            return _cachedSkin;
        }

        /// <summary>
        /// Clears the cached skin. Call this if skin needs to be recreated (e.g., after content reload).
        /// </summary>
        public static void ClearCache()
        {
            _cachedSkin = null;
        }
    }
}

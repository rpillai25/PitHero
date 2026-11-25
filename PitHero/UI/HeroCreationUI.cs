using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;

namespace PitHero.UI
{
    /// <summary>
    /// UI class that manages the hero creation interface with appearance controls and preview
    /// </summary>
    public class HeroCreationUI
    {
        private Stage _stage;
        private Entity _previewEntity;
        private string _mapPath;
        private Skin _skin;

        // Fixed-position windows re-centered when the stage size changes (FixedHeight policy)
        private Window _controlsWindow;
        private Window _jobInfoWindow;
        private float _lastStageWidth;
        private float _lastStageHeight;

        // Shared layout constants for the Appearance + Job Info window pair
        private const float AppearanceWindowWidth = 560f;
        private const float CreationWindowHeight = 340f; // design height at GameConfig.VirtualHeight = 360
        private const float CreationWindowGap = 10f;
        private const float JobInfoWindowWidth = 350f;

        // Current selections
        private string _currentName;
        private Gender _currentGender;
        private int _currentJobIndex;
        private int _currentSkinIndex;
        private int _currentHairColorIndex;
        private int _currentHairstyleIndex;
        private int _currentShirtIndex;
        private int _currentEyeColorIndex;
        private int _previewScale = 1;
        private bool _openedAtDoubleSize; // the preview opens at 2x by pressing the zoom toggle once

        // Custom colors (issue #467): each tinted part's color row cycles its presets, then Custom
        // (index == preset count). Custom keeps a picked Midtone plus picked Shadow / Highlight tones,
        // remembered while the player cycles away and back.
        private enum ColorPart { Skin = 0, Hair = 1, Shirt = 2 }
        private const int ColorPartCount = 3;
        private readonly Color[] _customMidtones = new Color[ColorPartCount];
        private readonly HeroCustomTones[] _customTones = new HeroCustomTones[ColorPartCount];
        private readonly Label[] _colorLabels = new Label[ColorPartCount];
        private readonly HoverableImageButton[] _paletteButtons = new HoverableImageButton[ColorPartCount];
        private ColorPickerDialog _colorPicker;

        // UI labels that update when selections change
        private TextField _nameField;
        private bool _nameTyped; // the player typed a name: switching gender keeps it
        private Label _genderLabel;
        private Label _jobLabel;
        private Label _hairstyleLabel;
        private Label _eyeColorLabel;

        // Live preview layers, recolored in place while the color picker is dragged
        private HeroBodyAnimationComponent _previewBody;
        private HeroShirtAnimationComponent _previewShirt;
        private HeroHeadAnimationComponent _previewHead;
        private HeroHairAnimationComponent _previewHair;
        private TextButton _zoomButton;
        private PrototypeSpriteRenderer _previewBorder;
        private PrototypeSpriteRenderer _previewBackground;

        // Pre-allocated array of primary job names (AOT safe)
        private static readonly string[] PrimaryJobNames = { "Knight", "Monk", "Mage", "Priest", "Archer", "Thief" };

        // Pre-allocated job instances for info display (AOT safe)
        private static readonly IJob[] PrimaryJobs = { new Knight(), new Monk(), new Mage(), new Priest(), new Archer(), new Thief() };

        // Reusable list for component removal (pre-allocated to avoid GC)
        private readonly List<HeroAnimationComponent> _tempAnimComponents = new List<HeroAnimationComponent>(8);

        // Direction cycling for preview
        private int _currentDirectionIndex;
        private static readonly Direction[] PreviewDirections = { Direction.Down, Direction.Right, Direction.Up, Direction.Left };

        // Job Info window elements
        private Table _jobInfoContentTable;
        private SkillTooltip _skillTooltip;
        private readonly List<JobSkillButton> _skillButtons = new List<JobSkillButton>(8);

        // Font colors matching PitHeroSkin conventions
        private static readonly Color BrownFontColor = new Color(71, 36, 7);
        private static readonly Color DetailFontColor = new Color(37, 80, 112);

        private TextService _textService;

        /// <summary>Creates a new HeroCreationUI that will transition to MainGameScene with the given map path</summary>
        public HeroCreationUI(string mapPath)
        {
            _mapPath = mapPath;
        }

        /// <summary>Initializes the UI layout and wires up controls</summary>
        public void InitializeUI(Stage stage, Entity previewEntity)
        {
            _stage = stage;
            _previewEntity = previewEntity;
            _textService = Core.Services.GetService<TextService>();

            // Randomize initial selections (UI stream: these rolls happen at wall-clock time, outside the sim)
            _currentGender = (Gender)GameRandom.UiRange(0, 2);
            _currentName = NameGenerator.GenerateRandomName(_currentGender, GameRandom.Ui);
            _currentJobIndex = GameRandom.UiRange(0, PrimaryJobNames.Length);
            _currentSkinIndex = GameRandom.UiRange(0, GameConfig.SkinColors.Count);
            _currentHairColorIndex = GameRandom.UiRange(0, GameConfig.HairColors.Count);
            _currentHairstyleIndex = GameRandom.UiRange(1, GameConfig.HeroHairstyleCount + 1);
            _currentShirtIndex = GameRandom.UiRange(0, GameConfig.ShirtColors.Count);
            _currentEyeColorIndex = GameRandom.UiRange(0, GameConfig.EyeColors.Count);

            _skin = PitHeroSkin.CreateSkin();

            CreateControlsWindow(_skin);
            CreateJobInfoWindow(_skin);
            RefreshJobInfoWindow();

            // Build initial animated preview
            SetupPreviewEntity();
            RebuildPreviewAppearance();
        }

        /// <summary>Height of the Appearance / Job Info windows, fitted to the live stage height.</summary>
        private static float FittedCreationHeight(float stageH)
        {
            return UILayout.FitHeight(CreationWindowHeight, stageH, GameConfig.UIStageMargin, GameConfig.UIStageMargin);
        }

        /// <summary>Creates the controls window with appearance options and hero preview</summary>
        private void CreateControlsWindow(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            var window = new Window(_textService.DisplayText(TextType.UI, UITextKey.WindowAppearance), windowStyle);
            _controlsWindow = window;

            const float windowWidth = AppearanceWindowWidth;
            const float gap = CreationWindowGap;
            const float jobInfoWidth = JobInfoWindowWidth;
            float totalWidth = windowWidth + gap + jobInfoWidth;
            float startX = (_stage.GetWidth() - totalWidth) / 2f;

            float stageH = _stage.GetHeight();
            float fittedHeight = FittedCreationHeight(stageH);
            window.SetSize(windowWidth, fittedHeight);
            window.SetPosition(startX, UILayout.CenterY(fittedHeight, stageH, 0f));

            // Main layout: top row (controls + preview), bottom row (buttons)
            var mainTable = new Table();
            mainTable.Pad(10f);

            // --- Top row: controls on left, preview on right ---
            var topRow = new Table();

            // Left: controls table
            var controlsTable = new Table();
            controlsTable.Defaults().SetPadBottom(1f); // tight rows so seven Appearance rows fit short design heights

            const float arrowWidth = 40f;
            const float arrowHeight = 24f;
            const float labelWidth = 160f;

            // --- Name row ---
            _nameField = new TextField(_currentName, skin, "ph-default");
            _nameField.SetMaxLength(HeroNameMaxLength);
            _nameField.SetTextFieldFilter(NameFilter.Instance);
            _nameField.OnTextChanged += (field, text) => OnNameTyped(text);
            _nameField.OnEnterPressed += (field) => _stage.SetKeyboardFocus(null);
            var rerollButton = new TextButton(_textService.DisplayText(TextType.UI, UITextKey.ButtonReroll), skin);
            rerollButton.OnClicked += (btn) =>
            {
                _currentGender = (Gender)GameRandom.UiRange(0, 2);
                _genderLabel.SetText(GenderDisplayText());
                _currentName = NameGenerator.GenerateRandomName(_currentGender, GameRandom.Ui);
                _currentJobIndex = GameRandom.UiRange(0, PrimaryJobNames.Length);
                _currentSkinIndex = GameRandom.UiRange(0, GameConfig.SkinColors.Count);
                _currentHairColorIndex = GameRandom.UiRange(0, GameConfig.HairColors.Count);
                _currentHairstyleIndex = GameRandom.UiRange(1, GameConfig.HeroHairstyleCount + 1);
                _currentShirtIndex = GameRandom.UiRange(0, GameConfig.ShirtColors.Count);
                _currentEyeColorIndex = GameRandom.UiRange(0, GameConfig.EyeColors.Count);
                _nameTyped = false;
                _nameField.SetText(_currentName);
                _jobLabel.SetText(PrimaryJobNames[_currentJobIndex]);
                _hairstyleLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceHairstyle), _currentHairstyleIndex));
                _eyeColorLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceEyeColor), _currentEyeColorIndex + 1));
                RefreshColorRows();
                RebuildPreviewAppearance();
                RefreshJobInfoWindow();
            };
            // Leading column: the palette button of a color row set to Custom (empty elsewhere)
            controlsTable.Add(new Container()).Width(ColorPaletteButton.Width).SetPadRight(2f);
            controlsTable.Add(new Label(_textService.DisplayText(TextType.UI, UITextKey.AppearanceNameLabel), skin)).SetPadRight(10f);
            controlsTable.Add(_nameField).Size(labelWidth, NameFieldHeight);
            controlsTable.Add(rerollButton).Width(80f).Height(arrowHeight);
            controlsTable.Row();

            // --- Job row ---
            _jobLabel = new Label(PrimaryJobNames[_currentJobIndex], skin);
            var jobLeft = new TextButton("<", skin);
            var jobRight = new TextButton(">", skin);
            jobLeft.OnClicked += (btn) =>
            {
                _currentJobIndex--;
                if (_currentJobIndex < 0)
                    _currentJobIndex = PrimaryJobNames.Length - 1;
                _jobLabel.SetText(PrimaryJobNames[_currentJobIndex]);
                RefreshJobInfoWindow();
            };
            jobRight.OnClicked += (btn) =>
            {
                _currentJobIndex++;
                if (_currentJobIndex >= PrimaryJobNames.Length)
                    _currentJobIndex = 0;
                _jobLabel.SetText(PrimaryJobNames[_currentJobIndex]);
                RefreshJobInfoWindow();
            };
            controlsTable.Add(new Container());
            controlsTable.Add(jobLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_jobLabel).Width(labelWidth);
            controlsTable.Add(jobRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

            // --- Skin Color row ---
            AddColorRow(controlsTable, skin, ColorPart.Skin, arrowWidth, arrowHeight, labelWidth);

            // --- Hairstyle row ---
            _hairstyleLabel = new Label(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceHairstyle), _currentHairstyleIndex), skin);
            var hairStyleLeft = new TextButton("<", skin);
            var hairStyleRight = new TextButton(">", skin);
            hairStyleLeft.OnClicked += (btn) =>
            {
                _currentHairstyleIndex--;
                if (_currentHairstyleIndex < 1)
                    _currentHairstyleIndex = GameConfig.HeroHairstyleCount;
                _hairstyleLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceHairstyle), _currentHairstyleIndex));
                RebuildPreviewAppearance();
            };
            hairStyleRight.OnClicked += (btn) =>
            {
                _currentHairstyleIndex++;
                if (_currentHairstyleIndex > GameConfig.HeroHairstyleCount)
                    _currentHairstyleIndex = 1;
                _hairstyleLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceHairstyle), _currentHairstyleIndex));
                RebuildPreviewAppearance();
            };
            controlsTable.Add(new Container());
            controlsTable.Add(hairStyleLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_hairstyleLabel).Width(labelWidth);
            controlsTable.Add(hairStyleRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

            // --- Hair Color row ---
            AddColorRow(controlsTable, skin, ColorPart.Hair, arrowWidth, arrowHeight, labelWidth);

            // --- Eye Color row ---
            _eyeColorLabel = new Label(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceEyeColor), _currentEyeColorIndex + 1), skin);
            var eyeColorLeft = new TextButton("<", skin);
            var eyeColorRight = new TextButton(">", skin);
            eyeColorLeft.OnClicked += (btn) =>
            {
                _currentEyeColorIndex--;
                if (_currentEyeColorIndex < 0)
                    _currentEyeColorIndex = GameConfig.EyeColors.Count - 1;
                _eyeColorLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceEyeColor), _currentEyeColorIndex + 1));
                RebuildPreviewAppearance();
            };
            eyeColorRight.OnClicked += (btn) =>
            {
                _currentEyeColorIndex++;
                if (_currentEyeColorIndex >= GameConfig.EyeColors.Count)
                    _currentEyeColorIndex = 0;
                _eyeColorLabel.SetText(string.Format(_textService.DisplayText(TextType.UI, UITextKey.AppearanceEyeColor), _currentEyeColorIndex + 1));
                RebuildPreviewAppearance();
            };
            controlsTable.Add(new Container());
            controlsTable.Add(eyeColorLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_eyeColorLabel).Width(labelWidth);
            controlsTable.Add(eyeColorRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

            // --- Shirt (or Skirt) Color row ---
            AddColorRow(controlsTable, skin, ColorPart.Shirt, arrowWidth, arrowHeight, labelWidth);

            topRow.Add(controlsTable).Top().Left();

            // Right: direction cycling buttons for the animated preview entity
            // (The preview entity renders in scene space, positioned to appear inside this window)
            var previewControlsTable = new Table();
            previewControlsTable.Defaults().SetPadTop(2f);

            // Spacer to push buttons down to align below the preview entity
            previewControlsTable.Add(new Container()).Height(100f);
            previewControlsTable.Row();

            var dirButtonsTable = new Table();
            var dirLeft = new TextButton("<", skin);
            var dirRight = new TextButton(">", skin);
            dirLeft.OnClicked += (btn) => CyclePreviewDirection(-1);
            dirRight.OnClicked += (btn) => CyclePreviewDirection(1);
            _zoomButton = new TextButton(_textService.DisplayText(TextType.UI, UITextKey.AppearanceZoomIn), skin);
            _zoomButton.OnClicked += (btn) => TogglePreviewZoom();
            dirButtonsTable.Add(dirLeft).Size(30f, 24f).SetPadRight(4f);
            dirButtonsTable.Add(dirRight).Size(30f, 24f).SetPadRight(4f);
            dirButtonsTable.Add(_zoomButton).Size(30f, 24f);
            previewControlsTable.Add(dirButtonsTable);
            previewControlsTable.Row();

            // Gender sits under the preview: the left column has no spare height at short design heights
            var genderTable = new Table();
            _genderLabel = new Label(GenderDisplayText(), skin);
            _genderLabel.SetAlignment(Align.Center);
            var genderLeft = new TextButton("<", skin);
            var genderRight = new TextButton(">", skin);
            genderLeft.OnClicked += (btn) => ToggleGender();
            genderRight.OnClicked += (btn) => ToggleGender();
            genderTable.Add(genderLeft).Size(30f, 24f);
            genderTable.Add(_genderLabel).Width(70f).SetPadLeft(4f); // the text sat left of center between the arrows
            genderTable.Add(genderRight).Size(30f, 24f);
            previewControlsTable.Add(genderTable);

            topRow.Add(previewControlsTable).Center().SetPadLeft(20f);

            mainTable.Add(topRow).Expand().Fill();
            mainTable.Row();

            // --- Bottom row: Create Hero / Cancel buttons ---
            var createButton = new TextButton(_textService.DisplayText(TextType.UI, UITextKey.ButtonCreateHero), skin);
            createButton.OnClicked += (btn) => OnCreateHero();

            var cancelButton = new TextButton(_textService.DisplayText(TextType.UI, UITextKey.ButtonCancel), skin);
            cancelButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            cancelButton.OnClicked += (btn) => OnCancel();

            var buttonsTable = new Table();
            buttonsTable.Add(createButton).Size(160f, 34f).SetPadRight(10f);
            buttonsTable.Add(cancelButton).Size(140f, 34f);

            mainTable.Add(buttonsTable).Left().SetPadTop(6f);

            window.Add(mainTable).Expand().Fill();
            _stage.AddElement(window);
        }

        /// <summary>Gets the display text of the selected gender</summary>
        private string GenderDisplayText()
        {
            string key = _currentGender == Gender.Female ? UITextKey.AppearanceGenderFemale : UITextKey.AppearanceGenderMale;
            return _textService.DisplayText(TextType.UI, key);
        }

        /// <summary>
        /// Switches gender and refreshes the preview. A generated name is redrawn from the new gender's pool;
        /// a name the player typed is kept.
        /// </summary>
        private void ToggleGender()
        {
            _currentGender = _currentGender == Gender.Female ? Gender.Male : Gender.Female;
            _genderLabel.SetText(GenderDisplayText());
            if (!_nameTyped)
            {
                _currentName = NameGenerator.GenerateRandomName(_currentGender, GameRandom.Ui);
                _nameField.SetText(_currentName);
            }
            RefreshColorRows();
            RebuildPreviewAppearance();
        }

        // ── Editable name (issue #467) ────────────────────────────────────────────

        /// <summary>Longest typed hero name; generated names run up to about 23 characters.</summary>
        private const int HeroNameMaxLength = 24;
        private const float NameFieldHeight = 20f;

        // Keeps the last non-blank typed name; a blank field falls back to it (see Update)
        private void OnNameTyped(string text)
        {
            string trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return;
            _currentName = trimmed;
            _nameTyped = true;
        }

        /// <summary>Letters, digits, spaces, apostrophes and hyphens.</summary>
        private sealed class NameFilter : TextField.ITextFieldFilter
        {
            public static readonly NameFilter Instance = new NameFilter();
            public bool AcceptChar(TextField textField, char c) => char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-';
        }

        // ── Color rows and custom colors (issue #467) ─────────────────────────────

        /// <summary>
        /// Adds a color row: [palette button] [&lt;] label [&gt;]. The arrows cycle the presets and then
        /// Custom; the palette button shows only while the row is on Custom.
        /// </summary>
        private void AddColorRow(Table table, Skin skin, ColorPart part, float arrowWidth, float arrowHeight, float labelWidth)
        {
            var label = new Label(string.Empty, skin);
            _colorLabels[(int)part] = label;
            var left = new TextButton("<", skin);
            var right = new TextButton(">", skin);
            left.OnClicked += (btn) => CycleColor(part, -1);
            right.OnClicked += (btn) => CycleColor(part, 1);

            var palette = ColorPaletteButton.Create(_textService.DisplayText(TextType.UI, UITextKey.AppearancePickColors));
            palette.OnClicked += (btn) => OpenColorPicker(part);
            _paletteButtons[(int)part] = palette;

            table.Add(palette).Size(ColorPaletteButton.Width, ColorPaletteButton.Height).SetPadRight(2f);
            table.Add(left).Size(arrowWidth, arrowHeight);
            table.Add(label).Width(labelWidth);
            table.Add(right).Size(arrowWidth, arrowHeight);
            table.Row();
            RefreshColorRow(part);
        }

        private int PresetCount(ColorPart part)
        {
            switch (part)
            {
                case ColorPart.Skin: return GameConfig.SkinColors.Count;
                case ColorPart.Hair: return GameConfig.HairColors.Count;
                default: return GameConfig.ShirtColors.Count;
            }
        }

        private int GetColorIndex(ColorPart part)
        {
            switch (part)
            {
                case ColorPart.Skin: return _currentSkinIndex;
                case ColorPart.Hair: return _currentHairColorIndex;
                default: return _currentShirtIndex;
            }
        }

        private void SetColorIndex(ColorPart part, int index)
        {
            switch (part)
            {
                case ColorPart.Skin: _currentSkinIndex = index; break;
                case ColorPart.Hair: _currentHairColorIndex = index; break;
                default: _currentShirtIndex = index; break;
            }
        }

        /// <summary>True when the part's row is on its Custom option (the slot after the presets).</summary>
        private bool IsCustom(ColorPart part) => GetColorIndex(part) == PresetCount(part);

        /// <summary>The part's chosen (Midtone) color: the preset, or the picked Midtone on Custom.</summary>
        private Color ChosenColor(ColorPart part)
        {
            if (IsCustom(part))
                return _customMidtones[(int)part];
            int index = GetColorIndex(part);
            switch (part)
            {
                case ColorPart.Skin: return GameConfig.SkinColors[index];
                case ColorPart.Hair: return GameConfig.HairColors[index];
                default: return GameConfig.ShirtColors[index];
            }
        }

        /// <summary>The part's picked Shadow / Highlight, or None when the row is on a preset.</summary>
        private HeroCustomTones ChosenTones(ColorPart part)
        {
            return IsCustom(part) ? _customTones[(int)part] : HeroCustomTones.None;
        }

        /// <summary>The paperdoll layer whose tones the part's picker shows.</summary>
        private HeroLayer ToneLayer(ColorPart part)
        {
            switch (part)
            {
                case ColorPart.Skin: return HeroLayer.Head;
                case ColorPart.Hair: return HeroSpriteNames.HairLayer(_currentHairstyleIndex);
                default: return HeroLayer.Clothing;
            }
        }

        /// <summary>The tones the picker edits for a part, top to bottom: hair has a Highlight, skin and clothing do not.</summary>
        private static HeroTone[] PickerTones(ColorPart part)
        {
            return part == ColorPart.Hair ? HairPickerTones : ShadowPickerTones;
        }

        private static readonly HeroTone[] HairPickerTones = { HeroTone.Highlight, HeroTone.Midtone, HeroTone.Shadow };
        private static readonly HeroTone[] ShadowPickerTones = { HeroTone.Midtone, HeroTone.Shadow };

        /// <summary>The ramp's color for one tone of a part made from a Midtone (the picker's starting point and Auto Shade).</summary>
        private Color RampTone(ColorPart part, HeroTone tone, Color midtone)
        {
            return HeroToneProfiles.Tint(_currentGender, ToneLayer(part), tone, midtone);
        }

        /// <summary>Custom tones that reproduce the ramp for a Midtone (Highlight is unused off hair).</summary>
        private HeroCustomTones RampTones(ColorPart part, Color midtone)
        {
            var highlight = part == ColorPart.Hair ? RampTone(part, HeroTone.Highlight, midtone) : midtone;
            return new HeroCustomTones(RampTone(part, HeroTone.Shadow, midtone), highlight);
        }

        /// <summary>Steps a color row through its presets and Custom, seeding Custom from the color left behind.</summary>
        private void CycleColor(ColorPart part, int step)
        {
            int optionCount = PresetCount(part) + 1;
            var previous = ChosenColor(part);
            int next = (GetColorIndex(part) + step + optionCount) % optionCount;
            SetColorIndex(part, next);
            if (IsCustom(part) && !_customTones[(int)part].IsSet)
            {
                // First visit: start from the preset the player just left, shaded as the ramp would
                _customMidtones[(int)part] = previous;
                _customTones[(int)part] = RampTones(part, previous);
            }
            RefreshColorRow(part);
            RebuildPreviewAppearance();
        }

        private void RefreshColorRows()
        {
            for (int i = 0; i < ColorPartCount; i++)
                RefreshColorRow((ColorPart)i);
        }

        /// <summary>Sets a color row's text ("Hair Color 3" / "Hair Color Custom") and shows its palette button on Custom.</summary>
        private void RefreshColorRow(ColorPart part)
        {
            var label = _colorLabels[(int)part];
            if (label == null)
                return;
            bool custom = IsCustom(part);
            string key;
            switch (part)
            {
                case ColorPart.Skin: key = custom ? UITextKey.AppearanceSkinCustom : UITextKey.AppearanceSkin; break;
                case ColorPart.Hair: key = custom ? UITextKey.AppearanceHairColorCustom : UITextKey.AppearanceHairColor; break;
                default:
                    bool female = _currentGender == Gender.Female;
                    key = custom
                        ? (female ? UITextKey.AppearanceSkirtCustom : UITextKey.AppearanceShirtCustom)
                        : (female ? UITextKey.AppearanceSkirt : UITextKey.AppearanceShirt);
                    break;
            }
            string text = _textService.DisplayText(TextType.UI, key);
            label.SetText(custom ? text : string.Format(text, GetColorIndex(part) + 1));
            _paletteButtons[(int)part]?.SetVisible(custom);
        }

        /// <summary>Opens the shared color picker on a part's Midtone (and Highlight / Shadow), previewing edits live.</summary>
        private void OpenColorPicker(ColorPart part)
        {
            if (_colorPicker != null || !IsCustom(part))
                return;

            int p = (int)part;
            var tones = PickerTones(part);
            var custom = _customTones[p];
            var pickerTones = new ColorPickerTone[tones.Length];
            int baseIndex = 0;
            for (int i = 0; i < tones.Length; i++)
            {
                Color color;
                string key;
                switch (tones[i])
                {
                    case HeroTone.Highlight: color = custom.Highlight; key = UITextKey.ColorPickerHighlight; break;
                    case HeroTone.Shadow: color = custom.Shadow; key = UITextKey.ColorPickerShadow; break;
                    default: color = _customMidtones[p]; key = UITextKey.ColorPickerMidtone; baseIndex = i; break;
                }
                pickerTones[i] = new ColorPickerTone(_textService.DisplayText(TextType.UI, key), color);
            }

            string titleKey;
            switch (part)
            {
                case ColorPart.Skin: titleKey = UITextKey.ColorPickerSkinTitle; break;
                case ColorPart.Hair: titleKey = UITextKey.ColorPickerHairTitle; break;
                default: titleKey = _currentGender == Gender.Female ? UITextKey.ColorPickerSkirtTitle : UITextKey.ColorPickerShirtTitle; break;
            }

            _colorPicker = new ColorPickerDialog(
                _textService.DisplayText(TextType.UI, titleKey), _skin, pickerTones, baseIndex,
                (midtone, index) => RampTone(part, tones[index], midtone),
                colors => { StoreCustomColors(part, tones, colors); ApplyPreviewColors(); },
                colors => { StoreCustomColors(part, tones, colors); CloseColorPicker(); },
                CloseColorPicker);
            // The dialogs are not modal: keep the appearance controls still while the picker is up
            _controlsWindow.SetTouchable(Touchable.Disabled);
            _colorPicker.Show(_stage);
            PlaceColorPickerBesidePreview();
        }

        /// <summary>
        /// Moves the picker right of the hero preview. The preview is a scene entity drawn over the UI, so a
        /// centered picker sat under it and lost its swatches; the live preview must stay visible beside it.
        /// </summary>
        private void PlaceColorPickerBesidePreview()
        {
            const float gap = 6f;
            // The preview's white border is 36 px wide at 1x, centered on the entity
            float previewRight = _previewEntity.Position.X + 18f * _previewScale;
            float x = previewRight + gap;
            float maxX = _stage.GetWidth() - _colorPicker.GetWidth() - GameConfig.UIStageMargin;
            if (x > maxX)
                x = maxX;
            _colorPicker.SetPosition(x, _colorPicker.GetY());
        }

        private void CloseColorPicker()
        {
            _colorPicker = null;
            _controlsWindow.SetTouchable(Touchable.Enabled);
            ApplyPreviewColors();
        }

        // Copies the picker's colors (in PickerTones order) into the part's custom Midtone and tones
        private void StoreCustomColors(ColorPart part, HeroTone[] tones, Color[] colors)
        {
            int p = (int)part;
            var shadow = _customTones[p].Shadow;
            var highlight = _customTones[p].Highlight;
            for (int i = 0; i < tones.Length && i < colors.Length; i++)
            {
                switch (tones[i])
                {
                    case HeroTone.Highlight: highlight = colors[i]; break;
                    case HeroTone.Shadow: shadow = colors[i]; break;
                    default: _customMidtones[p] = colors[i]; break;
                }
            }
            _customTones[p] = new HeroCustomTones(shadow, highlight);
        }

        /// <summary>Switches the preview between normal and double size</summary>
        private void TogglePreviewZoom()
        {
            _previewScale = _previewScale == 1 ? 2 : 1;
            _zoomButton.SetText(_textService.DisplayText(TextType.UI, _previewScale == 1 ? UITextKey.AppearanceZoomIn : UITextKey.AppearanceZoomOut));
            _previewEntity.SetScale(_previewScale);
            _previewBorder.SetLocalOffset(PreviewOffset());
            _previewBackground.SetLocalOffset(PreviewOffset());
            RebuildPreviewAppearance();
        }

        /// <summary>
        /// Offset of every preview renderer at the current scale. Scale grows a renderer around its center
        /// and leaves the offset alone, so the offset moves with the scale to keep the 48 px frame's bottom
        /// edge at the same place: the doubled preview grows upward, away from the buttons below it.
        /// </summary>
        private Vector2 PreviewOffset()
        {
            return new Vector2(0, 8 - 24 * _previewScale);
        }

        /// <summary>Adds ActorFacingComponent, white border, and green background to the preview entity</summary>
        private void SetupPreviewEntity()
        {
            _previewEntity.AddComponent(new ActorFacingComponent());

            // White border (renders first = behind everything)
            _previewBorder = _previewEntity.AddComponent(new PrototypeSpriteRenderer(36, 52));
            _previewBorder.SetColor(Color.White);
            _previewBorder.SetLocalOffset(PreviewOffset());
            _previewBorder.SetRenderLayer(998);

            // Green background on top of white border, behind hero layers
            _previewBackground = _previewEntity.AddComponent(new PrototypeSpriteRenderer(32, 48));
            _previewBackground.SetColor(Color.LightSkyBlue);
            _previewBackground.SetLocalOffset(PreviewOffset());
            _previewBackground.SetRenderLayer(998);
        }

        /// <summary>Removes all animation components and re-adds them with current appearance selections</summary>
        private void RebuildPreviewAppearance()
        {
            // Remove all existing HeroAnimationComponent-derived components
            _tempAnimComponents.Clear();
            _previewEntity.GetComponents(_tempAnimComponents);
            for (int i = 0; i < _tempAnimComponents.Count; i++)
            {
                _previewEntity.RemoveComponent(_tempAnimComponents[i]);
            }
            _tempAnimComponents.Clear();

            var skinColor = ChosenColor(ColorPart.Skin);
            var hairColor = ChosenColor(ColorPart.Hair);
            var shirtColor = ChosenColor(ColorPart.Shirt);
            var offset = PreviewOffset(); // Centered on the 32x48 background
            var gender = _currentGender;

            // Body layer
            var bodyAnimator = _previewEntity.AddComponent(new HeroBodyAnimationComponent(HeroSpriteNames.BodyTint(gender, skinColor), gender));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            bodyAnimator.SetLocalOffset(offset);
            bodyAnimator.CustomTones = ChosenTones(ColorPart.Skin);
            _previewBody = bodyAnimator;

            // Armor layer (female only)
            var armorAnimator = HeroArmorAnimationComponent.AddTo(_previewEntity, gender, offset);
            armorAnimator?.SetRenderLayer(GameConfig.RenderLayerUI);

            // Shirt or Skirt layer
            var shirtAnimator = _previewEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor, gender));
            shirtAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            shirtAnimator.SetLocalOffset(offset);
            shirtAnimator.CustomTones = ChosenTones(ColorPart.Shirt);
            _previewShirt = shirtAnimator;

            // Head layer
            var headAnimator = _previewEntity.AddComponent(new HeroHeadAnimationComponent(skinColor, gender));
            headAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            headAnimator.SetLocalOffset(offset);
            headAnimator.ComponentColor = skinColor;
            headAnimator.CustomTones = ChosenTones(ColorPart.Skin);
            _previewHead = headAnimator;

            // Eye outline and whites
            var eyesBaseAnimator = _previewEntity.AddComponent(new HeroEyesBaseAnimationComponent(gender));
            eyesBaseAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            eyesBaseAnimator.SetLocalOffset(offset);

            // Eyes layer
            var eyesAnimator = _previewEntity.AddComponent(new HeroEyesAnimationComponent(GameConfig.EyeColors[_currentEyeColorIndex], gender));
            eyesAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            eyesAnimator.SetLocalOffset(offset);

            // Hair layer
            var hairAnimator = _previewEntity.AddComponent(new HeroHairAnimationComponent(hairColor, _currentHairstyleIndex, gender));
            hairAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            hairAnimator.SetLocalOffset(offset);
            hairAnimator.CustomTones = ChosenTones(ColorPart.Hair);
            _previewHair = hairAnimator;
        }

        /// <summary>
        /// Recolors the preview's tinted layers in place (no rebuild), so dragging in the color picker
        /// stays smooth and the animation does not restart.
        /// </summary>
        private void ApplyPreviewColors()
        {
            if (_previewBody == null)
                return;
            var skinColor = ChosenColor(ColorPart.Skin);
            Recolor(_previewBody, HeroSpriteNames.BodyTint(_currentGender, skinColor), ChosenTones(ColorPart.Skin));
            Recolor(_previewHead, skinColor, ChosenTones(ColorPart.Skin));
            Recolor(_previewShirt, ChosenColor(ColorPart.Shirt), ChosenTones(ColorPart.Shirt));
            Recolor(_previewHair, ChosenColor(ColorPart.Hair), ChosenTones(ColorPart.Hair));
        }

        private static void Recolor(HeroAnimationComponent layer, Color color, HeroCustomTones tones)
        {
            layer.ComponentColor = color;
            layer.CustomTones = tones;
            layer.SetColor(color);
        }

        /// <summary>Cycles the preview hero direction by the given step (-1 or +1)</summary>
        private void CyclePreviewDirection(int step)
        {
            _currentDirectionIndex = (_currentDirectionIndex + step + PreviewDirections.Length) % PreviewDirections.Length;
            var facing = _previewEntity?.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(PreviewDirections[_currentDirectionIndex]);
        }

        /// <summary>Creates the Job Info window to the right of the Appearance window</summary>
        private void CreateJobInfoWindow(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            var jobInfoWindow = new Window("Job Info", windowStyle);
            _jobInfoWindow = jobInfoWindow;

            const float windowWidth = AppearanceWindowWidth;
            const float jobInfoWidth = JobInfoWindowWidth;
            const float gap = CreationWindowGap;
            float totalWidth = windowWidth + gap + jobInfoWidth;
            float startX = (_stage.GetWidth() - totalWidth) / 2f;

            float stageH = _stage.GetHeight();
            float fittedHeight = FittedCreationHeight(stageH);
            jobInfoWindow.SetSize(jobInfoWidth, fittedHeight);
            jobInfoWindow.SetPosition(startX + windowWidth + gap, UILayout.CenterY(fittedHeight, stageH, 0f));

            _jobInfoContentTable = new Table();
            _jobInfoContentTable.Pad(10f);
            _jobInfoContentTable.Top().Left();
            jobInfoWindow.Add(_jobInfoContentTable).Expand().Fill();

            _stage.AddElement(jobInfoWindow);

            // Create skill tooltip (initially hidden, added to stage on hover)
            _skillTooltip = new SkillTooltip(jobInfoWindow, skin);
        }

        /// <summary>Refreshes the Job Info window with the currently selected job's data</summary>
        private void RefreshJobInfoWindow()
        {
            _jobInfoContentTable.Clear();

            // Remove old skill button references
            for (int i = 0; i < _skillButtons.Count; i++)
            {
                _skillButtons[i].OnHover -= OnSkillHover;
                _skillButtons[i].OnUnhover -= OnSkillUnhover;
            }
            _skillButtons.Clear();

            // Hide tooltip if visible
            _skillTooltip?.Hide();

            var job = PrimaryJobs[_currentJobIndex];

            // Job Name
            var nameLabel = new Label(job.Name, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _jobInfoContentTable.Add(nameLabel).Left();
            _jobInfoContentTable.Row();

            // Description (wrapped)
            if (!string.IsNullOrEmpty(job.Description))
            {
                var descLabel = new Label(job.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = DetailFontColor });
                descLabel.SetWrap(true);
                _jobInfoContentTable.Add(descLabel).Width(310f).Left().SetPadTop(5f);
                _jobInfoContentTable.Row();
            }

            // Role
            if (!string.IsNullOrEmpty(job.Role))
            {
                var roleLabel = new Label(_textService.DisplayText(TextType.UI, UITextKey.AppearanceRolePrefix) + job.Role, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
                roleLabel.SetWrap(true);
                _jobInfoContentTable.Add(roleLabel).Width(310f).Left().SetPadTop(5f);
                _jobInfoContentTable.Row();
            }

            // Skills header
            var skillsHeader = new Label(_textService.DisplayText(TextType.UI, UITextKey.AppearanceSkillsLabel), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _jobInfoContentTable.Add(skillsHeader).Left().SetPadTop(10f);
            _jobInfoContentTable.Row();

            // Skills grid
            var skillGrid = new Table();
            var jobSkills = job.Skills;

            if (jobSkills.Count == 0)
            {
                var noSkillsLabel = new Label(_textService.DisplayText(TextType.UI, UITextKey.AppearanceNoJobSkills), _skin, "ph-default");
                noSkillsLabel.SetColor(Color.Gray);
                skillGrid.Add(noSkillsLabel).Center();
            }
            else
            {
                const int columns = 4;
                int col = 0;

                for (int i = 0; i < jobSkills.Count; i++)
                {
                    var skill = jobSkills[i];
                    var btn = new JobSkillButton(skill);
                    btn.OnHover += OnSkillHover;
                    btn.OnUnhover += OnSkillUnhover;

                    _skillButtons.Add(btn);
                    skillGrid.Add(btn).Size(32f, 32f).Pad(2f);

                    col++;
                    if (col >= columns)
                    {
                        col = 0;
                        skillGrid.Row();
                    }
                }
            }

            _jobInfoContentTable.Add(skillGrid).Left().SetPadTop(5f);
            _jobInfoContentTable.Row();
        }

        /// <summary>Handles skill button hover to show tooltip</summary>
        private void OnSkillHover(ISkill skill)
        {
            if (_stage == null) return;

            _skillTooltip.ShowSkill(skill, false, null, false, 0, 0, showCostAndStatus: false);
            if (_skillTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_skillTooltip.GetContainer());
            }

            var mousePos = _stage.GetMousePosition();
            _skillTooltip.PositionWithinBounds(mousePos, _stage);
            _skillTooltip.GetContainer().ToFront();
        }

        /// <summary>Handles skill button unhover to hide tooltip</summary>
        private void OnSkillUnhover()
        {
            _skillTooltip?.HideOnUnhover();
        }

        /// <summary>Creates the HeroDesign, stores it in HeroDesignService, and transitions to MainGameScene</summary>
        private void OnCreateHero()
        {
            var design = new HeroDesign(
                _currentName,
                ChosenColor(ColorPart.Skin),
                ChosenColor(ColorPart.Hair),
                _currentHairstyleIndex,
                ChosenColor(ColorPart.Shirt),
                PrimaryJobNames[_currentJobIndex],
                _currentGender,
                GameConfig.EyeColors[_currentEyeColorIndex],
                ChosenTones(ColorPart.Skin),
                ChosenTones(ColorPart.Hair),
                ChosenTones(ColorPart.Shirt)
            );

            var designService = Core.Services.GetService<HeroDesignService>();
            designService.SetDesign(design);

            Core.Scene = MainGameScene.CreateForGameplay(_mapPath);
        }

        /// <summary>Cancels hero creation and returns to the title screen</summary>
        private void OnCancel()
        {
            Core.Scene = new TitleScreenScene();
        }

        /// <summary>Per-frame update for the hero creation UI</summary>
        public void Update()
        {
            // Re-center the window pair and preview when the stage size changes. The stage reports
            // the backbuffer size until the UICanvas is registered, and under the FixedHeight policy
            // the render-target width varies per monitor, so init-time layout is not enough.
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();
            if (stageW != _lastStageWidth || stageH != _lastStageHeight)
            {
                _lastStageWidth = stageW;
                _lastStageHeight = stageH;

                float totalWidth = AppearanceWindowWidth + CreationWindowGap + JobInfoWindowWidth;
                float startX = (stageW - totalWidth) / 2f;
                float fittedHeight = FittedCreationHeight(stageH);
                float windowY = UILayout.CenterY(fittedHeight, stageH, 0f);

                _controlsWindow?.SetSize(AppearanceWindowWidth, fittedHeight);
                _controlsWindow?.SetPosition(startX, windowY);
                _jobInfoWindow?.SetSize(JobInfoWindowWidth, fittedHeight);
                _jobInfoWindow?.SetPosition(startX + AppearanceWindowWidth + CreationWindowGap, windowY);
                // Preview sits above the direction buttons on the right side of the Appearance window
                _previewEntity?.SetPosition(startX + AppearanceWindowWidth - 128f, stageH * 0.48f);
            }

            // Once the name field loses focus, show the name the hero will get: a blank field, or one with
            // stray outer spaces, goes back to the last real name
            if (_nameField != null && !ReferenceEquals(_stage.GetKeyboardFocus(), _nameField) && _nameField.GetText() != _currentName)
                _nameField.SetText(_currentName);

            // Open at double size: the same toggle the zoom button runs, once the layout has been placed
            if (!_openedAtDoubleSize)
            {
                _openedAtDoubleSize = true;
                TogglePreviewZoom();
            }
        }

        /// <summary>Skill button for job info display with hover tooltip support</summary>
        private class JobSkillButton : Element, IInputListener
        {
            private readonly ISkill _skill;
            private SpriteDrawable _iconDrawable;
            private SpriteDrawable _selectBoxDrawable;
            private bool _isHovered;

            public event System.Action<ISkill> OnHover;
            public event System.Action OnUnhover;

            public JobSkillButton(ISkill skill)
            {
                _skill = skill;

                if (Core.Content != null)
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var iconSprite = skillsAtlas.GetSprite(skill.Id);

                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    if (iconSprite == null)
                        iconSprite = uiAtlas.GetSprite("SkillIcon1");

                    _iconDrawable = new SpriteDrawable(iconSprite);

                    var selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                    _selectBoxDrawable = new SpriteDrawable(selectBoxSprite);
                }

                SetSize(32f, 32f);
                SetTouchable(Touchable.Enabled);
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                base.Draw(batcher, parentAlpha);

                if (_iconDrawable != null)
                    _iconDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                if (_isHovered && _selectBoxDrawable != null)
                    _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            #region IInputListener

            void IInputListener.OnMouseEnter()
            {
                _isHovered = true;
                OnHover?.Invoke(_skill);
            }

            void IInputListener.OnMouseExit()
            {
                _isHovered = false;
                OnUnhover?.Invoke();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;

            #endregion
        }
    }
}

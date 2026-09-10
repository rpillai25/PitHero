using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Scenes;
using PitHero.Services;
using System;

namespace PitHero.UI
{
    /// <summary>UI for displaying save/load slots with game state previews.</summary>
    public class SaveLoadUI
    {
        /// <summary>Whether the UI is in save mode or load mode.</summary>
        public enum Mode
        {
            Save,
            Load
        }
        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }

        private const float WindowWidth = 500f;
        private const float WindowHeight = 300f; // design height at GameConfig.VirtualHeight = 360; fitted to the stage at show time
        private const float SlotRowHeight = 50f;
        private const float AutoSaveRowHeight = 72f; // tag line above the preview columns, block centered vertically
        private const float SlotPadding = 4f;

        /// <summary>Sentinel slot index for the dedicated autosave file (issue #409); listed first in Load mode only.</summary>
        public const int AutoSaveSlot = -1;

        private static readonly Color TimeHeaderColor = new Color(172, 50, 50);
        private static readonly Color AutoSaveRowTint = new Color(153, 229, 80);   // multiplies the row nine-patch only; labels keep their own colors
        private static readonly Color AutoSaveLabelColor = new Color(251, 140, 0);      // "AutoSave" tag color (Skullboy font), chosen to contrast the row tint

        private Stage _stage;
        private Window _window;
        private Window _confirmDialog;
        private Mode _currentMode;
        private Action _onClose;
        private Skin _skin;
        private SpriteAtlas _actorsAtlas;
        private TextService _textService;

        /// <summary>Whether the save/load window is currently visible.</summary>
        public bool IsVisible => _window != null && _window.IsVisible();

        /// <summary>Shows the save/load UI on the given stage.</summary>
        public void Show(Stage stage, Mode mode, Action onClose = null)
        {
            _stage = stage;
            _currentMode = mode;
            _onClose = onClose;
            _skin = PitHeroSkin.CreateSkin();
            BuildWindow();
        }

        /// <summary>Hides the save/load UI and removes it from the stage.</summary>
        public void Hide()
        {
            HideConfirmDialog();

            if (_window != null)
            {
                _window.Remove();
                _window = null;
            }

            _onClose?.Invoke();
        }

        /// <summary>Builds the main save/load window with slot rows inside a scroll pane.</summary>
        private void BuildWindow()
        {
            var windowStyle = _skin.Get<WindowStyle>("ph-default");
            string title = _currentMode == Mode.Save 
                ? GetText(TextType.UI, UITextKey.WindowSaveGame) 
                : GetText(TextType.UI, UITextKey.WindowLoadGame);
            _window = new Window(title, windowStyle);
            float stageH = _stage.GetHeight();
            float windowH = UILayout.FitHeight(WindowHeight, stageH, GameConfig.UIStageMargin, GameConfig.UIStageMargin);
            _window.SetSize(WindowWidth, windowH);
            _window.SetMovable(false);

            var contentTable = new Table();
            contentTable.Pad(10f);

            // Build slot rows into a container table
            var slotsTable = new Table();
            var service = Core.Services.GetService<SaveLoadService>();

            // Load the actors atlas for hero sprite previews
            try
            {
                _actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn("[SaveLoadUI] Failed to load Actors.atlas for hero previews: " + ex.Message);
                _actorsAtlas = null;
            }

            // Autosaves are only offered for loading; manual saves never overwrite them. One row per
            // hero, most recently played first, re-scanned from disk so heroes played in an earlier
            // run of the game show up too.
            if (_currentMode == Mode.Load && service != null)
            {
                Core.Services.GetService<AutoSaveService>()?.WaitForCompletion();
                service.RefreshAutoSavePreviews();

                var autoSaves = service.AutoSaveEntries;
                for (int i = 0; i < autoSaves.Count; i++)
                {
                    BuildSlotRow(slotsTable, AutoSaveSlot, autoSaves[i].Preview, autoSaves[i].HeroId);
                    slotsTable.Row();
                }
            }

            for (int i = 0; i < SaveLoadService.MaxSlots; i++)
            {
                SaveData preview = service != null ? service.GetSlotPreview(i) : null;
                BuildSlotRow(slotsTable, i, preview);
                slotsTable.Row();
            }

            // Wrap slots in a scroll pane (vertical only)
            var scrollPane = new ScrollPane(slotsTable, _skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            contentTable.Add(scrollPane).Expand().Fill().SetPadBottom(8f);
            contentTable.Row();

            // Close button at the bottom
            var closeButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonClose), _skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (button) => Hide();
            contentTable.Add(closeButton).SetMinWidth(80f).Height(28f);

            _window.Add(contentTable).Expand().Fill();

            // Center the window on stage
            _window.SetPosition(
                (_stage.GetWidth() - WindowWidth) / 2f,
                UILayout.CenterY(windowH, stageH, 0f)
            );

            _stage.AddElement(_window);
            _window.SetVisible(true);
            _window.ToFront();
        }

        /// <summary>
        /// Builds a single save slot row with preview data or an empty label. For an autosave row
        /// (slotIndex == AutoSaveSlot) autoSaveHeroId names the hero the row belongs to.
        /// </summary>
        private void BuildSlotRow(Table container, int slotIndex, SaveData preview, int autoSaveHeroId = 0)
        {
            var rowTable = new Table();
            bool isAutoSave = slotIndex == AutoSaveSlot;
            float rowHeight = isAutoSave ? AutoSaveRowHeight : SlotRowHeight;

            if (preview != null)
            {
                // AutoSave tag: its own full-width line, centered over the three preview columns
                if (isAutoSave)
                {
                    // The previous 2px bottom pad becomes +8 top / -6 bottom: the tag moves down 8px while the
                    // block height stays the same, so the preview columns below keep their position
                    rowTable.Add(CreateAutoSaveLabel()).SetColspan(3).Center().SetPadTop(8f).SetPadBottom(-6f);
                    rowTable.Row();
                }

                // Left column: hero sprite preview
                if (_actorsAtlas != null)
                {
                    var heroDrawable = new HeroPreviewDrawable(
                        _actorsAtlas, preview.SkinColor, preview.HairColor,
                        preview.ShirtColor, preview.HairstyleIndex);
                    var heroImage = new Image(heroDrawable, Scaling.Fit);
                    rowTable.Add(heroImage).Size(32f, 46f).SetPadLeft(4f).SetPadRight(8f);
                }

                // Middle column: hero name and level
                var infoTable = new Table();
                var nameLabel = new Label(preview.HeroName ?? GetText(TextType.UI, UITextKey.SaveLoadUnknown), _skin, "ph-default");
                infoTable.Add(nameLabel).Left();
                infoTable.Row();

                var levelLabel = new Label(string.Format(GetText(TextType.UI, UITextKey.SaveLoadLevelLabel), preview.Level), _skin, "ph-default");
                infoTable.Add(levelLabel).Left();

                // Expand horizontally only: with no cell claiming the spare height, the table centers
                // its rows as one block, so the AutoSave tag sits just above the name instead of at the top
                rowTable.Add(infoTable).SetExpandX().Left().SetPadLeft(8f);

                // Right column: time header and formatted time
                var timeTable = new Table();
                var timeHeaderLabel = new Label(GetText(TextType.UI, UITextKey.SaveLoadTimeHeader), _skin, "ph-default");
                // Create a unique style so color doesn't bleed to other labels
                var timeHeaderStyle = new LabelStyle
                {
                    Font = timeHeaderLabel.GetStyle().Font,
                    FontColor = TimeHeaderColor,
                    FontScaleX = 1f,
                    FontScaleY = 1f
                };
                timeHeaderLabel.SetStyle(timeHeaderStyle);
                timeTable.Add(timeHeaderLabel).Right();
                timeTable.Row();

                var timeValueLabel = new Label(FormatTime(preview.TotalTimePlayed), _skin, "ph-default");
                timeTable.Add(timeValueLabel).Right();

                rowTable.Add(timeTable).Right().SetPadRight(8f);
            }
            else
            {
                // Autosave rows only exist when that hero has one, so an empty row is always a manual slot
                var emptyLabel = new Label(GetText(TextType.UI, UITextKey.SaveLoadEmptySlot), _skin, "ph-default");
                rowTable.Add(emptyLabel).Expand().Center();
            }

            // Wrap the row in a clickable TextButton to make the entire row clickable
            var slotButton = new TextButton("", _skin, "ph-default");
            slotButton.ClearChildren();
            slotButton.Add(rowTable).Expand().Fill();
            slotButton.SetSize(WindowWidth - 40f, rowHeight);
            if (isAutoSave)
                slotButton.SetColor(AutoSaveRowTint);

            // Capture the row's identity for the closure
            int capturedIndex = slotIndex;
            int capturedHeroId = autoSaveHeroId;
            string capturedHeroName = preview?.HeroName;
            bool hasData = preview != null;

            // In load mode, empty slots are not clickable
            if (_currentMode == Mode.Load && !hasData)
            {
                slotButton.SetDisabled(true);
            }
            else
            {
                slotButton.OnClicked += (button) => ShowConfirmDialog(capturedIndex, capturedHeroId, capturedHeroName);
            }

            container.Add(slotButton).Width(WindowWidth - 40f).Height(rowHeight).SetPadBottom(SlotPadding);
        }

        /// <summary>
        /// "AutoSave" tag label in the Skullboy HUD font. It gets its own LabelStyle so the font and color
        /// never bleed into other labels.
        /// </summary>
        private Label CreateAutoSaveLabel()
        {
            // Core.Content caches the font, so this shares the instance with the HUD rather than loading a second copy
            var ownStyle = new LabelStyle
            {
                Font = Core.Content.LoadBitmapFont(GameConfig.FontPathHud),
                FontColor = AutoSaveLabelColor,
                FontScaleX = 1f,
                FontScaleY = 1f
            };
            return new Label(GetText(TextType.UI, UITextKey.SaveLoadAutoSave), ownStyle);
        }

        /// <summary>Shows a confirmation dialog before saving or loading.</summary>
        private void ShowConfirmDialog(int slotIndex, int autoSaveHeroId = 0, string autoSaveHeroName = null)
        {
            HideConfirmDialog();

            string title;
            string message;
            string confirmText;

            if (_currentMode == Mode.Save)
            {
                title = GetText(TextType.UI, UITextKey.DialogConfirmSave);
                message = string.Format(GetText(TextType.UI, UITextKey.ConfirmOverwriteSaveSlot), slotIndex + 1);
                confirmText = GetText(TextType.UI, UITextKey.ButtonSave);
            }
            else
            {
                title = GetText(TextType.UI, UITextKey.DialogConfirmLoad);
                // Several heroes can have an autosave, so the prompt names the one being loaded
                message = slotIndex == AutoSaveSlot
                    ? string.Format(GetText(TextType.UI, UITextKey.ConfirmLoadAutoSave),
                        autoSaveHeroName ?? GetText(TextType.UI, UITextKey.SaveLoadUnknown))
                    : string.Format(GetText(TextType.UI, UITextKey.ConfirmLoadSaveSlot), slotIndex + 1);
                confirmText = GetText(TextType.UI, UITextKey.ButtonLoad);
            }

            var windowStyle = _skin.Get<WindowStyle>("ph-default");
            _confirmDialog = new Window(title, windowStyle);
            _confirmDialog.SetSize(300f, 150f);
            _confirmDialog.SetMovable(false);

            var dialogTable = new Table();
            dialogTable.Pad(20f);

            var messageLabel = new Label(message, _skin, "ph-default");
            messageLabel.SetWrap(true);
            dialogTable.Add(messageLabel).Width(260f).SetPadBottom(15f);
            dialogTable.Row();

            // Button row
            var buttonTable = new Table();

            int capturedSlot = slotIndex;
            int capturedHeroId = autoSaveHeroId;
            var confirmButton = new TextButton(confirmText, _skin, "ph-default");
            confirmButton.OnClicked += (button) =>
            {
                HideConfirmDialog();
                if (_currentMode == Mode.Save)
                    PerformSave(capturedSlot);
                else
                    PerformLoad(capturedSlot, capturedHeroId);
            };
            buttonTable.Add(confirmButton).SetMinWidth(80f).Height(24f).SetPadRight(10f);

            var cancelButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonCancel), _skin, "ph-default");
            cancelButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            cancelButton.OnClicked += (button) => HideConfirmDialog();
            buttonTable.Add(cancelButton).SetMinWidth(80f).Height(24f);

            dialogTable.Add(buttonTable);

            _confirmDialog.Add(dialogTable).Expand().Fill();

            // Center the confirm dialog on stage
            _confirmDialog.SetPosition(
                (_stage.GetWidth() - 300f) / 2f,
                (_stage.GetHeight() - 150f) / 2f
            );

            _stage.AddElement(_confirmDialog);
            _confirmDialog.SetVisible(true);
            _confirmDialog.ToFront();
        }

        /// <summary>Hides and removes the confirmation dialog.</summary>
        private void HideConfirmDialog()
        {
            if (_confirmDialog != null)
            {
                _confirmDialog.Remove();
                _confirmDialog = null;
            }
        }

        /// <summary>Gathers current game state and saves it to the specified slot.</summary>
        private void PerformSave(int slotIndex)
        {
            var service = Core.Services.GetService<SaveLoadService>();
            if (service != null)
            {
                var saveData = SaveLoadService.GatherCurrentState();
                service.SaveToSlot(slotIndex, saveData);
                service.RefreshSlotPreviews();
                // A manual save restarts the autosave countdown (issue #409)
                Core.Services.GetService<AutoSaveService>()?.ResetTimer();
                Debug.Log("SaveLoadUI: Saved to slot " + slotIndex);
            }

            Hide();
        }

        /// <summary>
        /// Loads game state from the specified slot and transitions to the game scene. For the autosave
        /// sentinel, autoSaveHeroId names the hero whose autosave to load.
        /// </summary>
        private void PerformLoad(int slotIndex, int autoSaveHeroId = 0)
        {
            var service = Core.Services.GetService<SaveLoadService>();
            if (service != null)
            {
                SaveData data;
                if (slotIndex == AutoSaveSlot)
                {
                    // Never read an autosave while its worker may still be writing it
                    Core.Services.GetService<AutoSaveService>()?.WaitForCompletion();
                    data = service.LoadFromAutoSave(autoSaveHeroId);
                }
                else
                {
                    data = service.LoadFromSlot(slotIndex);
                }
                if (data != null)
                {
                    SaveLoadService.ApplyLoadedState(data);
                    Core.Scene = MainGameScene.CreateForGameplay(MainGameScene.DefaultMapPath);
                    Debug.Log("SaveLoadUI: Loaded from slot " + slotIndex);
                }
            }

            Hide();
        }

        /// <summary>Formats total seconds into HH:MM:SS display string.</summary>
        private static string FormatTime(float totalSeconds)
        {
            int total = (int)totalSeconds;
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int seconds = total % 60;
            return hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }
    }
}

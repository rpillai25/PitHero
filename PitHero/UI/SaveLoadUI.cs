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

        private const float WindowWidth = 500f;
        private const float WindowHeight = 300f;
        private const float SlotRowHeight = 50f;
        private const float SlotPadding = 4f;

        private static readonly Color TimeHeaderColor = new Color(100, 149, 237);

        private Stage _stage;
        private Window _window;
        private Window _confirmDialog;
        private Mode _currentMode;
        private Action _onClose;
        private Skin _skin;
        private SpriteAtlas _actorsAtlas;

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
            string title = _currentMode == Mode.Save ? "Save Game" : "Load Game";
            _window = new Window(title, windowStyle);
            _window.SetSize(WindowWidth, WindowHeight);
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
            var closeButton = new TextButton("Close", _skin, "ph-default");
            closeButton.OnClicked += (button) => Hide();
            contentTable.Add(closeButton).SetMinWidth(80f).Height(28f);

            _window.Add(contentTable).Expand().Fill();

            // Center the window on stage
            _window.SetPosition(
                (_stage.GetWidth() - WindowWidth) / 2f,
                (_stage.GetHeight() - WindowHeight) / 2f
            );

            _stage.AddElement(_window);
            _window.SetVisible(true);
            _window.ToFront();
        }

        /// <summary>Builds a single save slot row with preview data or an empty label.</summary>
        private void BuildSlotRow(Table container, int slotIndex, SaveData preview)
        {
            var rowTable = new Table();

            if (preview != null)
            {
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
                var nameLabel = new Label(preview.HeroName ?? "Unknown", _skin, "ph-default");
                infoTable.Add(nameLabel).Left();
                infoTable.Row();

                var levelLabel = new Label("Level " + preview.Level, _skin, "ph-default");
                infoTable.Add(levelLabel).Left();

                rowTable.Add(infoTable).Expand().Left().SetPadLeft(8f);

                // Right column: time header and formatted time
                var timeTable = new Table();
                var timeHeaderLabel = new Label("TIME", _skin, "ph-default");
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
                var emptyLabel = new Label("- Empty -", _skin, "ph-default");
                rowTable.Add(emptyLabel).Expand().Center();
            }

            // Wrap the row in a clickable TextButton to make the entire row clickable
            var slotButton = new TextButton("", _skin, "ph-default");
            slotButton.ClearChildren();
            slotButton.Add(rowTable).Expand().Fill();
            slotButton.SetSize(WindowWidth - 40f, SlotRowHeight);

            // Capture the index for the closure
            int capturedIndex = slotIndex;
            bool hasData = preview != null;

            // In load mode, empty slots are not clickable
            if (_currentMode == Mode.Load && !hasData)
            {
                slotButton.SetDisabled(true);
            }
            else
            {
                slotButton.OnClicked += (button) => ShowConfirmDialog(capturedIndex);
            }

            container.Add(slotButton).Width(WindowWidth - 40f).Height(SlotRowHeight).SetPadBottom(SlotPadding);
        }

        /// <summary>Shows a confirmation dialog before saving or loading.</summary>
        private void ShowConfirmDialog(int slotIndex)
        {
            HideConfirmDialog();

            string title;
            string message;
            string confirmText;

            if (_currentMode == Mode.Save)
            {
                title = "Confirm Save";
                message = "Overwrite save in slot " + (slotIndex + 1) + "?";
                confirmText = "Save";
            }
            else
            {
                title = "Confirm Load";
                message = "Load save from slot " + (slotIndex + 1) + "?";
                confirmText = "Load";
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
            var confirmButton = new TextButton(confirmText, _skin, "ph-default");
            confirmButton.OnClicked += (button) =>
            {
                HideConfirmDialog();
                if (_currentMode == Mode.Save)
                    PerformSave(capturedSlot);
                else
                    PerformLoad(capturedSlot);
            };
            buttonTable.Add(confirmButton).SetMinWidth(80f).Height(24f).SetPadRight(10f);

            var cancelButton = new TextButton("Cancel", _skin, "ph-default");
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
                Debug.Log("SaveLoadUI: Saved to slot " + slotIndex);
            }

            Hide();
        }

        /// <summary>Loads game state from the specified slot and transitions to the game scene.</summary>
        private void PerformLoad(int slotIndex)
        {
            var service = Core.Services.GetService<SaveLoadService>();
            if (service != null)
            {
                var data = service.LoadFromSlot(slotIndex);
                if (data != null)
                {
                    SaveLoadService.ApplyLoadedState(data);
                    var mainGameScene = new MainGameScene("Content/Tilemaps/PitHero.tmx");
                    mainGameScene.ClearColor = new Color(71, 114, 56);
                    mainGameScene.LetterboxColor = new Color(71, 114, 56);
                    Core.Scene = mainGameScene;
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

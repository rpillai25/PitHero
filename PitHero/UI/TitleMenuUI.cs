using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Scenes;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for the title screen menu
    /// </summary>
    public class TitleMenuUI
    {
        private Stage _stage;
        private Image _titleLogo;
        private Table _mainMenuTable;
        private Window _quitConfirmationDialog;
        private SaveLoadUI _saveLoadUI;
        private TextService _textService;

        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            _textService = Core.Services.GetService<TextService>();

            var skin = PitHeroSkin.CreateSkin();

            CreateTitleLogo();
            CreateMainMenu(skin);
            CreateQuitConfirmationDialog(skin);
        }

        private void CreateTitleLogo()
        {
            // Load the UI atlas and get the PitHeroTitle sprite
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var titleSprite = uiAtlas.GetSprite("PitHeroTitle");
            _titleLogo = new Image(titleSprite);

            // Center the logo horizontally and position it higher to allow more space for buttons
            float logoX = (_stage.GetWidth() - titleSprite.SourceRect.Width) / 2f;
            float logoY = GameConfig.VirtualHeight * 0.10f; // 10% from top (moved higher)

            _titleLogo.SetPosition(logoX, logoY);
            _stage.AddElement(_titleLogo);
        }

        private void CreateMainMenu(Skin skin)
        {
            _mainMenuTable = new Table();
            _mainMenuTable.SetFillParent(true);

            // Create buttons
            var newButton = new TextButton(_textService.DisplayText(DialogueType.UI, TextKey.ButtonNew), skin);
            var loadButton = new TextButton(_textService.DisplayText(DialogueType.UI, TextKey.ButtonLoad), skin);
            var quitButton = new TextButton(_textService.DisplayText(DialogueType.UI, TextKey.ButtonQuit), skin);

            // Set button sizes
            const float buttonWidth = 200f;
            const float buttonHeight = 50f;

            // Wire up button events
            newButton.OnClicked += (button) => StartGame("Content/Tilemaps/PitHero.tmx");
            loadButton.OnClicked += (button) => ShowLoadUI();
            quitButton.OnClicked += (button) => ShowQuitConfirmation();

            // Add buttons to table horizontally with spacing
            _mainMenuTable.Add(newButton).Size(buttonWidth, buttonHeight).SetPadRight(20);
            _mainMenuTable.Add(loadButton).Size(buttonWidth, buttonHeight).SetPadRight(20);
            _mainMenuTable.Add(quitButton).Size(buttonWidth, buttonHeight);

            // Center the table vertically in the lower portion of the screen
            _mainMenuTable.Center();
            _mainMenuTable.SetY(_stage.GetHeight() * 0.25f); // Start at 25% down the screen

            _stage.AddElement(_mainMenuTable);
        }



        private void CreateQuitConfirmationDialog(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _quitConfirmationDialog = new Window(_textService.DisplayText(DialogueType.UI, TextKey.DialogReallyQuit), windowStyle);
            _quitConfirmationDialog.SetSize(300, 150);

            var dialogTable = new Table();
            dialogTable.Pad(20);

            // Message
            dialogTable.Add(new Label(_textService.DisplayText(DialogueType.UI, TextKey.ConfirmQuitMessage), skin)).SetPadBottom(20);
            dialogTable.Row();

            // Button row
            var buttonTable = new Table();

            var yesButton = new TextButton(_textService.DisplayText(DialogueType.UI, TextKey.ButtonYes), skin);
            yesButton.OnClicked += (button) =>
            {
                HideQuitConfirmation();
                Core.Exit();
            };
            buttonTable.Add(yesButton).Width(80).Height(24).SetPadRight(10);

            var noButton = new TextButton(_textService.DisplayText(DialogueType.UI, TextKey.ButtonNo), skin);
            noButton.OnClicked += (button) => HideQuitConfirmation();
            buttonTable.Add(noButton).Width(80).Height(24);;

            dialogTable.Add(buttonTable);

            _quitConfirmationDialog.Add(dialogTable).Expand().Fill();
            _quitConfirmationDialog.SetVisible(false);

            // Center the dialog
            _quitConfirmationDialog.SetPosition(
                (_stage.GetWidth() - _quitConfirmationDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _quitConfirmationDialog.GetHeight()) / 2
            );

            _stage.AddElement(_quitConfirmationDialog);
        }

        private void StartGame(string mapPath)
        {
            // Transition to hero creation scene instead of directly to the game
            var heroCreationScene = new HeroCreationScene(mapPath);
            Core.Scene = heroCreationScene;
        }

        private void ShowQuitConfirmation()
        {
            _quitConfirmationDialog.SetVisible(true);
            _quitConfirmationDialog.ToFront();
        }

        /// <summary>Shows the load UI.</summary>
        private void ShowLoadUI()
        {
            if (_saveLoadUI == null)
                _saveLoadUI = new SaveLoadUI();
            
            _saveLoadUI.Show(_stage, SaveLoadUI.Mode.Load);
        }

        private void HideQuitConfirmation()
        {
            _quitConfirmationDialog.SetVisible(false);
        }

        public void Update()
        {
            // Handle any per-frame updates if needed
        }
    }
}
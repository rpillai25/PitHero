using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Scenes;

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

        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            var skin = Skin.CreateDefaultSkin();

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
            var newButton = new TextButton("New", skin);
            var loadButton = new TextButton("Load", skin);
            var quitButton = new TextButton("Quit", skin);

            // Set button sizes
            const float buttonWidth = 200f;
            const float buttonHeight = 50f;

            // Wire up button events
            newButton.OnClicked += (button) => StartGame("Content/Tilemaps/PitHeroLarge.tmx");
            loadButton.OnClicked += (button) => { /* TODO: Implement load functionality */ };
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
            _quitConfirmationDialog = new Window("Really Quit?", windowStyle);
            _quitConfirmationDialog.SetSize(300, 150);
            
            var dialogTable = new Table();
            dialogTable.Pad(20);
            
            // Message
            dialogTable.Add(new Label("Are you sure you want to quit?", skin)).SetPadBottom(20);
            dialogTable.Row();
            
            // Button row
            var buttonTable = new Table();
            
            var yesButton = new TextButton("Yes", skin);
            yesButton.OnClicked += (button) => {
                HideQuitConfirmation();
                Core.Exit();
            };
            buttonTable.Add(yesButton).Width(80).SetPadRight(10);
            
            var noButton = new TextButton("No", skin);
            noButton.OnClicked += (button) => HideQuitConfirmation();
            buttonTable.Add(noButton).Width(80);
            
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
            // Create and switch to the main game scene with the selected map
            var mainGameScene = new MainGameScene(mapPath);
            Color grassColor = new Color(71, 114, 56);
            mainGameScene.ClearColor = grassColor;   // Set background
            // Optional: letterbox bars (if any) also blue
            mainGameScene.LetterboxColor = grassColor;
            Core.Scene = mainGameScene;
        }

        private void ShowQuitConfirmation()
        {
            _quitConfirmationDialog.SetVisible(true);
            _quitConfirmationDialog.ToFront();
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
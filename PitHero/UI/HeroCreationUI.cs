using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using RolePlayingFramework.Heroes;
using PitHero.Services;
using PitHero.Util;

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

        // Current selections
        private string _currentName;
        private int _currentSkinIndex;
        private int _currentHairColorIndex;
        private int _currentHairstyleIndex;
        private int _currentShirtIndex;

        // UI labels that update when selections change
        private Label _nameLabel;
        private Label _hairstyleLabel;
        private Label _skinLabel;
        private Label _hairColorLabel;
        private Label _shirtLabel;

        // Reusable list for component removal (pre-allocated to avoid GC)
        private readonly List<HeroAnimationComponent> _tempAnimComponents = new List<HeroAnimationComponent>(8);

        // Direction cycling for preview
        private int _currentDirectionIndex;
        private static readonly Direction[] PreviewDirections = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };

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

            // Randomize initial selections
            _currentName = NameGenerator.GenerateRandomName();
            _currentSkinIndex = Nez.Random.Range(0, GameConfig.SkinColors.Count);
            _currentHairColorIndex = Nez.Random.Range(0, GameConfig.HairColors.Count);
            _currentHairstyleIndex = Nez.Random.Range(1, GameConfig.MaleHeroHairstyleCount + 1);
            _currentShirtIndex = Nez.Random.Range(0, GameConfig.ShirtColors.Count);

            var skin = PitHeroSkin.CreateSkin();

            CreateTitleLabel(skin);
            CreateControlsWindow(skin);

            // Build initial preview appearance
            SetupPreviewEntity();
            RebuildPreviewAppearance();
        }

        /// <summary>Creates the title label centered over the hero preview panel with white text</summary>
        private void CreateTitleLabel(Skin skin)
        {
            // Center in the left panel (0 to 60% of stage width)
            float leftPanelWidth = _stage.GetWidth() * 0.6f;
            var defaultLabelStyle = skin.Get<LabelStyle>();
            var whiteLabelStyle = new LabelStyle
            {
                Font = defaultLabelStyle.Font,
                FontColor = Color.White,
                FontScaleX = defaultLabelStyle.FontScaleX,
                FontScaleY = defaultLabelStyle.FontScaleY
            };
            var titleLabel = new Label("Create Your Hero", whiteLabelStyle);
            titleLabel.SetPosition(
                (leftPanelWidth - 200f) / 2f,
                20f
            );
            _stage.AddElement(titleLabel);
        }

        /// <summary>Creates the controls window on the right side with all appearance options</summary>
        private void CreateControlsWindow(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            var window = new Window("Appearance", windowStyle);
            window.SetSize(500f, 300f);
            window.SetPosition(
                (_stage.GetWidth() - 500f) / 2f,
                (_stage.GetHeight() - 300f) / 2f
            );

            var table = new Table();
            table.Pad(10f);
            table.Defaults().SetPadBottom(8f);

            const float arrowWidth = 40f;
            const float arrowHeight = 30f;
            const float labelWidth = 160f;

            // --- Name row ---
            _nameLabel = new Label(_currentName, skin);
            var rerollButton = new TextButton("Reroll", skin);
            rerollButton.OnClicked += (btn) =>
            {
                _currentName = NameGenerator.GenerateRandomName();
                _nameLabel.SetText(_currentName);
            };
            table.Add(new Label("Name:", skin)).SetPadRight(10f);
            table.Add(_nameLabel).Width(labelWidth);
            table.Add(rerollButton).Width(80f).Height(arrowHeight);
            table.Row();

            // --- Hairstyle row ---
            _hairstyleLabel = new Label("Hairstyle " + _currentHairstyleIndex, skin);
            var hairStyleLeft = new TextButton("<", skin);
            var hairStyleRight = new TextButton(">", skin);
            hairStyleLeft.OnClicked += (btn) =>
            {
                _currentHairstyleIndex--;
                if (_currentHairstyleIndex < 1)
                    _currentHairstyleIndex = GameConfig.MaleHeroHairstyleCount;
                _hairstyleLabel.SetText("Hairstyle " + _currentHairstyleIndex);
                RebuildPreviewAppearance();
            };
            hairStyleRight.OnClicked += (btn) =>
            {
                _currentHairstyleIndex++;
                if (_currentHairstyleIndex > GameConfig.MaleHeroHairstyleCount)
                    _currentHairstyleIndex = 1;
                _hairstyleLabel.SetText("Hairstyle " + _currentHairstyleIndex);
                RebuildPreviewAppearance();
            };
            table.Add(hairStyleLeft).Size(arrowWidth, arrowHeight);
            table.Add(_hairstyleLabel).Width(labelWidth);
            table.Add(hairStyleRight).Size(arrowWidth, arrowHeight);
            table.Row();

            // --- Skin Color row ---
            _skinLabel = new Label("Skin " + (_currentSkinIndex + 1), skin);
            var skinLeft = new TextButton("<", skin);
            var skinRight = new TextButton(">", skin);
            skinLeft.OnClicked += (btn) =>
            {
                _currentSkinIndex--;
                if (_currentSkinIndex < 0)
                    _currentSkinIndex = GameConfig.SkinColors.Count - 1;
                _skinLabel.SetText("Skin " + (_currentSkinIndex + 1));
                RebuildPreviewAppearance();
            };
            skinRight.OnClicked += (btn) =>
            {
                _currentSkinIndex++;
                if (_currentSkinIndex >= GameConfig.SkinColors.Count)
                    _currentSkinIndex = 0;
                _skinLabel.SetText("Skin " + (_currentSkinIndex + 1));
                RebuildPreviewAppearance();
            };
            table.Add(skinLeft).Size(arrowWidth, arrowHeight);
            table.Add(_skinLabel).Width(labelWidth);
            table.Add(skinRight).Size(arrowWidth, arrowHeight);
            table.Row();

            // --- Hair Color row ---
            _hairColorLabel = new Label("Hair Color " + (_currentHairColorIndex + 1), skin);
            var hairColorLeft = new TextButton("<", skin);
            var hairColorRight = new TextButton(">", skin);
            hairColorLeft.OnClicked += (btn) =>
            {
                _currentHairColorIndex--;
                if (_currentHairColorIndex < 0)
                    _currentHairColorIndex = GameConfig.HairColors.Count - 1;
                _hairColorLabel.SetText("Hair Color " + (_currentHairColorIndex + 1));
                RebuildPreviewAppearance();
            };
            hairColorRight.OnClicked += (btn) =>
            {
                _currentHairColorIndex++;
                if (_currentHairColorIndex >= GameConfig.HairColors.Count)
                    _currentHairColorIndex = 0;
                _hairColorLabel.SetText("Hair Color " + (_currentHairColorIndex + 1));
                RebuildPreviewAppearance();
            };
            table.Add(hairColorLeft).Size(arrowWidth, arrowHeight);
            table.Add(_hairColorLabel).Width(labelWidth);
            table.Add(hairColorRight).Size(arrowWidth, arrowHeight);
            table.Row();

            // --- Shirt Color row ---
            _shirtLabel = new Label("Shirt " + (_currentShirtIndex + 1), skin);
            var shirtLeft = new TextButton("<", skin);
            var shirtRight = new TextButton(">", skin);
            shirtLeft.OnClicked += (btn) =>
            {
                _currentShirtIndex--;
                if (_currentShirtIndex < 0)
                    _currentShirtIndex = GameConfig.ShirtColors.Count - 1;
                _shirtLabel.SetText("Shirt " + (_currentShirtIndex + 1));
                RebuildPreviewAppearance();
            };
            shirtRight.OnClicked += (btn) =>
            {
                _currentShirtIndex++;
                if (_currentShirtIndex >= GameConfig.ShirtColors.Count)
                    _currentShirtIndex = 0;
                _shirtLabel.SetText("Shirt " + (_currentShirtIndex + 1));
                RebuildPreviewAppearance();
            };
            table.Add(shirtLeft).Size(arrowWidth, arrowHeight);
            table.Add(_shirtLabel).Width(labelWidth);
            table.Add(shirtRight).Size(arrowWidth, arrowHeight);
            table.Row();

            // --- Create Hero / Cancel / Change Direction buttons ---
            var createButton = new TextButton("Create Hero", skin);
            createButton.OnClicked += (btn) => OnCreateHero();

            var cancelButton = new TextButton("Cancel", skin);
            cancelButton.OnClicked += (btn) => OnCancel();

            var directionButton = new TextButton("Change Direction", skin);
            directionButton.OnClicked += (btn) => CyclePreviewDirection();

            table.Row();
            table.Add(createButton).Size(160f, 40f).SetPadTop(10f).SetPadRight(6f);
            table.Add(cancelButton).Size(100f, 40f).SetPadTop(10f).SetPadRight(6f);
            table.Add(directionButton).Size(160f, 40f).SetPadTop(10f);

            window.Add(table).Expand().Fill();
            _stage.AddElement(window);
        }

        /// <summary>Adds ActorFacingComponent, white border, and green background to the preview entity</summary>
        private void SetupPreviewEntity()
        {
            _previewEntity.AddComponent(new ActorFacingComponent());

            var bgOffset = new Vector2(0, -GameConfig.TileSize / 2);

            // White border (renders first = behind everything)
            var border = _previewEntity.AddComponent(new PrototypeSpriteRenderer(36, 50));
            border.SetColor(Color.White);
            border.SetLocalOffset(bgOffset);
            border.SetRenderLayer(16);

            // Green background on top of white border, behind hero layers
            var background = _previewEntity.AddComponent(new PrototypeSpriteRenderer(32, 46));
            background.SetColor(Color.Green);
            background.SetLocalOffset(bgOffset);
            background.SetRenderLayer(15);
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

            var skinColor = GameConfig.SkinColors[_currentSkinIndex];
            var hairColor = GameConfig.HairColors[_currentHairColorIndex];
            var shirtColor = GameConfig.ShirtColors[_currentShirtIndex];
            var offset = new Vector2(0, -GameConfig.TileSize / 2);

            // Body layer
            var bodyAnimator = _previewEntity.AddComponent(new HeroBodyAnimationComponent(skinColor));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerHeroBody);
            bodyAnimator.SetLocalOffset(offset);

            // Hand2 layer
            var hand2Animator = _previewEntity.AddComponent(new HeroHand2AnimationComponent(skinColor));
            hand2Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand2);
            hand2Animator.SetLocalOffset(offset);
            hand2Animator.ComponentColor = skinColor;

            // Pants layer
            var pantsAnimator = _previewEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetRenderLayer(GameConfig.RenderLayerHeroPants);
            pantsAnimator.SetLocalOffset(offset);

            // Shirt layer
            var shirtAnimator = _previewEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor));
            shirtAnimator.SetRenderLayer(GameConfig.RenderLayerHeroShirt);
            shirtAnimator.SetLocalOffset(offset);

            // Head layer
            var headAnimator = _previewEntity.AddComponent(new HeroHeadAnimationComponent(skinColor));
            headAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHead);
            headAnimator.SetLocalOffset(offset);
            headAnimator.ComponentColor = skinColor;

            // Eyes layer
            var eyesAnimator = _previewEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetRenderLayer(GameConfig.RenderLayerHeroEyes);
            eyesAnimator.SetLocalOffset(offset);

            // Hair layer
            var hairAnimator = _previewEntity.AddComponent(new HeroHairAnimationComponent(hairColor, _currentHairstyleIndex));
            hairAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHair);
            hairAnimator.SetLocalOffset(offset);

            // Hand1 layer
            var hand1Animator = _previewEntity.AddComponent(new HeroHand1AnimationComponent(skinColor));
            hand1Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand1);
            hand1Animator.SetLocalOffset(offset);
            hand1Animator.ComponentColor = skinColor;
        }

        /// <summary>Creates the HeroDesign, stores it in HeroDesignService, and transitions to MainGameScene</summary>
        private void OnCreateHero()
        {
            var design = new HeroDesign(
                _currentName,
                GameConfig.SkinColors[_currentSkinIndex],
                GameConfig.HairColors[_currentHairColorIndex],
                _currentHairstyleIndex,
                GameConfig.ShirtColors[_currentShirtIndex]
            );

            var designService = Core.Services.GetService<HeroDesignService>();
            designService.SetDesign(design);

            var mainGameScene = new MainGameScene(_mapPath);
            Color grassColor = new Color(71, 114, 56);
            mainGameScene.ClearColor = grassColor;
            mainGameScene.LetterboxColor = grassColor;
            Core.Scene = mainGameScene;
        }

        /// <summary>Cancels hero creation and returns to the title screen</summary>
        private void OnCancel()
        {
            Core.Scene = new TitleScreenScene();
        }

        /// <summary>Cycles the preview hero to the next cardinal direction</summary>
        private void CyclePreviewDirection()
        {
            _currentDirectionIndex = (_currentDirectionIndex + 1) % PreviewDirections.Length;
            var facing = _previewEntity?.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(PreviewDirections[_currentDirectionIndex]);
        }

        /// <summary>Per-frame update for the hero creation UI</summary>
        public void Update()
        {
            // Handle any per-frame updates if needed
        }
    }
}

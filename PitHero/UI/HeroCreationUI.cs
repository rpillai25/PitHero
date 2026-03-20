using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;
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
        private Skin _skin;

        // Current selections
        private string _currentName;
        private int _currentJobIndex;
        private int _currentSkinIndex;
        private int _currentHairColorIndex;
        private int _currentHairstyleIndex;
        private int _currentShirtIndex;

        // UI labels that update when selections change
        private Label _nameLabel;
        private Label _jobLabel;
        private Label _hairstyleLabel;
        private Label _skinLabel;
        private Label _hairColorLabel;
        private Label _shirtLabel;

        // Pre-allocated array of primary job names (AOT safe)
        private static readonly string[] PrimaryJobNames = { "Knight", "Monk", "Mage", "Priest", "Archer", "Thief" };

        // Pre-allocated job instances for info display (AOT safe)
        private static readonly IJob[] PrimaryJobs = { new Knight(), new Monk(), new Mage(), new Priest(), new Archer(), new Thief() };

        // Reusable list for component removal (pre-allocated to avoid GC)
        private readonly List<HeroAnimationComponent> _tempAnimComponents = new List<HeroAnimationComponent>(8);

        // Direction cycling for preview
        private int _currentDirectionIndex;
        private static readonly Direction[] PreviewDirections = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };

        // Job Info window elements
        private Table _jobInfoContentTable;
        private SkillTooltip _skillTooltip;
        private readonly List<JobSkillButton> _skillButtons = new List<JobSkillButton>(8);

        // Font colors matching PitHeroSkin conventions
        private static readonly Color BrownFontColor = new Color(71, 36, 7);
        private static readonly Color DetailFontColor = new Color(37, 80, 112);

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
            _currentJobIndex = Nez.Random.Range(0, PrimaryJobNames.Length);
            _currentSkinIndex = Nez.Random.Range(0, GameConfig.SkinColors.Count);
            _currentHairColorIndex = Nez.Random.Range(0, GameConfig.HairColors.Count);
            _currentHairstyleIndex = Nez.Random.Range(1, GameConfig.MaleHeroHairstyleCount + 1);
            _currentShirtIndex = Nez.Random.Range(0, GameConfig.ShirtColors.Count);

            _skin = PitHeroSkin.CreateSkin();

            CreateControlsWindow(_skin);
            CreateJobInfoWindow(_skin);
            RefreshJobInfoWindow();

            // Build initial animated preview
            SetupPreviewEntity();
            RebuildPreviewAppearance();
        }

        /// <summary>Creates the controls window with appearance options and hero preview</summary>
        private void CreateControlsWindow(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            var window = new Window("Appearance", windowStyle);

            const float windowWidth = 560f;
            const float windowHeight = 340f;
            const float gap = 10f;
            const float jobInfoWidth = 350f;
            float totalWidth = windowWidth + gap + jobInfoWidth;
            float startX = (_stage.GetWidth() - totalWidth) / 2f;

            window.SetSize(windowWidth, windowHeight);
            window.SetPosition(startX, (_stage.GetHeight() - windowHeight) / 2f);

            // Main layout: top row (controls + preview), bottom row (buttons)
            var mainTable = new Table();
            mainTable.Pad(10f);

            // --- Top row: controls on left, preview on right ---
            var topRow = new Table();

            // Left: controls table
            var controlsTable = new Table();
            controlsTable.Defaults().SetPadBottom(8f);

            const float arrowWidth = 40f;
            const float arrowHeight = 30f;
            const float labelWidth = 160f;

            // --- Name row ---
            _nameLabel = new Label(_currentName, skin);
            var rerollButton = new TextButton("Reroll", skin);
            rerollButton.OnClicked += (btn) =>
            {
                _currentName = NameGenerator.GenerateRandomName();
                _currentJobIndex = Nez.Random.Range(0, PrimaryJobNames.Length);
                _currentSkinIndex = Nez.Random.Range(0, GameConfig.SkinColors.Count);
                _currentHairColorIndex = Nez.Random.Range(0, GameConfig.HairColors.Count);
                _currentHairstyleIndex = Nez.Random.Range(1, GameConfig.MaleHeroHairstyleCount + 1);
                _currentShirtIndex = Nez.Random.Range(0, GameConfig.ShirtColors.Count);
                _nameLabel.SetText(_currentName);
                _jobLabel.SetText(PrimaryJobNames[_currentJobIndex]);
                _hairstyleLabel.SetText("Hairstyle " + _currentHairstyleIndex);
                _skinLabel.SetText("Skin " + (_currentSkinIndex + 1));
                _hairColorLabel.SetText("Hair Color " + (_currentHairColorIndex + 1));
                _shirtLabel.SetText("Shirt " + (_currentShirtIndex + 1));
                RebuildPreviewAppearance();
                RefreshJobInfoWindow();
            };
            controlsTable.Add(new Label("Name:", skin)).SetPadRight(10f);
            controlsTable.Add(_nameLabel).Width(labelWidth);
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
            controlsTable.Add(jobLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_jobLabel).Width(labelWidth);
            controlsTable.Add(jobRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

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
            controlsTable.Add(hairStyleLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_hairstyleLabel).Width(labelWidth);
            controlsTable.Add(hairStyleRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

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
            controlsTable.Add(skinLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_skinLabel).Width(labelWidth);
            controlsTable.Add(skinRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

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
            controlsTable.Add(hairColorLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_hairColorLabel).Width(labelWidth);
            controlsTable.Add(hairColorRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

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
            controlsTable.Add(shirtLeft).Size(arrowWidth, arrowHeight);
            controlsTable.Add(_shirtLabel).Width(labelWidth);
            controlsTable.Add(shirtRight).Size(arrowWidth, arrowHeight);
            controlsTable.Row();

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
            dirButtonsTable.Add(dirLeft).Size(30f, 24f).SetPadRight(4f);
            dirButtonsTable.Add(dirRight).Size(30f, 24f);
            previewControlsTable.Add(dirButtonsTable);

            topRow.Add(previewControlsTable).Center().SetPadLeft(20f);

            mainTable.Add(topRow).Expand().Fill();
            mainTable.Row();

            // --- Bottom row: Create Hero / Cancel buttons ---
            var createButton = new TextButton("Create Hero", skin);
            createButton.OnClicked += (btn) => OnCreateHero();

            var cancelButton = new TextButton("Cancel", skin);
            cancelButton.OnClicked += (btn) => OnCancel();

            var buttonsTable = new Table();
            buttonsTable.Add(createButton).Size(160f, 40f).SetPadRight(10f);
            buttonsTable.Add(cancelButton).Size(140f, 40f);

            mainTable.Add(buttonsTable).Left().SetPadTop(10f);

            window.Add(mainTable).Expand().Fill();
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
            border.SetRenderLayer(998);

            // Green background on top of white border, behind hero layers
            var background = _previewEntity.AddComponent(new PrototypeSpriteRenderer(32, 46));
            background.SetColor(Color.Green);
            background.SetLocalOffset(bgOffset);
            background.SetRenderLayer(998);
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
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            bodyAnimator.SetLocalOffset(offset);

            // Hand2 layer
            var hand2Animator = _previewEntity.AddComponent(new HeroHand2AnimationComponent(skinColor));
            hand2Animator.SetRenderLayer(GameConfig.RenderLayerUI);
            hand2Animator.SetLocalOffset(offset);
            hand2Animator.ComponentColor = skinColor;

            // Pants layer
            var pantsAnimator = _previewEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            pantsAnimator.SetLocalOffset(offset);

            // Shirt layer
            var shirtAnimator = _previewEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor));
            shirtAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            shirtAnimator.SetLocalOffset(offset);

            // Head layer
            var headAnimator = _previewEntity.AddComponent(new HeroHeadAnimationComponent(skinColor));
            headAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            headAnimator.SetLocalOffset(offset);
            headAnimator.ComponentColor = skinColor;

            // Eyes layer
            var eyesAnimator = _previewEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            eyesAnimator.SetLocalOffset(offset);

            // Hair layer
            var hairAnimator = _previewEntity.AddComponent(new HeroHairAnimationComponent(hairColor, _currentHairstyleIndex));
            hairAnimator.SetRenderLayer(GameConfig.RenderLayerUI);
            hairAnimator.SetLocalOffset(offset);

            // Hand1 layer
            var hand1Animator = _previewEntity.AddComponent(new HeroHand1AnimationComponent(skinColor));
            hand1Animator.SetRenderLayer(GameConfig.RenderLayerUI);
            hand1Animator.SetLocalOffset(offset);
            hand1Animator.ComponentColor = skinColor;
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

            const float windowWidth = 560f;
            const float jobInfoWidth = 350f;
            const float windowHeight = 340f;
            const float gap = 10f;
            float totalWidth = windowWidth + gap + jobInfoWidth;
            float startX = (_stage.GetWidth() - totalWidth) / 2f;

            jobInfoWindow.SetSize(jobInfoWidth, windowHeight);
            jobInfoWindow.SetPosition(startX + windowWidth + gap, (_stage.GetHeight() - windowHeight) / 2f);

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
            _skillTooltip.GetContainer().Remove();

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
                var roleLabel = new Label("Role: " + job.Role, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
                roleLabel.SetWrap(true);
                _jobInfoContentTable.Add(roleLabel).Width(310f).Left().SetPadTop(5f);
                _jobInfoContentTable.Row();
            }

            // Skills header
            var skillsHeader = new Label("Skills:", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _jobInfoContentTable.Add(skillsHeader).Left().SetPadTop(10f);
            _jobInfoContentTable.Row();

            // Skills grid
            var skillGrid = new Table();
            var jobSkills = job.Skills;

            if (jobSkills.Count == 0)
            {
                var noSkillsLabel = new Label("No job skills", _skin, "ph-default");
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
            _skillTooltip.GetContainer().Remove();
        }

        /// <summary>Creates the HeroDesign, stores it in HeroDesignService, and transitions to MainGameScene</summary>
        private void OnCreateHero()
        {
            var design = new HeroDesign(
                _currentName,
                GameConfig.SkinColors[_currentSkinIndex],
                GameConfig.HairColors[_currentHairColorIndex],
                _currentHairstyleIndex,
                GameConfig.ShirtColors[_currentShirtIndex],
                PrimaryJobNames[_currentJobIndex]
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

        /// <summary>Per-frame update for the hero creation UI</summary>
        public void Update()
        {
            // Handle any per-frame updates if needed
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

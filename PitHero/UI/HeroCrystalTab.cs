using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.UI
{
    /// <summary>
    /// UI component for the Hero Crystal tab showing job info and skill purchase grid
    /// </summary>
    public class HeroCrystalTab
    {
        private Table _mainContainer;
        private HeroComponent _heroComponent;
        
        // Info display labels
        private Label _jobNameLabel;
        private Label _levelLabel;
        private Label _jobLevelLabel;
        private Label _currentJPLabel;
        private Label _totalJPLabel;
        private Label _statsLabel;
        
        // Skill grid
        private Table _skillGridContainer;
        private List<SkillButton> _skillButtons;
        
        // Confirmation dialog
        private Dialog _confirmDialog;
        private ISkill _pendingSkillPurchase;
        
        // Tooltip for skills
        private SkillTooltip _skillTooltip;
        private Stage _stage;
        
        public HeroCrystalTab()
        {
            _skillButtons = new List<SkillButton>();
        }
        
        /// <summary>Creates and returns the main container for this tab</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _stage = stage;
            _mainContainer = new Table();
            _mainContainer.SetFillParent(false);
            
            // Create skill tooltip
            var dummyTarget = new Element();
            dummyTarget.SetSize(0, 0);
            _skillTooltip = new SkillTooltip(dummyTarget, skin);
            
            // Top section: Crystal info
            var infoSection = CreateInfoSection(skin);
            _mainContainer.Add(infoSection).Expand().Fill().Pad(10f).Top();
            _mainContainer.Row();
            
            // Middle section: Skill grid in scroll pane
            var skillGridPane = CreateSkillGrid(skin);
            _mainContainer.Add(skillGridPane).Expand().Fill().Pad(10f);
            
            // Create confirmation dialog (hidden initially)
            CreateConfirmationDialog(skin);
            
            return _mainContainer;
        }
        
        private Table CreateInfoSection(Skin skin)
        {
            var infoTable = new Table();
            
            // Left column: Job and Level info
            var leftCol = new Table();
            _jobNameLabel = new Label("Job: Unknown", skin);
            leftCol.Add(_jobNameLabel).Left();
            leftCol.Row();
            
            _levelLabel = new Label("Level: 1", skin);
            leftCol.Add(_levelLabel).Left();
            leftCol.Row();
            
            _jobLevelLabel = new Label("Job Level: 1", skin);
            leftCol.Add(_jobLevelLabel).Left();
            
            // Right column: JP info and stats
            var rightCol = new Table();
            _currentJPLabel = new Label("Current JP: 0", skin);
            rightCol.Add(_currentJPLabel).Left();
            rightCol.Row();
            
            _totalJPLabel = new Label("Total JP: 0", skin);
            rightCol.Add(_totalJPLabel).Left();
            rightCol.Row();
            
            _statsLabel = new Label("STR:0 AGI:0 VIT:0 MAG:0", skin);
            rightCol.Add(_statsLabel).Left();
            
            infoTable.Add(leftCol).Left().Expand().Pad(5f);
            infoTable.Add(rightCol).Right().Expand().Pad(5f);
            
            return infoTable;
        }
        
        private ScrollPane CreateSkillGrid(Skin skin)
        {
            _skillGridContainer = new Table();
            _skillGridContainer.Defaults().Pad(2f);
            
            var scrollPane = new ScrollPane(_skillGridContainer, skin);
            scrollPane.SetScrollingDisabled(true, false);
            
            return scrollPane;
        }
        
        /// <summary>Updates the tab with hero data</summary>
        public void UpdateWithHero(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            
            if (_heroComponent?.LinkedHero == null)
            {
                ClearDisplay();
                return;
            }
            
            var hero = _heroComponent.LinkedHero;
            
            // Update info labels
            _jobNameLabel.SetText($"Job: {hero.Job.Name}");
            _levelLabel.SetText($"Level: {hero.Level}");
            _jobLevelLabel.SetText($"Job Level: {hero.GetJobLevel()}");
            _currentJPLabel.SetText($"Current JP: {hero.GetCurrentJP()}");
            _totalJPLabel.SetText($"Total JP: {hero.GetTotalJP()}");
            
            var stats = hero.GetTotalStats();
            _statsLabel.SetText($"STR:{stats.Strength} AGI:{stats.Agility} VIT:{stats.Vitality} MAG:{stats.Magic}");
            
            // Rebuild skill grid
            RebuildSkillGrid(hero);
        }
        
        private void RebuildSkillGrid(Hero hero)
        {
            _skillGridContainer.Clear();
            _skillButtons.Clear();
            
            var skills = hero.Job.Skills;
            if (skills.Count == 0)
            {
                var noSkillsLabel = new Label("No skills available", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray });
                _skillGridContainer.Add(noSkillsLabel).Center();
                return;
            }
            
            // Create skill buttons in a grid (4 columns)
            const int columns = 4;
            int row = 0;
            int col = 0;
            
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var isLearned = hero.LearnedSkills.ContainsKey(skill.Id);
                
                var skillButton = new SkillButton(skill, isLearned);
                skillButton.OnHover += OnSkillHover;
                skillButton.OnUnhover += OnSkillUnhover;
                skillButton.OnClick += OnSkillClick;
                
                _skillButtons.Add(skillButton);
                _skillGridContainer.Add(skillButton).Size(32f, 32f);
                
                col++;
                if (col >= columns)
                {
                    col = 0;
                    row++;
                    _skillGridContainer.Row();
                }
            }
        }
        
        private void OnSkillHover(ISkill skill, bool isLearned)
        {
            if (_stage == null) return;
            
            // Show tooltip at cursor
            _skillTooltip.ShowSkill(skill, isLearned, _heroComponent?.LinkedHero);
            if (_skillTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_skillTooltip.GetContainer());
            }
            
            var mousePos = _stage.GetMousePosition();
            _skillTooltip.GetContainer().SetPosition(mousePos.X + 10, mousePos.Y + 10);
            _skillTooltip.GetContainer().ToFront();
        }
        
        private void OnSkillUnhover()
        {
            _skillTooltip.GetContainer().Remove();
        }
        
        private void OnSkillClick(ISkill skill, bool isLearned)
        {
            if (isLearned)
            {
                // Already learned, do nothing
                return;
            }
            
            if (_heroComponent?.LinkedHero == null)
                return;
            
            var hero = _heroComponent.LinkedHero;
            
            // Check if can afford
            if (hero.GetCurrentJP() < skill.JPCost)
            {
                Debug.Log($"[HeroCrystalTab] Cannot afford skill {skill.Name} (Cost: {skill.JPCost} JP, Current: {hero.GetCurrentJP()} JP)");
                return;
            }
            
            // Show confirmation dialog
            _pendingSkillPurchase = skill;
            ShowConfirmationDialog(skill);
        }
        
        private void CreateConfirmationDialog(Skin skin)
        {
            _confirmDialog = new Dialog("Confirm Purchase", skin);
            
            var contentTable = _confirmDialog.GetContentTable();
            var messageLabel = new Label("", skin);
            messageLabel.SetWrap(true);
            contentTable.Add(messageLabel).Width(250f).Pad(10f);
            
            var buttonTable = _confirmDialog.GetButtonTable();
            
            var yesButton = new TextButton("Yes", skin);
            yesButton.OnClicked += (btn) => ConfirmPurchase();
            buttonTable.Add(yesButton).Width(80f).Pad(5f);
            
            var noButton = new TextButton("No", skin);
            noButton.OnClicked += (btn) => CancelPurchase();
            buttonTable.Add(noButton).Width(80f).Pad(5f);
        }
        
        private void ShowConfirmationDialog(ISkill skill)
        {
            if (_confirmDialog == null || _stage == null)
                return;
            
            var contentTable = _confirmDialog.GetContentTable();
            var messageLabel = contentTable.GetChildren().First() as Label;
            if (messageLabel != null)
            {
                messageLabel.SetText($"Learn {skill.Name} for {skill.JPCost} JP?\n\n{skill.Kind} Skill\nAP Cost: {skill.APCost}");
            }
            
            _confirmDialog.Show(_stage);
            _confirmDialog.SetPosition(
                (_stage.GetWidth() - _confirmDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _confirmDialog.GetHeight()) / 2
            );
        }
        
        private void ConfirmPurchase()
        {
            if (_pendingSkillPurchase == null || _heroComponent?.LinkedHero == null)
            {
                _confirmDialog?.Hide();
                return;
            }
            
            var hero = _heroComponent.LinkedHero;
            if (hero.TryPurchaseSkill(_pendingSkillPurchase))
            {
                Debug.Log($"[HeroCrystalTab] Successfully purchased skill: {_pendingSkillPurchase.Name}");
                
                // Refresh the display
                UpdateWithHero(_heroComponent);
            }
            else
            {
                Debug.Log($"[HeroCrystalTab] Failed to purchase skill: {_pendingSkillPurchase.Name}");
            }
            
            _pendingSkillPurchase = null;
            _confirmDialog?.Hide();
        }
        
        private void CancelPurchase()
        {
            _pendingSkillPurchase = null;
            _confirmDialog?.Hide();
        }
        
        /// <summary>Updates tooltip position if visible</summary>
        public void Update()
        {
            if (_skillTooltip != null && _skillTooltip.GetContainer().HasParent() && _stage != null)
            {
                var mousePos = _stage.GetMousePosition();
                _skillTooltip.GetContainer().SetPosition(mousePos.X + 10, mousePos.Y + 10);
            }
        }
        
        private void ClearDisplay()
        {
            _jobNameLabel?.SetText("Job: Unknown");
            _levelLabel?.SetText("Level: 1");
            _jobLevelLabel?.SetText("Job Level: 1");
            _currentJPLabel?.SetText("Current JP: 0");
            _totalJPLabel?.SetText("Total JP: 0");
            _statsLabel?.SetText("STR:0 AGI:0 VIT:0 MAG:0");
            
            _skillGridContainer?.Clear();
            _skillButtons?.Clear();
        }
        
        /// <summary>Individual skill button with hover/click support</summary>
        private class SkillButton : Element, IInputListener
        {
            private ISkill _skill;
            private bool _isLearned;
            private SpriteDrawable _iconDrawable;
            
            public event System.Action<ISkill, bool> OnHover;
            public event System.Action OnUnhover;
            public event System.Action<ISkill, bool> OnClick;
            
            public SkillButton(ISkill skill, bool isLearned)
            {
                _skill = skill;
                _isLearned = isLearned;
                
                CreateButton();
                SetTouchable(Touchable.Enabled);
            }
            
            private void CreateButton()
            {
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                
                // Use different colored icons based on skill index for variety
                // Cycle through SkillIcon1-10
                var iconIndex = (System.Math.Abs(_skill.Id.GetHashCode()) % 10) + 1;
                var iconSprite = uiAtlas.GetSprite($"SkillIcon{iconIndex}");
                
                _iconDrawable = new SpriteDrawable(iconSprite);
                
                // If not learned, apply grayscale effect by reducing alpha
                if (!_isLearned)
                {
                    _iconDrawable.TintColor = new Color(128, 128, 128, 200);
                }
                
                SetSize(24f, 24f);
            }
            
            public override void Draw(Batcher batcher, float parentAlpha)
            {
                base.Draw(batcher, parentAlpha);
                
                if (_iconDrawable != null)
                {
                    _iconDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                }
            }
            
            #region IInputListener Implementation
            
            void IInputListener.OnMouseEnter()
            {
                OnHover?.Invoke(_skill, _isLearned);
            }
            
            void IInputListener.OnMouseExit()
            {
                OnUnhover?.Invoke();
            }
            
            void IInputListener.OnMouseMoved(Vector2 mousePos)
            {
                // Not needed
            }
            
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                return true;
            }
            
            bool IInputListener.OnRightMousePressed(Vector2 mousePos)
            {
                return false;
            }
            
            void IInputListener.OnLeftMouseUp(Vector2 mousePos)
            {
                OnClick?.Invoke(_skill, _isLearned);
            }
            
            void IInputListener.OnRightMouseUp(Vector2 mousePos)
            {
                // Not needed
            }
            
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta)
            {
                return false;
            }
            
            #endregion
        }
    }
}

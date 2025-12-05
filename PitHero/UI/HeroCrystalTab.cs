using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Synergies;
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
        
        // Skill grids (3 sections)
        private Table _jobSkillsGridContainer;
        private Table _synergySkillsGridContainer;
        private Table _synergyEffectsGridContainer;
        private List<SkillButton> _skillButtons;
        private List<SynergyEffectButton> _effectButtons;
        
        // Confirmation dialog
        private Dialog _confirmDialog;
        private ISkill _pendingSkillPurchase;
        
        // Tooltip for skills
        private SkillTooltip _skillTooltip;
        private Stage _stage;
        
        public HeroCrystalTab()
        {
            _skillButtons = new List<SkillButton>();
            _effectButtons = new List<SynergyEffectButton>();
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
            
            // Middle section: Three skill grids in scroll pane
            var skillGridsPane = CreateSkillGrids(skin);
            _mainContainer.Add(skillGridsPane).Expand().Fill().Pad(10f);
            
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
        
        private ScrollPane CreateSkillGrids(Skin skin)
        {
            var containerTable = new Table();
            
            // Section 1: Job Skills
            var jobSkillsLabel = new Label("Job Skills", skin);
            containerTable.Add(jobSkillsLabel).Left().Pad(5f);
            containerTable.Row();
            
            _jobSkillsGridContainer = new Table();
            _jobSkillsGridContainer.Defaults().Pad(2f);
            containerTable.Add(_jobSkillsGridContainer).Left().Pad(5f);
            containerTable.Row();
            
            // Section 2: Synergy Skills
            var synergySkillsLabel = new Label("Synergy Skills", skin);
            containerTable.Add(synergySkillsLabel).Left().Pad(5f).SetPadTop(10f);
            containerTable.Row();
            
            _synergySkillsGridContainer = new Table();
            _synergySkillsGridContainer.Defaults().Pad(2f);
            containerTable.Add(_synergySkillsGridContainer).Left().Pad(5f);
            containerTable.Row();
            
            // Section 3: Synergy Effects
            var synergyEffectsLabel = new Label("Synergy Effects", skin);
            containerTable.Add(synergyEffectsLabel).Left().Pad(5f).SetPadTop(10f);
            containerTable.Row();
            
            _synergyEffectsGridContainer = new Table();
            _synergyEffectsGridContainer.Defaults().Pad(2f);
            containerTable.Add(_synergyEffectsGridContainer).Left().Pad(5f);
            containerTable.Row();
            
            var scrollPane = new ScrollPane(containerTable, skin);
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
            _jobSkillsGridContainer.Clear();
            _synergySkillsGridContainer.Clear();
            _synergyEffectsGridContainer.Clear();
            _skillButtons.Clear();
            _effectButtons.Clear();
            
            var crystal = hero.BoundCrystal;
            if (crystal == null)
            {
                var noCrystalLabel = new Label("No crystal bound", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray });
                _jobSkillsGridContainer.Add(noCrystalLabel).Center();
                return;
            }
            
            // Section 1: Job Skills
            PopulateJobSkills(hero, crystal);
            
            // Section 2: Synergy Skills (discovered synergies with unlockable skills)
            PopulateSynergySkills(hero, crystal);
            
            // Section 3: Synergy Effects (active synergy patterns)
            PopulateSynergyEffects(hero);
        }
        
        private void PopulateJobSkills(Hero hero, HeroCrystal crystal)
        {
            var jobSkills = hero.Job.Skills;
            
            if (jobSkills.Count == 0)
            {
                var noSkillsLabel = new Label("No job skills available", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray });
                _jobSkillsGridContainer.Add(noSkillsLabel).Center();
                return;
            }
            
            const int columns = 4;
            int col = 0;
            
            for (int i = 0; i < jobSkills.Count; i++)
            {
                var skill = jobSkills[i];
                bool isLearned = crystal.HasSkill(skill.Id);
                
                var skillButton = new SkillButton(
                    skill, 
                    isLearned, 
                    false, // Not a synergy skill
                    0, 
                    0, 
                    null);
                skillButton.OnHover += OnSkillHover;
                skillButton.OnUnhover += OnSkillUnhover;
                skillButton.OnClick += OnSkillClick;
                
                _skillButtons.Add(skillButton);
                _jobSkillsGridContainer.Add(skillButton).Size(32f, 32f);
                
                col++;
                if (col >= columns)
                {
                    col = 0;
                    _jobSkillsGridContainer.Row();
                }
            }
        }
        
        private void PopulateSynergySkills(Hero hero, HeroCrystal crystal)
        {
            var discoveredSynergyIds = crystal.DiscoveredSynergyIds;
            var learnedSynergySkillIds = crystal.LearnedSynergySkillIds;
            var processedSkillIds = new HashSet<string>();
            var synergySkills = new List<SkillDisplayInfo>();
            
            foreach (var synergyId in discoveredSynergyIds)
            {
                var pattern = SynergyDetector.GetPatternById(synergyId);
                if (pattern == null || pattern.UnlockedSkill == null)
                    continue;
                
                var synergySkill = pattern.UnlockedSkill;
                
                // Skip if already processed (avoid duplicates)
                if (processedSkillIds.Contains(synergySkill.Id))
                    continue;
                processedSkillIds.Add(synergySkill.Id);
                
                bool isLearned = learnedSynergySkillIds.Contains(synergySkill.Id);
                int currentPoints = crystal.GetSynergyPoints(synergyId);
                int requiredPoints = pattern.SynergyPointsRequired;
                
                synergySkills.Add(new SkillDisplayInfo
                {
                    Skill = synergySkill,
                    IsLearned = isLearned,
                    IsSynergySkill = true,
                    SynergyCurrentPoints = currentPoints,
                    SynergyRequiredPoints = requiredPoints,
                    SynergyPatternId = synergyId
                });
            }
            
            if (synergySkills.Count == 0)
            {
                var noSkillsLabel = new Label("No synergy skills discovered", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray });
                _synergySkillsGridContainer.Add(noSkillsLabel).Center();
                return;
            }
            
            const int columns = 4;
            int col = 0;
            
            for (int i = 0; i < synergySkills.Count; i++)
            {
                var skillInfo = synergySkills[i];
                var skillButton = new SkillButton(
                    skillInfo.Skill, 
                    skillInfo.IsLearned, 
                    skillInfo.IsSynergySkill,
                    skillInfo.SynergyCurrentPoints,
                    skillInfo.SynergyRequiredPoints,
                    skillInfo.SynergyPatternId);
                skillButton.OnHover += OnSkillHover;
                skillButton.OnUnhover += OnSkillUnhover;
                skillButton.OnClick += OnSkillClick;
                
                _skillButtons.Add(skillButton);
                _synergySkillsGridContainer.Add(skillButton).Size(32f, 32f);
                
                col++;
                if (col >= columns)
                {
                    col = 0;
                    _synergySkillsGridContainer.Row();
                }
            }
        }
        
        private void PopulateSynergyEffects(Hero hero)
        {
            var activeSynergyGroups = hero.ActiveSynergyGroups;
            
            if (activeSynergyGroups.Count == 0)
            {
                var noEffectsLabel = new Label("No active synergy effects", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray });
                _synergyEffectsGridContainer.Add(noEffectsLabel).Center();
                return;
            }
            
            const int columns = 4;
            int col = 0;
            
            for (int i = 0; i < activeSynergyGroups.Count; i++)
            {
                var group = activeSynergyGroups[i];
                var pattern = group.Pattern;
                
                var effectButton = new SynergyEffectButton(pattern, group.InstanceCount, group.TotalMultiplier);
                effectButton.OnHover += OnEffectHover;
                effectButton.OnUnhover += OnEffectUnhover;
                
                _effectButtons.Add(effectButton);
                _synergyEffectsGridContainer.Add(effectButton).Size(32f, 32f);
                
                col++;
                if (col >= columns)
                {
                    col = 0;
                    _synergyEffectsGridContainer.Row();
                }
            }
        }
        
        private void OnSkillHover(ISkill skill, bool isLearned, bool isSynergySkill, int synergyCurrentPoints, int synergyRequiredPoints)
        {
            if (_stage == null) return;
            
            // Show tooltip at cursor
            _skillTooltip.ShowSkill(skill, isLearned, _heroComponent?.LinkedHero, isSynergySkill, synergyCurrentPoints, synergyRequiredPoints);
            if (_skillTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_skillTooltip.GetContainer());
            }
            
            var mousePos = _stage.GetMousePosition();
            _skillTooltip.PositionWithinBounds(mousePos, _stage);
            _skillTooltip.GetContainer().ToFront();
        }
        
        private void OnSkillUnhover()
        {
            _skillTooltip.GetContainer().Remove();
        }
        
        private void OnEffectHover(SynergyPattern pattern, int instanceCount, float multiplier)
        {
            if (_stage == null) return;
            
            // Show tooltip for synergy effect
            _skillTooltip.ShowSynergyEffect(pattern, instanceCount, multiplier);
            if (_skillTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_skillTooltip.GetContainer());
            }
            
            var mousePos = _stage.GetMousePosition();
            _skillTooltip.PositionWithinBounds(mousePos, _stage);
            _skillTooltip.GetContainer().ToFront();
        }
        
        private void OnEffectUnhover()
        {
            _skillTooltip.GetContainer().Remove();
        }
        
        private void OnSkillClick(ISkill skill, bool isLearned)
        {
            if (_heroComponent?.LinkedHero == null)
                return;
            
            var hero = _heroComponent.LinkedHero;
            
            // If skill is learned and Active, allow selecting it for shortcut bar
            if (isLearned && skill.Kind == SkillKind.Active)
            {
                // Toggle selection for active skills
                if (InventorySelectionManager.HasSelection() && 
                    InventorySelectionManager.IsSelectionFromHeroCrystalTab() &&
                    InventorySelectionManager.GetSelectedSkill() == skill)
                {
                    // Clicking the same skill deselects it
                    InventorySelectionManager.ClearSelection();
                    Debug.Log($"[HeroCrystalTab] Deselected skill: {skill.Name}");
                }
                else
                {
                    // Select this skill for assignment to shortcut bar
                    InventorySelectionManager.SetSelectedFromHeroCrystalTab(skill, _heroComponent);
                    Debug.Log($"[HeroCrystalTab] Selected skill for shortcut bar: {skill.Name}");
                }
                return;
            }
            
            // If skill is not learned, show purchase dialog
            if (!isLearned)
            {
                // Check if this is a synergy skill (can't be purchased with JP)
                if (hero.BoundCrystal != null)
                {
                    var learnedSynergyIds = hero.BoundCrystal.LearnedSynergySkillIds;
                    bool isSynergySkill = false;
                    foreach (var synergyId in learnedSynergyIds)
                    {
                        if (synergyId == skill.Id)
                        {
                            isSynergySkill = true;
                            break;
                        }
                    }
                    
                    if (isSynergySkill)
                    {
                        Debug.Log($"[HeroCrystalTab] Cannot purchase synergy skill {skill.Name} - it is unlocked automatically through synergy points");
                        return;
                    }
                }
                
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
                messageLabel.SetText($"Learn {skill.Name} for {skill.JPCost} JP?\n\n{skill.Kind} Skill\nMP Cost: {skill.MPCost}");
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
                _skillTooltip.PositionWithinBounds(mousePos, _stage);
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
            
            _jobSkillsGridContainer?.Clear();
            _synergySkillsGridContainer?.Clear();
            _synergyEffectsGridContainer?.Clear();
            _skillButtons?.Clear();
            _effectButtons?.Clear();
        }
        
        /// <summary>Helper struct to track skill display information</summary>
        private struct SkillDisplayInfo
        {
            public ISkill Skill;
            public bool IsLearned;
            public bool IsSynergySkill;
            public int SynergyCurrentPoints;
            public int SynergyRequiredPoints;
            public string SynergyPatternId;
        }
        
        /// <summary>Individual skill button with hover/click support</summary>
        private class SkillButton : Element, IInputListener
        {
            private ISkill _skill;
            private bool _isLearned;
            private bool _isSynergySkill;
            private int _synergyCurrentPoints;
            private int _synergyRequiredPoints;
            private string _synergyPatternId;
            private SpriteDrawable _iconDrawable;
            private Sprite _selectBoxSprite;
            private SpriteDrawable _selectBoxDrawable;
            private Sprite _highlightBoxSprite;
            private SpriteDrawable _highlightBoxDrawable;
            private bool _isHovered;
            
            public event System.Action<ISkill, bool, bool, int, int> OnHover;
            public event System.Action OnUnhover;
            public event System.Action<ISkill, bool> OnClick;
            
            public SkillButton(ISkill skill, bool isLearned, bool isSynergySkill = false, 
                int synergyCurrentPoints = 0, int synergyRequiredPoints = 0, string synergyPatternId = null)
            {
                _skill = skill;
                _isLearned = isLearned;
                _isSynergySkill = isSynergySkill;
                _synergyCurrentPoints = synergyCurrentPoints;
                _synergyRequiredPoints = synergyRequiredPoints;
                _synergyPatternId = synergyPatternId;
                
                CreateButton();
                SetTouchable(Touchable.Enabled);
            }
            
            private void CreateButton()
            {
                // Load skill icon from SkillsStencils atlas using skill ID
                var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                
                // Try to get the sprite using the skill's ID
                var iconSprite = skillsAtlas.GetSprite(_skill.Id);
                
                // Load UI atlas once for both fallback, SelectBox, and HighlightBox
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                
                // Fallback to a default icon if sprite not found
                if (iconSprite == null)
                {
                    iconSprite = uiAtlas.GetSprite("SkillIcon1");
                }
                
                _iconDrawable = new SpriteDrawable(iconSprite);
                
                // Load SelectBox sprite for hover visualization
                _selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                _selectBoxDrawable = new SpriteDrawable(_selectBoxSprite);
                
                // Load HighlightBox sprite for selection visualization
                _highlightBoxSprite = uiAtlas.GetSprite("HighlightBox");
                _highlightBoxDrawable = new SpriteDrawable(_highlightBoxSprite);
                
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
                
                // Draw SelectBox if hovering over a learned Active skill
                if (_isLearned && _skill.Kind == SkillKind.Active && _isHovered && _selectBoxDrawable != null)
                {
                    _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                }
                
                // Draw HighlightBox if this skill is selected and is an Active skill
                if (_isLearned && _skill.Kind == SkillKind.Active && 
                    InventorySelectionManager.IsSelectionFromHeroCrystalTab() &&
                    InventorySelectionManager.GetSelectedSkill() == _skill &&
                    _highlightBoxDrawable != null)
                {
                    _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                }
            }
            
            #region IInputListener Implementation
            
            void IInputListener.OnMouseEnter()
            {
                _isHovered = true;
                OnHover?.Invoke(_skill, _isLearned, _isSynergySkill, _synergyCurrentPoints, _synergyRequiredPoints);
            }
            
            void IInputListener.OnMouseExit()
            {
                _isHovered = false;
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
        
        /// <summary>Button for displaying synergy pattern effects (not learnable, only active)</summary>
        private class SynergyEffectButton : Element, IInputListener
        {
            private SynergyPattern _pattern;
            private int _instanceCount;
            private float _multiplier;
            private SpriteDrawable _iconDrawable;
            
            public event System.Action<SynergyPattern, int, float> OnHover;
            public event System.Action OnUnhover;
            
            public SynergyEffectButton(SynergyPattern pattern, int instanceCount, float multiplier)
            {
                _pattern = pattern;
                _instanceCount = instanceCount;
                _multiplier = multiplier;
                
                CreateButton();
                SetTouchable(Touchable.Enabled);
            }
            
            private void CreateButton()
            {
                // Load synergy effect icon from SkillsStencils atlas using pattern ID
                var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                
                // Use the pattern ID as sprite name (same as StencilLibraryPanel logic)
                string spriteName;
                if (_pattern.UnlockedSkill != null)
                {
                    spriteName = _pattern.UnlockedSkill.Id;
                }
                else
                {
                    spriteName = _pattern.Id;
                }
                
                var iconSprite = skillsAtlas.GetSprite(spriteName);
                
                // Fallback to a default icon if sprite not found
                if (iconSprite == null)
                {
                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    iconSprite = uiAtlas.GetSprite("SkillIcon1");
                }
                
                _iconDrawable = new SpriteDrawable(iconSprite);
                _iconDrawable.TintColor = new Color(200, 255, 128, 200);

                // Active synergy effects are always shown in full color

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
                OnHover?.Invoke(_pattern, _instanceCount, _multiplier);
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
                // Effects can't be clicked
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

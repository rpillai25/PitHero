using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System;

namespace PitHero.UI
{
    /// <summary>Dialog for creating new crystals from primary jobs.  Presents a &lt; Job &gt; arrow
    /// selector on the left and a live Job Info panel on the right, matching the Hero Creation UI.</summary>
    public class CrystalCreationDialog : Window
    {
        // ── Job data ──────────────────────────────────────────────────────────────
        private static readonly string[] JobNames = { "Knight", "Monk", "Mage", "Priest", "Archer", "Thief" };
        private static readonly IJob[]   Jobs     = { new Knight(), new Monk(), new Mage(), new Priest(), new Archer(), new Thief() };

        // Font colors matching PitHeroSkin / HeroCreationUI conventions
        private static readonly Color BrownFontColor  = new Color(71, 36, 7);
        private static readonly Color DetailFontColor = new Color(37, 80, 112);

        // ── State ─────────────────────────────────────────────────────────────────
        private int _currentJobIndex;
        private Label _jobLabel;
        private Table _jobInfoTable;
        private readonly List<JobSkillButton> _skillButtons = new List<JobSkillButton>(8);
        private SkillTooltip _skillTooltip;

        private CrystalCollectionService _crystalService;
        private TextService _textService;
        private Skin _skin;
        private Stage _stage;
        private TextButton _createBtn;
        private int _lastFundsSeen = -1;

        public event Action<HeroCrystal> OnCrystalCreated;
        public event Action OnClosed;

        // Shown dialogs, tracked so HeroUI's polled outside-click dismissal and Escape handling can
        // defer to an open creation dialog (same pattern as ConfirmationDialog). Self-pruning.
        private static readonly List<CrystalCreationDialog> _shown = new List<CrystalCreationDialog>();

        /// <summary>True while any creation dialog is on a stage and visible.</summary>
        public static bool AnyVisible
        {
            get
            {
                Prune();
                return _shown.Count > 0;
            }
        }

        /// <summary>Frame on which a creation dialog last closed. Lets same-frame outside-click
        /// polling ignore the click that dismissed the dialog.</summary>
        public static uint LastCloseFrame { get; private set; }

        /// <summary>Closes the most recently shown visible dialog. Returns true if one was closed
        /// (used by Escape handling to peel the dialog before the hero window).</summary>
        public static bool TryCloseTopMost()
        {
            Prune();
            if (_shown.Count == 0) return false;
            _shown[_shown.Count - 1].Close();
            return true;
        }

        private static void Prune()
        {
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var d = _shown[i];
                if (d.GetParent() == null || !d.IsVisible())
                    _shown.RemoveAt(i);
            }
        }

        /// <summary>Single close path: stamps LastCloseFrame, fires OnClosed and removes the dialog.</summary>
        public void Close()
        {
            LastCloseFrame = Time.FrameCount;
            _shown.Remove(this);
            OnClosed?.Invoke();
            Remove();
        }

        public CrystalCreationDialog(Skin skin, Stage stage, CrystalCollectionService crystalService) : base("", skin)
        {
            _skin          = skin;
            _stage         = stage;
            _crystalService = crystalService;
            _textService   = Core.Services?.GetService<TextService>();

            SetMovable(true);
            SetResizable(false);

            // ── Main horizontal layout ─────────────────────────────────────────
            var mainTable = new Table();
            mainTable.Pad(10f);

            // ── Left panel: title + selector + buttons ─────────────────────────
            var leftTable = new Table();
            leftTable.Top().Left();

            var titleText = GetText(UITextKey.CrystalCreationTitle);
            leftTable.Add(new Label(titleText, skin, "ph-default")).Left().Pad(4);
            leftTable.Row();

            // Fee line, gold-colored like MercenaryHireDialog's cost label
            var defaultLabelStyle = skin.Get<LabelStyle>("ph-default");
            var feeLabelStyle = new LabelStyle
            {
                Font = defaultLabelStyle.Font,
                FontColor = new Color(184, 138, 13)
            };
            var feeLabel = new Label(string.Format(GetText(UITextKey.CrystalCreationFeeLabel), GameConfig.CrystalCreationFee), feeLabelStyle);
            leftTable.Add(feeLabel).Left().Pad(0, 4, 4, 4);
            leftTable.Row();

            // < JobName > row
            var selectorRow = new Table();
            var prevBtn = new TextButton("<", skin, "ph-default");
            var nextBtn = new TextButton(">", skin, "ph-default");
            _jobLabel = new Label(JobNames[_currentJobIndex], skin, "ph-default");

            prevBtn.OnClicked += _ =>
            {
                _currentJobIndex = (_currentJobIndex - 1 + JobNames.Length) % JobNames.Length;
                _jobLabel.SetText(JobNames[_currentJobIndex]);
                RefreshJobInfo();
            };
            nextBtn.OnClicked += _ =>
            {
                _currentJobIndex = (_currentJobIndex + 1) % JobNames.Length;
                _jobLabel.SetText(JobNames[_currentJobIndex]);
                RefreshJobInfo();
            };

            selectorRow.Add(prevBtn).Size(30f, 24f);
            selectorRow.Add(_jobLabel).Width(64f).Pad(4, 10, 4, 10);
            selectorRow.Add(nextBtn).Size(30f, 24f);
            leftTable.Add(selectorRow).Left().Pad(6);
            leftTable.Row();

            // Create / Cancel buttons. Create gets a cloned style so it has a disabled
            // visual when the player can't afford the fee (shared styles must never be mutated).
            var baseButtonStyle = skin.Get<TextButtonStyle>("ph-default");
            var createButtonStyle = new TextButtonStyle
            {
                Up = baseButtonStyle.Up,
                Down = baseButtonStyle.Down,
                Over = baseButtonStyle.Over,
                FontColor = baseButtonStyle.FontColor,
                DownFontColor = baseButtonStyle.DownFontColor,
                OverFontColor = baseButtonStyle.OverFontColor,
                DisabledFontColor = Color.Gray,
                PressedOffsetX = baseButtonStyle.PressedOffsetX,
                PressedOffsetY = baseButtonStyle.PressedOffsetY
            };
            _createBtn = new TextButton(GetText(UITextKey.CrystalCreationCreateButton), createButtonStyle);
            _createBtn.OnClicked += OnCreateClicked;
            var cancelBtn = new TextButton(GetText(UITextKey.ButtonCancel), skin, "ph-default");
            cancelBtn.OnClicked += _ => Close();

            var btnRow = new Table();
            btnRow.Add(_createBtn).Height(24).Pad(4);
            btnRow.Add(cancelBtn).Height(24).Pad(4);
            leftTable.Add(btnRow).Left();
            UpdateCreateAffordability();

            // ── Right panel: job info ──────────────────────────────────────────
            _jobInfoTable = new Table();
            _jobInfoTable.Top().Left();

            mainTable.Add(leftTable).Top().Left().Width(185f);
            mainTable.Add(_jobInfoTable).Top().Left().Width(280f).SetPadLeft(28f);

            Add(mainTable).Expand().Fill();
            Pack();

            // Position near center of stage
            float dialogW = GetWidth();
            float dialogH = GetHeight();
            SetPosition(
                (_stage.GetWidth()  - dialogW) / 2f,
                UILayout.CenterY(dialogH, _stage.GetHeight(), 0f)
            );

            _skillTooltip = new SkillTooltip(this, skin);
            RefreshJobInfo();

            // Track for HeroUI's outside-click/Escape deferral. The dialog is added to the stage
            // synchronously right after construction, so pruning never sees a pre-stage gap.
            _shown.Add(this);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;

        /// <summary>Disables the Create button while the player can't cover the creation fee.
        /// Re-evaluated from Draw whenever funds change (the game keeps running behind the dialog).</summary>
        private void UpdateCreateAffordability()
        {
            var funds = Core.Services?.GetService<GameStateService>()?.Funds ?? 0;
            _lastFundsSeen = funds;
            _createBtn.SetDisabled(funds < GameConfig.CrystalCreationFee);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            var funds = Core.Services?.GetService<GameStateService>()?.Funds ?? 0;
            if (funds != _lastFundsSeen)
                UpdateCreateAffordability();
            base.Draw(batcher, parentAlpha);
        }

        /// <summary>Repopulates the job info panel with the currently selected job's data.</summary>
        private void RefreshJobInfo()
        {
            _jobInfoTable.Clear();

            for (int i = 0; i < _skillButtons.Count; i++)
            {
                _skillButtons[i].OnHover   -= OnSkillHover;
                _skillButtons[i].OnUnhover -= OnSkillUnhover;
            }
            _skillButtons.Clear();

            _skillTooltip?.Hide();

            var job = Jobs[_currentJobIndex];

            // Job name
            var nameLabel = new Label(job.Name, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _jobInfoTable.Add(nameLabel).Left();
            _jobInfoTable.Row();

            // Description
            if (!string.IsNullOrEmpty(job.Description))
            {
                var descLabel = new Label(job.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = DetailFontColor });
                descLabel.SetWrap(true);
                _jobInfoTable.Add(descLabel).Width(260f).Left().SetPadTop(4f);
                _jobInfoTable.Row();
            }

            // Role
            if (!string.IsNullOrEmpty(job.Role))
            {
                var rolePrefix = _textService?.DisplayText(TextType.UI, UITextKey.AppearanceRolePrefix) ?? "Role: ";
                var roleLabel = new Label(rolePrefix + job.Role, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
                roleLabel.SetWrap(true);
                _jobInfoTable.Add(roleLabel).Width(260f).Left().SetPadTop(4f);
                _jobInfoTable.Row();
            }

            // Skills header
            var skillsHeader = _textService?.DisplayText(TextType.UI, UITextKey.AppearanceSkillsLabel) ?? "Skills";
            _jobInfoTable.Add(new Label(skillsHeader, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor })).Left().SetPadTop(8f);
            _jobInfoTable.Row();

            // Skills grid
            var skillGrid = new Table();
            var jobSkills = job.Skills;

            if (jobSkills.Count == 0)
            {
                var noSkillsText = _textService?.DisplayText(TextType.UI, UITextKey.AppearanceNoJobSkills) ?? "No skills";
                skillGrid.Add(new Label(noSkillsText, _skin, "ph-default")).Center();
            }
            else
            {
                const int columns = 4;
                int col = 0;
                for (int i = 0; i < jobSkills.Count; i++)
                {
                    var skill = jobSkills[i];
                    var btn   = new JobSkillButton(skill);
                    btn.OnHover   += OnSkillHover;
                    btn.OnUnhover += OnSkillUnhover;
                    _skillButtons.Add(btn);
                    skillGrid.Add(btn).Size(32f, 32f).Pad(2f);
                    col++;
                    if (col >= columns) { col = 0; skillGrid.Row(); }
                }
            }

            _jobInfoTable.Add(skillGrid).Left().SetPadTop(4f);
        }

        private void OnSkillHover(ISkill skill)
        {
            if (_stage == null || _skillTooltip == null) return;
            _skillTooltip.ShowSkill(skill, false, null, false, 0, 0, showCostAndStatus: false);
            if (_skillTooltip.GetContainer().GetParent() == null)
                _stage.AddElement(_skillTooltip.GetContainer());
            _skillTooltip.PositionWithinBounds(_stage.GetMousePosition(), _stage);
            _skillTooltip.GetContainer().ToFront();
        }

        private void OnSkillUnhover()
        {
            _skillTooltip?.HideOnUnhover();
        }

        private void OnCreateClicked(Button btn)
        {
            if (_createBtn.GetDisabled())
                return;

            // Lazy-fetch service in case it wasn't available at construction time.
            if (_crystalService == null)
                _crystalService = Core.Services?.GetService<CrystalCollectionService>();

            var gameState = Core.Services?.GetService<GameStateService>();
            if (gameState == null || gameState.Funds < GameConfig.CrystalCreationFee)
                return;

            var job  = Jobs[_currentJobIndex];
            var stats = new StatBlock(
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6)
            );
            var crystal = new HeroCrystal(JobNames[_currentJobIndex], job, 1, stats);
            if (_crystalService != null && _crystalService.TryAddToInventory(crystal))
            {
                // Deduct only after the crystal actually landed in the inventory
                gameState.Funds -= GameConfig.CrystalCreationFee;
                OnCrystalCreated?.Invoke(crystal);
                Close();
            }
        }

        // ── Private inner class: skill button with hover support ──────────────────

        /// <summary>Draws a skill icon with a hover highlight, matching the HeroCreationUI pattern.</summary>
        private class JobSkillButton : Element, IInputListener
        {
            private readonly ISkill     _skill;
            private SpriteDrawable _iconDrawable;
            private SpriteDrawable _selectBoxDrawable;
            private bool _isHovered;

            public event Action<ISkill> OnHover;
            public event Action         OnUnhover;

            public JobSkillButton(ISkill skill)
            {
                _skill = skill;
                if (Core.Content != null)
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var icon        = skillsAtlas.GetSprite(skill.Id);
                    var uiAtlas     = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    if (icon == null) icon = uiAtlas.GetSprite("SkillIcon1");
                    _iconDrawable      = new SpriteDrawable(icon);
                    _selectBoxDrawable = new SpriteDrawable(uiAtlas.GetSprite("SelectBox"));
                }
                SetSize(32f, 32f);
                SetTouchable(Touchable.Enabled);
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                base.Draw(batcher, parentAlpha);
                _iconDrawable?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                if (_isHovered) _selectBoxDrawable?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            void IInputListener.OnMouseEnter()          { _isHovered = true;  OnHover?.Invoke(_skill); }
            void IInputListener.OnMouseExit()           { _isHovered = false; OnUnhover?.Invoke(); }
            void IInputListener.OnMouseMoved(Vector2 _) { }
            bool IInputListener.OnLeftMousePressed(Vector2 _)  => true;
            bool IInputListener.OnRightMousePressed(Vector2 _) => false;
            void IInputListener.OnLeftMouseUp(Vector2 _)  { }
            void IInputListener.OnRightMouseUp(Vector2 _) { }
            bool IInputListener.OnMouseScrolled(int _)    => false;
        }
    }
}

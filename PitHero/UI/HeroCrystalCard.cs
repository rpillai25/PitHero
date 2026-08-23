using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Synergies;
using System;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>UI window that displays detailed information about a hero crystal.</summary>
    public class HeroCrystalCard : Window
    {
        private const float CARD_WIDTH = 200f;
        private const float CARD_PADDING = 5f;
        private const int SKILL_COLUMNS = 4;
        private const float SKILL_BUTTON_SIZE = 28f;
        private const float SKILL_SCROLL_HEIGHT = 80f;

        private static readonly Color BrownFontColor = new Color(71, 36, 7);

        private HeroCrystal _crystal;
        private Table _contentTable;
        private TextService _textService;
        private SkillTooltip _skillTooltip;
        private new Stage _stage;
        private Skin _skin;
        private readonly List<SkillIconButton> _skillButtons = new List<SkillIconButton>(16);

        public HeroCrystalCard(Skin skin, Stage stage) : base("", skin)
        {
            _skin = skin;
            _stage = stage;
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);

            _skillTooltip = new SkillTooltip(new Element(), skin);

            _contentTable = new Table();
            Add(_contentTable).Expand().Fill().Pad(CARD_PADDING);

            SetWidth(CARD_WIDTH);
            SetVisible(false);
        }

        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService;
        }

        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key;
        }

        /// <summary>Populates the card with the given crystal's data and makes it visible.</summary>
        public void ShowCrystal(HeroCrystal crystal)
        {
            _crystal = crystal;
            if (_crystal == null)
            {
                SetVisible(false);
                return;
            }

            RebuildContent();
            SetVisible(true);
            Pack();
        }

        /// <summary>Hides the crystal card and the skill tooltip.</summary>
        public void Hide()
        {
            SetVisible(false);
            _crystal = null;
            _skillTooltip?.Hide();
        }

        /// <summary>Positions the card so its left edge is just past the right edge of the given window, shifted down 32px.</summary>
        public void PositionAtWindowRight(Window heroWindow)
        {
            if (heroWindow == null) return;
            float x = heroWindow.GetX() + heroWindow.GetWidth() + 10f;
            float y = heroWindow.GetY() + 32f;
            float stageH = _stage.GetHeight();
            if (y + GetHeight() > stageH) y = stageH - GetHeight();
            if (y < 0) y = 0;
            SetPosition(x, y);
        }

        /// <summary>Positions the card so its left edge is just to the left of the given window, shifted down 32px.</summary>
        public void PositionAtWindowLeft(Window window)
        {
            if (window == null) return;
            Pack();
            float x = window.GetX() - GetWidth() - 10f;
            float y = window.GetY() + 32f;
            float stageH = _stage.GetHeight();
            if (y + GetHeight() > stageH) y = stageH - GetHeight();
            if (y < 0) y = 0;
            if (x < 0) x = 0;
            SetPosition(x, y);
        }

        private void RebuildContent()
        {
            _contentTable.Clear();
            _skillButtons.Clear();

            if (_crystal == null) return;

            // Title: Job name (Expert/Hero/Legend/Chosen One for combo crystals)
            var titleText = _crystal.Job.Name;
            var titleStyle = new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = _crystal.Color };
            _contentTable.Add(new Label(titleText, titleStyle)).Left().Pad(0, 0, 2, 0);
            _contentTable.Row();

            // Stats
            var statsText = string.Format(
                GetText(TextType.UI, UITextKey.CrystalCardStatsLabel),
                _crystal.BaseStats.Strength, _crystal.BaseStats.Agility,
                _crystal.BaseStats.Vitality, _crystal.BaseStats.Magic);
            AddDefaultLabel(statsText, padTop: 3);

            // ── Job Skills ───────────────────────────────────────────────────────
            AddDefaultLabel(GetText(TextType.UI, UITextKey.CrystalCardJobSkillsLabel), padTop: 4);

            var jobSkillsTable = new Table();
            jobSkillsTable.Defaults().Pad(1f);
            var jobSkills = _crystal.Job.Skills;
            int col = 0;
            for (int i = 0; i < jobSkills.Count; i++)
            {
                var skill = jobSkills[i];
                bool isLearned = _crystal.HasSkill(skill.Id);
                var btn = new SkillIconButton(skill, isLearned, false);
                btn.OnHover += OnSkillHover;
                btn.OnUnhover += OnSkillUnhover;
                _skillButtons.Add(btn);
                jobSkillsTable.Add(btn).Size(SKILL_BUTTON_SIZE);
                col++;
                if (col >= SKILL_COLUMNS) { jobSkillsTable.Row(); col = 0; }
            }
            if (jobSkills.Count == 0)
                jobSkillsTable.Add(new Label("-", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray }));

            var jobScrollPane = new ScrollPane(jobSkillsTable, _skin, "ph-default");
            jobScrollPane.SetScrollingDisabled(true, false);
            jobScrollPane.SetFadeScrollBars(false);
            _contentTable.Add(jobScrollPane).Left().Height(SKILL_SCROLL_HEIGHT).Width(CARD_WIDTH - CARD_PADDING * 2);
            _contentTable.Row();

            // ── Synergy Skills ───────────────────────────────────────────────────
            AddDefaultLabel(GetText(TextType.UI, UITextKey.CrystalCardSynergySkillsLabel), padTop: 4);

            var synergyTable = new Table();
            synergyTable.Defaults().Pad(1f);
            col = 0;

            var discoveredSynergyIds = _crystal.DiscoveredSynergyIds;
            // Cast to ICollection<string> to access Contains without LINQ
            var learnedSynergyIds = _crystal.LearnedSynergySkillIds as ICollection<string>;
            var processedSkillIds = new HashSet<string>();

            int synergyCount = 0;
            // IReadOnlyCollection has no indexer, use GetEnumerator manually (UI-only code path)
            var discoveredEnumerator = discoveredSynergyIds.GetEnumerator();
            while (discoveredEnumerator.MoveNext())
            {
                var synergyId = discoveredEnumerator.Current;
                var pattern = SynergyPatternRegistry.GetById(synergyId);
                if (pattern?.UnlockedSkill == null) continue;
                var skill = pattern.UnlockedSkill;
                if (!processedSkillIds.Add(skill.Id)) continue;
                bool isLearned = learnedSynergyIds != null && learnedSynergyIds.Contains(skill.Id);
                var btn = new SkillIconButton(skill, isLearned, true,
                    _crystal.GetSynergyPoints(synergyId), pattern.SynergyPointsRequired);
                btn.OnHover += OnSkillHover;
                btn.OnUnhover += OnSkillUnhover;
                _skillButtons.Add(btn);
                synergyTable.Add(btn).Size(SKILL_BUTTON_SIZE);
                col++;
                if (col >= SKILL_COLUMNS) { synergyTable.Row(); col = 0; }
                synergyCount++;
            }
            if (synergyCount == 0)
                synergyTable.Add(new Label("-", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Gray }));

            var synergyScrollPane = new ScrollPane(synergyTable, _skin, "ph-default");
            synergyScrollPane.SetScrollingDisabled(true, false);
            synergyScrollPane.SetFadeScrollBars(false);
            _contentTable.Add(synergyScrollPane).Left().Height(SKILL_SCROLL_HEIGHT).Width(CARD_WIDTH - CARD_PADDING * 2);
            _contentTable.Row();

            Pack();
        }

        private void AddDefaultLabel(string text, float padTop = 0)
        {
            var lbl = new Label(text, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _contentTable.Add(lbl).Left().Pad(padTop, 0, 2, 0);
            _contentTable.Row();
        }

        private void OnSkillHover(ISkill skill, bool isLearned, bool isSynergy, int synergyCurrentPoints, int synergyRequiredPoints)
        {
            if (_stage == null) return;
            _skillTooltip.ShowSkill(skill, isLearned, null, isSynergy, synergyCurrentPoints, synergyRequiredPoints);
            if (_skillTooltip.GetContainer().GetParent() == null)
                _stage.AddElement(_skillTooltip.GetContainer());
            _skillTooltip.PositionWithinBounds(_stage.GetMousePosition(), _stage);
            _skillTooltip.GetContainer().ToFront();
        }

        private void OnSkillUnhover()
        {
            _skillTooltip?.HideOnUnhover();
        }

        // ── Private skill icon button ───────────────────────────────────────────

        private class SkillIconButton : Element, IInputListener
        {
            private readonly ISkill _skill;
            private readonly bool _isLearned;
            private readonly bool _isSynergy;
            private readonly int _synergyCurrentPoints;
            private readonly int _synergyRequiredPoints;
            private SpriteDrawable _iconDrawable;
            private SpriteDrawable _highlightBoxDrawable;
            private bool _isHovered;

            public event Action<ISkill, bool, bool, int, int> OnHover;
            public event Action OnUnhover;

            public SkillIconButton(ISkill skill, bool isLearned, bool isSynergy,
                int synergyCurrentPoints = 0, int synergyRequiredPoints = 0)
            {
                _skill = skill;
                _isLearned = isLearned;
                _isSynergy = isSynergy;
                _synergyCurrentPoints = synergyCurrentPoints;
                _synergyRequiredPoints = synergyRequiredPoints;
                if (Core.Content != null)
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    var icon = skillsAtlas.GetSprite(skill.Id) ?? uiAtlas.GetSprite("SkillIcon1");
                    _iconDrawable = new SpriteDrawable(icon);
                    // Unlearned skills draw faded, matching HeroCrystalTab
                    _iconDrawable.TintColor = isLearned ? Color.White : new Color(128, 128, 128, 200);
                    var hl = uiAtlas.GetSprite("HighlightBox");
                    if (hl != null) _highlightBoxDrawable = new SpriteDrawable(hl);
                }
                SetSize(28f, 28f);
                SetTouchable(Touchable.Enabled);
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                base.Draw(batcher, parentAlpha);
                _iconDrawable?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                if (_isHovered && _highlightBoxDrawable != null)
                    _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            void IInputListener.OnMouseEnter()  { _isHovered = true;  OnHover?.Invoke(_skill, _isLearned, _isSynergy, _synergyCurrentPoints, _synergyRequiredPoints); }
            void IInputListener.OnMouseExit()   { _isHovered = false; OnUnhover?.Invoke(); }
            void IInputListener.OnMouseMoved(Vector2 _) { }
            bool IInputListener.OnLeftMousePressed(Vector2 _)  => true;
            bool IInputListener.OnRightMousePressed(Vector2 _) => false;
            void IInputListener.OnLeftMouseUp(Vector2 _)  { }
            void IInputListener.OnRightMouseUp(Vector2 _) { }
            bool IInputListener.OnMouseScrolled(int _)    => false;
        }
    }
}

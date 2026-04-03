using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// UI component for the Mercenaries tab showing hired mercenary info and job skills.
    /// </summary>
    public class MercenariesTab
    {
        private Table _mainContainer;
        private Stage _stage;
        private Skin _skin;
        private TextService _textService;

        // Per-mercenary display containers (up to 2)
        private readonly Table[] _mercRows = new Table[2];

        // Per-mercenary labels
        private readonly Label[] _nameLabels = new Label[2];
        private readonly Label[] _levelLabels = new Label[2];
        private readonly Label[] _jobLabels = new Label[2];
        private readonly Label[] _statsLabels = new Label[2];
        private readonly Table[] _skillGrids = new Table[2];
        private readonly Label[] _noMercLabels = new Label[2];

        // Per-mercenary sprite preview containers
        private readonly Table[] _previewContainers = new Table[2];

        // Skill buttons for tooltip support
        private readonly List<MercSkillButton> _skillButtons = new List<MercSkillButton>(32);

        // Tooltip for skills
        private SkillTooltip _skillTooltip;

        /// <summary>Creates and returns the main container for this tab.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _stage = stage;
            _skin = skin;
            _textService = Core.Services.GetService<TextService>();
            _mainContainer = new Table();
            _mainContainer.SetFillParent(false);

            // Create skill tooltip
            var dummyTarget = new Element();
            dummyTarget.SetSize(0, 0);
            _skillTooltip = new SkillTooltip(dummyTarget, skin);

            var scrollContainer = new Table();

            for (int m = 0; m < 2; m++)
            {
                _mercRows[m] = new Table();
                BuildMercenaryRow(m, skin);
                scrollContainer.Add(_mercRows[m]).Expand().Fill().Pad(5f).Top();
                scrollContainer.Row();

                // Separator between mercenaries
                if (m == 0)
                {
                    var separator = new Table();
                    separator.SetBackground(new PrimitiveDrawable(new Color(100, 100, 100, 150)));
                    scrollContainer.Add(separator).Height(1f).Expand().Fill().SetPadTop(5f).SetPadBottom(5f);
                    scrollContainer.Row();
                }
            }

            var scrollPane = new ScrollPane(scrollContainer, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            _mainContainer.Add(scrollPane).Expand().Fill().Pad(10f);

            return _mainContainer;
        }

        /// <summary>Builds the layout for a single mercenary row.</summary>
        private void BuildMercenaryRow(int index, Skin skin)
        {
            var row = _mercRows[index];
            row.Clear();

            // No-merc placeholder label (shown when no mercenary is assigned)
            string noMercText = index == 0 ? _textService.DisplayText(DialogueType.UI, TextKey.MercenaryNoMercenariesHired) : "";
            _noMercLabels[index] = new Label(noMercText, skin, "ph-default");
            _noMercLabels[index].SetColor(Color.Gray);

            // Labels
            _nameLabels[index] = new Label("", skin, "ph-default");
            _levelLabels[index] = new Label("", skin, "ph-default");
            _jobLabels[index] = new Label("", skin, "ph-default");
            _statsLabels[index] = new Label("", skin, "ph-default");

            // Skill grid
            _skillGrids[index] = new Table();
            _skillGrids[index].Defaults().Pad(2f);

            // Sprite preview container
            _previewContainers[index] = new Table();

            // Info section: left column (labels) + middle column (sprite preview)
            var infoSection = new Table();

            // Left column: Name, Level, Job, Stats (all left-aligned, similar to Hero Info tab)
            var leftCol = new Table();
            leftCol.Add(_nameLabels[index]).Left();
            leftCol.Row();
            leftCol.Add(_levelLabels[index]).Left();
            leftCol.Row();
            leftCol.Add(_jobLabels[index]).Left();
            leftCol.Row();
            leftCol.Add(_statsLabels[index]).Left();

            infoSection.Add(leftCol).Left().Expand().Pad(5f);
            infoSection.Add(_previewContainers[index]).Center().Pad(5f).SetPadRight(64f);

            row.Add(infoSection).Expand().Fill();
            row.Row();

            // Row: "Job Skills" label
            var skillsSectionLabel = new Label(_textService.DisplayText(DialogueType.UI, TextKey.LabelJobSkills), skin, "ph-default");
            row.Add(skillsSectionLabel).Left().SetPadTop(4f);
            row.Row();

            // Row: Skill icon grid
            row.Add(_skillGrids[index]).Left().SetPadTop(2f);
            row.Row();

            // The no-merc label is shown/hidden via SetVisible on the row
        }

        /// <summary>Refreshes the tab with current mercenary data and appearance.</summary>
        public void UpdateWithMercenaries(List<Mercenary> hiredMercenaries, List<MercenaryAppearance> appearances)
        {
            _skillButtons.Clear();

            for (int m = 0; m < 2; m++)
            {
                _skillGrids[m].Clear();
                _previewContainers[m].Clear();

                Mercenary merc = null;
                if (hiredMercenaries != null && m < hiredMercenaries.Count)
                    merc = hiredMercenaries[m];

                if (merc == null)
                {
                    // Hide info, show placeholder for first row only
                    _nameLabels[m].SetText("");
                    _levelLabels[m].SetText("");
                    _jobLabels[m].SetText("");
                    _statsLabels[m].SetText("");
                    _mercRows[m].SetVisible(m == 0);
                    if (m == 0)
                    {
                        _skillGrids[m].Add(_noMercLabels[m]).Center();
                    }
                    continue;
                }

                _mercRows[m].SetVisible(true);

                // Update info labels
                _nameLabels[m].SetText(string.Format(_textService.DisplayText(DialogueType.UI, TextKey.MercenaryNameLabel), merc.Name));
                _levelLabels[m].SetText(string.Format(_textService.DisplayText(DialogueType.UI, TextKey.HeroLevelLabel), merc.Level));
                _jobLabels[m].SetText(string.Format(_textService.DisplayText(DialogueType.UI, TextKey.HeroJobLabel), merc.Job.Name));

                var stats = merc.GetTotalStats();
                _statsLabels[m].SetText(string.Format(_textService.DisplayText(DialogueType.UI, TextKey.HeroStatsLabel), stats.Strength, stats.Agility, stats.Vitality, stats.Magic));

                // Update sprite preview
                if (appearances != null && m < appearances.Count)
                {
                    var appearance = appearances[m];
                    try
                    {
                        var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                        var drawable = new HeroPreviewDrawable(actorsAtlas, appearance.SkinColor, appearance.HairColor, appearance.ShirtColor, appearance.HairstyleIndex);
                        var previewImage = new Image(drawable, Scaling.Fit);
                        _previewContainers[m].Add(previewImage).Size(32f, 46f);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.Warn("[MercenariesTab] Failed to load mercenary preview: " + ex.Message);
                    }
                }

                // Populate job skill icons
                PopulateSkillGrid(m, merc);
            }
        }

        /// <summary>Populates the skill grid for a mercenary.</summary>
        private void PopulateSkillGrid(int index, Mercenary merc)
        {
            var grid = _skillGrids[index];
            var jobSkills = merc.Job.Skills;

            if (jobSkills.Count == 0)
            {
                var noSkillsLabel = new Label(_textService.DisplayText(DialogueType.UI, TextKey.MercenaryNoJobSkills), _skin, "ph-default");
                noSkillsLabel.SetColor(Color.Gray);
                grid.Add(noSkillsLabel).Center();
                return;
            }

            const int columns = 4;
            int col = 0;

            for (int i = 0; i < jobSkills.Count; i++)
            {
                var skill = jobSkills[i];
                bool isLearned = merc.LearnedSkills.ContainsKey(skill.Id);

                var btn = new MercSkillButton(skill, isLearned);
                btn.OnHover += OnSkillHover;
                btn.OnUnhover += OnSkillUnhover;

                _skillButtons.Add(btn);
                grid.Add(btn).Size(32f, 32f);

                col++;
                if (col >= columns)
                {
                    col = 0;
                    grid.Row();
                }
            }
        }

        /// <summary>Handles skill hover to show tooltip.</summary>
        private void OnSkillHover(ISkill skill, bool isLearned)
        {
            if (_stage == null) return;

            // Show a simplified tooltip (no JP cost or learned status for mercenaries)
            _skillTooltip.ShowSkill(skill, isLearned, null, false, 0, 0, showCostAndStatus: false);
            if (_skillTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_skillTooltip.GetContainer());
            }

            var mousePos = _stage.GetMousePosition();
            _skillTooltip.PositionWithinBounds(mousePos, _stage);
            _skillTooltip.GetContainer().ToFront();
        }

        /// <summary>Handles skill unhover to hide tooltip.</summary>
        private void OnSkillUnhover()
        {
            _skillTooltip.GetContainer().Remove();
        }

        /// <summary>Simplified skill button for mercenary job skills (display-only, no purchase).</summary>
        private class MercSkillButton : Element, IInputListener
        {
            private readonly ISkill _skill;
            private readonly bool _isLearned;
            private SpriteDrawable _iconDrawable;
            private SpriteDrawable _selectBoxDrawable;
            private bool _isHovered;

            public event System.Action<ISkill, bool> OnHover;
            public event System.Action OnUnhover;

            public MercSkillButton(ISkill skill, bool isLearned)
            {
                _skill = skill;
                _isLearned = isLearned;

                if (Core.Content != null)
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var iconSprite = skillsAtlas.GetSprite(skill.Id);

                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    if (iconSprite == null)
                        iconSprite = uiAtlas.GetSprite("SkillIcon1");

                    _iconDrawable = new SpriteDrawable(iconSprite);

                    if (!isLearned)
                        _iconDrawable.TintColor = new Color(128, 128, 128, 200);

                    var selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                    _selectBoxDrawable = new SpriteDrawable(selectBoxSprite);
                }

                SetSize(24f, 24f);
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
                OnHover?.Invoke(_skill, _isLearned);
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

    /// <summary>Lightweight struct holding mercenary appearance data for UI display.</summary>
    public struct MercenaryAppearance
    {
        public Color SkinColor;
        public Color HairColor;
        public int HairstyleIndex;
        public Color ShirtColor;
    }
}

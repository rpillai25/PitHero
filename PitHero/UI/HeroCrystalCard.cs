using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;

namespace PitHero.UI
{
    /// <summary>UI window that displays detailed information about a hero crystal.</summary>
    public class HeroCrystalCard : Window
    {
        private const float CARD_WIDTH = 220f;
        private const float CARD_PADDING = 5f;
        private const int SKILL_COLUMNS = 4;
        private const float SKILL_BUTTON_SIZE = 32f;

        private HeroCrystal _crystal;
        private Table _contentTable;
        private TextService _textService;
        private new Stage _stage;

        public HeroCrystalCard(Skin skin, Stage stage) : base("", skin)
        {
            _stage = stage;
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);

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

        /// <summary>Hides the crystal card.</summary>
        public void Hide()
        {
            SetVisible(false);
            _crystal = null;
        }

        /// <summary>Positions the card near the given stage coordinates, clamped to screen.</summary>
        public void PositionNear(Vector2 stagePos)
        {
            SetPosition(stagePos.X + 10, stagePos.Y + 10);
            
            if (GetX() + GetWidth() > _stage.GetWidth())
                SetX(_stage.GetWidth() - GetWidth());
            if (GetY() + GetHeight() > _stage.GetHeight())
                SetY(_stage.GetHeight() - GetHeight());
        }

        private void RebuildContent()
        {
            _contentTable.Clear();

            if (_crystal == null) return;

            // Title: Job name or "Combo"
            var titleText = _crystal.IsCombo ? GetText(TextType.UI, UITextKey.CrystalTooltipCombo) : _crystal.Job.Name;
            var titleStyle = new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = _crystal.Color };
            var titleLabel = new Label(titleText, titleStyle);
            _contentTable.Add(titleLabel).Left().Pad(0, 0, 2, 0);
            _contentTable.Row();

            // Level
            var levelText = string.Format(GetText(TextType.UI, UITextKey.CrystalCardLevelLabel), _crystal.Level);
            _contentTable.Add(new Label(levelText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White })).Left().Pad(0, 0, 2, 0);
            _contentTable.Row();

            // Job Level
            var jobLevelText = string.Format(GetText(TextType.UI, UITextKey.CrystalCardJobLevelLabel), _crystal.JobLevel);
            _contentTable.Add(new Label(jobLevelText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White })).Left().Pad(0, 0, 2, 0);
            _contentTable.Row();

            // Stats
            var statsText = string.Format(GetText(TextType.UI, UITextKey.CrystalCardStatsLabel), 
                _crystal.BaseStats.Strength, _crystal.BaseStats.Agility, _crystal.BaseStats.Vitality, _crystal.BaseStats.Magic);
            _contentTable.Add(new Label(statsText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White })).Left().Pad(0, 0, 5, 0);
            _contentTable.Row();

            // Job Skills
            _contentTable.Add(new Label(GetText(TextType.UI, UITextKey.CrystalCardJobSkillsLabel), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White })).Left().Pad(0, 0, 2, 0);
            _contentTable.Row();

            var skillsGrid = new Table();
            var skills = _crystal.Job.Skills;
            int col = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var skillLabel = new Label(skill.Name.Substring(0, System.Math.Min(3, skill.Name.Length)), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                skillsGrid.Add(skillLabel).Size(SKILL_BUTTON_SIZE);
                col++;
                if (col >= SKILL_COLUMNS) { skillsGrid.Row(); col = 0; }
            }
            _contentTable.Add(skillsGrid).Left();
            _contentTable.Row();

            Pack();
        }
    }
}

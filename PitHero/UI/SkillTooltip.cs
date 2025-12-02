using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;

namespace PitHero.UI
{
    /// <summary>Tooltip for displaying skill information</summary>
    public class SkillTooltip
    {
        private Window _container;
        private Table _contentTable;
        
        public SkillTooltip(Element target, Skin skin)
        {
            _container = new Window("", skin);
            _container.SetMovable(false);
            _container.SetResizable(false);
            _container.SetKeepWithinStage(false);
            _container.SetColor(GameConfig.TransparentMenu);
            
            _contentTable = new Table();
            _container.Add(_contentTable).Expand().Fill().Pad(5f);
            
            _container.SetVisible(false);
        }
        
        public void ShowSkill(ISkill skill, bool isLearned, Hero hero, bool isSynergySkill = false, int synergyCurrentPoints = 0, int synergyRequiredPoints = 0)
        {
            _contentTable.Clear();
            
            // Skill name
            var nameColor = isLearned ? Color.Green : Color.White;
            var nameLabel = new Label(skill.Name, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = nameColor });
            _contentTable.Add(nameLabel).Left();
            _contentTable.Row();
            
            // Skill type
            var typeText = $"{skill.Kind}";
            if (skill.Kind == SkillKind.Active)
            {
                typeText += $" (MP: {skill.MPCost})";
            }
            var typeLabel = new Label(typeText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.LightGray });
            _contentTable.Add(typeLabel).Left();
            _contentTable.Row();
            
            // Description
            if (!string.IsNullOrEmpty(skill.Description))
            {
                var descLabel = new Label(skill.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                descLabel.SetWrap(true);
                _contentTable.Add(descLabel).Width(200f).Left().SetPadTop(5f).SetPadBottom(5f);
                _contentTable.Row();
            }
            
            // Synergy skill shows progress instead of JP cost
            if (isSynergySkill)
            {
                if (isLearned)
                {
                    // Already learned synergy skill
                    var learnedLabel = new Label("(Learned)", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Green });
                    _contentTable.Add(learnedLabel).Left();
                    _contentTable.Row();
                }
                else
                {
                    // Show synergy progress
                    var progressText = $"Progress: {synergyCurrentPoints} / {synergyRequiredPoints} SP";
                    var progressColor = synergyCurrentPoints >= synergyRequiredPoints ? Color.Green : Color.Cyan;
                    var progressLabel = new Label(progressText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = progressColor });
                    _contentTable.Add(progressLabel).Left();
                    _contentTable.Row();
                }
            }
            else
            {
                // Regular JP cost for job skills
                var costText = $"Cost: {skill.JPCost} JP";
                var costLabel = new Label(costText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Yellow });
                _contentTable.Add(costLabel).Left();
                _contentTable.Row();
                
                // Status
                if (isLearned)
                {
                    var learnedLabel = new Label("(Learned)", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Green });
                    _contentTable.Add(learnedLabel).Left();
                    _contentTable.Row();
                }
                else if (hero != null)
                {
                    if (hero.GetCurrentJP() < skill.JPCost)
                    {
                        var insufficientJPLabel = new Label("(Insufficient JP)", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Red });
                        _contentTable.Add(insufficientJPLabel).Left();
                        _contentTable.Row();
                    }
                }
            }
            
            _container.SetVisible(true);
            _container.Pack();
        }
        
        public Window GetContainer() => _container;
    }
}

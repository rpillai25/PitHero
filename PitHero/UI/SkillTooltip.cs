using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Synergies;

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
        
        public void ShowSynergyEffect(SynergyPattern pattern, int instanceCount, float multiplier)
        {
            _contentTable.Clear();
            
            // Pattern name
            var nameLabel = new Label(SanitizeText(pattern.Name), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Cyan });
            _contentTable.Add(nameLabel).Left();
            _contentTable.Row();
            
            // Instance count and multiplier
            var instanceText = $"Active: {instanceCount}x (Multiplier: {multiplier:F2}x)";
            var instanceLabel = new Label(SanitizeText(instanceText), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.LightGreen });
            _contentTable.Add(instanceLabel).Left();
            _contentTable.Row();
            
            // Description
            if (!string.IsNullOrEmpty(pattern.Description))
            {
                var descLabel = new Label(SanitizeText(pattern.Description), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                descLabel.SetWrap(true);
                _contentTable.Add(descLabel).Width(200f).Left().SetPadTop(5f).SetPadBottom(5f);
                _contentTable.Row();
            }
            
            // Show effects
            var effects = pattern.Effects;
            if (effects.Count > 0)
            {
                var effectsLabel = new Label("Effects:", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Yellow });
                _contentTable.Add(effectsLabel).Left().SetPadTop(5f);
                _contentTable.Row();
                
                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    // Replace bullet with dash for compatibility
                    var effectText = SanitizeText(effect.Description);
                    if (!effectText.StartsWith("-") && !effectText.StartsWith("*"))
                    {
                        effectText = "- " + effectText;
                    }
                    var effectLabel = new Label(effectText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.LightGray });
                    effectLabel.SetWrap(true);
                    _contentTable.Add(effectLabel).Width(200f).Left();
                    _contentTable.Row();
                }
            }
            
            // Note about temporary nature
            var noteLabel = new Label("(Active only while pattern is formed)", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Orange });
            noteLabel.SetFontScale(0.8f);
            _contentTable.Add(noteLabel).Left().SetPadTop(5f);
            _contentTable.Row();
            
            _container.SetVisible(true);
            _container.Pack();
        }
        
        /// <summary>Sanitizes text by removing or replacing unsupported characters.</summary>
        private string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Replace common unsupported characters
            text = text.Replace('\u2022', '-');  // Bullet point •
            text = text.Replace('\u2013', '-');  // En dash –
            text = text.Replace('\u2014', '-');  // Em dash —
            text = text.Replace('\u2018', '\''); // Left single quote '
            text = text.Replace('\u2019', '\''); // Right single quote '
            text = text.Replace('\u201C', '"');  // Left double quote "
            text = text.Replace('\u201D', '"');  // Right double quote "
            text = text.Replace("\u2026", "..."); // Ellipsis …
            
            // Filter out any remaining non-ASCII characters that might not be in the font
            var result = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                // Keep alphanumeric, common punctuation, and whitespace
                if (char.IsLetterOrDigit(c) || 
                    char.IsWhiteSpace(c) || 
                    ".,!?()-+:;/%*#@[]{}|<>=_&$\"'".Contains(c))
                {
                    result.Append(c);
                }
            }
            
            return result.ToString();
        }
        
        public Window GetContainer() => _container;
    }
}

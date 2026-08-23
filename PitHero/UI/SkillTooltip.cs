using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Synergies;
using System.ComponentModel;

namespace PitHero.UI
{
    /// <summary>Tooltip for displaying skill information</summary>
    public class SkillTooltip
    {
        private Window _container;
        private Table _contentTable;
        private TextService _textService;

        // Content cache. A hovered icon re-enters constantly (border jitter, tab refreshes), and the
        // card is rebuilt from scratch on every ShowSkill, so key the built content on everything it
        // renders and skip the rebuild when nothing changed. The two card kinds clear each other's
        // key so switching between a skill card and a synergy card always rebuilds.
        private ISkill _cachedSkill;
        private SynergyPattern _cachedPattern;
        private bool _cachedIsLearned;
        private Hero _cachedHero;
        private int _cachedHeroJP;
        private bool _cachedIsSynergySkill;
        private int _cachedCurrentPoints;
        private int _cachedRequiredPoints;
        private bool _cachedShowCostAndStatus;
        private string _cachedOwnerName;
        private string _cachedMultiplierLine;
        private bool _cachedShowGrantsSkillNote;

        // Frame stamp of the most recent Show* call, -1 when the card is down. Nez fires OnMouseEnter
        // on the element being entered BEFORE OnMouseExit on the one being left (Stage.UpdateInputMoved),
        // so moving straight from one icon to its neighbour raises the card for the new entry and then
        // immediately tears it back down — the card only ever survived when the cursor arrived from dead
        // space. HideOnUnhover consults this stamp so the exit cannot cancel a sibling's fresh show.
        private long _lastShownFrame = -1;

        // Brown font color matching PitHeroSkin default
        private static readonly Color BrownFontColor = new Color(71, 36, 7);
        private static readonly Color Detail1FontColor = new Color(37, 80, 112);
        private static readonly Color SynergyGreen = new Color(11, 117, 11);
        private static readonly Color SynergyCyan = new Color(0, 156, 156);
        private static readonly Color SynergyOrange = new Color(201, 132, 4);
        private static readonly Color SynergyYellow = new Color(135, 135, 20);

        public SkillTooltip(Element target, Skin skin)
        {
            _container = new Window("", skin);
            _container.SetMovable(false);
            _container.SetResizable(false);
            _container.SetKeepWithinStage(false);
            _container.SetColor(GameConfig.TransparentMenu);

            // The card sits directly under the cursor and follows it, so a touchable window would win
            // the hit test over the icons it overlaps: the neighboring button never gets MouseEnter
            // and hover feels laggy when moving between adjacent skills. Tooltips never take input.
            _container.SetTouchable(Touchable.Disabled);

            _contentTable = new Table();
            _container.Add(_contentTable).Expand().Fill().Pad(5f);

            _container.SetVisible(false);
        }

        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }

        public void ShowSkill(ISkill skill, bool isLearned, Hero hero, bool isSynergySkill = false, int synergyCurrentPoints = 0, int synergyRequiredPoints = 0, bool showCostAndStatus = true, string ownerName = null)
        {
            // Hero JP is part of the key: it drives the "insufficient JP" line.
            var heroJP = hero != null ? hero.GetCurrentJP() : 0;
            if (ReferenceEquals(skill, _cachedSkill) && _cachedPattern == null
                && isLearned == _cachedIsLearned && ReferenceEquals(hero, _cachedHero) && heroJP == _cachedHeroJP
                && isSynergySkill == _cachedIsSynergySkill
                && synergyCurrentPoints == _cachedCurrentPoints && synergyRequiredPoints == _cachedRequiredPoints
                && showCostAndStatus == _cachedShowCostAndStatus && ownerName == _cachedOwnerName)
            {
                MarkShown();
                return;
            }

            _contentTable.Clear();

            // Skill name, optionally suffixed with the owning character's name, e.g. "Fire (Fynn Swift)"
            var nameText = ownerName != null
                ? string.Format(GetText(TextType.UI, UITextKey.SkillOwnerFormat), skill.Name, ownerName)
                : skill.Name;
            var nameColor = isLearned ? Color.Green : BrownFontColor;
            var nameLabel = new Label(nameText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = nameColor });
            _contentTable.Add(nameLabel).Left();
            _contentTable.Row();

            // Skill type
            var typeText = $"{skill.Kind}";
            if (skill.Kind == SkillKind.Active)
            {
                typeText += $" (MP: {skill.MPCost})";
            }
            var typeLabel = new Label(typeText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Detail1FontColor });
            _contentTable.Add(typeLabel).Left();
            _contentTable.Row();

            // Description
            if (!string.IsNullOrEmpty(skill.Description))
            {
                var descLabel = new Label(skill.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
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
                    var learnedLabel = new Label(GetText(TextType.UI, UITextKey.SkillLearned), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyGreen });
                    _contentTable.Add(learnedLabel).Left();
                    _contentTable.Row();
                }
                else
                {
                    // Show synergy progress
                    var progressText = string.Format(GetText(TextType.UI, UITextKey.SkillProgress), synergyCurrentPoints, synergyRequiredPoints);
                    var progressColor = synergyCurrentPoints >= synergyRequiredPoints ? Color.Green : Color.Cyan;
                    var progressLabel = new Label(progressText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = progressColor });
                    _contentTable.Add(progressLabel).Left();
                    _contentTable.Row();
                }
            }
            else if (showCostAndStatus)
            {
                // Regular JP cost for job skills
                var costText = string.Format(GetText(TextType.UI, UITextKey.SkillJpCost), skill.JPCost);
                var costLabel = new Label(costText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Detail1FontColor });
                _contentTable.Add(costLabel).Left();
                _contentTable.Row();

                // Status
                if (isLearned)
                {
                    var learnedLabel = new Label(GetText(TextType.UI, UITextKey.SkillLearned), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Green });
                    _contentTable.Add(learnedLabel).Left();
                    _contentTable.Row();
                }
                else if (hero != null)
                {
                    if (hero.GetCurrentJP() < skill.JPCost)
                    {
                        var insufficientJPLabel = new Label(GetText(TextType.UI, UITextKey.SkillInsufficientJp), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Red });
                        _contentTable.Add(insufficientJPLabel).Left();
                        _contentTable.Row();
                    }
                }
            }

            _cachedSkill = skill;
            _cachedPattern = null;
            _cachedIsLearned = isLearned;
            _cachedHero = hero;
            _cachedHeroJP = heroJP;
            _cachedIsSynergySkill = isSynergySkill;
            _cachedCurrentPoints = synergyCurrentPoints;
            _cachedRequiredPoints = synergyRequiredPoints;
            _cachedShowCostAndStatus = showCostAndStatus;
            _cachedOwnerName = ownerName;

            MarkShown();
            _container.Pack();
        }

        public void ShowSynergyEffect(SynergyPattern pattern, int instanceCount, float multiplier)
        {
            // Instance count and multiplier, truncated (not rounded) to 2 decimals so the
            // display never overstates the bonus (e.g. 1.9375 shows as 1.93, not 1.94)
            var truncatedMultiplier = System.MathF.Truncate(multiplier * 100f) / 100f;
            var instanceText = string.Format(GetText(TextType.UI, UITextKey.SkillActiveMultiplier), instanceCount, truncatedMultiplier);
            BuildSynergyCard(pattern, instanceText);
        }

        /// <summary>
        /// Shows the synergy card for a pattern without any active-instance multiplier info —
        /// used by the stencil library, where the pattern isn't tied to a hero's active synergies.
        /// </summary>
        public void ShowSynergyPattern(SynergyPattern pattern)
        {
            BuildSynergyCard(pattern, null, showGrantsSkillNote: pattern.UnlockedSkill != null);
        }

        private void BuildSynergyCard(SynergyPattern pattern, string multiplierLine, bool showGrantsSkillNote = false)
        {
            if (ReferenceEquals(pattern, _cachedPattern) && multiplierLine == _cachedMultiplierLine
                && showGrantsSkillNote == _cachedShowGrantsSkillNote)
            {
                MarkShown();
                return;
            }

            _contentTable.Clear();

            // Pattern name
            var nameLabel = new Label(SanitizeText(pattern.Name), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyCyan });
            _contentTable.Add(nameLabel).Left();
            _contentTable.Row();

            if (multiplierLine != null)
            {
                var instanceLabel = new Label(SanitizeText(multiplierLine), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyGreen });
                _contentTable.Add(instanceLabel).Left();
                _contentTable.Row();
            }

            // Description
            if (!string.IsNullOrEmpty(pattern.Description))
            {
                var descLabel = new Label(SanitizeText(pattern.Description), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
                descLabel.SetWrap(true);
                _contentTable.Add(descLabel).Width(200f).Left().SetPadTop(5f).SetPadBottom(5f);
                _contentTable.Row();
            }

            // Show effects
            var effects = pattern.Effects;
            if (effects.Count > 0)
            {
                var effectsLabel = new Label(GetText(TextType.UI, UITextKey.SkillEffectsLabel), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyYellow });
                _contentTable.Add(effectsLabel).Left().SetPadTop(5f);
                _contentTable.Row();

                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    // Replace bullet with dash for compatibility
                    var effectText = SanitizeText(effect.Description);

                    var effectLabel = new Label(effectText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Detail1FontColor });
                    effectLabel.SetWrap(true);
                    _contentTable.Add(effectLabel).Width(200f).Left();
                    _contentTable.Row();
                }
            }

            // Note about temporary nature
            var noteLabel = new Label(GetText(TextType.UI, UITextKey.SkillActivePatternNote), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyOrange });
            _contentTable.Add(noteLabel).Left().SetPadTop(5f);
            _contentTable.Row();

            if (showGrantsSkillNote)
            {
                var grantsLabel = new Label(GetText(TextType.UI, UITextKey.SkillGrantsSynergySkill), new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = SynergyGreen });
                _contentTable.Add(grantsLabel).Left().SetPadTop(5f);
                _contentTable.Row();
            }

            _cachedSkill = null;
            _cachedPattern = pattern;
            _cachedMultiplierLine = multiplierLine;
            _cachedShowGrantsSkillNote = showGrantsSkillNote;

            MarkShown();
            _container.Pack();
        }

        /// <summary>Marks the card visible and records the frame, so a sibling icon's mouse-exit
        /// later in the same frame knows not to take it back down.</summary>
        private void MarkShown()
        {
            _container.SetVisible(true);
            _lastShownFrame = Time.FrameCount;
        }

        /// <summary>Hides the card in response to a mouse-exit. No-ops when another icon already
        /// claimed the card this frame — see the _lastShownFrame note above.</summary>
        public void HideOnUnhover()
        {
            if (_lastShownFrame == Time.FrameCount)
                return;
            Hide();
        }

        /// <summary>Hides the card unconditionally: teardown, drag start, panel close.</summary>
        public void Hide()
        {
            _lastShownFrame = -1;
            _container.Remove();
        }

        /// <summary>Sanitizes text by removing or replacing unsupported characters.</summary>
        private string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Replace common unsupported characters
            text = text.Replace('\u2022', '-');  // Bullet point �
            text = text.Replace('\u2013', '-');  // En dash �
            text = text.Replace('\u2014', '-');  // Em dash �
            text = text.Replace('\u2018', '\''); // Left single quote '
            text = text.Replace('\u2019', '\''); // Right single quote '
            text = text.Replace('\u201C', '"');  // Left double quote "
            text = text.Replace('\u201D', '"');  // Right double quote "
            text = text.Replace("\u2026", "..."); // Ellipsis �

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

        /// <summary>
        /// Positions the tooltip near the mouse cursor while keeping it within stage bounds.
        /// </summary>
        /// <param name="mousePos">Current mouse position</param>
        /// <param name="stage">The UI stage</param>
        /// <param name="offsetX">Default X offset from cursor (can be negative)</param>
        /// <param name="offsetY">Default Y offset from cursor (can be negative)</param>
        public void PositionWithinBounds(Vector2 mousePos, Stage stage, float offsetX = 10f, float offsetY = 10f)
        {
            if (stage == null || _container == null)
                return;

            // Make sure container is packed to get accurate size
            _container.Pack();

            float tooltipWidth = _container.GetWidth();
            float tooltipHeight = _container.GetHeight();
            float stageWidth = stage.GetWidth();
            float stageHeight = stage.GetHeight();

            // Start with default position (cursor + offset)
            float x = mousePos.X + offsetX;
            float y = mousePos.Y + offsetY;

            // Check right edge
            if (x + tooltipWidth > stageWidth)
            {
                // Position to the left of cursor instead
                x = mousePos.X - tooltipWidth - 10f;

                // If still off screen, clamp to right edge
                if (x < 0)
                {
                    x = stageWidth - tooltipWidth - 5f;
                }
            }

            // Check left edge
            if (x < 0)
            {
                x = 5f; // Small margin from left edge
            }

            // Check bottom edge
            if (y + tooltipHeight > stageHeight)
            {
                // Position above cursor instead
                y = mousePos.Y - tooltipHeight - 10f;

                // If still off screen, clamp to bottom edge
                if (y < 0)
                {
                    y = stageHeight - tooltipHeight - 5f;
                }
            }

            // Check top edge
            if (y < 0)
            {
                y = 5f; // Small margin from top edge
            }

            _container.SetPosition(x, y);
        }

        public Window GetContainer() => _container;
    }
}

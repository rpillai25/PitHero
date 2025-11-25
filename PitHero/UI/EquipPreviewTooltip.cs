using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using Nez.BitmapFonts;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>Tooltip that displays stat changes when comparing gear with currently equipped gear.</summary>
    public class EquipPreviewTooltip : Tooltip
    {
        private const float CARD_PADDING = 5f;
        private const float LINE_SPACING = 2f;
        
        private IGear _newGear;
        private IGear _equippedGear;
        private Table _contentTable;
        private Container _wrapper;

        public EquipPreviewTooltip(Element targetElement, Skin skin) : base(null, targetElement)
        {
            // Create content table
            _contentTable = new Table();
            
            // Create wrapper with background and padding
            _wrapper = new Container(_contentTable);
            _wrapper.SetBackground(skin.Get<WindowStyle>().Background);
            
            // Add padding around content
            var wrapperTable = new Table();
            wrapperTable.Add(_wrapper).Pad(CARD_PADDING);
            
            // Set the wrapper as the tooltip content
            _container.SetElement(wrapperTable);
            _container.SetTouchable(Touchable.Disabled);
        }

        /// <summary>Shows the tooltip comparing new gear with currently equipped gear.</summary>
        public void ShowComparison(IGear newGear, IGear equippedGear)
        {
            _newGear = newGear;
            _equippedGear = equippedGear;
            
            if (_newGear == null || _equippedGear == null)
            {
                return;
            }

            RebuildContent();
            _container.Pack();
        }

        /// <summary>Rebuilds the tooltip content for the stat comparison.</summary>
        private void RebuildContent()
        {
            _contentTable.Clear();

            if (_newGear == null || _equippedGear == null) return;

            var font = Graphics.Instance.BitmapFont;
            float maxLineWidth = 0f;
            bool hasAnyChanges = false;

            // Title
            var titleText = "Changes";
            var titleLabel = new Label(titleText, new LabelStyle { Font = font, FontColor = Color.White });
            _contentTable.Add(titleLabel).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();
            maxLineWidth = Max(maxLineWidth, Measure(font, titleText));

            // Compare StatBlock bonuses
            var newStats = _newGear.StatBonus;
            var equippedStats = _equippedGear.StatBonus;
            
            // Strength
            int strengthDiff = newStats.Strength - equippedStats.Strength;
            if (strengthDiff != 0)
            {
                hasAnyChanges = true;
                var text = strengthDiff > 0 ? $"+{strengthDiff} Strength" : $"{strengthDiff} Strength";
                var color = strengthDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // Agility
            int agilityDiff = newStats.Agility - equippedStats.Agility;
            if (agilityDiff != 0)
            {
                hasAnyChanges = true;
                var text = agilityDiff > 0 ? $"+{agilityDiff} Agility" : $"{agilityDiff} Agility";
                var color = agilityDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // Vitality
            int vitalityDiff = newStats.Vitality - equippedStats.Vitality;
            if (vitalityDiff != 0)
            {
                hasAnyChanges = true;
                var text = vitalityDiff > 0 ? $"+{vitalityDiff} Vitality" : $"{vitalityDiff} Vitality";
                var color = vitalityDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // Magic
            int magicDiff = newStats.Magic - equippedStats.Magic;
            if (magicDiff != 0)
            {
                hasAnyChanges = true;
                var text = magicDiff > 0 ? $"+{magicDiff} Magic" : $"{magicDiff} Magic";
                var color = magicDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // Compare flat bonuses
            // Attack
            int attackDiff = _newGear.AttackBonus - _equippedGear.AttackBonus;
            if (attackDiff != 0)
            {
                hasAnyChanges = true;
                var text = attackDiff > 0 ? $"+{attackDiff} Attack" : $"{attackDiff} Attack";
                var color = attackDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // Defense
            int defenseDiff = _newGear.DefenseBonus - _equippedGear.DefenseBonus;
            if (defenseDiff != 0)
            {
                hasAnyChanges = true;
                var text = defenseDiff > 0 ? $"+{defenseDiff} Defense" : $"{defenseDiff} Defense";
                var color = defenseDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // HP
            int hpDiff = _newGear.HPBonus - _equippedGear.HPBonus;
            if (hpDiff != 0)
            {
                hasAnyChanges = true;
                var text = hpDiff > 0 ? $"+{hpDiff} HP" : $"{hpDiff} HP";
                var color = hpDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // AP (MP)
            int apDiff = _newGear.MPBonus - _equippedGear.MPBonus;
            if (apDiff != 0)
            {
                hasAnyChanges = true;
                var text = apDiff > 0 ? $"+{apDiff} MP" : $"{apDiff} MP";
                var color = apDiff > 0 ? Color.Green : Color.Red;
                var label = new Label(text, new LabelStyle { Font = font, FontColor = color });
                _contentTable.Add(label).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, text));
            }

            // If no changes, just clear the table
            if (!hasAnyChanges)
            {
                _contentTable.Clear();
            }

            // Ensure wrapper width is longest line plus padding on both sides
            var targetMinWidth = maxLineWidth + CARD_PADDING * 2f;
            _wrapper.SetMinSize(targetMinWidth, 0f);
        }

        /// <summary>Returns the larger of a and b.</summary>
        private static float Max(float a, float b) => a > b ? a : b;

        /// <summary>Measures a single-line text width with the provided font.</summary>
        private static float Measure(BitmapFont font, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return font.MeasureString(text).X;
        }
    }
}

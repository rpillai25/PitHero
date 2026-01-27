using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using Nez.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Synergies;
using System;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>Tooltip that displays item card information at cursor position.</summary>
    public class ItemCardTooltip : Tooltip
    {
        private const float CARD_PADDING = 5f;
        private const float LINE_SPACING = 2f;
        private const float SYNERGY_ICON_SIZE = 20f;
        private const float SYNERGY_ICON_SPACING = 4f;

        // Brown font color matching PitHeroSkin default
        private static readonly Color BrownFontColor = new Color(71, 36, 7);
        private static readonly Color SynergyCyan = new Color(0, 156, 156);
        private static readonly Color SynergyGreen = new Color(11, 117, 11);

        private IItem _item;
        private List<ActiveSynergy> _synergies;
        private Table _contentTable;
        private Container _wrapper;

        public ItemCardTooltip(Element targetElement, Skin skin) : base(null, targetElement)
        {
            // Create content table
            _contentTable = new Table();
            _container.SetColor(GameConfig.TransparentMenu);

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

        /// <summary>Shows the tooltip with the specified item.</summary>
        public void ShowItem(IItem item)
        {
            ShowItem(item, null);
        }

        /// <summary>Shows the tooltip with the specified item and synergy information.</summary>
        public void ShowItem(IItem item, List<ActiveSynergy> synergies)
        {
            _item = item;
            _synergies = synergies;
            if (_item == null)
            {
                return;
            }

            RebuildContent();
            _container.Pack();
        }

        /// <summary>Rebuilds the tooltip content for the current item and sizes width to longest line plus padding.</summary>
        private void RebuildContent()
        {
            _contentTable.Clear();

            if (_item == null) return;

            var font = Graphics.Instance.BitmapFont;
            float maxLineWidth = 0f;

            var rarityColor = ItemDisplayHelper.GetRarityColor(_item.Rarity);
            var typeString = ItemDisplayHelper.GetItemTypeString(_item.Kind);
            var rarityString = ItemDisplayHelper.GetRarityString(_item.Rarity);

            // Item Name (with rarity color)
            var nameLabel = new Label(_item.Name, new LabelStyle { Font = font, FontColor = rarityColor });
            _contentTable.Add(nameLabel).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();
            maxLineWidth = Max(maxLineWidth, Measure(font, _item.Name));

            // Rarity and Type (with rarity color)
            var rarityTypeText = $"{rarityString} {typeString}";
            var rarityTypeLabel = new Label(rarityTypeText, new LabelStyle { Font = font, FontColor = rarityColor });
            _contentTable.Add(rarityTypeLabel).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();
            maxLineWidth = Max(maxLineWidth, Measure(font, rarityTypeText));

            // Only add description if it won't be duplicated by generated effect lines
            bool skipDescription = false;
            if (_item is Consumable c)
            {
                if (c.HPRestoreAmount != 0 || c.MPRestoreAmount != 0)
                    skipDescription = true;
            }
            if (!skipDescription)
            {
                var descText = _item.Description;
                var descLabel = new Label(descText, new LabelStyle { Font = font, FontColor = BrownFontColor });
                // no wrapping so width reflects actual longest line
                descLabel.SetWrap(false);
                _contentTable.Add(descLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, descText));
            }

            // Add conditional properties and track max width
            maxLineWidth = Max(maxLineWidth, AddConditionalProperties(font));

            // Sell Price (always shown)
            var sellPrice = _item.GetSellPrice();
            var priceText = $"Sell Price: {sellPrice}G";
            var priceLabel = new Label(priceText, new LabelStyle { Font = font, FontColor = BrownFontColor });
            _contentTable.Add(priceLabel).Left();
            _contentTable.Row();
            maxLineWidth = Max(maxLineWidth, Measure(font, priceText));

            // Add synergy information if available
            if (_synergies != null && _synergies.Count > 0)
            {
                // Add spacing before synergy section
                var spacer = new Container();
                spacer.SetHeight(LINE_SPACING * 2);
                _contentTable.Add(spacer);
                _contentTable.Row();

                // Add "Synergies:" header
                var synergyHeaderText = "Synergies:";
                var synergyHeaderLabel = new Label(synergyHeaderText, new LabelStyle { Font = font, FontColor = SynergyGreen });
                _contentTable.Add(synergyHeaderLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
                maxLineWidth = Max(maxLineWidth, Measure(font, synergyHeaderText));

                // Add each synergy with icon
                float synergyLineWidth = AddSynergyEntries(font);
                maxLineWidth = Max(maxLineWidth, synergyLineWidth);
            }

            // Ensure wrapper width is longest line plus padding on both sides
            var targetMinWidth = maxLineWidth + CARD_PADDING * 2f;
            _wrapper.SetMinSize(targetMinWidth, 0f);
        }

        /// <summary>Adds conditional properties to the tooltip and returns the max line width added.</summary>
        private float AddConditionalProperties(BitmapFont font)
        {
            float max = 0f;
            // Check for HP/AP restore (Consumables)
            if (_item is Consumable consumable)
            {
                if (consumable.HPRestoreAmount > 0)
                {
                    var text = $"Restores {consumable.HPRestoreAmount} HP";
                    var hpLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                else if (consumable.HPRestoreAmount < 0)
                {
                    var text = "Fully restores HP";
                    var hpLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }

                if (consumable.MPRestoreAmount > 0)
                {
                    var text = $"Restores {consumable.MPRestoreAmount} MP";
                    var apLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                else if (consumable.MPRestoreAmount < 0)
                {
                    var text = "Fully restores MP";
                    var apLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
            }

            // Check for stat bonuses (Gear)
            if (_item is IGear gear)
            {
                // StatBlock bonuses
                var stats = gear.StatBonus;
                if (stats.Strength > 0)
                {
                    var text = $"+{stats.Strength} Strength";
                    var strLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(strLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (stats.Agility > 0)
                {
                    var text = $"+{stats.Agility} Agility";
                    var agiLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(agiLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (stats.Vitality > 0)
                {
                    var text = $"+{stats.Vitality} Vitality";
                    var vitLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(vitLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (stats.Magic > 0)
                {
                    var text = $"+{stats.Magic} Magic";
                    var magLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(magLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }

                // Flat bonuses
                if (gear.AttackBonus != 0)
                {
                    var text = $"+{gear.AttackBonus} Attack";
                    var atkLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(atkLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (gear.DefenseBonus != 0)
                {
                    var text = $"+{gear.DefenseBonus} Defense";
                    var defLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(defLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (gear.HPBonus != 0)
                {
                    var text = $"+{gear.HPBonus} HP";
                    var hpLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }
                if (gear.MPBonus != 0)
                {
                    var text = $"+{gear.MPBonus} MP";
                    var apLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                    max = Max(max, Measure(font, text));
                }

                // Elemental resistances
                foreach (var resistance in gear.ElementalProps.Resistances)
                {
                    if (resistance.Value != 0)
                    {
                        //If resistance value > 0 it's a resistance, else it's a weakness;
                        var type = resistance.Value > 0 ? "Resistance" : "Weakness";
                        var text = $"{Mathf.RoundToInt(Math.Abs(resistance.Value) * 100)}% {resistance.Key} {type}";
                        var resLabel = new Label(text, new LabelStyle { Font = font, FontColor = BrownFontColor });
                        _contentTable.Add(resLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                        _contentTable.Row();
                        max = Max(max, Measure(font, text));
                    }
                }
            }

            return max;
        }

        /// <summary>Adds synergy entries with icons and names, returns max line width.</summary>
        private float AddSynergyEntries(BitmapFont font)
        {
            if (_synergies == null || _synergies.Count == 0)
                return 0f;

            float maxWidth = 0f;

            // Load synergy icon atlas
            var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            for (int i = 0; i < _synergies.Count; i++)
            {
                var synergy = _synergies[i];
                var pattern = synergy.Pattern;

                // Create a horizontal container for icon + name
                var synergyRow = new Table();

                // Get synergy icon sprite
                string spriteName;
                if (pattern.UnlockedSkill != null)
                {
                    spriteName = pattern.UnlockedSkill.Id;
                }
                else
                {
                    spriteName = pattern.Id;
                }

                var iconSprite = skillsAtlas.GetSprite(spriteName);
                if (iconSprite == null)
                {
                    // Fallback to default icon
                    iconSprite = uiAtlas.GetSprite("SkillIcon1");
                }

                // Create icon image
                if (iconSprite != null)
                {
                    var iconDrawable = new SpriteDrawable(iconSprite);
                    var iconImage = new Image(iconDrawable);
                    iconImage.SetSize(SYNERGY_ICON_SIZE, SYNERGY_ICON_SIZE);
                    synergyRow.Add(iconImage).Pad(0, 0, 0, SYNERGY_ICON_SPACING);
                }

                // Create synergy name label
                var nameLabel = new Label(pattern.Name, new LabelStyle { Font = font, FontColor = SynergyCyan });
                synergyRow.Add(nameLabel);

                // Add the row to content table
                _contentTable.Add(synergyRow).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();

                // Calculate width (icon + spacing + text)
                float rowWidth = SYNERGY_ICON_SIZE + SYNERGY_ICON_SPACING + Measure(font, pattern.Name);
                maxWidth = Max(maxWidth, rowWidth);
            }

            return maxWidth;
        }

        /// <summary>Returns the larger of a and b.</summary>
        private static float Max(float a, float b) => a > b ? a : b;

        /// <summary>Measures a single-line text width with the provided font.</summary>
        private static float Measure(BitmapFont font, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return font.MeasureString(text).X;
        }

        /// <summary>Gets the current item being displayed.</summary>
        public IItem CurrentItem => _item;
    }
}

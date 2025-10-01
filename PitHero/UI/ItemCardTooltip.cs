using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.UI
{
    /// <summary>Tooltip that displays item card information at cursor position.</summary>
    public class ItemCardTooltip : Tooltip
    {
        private const float CARD_WIDTH = 200f;
        private const float CARD_PADDING = 5f;
        private const float LINE_SPACING = 2f;
        
        private IItem _item;
        private Table _contentTable;
        private Container _wrapper;

        public ItemCardTooltip(Element targetElement, Skin skin) : base(null, targetElement)
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

        /// <summary>Shows the tooltip with the specified item.</summary>
        public void ShowItem(IItem item)
        {
            _item = item;
            if (_item == null)
            {
                return;
            }

            RebuildContent();
            _container.Pack();
        }

        /// <summary>Rebuilds the tooltip content for the current item.</summary>
        private void RebuildContent()
        {
            _contentTable.Clear();

            if (_item == null) return;

            var rarityColor = ItemDisplayHelper.GetRarityColor(_item.Rarity);
            var typeString = ItemDisplayHelper.GetItemTypeString(_item.Kind);
            var rarityString = ItemDisplayHelper.GetRarityString(_item.Rarity);

            // Item Name (with rarity color)
            var nameLabel = new Label(_item.Name, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = rarityColor });
            _contentTable.Add(nameLabel).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();

            // Rarity and Type (with rarity color)
            var rarityTypeText = $"{rarityString} {typeString}";
            var rarityTypeLabel = new Label(rarityTypeText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = rarityColor });
            _contentTable.Add(rarityTypeLabel).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();

            // Only add description if it won't be duplicated by generated effect lines
            bool skipDescription = false;
            if (_item is Consumable c)
            {
                // If it actually restores something we will show generated lines so skip textual duplication
                if (c.HPRestoreAmount != 0 || c.APRestoreAmount != 0)
                    skipDescription = true;
            }
            if (!skipDescription)
            {
                var descLabel = new Label(_item.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                descLabel.SetWrap(true);
                _contentTable.Add(descLabel).Width(CARD_WIDTH - CARD_PADDING * 2).Left().Pad(0, 0, LINE_SPACING, 0);
                _contentTable.Row();
            }

            // Add conditional properties
            AddConditionalProperties();

            // Sell Price (always shown)
            var sellPrice = _item.GetSellPrice();
            var priceLabel = new Label($"Sell Price: {sellPrice}G", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
            _contentTable.Add(priceLabel).Left();
            _contentTable.Row();
        }

        /// <summary>Adds conditional properties to the tooltip based on item type.</summary>
        private void AddConditionalProperties()
        {
            // Check for HP/AP restore (Consumables)
            if (_item is Consumable consumable)
            {
                if (consumable.HPRestoreAmount > 0)
                {
                    var hpLabel = new Label($"Restores {consumable.HPRestoreAmount} HP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                else if (consumable.HPRestoreAmount < 0)
                {
                    var hpLabel = new Label("Fully restores HP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }

                if (consumable.APRestoreAmount > 0)
                {
                    var apLabel = new Label($"Restores {consumable.APRestoreAmount} AP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                else if (consumable.APRestoreAmount < 0)
                {
                    var apLabel = new Label("Fully restores AP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
            }

            // Check for stat bonuses (Gear)
            if (_item is IGear gear)
            {
                // StatBlock bonuses
                var stats = gear.StatBonus;
                if (stats.Strength > 0)
                {
                    var strLabel = new Label($"+{stats.Strength} Strength", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(strLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (stats.Agility > 0)
                {
                    var agiLabel = new Label($"+{stats.Agility} Agility", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(agiLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (stats.Vitality > 0)
                {
                    var vitLabel = new Label($"+{stats.Vitality} Vitality", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(vitLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (stats.Magic > 0)
                {
                    var magLabel = new Label($"+{stats.Magic} Magic", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(magLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }

                // Flat bonuses
                if (gear.AttackBonus != 0)
                {
                    var atkLabel = new Label($"+{gear.AttackBonus} Attack", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(atkLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (gear.DefenseBonus != 0)
                {
                    var defLabel = new Label($"+{gear.DefenseBonus} Defense", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(defLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (gear.HPBonus != 0)
                {
                    var hpLabel = new Label($"+{gear.HPBonus} HP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(hpLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
                if (gear.APBonus != 0)
                {
                    var apLabel = new Label($"+{gear.APBonus} AP", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
                    _contentTable.Add(apLabel).Left().Pad(0, 0, LINE_SPACING, 0);
                    _contentTable.Row();
                }
            }
        }

        /// <summary>Gets the current item being displayed.</summary>
        public IItem CurrentItem => _item;
    }
}

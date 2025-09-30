using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.UI
{
    /// <summary>UI window that displays detailed information about an item.</summary>
    public class ItemCard : Window
    {
        private const float CARD_WIDTH = 200f;
        private const float CARD_PADDING = 5f;
        private const float LINE_SPACING = 2f;
        
        private IItem _item;
        private Table _contentTable;

        public ItemCard(Skin skin) : base("", skin)
        {
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);
            
            _contentTable = new Table();
            Add(_contentTable).Expand().Fill().Pad(CARD_PADDING);
            
            SetWidth(CARD_WIDTH);
            SetVisible(false);
        }

        /// <summary>Shows the card with the specified item.</summary>
        public void ShowItem(IItem item)
        {
            _item = item;
            if (_item == null)
            {
                SetVisible(false);
                return;
            }

            RebuildContent();
            SetVisible(true);
            Pack();
        }

        /// <summary>Hides the card.</summary>
        public void Hide()
        {
            SetVisible(false);
            _item = null;
        }

        /// <summary>Rebuilds the card content for the current item.</summary>
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

            // Description (wrapped, white text)
            var descLabel = new Label(_item.Description, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
            descLabel.SetWrap(true);
            _contentTable.Add(descLabel).Width(CARD_WIDTH - CARD_PADDING * 2).Left().Pad(0, 0, LINE_SPACING, 0);
            _contentTable.Row();

            // Add conditional properties
            AddConditionalProperties();

            // Sell Price (always shown)
            var sellPrice = _item.GetSellPrice();
            var priceLabel = new Label($"Sell Price: {sellPrice}G", new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White });
            _contentTable.Add(priceLabel).Left();
            _contentTable.Row();
        }

        /// <summary>Adds conditional properties to the card based on item type.</summary>
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

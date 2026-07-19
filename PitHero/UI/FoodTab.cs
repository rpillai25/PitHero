using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Dining;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI component for the Food tab (issue #319): pick the hero's favorite food (radio list of
    /// all dishes with sprite, price and effects) and toggle "Eat at tavern" for the morning
    /// breakfast trip.
    /// </summary>
    public class FoodTab
    {
        private Table _mainContainer;
        private TextService _textService;

        private HoverableCheckBox _eatAtTavernCheckBox;
        private readonly CheckBox[] _dishRadios = new CheckBox[DishTypeInfo.Count];
        private ButtonGroup _dishGroup;
        private bool _refreshing;

        /// <summary>Creates and returns the main container for this tab.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _textService = Core.Services.GetService<TextService>();
            _mainContainer = new Table();
            _mainContainer.SetFillParent(false);

            var container = new Table();

            // "Favorite Food" header (extra top padding to clear tab buttons)
            var favoriteLabel = new Label(GetText(UITextKey.FoodFavoriteLabel), skin, "ph-default");
            container.Add(favoriteLabel).SetAlign(Align.Left).SetPadTop(172f).SetPadBottom(5f);
            container.Row();

            // "Eat at tavern" checkbox with tooltip, just below the header
            _eatAtTavernCheckBox = new HoverableCheckBox(
                GetText(UITextKey.FoodEatAtTavern), skin,
                GetText(UITextKey.FoodEatAtTavernTooltip), stage);
            _eatAtTavernCheckBox.OnChanged += (isChecked) =>
            {
                var dining = Core.Services.GetService<PartyDiningService>();
                if (dining != null) dining.EatAtTavern = isChecked;
            };
            container.Add(_eatAtTavernCheckBox).Left().SetPadBottom(10f);
            container.Row();

            // Radio list of all dishes: sprite, name, price, effects
            _dishGroup = new ButtonGroup();
            _dishGroup.SetMinCheckCount(1);
            _dishGroup.SetMaxCheckCount(1);

            var cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                var dish = (DishType)i;
                var def = DishConfig.GetDefinition(dish);

                var row = new Table();

                var radio = new CheckBox("", skin, "ph-default");
                _dishRadios[i] = radio;
                _dishGroup.Add(radio);
                int dishIndex = i;
                radio.OnChanged += (isChecked) =>
                {
                    if (_refreshing || !isChecked) return;
                    var dining = Core.Services.GetService<PartyDiningService>();
                    if (dining != null) dining.FavoriteDishId = dishIndex;
                };
                row.Add(radio).SetPadRight(6f);

                var sprite = cropsAtlas?.GetSprite(def.BaseSpriteName + "_Large");
                if (sprite != null)
                    row.Add(new Image(sprite)).SetPadRight(8f);

                var infoTable = new Table();
                var nameLabel = new Label(GetText(def.NameKey) + "  " + DishConfig.GetPrice(dish) + "g", skin, "ph-default");
                infoTable.Add(nameLabel).Left();
                infoTable.Row();
                var effectsLabel = new Label(BuildEffectsText(def), skin, "ph-default");
                effectsLabel.SetColor(Color.Gray);
                infoTable.Add(effectsLabel).Left();
                row.Add(infoTable).Left().SetExpandX().SetFillX();

                container.Add(row).Left().SetExpandX().SetFillX().SetPadBottom(6f);
                container.Row();
            }

            var dining0 = Core.Services.GetService<PartyDiningService>();
            if (dining0 != null)
            {
                _eatAtTavernCheckBox.IsChecked = dining0.EatAtTavern;
                if (dining0.FavoriteDishId >= 0 && dining0.FavoriteDishId < DishTypeInfo.Count)
                    _dishRadios[dining0.FavoriteDishId].IsChecked = true;
            }

            var scrollPane = new ScrollPane(container, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            _mainContainer.Add(scrollPane).Expand().Fill().Pad(10f);
            return _mainContainer;
        }

        /// <summary>Syncs the checkbox and radio states from PartyDiningService (e.g. after a load).</summary>
        public void RefreshFromService()
        {
            var dining = Core.Services.GetService<PartyDiningService>();
            if (dining == null || _eatAtTavernCheckBox == null)
                return;

            _refreshing = true;
            _eatAtTavernCheckBox.IsChecked = dining.EatAtTavern;
            if (dining.FavoriteDishId >= 0 && dining.FavoriteDishId < DishTypeInfo.Count
                && !_dishRadios[dining.FavoriteDishId].IsChecked)
            {
                _dishRadios[dining.FavoriteDishId].IsChecked = true;
            }
            _refreshing = false;
        }

        private string BuildEffectsText(DishDefinition def)
        {
            var sb = new System.Text.StringBuilder(64);
            if (def.RestoreHP > 0)
                Append(sb, "HP +" + def.RestoreHP + " now");
            if (def.RestoreFullMP)
                Append(sb, "Full MP now");
            else if (def.RestoreMP > 0)
                Append(sb, "MP +" + def.RestoreMP + " now");
            for (int b = 0; b < def.Buffs.Length; b++)
            {
                var buff = def.Buffs[b];
                switch (buff.Type)
                {
                    case RolePlayingFramework.Combat.BuffType.AttackUp: Append(sb, "ATK +" + buff.Magnitude); break;
                    case RolePlayingFramework.Combat.BuffType.DefenseUp: Append(sb, "DEF +" + buff.Magnitude); break;
                    case RolePlayingFramework.Combat.BuffType.AgilityUp: Append(sb, "AGI +" + buff.Magnitude); break;
                    case RolePlayingFramework.Combat.BuffType.MagicUp: Append(sb, "MAG +" + buff.Magnitude); break;
                    case RolePlayingFramework.Combat.BuffType.EvasionUp: Append(sb, "EVA +" + buff.Magnitude); break;
                    case RolePlayingFramework.Combat.BuffType.HPRegen: Append(sb, "HP +" + buff.Magnitude + "/round"); break;
                    case RolePlayingFramework.Combat.BuffType.MPRegen: Append(sb, "MP +" + buff.Magnitude + "/round"); break;
                }
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static void Append(System.Text.StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(text);
        }

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;
    }
}

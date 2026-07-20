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
        private readonly Image[] _dishImages = new Image[DishTypeInfo.Count];
        private readonly Label[] _dishNameLabels = new Label[DishTypeInfo.Count];
        private readonly Label[] _dishEffectsLabels = new Label[DishTypeInfo.Count];
        private readonly SineWaveLabel[] _dishMissingLabels = new SineWaveLabel[DishTypeInfo.Count];
        private readonly Element[] _dishMissingPlaceholders = new Element[DishTypeInfo.Count];
        private readonly Cell[] _dishMissingCells = new Cell[DishTypeInfo.Count];
        private ButtonGroup _dishGroup;
        private bool _refreshing;

        private static readonly Color DimmedSpriteColor = new Color(110, 110, 110, 200);
        private static readonly Color DimmedNameColor = Color.Gray;
        private static readonly Color DimmedEffectsColor = new Color(110, 110, 110, 255);

        /// <summary>Creates and returns the main container for this tab.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _textService = Core.Services.GetService<TextService>();
            _mainContainer = new Table();
            _mainContainer.SetFillParent(false);

            var container = new Table();

            // "Favorite Food" header — 16px below the tab strip (10px scroll-pane pad + 6 here)
            var favoriteLabel = new Label(GetText(UITextKey.FoodFavoriteLabel), skin, "ph-default");
            container.Add(favoriteLabel).SetAlign(Align.Left).SetPadTop(6f).SetPadBottom(5f);
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
                {
                    var image = new Image(sprite);
                    _dishImages[i] = image;
                    row.Add(image).SetPadRight(8f);
                }

                var infoTable = new Table();
                infoTable.Left(); // table contents hug the left edge (Table centers by default)
                var nameLabel = new Label(GetText(def.NameKey) + "  " + DishConfig.GetPrice(dish) + "g", skin, "ph-default");
                nameLabel.SetWrap(true);
                _dishNameLabels[i] = nameLabel;
                infoTable.Add(nameLabel).Left().SetExpandX().SetFillX();
                infoTable.Row();
                var effectsLabel = new Label(BuildEffectsText(def), skin, "ph-default");
                effectsLabel.SetWrap(true);
                effectsLabel.SetColor(Color.Gray);
                _dishEffectsLabels[i] = effectsLabel;
                infoTable.Add(effectsLabel).Left().SetExpandX().SetFillX();

                // Red waving "Missing ingredients!" (same style as MonsterUI's Sleeping label),
                // swapped in and out of a dedicated cell so available dishes reserve no space.
                var missingStyle = skin.Get<LabelStyle>("ph-sleeping")
                    ?? new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Red };
                _dishMissingLabels[i] = new SineWaveLabel(GetText(UITextKey.FoodMissingIngredients), missingStyle);
                _dishMissingPlaceholders[i] = new Element();
                infoTable.Row();
                _dishMissingCells[i] = infoTable.Add(_dishMissingPlaceholders[i]).Left();

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

            RefreshDishAvailability();

            var scrollPane = new ScrollPane(container, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            _mainContainer.Add(scrollPane).Expand().Fill().Pad(10f).SetPadLeft(24f).SetPadRight(16f);
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

            RefreshDishAvailability();
        }

        /// <summary>
        /// Dims dishes whose recipe the kitchen (fridge + storage) can't currently cover and
        /// appends a "Missing ingredients" note. Purely informational — the dish stays
        /// selectable as a favorite for when stock catches up.
        /// </summary>
        private void RefreshDishAvailability()
        {
            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            if (coordinator == null)
                return;

            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                bool coverable = coordinator.CanCoverRecipe((DishType)i);

                _dishImages[i]?.SetColor(coverable ? Color.White : DimmedSpriteColor);
                _dishNameLabels[i]?.SetColor(coverable ? Color.White : DimmedNameColor);
                _dishEffectsLabels[i]?.SetColor(coverable ? Color.Gray : DimmedEffectsColor);
                _dishMissingCells[i]?.SetElement(coverable ? _dishMissingPlaceholders[i] : _dishMissingLabels[i]);
            }
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

using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Dining;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI component for the Food tab (issue #319): pick the hero's favorite food (radio list of
    /// dishes with sprite, price and effects) and toggle "Eat at tavern" for the auto-dine trips.
    /// Dish progression (issue #417): dishes whose recipe crops are still locked are hidden;
    /// soft-unlocked dishes show faded with a "View requirements" button; fully unlocked dishes
    /// are selectable. The tab only reads unlock state — the gate itself lives in the kitchen.
    /// </summary>
    public class FoodTab
    {
        private Table _mainContainer;
        private TextService _textService;
        private Stage _stage;
        private Skin _skin;

        private HoverableCheckBox _eatAtTavernCheckBox;
        private readonly CheckBox[] _dishRadios = new CheckBox[DishTypeInfo.Count];
        private readonly Image[] _dishImages = new Image[DishTypeInfo.Count];
        private readonly Label[] _dishNameLabels = new Label[DishTypeInfo.Count];
        private readonly Label[] _dishEffectsLabels = new Label[DishTypeInfo.Count];
        private readonly SineWaveLabel[] _dishMissingLabels = new SineWaveLabel[DishTypeInfo.Count];
        private readonly TextButton[] _dishLockedButtons = new TextButton[DishTypeInfo.Count];
        private readonly Element[] _dishMissingPlaceholders = new Element[DishTypeInfo.Count];
        private readonly Cell[] _dishMissingCells = new Cell[DishTypeInfo.Count];
        private readonly Table[] _dishRows = new Table[DishTypeInfo.Count];
        private readonly Element[] _dishRowPlaceholders = new Element[DishTypeInfo.Count];
        private readonly Cell[] _dishRowCells = new Cell[DishTypeInfo.Count];
        private ButtonGroup _dishGroup;
        private bool _refreshing;

        private const float RowPadBottom = 6f;

        private static readonly Color DimmedSpriteColor = new Color(110, 110, 110, 200);

        // Label text colors. Label.SetColor only tints a label's background, never its text, so each dish
        // label owns its LabelStyle and RefreshDishAvailability recolors it with SetFontColor. Dimmed text
        // is lighter (washed out) because the window background is light parchment (230,204,135): effects are
        // 5.3:1 contrast, dimmed effects 3.1:1 so they recede but stay legible.
        private static readonly Color EffectsFontColor = PitHeroSkin.BuffFontColor;
        private static readonly Color DimmedEffectsFontColor = new Color(70, 125, 75);
        private Color _nameFontColor;
        private Color _dimmedNameFontColor;

        /// <summary>Creates and returns the main container for this tab.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _textService = Core.Services.GetService<TextService>();
            _stage = stage;
            _skin = skin;
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

            // Radio list of dishes in progression order: sprite, name, price, effects
            _dishGroup = new ButtonGroup();
            _dishGroup.SetMinCheckCount(1);
            _dishGroup.SetMaxCheckCount(1);

            var cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

            var labelFont = skin.Get<LabelStyle>("ph-default").Font;
            _nameFontColor = skin.Get<LabelStyle>("ph-default").FontColor;
            _dimmedNameFontColor = skin.Get<LabelStyle>("ph-grayed").FontColor;

            var order = DishUnlockConfig.ProgressionOrder;
            for (int o = 0; o < order.Length; o++)
            {
                var dish = order[o];
                int i = (int)dish;
                var def = DishConfig.GetDefinition(dish);

                var row = new Table();
                _dishRows[i] = row;

                var radio = new CheckBox("", skin, "ph-radio");
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
                // Own LabelStyle per label so RefreshDishAvailability can recolor it without touching "ph-default"
                var nameLabel = new Label(GetText(def.NameKey) + "  " + DishConfig.GetPrice(dish) + "g",
                    new LabelStyle(labelFont, _nameFontColor));
                nameLabel.SetWrap(true);
                _dishNameLabels[i] = nameLabel;
                infoTable.Add(nameLabel).Left().SetExpandX().SetFillX();
                infoTable.Row();
                var effectsLabel = new Label(MealBuffDisplay.BuildEffectsText(def, false, int.MaxValue),
                    new LabelStyle(labelFont, EffectsFontColor));
                effectsLabel.SetWrap(true);
                _dishEffectsLabels[i] = effectsLabel;
                infoTable.Add(effectsLabel).Left().SetExpandX().SetFillX();

                // Status cell: red waving "Missing ingredients!" (same style as MonsterUI's Sleeping label)
                // or a "View requirements" button for a soft-unlocked dish, swapped in and out of a
                // dedicated cell so ready dishes reserve no space.
                var missingStyle = skin.Get<LabelStyle>("ph-sleeping")
                    ?? new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.Red };
                _dishMissingLabels[i] = new SineWaveLabel(GetText(UITextKey.FoodMissingIngredients), missingStyle);
                var lockedButton = new TextButton(GetText(UITextKey.FoodViewRequirements), skin, "ph-default");
                lockedButton.OnClicked += (_) => new DishUnlockRequirementsDialog((DishType)dishIndex, _skin).Show(_stage);
                _dishLockedButtons[i] = lockedButton;
                _dishMissingPlaceholders[i] = new Element();
                infoTable.Row();
                _dishMissingCells[i] = infoTable.Add(_dishMissingPlaceholders[i]).Left();

                row.Add(infoTable).Left().SetExpandX().SetFillX();

                _dishRowPlaceholders[i] = new Element();
                _dishRowCells[i] = container.Add(row).Left().SetExpandX().SetFillX().SetPadBottom(RowPadBottom);
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

        /// <summary>Syncs the checkbox and radio states from PartyDiningService (e.g. after a load or when the tab is shown).</summary>
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
        /// Applies dish progression and stock to every row: crop-locked dishes are hidden,
        /// soft-unlocked dishes are dimmed with a "View requirements" button, and unlocked dishes
        /// whose recipe the kitchen (fridge + storage) can't currently cover are dimmed with a
        /// "Missing ingredients" note (still selectable as a favorite for when stock catches up).
        /// </summary>
        private void RefreshDishAvailability()
        {
            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            if (coordinator == null)
                return;

            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                var dish = (DishType)i;
                if (_dishRows[i] == null)
                    continue;

                bool soft = DishUnlockTracker.IsSoftUnlocked(dish);
                bool full = soft && DishUnlockTracker.IsFullyUnlocked(dish);
                bool coverable = full && coordinator.CanCoverRecipe(dish);

                _dishRowCells[i].SetElement(soft ? _dishRows[i] : _dishRowPlaceholders[i]);
                _dishRowCells[i].SetPadBottom(soft ? RowPadBottom : 0f);
                _dishRadios[i]?.SetDisabled(!full);

                _dishImages[i]?.SetColor(coverable ? Color.White : DimmedSpriteColor);
                _dishNameLabels[i]?.SetFontColor(coverable ? _nameFontColor : _dimmedNameFontColor);
                _dishEffectsLabels[i]?.SetFontColor(coverable ? EffectsFontColor : DimmedEffectsFontColor);
                Element status = !full ? _dishLockedButtons[i]
                    : (coverable ? _dishMissingPlaceholders[i] : _dishMissingLabels[i]);
                _dishMissingCells[i]?.SetElement(status);
            }
        }

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;
    }
}

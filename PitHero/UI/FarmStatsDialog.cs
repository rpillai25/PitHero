using System.Collections.Generic;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// The Farm Stats window (issue #413), opened from the Farm sub-bar. Lists the lifetime totals
    /// under two headings: "Field" (units harvested per crop, in progression order) and "Kitchen"
    /// (dishes served per dish). Rebuilt on every Show from GameStateService so it is always current.
    /// </summary>
    public class FarmStatsDialog
    {
        private const float WinPad = 16f;
        private const float ScrollHeight = 224f;
        private const float ContentWidth = 300f;
        private const float IconSize = 20f;
        private const float HeadingPadTop = 8f;
        // Keeps the count column clear of the scroll pane's vertical scrollbar
        private const float CountPadRight = 40f;
        // Dish art variant (same atlas as crops); the Food tab uses the same one, scaled to fit
        private const string DishSpriteSuffix = "_Large";

        private readonly Stage _stage;
        private readonly Skin _skin;
        private readonly SpriteAtlas _cropsAtlas;
        private readonly List<Element> _dismissEnvelope = new List<Element>(1);

        private Window _window;
        private Table _outerTable;
        private Table _contentTable;
        private Cell _scrollCell;
        private uint _shownFrame;
        private TextService _textService;

        public FarmStatsDialog(Stage stage)
        {
            _stage = stage;
            _skin = PitHeroSkin.CreateSkin();
            _cropsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            CreateWindow();
            _dismissEnvelope.Add(_window);
        }

        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        private void CreateWindow()
        {
            _window = new Window(GetText(UITextKey.WindowFarmStats), _skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            _outerTable = new Table();
            _outerTable.Pad(WinPad);

            _contentTable = new Table();
            _contentTable.Top().Left();
            var scroll = new ScrollPane(_contentTable, _skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            _scrollCell = _outerTable.Add(scroll).Width(ContentWidth + 24f).Height(ScrollHeight);
            _outerTable.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), _skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Hide();
            _outerTable.Add(closeButton).Width(100f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadTop(8f);

            _window.Add(_outerTable).Expand().Fill();
            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        /// <summary>Rebuilds both sections from the live counters.</summary>
        private void Rebuild()
        {
            _contentTable.Clear();
            var gameState = Core.Services?.GetService<GameStateService>();

            AddHeading(UITextKey.HeadingFarmStatsField, false);
            var order = CropUnlockConfig.ProgressionOrder;
            for (int i = 0; i < order.Length; i++)
            {
                var crop = order[i];
                int total = gameState != null ? CropUnlockConfig.GetTotal(gameState.CropHarvestedTotals, crop) : 0;
                var sprite = _cropsAtlas?.GetSprite(CropConfig.GetHarvestSpriteName(crop));
                AddRow(sprite, GetText(CropConfig.GetHarvestDisplayNameKey(crop)), total);
            }

            AddHeading(UITextKey.HeadingFarmStatsKitchen, true);
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                var dish = (DishType)i;
                var def = DishConfig.GetDefinition(dish);
                int served = gameState != null && i < gameState.DishesServedTotals.Length ? gameState.DishesServedTotals[i] : 0;
                var sprite = _cropsAtlas?.GetSprite(def.BaseSpriteName + DishSpriteSuffix);
                AddRow(sprite, GetText(def.NameKey), served);
            }
        }

        private void AddHeading(string key, bool gapAbove)
        {
            var heading = new Label(GetText(key), _skin, "ph-meal-header");
            _contentTable.Add(heading).Left().SetColspan(3).SetPadTop(gapAbove ? HeadingPadTop : 0f).SetPadBottom(2f);
            _contentTable.Row();
        }

        private void AddRow(Nez.Textures.Sprite sprite, string name, int value)
        {
            if (sprite != null)
                _contentTable.Add(new Image(new SpriteDrawable(sprite), Scaling.Fit)).Size(IconSize, IconSize).Left().SetPadRight(4f);
            else
                _contentTable.Add().Size(IconSize, IconSize).SetPadRight(4f);
            _contentTable.Add(new Label(name, _skin, "ph-default")).Left().SetExpandX();
            _contentTable.Add(new Label(value.ToString(), _skin, "ph-default")).Right().SetPadRight(CountPadRight);
            _contentTable.Row();
        }

        /// <summary>Rebuilds the stats and shows the window centered on the stage.</summary>
        public void Show()
        {
            Rebuild();
            float stageH = _stage.GetHeight();
            UILayout.FitScrollCellToStage(_window, _outerTable, _scrollCell, ScrollHeight, stageH, GameConfig.UIStageMargin);
            _window.SetPosition(
                (_stage.GetWidth() - _window.GetWidth()) / 2f,
                UILayout.CenterY(_window.GetHeight(), stageH, 0f));
            _window.SetVisible(true);
            _window.ToFront();
            _shownFrame = Time.FrameCount;
        }

        /// <summary>Hides the window.</summary>
        public void Hide()
        {
            _window?.SetVisible(false);
        }

        /// <summary>True while the window is visible.</summary>
        public bool IsVisible() => _window != null && _window.IsVisible();

        /// <summary>Per-frame poll: dismisses on an outside click.</summary>
        public void Update()
        {
            if (!IsVisible())
                return;
            if (!ConfirmationDialog.AnyVisible
                && OutsideClickDismissal.ShouldDismiss(_dismissEnvelope, _stage, _shownFrame))
                Hide();
        }
    }
}

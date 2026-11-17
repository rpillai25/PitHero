using System.Collections.Generic;
using System.Globalization;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.Config;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// The Stats window (issue #456), opened from the last top-bar button. Five tabs of lifetime
    /// totals for the current hero: Game (earn rate and adventure counters), Farming (per crop harvested /
    /// sold / seed cost / profit), Cooking (per dish cooked / profit), Fishing (per fish caught / gold,
    /// ??? until caught) and Digging (per gem found / gold, ??? until found); all but Game end in Total Earned.
    /// Every tab is rebuilt on Show from GameStateService, so it is always current when opened.
    /// Replaces the Farm Stats window (issue #413).
    /// </summary>
    public class StatsDialog
    {
        private const float WindowWidthDesign = 380f;
        private const float WindowHeightDesign = 300f;
        private const float TabStripMargin = 24f;
        private const float IconSize = 20f;
        // Value columns size to their widest entry (header or number), never below this
        private const float ValueColumnMinWidth = 48f;
        // Space between adjacent columns so headers like "Harvested" and "Sold" never touch
        private const float ColumnGap = 12f;
        // Tab cell padding left + right (see AddTab)
        private const float TabContentPadX = 12f + 8f;
        // Room for the vertical scrollbar beside the content
        private const float ScrollBarAllowance = 16f;
        private const float HeadingPadTop = 8f;
        private const float ContentPadLeft = 6f;
        // Keeps the last value column clear of the scroll pane's vertical scrollbar
        private const float LastColumnPadRight = 16f;
        private const string DishSpriteSuffix = "_Large";

        private readonly Stage _stage;
        private readonly Skin _skin;
        private readonly SpriteAtlas _cropsAtlas;
        private readonly SpriteAtlas _actorsAtlas;
        private readonly List<Element> _dismissEnvelope = new List<Element>(1);

        private Window _window;
        private TabPane _tabPane;
        private Table _gameTable;
        private Table _farmTable;
        private Table _cookTable;
        private Table _fishTable;
        private Table _digTable;
        private float _minTabStripWidth;
        private uint _shownFrame;
        private TextService _textService;

        /// <summary>The top-bar button that toggles this window; a click on it never counts as an outside click.</summary>
        public Element ToggleButton { get; set; }

        public StatsDialog(Stage stage)
        {
            _stage = stage;
            _skin = PitHeroSkin.CreateSkin();
            _cropsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            _actorsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            CreateWindow();
            _dismissEnvelope.Add(_window);
        }

        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>
        /// Window width that fits the tab strip and the widest tab content (measured after a rebuild, so
        /// longer localized headers or bigger numbers widen the window instead of clipping).
        /// </summary>
        private float MeasureWindowWidth()
        {
            float width = _minTabStripWidth > WindowWidthDesign ? _minTabStripWidth : WindowWidthDesign;
            width = System.Math.Max(width, MeasureTabWidth(_gameTable));
            width = System.Math.Max(width, MeasureTabWidth(_farmTable));
            width = System.Math.Max(width, MeasureTabWidth(_cookTable));
            width = System.Math.Max(width, MeasureTabWidth(_fishTable));
            width = System.Math.Max(width, MeasureTabWidth(_digTable));
            return width;
        }

        private static float MeasureTabWidth(Table content)
        {
            content.Invalidate();
            return content.PreferredWidth + TabContentPadX + ScrollBarAllowance;
        }

        private void CreateWindow()
        {
            _window = new Window("", _skin.Get<WindowStyle>("ph-default"));
            _window.SetMovable(false);
            _window.SetResizable(false);
            _window.Pad(0);

            _tabPane = new TabPane(_skin.Get<TabWindowStyle>("ph-default"));
            var tabStyle = new TabStyle { Background = null };
            _gameTable = AddTab(UITextKey.TabStatsGame, tabStyle);
            _farmTable = AddTab(UITextKey.TabStatsFarm, tabStyle);
            _cookTable = AddTab(UITextKey.TabStatsCooking, tabStyle);
            _fishTable = AddTab(UITextKey.TabStatsFishing, tabStyle);
            _digTable = AddTab(UITextKey.TabStatsDigging, tabStyle);

            // Measured from the real tab buttons so the strip always fits (HeroUI lesson)
            _minTabStripWidth = TabStripMargin;
            for (int i = 0; i < _tabPane.TabButtons.Count; i++)
                _minTabStripWidth += _tabPane.TabButtons[i].PreferredWidth;

            _window.Add(_tabPane).Expand().Fill().Pad(0);
            _window.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), _skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Hide();
            _window.Add(closeButton).Width(100f).SetMinHeight(GameConfig.DialogButtonMinHeight).Pad(4f, 0f, 12f, 0f);

            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        /// <summary>Adds a tab holding a vertically scrolling table and returns that table.</summary>
        private Table AddTab(string nameKey, TabStyle tabStyle)
        {
            var tab = new Tab(GetText(nameKey), tabStyle);
            var content = new Table();
            content.Top().Left();
            // Keeps the first column off the window's left edge (counted in PreferredWidth, so the window widens to match)
            content.PadLeft(ContentPadLeft);
            var scroll = new ScrollPane(content, _skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            scroll.SetFadeScrollBars(false);
            tab.Add(scroll).Expand().Fill().Pad(8f, 12f, 4f, 8f);
            _tabPane.AddTab(tab);
            return content;
        }

        // ── Game tab ──────────────────────────────────────────────────────────────

        private void RebuildGame(LifetimeStats stats)
        {
            _gameTable.Clear();
            int goldRate = stats != null ? (int)System.Math.Round(stats.GoldPerHour) : 0;
            AddGameRow(UITextKey.LabelStatsGoldRate, string.Format(GetText(UITextKey.LabelStatsGoldRateFormat), Format(goldRate)));
            AddGameRow(UITextKey.LabelStatsMonstersDefeated, Format(stats?.MonstersDefeated ?? 0));
            AddGameRow(UITextKey.LabelStatsTreasuresFound, Format(stats?.TreasuresFound ?? 0));
            AddGameRow(UITextKey.LabelStatsMercenariesHired, Format(stats?.MercenariesHired ?? 0));
            AddGameRow(UITextKey.LabelStatsHeroDefeats, Format(stats?.HeroDefeats ?? 0));
            AddGameRow(UITextKey.LabelStatsMercenaryDeaths, Format(stats?.MercenaryDeaths ?? 0));
            AddGameRow(UITextKey.LabelStatsPitLevelsCleared, Format(stats?.PitLevelsCleared ?? 0));
        }

        private void AddGameRow(string labelKey, string value)
        {
            // Same row height as the icon rows on the other tabs, so the lines breathe alike
            _gameTable.Add(new Label(GetText(labelKey), _skin, "ph-default")).Left().SetExpandX().SetMinHeight(IconSize);
            // The value sits one column in from the right edge; the blank last column keeps it there
            AddValueCell(_gameTable, value, 0f);
            AddValueCell(_gameTable, null, LastColumnPadRight);
            _gameTable.Row();
        }

        // ── Farming tab ───────────────────────────────────────────────────────────

        private void RebuildFarming(GameStateService gameState)
        {
            _farmTable.Clear();
            var stats = gameState?.Stats;

            AddHeaderRow(_farmTable, UITextKey.HeadingFarmStatsField, false,
                UITextKey.LabelStatsColHarvested, UITextKey.LabelStatsColSold, UITextKey.LabelStatsColSeedCost, UITextKey.LabelStatsColProfit);
            var order = CropUnlockConfig.ProgressionOrder;
            for (int i = 0; i < order.Length; i++)
            {
                var crop = order[i];
                int harvested = gameState != null ? CropUnlockConfig.GetTotal(gameState.CropHarvestedTotals, crop) : 0;
                int sold = stats != null ? stats.CropUnitsSold[(int)crop] : 0;
                int seedCost = stats != null ? stats.CropSeedGold[(int)crop] : 0;
                int profit = stats != null ? stats.GetCropProfit(crop) : 0;
                var sprite = _cropsAtlas?.GetSprite(CropConfig.GetHarvestSpriteName(crop));
                AddValueRow(_farmTable, sprite, GetText(CropConfig.GetHarvestDisplayNameKey(crop)),
                    Format(harvested), Format(sold), FormatGold(seedCost), FormatGold(profit));
            }
            AddTotalEarnedRow(_farmTable, 4, stats?.TotalCropSaleGold ?? 0);
        }

        // ── Cooking tab ───────────────────────────────────────────────────────────

        private void RebuildCooking(GameStateService gameState)
        {
            _cookTable.Clear();
            var stats = gameState?.Stats;

            AddHeaderRow(_cookTable, UITextKey.HeadingFarmStatsKitchen, false,
                UITextKey.LabelStatsColCooked, UITextKey.LabelStatsColProfit);
            var dishOrder = DishUnlockConfig.ProgressionOrder;
            for (int i = 0; i < dishOrder.Length; i++)
            {
                var dish = dishOrder[i];
                var def = DishConfig.GetDefinition(dish);
                int cooked = gameState != null ? DishUnlockConfig.GetTotal(gameState.DishesServedTotals, dish) : 0;
                int profit = stats != null ? stats.DishGold[(int)dish] : 0;
                var sprite = _cropsAtlas?.GetSprite(def.BaseSpriteName + DishSpriteSuffix);
                // Dish progression (issue #417): not-yet-orderable dishes are marked locked
                bool unlocked = gameState == null
                    || DishUnlockConfig.IsFullyUnlocked(dish, gameState.CropHarvestedTotals, gameState.DishesServedTotals);
                string name = unlocked
                    ? GetText(def.NameKey)
                    : string.Format(GetText(UITextKey.LabelFarmStatsLocked), GetText(def.NameKey));
                AddValueRow(_cookTable, sprite, name, Format(cooked), FormatGold(profit));
            }
            AddTotalEarnedRow(_cookTable, 2, stats?.TotalDishGold ?? 0);
        }

        // ── Fishing tab ───────────────────────────────────────────────────────────

        private void RebuildFishing(LifetimeStats stats)
        {
            _fishTable.Clear();
            AddHeaderRow(_fishTable, UITextKey.HeadingStatsFish, false,
                UITextKey.LabelStatsColCaught, UITextKey.LabelStatsColGold);
            for (int i = 0; i < FishingConfig.CatchCount; i++)
            {
                var type = (FishCatchType)i;
                if (FishingConfig.IsJunk(type))
                    continue;
                int caught = stats != null ? stats.FishCaught[i] : 0;
                if (caught <= 0)
                {
                    // Not caught yet: no art, no name, no numbers
                    AddValueRow(_fishTable, null, GetText(UITextKey.LabelCropUnknown), null, null);
                    continue;
                }
                var sprite = _actorsAtlas?.GetSprite(FishingConfig.GetSpriteName(type));
                AddValueRow(_fishTable, sprite, GetText(FishingConfig.GetNameKey(type)),
                    Format(caught), FormatGold(stats.FishGold[i]));
            }
            AddTotalEarnedRow(_fishTable, 2, stats?.TotalFishGold ?? 0);
        }

        // ── Digging tab ───────────────────────────────────────────────────────────

        private void RebuildDigging(LifetimeStats stats)
        {
            _digTable.Clear();
            AddHeaderRow(_digTable, UITextKey.HeadingStatsGems, false,
                UITextKey.LabelStatsColFound, UITextKey.LabelStatsColGold);
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                var gem = (GemType)i;
                int found = stats != null ? stats.GemsFound[i] : 0;
                if (found <= 0)
                {
                    // Not found yet: no art, no name, no numbers
                    AddValueRow(_digTable, null, GetText(UITextKey.LabelCropUnknown), null, null);
                    continue;
                }
                var sprite = _cropsAtlas?.GetSprite(GemDigConfig.GetSpriteName(gem));
                AddValueRow(_digTable, sprite, GetText(GemDigConfig.GetNameKey(gem)),
                    Format(found), FormatGold(stats.GemGold[i]));
            }
            AddTotalEarnedRow(_digTable, 2, stats?.TotalGemGold ?? 0);
        }

        /// <summary>
        /// "Total Earned" under the last row: the label in the name column, the gold in the last of the
        /// table's <paramref name="valueColumns"/> value columns (the others blank so columns stay aligned).
        /// </summary>
        private void AddTotalEarnedRow(Table table, int valueColumns, long gold)
        {
            table.Add().Size(IconSize, IconSize).SetPadRight(4f).SetPadTop(HeadingPadTop);
            table.Add(new Label(GetText(UITextKey.LabelStatsTotalEarned), _skin, "ph-meal-header")).Left().SetExpandX().SetPadTop(HeadingPadTop);
            for (int c = 0; c < valueColumns - 1; c++)
                AddValueCell(table, null, 0f);
            AddValueCell(table, string.Format(GetText(UITextKey.LabelStatsGoldFormat), gold.ToString("N0", CultureInfo.InvariantCulture)), LastColumnPadRight);
            table.Row();
        }

        // ── Shared row builders: icon | name | value columns ────────────────────────────────────

        /// <summary>Section heading over icon + name, then one right-aligned header per value column (null leaves it blank).</summary>
        private void AddHeaderRow(Table table, string headingKey, bool gapAbove, params string[] columnKeys)
        {
            float padTop = gapAbove ? HeadingPadTop : 0f;
            table.Add(new Label(GetText(headingKey), _skin, "ph-meal-header")).Left().SetColspan(2).SetPadTop(padTop).SetPadBottom(2f);
            for (int c = 0; c < columnKeys.Length; c++)
                AddColumnHeader(table, columnKeys[c], padTop, c == columnKeys.Length - 1 ? LastColumnPadRight : 0f);
            table.Row();
        }

        private void AddColumnHeader(Table table, string key, float padTop, float padRight)
        {
            Cell cell;
            if (key != null)
            {
                var label = new Label(GetText(key), _skin, "ph-default");
                label.SetAlignment(Align.Right);
                cell = table.Add(label);
            }
            else
            {
                cell = table.Add();
            }
            cell.SetMinWidth(ValueColumnMinWidth).SetPadLeft(ColumnGap).Right().SetPadTop(padTop).SetPadRight(padRight).SetPadBottom(2f);
        }

        /// <summary>Icon, name, then one right-aligned cell per value (null leaves it blank). Rows in one table pass the same count.</summary>
        private void AddValueRow(Table table, Nez.Textures.Sprite sprite, string name, params string[] values)
        {
            if (sprite != null)
                table.Add(new Image(new SpriteDrawable(sprite), Scaling.Fit)).Size(IconSize, IconSize).Left().SetPadRight(4f);
            else
                table.Add().Size(IconSize, IconSize).SetPadRight(4f);
            table.Add(new Label(name, _skin, "ph-default")).Left().SetExpandX();
            for (int c = 0; c < values.Length; c++)
                AddValueCell(table, values[c], c == values.Length - 1 ? LastColumnPadRight : 0f);
            table.Row();
        }

        private void AddValueCell(Table table, string value, float padRight)
        {
            Cell cell;
            if (value != null)
            {
                var label = new Label(value, _skin, "ph-default");
                label.SetAlignment(Align.Right);
                cell = table.Add(label);
            }
            else
            {
                cell = table.Add();
            }
            cell.SetMinWidth(ValueColumnMinWidth).SetPadLeft(ColumnGap).Right().SetPadRight(padRight);
        }

        private static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private string FormatGold(int value) => string.Format(GetText(UITextKey.LabelStatsGoldFormat), Format(value));

        // ── Show / hide ───────────────────────────────────────────────────────────

        /// <summary>Rebuilds every tab and shows the window centered on the stage (the last-picked tab stays selected).</summary>
        public void Show()
        {
            var gameState = Core.Services?.GetService<GameStateService>();
            var stats = gameState?.Stats;
            RebuildGame(stats);
            RebuildFarming(gameState);
            RebuildCooking(gameState);
            RebuildFishing(stats);
            RebuildDigging(stats);

            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();
            float winH = UILayout.FitHeight(WindowHeightDesign, stageH, GameConfig.UIStageMargin, GameConfig.UIStageMargin);
            float wanted = MeasureWindowWidth();
            float winW = wanted < stageW ? wanted : stageW;
            _window.SetSize(winW, winH);
            _window.Validate();
            _window.SetPosition((stageW - winW) / 2f, UILayout.CenterY(winH, stageH, 0f));
            _window.SetVisible(true);
            _window.ToFront();
            _shownFrame = Time.FrameCount;
        }

        /// <summary>Opens the window, or closes it when it is already open (the top-bar button).</summary>
        public void Toggle()
        {
            if (IsVisible())
                Hide();
            else
                Show();
        }

        /// <summary>Hides the window.</summary>
        public void Hide()
        {
            _window?.SetVisible(false);
        }

        /// <summary>True while the window is visible.</summary>
        public bool IsVisible() => _window != null && _window.IsVisible();

        /// <summary>Per-frame poll: dismisses on an outside click (the toggle button excepted).</summary>
        public void Update()
        {
            if (!IsVisible())
                return;
            if (!ConfirmationDialog.AnyVisible
                && !OutsideClickDismissal.IsMouseInside(ToggleButton, _stage)
                && OutsideClickDismissal.ShouldDismiss(_dismissEnvelope, _stage, _shownFrame))
                Hide();
        }
    }
}

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.UI
{
    /// <summary>
    /// Compact "Monster Info" card docked to the right of the aggregate Monsters window (issue #394).
    /// Shows the roster's daytime/nighttime totals plus the farm/kitchen/idle split of the shift that
    /// is currently awake.
    /// </summary>
    public class MonsterInfoPanel : Window
    {
        private const float ContentPadding = 6f;

        /// <summary>
        /// Widest count text the card must be able to show without truncating. The width is measured
        /// against this once, so a roster that grows from 9 to 999 never re-Packs the card.
        /// </summary>
        private const string WidestValue = "8888";

        /// <summary>
        /// Measured once at construction and fixed thereafter. MonsterUI.PositionWindow derives the
        /// roster X from this card every frame, so a Pack()-driven width would slide the whole window
        /// sideways the moment a counter crossed 9 to 10. It is measured rather than hardcoded
        /// because the captions are localized — a hardcoded guess truncates the moment they change.
        /// </summary>
        public float PanelWidth { get; private set; }
        private const float CaptionGap = 12f;
        /// <summary>Vertical gap between stat rows, so the labels do not run together.</summary>
        private const float RowGap = 4f;

        private static readonly Color BrownColor = new Color(71, 36, 7);

        private readonly Table _contentTable;
        private readonly Label _daytimeValue;
        private readonly Label _nighttimeValue;
        private readonly Label _farmValue;
        private readonly Label _kitchenValue;
        private readonly Label _idleValue;

        private TextService _textService;
        private bool _hasStatRow;

        public MonsterInfoPanel(Skin skin) : base("", skin)
        {
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);

            GetTitleLabel().SetText(GetText(UITextKey.WindowMonsterInfo));

            _contentTable = new Table();
            _contentTable.Top().Left();

            _daytimeValue   = AddStatRow(UITextKey.MonsterInfoTotalDaytime);
            _nighttimeValue = AddStatRow(UITextKey.MonsterInfoTotalNighttime);
            _farmValue      = AddStatRow(UITextKey.MonsterInfoFarmWorkers);
            _kitchenValue   = AddStatRow(UITextKey.MonsterInfoKitchenWorkers);
            _idleValue      = AddStatRow(UITextKey.MonsterInfoIdleWorkers);

            Add(_contentTable).Expand().Fill().Pad(ContentPadding);
            SetVisible(false);
            MeasurePanelWidth();
        }

        /// <summary>
        /// Packs once with the widest count text every row could hold and keeps that width. Doing it
        /// here means the card fits its longest localized caption plus a four-digit value, and never
        /// needs to resize again.
        /// </summary>
        private void MeasurePanelWidth()
        {
            _daytimeValue.SetText(WidestValue);
            _nighttimeValue.SetText(WidestValue);
            _farmValue.SetText(WidestValue);
            _kitchenValue.SetText(WidestValue);
            _idleValue.SetText(WidestValue);
            Pack();
            PanelWidth = GetWidth();

            _daytimeValue.SetText("0");
            _nighttimeValue.SetText("0");
            _farmValue.SetText("0");
            _kitchenValue.SetText("0");
            _idleValue.SetText("0");
            PackFixedWidth();
        }

        /// <summary>Recomputes the counts from the roster and resizes the card to fit.</summary>
        public void Refresh(IReadOnlyList<AlliedMonster> roster, bool isNighttime)
        {
            var stats = MonsterInfoStats.Compute(roster, isNighttime);
            _daytimeValue.SetText(stats.TotalDaytime.ToString());
            _nighttimeValue.SetText(stats.TotalNighttime.ToString());
            _farmValue.SetText(stats.FarmWorkers.ToString());
            _kitchenValue.SetText(stats.KitchenWorkers.ToString());
            _idleValue.SetText(stats.IdleWorkers.ToString());
            PackFixedWidth();
        }

        /// <summary>Packs, then pins the width back so the card never resizes under the layout.</summary>
        private void PackFixedWidth()
        {
            Pack();
            SetSize(PanelWidth, GetHeight());
        }

        /// <summary>
        /// Adds a caption/value row and returns the value label for later updates. Every row but the
        /// first carries the gap, so the spacing lands between the labels rather than above the list.
        /// Both cells take it, or the caption and its value would sit on different baselines.
        /// </summary>
        private Label AddStatRow(string captionKey)
        {
            float padTop = _hasStatRow ? RowGap : 0f;
            _hasStatRow = true;

            var valueLabel = new Label("0", BrownStyle());
            _contentTable.Add(new Label(GetText(captionKey), BrownStyle())).Left().SetPadRight(CaptionGap).SetPadTop(padTop);
            _contentTable.Add(valueLabel).Right().SetExpandX().SetPadTop(padTop);
            _contentTable.Row();
            return valueLabel;
        }

        private static LabelStyle BrownStyle() =>
            new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownColor };

        private string GetText(string key)
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }
    }
}

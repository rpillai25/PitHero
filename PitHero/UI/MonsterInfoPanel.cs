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
            Pack();
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
            Pack();
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

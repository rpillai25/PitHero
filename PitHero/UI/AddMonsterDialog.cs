using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Config;
using PitHero.Services;
using RolePlayingFramework.Enemies;

namespace PitHero.UI
{
    /// <summary>
    /// Dialog for manually adding monsters to a Monster House in exchange for gold (issue #283).
    /// Lists every monster type the player has defeated in battle that is recruitable, split into
    /// Daytime and Nocturnal sections of 64×64 sprite slots. Clicking a slot confirms a gold
    /// purchase and drops a new allied monster into the house if there is room.
    /// </summary>
    public class AddMonsterDialog : Window
    {
        private readonly Skin _skin;
        private Stage _stage;
        private TextService _textService;

        private readonly Label _remainingLabel;
        private readonly Table _contentTable;

        private int _houseId = -1;
        private bool _isVisible;

        private const float SlotSize = 64f;
        private const int Columns = 6;
        private static readonly Color BrownColor = new Color(71, 36, 7);

        /// <summary>Whether the dialog is currently visible.</summary>
        public bool IsVisible => _isVisible;

        public AddMonsterDialog(Skin skin, Stage stage) : base("", skin, "ph-default")
        {
            _skin = skin;
            _stage = stage;
            SetMovable(false);
            SetResizable(false);
            SetSize(460f, 340f);

            GetTitleLabel().SetText(GetText(UITextKey.AddMonsterWindowTitle));

            _remainingLabel = new Label("", skin, "ph-default");
            Add(_remainingLabel).Left().Pad(6f, 8f, 4f, 8f);
            Row();

            _contentTable = new Table();
            _contentTable.Top().Left();
            var scrollPane = new ScrollPane(_contentTable, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);
            Add(scrollPane).Expand().Fill().Pad(4f, 8f, 4f, 8f);
            Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.OnClicked += (_) => Close();
            Add(closeButton).Width(120f).Pad(4f, 8f, 8f, 8f);

            SetVisible(false);
        }

        /// <summary>Opens the dialog for the given Monster House (by UniqueId).</summary>
        public void ShowForHouse(int houseId)
        {
            _houseId = houseId;

            if (!_isVisible)
            {
                UIWindowManager.OnUIWindowOpening();
                _stage?.AddElement(this);
                SetVisible(true);
                ToFront();
                _isVisible = true;
                Core.Services.GetService<PauseService>()?.Pause();
            }

            Rebuild();
            CenterOnStage();
        }

        /// <summary>Closes the dialog and unpauses.</summary>
        public void Close()
        {
            if (!_isVisible) return;
            UIWindowManager.OnUIWindowClosing();
            SetVisible(false);
            Remove();
            _isVisible = false;
            _houseId = -1;
            Core.Services.GetService<PauseService>()?.Unpause();
        }

        private void CenterOnStage()
        {
            var stage = _stage ?? GetStage();
            if (stage == null) return;
            SetPosition((stage.GetWidth() - GetWidth()) / 2f, (stage.GetHeight() - GetHeight()) / 2f);
        }

        private void Rebuild()
        {
            _contentTable.Clear();

            var alliedManager = Core.Services?.GetService<AlliedMonsterManager>();
            int remaining = GameConfig.MonsterHouseCapacity;
            if (alliedManager != null)
                remaining = GameConfig.MonsterHouseCapacity - alliedManager.GetLinkedMonsterCount(_houseId);
            if (remaining < 0) remaining = 0;
            _remainingLabel.SetText(string.Format(GetText(UITextKey.AddMonsterRemainingSpace), remaining));

            // Gather defeated + recruitable monster types, split by day/night, in EnemyId order.
            var defeated = Core.Services?.GetService<DefeatedMonsterService>();
            var daytime = new List<IEnemy>();
            var nocturnal = new List<IEnemy>();
            if (defeated != null)
            {
                var ids = (EnemyId[])Enum.GetValues(typeof(EnemyId));
                for (int i = 0; i < ids.Length; i++)
                {
                    if (!defeated.IsDefeated(ids[i])) continue;
                    var enemy = EnemyFactory.Create(ids[i]);
                    if (!enemy.IsRecruitable) continue;
                    if (MonsterScheduleConfig.IsNocturnal(enemy.Name))
                        nocturnal.Add(enemy);
                    else
                        daytime.Add(enemy);
                }
            }

            var actorsAtlas = TryLoadActorsAtlas();

            AddSection(GetText(UITextKey.AddMonsterDaytimeLabel), daytime, actorsAtlas, remaining);
            AddSection(GetText(UITextKey.AddMonsterNocturnalLabel), nocturnal, actorsAtlas, remaining);
        }

        private void AddSection(string header, List<IEnemy> monsters, Nez.Sprites.SpriteAtlas actorsAtlas, int remaining)
        {
            _contentTable.Add(new Label(header, _skin, "ph-default")).Left().Pad(6f, 2f, 2f, 2f);
            _contentTable.Row();

            if (monsters.Count == 0)
            {
                _contentTable.Add(new Label("  -", BrownStyle())).Left().Pad(0f, 8f, 4f, 2f);
                _contentTable.Row();
                return;
            }

            var grid = new Table();
            grid.Left();
            for (int i = 0; i < monsters.Count; i++)
            {
                var enemy = monsters[i];
                grid.Add(CreateSlot(enemy, actorsAtlas, remaining)).Size(SlotSize, SlotSize).Pad(2f);
                if ((i + 1) % Columns == 0)
                    grid.Row();
            }
            _contentTable.Add(grid).Left().Pad(0f, 4f, 6f, 2f);
            _contentTable.Row();
        }

        private Element CreateSlot(IEnemy enemy, Nez.Sprites.SpriteAtlas actorsAtlas, int remaining)
        {
            string typeDisplayName = GetText(TextType.Monster, enemy.Name);

            Nez.Textures.Sprite sprite = null;
            if (actorsAtlas != null)
            {
                try
                {
                    var anim = actorsAtlas.GetAnimation($"{enemy.EnemyId}MoveDown");
                    if (anim?.Sprites != null && anim.Sprites.Length > 0)
                        sprite = anim.Sprites[0];
                }
                catch (Exception) { sprite = null; }
            }

            if (sprite == null)
            {
                // Fallback: a labeled button so the slot is still clickable.
                var fallback = new TextButton("?", _skin, "ph-default");
                fallback.OnClicked += (_) => PromptPurchase(enemy, typeDisplayName);
                return fallback;
            }

            var style = new ImageButtonStyle { ImageUp = new SpriteDrawable(sprite) };
            var btn = new HoverableImageButton(style, typeDisplayName);
            btn.GetImageCell().Size(SlotSize, SlotSize);
            // Show sprites at their natural size (centered in the 64×64 slot); only scale down the
            // ones that are actually larger than the slot. Avoids blowing small sprites up to 64×64.
            bool oversized = sprite.SourceRect.Width > SlotSize || sprite.SourceRect.Height > SlotSize;
            btn.GetImage().SetScaling(oversized ? Scaling.Fit : Scaling.None);
            btn.SetDisabled(remaining <= 0);
            btn.OnClicked += (_) => PromptPurchase(enemy, typeDisplayName);
            return btn;
        }

        private void PromptPurchase(IEnemy enemy, string typeDisplayName)
        {
            int cost = MonsterScheduleConfig.IsNocturnal(enemy.Name)
                ? GameConfig.NocturnalMonsterAddCostGold
                : GameConfig.DaytimeMonsterAddCostGold;

            var message = string.Format(GetText(UITextKey.AddMonsterConfirmPrompt), typeDisplayName, cost);
            var confirm = new ConfirmationDialog(GetText(UITextKey.AddMonsterWindowTitle), message, _skin,
                onYes: () => TryPurchase(enemy, cost));
            var stage = _stage ?? GetStage();
            if (stage != null)
                confirm.Show(stage);
        }

        private void TryPurchase(IEnemy enemy, int cost)
        {
            var alliedManager = Core.Services?.GetService<AlliedMonsterManager>();
            var gameState = Core.Services?.GetService<GameStateService>();
            if (alliedManager == null || gameState == null) return;

            // Re-check room and gold at confirm time (state may have changed).
            if (alliedManager.IsHouseFull(_houseId)) return;
            if (gameState.Funds < cost) return;

            var added = alliedManager.AddPurchasedMonster(enemy, _houseId);
            if (added == null) return;

            gameState.Funds -= cost;

            // Close if the house just filled up; otherwise refresh the remaining-space display.
            if (alliedManager.IsHouseFull(_houseId))
                Close();
            else
                Rebuild();
        }

        private Nez.Sprites.SpriteAtlas TryLoadActorsAtlas()
        {
            try { return Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas"); }
            catch (Exception ex)
            {
                Debug.Warn($"[AddMonsterDialog] Failed to load Actors.atlas: {ex.Message}");
                return null;
            }
        }

        private LabelStyle BrownStyle() => new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownColor };

        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        private string GetText(TextType type, string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(type, key) ?? key;
        }
    }
}

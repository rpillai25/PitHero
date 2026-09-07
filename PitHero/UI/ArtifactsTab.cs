using Nez;
using Nez.UI;
using PitHero.Artifacts;
using PitHero.Services;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// The Party window's Artifacts tab: a small fixed grid of system-level artifacts the player owns
    /// (they belong to the player, not the hero, so the grid is the same for every save). Clicking an
    /// artifact opens its card.
    /// </summary>
    public class ArtifactsTab
    {
        private Skin _skin;
        private Stage _stage;
        private readonly List<ArtifactSlot> _slots = new List<ArtifactSlot>(GameConfig.ArtifactGridColumns * GameConfig.ArtifactGridRows);
        private readonly List<ArtifactType> _owned = new List<ArtifactType>(ArtifactCatalog.Count);
        private int _shownVersion = -1;
        private ArtifactInfoDialog _dialog;

        /// <summary>Builds the tab content: the grid, filled from the artifact service.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _skin = skin;
            _stage = stage;

            var grid = new Table();
            grid.Top().Left().Pad(4f);
            for (int row = 0; row < GameConfig.ArtifactGridRows; row++)
            {
                for (int col = 0; col < GameConfig.ArtifactGridColumns; col++)
                {
                    var slot = new ArtifactSlot();
                    slot.OnClicked += ShowCard;
                    _slots.Add(slot);
                    grid.Add(slot).Size(GameConfig.ArtifactSlotSize, GameConfig.ArtifactSlotSize).Pad(2f);
                }
                grid.Row();
            }

            var content = new Table();
            content.Top().Left().Pad(8f).PadLeft(24f);
            content.Add(grid).Top().Left();

            Refresh();
            return content;
        }

        /// <summary>Re-reads ownership into the slots (cheap; skips when nothing changed).</summary>
        public void Refresh()
        {
            var service = ArtifactService.Current;
            int version = service != null ? service.Version : 0;
            if (version == _shownVersion)
                return;
            _shownVersion = version;

            _owned.Clear();
            service?.GetOwnedInOrder(_owned);
            var textService = Core.Services?.GetService<TextService>();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < _owned.Count)
                {
                    string name = textService?.DisplayText(TextType.UI, ArtifactCatalog.GetNameKey(_owned[i])) ?? _owned[i].ToString();
                    _slots[i].SetArtifact(_owned[i], name);
                }
                else
                {
                    _slots[i].SetArtifact(null, null);
                }
            }
        }

        private void ShowCard(ArtifactType artifact)
        {
            if (_stage == null || _skin == null)
                return;
            _dialog?.Remove();
            _dialog = new ArtifactInfoDialog(artifact, _skin);
            _dialog.Show(_stage);
        }
    }
}

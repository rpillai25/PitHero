using System.Collections.Generic;
using Nez;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Automatically sells harvested crop stacks that have reached their max stack size, for crop
    /// types the player has designated. Sell passes are throttled to once per second and only fire
    /// while the game is unpaused.
    /// </summary>
    public class AutoCropSellService
    {
        private const string SellSource = "sell_crops";

        private readonly CropStorageInventoryService _storage;
        private readonly GameStateService _gameState;
        private readonly List<int> _idScratch = new List<int>(16);
        private float _throttleTimer;

        /// <summary>Whether automatic crop selling is active.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Per-crop auto-sell designations indexed by CropType. All true by default so that the
        /// first time auto-sell is enabled every crop type participates.
        /// </summary>
        public bool[] Designations { get; } = new bool[CropTypeInfo.Count];

        /// <summary>Initialises the service with the required dependencies.</summary>
        public AutoCropSellService(CropStorageInventoryService storage, GameStateService gameState)
        {
            _storage = storage;
            _gameState = gameState;
            for (int i = 0; i < Designations.Length; i++)
                Designations[i] = true;
        }

        /// <summary>
        /// Advances the sell throttle and, when enabled, sells all full designated crop stacks.
        /// Called once per game frame while the game is unpaused.
        /// </summary>
        public void Update()
        {
            if (!Enabled) return;

            _throttleTimer += Time.DeltaTime;
            if (_throttleTimer < 1f) return;
            _throttleTimer = 0f;

            TrySellPass();
        }

        /// <summary>
        /// Executes one sell pass: sells every designated crop stack at max stack size across all
        /// Crop Storage buildings. Idempotent and safe to call directly from tests, bypassing the
        /// 1-second throttle gate.
        /// </summary>
        public void TrySellPass()
        {
            if (_storage == null || _gameState == null) return;

            _storage.CopyBuildingIds(_idScratch);
            for (int b = 0; b < _idScratch.Count; b++)
            {
                int buildingId = _idScratch[b];
                var slots = _storage.GetSlots(buildingId);
                for (int s = 0; s < slots.Count; s++)
                {
                    if (slots[s].IsEmpty) continue;
                    var crop = slots[s].Type;
                    if (!Designations[(int)crop]) continue;
                    if (slots[s].Count < CropConfig.GetMaxHarvestStack(crop)) continue;

                    int gold = CropConfig.GetHarvestStackSellPrice(crop, slots[s].Count);
                    _gameState.AddFunds(gold, SellSource);
                    _storage.ClearSlot(buildingId, s);
                }
            }
        }
    }
}

using Nez;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Automatically purchases seeds from the shop to fulfil outstanding crop planting plans.
    /// Purchases are throttled to once per second and only fire while the game is unpaused.
    /// </summary>
    public class AutoSeedPurchaseService
    {
        private readonly CropPlantingService _cropPlanting;
        private readonly CropGrowthService   _cropGrowth;
        private readonly GameStateService    _gameState;
        private readonly FarmTaskCoordinator _coordinator;
        private float _throttleTimer;

        /// <summary>Whether automatic seed purchasing is active.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Minimum gold the player must have above the seed price before a purchase is made.
        /// No purchase fires when Funds - price &lt; GoldBuffer.
        /// </summary>
        public int GoldBuffer { get; set; } = 200;

        /// <summary>
        /// Initialises the service with the required dependencies.
        /// <paramref name="coordinator"/> may be null in headless test contexts.
        /// </summary>
        public AutoSeedPurchaseService(
            CropPlantingService cropPlanting,
            CropGrowthService   cropGrowth,
            GameStateService    gameState,
            FarmTaskCoordinator coordinator)
        {
            _cropPlanting = cropPlanting;
            _cropGrowth   = cropGrowth;
            _gameState    = gameState;
            _coordinator  = coordinator;
        }

        /// <summary>
        /// Advances the purchase throttle and, when enabled, buys seeds for all under-stocked plans.
        /// Called once per game frame while the game is unpaused.
        /// </summary>
        public void Update()
        {
            if (!Enabled) return;

            _throttleTimer += Time.DeltaTime;
            if (_throttleTimer < 1f) return;
            _throttleTimer = 0f;

            TryPurchasePass();
        }

        /// <summary>
        /// Executes one purchase pass: buys seeds for any under-stocked plans while funds and the
        /// gold-buffer constraint allow. Idempotent and safe to call directly from tests, bypassing
        /// the 1-second throttle gate.
        /// </summary>
        public void TryPurchasePass()
        {
            if (_cropPlanting == null || _gameState == null) return;

            bool boughtAnything = false;
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                var crop  = (CropType)i;
                int price = CropConfig.GetSeedPrice(crop);
                if (price <= 0) continue;

                int needed = _cropPlanting.CountUnplantedPlans(crop, _cropGrowth);
                if (needed <= 0) continue;
                // Never buy past the per-crop inventory cap — AddSeeds would clamp the
                // overflow away and the gold would be wasted.
                if (needed > GameConfig.SeedInventoryMaxPerCrop)
                    needed = GameConfig.SeedInventoryMaxPerCrop;

                int owned = _cropPlanting.SeedInventory != null
                    ? _cropPlanting.SeedInventory[(int)crop]
                    : 0;

                while (owned < needed && _gameState.Funds - price >= GoldBuffer)
                {
                    _gameState.Funds -= price;
                    _cropPlanting.AddSeeds(crop, 1);
                    owned++;
                    boughtAnything = true;
                }
            }

            if (boughtAnything)
                _coordinator?.RescanForPlanting();
        }
    }
}

using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Per-tile dig cooldowns for the Digging Field (issue #452). Each of the field's 32 tiles can be
    /// dug once, then rests for <see cref="GameConfig.DiggingFieldCooldownSeconds"/> of simulation
    /// time before it can be dug again. Cooldowns are indexed by the tile's place inside the field
    /// (<see cref="BuildingConfig.GetDiggingFieldTileIndex"/>), so moving the field keeps them.
    ///
    /// Simulation state, read and written only by the DigGem handler (the UI reads it to draw the
    /// overlay and gate clicks). Scene-scoped and never saved: a load, a new game or a replay seek
    /// starts with every tile ready, exactly as the recording's start state does.
    /// </summary>
    public class DiggingFieldService
    {
        /// <summary>Number of diggable tiles (the whole 8x4 footprint).</summary>
        public const int TileCount = BuildingConfig.DiggingFieldWidth * BuildingConfig.DiggingFieldHeight;

        private readonly float[] _readyAt = new float[TileCount];
        private readonly BuildingService _buildings;
        private readonly System.Func<float> _now;

        /// <summary>Creates the service over the scene's buildings, timed by SimulationClock.Now.</summary>
        public DiggingFieldService(BuildingService buildings) : this(buildings, () => SimulationClock.Now) { }

        /// <summary>Creates the service with an injectable clock (tests).</summary>
        public DiggingFieldService(BuildingService buildings, System.Func<float> now)
        {
            _buildings = buildings;
            _now = now;
        }

        /// <summary>The placed Digging Field, or null when none is placed.</summary>
        public PlacedBuilding GetField()
        {
            if (_buildings == null)
                return null;
            var all = _buildings.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.DiggingField)
                    return all[i];
            return null;
        }

        /// <summary>Index of a world tile inside the placed field, or -1 (outside, or no field).</summary>
        public int GetTileIndex(int tileX, int tileY)
        {
            var field = GetField();
            return field == null ? -1 : BuildingConfig.GetDiggingFieldTileIndex(field.TileX, field.TileY, tileX, tileY);
        }

        /// <summary>True while the tile at this field index is still resting after a dig.</summary>
        public bool IsCooling(int index)
        {
            return index >= 0 && index < TileCount && _now() < _readyAt[index];
        }

        /// <summary>
        /// Remaining cooldown of the tile at this field index as a fraction, 1 just after a dig down to
        /// 0 when ready (for the overlay).
        /// </summary>
        public float GetCooldownFraction(int index)
        {
            if (index < 0 || index >= TileCount)
                return 0f;
            float left = _readyAt[index] - _now();
            if (left <= 0f)
                return 0f;
            float total = GameConfig.DiggingFieldCooldownSeconds;
            return total > 0f ? (left > total ? 1f : left / total) : 0f;
        }

        /// <summary>Starts the tile's cooldown now.</summary>
        public void StartCooldown(int index)
        {
            if (index >= 0 && index < TileCount)
                _readyAt[index] = _now() + GameConfig.DiggingFieldCooldownSeconds;
        }

        /// <summary>
        /// Digs a world tile of the field: resolves the find from the roll (advancing pity), pays a
        /// gem's value as <paramref name="goldSource"/> and rests the tile. Returns false and changes
        /// nothing when the Prospector's Map is not owned, the tile is outside the field, or it is
        /// still resting. Shared by the DigGem command (the player) and the DigBot, so a bot dig is
        /// exactly a player dig.
        /// </summary>
        public bool TryDig(GameStateService gameState, int tileX, int tileY, long roll, string goldSource, out PitHero.Config.DigFind find)
        {
            find = default;
            if (!PitHero.Artifacts.LocalArtifactEffects.HasGemDigSite(gameState))
                return false;
            int index = GetTileIndex(tileX, tileY);
            if (index < 0 || IsCooling(index))
                return false;
            // Every dig rests the tile; only a gem pays (an empty dig or junk is worth nothing)
            find = gameState.Dig(roll);
            if (find.IsGem)
                gameState.AddFunds(PitHero.Config.GemDigConfig.GetValue(find), goldSource);
            StartCooldown(index);
            return true;
        }

        /// <summary>Makes every tile ready.</summary>
        public void Reset()
        {
            for (int i = 0; i < TileCount; i++)
                _readyAt[i] = 0f;
        }
    }
}

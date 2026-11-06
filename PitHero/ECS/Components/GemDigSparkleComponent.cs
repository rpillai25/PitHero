using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Config;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Equipment;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Star glints on the gems drawn into the gem dig site (issue #452), showing how much pity has
    /// built up. At heat 0 a lone white glint appears every few seconds; as a tier's odds climb
    /// toward their cap the glints come faster, tinted cyan / pink / yellow for Rare / Epic / Legendary, and near the
    /// cap a gold glimmer puff rises from the mound. A find that resets the counters calms it down.
    ///
    /// Cosmetic only: it reads the pity counters and never writes simulation state. Its randomness
    /// is a private reseeded System.Random (never Nez.Random); the glints are stock SpriteRenderers,
    /// so the replay frame stream records them. It hides everything during replay seeks.
    /// </summary>
    public class GemDigSparkleComponent : Component, IUpdatable
    {
        // Twinkle: dot, plus, star, plus, dot (indices into _frames)
        private static readonly int[] Sequence = { 0, 1, 2, 1, 0 };
        private const int RandomSeed = 452;

        private readonly Sprite[] _frames;
        private readonly Vector2 _glimmerPosition;
        private SpriteRenderer[] _glints;
        private float[] _glintElapsed;
        private bool[] _glintActive;
        private float _spawnTimer = 1f;
        private float _glimmerTimer;
        private System.Random _random;
        private GameStateService _gameState;
        private PauseService _pauseService;

        /// <summary>Creates the glints from the three twinkle frames; glimmer puffs spawn at glimmerPosition.</summary>
        public GemDigSparkleComponent(Sprite dot, Sprite plus, Sprite star, Vector2 glimmerPosition)
        {
            _frames = new[] { dot, plus, star };
            _glimmerPosition = glimmerPosition;
        }

        /// <summary>Builds one hidden glint renderer per gem point.</summary>
        public override void OnAddedToEntity()
        {
            var offsets = GameConfig.GemDigSiteGlintOffsets;
            _glints = new SpriteRenderer[offsets.Length];
            _glintElapsed = new float[offsets.Length];
            _glintActive = new bool[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var glint = Entity.AddComponent(new SpriteRenderer(_frames[0]));
                glint.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
                glint.SetLocalOffset(new Vector2(offsets[i].X, offsets[i].Y));
                glint.Enabled = false;
                _glints[i] = glint;
            }
            _random = new System.Random(RandomSeed);
            _pauseService = Core.Services.GetService<PauseService>();
        }

        /// <summary>Spawns, animates and retires glints on simulation time.</summary>
        public void Update()
        {
            if (Core.CosmeticUpdatesSuspended)
            {
                HideAll(); // replay seek: no glint should be mid-twinkle when the seek ends
                return;
            }
            if (_pauseService?.IsPaused == true)
                return;

            _gameState ??= Core.Services.GetService<GameStateService>();
            float heat = 0f;
            var tier = ItemRarity.Normal;
            if (_gameState != null)
                heat = _gameState.GetGemPityHeat(out tier);

            float dt = Time.DeltaTime;
            AdvanceGlints(dt, GetTint(heat, tier));

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                StartGlint();
                float interval = MathHelper.Lerp(GameConfig.GemGlintIdleIntervalSeconds, GameConfig.GemGlintHotIntervalSeconds, heat);
                _spawnTimer = interval * (0.5f + (float)_random.NextDouble()); // 0.5x..1.5x jitter
            }

            if (heat >= GameConfig.GemGlimmerHeatThreshold)
            {
                _glimmerTimer -= dt;
                if (_glimmerTimer <= 0f)
                {
                    ParticleEffectManager.Current?.SpawnEffectAtPosition(ParticleEffectType.GemGlimmer, _glimmerPosition,
                        Entity.Scene, GameConfig.RenderLayerActorPropOverlay);
                    _glimmerTimer = GameConfig.GemGlimmerIntervalSeconds;
                }
            }
            else
            {
                _glimmerTimer = 0f; // the first puff comes as soon as the heat crosses the threshold
            }
        }

        /// <summary>
        /// White at heat 0; otherwise leans toward the hottest tier's glint tint (bright DB32 accents:
        /// the rarity colors themselves are too dark to read as a glint on the dirt).
        /// </summary>
        private static Color GetTint(float heat, ItemRarity tier)
        {
            Color target;
            switch (tier)
            {
                case ItemRarity.Rare: target = GameConfig.GemGlintRareTint; break;
                case ItemRarity.Epic: target = GameConfig.GemGlintEpicTint; break;
                case ItemRarity.Legendary: target = GameConfig.GemGlintLegendaryTint; break;
                default: return Color.White;
            }
            if (heat <= 0f)
                return Color.White;
            return Color.Lerp(Color.White, target, GameConfig.GemGlintTintStrength);
        }

        private void AdvanceGlints(float dt, Color tint)
        {
            for (int i = 0; i < _glints.Length; i++)
            {
                if (!_glintActive[i])
                    continue;
                _glintElapsed[i] += dt;
                int step = (int)(_glintElapsed[i] / GameConfig.GemGlintFrameSeconds);
                if (step >= Sequence.Length)
                {
                    _glintActive[i] = false;
                    _glints[i].Enabled = false;
                    continue;
                }
                _glints[i].SetSprite(_frames[Sequence[step]]);
                _glints[i].SetColor(tint);
            }
        }

        /// <summary>Starts a glint on a random gem point that is not already twinkling.</summary>
        private void StartGlint()
        {
            int free = 0;
            for (int i = 0; i < _glintActive.Length; i++)
                if (!_glintActive[i]) free++;
            if (free == 0)
                return;
            int pick = _random.Next(free);
            for (int i = 0; i < _glintActive.Length; i++)
            {
                if (_glintActive[i])
                    continue;
                if (pick-- > 0)
                    continue;
                _glintActive[i] = true;
                _glintElapsed[i] = 0f;
                _glints[i].SetSprite(_frames[Sequence[0]]);
                _glints[i].Enabled = true;
                return;
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _glints.Length; i++)
            {
                _glintActive[i] = false;
                _glints[i].Enabled = false;
            }
        }
    }
}

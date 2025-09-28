using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using RolePlayingFramework.Equipment;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that manages treasure chest state and appearance
    /// </summary>
    public class TreasureComponent : Component
    {
        public enum TreasureState
        {
            CLOSED,
            OPEN
        }

        private TreasureState _state = TreasureState.CLOSED;
        private int _level = 1;
        private SpriteRenderer _baseRenderer;
        private SpriteRenderer _woodRenderer;
        private SpriteAtlas _actorsAtlas;
        private IItem? _containedItem;

        /// <summary>
        /// Current state of the treasure chest
        /// </summary>
        public TreasureState State 
        { 
            get => _state;
            set 
            {
                if (_state != value)
                {
                    _state = value;
                    UpdateSprites();
                }
            }
        }

        /// <summary>
        /// Treasure level (1-5) which determines the wood color
        /// </summary>
        public int Level 
        { 
            get => _level;
            set 
            {
                if (_level != value && value >= 1 && value <= 5)
                {
                    _level = value;
                    UpdateWoodColor();
                }
            }
        }

        /// <summary>
        /// Item contained within this treasure chest
        /// </summary>
        public IItem? ContainedItem 
        { 
            get => _containedItem;
            set => _containedItem = value;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            LoadAtlas();
            InitializeRenderers();
        }

        private void LoadAtlas()
        {
            try
            {
                _actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[TreasureComponent] Failed to load Actors.atlas: {ex.Message}");
            }
        }

        private void InitializeRenderers()
        {
            if (_actorsAtlas == null)
            {
                Debug.Warn("[TreasureComponent] Cannot initialize renderers without atlas");
                return;
            }

            // Create base renderer on the treasure base layer
            _baseRenderer = Entity.AddComponent(new SpriteRenderer());
            _baseRenderer.SetRenderLayer(GameConfig.RenderLayerTreasureBase);

            // Create wood renderer on the treasure wood layer
            _woodRenderer = Entity.AddComponent(new SpriteRenderer());
            _woodRenderer.SetRenderLayer(GameConfig.RenderLayerTreasureWood);

            UpdateSprites();
            UpdateWoodColor();
        }

        private void UpdateSprites()
        {
            if (_baseRenderer == null || _woodRenderer == null || _actorsAtlas == null)
                return;

            // Load appropriate sprites based on state
            if (_state == TreasureState.CLOSED)
            {
                _baseRenderer.Sprite = _actorsAtlas.GetSprite("treasure_base_closed");
                _woodRenderer.Sprite = _actorsAtlas.GetSprite("treasure_wood_closed");
            }
            else
            {
                _baseRenderer.Sprite = _actorsAtlas.GetSprite("treasure_base_open");
                _woodRenderer.Sprite = _actorsAtlas.GetSprite("treasure_wood_open");
            }
        }

        private void UpdateWoodColor()
        {
            if (_woodRenderer == null)
                return;

            // Set wood color based on treasure level
            _woodRenderer.Color = _level switch
            {
                1 => GameConfig.TREASURE_SHADE_1, // Brown - Common
                2 => GameConfig.TREASURE_SHADE_2, // Green - Special
                3 => GameConfig.TREASURE_SHADE_3, // Blue - Rare
                4 => GameConfig.TREASURE_SHADE_4, // Purple - Epic
                5 => GameConfig.TREASURE_SHADE_5, // Gold - Legendary
                _ => GameConfig.TREASURE_SHADE_1  // Default to brown
            };
        }

        /// <summary>
        /// Generate an appropriate item for the given treasure level
        /// </summary>
        public static IItem GenerateItemForTreasureLevel(int treasureLevel)
        {
            var rarity = RarityUtils.GetRarityForTreasureLevel(treasureLevel);
            
            // For now, create a simple bag item based on rarity
            // This can be expanded to generate different types of items
            return rarity switch
            {
                ItemRarity.Normal => BagItems.StandardBag(),
                ItemRarity.Uncommon => BagItems.ForagersBag(), 
                ItemRarity.Rare => BagItems.TravellersBag(),
                ItemRarity.Epic => BagItems.AdventurersBag(),
                ItemRarity.Legendary => BagItems.MerchantsBag(),
                _ => BagItems.StandardBag()
            };
        }

        /// <summary>
        /// Determine treasure level based on pit level using probability distribution
        /// </summary>
        public static int DetermineTreasureLevel(int pitLevel)
        {
            var random = Nez.Random.NextFloat();
            
            return pitLevel switch
            {
                // Pit Levels 1-10: Only Level 1 (100%)
                <= 10 => 1,
                
                // Pit Levels 10-30: Level 1 (80%), Level 2 (20%)
                <= 30 => random < 0.8f ? 1 : 2,
                
                // Pit Levels 30-60: Level 1 (70%), Level 2 (20%), Level 3 (10%)
                <= 60 => random switch
                {
                    < 0.7f => 1,
                    < 0.9f => 2,
                    _ => 3
                },
                
                // Pit Levels 60-90: Level 1 (55%), Level 2 (25%), Level 3 (15%), Level 4 (5%)
                <= 90 => random switch
                {
                    < 0.55f => 1,
                    < 0.8f => 2,
                    < 0.95f => 3,
                    _ => 4
                },
                
                // Pit Levels 90+: Level 1 (40%), Level 2 (30%), Level 3 (20%), Level 4 (9%), Level 5 (1%)
                _ => random switch
                {
                    < 0.4f => 1,
                    < 0.7f => 2,
                    < 0.9f => 3,
                    < 0.99f => 4,
                    _ => 5
                }
            };
        }

        /// <summary>
        /// Initialize this treasure chest for a specific pit level
        /// </summary>
        public void InitializeForPitLevel(int pitLevel)
        {
            Level = DetermineTreasureLevel(pitLevel);
            ContainedItem = GenerateItemForTreasureLevel(Level);
        }
    }
}
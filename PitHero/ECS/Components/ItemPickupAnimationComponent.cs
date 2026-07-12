using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using RolePlayingFramework.Equipment;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that animates an item pickup by making the sprite rise up one tile and then disappear
    /// </summary>
    public class ItemPickupAnimationComponent : Component, IUpdatable
    {
        private SpriteRenderer _renderer;
        private IItem _item;
        private readonly Sprite _explicitSprite;
        private readonly string _debugName;
        private Vector2 _startPosition;
        private float _animationDuration = 1.0f; // 1 second animation
        private float _elapsedTime;
        private bool _animationComplete = false;

        /// <summary>Creates an animation that loads the sprite from Items.atlas by item name.</summary>
        public ItemPickupAnimationComponent(IItem item)
        {
            _item = item;
        }

        /// <summary>Creates an animation using an explicitly provided sprite (e.g. from CropsProps.atlas).</summary>
        public ItemPickupAnimationComponent(Sprite sprite, string debugName)
        {
            _explicitSprite = sprite;
            _debugName = debugName ?? "pickup";
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _startPosition = Entity.Transform.Position;
            _elapsedTime = 0f;
            LoadItemSprite();
            string displayName = _item?.Name ?? _debugName ?? "?";
            Debug.Log($"[ItemPickupAnimation] Started pickup animation for {displayName} at position X: {_startPosition.X}, Y: {_startPosition.Y}");
        }

        private void LoadItemSprite()
        {
            // Use explicit sprite when provided (e.g. crop sprites from CropsProps.atlas)
            if (_explicitSprite != null)
            {
                _renderer = Entity.AddComponent(new SpriteRenderer(_explicitSprite));
                _renderer.SetRenderLayer(GameConfig.RenderLayerPickupItem);
                Debug.Log($"[ItemPickupAnimation] Using explicit sprite '{_debugName}'");
                return;
            }

            try
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                // SpriteName, not Name — tier-scaled gear display names carry a "+N" suffix
                // ("TatteredCloth+2") that is not an atlas key.
                var itemSprite = itemsAtlas?.GetSprite(_item.SpriteName);

                if (itemSprite != null)
                {
                    _renderer = Entity.AddComponent(new SpriteRenderer(itemSprite));
                    _renderer.SetRenderLayer(GameConfig.RenderLayerPickupItem);
                    Debug.Log($"[ItemPickupAnimation] Loaded sprite '{_item.SpriteName}' from Items.atlas");
                }
                else
                {
                    Debug.Warn($"[ItemPickupAnimation] Sprite '{_item.SpriteName}' not found in Items.atlas");
                    // Fallback to prototype renderer
                    var prototypeRenderer = Entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                    prototypeRenderer.Color = Color.Yellow; // Fallback color for missing sprites
                    prototypeRenderer.SetRenderLayer(GameConfig.RenderLayerPickupItem);
                    _renderer = prototypeRenderer; // Keep reference for alpha fading
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[ItemPickupAnimation] Failed to load Items.atlas: {ex.Message}");
                // Fallback to prototype renderer
                var prototypeRenderer = Entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                prototypeRenderer.Color = Color.Yellow;
                prototypeRenderer.SetRenderLayer(GameConfig.RenderLayerPickupItem);
                _renderer = prototypeRenderer; // Keep reference for alpha fading
            }
        }

        public void Update()
        {
            if (_animationComplete) return;

            _elapsedTime += Time.DeltaTime;
            var progress = _elapsedTime / _animationDuration;

            if (progress >= 1.0f)
            {
                // Animation complete
                _animationComplete = true;
                string displayName = _item?.Name ?? _debugName ?? "?";
                Debug.Log($"[ItemPickupAnimation] Animation complete for {displayName}, removing entity");
                Entity.Destroy();
                return;
            }

            // Update position using easing
            var easedProgress = EaseBackOut(progress);
            var yOffset = -GameConfig.TileSize * easedProgress;
            Entity.Transform.Position = _startPosition + new Vector2(0, yOffset);

            // Fade out in the last 20% of the animation
            if (progress > 0.8f && _renderer != null)
            {
                var fadeProgress = (progress - 0.8f) / 0.2f;
                var alpha = (byte)(255 * (1f - fadeProgress));
                _renderer.Color = new Color(_renderer.Color.R, _renderer.Color.G, _renderer.Color.B, alpha);
            }
        }

        /// <summary>
        /// Back-out easing function for smooth animation
        /// </summary>
        private float EaseBackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            return 1f + c3 * (t - 1f) * (t - 1f) * (t - 1f) + c1 * (t - 1f) * (t - 1f);
        }
    }
}
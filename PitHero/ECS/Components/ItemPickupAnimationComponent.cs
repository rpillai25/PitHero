using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Tweens;
using RolePlayingFramework.Equipment;
using System.Collections;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that animates an item pickup by making the sprite rise up one tile and then disappear
    /// </summary>
    public class ItemPickupAnimationComponent : Component
    {
        private SpriteRenderer _renderer;
        private IItem _item;
        private Vector2 _startPosition;
        private float _animationDuration = 1.0f; // 1 second animation
        
        public ItemPickupAnimationComponent(IItem item)
        {
            _item = item;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _startPosition = Entity.Transform.Position;
            LoadItemSprite();
            StartAnimation();
        }

        private void LoadItemSprite()
        {
            try
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var itemSprite = itemsAtlas?.GetSprite(_item.Name);
                
                if (itemSprite != null)
                {
                    _renderer = Entity.AddComponent(new SpriteRenderer(itemSprite));
                    _renderer.SetRenderLayer(GameConfig.RenderLayerItem);
                }
                else
                {
                    Debug.Warn($"[ItemPickupAnimation] Sprite '{_item.Name}' not found in Items.atlas");
                    // Fallback to prototype renderer
                    var prototypeRenderer = Entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                    prototypeRenderer.Color = Color.Yellow; // Fallback color for missing sprites
                    prototypeRenderer.SetRenderLayer(GameConfig.RenderLayerItem);
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[ItemPickupAnimation] Failed to load Items.atlas: {ex.Message}");
                // Fallback to prototype renderer
                var prototypeRenderer = Entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                prototypeRenderer.Color = Color.Yellow;
                prototypeRenderer.SetRenderLayer(GameConfig.RenderLayerItem);
            }
        }

        private void StartAnimation()
        {
            // Move up one tile (32 pixels) over the animation duration
            var targetPosition = _startPosition + new Vector2(0, -GameConfig.TileSize);
            
            // Create a coroutine to handle the animation
            Core.StartCoroutine(AnimatePickup(targetPosition));
        }

        private IEnumerator AnimatePickup(Vector2 targetPosition)
        {
            var tween = Entity.Transform.TweenPositionTo(targetPosition, _animationDuration)
                                        .SetEaseType(EaseType.BackOut);
            
            yield return tween.WaitForCompletion();
            
            // Animation complete, remove this entity
            Debug.Log($"[ItemPickupAnimation] Animation complete for {_item.Name}, removing entity");
            Entity.Destroy();
        }
    }
}
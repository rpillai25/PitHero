using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Dining;

namespace PitHero.Services
{
    /// <summary>
    /// Pre-created job-hat entities worn by kitchen monsters while they are actively doing
    /// kitchen work (ChefHat/ServerHat/CourierHat from CropsProps.atlas). On attach the hat
    /// entity's transform is parented to the worker entity's transform and positioned top-center
    /// on the head; on detach it is unparented, hidden, and returned to the pool.
    /// </summary>
    public class KitchenHatService
    {
        // 3 cooks + 2 servers + 2 runners
        private const int PoolSize = GameConfig.MaxKitchenCooks + GameConfig.MaxKitchenServers + GameConfig.MaxKitchenRunners;

        private Scene _scene;
        private SpriteAtlas _cropsAtlas;
        private readonly List<Entity> _pool = new List<Entity>(PoolSize);

        /// <summary>Provides the scene and pre-creates the hidden hat pool for it.</summary>
        public void SetScene(Scene scene)
        {
            _scene = scene;
            _pool.Clear(); // any previous entities belonged to the old scene
            for (int i = 0; i < PoolSize; i++)
            {
                var hat = _scene.CreateEntity("kitchen-hat-" + i);
                var renderer = hat.AddComponent(new SpriteRenderer());
                renderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
                hat.SetEnabled(false);
                _pool.Add(hat);
            }
        }

        private SpriteAtlas CropsAtlas
            => _cropsAtlas ??= Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

        private static string SpriteNameFor(KitchenRole role)
        {
            switch (role)
            {
                case KitchenRole.Cook:   return "ChefHat";
                case KitchenRole.Server: return "ServerHat";
                default:                 return "CourierHat";
            }
        }

        /// <summary>
        /// Puts a role hat on the worker: parents a pooled hat entity to the worker's transform,
        /// top-center where the head is. Returns null when no scene/sprite is available.
        /// </summary>
        public Entity AttachHat(KitchenRole role, Entity worker, SpriteRenderer bodyRenderer)
        {
            if (_scene == null || worker == null)
                return null;

            var sprite = CropsAtlas?.GetSprite(SpriteNameFor(role));
            if (sprite == null)
                return null;

            Entity hat = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].IsDestroyed && !_pool[i].Enabled)
                {
                    hat = _pool[i];
                    break;
                }
            }
            if (hat == null)
                return null; // pool exhausted (cannot happen at current staffing caps)

            var renderer = hat.GetComponent<SpriteRenderer>();
            renderer.Sprite = sprite;

            // Sit the hat on top of the head: body sprite is centered on the entity, so the head
            // top is at -bodyHeight/2; overlap pulls the brim down onto the head.
            float bodyHeight = bodyRenderer?.Sprite != null
                ? bodyRenderer.Sprite.SourceRect.Height
                : GameConfig.TileSize;
            float hatHeight = sprite.SourceRect.Height;
            float localY = -bodyHeight / 2f - hatHeight / 2f + GameConfig.KitchenHatOverlapPixels;

            // Draw just above the body (same convention as the carry sprite)
            if (bodyRenderer != null)
                renderer.SetLayerDepth(bodyRenderer.LayerDepth - 0.0001f);

            hat.Transform.SetParent(worker.Transform);
            hat.Transform.SetLocalPosition(new Vector2(0f, localY));
            hat.SetEnabled(true);
            return hat;
        }

        /// <summary>Takes the hat off: unparents, hides, and returns it to the pool. Null-safe.</summary>
        public void DetachHat(Entity hat)
        {
            if (hat == null || hat.IsDestroyed)
                return;
            hat.Transform.SetParent(null);
            hat.SetEnabled(false);
        }
    }
}

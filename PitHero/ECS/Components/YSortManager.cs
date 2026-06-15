using Nez;

namespace PitHero.ECS.Components
{
    public class YSortManager : SceneComponent
    {
        private static readonly int[] _ySortLayers =
        {
            GameConfig.RenderLayerActors,
            GameConfig.RenderLayerSingleTileObject
        };

        public override void Update()
        {
            var camera = Scene.Camera;

            foreach (var layer in _ySortLayers)
            {
                var renderables = Scene.RenderableComponents.ComponentsWithRenderLayer(layer);
                for (var i = 0; i < renderables.Length; i++)
                {
                    var renderable = renderables.Buffer[i] as RenderableComponent;
                    if (renderable == null || !renderable.Enabled) continue;
                    if (!camera.Bounds.Intersects(renderable.Bounds)) continue;

                    var depth = Mathf.Clamp01(1f - renderable.Entity.Transform.Position.Y
                                                   * GameConfig.YSortDepthScale);
                    renderable.SetLayerDepth(depth);
                }
            }
        }
    }
}

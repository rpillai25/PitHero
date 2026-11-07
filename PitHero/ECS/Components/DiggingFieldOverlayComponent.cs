using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Services.Replay.Frames;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Darkens each Digging Field tile that is resting after a dig (issue #452), read every frame from
    /// <see cref="DiggingFieldService"/>. Lives on the field's entity, whose position is the field's
    /// top-left corner, one render layer in front of the field sprite. Draws nothing it owns: the
    /// cooldowns are simulation state, so the overlay matches the sim at every tick.
    /// </summary>
    public class DiggingFieldOverlayComponent : RenderableComponent, IFrameCapturable
    {
        private DiggingFieldService _service;

        /// <summary>The whole field, so the camera culls it like the field sprite.</summary>
        public override float Width => BuildingConfig.DiggingFieldWidth * GameConfig.TileSize;

        /// <summary>The whole field, so the camera culls it like the field sprite.</summary>
        public override float Height => BuildingConfig.DiggingFieldHeight * GameConfig.TileSize;

        /// <summary>Bounds start at the entity (the field's top-left), not centered on it.</summary>
        public override RectangleF Bounds
        {
            get
            {
                var p = Entity.Transform.Position;
                return new RectangleF(p.X, p.Y, Width, Height);
            }
        }

        private DiggingFieldService Service =>
            _service ??= Core.Instance != null ? Core.Services.GetService<DiggingFieldService>() : null;

        /// <summary>Fills each resting tile with the cooldown overlay color.</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            var svc = Service;
            if (svc == null)
                return;
            var origin = Entity.Transform.Position;
            int size = GameConfig.TileSize;
            var color = GameConfig.DiggingFieldCooldownOverlayColor;
            for (int i = 0; i < DiggingFieldService.TileCount; i++)
            {
                if (!svc.IsCooling(i))
                    continue;
                float x = origin.X + (i % BuildingConfig.DiggingFieldWidth) * size;
                float y = origin.Y + (i / BuildingConfig.DiggingFieldWidth) * size;
                batcher.DrawRect(x, y, size, size, color);
            }
        }

        /// <summary>Replay frame: one filled world-space Rect op per resting tile.</summary>
        public void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx)
        {
            var svc = Service;
            if (svc == null)
                return;
            var origin = Entity.Transform.Position;
            int size = GameConfig.TileSize;
            uint color = GameConfig.DiggingFieldCooldownOverlayColor.PackedValue;
            for (int i = 0; i < DiggingFieldService.TileCount; i++)
            {
                if (!svc.IsCooling(i))
                    continue;
                w.WriteRect(origin.X + (i % BuildingConfig.DiggingFieldWidth) * size,
                    origin.Y + (i / BuildingConfig.DiggingFieldWidth) * size, size, size, color, FrameOpFlags.None);
            }
        }
    }
}

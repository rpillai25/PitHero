using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Hides a renderable while the fog-of-war tile over its entity is covered and reveals it when
    /// that tile is uncovered. Attach to static pit entities (treasure chests, walls, wizard orb)
    /// so they don't show through fog now that actors render above the FogOfWar layer.
    /// Reveal is driven by <see cref="TiledMapService.RefreshFogHiddenEntities"/> after any fog clear.
    /// </summary>
    public class FogHideableComponent : Component
    {
        // Static registry maintained by OnAddedToEntity/OnRemovedFromEntity so the fog-clear path can
        // iterate without calling FindEntitiesWithTag (which allocates a new list every invocation).

        /// <summary>All live FogHideableComponent instances in the current scene.</summary>
        public static readonly List<FogHideableComponent> Active = new List<FogHideableComponent>(64);

        private readonly RenderableComponent _target;
        private Point _tile; // cached — targets are static entities that never change tiles
        private bool _hiddenByFog;

        /// <summary>Creates the component for the renderable that should be fog-gated.</summary>
        public FogHideableComponent(RenderableComponent target)
        {
            _target = target;
        }

        /// <summary>Registers in the static registry and hides the target if its tile is fogged.</summary>
        public override void OnAddedToEntity()
        {
            Active.Add(this);

            var pos = Entity.Transform.Position;
            _tile = new Point(
                (int)System.Math.Floor(pos.X / GameConfig.TileSize),
                (int)System.Math.Floor(pos.Y / GameConfig.TileSize));

            var tms = Core.Services?.GetService<TiledMapService>();
            if (tms != null && tms.IsFogOfWarTile(_tile.X, _tile.Y))
            {
                _target?.SetEnabled(false);
                _hiddenByFog = true;
            }
        }

        /// <summary>Removes this component from the static registry.</summary>
        public override void OnRemovedFromEntity()
        {
            // Backward search so the common case (last added = first removed) is O(1)
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] == this)
                {
                    Active.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>Re-enables all fog-hidden targets whose tile is no longer fogged.</summary>
        public static void RevealUnfogged(TiledMapService tms)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var hideable = Active[i];
                if (!hideable._hiddenByFog)
                    continue;
                if (!tms.IsFogOfWarTile(hideable._tile.X, hideable._tile.Y))
                {
                    hideable._target?.SetEnabled(true);
                    hideable._hiddenByFog = false;
                }
            }
        }
    }
}

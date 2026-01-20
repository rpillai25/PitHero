using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.AI
{
    /// <summary>
    /// Action that makes the mercenary follow their target using A* pathfinding.
    /// Completes when mercenary reaches the same tile as target.
    /// Will be interrupted by state machine if target's location changes unexpectedly.
    /// </summary>
    public class FollowTargetAction : MercenaryActionBase
    {
        public FollowTargetAction() : base(GoapConstants.FollowTargetAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);

            SetPostcondition(GoapConstants.MercenaryFollowingTarget, true);
        }

        public override bool Execute(MercenaryComponent mercenary)
        {
            if (mercenary?.FollowTarget == null)
                return true;

            var mercTile = new Microsoft.Xna.Framework.Point(
                (int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize)
            );

            Microsoft.Xna.Framework.Point targetTile;
            var targetHero = mercenary.FollowTarget.GetComponent<HeroComponent>();
            if (targetHero != null)
            {
                targetTile = new Microsoft.Xna.Framework.Point(
                    (int)(targetHero.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(targetHero.Entity.Transform.Position.Y / GameConfig.TileSize)
                );
            }
            else
            {
                var targetMerc = mercenary.FollowTarget.GetComponent<MercenaryComponent>();
                if (targetMerc != null)
                {
                    targetTile = new Microsoft.Xna.Framework.Point(
                        (int)(targetMerc.Entity.Transform.Position.X / GameConfig.TileSize),
                        (int)(targetMerc.Entity.Transform.Position.Y / GameConfig.TileSize)
                    );
                }
                else
                {
                    return true;
                }
            }

            if (mercTile.X == targetTile.X && mercTile.Y == targetTile.Y)
            {
                return true;
            }

            // Set up following behavior
            var followComponent = mercenary.Entity.GetComponent<MercenaryFollowComponent>();
            if (followComponent == null)
            {
                Nez.Debug.Log($"[FollowTargetAction] Adding MercenaryFollowComponent to {mercenary.Entity.Name}");
                followComponent = mercenary.Entity.AddComponent(new MercenaryFollowComponent());
            }

            // Following action is continuous - never completes until reaching the target tile
            // The state machine will interrupt this action if world state changes unexpectedly
            return false;
        }
    }
}


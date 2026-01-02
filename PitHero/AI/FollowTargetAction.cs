using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.AI
{
    /// <summary>
    /// Action that makes the mercenary follow their target using A* pathfinding.
    /// Completes when mercenary reaches the same location as target (both in pit or both outside).
    /// Will be interrupted by state machine if target's location changes unexpectedly.
    /// </summary>
    public class FollowTargetAction : MercenaryActionBase
    {
        public FollowTargetAction() : base(GoapConstants.FollowTargetAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            // Both must be in same location (both in pit or both out of pit)
            // This is implicitly handled by the goal state wanting MercenaryInsidePit to match TargetInsidePit

            SetPostcondition(GoapConstants.MercenaryFollowingTarget, true);
        }

        public override bool Execute(MercenaryComponent mercenary)
        {
            // Check if mercenary and target are in the same location (both in pit or both outside pit)
            bool mercInPit = IsMercenaryInsidePit(mercenary);
            bool targetInPit = IsTargetInsidePit(mercenary);

            if (mercInPit != targetInPit)
            {
                // Not in same location - this action cannot execute, complete immediately
                // so the state machine can proceed to WalkToPitEdgeAction or MercenaryJumpIntoPitAction
                Nez.Debug.Log($"[FollowTargetAction] {mercenary.Entity.Name} not in same location as target (merc in pit={mercInPit}, target in pit={targetInPit}), completing action");
                return true;
            }

            // In same location - set up following behavior
            var followComponent = mercenary.Entity.GetComponent<MercenaryFollowComponent>();
            if (followComponent == null)
            {
                Nez.Debug.Log($"[FollowTargetAction] Adding MercenaryFollowComponent to {mercenary.Entity.Name}");
                followComponent = mercenary.Entity.AddComponent(new MercenaryFollowComponent());
            }

            // Following action is continuous - never completes while in same location
            // The state machine will interrupt this action if world state changes unexpectedly
            return false;
        }

        private bool IsMercenaryInsidePit(MercenaryComponent mercenary)
        {
            var currentTile = new Microsoft.Xna.Framework.Point(
                (int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize)
            );

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return false;

            var pitLeft = GameConfig.PitRectX;
            var pitTop = GameConfig.PitRectY;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitHeight = GameConfig.PitRectHeight;
            var pitRight = pitLeft + pitWidth - 1;
            var pitBottom = pitTop + pitHeight - 1;

            // Use exclusive boundaries - pit edge is NOT inside the pit
            return currentTile.X > pitLeft && currentTile.X < pitRight && 
                   currentTile.Y > pitTop && currentTile.Y < pitBottom;
        }

        private bool IsTargetInsidePit(MercenaryComponent mercenary)
        {
            if (mercenary?.FollowTarget == null)
                return false;

            var targetHero = mercenary.FollowTarget.GetComponent<HeroComponent>();
            if (targetHero != null)
            {
                return targetHero.InsidePit;
            }

            var targetMerc = mercenary.FollowTarget.GetComponent<MercenaryComponent>();
            if (targetMerc != null)
            {
                var targetTile = new Microsoft.Xna.Framework.Point(
                    (int)(targetMerc.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(targetMerc.Entity.Transform.Position.Y / GameConfig.TileSize)
                );

                var pitWidthManager = Core.Services.GetService<PitWidthManager>();
                if (pitWidthManager == null)
                    return false;

                var pitLeft = GameConfig.PitRectX;
                var pitTop = GameConfig.PitRectY;
                var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
                var pitHeight = GameConfig.PitRectHeight;
                var pitRight = pitLeft + pitWidth - 1;
                var pitBottom = pitTop + pitHeight - 1;

                // Use exclusive boundaries - pit edge is NOT inside the pit
                return targetTile.X > pitLeft && targetTile.X < pitRight && 
                       targetTile.Y > pitTop && targetTile.Y < pitBottom;
            }

            return false;
        }
    }
}


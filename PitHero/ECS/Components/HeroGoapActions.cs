using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// GOAP Action: Move from center to adjacent to pit
    /// </summary>
    public class MoveToPitAction : HeroActionBase
    {
        public MoveToPitAction() : base("MoveToPit", 2)
        {
            // Precondition: Hero is at center
            SetPrecondition("IsAtCenter", true);
            
            // Effect: Hero is adjacent to pit
            SetPostcondition("IsAtCenter", false);
            SetPostcondition("IsAdjacentToPit", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            var pitCenterPosition = GetPitCenterWorldPosition();
            
            // Move towards the pit center (which should trigger adjacency)
            var moved = MoveTowards(hero, pitCenterPosition, Time.DeltaTime);
            
            if (moved || hero.IsAdjacentToPit)
            {
                hero.IsAtCenter = false;
                return true; // Action completed
            }
            
            return false; // Still moving
        }
    }

    /// <summary>
    /// GOAP Action: Jump into the pit
    /// </summary>
    public class JumpIntoPitAction : HeroActionBase
    {
        public JumpIntoPitAction() : base("JumpIntoPit", 1)
        {
            // Precondition: Hero is adjacent to pit
            SetPrecondition("IsAdjacentToPit", true);
            
            // Effect: Hero is inside pit
            SetPostcondition("IsAdjacentToPit", false);
            SetPostcondition("IsInsidePit", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (!hero.IsAdjacentToPit)
                return false;

            // Calculate jump destination (towards pit center)
            var currentPosition = hero.Entity.Transform.Position;
            var pitCenter = GetPitCenterWorldPosition();
            var jumpDirection = Vector2.Normalize(pitCenter - currentPosition);
            var jumpDistance = 96f; // Jump over collision tile (1.5 tiles)
            
            var jumpTarget = currentPosition + jumpDirection * jumpDistance;
            
            // Perform the jump (instant teleport for simplicity)
            hero.Entity.Transform.Position = jumpTarget;
            
            // Record milestone
            var historian = hero.Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            
            return true; // Action completed
        }
    }

    /// <summary>
    /// GOAP Action: Jump out of the pit
    /// </summary>
    public class JumpOutOfPitAction : HeroActionBase
    {
        public JumpOutOfPitAction() : base("JumpOutOfPit", 1)
        {
            // Precondition: Hero is inside pit
            SetPrecondition("IsInsidePit", true);
            
            // Effect: Hero is adjacent to pit and just jumped out
            SetPostcondition("IsInsidePit", false);
            SetPostcondition("JustJumpedOutOfPit", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (!hero.IsInsidePit)
                return false;

            // Jump to an adjacent tile outside the collision rectangle
            var currentPosition = hero.Entity.Transform.Position;
            var mapCenter = GetMapCenterWorldPosition();
            var jumpDirection = Vector2.Normalize(mapCenter - currentPosition);
            var jumpDistance = 128f; // Jump outside pit collision area (2 tiles)
            
            var jumpTarget = currentPosition + jumpDirection * jumpDistance;
            
            // Perform the jump (instant teleport for simplicity)
            hero.Entity.Transform.Position = jumpTarget;
            
            hero.JustJumpedOutOfPit = true;
            
            // Record milestone
            var historian = hero.Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
            
            return true; // Action completed
        }
    }

    /// <summary>
    /// GOAP Action: Move from pit area back to center
    /// </summary>
    public class MoveToCenterAction : HeroActionBase
    {
        public MoveToCenterAction() : base("MoveToCenter", 2)
        {
            // Precondition: Just jumped out of pit
            SetPrecondition("JustJumpedOutOfPit", true);
            
            // Effect: Hero is at center
            SetPostcondition("JustJumpedOutOfPit", false);
            SetPostcondition("IsAtCenter", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            var centerPosition = GetMapCenterWorldPosition();
            
            // Move towards the center
            var moved = MoveTowards(hero, centerPosition, Time.DeltaTime);
            
            if (moved)
            {
                hero.IsAtCenter = true;
                hero.JustJumpedOutOfPit = false;
                
                // Record milestone
                var historian = hero.Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.ReturnedToCenter, Time.TotalTime);
                
                return true; // Action completed
            }
            
            return false; // Still moving
        }
    }
}
using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    public class MoveToPitAction : HeroActionBase
    {
        public MoveToPitAction() : base("MoveToPit", 2)
        {
            // Removed strict IsAtCenter precondition so we can approach pit from anywhere not already adjacent/inside
            SetPrecondition("IsInsidePit", false);
            SetPrecondition("IsAdjacentToPit", false);
            SetPrecondition("IsAtCenter", true);
            SetPrecondition("JustOut", false);

            SetPostcondition("IsAdjacentToPit", true);
            SetPostcondition("IsAtCenter", false);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (hero.IsInsidePit) return true; // Shouldn't happen due to preconditions; fail-safe
            
            var pit = GetPitCenterWorldPosition();
            var done = MoveTowards(hero, pit, Time.DeltaTime);

            // Acquire adjacency using tile helper (more exact)
            if (!hero.IsAdjacentToPit && hero.CheckAdjacentToPit(hero.Entity.Transform.Position))
                hero.IsAdjacentToPit = true;

            if (hero.IsAdjacentToPit || done)
            {
                hero.IsAtCenter = false;
                return true;
            }
            return false;
        }
    }

    public class JumpIntoPitAction : HeroActionBase
    {
        public JumpIntoPitAction() : base("JumpIntoPit", 1)
        {
            SetPrecondition("IsAdjacentToPit", true);
            SetPostcondition("IsAdjacentToPit", false);
            SetPostcondition("IsInsidePit", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (!hero.IsAdjacentToPit)
                return false;

            var currentPosition = hero.Entity.Transform.Position;
            var pitCenter = GetPitCenterWorldPosition();
            var dir = Vector2.Normalize(pitCenter - currentPosition);
            var jumpDistance = 96f;

            hero.Entity.Transform.Position = currentPosition + dir * jumpDistance;
            hero.IsInsidePit = true;

            hero.Entity.GetComponent<Historian>()?
                .RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);

            return true;
        }
    }

    public class JumpOutOfPitAction : HeroActionBase
    {
        public JumpOutOfPitAction() : base("JumpOutOfPit", 1)
        {
            SetPrecondition("IsInsidePit", true);
            SetPostcondition("IsInsidePit", false);
            SetPostcondition("JustJumpedOutOfPit", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (!hero.IsInsidePit)
                return false;

            var current = hero.Entity.Transform.Position;
            var mapCenter = GetMapCenterWorldPosition();
            var dir = Vector2.Normalize(mapCenter - current);
            var jumpDistance = 128f;

            hero.Entity.Transform.Position = current + dir * jumpDistance;
            hero.IsInsidePit = false;
            hero.JustJumpedOutOfPit = true;

            hero.Entity.GetComponent<Historian>()?
                .RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);

            return true;
        }
    }

    public class MoveToCenterAction : HeroActionBase
    {
        public MoveToCenterAction() : base("MoveToCenter", 2)
        {
            SetPrecondition("JustJumpedOutOfPit", true);
            SetPostcondition("JustJumpedOutOfPit", false);
            SetPostcondition("IsAtCenter", true);
        }

        public override bool Execute(HeroComponent hero)
        {
            var center = GetMapCenterWorldPosition();
            var reached = MoveTowards(hero, center, Time.DeltaTime);

            if (Vector2.Distance(hero.Entity.Transform.Position, center) <= 12f)
                reached = true;

            if (reached)
            {
                hero.IsAtCenter = true;
                hero.JustJumpedOutOfPit = false;
                hero.Entity.GetComponent<Historian>()?
                    .RecordMilestone(MilestoneType.ReturnedToCenter, Time.TotalTime);
                return true;
            }

            return false;
        }
    }
}
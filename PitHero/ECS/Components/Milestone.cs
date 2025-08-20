using System;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Represents a milestone achieved by a hero
    /// </summary>
    public class Milestone
    {
        public MilestoneType Type { get; }
        public DateTime Timestamp { get; }
        public double GameTime { get; }
        public string Description { get; }

        public Milestone(MilestoneType type, double gameTime, string description = null)
        {
            Type = type;
            GameTime = gameTime;
            Timestamp = DateTime.UtcNow;
            Description = description ?? GetDefaultDescription(type);
        }

        private string GetDefaultDescription(MilestoneType type)
        {
            return type switch
            {
                MilestoneType.FirstJumpIntoPit => "Hero jumped into the pit for the first time",
                MilestoneType.FirstTreasureFound => "Hero found their first treasure",
                MilestoneType.FirstJumpOutOfPit => "Hero jumped out of the pit for the first time",
                MilestoneType.ReturnedToCenter => "Hero returned to the center",
                _ => $"Milestone achieved: {type}"
            };
        }
    }
}
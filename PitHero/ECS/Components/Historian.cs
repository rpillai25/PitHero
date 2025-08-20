using System.Collections.Generic;
using System.Linq;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that tracks a hero's milestone achievements
    /// </summary>
    public class Historian : Component
    {
        private readonly List<Milestone> _milestones;
        private readonly object _lock = new object();

        public Historian()
        {
            _milestones = new List<Milestone>();
        }

        /// <summary>
        /// Record a milestone for this hero
        /// </summary>
        public void RecordMilestone(MilestoneType type, double gameTime, string description = null)
        {
            lock (_lock)
            {
                // Check if this milestone type has already been achieved
                if (!_milestones.Any(m => m.Type == type))
                {
                    var milestone = new Milestone(type, gameTime, description);
                    _milestones.Add(milestone);
                }
            }
        }

        /// <summary>
        /// Get all milestones achieved by this hero
        /// </summary>
        public IReadOnlyList<Milestone> GetMilestones()
        {
            lock (_lock)
            {
                return _milestones.OrderBy(m => m.GameTime).ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Check if a specific milestone has been achieved
        /// </summary>
        public bool HasAchievedMilestone(MilestoneType type)
        {
            lock (_lock)
            {
                return _milestones.Any(m => m.Type == type);
            }
        }

        /// <summary>
        /// Get the number of milestones achieved
        /// </summary>
        public int MilestoneCount
        {
            get
            {
                lock (_lock)
                {
                    return _milestones.Count;
                }
            }
        }

        /// <summary>
        /// Generate a summary of hero's milestones for display on death
        /// </summary>
        public string GenerateSummary()
        {
            lock (_lock)
            {
                if (_milestones.Count == 0)
                {
                    return "Hero achieved no milestones during their journey.";
                }

                var summary = $"Hero achieved {_milestones.Count} milestones:\n";
                foreach (var milestone in _milestones.OrderBy(m => m.GameTime))
                {
                    summary += $"- {milestone.Description}\n";
                }
                return summary;
            }
        }
    }
}
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using PitHero.AI.Interfaces;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual pit level manager that implements IPitLevelManager without Nez dependencies
    /// </summary>
    public class VirtualPitLevelManager : IPitLevelManager
    {
        private readonly VirtualWorldState _worldState;
        private readonly Queue<int> _levelQueue = new Queue<int>();

        public VirtualPitLevelManager(VirtualWorldState worldState)
        {
            _worldState = worldState;
        }

        public int CurrentLevel => _worldState.PitLevel;

        public bool HasQueuedLevel => _levelQueue.Count > 0;

        public void QueueLevel(int level)
        {
            _levelQueue.Enqueue(level);
            System.Console.WriteLine($"[VirtualPitLevelManager] Queued pit level {level}");
        }

        public int? DequeueLevel()
        {
            if (_levelQueue.Count == 0)
                return null;

            var level = _levelQueue.Dequeue();
            System.Console.WriteLine($"[VirtualPitLevelManager] Dequeued pit level {level}");
            return level;
        }

        public void RegeneratePit(int level, Point? heroPosition = null)
        {
            _worldState.RegeneratePit(level);
            if (heroPosition.HasValue)
            {
                _worldState.MoveHeroTo(heroPosition.Value);
            }
            System.Console.WriteLine($"[VirtualPitLevelManager] Regenerated pit at level {level}");
        }
    }
}
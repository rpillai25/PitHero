using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.AI
{
    public class GoapConstants
    {
        // State names (Conditions)
        public const string HeroInitialized = "HeroInitialized";
        public const string PitInitialized = "PitInitialized";
        public const string MovingLeft = "MovingLeft";
        public const string MovingToPit = "MovingToPit";
        public const string AdjacentToPitBoundaryFromOutside = "AdjacentToPitBoundaryFromOutside";
        public const string AdjacentToPitBoundaryFromInside = "AdjacentToPitBoundaryFromInside";
        public const string EnteredPit = "EnteredPit";

        // Action names
        public const string MoveLeftAction = "MoveLeftAction";
        public const string MoveToPitAction = "MoveToPitAction";
        public const string JumpIntoPitAction = "JumpIntoPitAction";
    }
}

namespace PitHero.AI
{
    public class GoapConstants
    {
        // State names (Conditions)
        public const string HeroInitialized = "HeroInitialized";
        public const string PitInitialized = "PitInitialized";
        public const string MovingToPit = "MovingToPit";
        public const string AdjacentToPitBoundaryFromOutside = "AdjacentToPitBoundaryFromOutside";
        public const string AdjacentToPitBoundaryFromInside = "AdjacentToPitBoundaryFromInside";
        public const string EnteredPit = "EnteredPit";

        // Action names
        public const string MoveToPitAction = "MoveToPitAction";
        public const string JumpIntoPitAction = "JumpIntoPitAction";
    }
}

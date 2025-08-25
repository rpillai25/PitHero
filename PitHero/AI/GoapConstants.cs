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
        public const string InsidePit = "InsidePit";
        public const string MapExplored = "MapExplored";
        
        // New states for wizard orb and pit regeneration workflow
        public const string OutsidePit = "OutsidePit";
        public const string FoundWizardOrb = "FoundWizardOrb";
        public const string AtWizardOrb = "AtWizardOrb";
        public const string ActivatedWizardOrb = "ActivatedWizardOrb";
        public const string MovingToInsidePitEdge = "MovingToInsidePitEdge";
        public const string ReadyToJumpOutOfPit = "ReadyToJumpOutOfPit";
        public const string AtPitGenPoint = "AtPitGenPoint";
        public const string MovingToPitGenPoint = "MovingToPitGenPoint";

        // Action names
        public const string MoveToPitAction = "MoveToPitAction";
        public const string JumpIntoPitAction = "JumpIntoPitAction";
        public const string WanderAction = "WanderAction";
        
        // New action names for wizard orb and pit regeneration workflow
        public const string MoveToWizardOrbAction = "MoveToWizardOrbAction";
        public const string ActivateWizardOrbAction = "ActivateWizardOrbAction";
        public const string MovingToInsidePitEdgeAction = "MovingToInsidePitEdgeAction";
        public const string JumpOutOfPitAction = "JumpOutOfPitAction";
        public const string MoveToPitGenPointAction = "MoveToPitGenPointAction";
    }
}

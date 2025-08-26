namespace PitHero.AI
{
    public class GoapConstants
    {
        // GOAP States (only these 7 states should exist)
        public const string HeroInitialized = "HeroInitialized";
        public const string PitInitialized = "PitInitialized";
        public const string InsidePit = "InsidePit";
        public const string OutsidePit = "OutsidePit";
        public const string ExploredPit = "ExploredPit";
        public const string FoundWizardOrb = "FoundWizardOrb";
        public const string ActivatedWizardOrb = "ActivatedWizardOrb";

        // GOAP Actions (only these 5 actions should exist)
        public const string JumpIntoPitAction = "JumpIntoPitAction";
        public const string WanderAction = "WanderAction";
        public const string ActivateWizardOrbAction = "ActivateWizardOrbAction";
        public const string JumpOutOfPitAction = "JumpOutOfPitAction";
        public const string ActivatePitRegenAction = "ActivatePitRegenAction";
    }
}

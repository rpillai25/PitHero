namespace PitHero.AI
{
    public class GoapConstants
    {
        // GOAP States (extended to include interactive entities)
        public const string HeroInitialized = "HeroInitialized";
        public const string PitInitialized = "PitInitialized";
        public const string InsidePit = "InsidePit";
        public const string OutsidePit = "OutsidePit";
        public const string ExploredPit = "ExploredPit";
        public const string FoundWizardOrb = "FoundWizardOrb";
        public const string ActivatedWizardOrb = "ActivatedWizardOrb";
        public const string AdjacentToMonster = "AdjacentToMonster";
        public const string AdjacentToChest = "AdjacentToChest";
        public const string HPCritical = "HPCritical";

        // Mercenary GOAP States
        public const string MercenaryInsidePit = "MercenaryInsidePit";
        public const string TargetInsidePit = "TargetInsidePit";
        public const string MercenaryFollowingTarget = "MercenaryFollowingTarget";
        public const string MercenaryAtPitEdge = "MercenaryAtPitEdge";

        // GOAP Actions (extended to include interactive entities)
        public const string JumpIntoPitAction = "JumpIntoPitAction";
        public const string WanderPitAction = "WanderPitAction";
        public const string ActivateWizardOrbAction = "ActivateWizardOrbAction";
        public const string JumpOutOfPitAction = "JumpOutOfPitAction";
        public const string AttackMonster = "AttackMonster";
        public const string OpenChest = "OpenChest";
        public const string SleepInBedAction = "SleepInBedAction";

        // Mercenary GOAP Actions
        public const string FollowTargetAction = "FollowTargetAction";
        public const string MercenaryJumpIntoPitAction = "MercenaryJumpIntoPitAction";
        public const string MercenaryJumpOutOfPitAction = "MercenaryJumpOutOfPitAction";
        public const string WalkToPitEdgeAction = "WalkToPitEdgeAction";
    }
}

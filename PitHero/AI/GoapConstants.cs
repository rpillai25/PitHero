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
        public const string HealingItemExhausted = "HealingItemExhausted";
        public const string HealingSkillExhausted = "HealingSkillExhausted";
        public const string InnExhausted = "InnExhausted";
        public const string IsAlive = "IsAlive";

        // Mercenary GOAP States
        public const string MercenaryInsidePit = "MercenaryInsidePit";
        public const string TargetInsidePit = "TargetInsidePit";
        public const string MercenaryFollowingTarget = "MercenaryFollowingTarget";
        public const string MercenaryAtPitEdge = "MercenaryAtPitEdge";
        public const string IsBeingPromotedToHero = "IsBeingPromotedToHero";
        public const string HasArrivedAtHeroStatue = "HasArrivedAtHeroStatue";

        // GOAP Actions (extended to include interactive entities)
        public const string JumpIntoPitAction = "JumpIntoPitAction";
        public const string WanderPitAction = "WanderPitAction";
        public const string ActivateWizardOrbAction = "ActivateWizardOrbAction";
        public const string JumpOutOfPitAction = "JumpOutOfPitAction";
        public const string AttackMonster = "AttackMonster";
        public const string OpenChest = "OpenChest";
        public const string SleepInBedAction = "SleepInBedAction";
        public const string UseHealingItemAction = "UseHealingItemAction";
        public const string UseHealingSkillAction = "UseHealingSkillAction";
        public const string JumpOutOfPitForInnAction = "JumpOutOfPitForInnAction";

        // Mercenary GOAP Actions
        public const string FollowTargetAction = "FollowTargetAction";
        public const string MercenaryJumpIntoPitAction = "MercenaryJumpIntoPitAction";
        public const string MercenaryJumpOutOfPitAction = "MercenaryJumpOutOfPitAction";
        public const string WalkToPitEdgeAction = "WalkToPitEdgeAction";
        public const string WalkToHeroStatueAction = "WalkToHeroStatueAction";
    }
}

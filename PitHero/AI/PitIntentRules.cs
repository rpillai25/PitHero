namespace PitHero.AI
{
    /// <summary>
    /// Pure rules for combining the hero's pit-transition intent with the authoritative
    /// InsidePit flag. Kept side-effect free so the logic is unit-testable.
    /// </summary>
    public static class PitIntentRules
    {
        /// <summary>
        /// The pit state the hero is heading toward: an active intent overrides the actual
        /// flag; with no intent the actual flag stands.
        /// </summary>
        public static bool EffectiveInsidePit(HeroPitIntent intent, bool actualInsidePit)
        {
            switch (intent)
            {
                case HeroPitIntent.EnteringPit:
                    return true;
                case HeroPitIntent.ExitingPit:
                    return false;
                default:
                    return actualInsidePit;
            }
        }

        /// <summary>
        /// Clears an intent once the InsidePit flag reaches the intended state; an intent
        /// whose destination hasn't been reached yet is left untouched.
        /// </summary>
        public static HeroPitIntent Settle(HeroPitIntent intent, bool newInsidePit)
        {
            if (intent == HeroPitIntent.EnteringPit && newInsidePit)
                return HeroPitIntent.None;
            if (intent == HeroPitIntent.ExitingPit && !newInsidePit)
                return HeroPitIntent.None;
            return intent;
        }
    }
}

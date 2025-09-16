using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.BattleActors
{
    /// <summary>
    /// Instance of an actor for use in battle.  Can be a character or enemy.
    /// </summary>
    public class BattleActor
    {
        public BattleActorData BattleActorData;

        /// <summary>
        /// Flags of all mostly temporary status effects on actor (Flags)
        /// Any persistent effects have to be copied to character prior to battle end.
        /// </summary>
        public StatusEffect StatusEffects;
    }
}

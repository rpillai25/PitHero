using PitHero.RolePlayingSystem.GameData;
using System.Collections.Generic;

namespace PitHero.RolePlayingSystem.Abilities
{
    public class Ability
    {
        public string Name;
        public int AttackPower;
        public int MPCost;
        /// <summary>
        /// -1 means ability always hits
        /// </summary>
        public int HitPercent;
        /// <summary>
        /// Attack Element(s) (Flags)
        /// </summary>
        public int AttackElement;
        /// <summary>
        /// StatusEffects inflicted (Flags)
        /// </summary>
        public int StatusEffects;
        /// <summary>
        /// # of turns the status is in effect.
        /// -1 = always remain until dispelled
        /// 0 = n/a        
        /// </summary>
        public int StatusEffectsDuration;
        public bool IgnoreTargetStatusDurationModifiers;
        public TargettingType TargettingType;
        public bool Reflectable;
        public AttackType AttackType;
    }
}

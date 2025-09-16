using PitHero.RolePlayingSystem.BattleActors.Characters;
using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.Items.Weapons
{
    public class Weapon : Item
    {
        public int Attack;
        /// <summary>
        /// Hit percentage
        /// </summary>
        public int HitPercent;
        /// <summary>
        /// Attack power when thrown
        /// </summary>
        public int ThrowAttack;
        
        /// <summary>
        /// Determines damage formula
        /// </summary>
        public AttackType AttackType;

        /// <summary>
        /// Critical hit rate
        /// </summary>
        public int CriticalPercent;

        /// <summary>
        /// Whether weapon can be thrown
        /// </summary>
        public bool Throwable;

        /// <summary>
        /// Bonuses to stats
        /// </summary>
        public IStatContainer StatBonuses;

        /// <summary>
        /// Category of attack
        /// </summary>
        public AttackCategory AttackCategory;

        /// <summary>
        /// Magic damage bonus for this element (Flags)
        /// </summary>
        public Element MagicElementUp;

        /// <summary>
        /// Strong against this type of enemy (Flags)
        /// </summary>
        public EnemyType StrongVs;

        /// <summary>
        /// Element associated with weapon (Flags)
        /// </summary>
        public Element AttackElement;

        /// <summary>
        /// Special characteristic of weapon (Flags)
        /// </summary>
        public WeaponSpecial WeaponSpecial;

        /// <summary>
        /// Spell or command that is invoked at given rate
        /// </summary>
        public WeaponAbility WeaponAbility;

        public WeaponStatusEffect StatusEffectInflicted;

        public WeaponItemEffect WeaponItemEffect;
        /// <summary>
        /// Vocations that can equip this weapon.  (Flags)
        /// </summary>
        public VocationType EquipVocations;
    }
}

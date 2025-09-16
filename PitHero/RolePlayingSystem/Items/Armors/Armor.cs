using PitHero.RolePlayingSystem.BattleActors.Characters;
using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.Items.Armors
{
    public class Armor : Item
    {
        public ArmorCatalog ArmorType;

        public int Defense;
        public int MagicDefense;
        public int EvadePercent;
        public int MagicEvadePercent;
        public int Weight;

        /// <summary>
        /// Bonuses to stats
        /// </summary>
        public IStatContainer StatBonuses;
        /// <summary>
        /// Elements that heal instead of damage (Flags)
        /// </summary>
        public Element ElementAbsorb;

        /// <summary>
        /// Elemens that never hit (Flags)
        /// </summary>
        public Element ElementImmunity;

        /// <summary>
        /// Elements do half damage (Flags)
        /// </summary>
        public Element ElementHalf;

        /// <summary>
        /// Elements do double damage and bypass Defense/Magic Defense (Flags)
        /// </summary>
        public Element ElementWeakness;

        /// <summary>
        /// Initial Status effects (Flags)
        /// </summary>
        public StatusEffect InitialStatusEffects;

        /// <summary>
        /// Status effects on armor that can never be dispelled (Flags)
        /// (Normally for InitialStatusEffects)
        /// </summary>
        public StatusEffect PermanentStatusEffects;

        /// <summary>
        /// Status effects that will never be successfully inflicted (Flags)
        /// </summary>
        public StatusEffect StatusImmunity;

        /// <summary>
        /// Caster given bonus to magic damage when using a spell of this element(s) (Flags)
        /// </summary>
        public Element MagicElementUp;

        /// <summary>
        /// Special characteristics of armor (Flags)
        /// </summary>
        public ArmorSpecial ArmorSpecial;

        /// <summary>
        /// Vocations that can equip this armor  (Flags)
        /// </summary>
        public VocationType EquipVocations;
    }
}

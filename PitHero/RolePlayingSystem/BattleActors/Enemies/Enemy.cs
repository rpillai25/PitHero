using PitHero.RolePlayingSystem.Abilities;
using PitHero.RolePlayingSystem.GameData;
using PitHero.RolePlayingSystem.Items;
using PitHero.RolePlayingSystem.Abilities.Enemy;
using static PitHero.RolePlayingSystem.Abilities.AbilityCache;
using System.Collections.Generic;

namespace PitHero.RolePlayingSystem.BattleActors.Enemies
{
    public class Enemy : BattleActorData
    {        
        public int Gold;
        public int AttackPower;
        public int AttackMult;
        public int EvadePercent;
        public int Defense;
        public int Speed;
        public int MagicPower;
        public int MagicMult;
        public int MagicEvadePercent;
        public int MagicDefense;
        /// <summary>
        /// Elements enemy is immune to. (Flags)
        /// </summary>
        public Element ElementImmunity;

        /// <summary>
        /// Status effects enemy is immune to. (Flags)
        /// </summary>
        public StatusEffect StatusImmunity;

        /// <summary>
        /// Elements that enemy can absorb. (Flags)
        /// </summary>
        public Element ElementAbsorb;

        /// <summary>
        /// AttackCategories that can't be evaded. (Flags)
        /// </summary>
        public AttackCategory CantEvade;

        /// <summary>
        /// Elemental weaknesses. (Flags)
        /// </summary>
        public Element ElementWeakness;

        /// <summary>
        /// Type of enemy. (Flags)
        /// </summary>
        public EnemyType EnemyType;

        /// <summary>
        /// Initial Status effects (Flags)
        /// </summary>
        public StatusEffect InitialStatusEffects;

        /// <summary>
        /// Status effects on enemy that can never be dispelled (Flags)
        /// (Normally for InitialStatusEffects)
        /// </summary>
        public StatusEffect PermanentStatusEffects;

        public List<Ability> AbilityImmunity;

        public EnemySpecialAttack EnemySpecialAttack;

        public List<Ability> Abilities;

        public Commands ControlCommands;
        public List<Ability> ControlAbilities;

        public Ability Catch;

        public List<Ability> Learnable;

        public Item DropAlways;
        public Item DropRare;
        public Item StealCommon;
        public Item StealRare;
    }
}

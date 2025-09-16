using PitHero.RolePlayingSystem.Abilities;
using PitHero.RolePlayingSystem.Abilities.Enemy;
using PitHero.RolePlayingSystem.Abilities.Magic.Black;
using PitHero.RolePlayingSystem.GameData;
using PitHero.RolePlayingSystem.Items;
using PitHero.RolePlayingSystem.Items.Armors;
using PitHero.RolePlayingSystem.Items.Consumable;
using System.Collections.Generic;
using static PitHero.RolePlayingSystem.Abilities.AbilityCache;

namespace PitHero.RolePlayingSystem.BattleActors.Enemies
{
    public class Ooze : Enemy
    {
        public Ooze()
        {
            Level = 6;
            BaseHP = 16;
            BaseMP = 3;
            AttackPower = 5;
            AttackMult = 1;
            EvadePercent = 0;
            Defense = 0;
            StatusImmunity = StatusEffect.Dead;
            EnemySpecialAttack = new EnemySpecialAttack("Critical", EnemySpecialtyEffect.Damage1_5x, true);
            Abilities = new List<Ability>() { EnemyAbility(EnemyAbilityCatalog.OozeWhip) };
            ControlCommands = Commands.Fight | Commands.Run;
            ControlAbilities = new List<Ability>() { EnemyAbility(EnemyAbilityCatalog.OozeWhip) };
            Learnable = new List<Ability>() { EnemyAbility(EnemyAbilityCatalog.OozeWhip) };
            Catch = BlackAbility(BlackAbilityCatalog.Flare);
            DropRare = ItemCache.Armor(ArmorCatalog.LeatherHelmet);
            StealCommon = ItemCache.Consumable(ConsumableCatalog.HealingHerb);
        }
    }
}

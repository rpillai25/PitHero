using System.Collections.Generic;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Skills
{
    // Mystic Avenger (Dragon Fist + Spellcloak) Skills
    
    // Passives
    public sealed class MysticCounterPassive : BaseSkill
    {
        public MysticCounterPassive() : base("mysticavenger.mystic_counter", "Mystic Counter", SkillKind.Passive, SkillTargetType.Self, 0, 180) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.EnableCounter = true; // Counterattack with magic on stealth (simplified)
        }
    }

    public sealed class ArcaneCloakPassive : BaseSkill
    {
        public ArcaneCloakPassive() : base("mysticavenger.arcane_cloak", "Arcane Cloak", SkillKind.Passive, SkillTargetType.Self, 0, 220) { }
        public override void ApplyPassive(Hero hero)
        {
            hero.DeflectChance += 0.10f; // +10% evasion
            // Magic attacks don't break stealth (placeholder)
        }
    }

    // Active Skills
    public sealed class DragonBoltSkill : BaseSkill
    {
        public DragonBoltSkill() : base("mysticavenger.dragon_bolt", "Dragon Bolt", SkillKind.Active, SkillTargetType.SingleEnemy, 7, 250) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            // Magic+physical damage to primary
            var resPhys = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            var resMag = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (resPhys.Hit) primary.TakeDamage(resPhys.Damage);
            if (resMag.Hit) primary.TakeDamage(resMag.Damage);
            // AoE splash to surrounding
            for (int i = 0; i < surrounding.Count && i < 3; i++)
            {
                var enemy = surrounding[i];
                var splash = resolver.Resolve(stats, enemy.Stats, DamageKind.Magical, hero.Level, enemy.Level);
                if (splash.Hit) enemy.TakeDamage(splash.Damage / 2);
            }
            return "DragonBolt";
        }
    }

    public sealed class MysticFadeSkill : BaseSkill
    {
        public MysticFadeSkill() : base("mysticavenger.mystic_fade", "Mystic Fade", SkillKind.Active, SkillTargetType.Self, 9, 220) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            // Untargetable for 2 turns, MP regen (placeholder)
            hero.RestoreMP(10);
            return "MysticFade";
        }
    }
}

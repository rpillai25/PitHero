using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using PitHero;

namespace RolePlayingFramework.Skills
{
    // ==============================================================================
    // KNIGHT SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Holy Strike from Paladin - Light damage based on Strength.</summary>
    public sealed class HolyStrikeSkill : BaseSkill
    {
        public HolyStrikeSkill() : base("synergy.holy_strike", SkillTextKey.Skill_Synergy_HolyStrike_Name, SkillTextKey.Skill_Synergy_HolyStrike_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 6, 200, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + (int)(stats.Strength * 0.5f));
            return "HolyStrike";
        }
    }

    /// <summary>Iaido Slash from Samurai - Quick draw attack with bonus crit.</summary>
    public sealed class IaidoSlashSkill : BaseSkill
    {
        public IaidoSlashSkill() : base("synergy.iaido_slash", SkillTextKey.Skill_Synergy_IaidoSlash_Name, SkillTextKey.Skill_Synergy_IaidoSlash_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 180, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.3f));
            return "IaidoSlash";
        }
    }

    /// <summary>Shadow Slash from Ninja - Dark damage with AGI bonus.</summary>
    public sealed class ShadowSlashSkill : BaseSkill
    {
        public ShadowSlashSkill() : base("synergy.shadow_slash", SkillTextKey.Skill_Synergy_ShadowSlash_Name, SkillTextKey.Skill_Synergy_ShadowSlash_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 175, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Agility / 2);
            return "ShadowSlash";
        }
    }

    /// <summary>Spellblade from War Mage - Magic-infused physical attack.</summary>
    public sealed class SpellbladeSkill : BaseSkill
    {
        public SpellbladeSkill() : base("synergy.spellblade", SkillTextKey.Skill_Synergy_Spellblade_Name, SkillTextKey.Skill_Synergy_Spellblade_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 7, 220, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 3);
            return "Spellblade";
        }
    }

    // ==============================================================================
    // MAGE SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Meteor from Wizard - Powerful fire AoE.</summary>
    public sealed class MeteorSkill : BaseSkill
    {
        public MeteorSkill() : base("synergy.meteor", SkillTextKey.Skill_Synergy_Meteor_Name, SkillTextKey.Skill_Synergy_Meteor_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 12, 350, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + (int)(stats.Magic * 0.8f * (1f + hero.FireDamageBonus)));
            }
            return "Meteor";
        }
    }

    /// <summary>Shadow Bolt from Spellcloak - Dark magic attack.</summary>
    public sealed class ShadowBoltSkill : BaseSkill
    {
        public ShadowBoltSkill() : base("synergy.shadow_bolt", SkillTextKey.Skill_Synergy_ShadowBolt_Name, SkillTextKey.Skill_Synergy_ShadowBolt_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 160, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 2);
            return "ShadowBolt";
        }
    }

    /// <summary>Elemental Volley from Arcane Archer - Magic arrow barrage.</summary>
    public sealed class ElementalVolleySkill : BaseSkill
    {
        public ElementalVolleySkill() : base("synergy.elemental_volley", SkillTextKey.Skill_Synergy_ElementalVolley_Name, SkillTextKey.Skill_Synergy_ElementalVolley_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 280, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Magic / 3);
            }
            return "ElementalVolley";
        }
    }

    /// <summary>Blitz from War Mage - Quick magic burst.</summary>
    public sealed class BlitzSkill : BaseSkill
    {
        public BlitzSkill() : base("synergy.blitz", SkillTextKey.Skill_Synergy_Blitz_Name, SkillTextKey.Skill_Synergy_Blitz_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 4, 150, ElementType.Wind) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Agility / 3);
            return "Blitz";
        }
    }

    // ==============================================================================
    // PRIEST SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Aura Heal from Paladin - Healing with protection.</summary>
    public sealed class AuraHealSkill : BaseSkill
    {
        public AuraHealSkill() : base("synergy.aura_heal", SkillTextKey.Skill_Synergy_AuraHeal_Name, SkillTextKey.Skill_Synergy_AuraHeal_Desc, SkillKind.Active, SkillTargetType.Self, 6, 200, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var mult = 1f + hero.HealPowerBonus;
            hero.RestoreHP((int)((25 + hero.GetTotalStats().Magic * 2.5f) * mult));
            hero.PassiveDefenseBonus += 2;
            return "AuraHeal";
        }
    }

    /// <summary>Purify from Wizard - Remove debuffs and heal.</summary>
    public sealed class PurifySkill : BaseSkill
    {
        public PurifySkill() : base("synergy.purify", SkillTextKey.Skill_Synergy_Purify_Name, SkillTextKey.Skill_Synergy_Purify_Desc, SkillKind.Active, SkillTargetType.Self, 5, 180, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var mult = 1f + hero.HealPowerBonus;
            hero.RestoreHP((int)((15 + hero.GetTotalStats().Magic * 2) * mult));
            return "Purify";
        }
    }

    /// <summary>Sacred Strike from Divine Fist - Holy martial attack.</summary>
    public sealed class SacredStrikeSkill : BaseSkill
    {
        public SacredStrikeSkill() : base("synergy.sacred_strike", SkillTextKey.Skill_Synergy_SacredStrike_Name, SkillTextKey.Skill_Synergy_SacredStrike_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 190, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 4);
            return "SacredStrike";
        }
    }

    /// <summary>Life Leech from Shadowmender - Drain HP from enemy.</summary>
    public sealed class LifeLeechSkill : BaseSkill
    {
        public LifeLeechSkill() : base("synergy.life_leech", SkillTextKey.Skill_Synergy_LifeLeech_Name, SkillTextKey.Skill_Synergy_LifeLeech_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 6, 210, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit)
            {
                int damage = res.Damage + stats.Magic / 3;
                primary.TakeDamage(damage);
                hero.RestoreHP(damage / 2);
            }
            return "LifeLeech";
        }
    }

    // ==============================================================================
    // MONK SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Dragon Claw from Dragon Fist - Powerful elemental strike.</summary>
    public sealed class DragonClawSkill : BaseSkill
    {
        public DragonClawSkill() : base("synergy.dragon_claw", SkillTextKey.Skill_Synergy_DragonClaw_Name, SkillTextKey.Skill_Synergy_DragonClaw_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 7, 230, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)((res.Damage + stats.Magic / 2) * (1f + hero.FireDamageBonus)));
            return "DragonClaw";
        }
    }

    /// <summary>Energy Burst from Dragon Fist - Ki AoE.</summary>
    public sealed class EnergyBurstSkill : BaseSkill
    {
        public EnergyBurstSkill() : base("synergy.energy_burst", SkillTextKey.Skill_Synergy_EnergyBurst_Name, SkillTextKey.Skill_Synergy_EnergyBurst_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 250, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Magic / 4);
            }
            return "EnergyBurst";
        }
    }

    /// <summary>Dragon Kick from Samurai - Powerful spinning kick.</summary>
    public sealed class DragonKickSkill : BaseSkill
    {
        public DragonKickSkill() : base("synergy.dragon_kick", SkillTextKey.Skill_Synergy_DragonKick_Name, SkillTextKey.Skill_Synergy_DragonKick_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 170, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.2f) + stats.Agility / 3);
            return "DragonKick";
        }
    }

    /// <summary>Sneak Punch from Shadow Fist - Stealth strike.</summary>
    public sealed class SneakPunchSkill : BaseSkill
    {
        public SneakPunchSkill() : base("synergy.sneak_punch", SkillTextKey.Skill_Synergy_SneakPunch_Name, SkillTextKey.Skill_Synergy_SneakPunch_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 4, 150, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Agility / 2);
            return "SneakPunch";
        }
    }

    // ==============================================================================
    // THIEF SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Smoke Bomb from Ninja - Evasion boost and escape.</summary>
    public sealed class SmokeBombSkill : BaseSkill
    {
        public SmokeBombSkill() : base("synergy.smoke_bomb", SkillTextKey.Skill_Synergy_SmokeBomb_Name, SkillTextKey.Skill_Synergy_SmokeBomb_Desc, SkillKind.Active, SkillTargetType.Self, 4, 140, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.DeflectChance += 0.2f;
            return "SmokeBomb";
        }
    }

    /// <summary>Poison Arrow from Stalker - DoT ranged attack.</summary>
    public sealed class PoisonArrowSkill : BaseSkill
    {
        public PoisonArrowSkill() : base("synergy.poison_arrow", SkillTextKey.Skill_Synergy_PoisonArrow_Name, SkillTextKey.Skill_Synergy_PoisonArrow_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 160, ElementType.Earth) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.1f) + stats.Agility / 4);
            return "PoisonArrow";
        }
    }

    /// <summary>Fade from Spellcloak - Stealth with magic.</summary>
    public sealed class FadeSkill : BaseSkill
    {
        public FadeSkill() : base("synergy.fade", SkillTextKey.Skill_Synergy_Fade_Name, SkillTextKey.Skill_Synergy_Fade_Desc, SkillKind.Active, SkillTargetType.Self, 5, 170, ElementType.Dark) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.DeflectChance += 0.15f;
            hero.MPTickRegen += 1;
            return "Fade";
        }
    }

    /// <summary>Ki Cloak from Shadow Fist - Ki shield.</summary>
    public sealed class KiCloakSkill : BaseSkill
    {
        public KiCloakSkill() : base("synergy.ki_cloak", SkillTextKey.Skill_Synergy_KiCloak_Name, SkillTextKey.Skill_Synergy_KiCloak_Desc, SkillKind.Active, SkillTargetType.Self, 5, 160, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            hero.PassiveDefenseBonus += 3;
            return "KiCloak";
        }
    }

    // ==============================================================================
    // BOWMAN SYNERGY SKILLS
    // ==============================================================================

    /// <summary>Piercing Arrow from Arcane Archer - Armor-piercing shot.</summary>
    public sealed class PiercingArrowSkill : BaseSkill
    {
        public PiercingArrowSkill() : base("synergy.piercing_arrow", SkillTextKey.Skill_Synergy_PiercingArrow_Name, SkillTextKey.Skill_Synergy_PiercingArrow_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 170, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.4f));
            return "PiercingArrow";
        }
    }

    /// <summary>Lightshot from Holy Archer - Holy ranged attack.</summary>
    public sealed class LightshotSkill : BaseSkill
    {
        public LightshotSkill() : base("synergy.lightshot", SkillTextKey.Skill_Synergy_Lightshot_Name, SkillTextKey.Skill_Synergy_Lightshot_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 175, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 3);
            return "Lightshot";
        }
    }

    /// <summary>Ki Arrow from Ki Shot - Ki-enhanced projectile.</summary>
    public sealed class KiArrowSkill : BaseSkill
    {
        public KiArrowSkill() : base("synergy.ki_arrow", SkillTextKey.Skill_Synergy_KiArrow_Name, SkillTextKey.Skill_Synergy_KiArrow_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 5, 165, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + (stats.Strength + stats.Magic) / 4);
            return "KiArrow";
        }
    }

    /// <summary>Arrow Flurry from Ki Shot - Rapid fire attack.</summary>
    public sealed class ArrowFlurrySkill : BaseSkill
    {
        public ArrowFlurrySkill() : base("synergy.arrow_flurry", SkillTextKey.Skill_Synergy_ArrowFlurry_Name, SkillTextKey.Skill_Synergy_ArrowFlurry_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 8, 240, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Physical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage((int)(res.Damage * 0.9f));
            }
            return "ArrowFlurry";
        }
    }

    // ==============================================================================
    // CROSS-CLASS SYNERGY SKILLS (Tertiary Job Concepts)
    // ==============================================================================

    /// <summary>Sacred Blade from Templar - Holy warrior ultimate attack.</summary>
    public sealed class SacredBladeSkill : BaseSkill
    {
        public SacredBladeSkill() : base("synergy.sacred_blade", SkillTextKey.Skill_Synergy_SacredBlade_Name, SkillTextKey.Skill_Synergy_SacredBlade_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 10, 320, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Strength / 2 + stats.Magic / 2);
            return "SacredBlade";
        }
    }

    /// <summary>Flash Strike from Shinobi Master - Ultimate speed attack.</summary>
    public sealed class FlashStrikeSkill : BaseSkill
    {
        public FlashStrikeSkill() : base("synergy.flash_strike", SkillTextKey.Skill_Synergy_FlashStrike_Name, SkillTextKey.Skill_Synergy_FlashStrike_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 8, 280, ElementType.Neutral) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Physical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage((int)(res.Damage * 1.5f) + stats.Agility / 2);
            return "FlashStrike";
        }
    }

    /// <summary>Soul Ward from Soul Guardian - Protective healing aura.</summary>
    public sealed class SoulWardSkill : BaseSkill
    {
        public SoulWardSkill() : base("synergy.soul_ward", SkillTextKey.Skill_Synergy_SoulWard_Name, SkillTextKey.Skill_Synergy_SoulWard_Desc, SkillKind.Active, SkillTargetType.Self, 8, 300, ElementType.Light) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var mult = 1f + hero.HealPowerBonus;
            hero.RestoreHP((int)((30 + hero.GetTotalStats().Magic * 3) * mult));
            hero.PassiveDefenseBonus += 4;
            return "SoulWard";
        }
    }

    /// <summary>Dragon Bolt from Mystic Avenger - Dragon magic projectile.</summary>
    public sealed class DragonBoltSkill : BaseSkill
    {
        public DragonBoltSkill() : base("synergy.dragon_bolt", SkillTextKey.Skill_Synergy_DragonBolt_Name, SkillTextKey.Skill_Synergy_DragonBolt_Desc, SkillKind.Active, SkillTargetType.SingleEnemy, 9, 290, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            var res = resolver.Resolve(stats, primary.Stats, DamageKind.Magical, hero.Level, primary.Level);
            if (res.Hit) primary.TakeDamage(res.Damage + stats.Magic / 2 + (int)(stats.Strength * 0.25f));
            return "DragonBolt";
        }
    }

    /// <summary>Elemental Storm from Spell Sniper - Ultimate AoE.</summary>
    public sealed class ElementalStormSkill : BaseSkill
    {
        public ElementalStormSkill() : base("synergy.elemental_storm", SkillTextKey.Skill_Synergy_ElementalStorm_Name, SkillTextKey.Skill_Synergy_ElementalStorm_Desc, SkillKind.Active, SkillTargetType.SurroundingEnemies, 15, 400, ElementType.Fire) { }
        public override string Execute(Hero hero, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            var stats = hero.GetTotalStats();
            for (int i = 0; i < surrounding.Count; i++)
            {
                var e = surrounding[i];
                var res = resolver.Resolve(stats, e.Stats, DamageKind.Magical, hero.Level, e.Level);
                if (res.Hit) e.TakeDamage(res.Damage + stats.Magic);
            }
            return "ElementalStorm";
        }
    }
}

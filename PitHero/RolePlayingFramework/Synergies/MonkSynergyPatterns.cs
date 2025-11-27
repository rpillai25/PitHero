using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Monk synergy patterns with martial arts focus.</summary>
    public static class MonkSynergyPatterns
    {
        /// <summary>Dragon Claw - Claws and fire orbs for draconic power.</summary>
        public static SynergyPattern CreateDragonClaw()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3),
                new Point(0, 4), new Point(2, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.HatHeadband,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("dragon_claw_stats", "+30 Strength, +20 Magic, +70 HP", new StatBlock(30, 0, 0, 20), hpBonus: 70),
                new PassiveAbilityEffect("dragon_claw_fire", "+25% Fire Damage", fireDamageBonus: 0.25f),
                new SkillModifierEffect("dragon_claw_mp", "-10% MP Cost", mpCostReductionPercent: 10f)
            };
            
            return new SynergyPattern(
                "monk.dragon_claw",
                "Dragon Claw",
                "Channel draconic fire through martial arts for devastating strikes",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 270,
                unlockedSkill: new DragonClawSkill()
            );
        }

        /// <summary>Energy Burst - Fists and orbs for ki explosion.</summary>
        public static SynergyPattern CreateEnergyBurst()
        {
            var offsets = new List<Point>
            { 
                new Point(1, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2),
                new Point(1, 3)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.WeaponKnuckle, ItemKind.Accessory,
                ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.HatHeadband,
                ItemKind.WeaponKnuckle
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("energy_burst_stats", "+22 Strength, +18 Magic, +50 MP", new StatBlock(22, 0, 0, 18), mpBonus: 50),
                new SkillModifierEffect("energy_burst_mp", "-12% MP Cost", mpCostReductionPercent: 12f)
            };
            
            return new SynergyPattern(
                "monk.energy_burst",
                "Energy Burst",
                "Release concentrated ki energy in explosive bursts",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 220,
                unlockedSkill: new EnergyBurstSkill()
            );
        }

        /// <summary>Dragon Kick - Boots and fists for powerful kicks.</summary>
        public static SynergyPattern CreateDragonKick()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(1, 1), new Point(2, 1),
                new Point(2, 2), new Point(3, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.ArmorGi,
                ItemKind.WeaponKnuckle, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.HatHeadband
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("dragon_kick_stats", "+18 Strength, +15 Agility", new StatBlock(18, 15, 0, 0)),
                new PassiveAbilityEffect("dragon_kick_deflect", "+8% Deflect", deflectChanceIncrease: 0.08f)
            };
            
            return new SynergyPattern(
                "monk.dragon_kick",
                "Dragon Kick",
                "Master devastating kicks with dragon-like force",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 185,
                unlockedSkill: new DragonKickSkill()
            );
        }

        /// <summary>Sneak Punch - Fists and dark items for stealth strikes.</summary>
        public static SynergyPattern CreateSneakPunch()
        {
            var offsets = new List<Point>
            { 
                new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.Accessory,
                ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.WeaponKnuckle, ItemKind.HatHeadband
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sneak_punch_stats", "+15 Strength, +18 Agility", new StatBlock(15, 18, 0, 0)),
                new PassiveAbilityEffect("sneak_punch_deflect", "+12% Deflect", deflectChanceIncrease: 0.12f)
            };
            
            return new SynergyPattern(
                "monk.sneak_punch",
                "Sneak Punch",
                "Strike from shadows with lightning-fast precision",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175,
                unlockedSkill: new SneakPunchSkill()
            );
        }

        /// <summary>Iron Fist - Pure fist mastery for raw power.</summary>
        public static SynergyPattern CreateIronFist()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.WeaponKnuckle, ItemKind.WeaponKnuckle
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("iron_fist_stats", "+12 Strength", new StatBlock(12, 0, 0, 0))
            };
            
            return new SynergyPattern(
                "monk.iron_fist",
                "Iron Fist",
                "Pure dedication to hand-to-hand combat",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 115
            );
        }

        /// <summary>Martial Focus - Fists and light armor for balanced combat.</summary>
        public static SynergyPattern CreateMartialFocus()
        {
            var offsets = new List<Point>
            { 
                new Point(1, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle,
                ItemKind.ArmorGi, ItemKind.HatHeadband, ItemKind.ArmorGi,
                ItemKind.WeaponKnuckle
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("martial_focus_stats", "+15 Strength, +12 Agility, +8 Vitality", new StatBlock(15, 12, 8, 0)),
                new PassiveAbilityEffect("martial_focus_counter", "Counter enabled", enableCounter: true)
            };
            
            return new SynergyPattern(
                "monk.martial_focus",
                "Martial Focus",
                "Perfect balance of offense and defense in martial arts",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 170
            );
        }

        /// <summary>Ki Mastery - Fists and orbs for spiritual energy.</summary>
        public static SynergyPattern CreateKiMastery()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi,
                ItemKind.WeaponKnuckle, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("ki_mastery_stats", "+16 Strength, +14 Magic, +40 MP", new StatBlock(16, 0, 0, 14), mpBonus: 40),
                new PassiveAbilityEffect("ki_mastery_regen", "+2 MP Regen", mpTickRegen: 2)
            };
            
            return new SynergyPattern(
                "monk.ki_mastery",
                "Ki Mastery",
                "Channel spiritual energy to enhance combat prowess",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 165
            );
        }

        /// <summary>Evasion Training - Light armor and boots for mobility.</summary>
        public static SynergyPattern CreateEvasionTraining()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.ArmorGi, ItemKind.WeaponKnuckle,
                ItemKind.HatHeadband, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("evasion_training_stats", "+8 Agility, +8 Vitality", new StatBlock(0, 8, 8, 0)),
                new PassiveAbilityEffect("evasion_training_deflect", "+15% Deflect", deflectChanceIncrease: 0.15f)
            };
            
            return new SynergyPattern(
                "monk.evasion_training",
                "Evasion Training",
                "Light armor maximizes mobility and evasion",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 145
            );
        }

        /// <summary>Register all Monk patterns.</summary>
        public static void RegisterAllMonkPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateDragonClaw());
            detector.RegisterPattern(CreateEnergyBurst());
            detector.RegisterPattern(CreateDragonKick());
            detector.RegisterPattern(CreateSneakPunch());
            detector.RegisterPattern(CreateIronFist());
            detector.RegisterPattern(CreateMartialFocus());
            detector.RegisterPattern(CreateKiMastery());
            detector.RegisterPattern(CreateEvasionTraining());
        }
    }
}

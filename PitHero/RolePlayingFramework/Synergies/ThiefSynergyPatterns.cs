using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Thief synergy patterns with stealth and agility focus.</summary>
    public static class ThiefSynergyPatterns
    {
        /// <summary>Smoke Bomb - Daggers and dark items for evasion.</summary>
        public static SynergyPattern CreateSmokeBomb()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory,
                ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("smoke_bomb_stats", "+12 Agility, +10 Strength", new StatBlock(10, 12, 0, 0)),
                new PassiveAbilityEffect("smoke_bomb_deflect", "+18% Deflect", deflectChanceIncrease: 0.18f)
            };
            
            return new SynergyPattern(
                "thief.smoke_bomb",
                "Smoke Bomb",
                "Vanish into shadows with enhanced evasion",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 165,
                unlockedSkill: new SmokeBombSkill()
            );
        }

        /// <summary>Poison Arrow - Daggers and bows for toxic strikes.</summary>
        public static SynergyPattern CreatePoisonArrow()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(1, 1), new Point(2, 1),
                new Point(2, 2), new Point(3, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi,
                ItemKind.WeaponSword, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("poison_arrow_stats", "+14 Agility, +10 Strength", new StatBlock(10, 14, 0, 0)),
                new PassiveAbilityEffect("poison_arrow_deflect", "+8% Deflect", deflectChanceIncrease: 0.08f)
            };
            
            return new SynergyPattern(
                "thief.poison_arrow",
                "Poison Arrow",
                "Coat weapons with deadly toxins for lingering damage",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 170,
                unlockedSkill: new PoisonArrowSkill()
            );
        }

        /// <summary>Fade - Dark orbs and light armor for magical stealth.</summary>
        public static SynergyPattern CreateFade()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(2, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Accessory, ItemKind.WeaponSword, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.HatHeadband
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("fade_stats", "+16 Agility, +12 Magic, +40 MP", new StatBlock(0, 16, 0, 12), mpBonus: 40),
                new PassiveAbilityEffect("fade_deflect", "+15% Deflect, +2 MP Regen", deflectChanceIncrease: 0.15f, mpTickRegen: 2),
                new SkillModifierEffect("fade_mp", "-10% MP Cost", mpCostReductionPercent: 10f)
            };
            
            return new SynergyPattern(
                "thief.fade",
                "Fade",
                "Blend magic and stealth to become nearly invisible",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 200,
                unlockedSkill: new FadeSkill()
            );
        }

        /// <summary>Ki Cloak - Light armor and orbs for defensive ki.</summary>
        public static SynergyPattern CreateKiCloak()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.WeaponSword,
                ItemKind.ArmorGi, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("ki_cloak_stats", "+12 Agility, +8 Magic, +30 MP", new StatBlock(0, 12, 0, 8), mpBonus: 30),
                new PassiveAbilityEffect("ki_cloak_defense", "+4 Defense, +10% Deflect", defenseBonus: 4, deflectChanceIncrease: 0.1f)
            };
            
            return new SynergyPattern(
                "thief.ki_cloak",
                "Ki Cloak",
                "Protective ki energy shields from harm",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175,
                unlockedSkill: new KiCloakSkill()
            );
        }

        /// <summary>Shadow Step - Simple diagonal for basic stealth movement.</summary>
        public static SynergyPattern CreateShadowStep()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0),
                new Point(1, 1),
                new Point(2, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword,
                ItemKind.ArmorGi,
                ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("shadow_step_stats", "+10 Agility", new StatBlock(0, 10, 0, 0)),
                new PassiveAbilityEffect("shadow_step_deflect", "+8% Deflect", deflectChanceIncrease: 0.08f)
            };
            
            return new SynergyPattern(
                "thief.shadow_step",
                "Shadow Step",
                "Move through shadows with enhanced speed",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 120
            );
        }

        /// <summary>Lockpicking - Simple pattern for utility bonus.</summary>
        public static SynergyPattern CreateLockpicking()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("lockpicking_stats", "+8 Agility, +5 Strength", new StatBlock(5, 8, 0, 0))
            };
            
            return new SynergyPattern(
                "thief.lockpicking",
                "Lockpicking",
                "Nimble fingers unlock hidden treasures",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 110
            );
        }

        /// <summary>Trap Mastery - Mixed daggers and bows for tactical combat.</summary>
        public static SynergyPattern CreateTrapMastery()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(2, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponSword,
                ItemKind.Accessory, ItemKind.ArmorGi
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("trap_mastery_stats", "+12 Agility, +8 Strength, +6 Vitality", new StatBlock(8, 12, 6, 0)),
                new PassiveAbilityEffect("trap_mastery_deflect", "+10% Deflect", deflectChanceIncrease: 0.1f)
            };
            
            return new SynergyPattern(
                "thief.trap_mastery",
                "Trap Mastery",
                "Set deadly traps and ambushes for enemies",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 155
            );
        }

        /// <summary>Assassin's Edge - Daggers and dark items for lethal strikes.</summary>
        public static SynergyPattern CreateAssassinsEdge()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponSword,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.HatHeadband
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("assassin_edge_stats", "+18 Agility, +15 Strength", new StatBlock(15, 18, 0, 0)),
                new PassiveAbilityEffect("assassin_edge_deflect", "+12% Deflect", deflectChanceIncrease: 0.12f)
            };
            
            return new SynergyPattern(
                "thief.assassins_edge",
                "Assassin's Edge",
                "Strike from darkness with deadly precision",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 180
            );
        }

        /// <summary>Register all Thief patterns.</summary>
        public static void RegisterAllThiefPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateSmokeBomb());
            detector.RegisterPattern(CreatePoisonArrow());
            detector.RegisterPattern(CreateFade());
            detector.RegisterPattern(CreateKiCloak());
            detector.RegisterPattern(CreateShadowStep());
            detector.RegisterPattern(CreateLockpicking());
            detector.RegisterPattern(CreateTrapMastery());
            detector.RegisterPattern(CreateAssassinsEdge());
        }
    }
}

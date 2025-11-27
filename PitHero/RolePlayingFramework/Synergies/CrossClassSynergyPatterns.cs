using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Cross-class synergy patterns combining multiple job archetypes.</summary>
    public static class CrossClassSynergyPatterns
    {
        /// <summary>Sacred Blade - Ultimate Knight+Priest combination.</summary>
        public static SynergyPattern CreateSacredBlade()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2), new Point(4, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3), new Point(4, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.Accessory, ItemKind.Shield,
                ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.HatPriest, ItemKind.Accessory, ItemKind.HatHelm,
                ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.Accessory, ItemKind.Shield, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.ArmorRobe
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sacred_blade_stats", "+40 Strength, +35 Magic, +30 Vitality, +100 HP, +80 MP", 
                    new StatBlock(40, 0, 30, 35), hpBonus: 100, mpBonus: 80),
                new PassiveAbilityEffect("sacred_blade_abilities", "+10 Defense, +15% Deflect, +25% Heal Power", 
                    defenseBonus: 10, deflectChanceIncrease: 0.15f, healPowerBonus: 0.25f),
                new SkillModifierEffect("sacred_blade_mp", "-15% MP Cost", mpCostReductionPercent: 15f)
            };
            
            return new SynergyPattern(
                "cross.sacred_blade",
                "Sacred Blade",
                "Holy warrior's ultimate power combining sword and divine magic",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 350,
                unlockedSkill: new SacredBladeSkill()
            );
        }

        /// <summary>Flash Strike - Knight+Thief speed combination.</summary>
        public static SynergyPattern CreateFlashStrike()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.ArmorGi,
                ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Shield, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.HatHeadband, ItemKind.WeaponSword,
                ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.Accessory, ItemKind.HatHelm,
                ItemKind.Accessory, ItemKind.WeaponSword, ItemKind.Shield, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("flash_strike_stats", "+35 Strength, +40 Agility, +80 HP", 
                    new StatBlock(35, 40, 0, 0), hpBonus: 80),
                new PassiveAbilityEffect("flash_strike_deflect", "+20% Deflect, +6 Defense", 
                    defenseBonus: 6, deflectChanceIncrease: 0.2f)
            };
            
            return new SynergyPattern(
                "cross.flash_strike",
                "Flash Strike",
                "Lightning-fast blade strikes combining warrior power with thief speed",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 310,
                unlockedSkill: new FlashStrikeSkill()
            );
        }

        /// <summary>Soul Ward - Priest+Knight defensive ultimate.</summary>
        public static SynergyPattern CreateSoulWard()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2), new Point(4, 2),
                new Point(0, 3), new Point(2, 3), new Point(4, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Shield, ItemKind.WeaponStaff, ItemKind.ArmorMail, ItemKind.ArmorRobe, ItemKind.Shield,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.HatPriest, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.ArmorMail, ItemKind.WeaponStaff, ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.ArmorRobe,
                ItemKind.Accessory, ItemKind.HatHelm, ItemKind.Accessory,
                ItemKind.Shield, ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.Shield
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("soul_ward_stats", "+25 Strength, +40 Magic, +35 Vitality, +120 HP, +90 MP", 
                    new StatBlock(25, 0, 35, 40), hpBonus: 120, mpBonus: 90),
                new PassiveAbilityEffect("soul_ward_abilities", "+12 Defense, +30% Heal Power, +3 MP Regen", 
                    defenseBonus: 12, healPowerBonus: 0.3f, mpTickRegen: 3),
                new PassiveAbilityEffect("soul_ward_counter", "Counter enabled", enableCounter: true)
            };
            
            return new SynergyPattern(
                "cross.soul_ward",
                "Soul Ward",
                "Ultimate protection combining divine magic and impenetrable defense",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 330,
                unlockedSkill: new SoulWardSkill()
            );
        }

        /// <summary>Dragon Bolt - Monk+Mage draconic magic.</summary>
        public static SynergyPattern CreateDragonBolt()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4),
                new Point(0, 5), new Point(1, 5), new Point(2, 5), new Point(3, 5)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.ArmorRobe,
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.HatWizard, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.HatHeadband,
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("dragon_bolt_stats", "+30 Strength, +40 Magic, +20 Agility, +100 MP", 
                    new StatBlock(30, 20, 0, 40), mpBonus: 100),
                new PassiveAbilityEffect("dragon_bolt_fire", "+35% Fire Damage", fireDamageBonus: 0.35f),
                new SkillModifierEffect("dragon_bolt_mp", "-15% MP Cost", mpCostReductionPercent: 15f)
            };
            
            return new SynergyPattern(
                "cross.dragon_bolt",
                "Dragon Bolt",
                "Channel draconic power through martial arts and arcane magic",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 320,
                unlockedSkill: new DragonBoltSkill()
            );
        }

        /// <summary>Elemental Storm - Ultimate Mage+Bowman combination.</summary>
        public static SynergyPattern CreateElementalStorm()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0), new Point(5, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1), new Point(5, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2), new Point(4, 2), new Point(5, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3), new Point(4, 3), new Point(5, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4), new Point(5, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.HatWizard, ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.HatHeadband,
                ItemKind.WeaponRod, ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("elemental_storm_stats", "+50 Magic, +30 Agility, +120 MP", 
                    new StatBlock(0, 30, 0, 50), mpBonus: 120),
                new PassiveAbilityEffect("elemental_storm_fire", "+40% Fire Damage, +3 MP Regen", 
                    fireDamageBonus: 0.4f, mpTickRegen: 3),
                new SkillModifierEffect("elemental_storm_mp", "-20% MP Cost", mpCostReductionPercent: 20f)
            };
            
            return new SynergyPattern(
                "cross.elemental_storm",
                "Elemental Storm",
                "Unleash devastating elemental magic across the battlefield",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 380,
                unlockedSkill: new ElementalStormSkill()
            );
        }

        /// <summary>Battle Mage - Knight+Mage balanced hybrid.</summary>
        public static SynergyPattern CreateBattleMage()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.HatWizard,
                ItemKind.WeaponSword, ItemKind.Shield, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.HatHelm
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("battle_mage_stats", "+25 Strength, +30 Magic, +20 Vitality, +70 HP, +70 MP", 
                    new StatBlock(25, 0, 20, 30), hpBonus: 70, mpBonus: 70),
                new PassiveAbilityEffect("battle_mage_abilities", "+6 Defense, +20% Fire Damage", 
                    defenseBonus: 6, fireDamageBonus: 0.2f),
                new SkillModifierEffect("battle_mage_mp", "-12% MP Cost", mpCostReductionPercent: 12f)
            };
            
            return new SynergyPattern(
                "cross.battle_mage",
                "Battle Mage",
                "Blend martial combat with arcane mastery",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 260
            );
        }

        /// <summary>Holy Warrior - Knight+Priest balanced combination.</summary>
        public static SynergyPattern CreateHolyWarrior()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.Shield,
                ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.HatHelm, ItemKind.HatPriest, ItemKind.WeaponStaff,
                ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Accessory, ItemKind.Shield,
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("holy_warrior_stats", "+30 Strength, +25 Magic, +25 Vitality, +90 HP, +60 MP", 
                    new StatBlock(30, 0, 25, 25), hpBonus: 90, mpBonus: 60),
                new PassiveAbilityEffect("holy_warrior_abilities", "+8 Defense, +18% Heal Power", 
                    defenseBonus: 8, healPowerBonus: 0.18f)
            };
            
            return new SynergyPattern(
                "cross.holy_warrior",
                "Holy Warrior",
                "Divine power flows through steel and spirit",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 270
            );
        }

        /// <summary>Shadow Master - Thief+Monk stealth combination.</summary>
        public static SynergyPattern CreateShadowMaster()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2), new Point(4, 2),
                new Point(0, 3), new Point(1, 3), new Point(3, 3), new Point(4, 3)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.WeaponSword,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.HatHeadband, ItemKind.Accessory, ItemKind.WeaponSword,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("shadow_master_stats", "+28 Strength, +38 Agility, +20 Magic, +70 HP", 
                    new StatBlock(28, 38, 0, 20), hpBonus: 70),
                new PassiveAbilityEffect("shadow_master_deflect", "+22% Deflect", deflectChanceIncrease: 0.22f),
                new PassiveAbilityEffect("shadow_master_counter", "Counter enabled", enableCounter: true)
            };
            
            return new SynergyPattern(
                "cross.shadow_master",
                "Shadow Master",
                "Master of stealth combat and shadow techniques",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 280
            );
        }

        /// <summary>Arcane Protector - Mage+Knight defensive magic.</summary>
        public static SynergyPattern CreateArcaneProtector()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3),
                new Point(0, 4), new Point(2, 4), new Point(3, 4)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Shield, ItemKind.WeaponRod, ItemKind.Accessory, ItemKind.Shield,
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.ArmorMail,
                ItemKind.WeaponRod, ItemKind.Accessory, ItemKind.HatWizard, ItemKind.Accessory,
                ItemKind.Shield, ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.HatHelm,
                ItemKind.Accessory, ItemKind.Shield, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("arcane_protector_stats", "+20 Strength, +35 Magic, +28 Vitality, +100 HP, +80 MP", 
                    new StatBlock(20, 0, 28, 35), hpBonus: 100, mpBonus: 80),
                new PassiveAbilityEffect("arcane_protector_defense", "+10 Defense, +12% Deflect, +2 MP Regen", 
                    defenseBonus: 10, deflectChanceIncrease: 0.12f, mpTickRegen: 2)
            };
            
            return new SynergyPattern(
                "cross.arcane_protector",
                "Arcane Protector",
                "Magical shields combine with steel for ultimate defense",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 290
            );
        }

        /// <summary>Elemental Champion - All equipment types ultimate pattern.</summary>
        public static SynergyPattern CreateElementalChampion()
        {
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0), new Point(5, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1), new Point(5, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2), new Point(4, 2), new Point(5, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3), new Point(3, 3), new Point(4, 3), new Point(5, 3),
                new Point(0, 4), new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4), new Point(5, 4),
                new Point(0, 5), new Point(1, 5), new Point(2, 5), new Point(3, 5), new Point(4, 5), new Point(5, 5)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.WeaponKnuckle, ItemKind.WeaponRod, ItemKind.WeaponStaff, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.ArmorMail, ItemKind.ArmorGi, ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Shield, ItemKind.HatHelm, ItemKind.HatHeadband, ItemKind.HatWizard, ItemKind.HatPriest, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.WeaponSword, ItemKind.WeaponKnuckle, ItemKind.Accessory, ItemKind.ArmorMail, ItemKind.Shield, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("elemental_champion_stats", "+45 Strength, +40 Agility, +40 Vitality, +45 Magic, +150 HP, +120 MP", 
                    new StatBlock(45, 40, 40, 45), hpBonus: 150, mpBonus: 120),
                new PassiveAbilityEffect("elemental_champion_abilities", "+12 Defense, +18% Deflect, +40% Fire Damage, +4 MP Regen", 
                    defenseBonus: 12, deflectChanceIncrease: 0.18f, fireDamageBonus: 0.4f, mpTickRegen: 4),
                new PassiveAbilityEffect("elemental_champion_counter", "Counter enabled, +25% Heal Power", 
                    enableCounter: true, healPowerBonus: 0.25f),
                new SkillModifierEffect("elemental_champion_mp", "-20% MP Cost", mpCostReductionPercent: 20f)
            };
            
            return new SynergyPattern(
                "cross.elemental_champion",
                "Elemental Champion",
                "Master all elements and combat styles for ultimate power",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 400
            );
        }

        /// <summary>Register all Cross-Class patterns.</summary>
        public static void RegisterAllCrossClassPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateSacredBlade());
            detector.RegisterPattern(CreateFlashStrike());
            detector.RegisterPattern(CreateSoulWard());
            detector.RegisterPattern(CreateDragonBolt());
            detector.RegisterPattern(CreateElementalStorm());
            detector.RegisterPattern(CreateBattleMage());
            detector.RegisterPattern(CreateHolyWarrior());
            detector.RegisterPattern(CreateShadowMaster());
            detector.RegisterPattern(CreateArcaneProtector());
            detector.RegisterPattern(CreateElementalChampion());
        }
    }
}

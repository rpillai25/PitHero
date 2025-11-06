using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Collection of example synergy patterns demonstrating the system.</summary>
    public static class ExampleSynergyPatterns
    {
        /// <summary>
        /// Creates a "Sword & Shield Mastery" synergy pattern.
        /// Pattern: Sword adjacent to Shield (horizontal or vertical)
        /// Effect: +5 Defense, +10% Deflect Chance
        /// </summary>
        public static SynergyPattern CreateSwordShieldMastery()
        {
            // Pattern: Sword at (0,0), Shield at (1,0) - horizontal adjacency
            var offsets = new List<Point> 
            { 
                new Point(0, 0),  // Sword position (anchor)
                new Point(1, 0)   // Shield position (adjacent right)
            };
            
            var requiredKinds = new List<ItemKind> 
            { 
                ItemKind.WeaponSword, 
                ItemKind.Shield 
            };
            
            var effects = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect(
                    "sword_shield_defense",
                    "+5 Defense, +10% Deflect",
                    defenseBonus: 5,
                    deflectChanceIncrease: 0.1f
                )
            };
            
            return new SynergyPattern(
                "sword_shield_mastery",
                "Sword & Shield Mastery",
                "A warrior's classic stance provides excellent defense.",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 100
            );
        }
        
        /// <summary>
        /// Creates a "Mage's Focus" synergy pattern.
        /// Pattern: Rod and 2 Accessories arranged in a triangle
        /// Effect: +5 Magic, -20% MP Cost
        /// </summary>
        public static SynergyPattern CreateMagesFocus()
        {
            // Pattern: Rod at (0,0), Accessory at (1,0), Accessory at (0,1)
            var offsets = new List<Point>
            {
                new Point(0, 0),  // Rod (anchor)
                new Point(1, 0),  // First accessory
                new Point(0, 1)   // Second accessory
            };
            
            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponRod,
                ItemKind.Accessory,
                ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect(
                    "mage_magic_boost",
                    "+5 Magic",
                    new StatBlock(0, 0, 0, 5) // +5 Magic
                ),
                new SkillModifierEffect(
                    "mage_mp_reduction",
                    "-20% MP Cost",
                    mpCostReductionPercent: 20f
                )
            };
            
            return new SynergyPattern(
                "mages_focus",
                "Mage's Focus",
                "Magical accessories amplify the rod's power and reduce spell costs.",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 150
            );
        }
        
        /// <summary>
        /// Creates a "Monk's Balance" synergy pattern.
        /// Pattern: Knuckle weapon surrounded by Gi armor and Headband
        /// Effect: +3 STR, +3 AGI, Counter enabled
        /// </summary>
        public static SynergyPattern CreateMonksBalance()
        {
            // Pattern: Knuckle at (1,1), Gi at (0,1), Headband at (2,1)
            var offsets = new List<Point>
            {
                new Point(1, 1),  // Knuckle (anchor)
                new Point(0, 1),  // Gi (left)
                new Point(2, 1)   // Headband (right)
            };
            
            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponKnuckle,
                ItemKind.ArmorGi,
                ItemKind.HatHeadband
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect(
                    "monk_balance_stats",
                    "+3 STR, +3 AGI",
                    new StatBlock(3, 3, 0, 0)
                ),
                new PassiveAbilityEffect(
                    "monk_counter",
                    "Counter enabled",
                    enableCounter: true
                )
            };
            
            return new SynergyPattern(
                "monks_balance",
                "Monk's Balance",
                "Harmony between body and mind enables devastating counters.",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 200
            );
        }
        
        /// <summary>
        /// Creates a "Heavy Armor Set" synergy pattern.
        /// Pattern: Mail armor, Helm, and Shield in an L-shape
        /// Effect: +10 Defense, +50 HP
        /// </summary>
        public static SynergyPattern CreateHeavyArmorSet()
        {
            // Pattern: Mail at (0,0), Helm at (0,1), Shield at (1,0)
            var offsets = new List<Point>
            {
                new Point(0, 0),  // Mail (anchor)
                new Point(0, 1),  // Helm (below)
                new Point(1, 0)   // Shield (right)
            };
            
            var requiredKinds = new List<ItemKind>
            {
                ItemKind.ArmorMail,
                ItemKind.HatHelm,
                ItemKind.Shield
            };
            
            var effects = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect(
                    "heavy_armor_defense",
                    "+10 Defense",
                    defenseBonus: 10
                ),
                new StatBonusEffect(
                    "heavy_armor_hp",
                    "+50 HP",
                    new StatBlock(0, 0, 0, 0),
                    hpBonus: 50
                )
            };
            
            return new SynergyPattern(
                "heavy_armor_set",
                "Heavy Armor Set",
                "Full plate protection provides exceptional durability.",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 150
            );
        }
        
        /// <summary>
        /// Creates a "Priest's Devotion" synergy pattern.
        /// Pattern: Staff and 2 Robes arranged vertically
        /// Effect: +20% Heal Power, +2 MP Regen per turn
        /// </summary>
        public static SynergyPattern CreatePriestsDevotion()
        {
            // Pattern: Staff at (0,0), Robe at (0,1), Priest Hat at (0,2)
            var offsets = new List<Point>
            {
                new Point(0, 0),  // Staff (anchor)
                new Point(0, 1),  // Robe (below)
                new Point(0, 2)   // Priest Hat (below robe)
            };
            
            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponStaff,
                ItemKind.ArmorRobe,
                ItemKind.HatPriest
            };
            
            var effects = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect(
                    "priest_heal_power",
                    "+20% Heal Power, +2 MP Regen",
                    healPowerBonus: 0.2f,
                    mpTickRegen: 2
                )
            };
            
            return new SynergyPattern(
                "priests_devotion",
                "Priest's Devotion",
                "Sacred vestments enhance healing powers and spiritual energy.",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175
            );
        }
        
        /// <summary>
        /// Registers all example patterns with a detector.
        /// </summary>
        public static void RegisterAllExamplePatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateSwordShieldMastery());
            detector.RegisterPattern(CreateMagesFocus());
            detector.RegisterPattern(CreateMonksBalance());
            detector.RegisterPattern(CreateHeavyArmorSet());
            detector.RegisterPattern(CreatePriestsDevotion());
        }
    }
}

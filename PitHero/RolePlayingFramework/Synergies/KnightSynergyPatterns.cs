using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Knight synergy patterns with equipment-based requirements.</summary>
    public static class KnightSynergyPatterns
    {
        /// <summary>Holy Strike - Light element weapon synergy unlocking Paladin attack.</summary>
        public static SynergyPattern CreateHolyStrike()
        {
            // L-Shape pattern:
            // [Sword]      
            // [Sword][Acc] 
            //        [Acc] 
            var offsets = new List<Point>
            { 
                new Point(0, 0),
                new Point(0, 1), new Point(1, 1),
                                 new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword,
                ItemKind.WeaponSword, ItemKind.Accessory,
                                      ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("holy_strike_stats", "+10 Strength, +5 Magic", new StatBlock(10, 0, 0, 5)),
                new SkillModifierEffect("holy_strike_mp", "-10% MP Cost", mpCostReductionPercent: 10f)
            };
            
            return new SynergyPattern(
                "knight.holy_strike",
                "Holy Strike",
                "Light-infused blade grants Paladin's holy attack ability",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 180,
                unlockedSkill: new HolyStrikeSkill()
            );
        }

        /// <summary>Iaido Slash - Sword mastery unlocking quick draw attack.</summary>
        public static SynergyPattern CreateIaidoSlash()
        {
            // Horizontal line:
            // [Sword][Sword][Shield][Helm]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.WeaponSword, ItemKind.Shield, ItemKind.HatHelm
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("iaido_stats", "+12 Strength, +8 Agility", new StatBlock(12, 8, 0, 0)),
                new PassiveAbilityEffect("iaido_counter", "+5% Deflect", deflectChanceIncrease: 0.05f)
            };
            
            return new SynergyPattern(
                "knight.iaido_slash",
                "Iaido Slash",
                "Master swordsman's stance enables lightning-fast draw attacks",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175,
                unlockedSkill: new IaidoSlashSkill()
            );
        }

        /// <summary>Shadow Slash - Dagger/Sword combo for dark ninja strike.</summary>
        public static SynergyPattern CreateShadowSlash()
        {
            // Cross pattern:
            //        [Sword]
            // [Sword][Acc  ][Acc]
            //        [Gi   ]
            var offsets = new List<Point>
            { 
                               new Point(1, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                               new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                                     ItemKind.WeaponSword,
                ItemKind.WeaponSword, ItemKind.Accessory, ItemKind.Accessory,
                                     ItemKind.ArmorGi
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("shadow_stats", "+10 Strength, +10 Agility", new StatBlock(10, 10, 0, 0)),
                new PassiveAbilityEffect("shadow_deflect", "+10% Deflect", deflectChanceIncrease: 0.1f)
            };
            
            return new SynergyPattern(
                "knight.shadow_slash",
                "Shadow Slash",
                "Blend warrior strength with ninja stealth for shadow-enhanced strikes",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 185,
                unlockedSkill: new ShadowSlashSkill()
            );
        }

        /// <summary>Spellblade - Sword and magic fusion for War Mage power.</summary>
        public static SynergyPattern CreateSpellblade()
        {
            // 3x3 rectangle:
            // [Sword][Acc  ][Acc  ]
            // [Rod  ][Acc  ][Robe ]
            // [Sword][Acc  ][WzHat]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.Accessory,  ItemKind.Accessory,
                ItemKind.WeaponRod,   ItemKind.Accessory,  ItemKind.ArmorRobe,
                ItemKind.WeaponSword, ItemKind.Accessory,  ItemKind.HatWizard
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("spellblade_stats", "+15 Strength, +20 Magic", new StatBlock(15, 0, 0, 20)),
                new SkillModifierEffect("spellblade_mp", "-15% MP Cost", mpCostReductionPercent: 15f),
                new PassiveAbilityEffect("spellblade_fire", "+20% Fire Damage", fireDamageBonus: 0.2f)
            };
            
            return new SynergyPattern(
                "knight.spellblade",
                "Spellblade",
                "Fuse martial prowess with arcane power for devastating magic-infused attacks",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 230,
                unlockedSkill: new SpellbladeSkill()
            );
        }

        /// <summary>Armor Mastery - Heavy armor defensive bonus.</summary>
        public static SynergyPattern CreateArmorMastery()
        {
            // 2x2 square:
            // [Mail ][Mail ]
            // [Helm ][Shield]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.ArmorMail, ItemKind.ArmorMail,
                ItemKind.HatHelm,   ItemKind.Shield
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("armor_mastery_stats", "+50 HP, +10 Vitality", new StatBlock(0, 0, 10, 0), hpBonus: 50),
                new PassiveAbilityEffect("armor_mastery_defense", "+5 Defense", defenseBonus: 5)
            };
            
            return new SynergyPattern(
                "knight.armor_mastery",
                "Armor Mastery",
                "Master the art of heavy armor for superior protection",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 140
            );
        }

        /// <summary>Sword Proficiency - Basic sword combat bonus.</summary>
        public static SynergyPattern CreateSwordProficiency()
        {
            // Horizontal line:
            // [Sword][Sword][Sword]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.WeaponSword, ItemKind.WeaponSword
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sword_prof_stats", "+8 Strength, +5 Agility", new StatBlock(8, 5, 0, 0))
            };
            
            return new SynergyPattern(
                "knight.sword_proficiency",
                "Sword Proficiency",
                "Dedication to the blade enhances sword combat effectiveness",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 120
            );
        }

        /// <summary>Guardian's Resolve - Shield and armor for ultimate defense.</summary>
        public static SynergyPattern CreateGuardiansResolve()
        {
            // 3x2 rectangle:
            // [Shield][Mail ][Helm ]
            // [Shield][Mail ][Acc  ]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.Shield,    ItemKind.ArmorMail, ItemKind.HatHelm,
                ItemKind.Shield,    ItemKind.ArmorMail, ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("guardian_stats", "+80 HP, +15 Vitality, +10 Strength", new StatBlock(10, 0, 15, 0), hpBonus: 80),
                new PassiveAbilityEffect("guardian_defense", "+8 Defense, +10% Deflect", defenseBonus: 8, deflectChanceIncrease: 0.1f),
                new PassiveAbilityEffect("guardian_counter", "Counter enabled", enableCounter: true)
            };
            
            return new SynergyPattern(
                "knight.guardians_resolve",
                "Guardian's Resolve",
                "Unwavering defense and counterattacks protect allies",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 190
            );
        }

        /// <summary>Berserker Rage - Aggressive fighting style with high damage.</summary>
        public static SynergyPattern CreateBerserkerRage()
        {
            // 2x3 rectangle:
            // [Sword][Mail ]
            // [Sword][Mail ]
            // [Helm ][Acc  ]
            var offsets = new List<Point>
            { 
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };
            
            var requiredKinds = new List<ItemKind>
            { 
                ItemKind.WeaponSword, ItemKind.ArmorMail,
                ItemKind.WeaponSword, ItemKind.ArmorMail,
                ItemKind.HatHelm,     ItemKind.Accessory
            };
            
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("berserker_stats", "+25 Strength, +60 HP, -5 Agility", new StatBlock(25, -5, 0, 0), hpBonus: 60),
                new PassiveAbilityEffect("berserker_power", "+20% Fire Damage", fireDamageBonus: 0.2f)
            };
            
            return new SynergyPattern(
                "knight.berserker_rage",
                "Berserker Rage",
                "Unleash primal fury for devastating offensive power",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 195
            );
        }

        /// <summary>Register all Knight patterns.</summary>
        public static void RegisterAllKnightPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateHolyStrike());
            detector.RegisterPattern(CreateIaidoSlash());
            detector.RegisterPattern(CreateShadowSlash());
            detector.RegisterPattern(CreateSpellblade());
            detector.RegisterPattern(CreateArmorMastery());
            detector.RegisterPattern(CreateSwordProficiency());
            detector.RegisterPattern(CreateGuardiansResolve());
            detector.RegisterPattern(CreateBerserkerRage());
        }
    }
}

using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Priest synergy patterns with healing and holy magic focus.</summary>
    public static class PriestSynergyPatterns
    {
        /// <summary>Aura Heal - Staves and light orbs for protective healing.</summary>
        public static SynergyPattern CreateAuraHeal()
        {
            var offsets = new List<Point>
            {
                new Point(1, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory,
                ItemKind.WeaponStaff, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.ArmorRobe
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("aura_heal_stats", "+15 Magic, +10 Vitality, +40 MP", new StatBlock(0, 0, 10, 15), mpBonus: 40),
                new PassiveAbilityEffect("aura_heal_power", "+20% Heal Power, +3 Defense", healPowerBonus: 0.2f, defenseBonus: 3)
            };

            return new SynergyPattern(
                "priest.aura_heal",
                "Aura Heal",
                "Holy aura provides healing with protective blessing",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 190,
                unlockedSkill: new AuraHealSkill()
            );
        }

        /// <summary>Purify - Staff arrangement for cleansing magic.</summary>
        public static SynergyPattern CreatePurify()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0),
                new Point(0, 1),
                new Point(1, 1),
                new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponStaff,
                ItemKind.ArmorRobe,
                ItemKind.Accessory,
                ItemKind.HatPriest
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("purify_stats", "+12 Magic, +8 Vitality", new StatBlock(0, 0, 8, 12)),
                new PassiveAbilityEffect("purify_heal", "+15% Heal Power", healPowerBonus: 0.15f)
            };

            return new SynergyPattern(
                "priest.purify",
                "Purify",
                "Sacred vestments enable cleansing and restoration",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175,
                unlockedSkill: new PurifySkill()
            );
        }

        /// <summary>Sacred Strike - Holy martial arts combination.</summary>
        public static SynergyPattern CreateSacredStrike()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponStaff, ItemKind.Accessory,
                ItemKind.WeaponKnuckle, ItemKind.ArmorRobe,
                ItemKind.WeaponStaff, ItemKind.HatPriest
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sacred_strike_stats", "+12 Strength, +15 Magic", new StatBlock(12, 0, 0, 15)),
                new PassiveAbilityEffect("sacred_strike_heal", "+10% Heal Power", healPowerBonus: 0.1f),
                new SkillModifierEffect("sacred_strike_mp", "-8% MP Cost", mpCostReductionPercent: 8f)
            };

            return new SynergyPattern(
                "priest.sacred_strike",
                "Sacred Strike",
                "Combine holy magic with martial prowess for divine combat",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 185,
                unlockedSkill: new SacredStrikeSkill()
            );
        }

        /// <summary>Life Leech - Dark orbs and staves for vampiric magic.</summary>
        public static SynergyPattern CreateLifeLeech()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory,
                ItemKind.WeaponStaff, ItemKind.HatPriest, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("life_leech_stats", "+25 Magic, +12 Vitality, +60 MP", new StatBlock(0, 0, 12, 25), mpBonus: 60),
                new PassiveAbilityEffect("life_leech_heal", "+15% Heal Power", healPowerBonus: 0.15f),
                new SkillModifierEffect("life_leech_mp", "-12% MP Cost", mpCostReductionPercent: 12f)
            };

            return new SynergyPattern(
                "priest.life_leech",
                "Life Leech",
                "Master forbidden dark magic to drain life force",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 235,
                unlockedSkill: new LifeLeechSkill()
            );
        }

        /// <summary>Divine Protection - Robes and staves for holy defense.</summary>
        public static SynergyPattern CreateDivineProtection()
        {
            var offsets = new List<Point>
            {
                new Point(1, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.HatPriest,
                ItemKind.ArmorRobe, ItemKind.WeaponStaff, ItemKind.ArmorRobe,
                ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("divine_protection_stats", "+50 HP, +12 Vitality, +8 Magic", new StatBlock(0, 0, 12, 8), hpBonus: 50),
                new PassiveAbilityEffect("divine_protection_defense", "+5 Defense, +8% Deflect", defenseBonus: 5, deflectChanceIncrease: 0.08f)
            };

            return new SynergyPattern(
                "priest.divine_protection",
                "Divine Protection",
                "Sacred vestments provide divine shielding",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 150
            );
        }

        /// <summary>Healing Amplification - Simple staff cluster for basic healing boost.</summary>
        public static SynergyPattern CreateHealingAmplification()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponStaff, ItemKind.Accessory,
                ItemKind.ArmorRobe, ItemKind.WeaponStaff
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("healing_amp_stats", "+10 Magic, +25 MP", new StatBlock(0, 0, 0, 10), mpBonus: 25),
                new PassiveAbilityEffect("healing_amp_power", "+12% Heal Power", healPowerBonus: 0.12f)
            };

            return new SynergyPattern(
                "priest.healing_amplification",
                "Healing Amplification",
                "Focus healing energy through sacred implements",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 130
            );
        }

        /// <summary>Holy Aura - Light orbs and robes for radiant energy.</summary>
        public static SynergyPattern CreateHolyAura()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.WeaponStaff, ItemKind.HatPriest
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("holy_aura_stats", "+14 Magic, +8 Vitality, +35 MP", new StatBlock(0, 0, 8, 14), mpBonus: 35),
                new PassiveAbilityEffect("holy_aura_regen", "+2 MP Regen", mpTickRegen: 2)
            };

            return new SynergyPattern(
                "priest.holy_aura",
                "Holy Aura",
                "Radiate holy energy that sustains magical power",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 160
            );
        }

        /// <summary>Sanctified Mind - Hats and staves for mental fortitude.</summary>
        public static SynergyPattern CreateSanctifiedMind()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.HatPriest, ItemKind.WeaponStaff,
                ItemKind.Accessory, ItemKind.ArmorRobe,
                ItemKind.HatPriest, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sanctified_mind_stats", "+18 Magic, +50 MP", new StatBlock(0, 0, 0, 18), mpBonus: 50),
                new SkillModifierEffect("sanctified_mind_mp", "-15% MP Cost", mpCostReductionPercent: 15f),
                new PassiveAbilityEffect("sanctified_mind_regen", "+2 MP Regen", mpTickRegen: 2)
            };

            return new SynergyPattern(
                "priest.sanctified_mind",
                "Sanctified Mind",
                "Sacred headwear enhances mental clarity and magical efficiency",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 170
            );
        }

        /// <summary>Divine Vestments - Vertical staff and robes for healing power.</summary>
        public static SynergyPattern CreateDivineVestments()
        {
            // Vertical line:
            // [Staff]
            // [Robe ]
            // [PrHat]
            var offsets = new List<Point>
            {
                new Point(0, 0),
                new Point(0, 1),
                new Point(0, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponStaff,
                ItemKind.ArmorRobe,
                ItemKind.HatPriest
            };

            var effects = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect("divine_vestments_power", "+20% Heal Power, +2 MP Regen", healPowerBonus: 0.2f, mpTickRegen: 2)
            };

            return new SynergyPattern(
                "priest.divine_vestments",
                "Divine Vestments",
                "Sacred vestments enhance healing powers and spiritual energy",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175
            );
        }

        /// <summary>Register all Priest patterns.</summary>
        public static void RegisterAllPriestPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateAuraHeal());
            detector.RegisterPattern(CreatePurify());
            detector.RegisterPattern(CreateSacredStrike());
            detector.RegisterPattern(CreateLifeLeech());
            detector.RegisterPattern(CreateDivineProtection());
            detector.RegisterPattern(CreateHealingAmplification());
            detector.RegisterPattern(CreateHolyAura());
            detector.RegisterPattern(CreateSanctifiedMind());
            detector.RegisterPattern(CreateDivineVestments());
        }
    }
}

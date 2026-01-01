using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Archer synergy patterns with ranged combat focus.</summary>
    public static class ArcherSynergyPatterns
    {
        /// <summary>Piercing Arrow - Bow line for armor-piercing shots.</summary>
        public static SynergyPattern CreatePiercingArrow()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("piercing_arrow_stats", "+14 Agility, +10 Strength", new StatBlock(10, 14, 0, 0)),
                new PassiveAbilityEffect("piercing_arrow_deflect", "+5% Deflect", deflectChanceIncrease: 0.05f)
            };

            return new SynergyPattern(
                "archer.piercing_arrow",
                "Piercing Arrow",
                "Perfect aim pierces through enemy defenses",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 180,
                unlockedSkill: new PiercingArrowSkill()
            );
        }

        /// <summary>Lightshot - Bows and light orbs for holy archery.</summary>
        public static SynergyPattern CreateLightshot()
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
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.ArmorGi
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("lightshot_stats", "+12 Agility, +10 Magic", new StatBlock(0, 12, 0, 10)),
                new SkillModifierEffect("lightshot_mp", "-8% MP Cost", mpCostReductionPercent: 8f)
            };

            return new SynergyPattern(
                "archer.lightshot",
                "Lightshot",
                "Infuse arrows with holy light for magical damage",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 185,
                unlockedSkill: new LightshotSkill()
            );
        }

        /// <summary>Ki Arrow - Bows and neutral orbs for energy shots.</summary>
        public static SynergyPattern CreateKiArrow()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1),
                new Point(0, 2), new Point(1, 2)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi,
                ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("ki_arrow_stats", "+14 Agility, +8 Strength, +8 Magic", new StatBlock(8, 14, 0, 8)),
                new PassiveAbilityEffect("ki_arrow_deflect", "+8% Deflect", deflectChanceIncrease: 0.08f)
            };

            return new SynergyPattern(
                "archer.ki_arrow",
                "Ki Arrow",
                "Channel spiritual energy through arrows",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 175,
                unlockedSkill: new KiArrowSkill()
            );
        }

        /// <summary>Arrow Flurry - Large bow arrangement for rapid fire.</summary>
        public static SynergyPattern CreateArrowFlurry()
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
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.HatHeadband, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("arrow_flurry_stats", "+25 Agility, +18 Strength, +60 HP", new StatBlock(18, 25, 0, 0), hpBonus: 60),
                new PassiveAbilityEffect("arrow_flurry_deflect", "+10% Deflect", deflectChanceIncrease: 0.1f)
            };

            return new SynergyPattern(
                "archer.arrow_flurry",
                "Arrow Flurry",
                "Unleash devastating rapid-fire arrow barrages",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 260,
                unlockedSkill: new ArrowFlurrySkill()
            );
        }

        /// <summary>Marksman - Simple line for basic bow proficiency.</summary>
        public static SynergyPattern CreateMarksman()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory, ItemKind.ArmorGi
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("marksman_stats", "+10 Agility", new StatBlock(0, 10, 0, 0))
            };

            return new SynergyPattern(
                "archer.marksman",
                "Marksman",
                "Basic archery training improves accuracy",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 115
            );
        }

        /// <summary>Eagle Eye - Bows and hats for enhanced precision.</summary>
        public static SynergyPattern CreateSharpAim()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.HatHeadband,
                ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("sharp_aim_stats", "+12 Agility, +6 Vitality", new StatBlock(0, 12, 6, 0)),
                new PassiveAbilityEffect("sharp_aim_deflect", "+8% Deflect", deflectChanceIncrease: 0.08f)
            };

            return new SynergyPattern(
                "archer.sharp_aim",
                "Sharp Aim",
                "Sharp vision identifies enemy weak points",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 140
            );
        }

        /// <summary>Ranger's Path - Bows and boots for mobile archery.</summary>
        public static SynergyPattern CreateRangersPath()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(2, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.HatHeadband
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("rangers_path_stats", "+14 Agility, +10 Vitality", new StatBlock(0, 14, 10, 0)),
                new PassiveAbilityEffect("rangers_path_deflect", "+10% Deflect", deflectChanceIncrease: 0.1f)
            };

            return new SynergyPattern(
                "archer.rangers_path",
                "Ranger's Path",
                "Move swiftly through terrain while maintaining aim",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 160
            );
        }

        /// <summary>Wind Archer - Bows and wind orbs for elemental shots.</summary>
        public static SynergyPattern CreateWindArcher()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorGi, ItemKind.HatHeadband
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("wind_archer_stats", "+16 Agility, +12 Magic, +40 MP", new StatBlock(0, 16, 0, 12), mpBonus: 40),
                new SkillModifierEffect("wind_archer_mp", "-10% MP Cost", mpCostReductionPercent: 10f)
            };

            return new SynergyPattern(
                "archer.wind_archer",
                "Wind Archer",
                "Wind magic guides arrows to their targets",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 170
            );
        }

        /// <summary>Register all Archer patterns.</summary>
        public static void RegisterAllArcherPatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreatePiercingArrow());
            detector.RegisterPattern(CreateLightshot());
            detector.RegisterPattern(CreateKiArrow());
            detector.RegisterPattern(CreateArrowFlurry());
            detector.RegisterPattern(CreateMarksman());
            //detector.RegisterPattern(CreateEagleEye());
            detector.RegisterPattern(CreateRangersPath());
            detector.RegisterPattern(CreateWindArcher());
        }
    }
}

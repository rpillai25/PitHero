using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Mage synergy patterns with magic-focused requirements.</summary>
    public static class MageSynergyPatterns
    {
        /// <summary>Meteor - Fire orb arrangement for massive AoE.</summary>
        public static SynergyPattern CreateMeteor()
        {
            var offsets = new List<Point>
            {
                new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1),
                new Point(0, 2), new Point(1, 2), new Point(2, 2), new Point(3, 2),
                new Point(1, 3), new Point(2, 3)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.WeaponRod, ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.ArmorRobe, ItemKind.HatWizard, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("meteor_stats", "+35 Magic, +80 MP", new StatBlock(0, 0, 0, 35), mpBonus: 80),
                new SkillModifierEffect("meteor_mp", "-15% MP Cost", mpCostReductionPercent: 15f),
                new PassiveAbilityEffect("meteor_fire", "+30% Fire Damage", fireDamageBonus: 0.3f)
            };

            return new SynergyPattern(
                "mage.meteor",
                "Meteor",
                "Channel destructive fire magic to summon devastating meteors",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 280,
                unlockedSkill: new MeteorSkill()
            );
        }

        /// <summary>Shadow Bolt - Dark orb T-shape for shadow magic.</summary>
        public static SynergyPattern CreateShadowBolt()
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
                ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.ArmorRobe
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("shadow_bolt_stats", "+15 Magic, +10 Agility", new StatBlock(0, 10, 0, 15)),
                new SkillModifierEffect("shadow_bolt_mp", "-10% MP Cost", mpCostReductionPercent: 10f)
            };

            return new SynergyPattern(
                "mage.shadow_bolt",
                "Shadow Bolt",
                "Master dark magic to strike from the shadows",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 170,
                unlockedSkill: new ShadowBoltSkill()
            );
        }

        /// <summary>Elemental Volley - Orbs and bows for magic archery.</summary>
        public static SynergyPattern CreateElementalVolley()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1),
                new Point(0, 2), new Point(2, 2),
                new Point(0, 3), new Point(1, 3), new Point(2, 3)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory,
                ItemKind.ArmorRobe, ItemKind.ArmorGi,
                ItemKind.Accessory, ItemKind.HatWizard, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("elemental_volley_stats", "+25 Magic, +15 Agility, +60 MP", new StatBlock(0, 15, 0, 25), mpBonus: 60),
                new SkillModifierEffect("elemental_volley_mp", "-15% MP Cost", mpCostReductionPercent: 15f),
                new PassiveAbilityEffect("elemental_volley_fire", "+20% Fire Damage", fireDamageBonus: 0.2f)
            };

            return new SynergyPattern(
                "mage.elemental_volley",
                "Elemental Volley",
                "Combine arcane and ranged combat for magical arrow barrages",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 240,
                unlockedSkill: new ElementalVolleySkill()
            );
        }

        /// <summary>Blitz - Wind orb cross for lightning-fast magic.</summary>
        public static SynergyPattern CreateBlitz()
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
                ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("blitz_stats", "+12 Magic, +12 Agility", new StatBlock(0, 12, 0, 12)),
                new SkillModifierEffect("blitz_mp", "-5% MP Cost", mpCostReductionPercent: 5f)
            };

            return new SynergyPattern(
                "mage.blitz",
                "Blitz",
                "Harness wind magic for rapid-fire lightning strikes",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 160,
                unlockedSkill: new BlitzSkill()
            );
        }

        /// <summary>Arcane Focus - Simple orb line for basic magic enhancement.</summary>
        public static SynergyPattern CreateArcaneFocus()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("arcane_focus_stats", "+8 Magic, +20 MP", new StatBlock(0, 0, 0, 8), mpBonus: 20)
            };

            return new SynergyPattern(
                "mage.arcane_focus",
                "Arcane Focus",
                "Basic orb arrangement amplifies magical power",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 110
            );
        }

        /// <summary>Elemental Mastery - Mixed element orbs for versatility.</summary>
        public static SynergyPattern CreateElementalMastery()
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
                ItemKind.Accessory, ItemKind.WeaponRod,
                ItemKind.Accessory, ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("elemental_mastery_stats", "+18 Magic, +40 MP", new StatBlock(0, 0, 0, 18), mpBonus: 40),
                new PassiveAbilityEffect("elemental_mastery_fire", "+15% Fire Damage", fireDamageBonus: 0.15f)
            };

            return new SynergyPattern(
                "mage.elemental_mastery",
                "Elemental Mastery",
                "Multiple elemental orbs grant command over diverse magic",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 165
            );
        }

        /// <summary>Spell Weaving - Hats and orbs for casting efficiency.</summary>
        public static SynergyPattern CreateSpellWeaving()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1), new Point(1, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.HatWizard, ItemKind.Accessory,
                ItemKind.Accessory, ItemKind.WeaponRod
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("spell_weaving_stats", "+10 Magic, +30 MP", new StatBlock(0, 0, 0, 10), mpBonus: 30),
                new SkillModifierEffect("spell_weaving_mp", "-10% MP Cost", mpCostReductionPercent: 10f),
                new PassiveAbilityEffect("spell_weaving_regen", "+2 MP Regen", mpTickRegen: 2)
            };

            return new SynergyPattern(
                "mage.spell_weaving",
                "Spell Weaving",
                "Wizard's hat and focus improve spell efficiency",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 155
            );
        }

        /// <summary>Mana Convergence - Robes and orbs for maximum MP pool.</summary>
        public static SynergyPattern CreateManaConvergence()
        {
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.Accessory, ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.ArmorRobe, ItemKind.Accessory, ItemKind.HatWizard
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("mana_convergence_stats", "+20 Magic, +70 MP", new StatBlock(0, 0, 0, 20), mpBonus: 70),
                new PassiveAbilityEffect("mana_convergence_regen", "+3 MP Regen", mpTickRegen: 3)
            };

            return new SynergyPattern(
                "mage.mana_convergence",
                "Mana Convergence",
                "Robes channel ambient mana for massive MP reserves",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 180
            );
        }

        /// <summary>Rod Focus - L-shaped rod and accessories for magic power.</summary>
        public static SynergyPattern CreateRodFocus()
        {
            // L-Shape:
            // [Rod  ][Acc  ]
            // [Acc  ]
            var offsets = new List<Point>
            {
                new Point(0, 0), new Point(1, 0),
                new Point(0, 1)
            };

            var requiredKinds = new List<ItemKind>
            {
                ItemKind.WeaponRod, ItemKind.Accessory,
                ItemKind.Accessory
            };

            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("rod_focus_magic", "+5 Magic", new StatBlock(0, 0, 0, 5)),
                new SkillModifierEffect("rod_focus_mp", "-20% MP Cost", mpCostReductionPercent: 20f)
            };

            return new SynergyPattern(
                "mage.rod_focus",
                "Rod Focus",
                "Magical accessories amplify the rod's power and reduce spell costs",
                offsets,
                requiredKinds,
                effects,
                synergyPointsRequired: 150
            );
        }

        /// <summary>Register all Mage patterns.</summary>
        public static void RegisterAllMagePatterns(SynergyDetector detector)
        {
            detector.RegisterPattern(CreateMeteor());
            detector.RegisterPattern(CreateShadowBolt());
            detector.RegisterPattern(CreateElementalVolley());
            detector.RegisterPattern(CreateBlitz());
            detector.RegisterPattern(CreateArcaneFocus());
            detector.RegisterPattern(CreateElementalMastery());
            detector.RegisterPattern(CreateSpellWeaving());
            detector.RegisterPattern(CreateManaConvergence());
            detector.RegisterPattern(CreateRodFocus());
        }
    }
}

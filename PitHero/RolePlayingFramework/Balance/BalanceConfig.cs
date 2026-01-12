using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Balance
{
    /// <summary>
    /// Centralized configuration for RPG balance formulas and progression.
    /// All formulas and constants are adjustable for easy tuning during playtesting.
    /// </summary>
    /// <remarks>
    /// Balance Goals:
    /// - Linear progression in early levels (1-10) with exponential scaling later
    /// - Monster stats should challenge players but remain beatable at appropriate levels
    /// - Equipment should provide meaningful bonuses without breaking balance
    /// - Elemental matchups should encourage strategic team composition
    /// - Attack damage should scale smoothly with stat differences
    /// </remarks>
    public static class BalanceConfig
    {
        #region Level Progression Constants

        /// <summary>
        /// Experience required to reach level 2 from level 1.
        /// Example: 100 XP for level 2
        /// </summary>
        public const int BaseExperienceForLevel2 = 100;

        /// <summary>
        /// Exponential scaling factor for experience curve.
        /// Higher values create steeper progression curves.
        /// Example: 1.5 provides moderate exponential growth
        /// </summary>
        public const float ExperienceScalingFactor = 1.5f;

        #endregion

        #region Monster Archetype and Stat Type Enums

        /// <summary>
        /// Defines monster archetype patterns that affect stat distribution.
        /// Each archetype has different multipliers for HP and stats.
        /// </summary>
        /// <remarks>
        /// Archetype Multipliers (HP / Str / Agi / Vit / Mag):
        /// - Balanced: 1.0 / 1.0 / 1.0 / 1.0 / 1.0 (baseline)
        /// - Tank: 1.5 / 0.8 / 0.6 / 1.3 / 0.7 (high HP/Vit, low Agi/Mag)
        /// - FastFragile: 0.7 / 1.2 / 1.5 / 0.6 / 0.9 (low HP/Vit, high Agi/Str)
        /// - MagicUser: 0.9 / 0.7 / 0.8 / 0.8 / 1.5 (moderate HP, high Mag)
        /// </remarks>
        public enum MonsterArchetype
        {
            /// <summary>Balanced stats across all categories.</summary>
            Balanced,
            /// <summary>High HP and defense, low speed.</summary>
            Tank,
            /// <summary>High speed and attack, low HP.</summary>
            FastFragile,
            /// <summary>High magic, moderate other stats.</summary>
            MagicUser
        }

        /// <summary>
        /// Defines the four primary stats used in combat calculations.
        /// </summary>
        public enum StatType
        {
            /// <summary>Physical attack power.</summary>
            Strength,
            /// <summary>Speed and evasion.</summary>
            Agility,
            /// <summary>HP and physical defense.</summary>
            Vitality,
            /// <summary>MP and magical power.</summary>
            Magic
        }

        #endregion

        #region Monster Archetype Multipliers

        /// <summary>
        /// Gets the HP multiplier for a given monster archetype.
        /// Used in CalculateMonsterHP formula.
        /// </summary>
        private static float GetArchetypeHPMultiplier(MonsterArchetype archetype)
        {
            return archetype switch
            {
                MonsterArchetype.Balanced => 1.0f,
                MonsterArchetype.Tank => 1.5f,
                MonsterArchetype.FastFragile => 0.7f,
                MonsterArchetype.MagicUser => 0.9f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Gets the stat multiplier for a given archetype and stat type combination.
        /// Used in CalculateMonsterStat formula.
        /// </summary>
        private static float GetArchetypeStatMultiplier(MonsterArchetype archetype, StatType statType)
        {
            return (archetype, statType) switch
            {
                // Balanced: all stats 1.0
                (MonsterArchetype.Balanced, _) => 1.0f,

                // Tank: high Vitality, moderate Strength, low Agility/Magic
                (MonsterArchetype.Tank, StatType.Strength) => 0.8f,
                (MonsterArchetype.Tank, StatType.Agility) => 0.6f,
                (MonsterArchetype.Tank, StatType.Vitality) => 1.3f,
                (MonsterArchetype.Tank, StatType.Magic) => 0.7f,

                // FastFragile: high Agility/Strength, low Vitality/Magic
                (MonsterArchetype.FastFragile, StatType.Strength) => 1.2f,
                (MonsterArchetype.FastFragile, StatType.Agility) => 1.5f,
                (MonsterArchetype.FastFragile, StatType.Vitality) => 0.6f,
                (MonsterArchetype.FastFragile, StatType.Magic) => 0.9f,

                // MagicUser: high Magic, low Strength/Agility, moderate Vitality
                (MonsterArchetype.MagicUser, StatType.Strength) => 0.7f,
                (MonsterArchetype.MagicUser, StatType.Agility) => 0.8f,
                (MonsterArchetype.MagicUser, StatType.Vitality) => 0.8f,
                (MonsterArchetype.MagicUser, StatType.Magic) => 1.5f,

                _ => 1.0f
            };
        }

        #endregion

        #region Rarity Multipliers

        /// <summary>
        /// Gets the equipment bonus multiplier for a given rarity level.
        /// Higher rarity = stronger bonuses.
        /// </summary>
        /// <remarks>
        /// Rarity Factors:
        /// - Common: 0.5x (half baseline)
        /// - Normal: 1.0x (baseline)
        /// - Rare: 1.5x
        /// - Epic: 2.0x
        /// - Legendary: 3.0x
        /// </remarks>
        private static float GetRarityMultiplier(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => 1.0f,
                ItemRarity.Uncommon => 1.5f,
                ItemRarity.Rare => 2.0f,
                ItemRarity.Epic => 2.5f,
                ItemRarity.Legendary => 3.5f,
                _ => 1.0f
            };
        }

        #endregion

        #region Level Progression Methods

        /// <summary>
        /// Calculates cumulative experience required to reach a given level.
        /// </summary>
        /// <param name="level">Target level (1-99).</param>
        /// <returns>Total experience required to reach the level. Level 1 returns 0.</returns>
        /// <remarks>
        /// Formula: Sum from level 2 to target level of (BaseExperienceForLevel2 * (currentLevel ^ ExperienceScalingFactor))
        /// Example progression:
        /// - Level 1: 0 XP
        /// - Level 2: 100 XP
        /// - Level 3: 282 XP (100 + 182)
        /// - Level 10: ~3158 XP
        /// - Level 50: ~316,228 XP
        /// - Level 99: ~1,621,810 XP
        /// </remarks>
        public static int CalculateExperienceForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > 99) level = 99;

            int totalExp = 0;
            for (int i = 2; i <= level; i++)
            {
                // Calculate XP needed for this level using exponential formula
                float xpForLevel = BaseExperienceForLevel2 * System.MathF.Pow(i - 1, ExperienceScalingFactor);
                totalExp += (int)xpForLevel;
            }

            return totalExp;
        }

        /// <summary>
        /// Estimates expected player level for a given pit level.
        /// Used for content difficulty scaling.
        /// </summary>
        /// <param name="pitLevel">Current pit depth (1-99+).</param>
        /// <returns>Expected player level for that pit depth.</returns>
        /// <remarks>
        /// Progression curve:
        /// - Pit 1-10: Levels 1-15 (1.5 levels per pit)
        /// - Pit 11-20: Levels 16-35 (2 levels per pit)
        /// - Pit 21-40: Levels 36-70 (1.75 levels per pit)
        /// - Pit 41+: Levels 71-99 (slower progression, ~0.7 levels per pit)
        /// 
        /// Tuning advice: Adjust multipliers if players are over/under-leveled for content.
        /// </remarks>
        public static int EstimatePlayerLevelForPitLevel(int pitLevel)
        {
            if (pitLevel <= 0) return 1;

            int estimatedLevel;
            if (pitLevel <= 10)
            {
                // Early game: 1.5 levels per pit (1-15)
                estimatedLevel = 1 + (int)(pitLevel * 1.5f);
            }
            else if (pitLevel <= 20)
            {
                // Mid-early game: 2 levels per pit (16-35)
                estimatedLevel = 16 + (pitLevel - 10) * 2;
            }
            else if (pitLevel <= 40)
            {
                // Mid-late game: 1.75 levels per pit (36-70)
                estimatedLevel = 36 + (int)((pitLevel - 20) * 1.75f);
            }
            else
            {
                // End game: slower progression (71-99)
                int pitsAbove40 = pitLevel - 40;
                estimatedLevel = 71 + (int)(pitsAbove40 * 0.7f);
            }

            // Cap at max level
            return System.Math.Min(estimatedLevel, 99);
        }

        #endregion

        #region Monster Stat Calculation Methods

        /// <summary>
        /// Calculates HP for a monster based on level and archetype.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <param name="archetype">Monster archetype affecting HP scaling.</param>
        /// <returns>Calculated HP value.</returns>
        /// <remarks>
        /// Formula: (25 + level * 8) * archetype_hp_multiplier
        /// 
        /// Example values:
        /// - Level 1 Balanced: 33 HP
        /// - Level 10 Balanced: 105 HP
        /// - Level 10 Tank: 158 HP
        /// - Level 50 Balanced: 425 HP
        /// - Level 99 Balanced: 817 HP
        /// - Level 99 Tank: 1226 HP
        /// 
        /// Tuning: Increased from (10 + level * 5) to provide better challenge in early game.
        /// Monsters now survive multiple hits even at low levels, enabling back-and-forth combat.
        /// </remarks>
        public static int CalculateMonsterHP(int level, MonsterArchetype archetype)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            float baseHP = 25 + level * 8;
            float archetypeMultiplier = GetArchetypeHPMultiplier(archetype);

            return (int)(baseHP * archetypeMultiplier);
        }

        /// <summary>
        /// Calculates a specific stat for a monster based on level, archetype, and stat type.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <param name="archetype">Monster archetype affecting stat distribution.</param>
        /// <param name="statType">Which stat to calculate (Strength, Agility, Vitality, Magic).</param>
        /// <returns>Calculated stat value.</returns>
        /// <remarks>
        /// Formula: (3 + level * 1.0) * archetype_stat_multiplier
        /// 
        /// Example values (Balanced archetype):
        /// - Level 1: 4 stat
        /// - Level 10: 13 stat
        /// - Level 30: 33 stat
        /// - Level 50: 53 stat
        /// - Level 99: 102 stat (capped at 99)
        /// 
        /// With archetype multipliers:
        /// - Level 50 Tank Vitality: 69 (53 * 1.3)
        /// - Level 50 FastFragile Agility: 80 (53 * 1.5)
        /// 
        /// Tuning: Increased from (1 + level * 2/3) to provide better challenge.
        /// Monsters now deal meaningful damage even at low levels, creating back-and-forth combat.
        /// </remarks>
        public static int CalculateMonsterStat(int level, MonsterArchetype archetype, StatType statType)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            float baseStat = 3 + level * 1.0f;
            float archetypeMultiplier = GetArchetypeStatMultiplier(archetype, statType);

            int finalStat = (int)(baseStat * archetypeMultiplier);

            // Ensure minimum of 1, cap at 99
            return System.Math.Min(99, System.Math.Max(1, finalStat));
        }

        /// <summary>
        /// Calculates experience yield for defeating a monster.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <returns>Experience points awarded for defeating this monster.</returns>
        /// <remarks>
        /// Formula: 10 + level * 8
        /// 
        /// Example values:
        /// - Level 1: 18 XP
        /// - Level 5: 50 XP
        /// - Level 10: 90 XP
        /// - Level 25: 210 XP
        /// - Level 50: 410 XP
        /// - Level 99: 802 XP
        /// 
        /// Tuning: Adjust multiplier (8) if leveling feels too fast/slow.
        /// Players should fight 5-8 monsters per level in early game.
        /// </remarks>
        public static int CalculateMonsterExperience(int level)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            return 10 + level * 8;
        }

        /// <summary>
        /// Calculates Job Points (JP) yield for defeating a monster.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <returns>Job Points awarded for defeating this monster.</returns>
        /// <remarks>
        /// Formula: 5 + level * 2
        /// 
        /// Example values:
        /// - Level 1: 7 JP
        /// - Level 5: 15 JP
        /// - Level 10: 25 JP
        /// - Level 25: 55 JP
        /// - Level 50: 105 JP
        /// - Level 99: 203 JP
        /// 
        /// Tuning: JP gain is slower than XP to require more battles for skill progression.
        /// Players should need 5-10 monsters per skill point threshold.
        /// </remarks>
        public static int CalculateMonsterJPYield(int level)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            return 5 + level * 2;
        }

        /// <summary>
        /// Calculates Synergy Points (SP) yield for defeating a monster.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <returns>Synergy Points awarded for defeating this monster.</returns>
        /// <remarks>
        /// Formula: 3 + level
        /// 
        /// Example values:
        /// - Level 1: 4 SP
        /// - Level 5: 8 SP
        /// - Level 10: 13 SP
        /// - Level 25: 28 SP
        /// - Level 50: 53 SP
        /// - Level 99: 102 SP
        /// 
        /// Tuning: SP gain is slower than JP to make synergy skill progression meaningful.
        /// With synergy requirements of 100-230 points, players should need 10-30 monsters
        /// to unlock a synergy skill, depending on level.
        /// </remarks>
        public static int CalculateMonsterSPYield(int level)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            return 3 + level;
        }

        /// <summary>
        /// Calculates Gold yield for defeating a monster.
        /// </summary>
        /// <param name="level">Monster level (1-99).</param>
        /// <returns>Gold awarded for defeating this monster.</returns>
        /// <remarks>
        /// Formula: 5 + level * 3
        /// 
        /// Example values:
        /// - Level 1: 8 gold
        /// - Level 5: 20 gold
        /// - Level 10: 35 gold
        /// - Level 25: 80 gold
        /// - Level 50: 155 gold
        /// - Level 99: 302 gold
        /// 
        /// Tuning: Gold gain is balanced to provide meaningful currency for purchasing equipment and items.
        /// Higher level monsters provide substantially more gold to match equipment costs at those levels.
        /// </remarks>
        public static int CalculateMonsterGoldYield(int level)
        {
            if (level < 1) level = 1;
            if (level > 99) level = 99;

            return 5 + level * 3;
        }

        #endregion

        #region Equipment Stat Calculation Methods

        /// <summary>
        /// Calculates attack bonus for equipment based on pit level and rarity.
        /// </summary>
        /// <param name="pitLevel">Current pit depth (1-99+).</param>
        /// <param name="rarity">Equipment rarity tier.</param>
        /// <returns>Attack bonus value.</returns>
        /// <remarks>
        /// Formula: (1 + pitLevel / 2) * rarity_multiplier
        /// 
        /// Example values:
        /// - Pit 1 Normal: 1 attack
        /// - Pit 10 Normal: 6 attack
        /// - Pit 10 Rare: 12 attack
        /// - Pit 20 Normal: 11 attack
        /// - Pit 50 Normal: 26 attack
        /// - Pit 50 Legendary: 91 attack
        /// - Pit 99 Legendary: 175 attack
        /// 
        /// Tuning: Adjust divisor (2) if weapons feel too weak/strong.
        /// </remarks>
        public static int CalculateEquipmentAttackBonus(int pitLevel, ItemRarity rarity)
        {
            if (pitLevel < 1) pitLevel = 1;

            float baseAttack = 1 + pitLevel / 2f;
            float rarityMultiplier = GetRarityMultiplier(rarity);

            return (int)(baseAttack * rarityMultiplier);
        }

        /// <summary>
        /// Calculates defense bonus for equipment based on pit level and rarity.
        /// </summary>
        /// <param name="pitLevel">Current pit depth (1-99+).</param>
        /// <param name="rarity">Equipment rarity tier.</param>
        /// <returns>Defense bonus value.</returns>
        /// <remarks>
        /// Formula: (1 + pitLevel / 3) * rarity_multiplier
        /// 
        /// Example values:
        /// - Pit 1 Normal: 1 defense
        /// - Pit 10 Normal: 4 defense
        /// - Pit 10 Rare: 8 defense
        /// - Pit 30 Normal: 11 defense
        /// - Pit 50 Normal: 17 defense
        /// - Pit 50 Legendary: 60 defense
        /// - Pit 99 Legendary: 116 defense
        /// 
        /// Tuning: Defense is intentionally lower than attack to keep combat fast-paced.
        /// Adjust divisor (3) if armor feels too weak/strong.
        /// </remarks>
        public static int CalculateEquipmentDefenseBonus(int pitLevel, ItemRarity rarity)
        {
            if (pitLevel < 1) pitLevel = 1;

            float baseDefense = 1 + pitLevel / 3f;
            float rarityMultiplier = GetRarityMultiplier(rarity);

            return (int)(baseDefense * rarityMultiplier);
        }

        /// <summary>
        /// Calculates stat bonus (STR/AGI/VIT/MAG) for equipment based on pit level and rarity.
        /// </summary>
        /// <param name="pitLevel">Current pit depth (1-99+).</param>
        /// <param name="rarity">Equipment rarity tier.</param>
        /// <returns>Stat bonus value.</returns>
        /// <remarks>
        /// Formula: (pitLevel / 5) * rarity_multiplier
        /// 
        /// Example values:
        /// - Pit 1 Normal: 0 stat
        /// - Pit 10 Normal: 2 stat
        /// - Pit 10 Rare: 4 stat
        /// - Pit 25 Normal: 5 stat
        /// - Pit 50 Normal: 10 stat
        /// - Pit 50 Legendary: 35 stat
        /// - Pit 99 Legendary: 69 stat
        /// 
        /// Tuning: Stat bonuses provide secondary scaling alongside base stats.
        /// Adjust divisor (5) if stat bonuses feel too impactful/negligible.
        /// </remarks>
        public static int CalculateEquipmentStatBonus(int pitLevel, ItemRarity rarity)
        {
            if (pitLevel < 1) pitLevel = 1;

            float baseStat = pitLevel / 5f;
            float rarityMultiplier = GetRarityMultiplier(rarity);

            return (int)(baseStat * rarityMultiplier);
        }

        #endregion

        #region Elemental Damage Multipliers

        /// <summary>
        /// Calculates damage multiplier for elemental matchups.
        /// </summary>
        /// <param name="attackElement">Element type of the attack.</param>
        /// <param name="defenderProps">Elemental properties of the defender.</param>
        /// <returns>Damage multiplier (1.0 = normal, 2.0 = weakness, 0.5 = resistance, 0.0 = absorption).</returns>
        /// <remarks>
        /// Standard matchups:
        /// - Same element: 0.5x (resistance)
        /// - Opposing element: 2.0x (weakness)
        /// - Neutral element: 1.0x
        /// - No relationship: 1.0x
        /// 
        /// Custom resistances in defenderProps.Resistances can override these values.
        /// Positive resistance values reduce damage further.
        /// Negative resistance values (weakness) increase damage.
        /// 
        /// Example: If defenderProps.Resistances[Fire] = 0.5, Fire damage is reduced by 50% (0.5x * base multiplier).
        /// Example: If defenderProps.Resistances[Ice] = -0.5, Ice damage is increased by 50% (1.5x * base multiplier).
        /// 
        /// Tuning: Adjust base multipliers if elemental matchups feel too weak/strong.
        /// </remarks>
        public static float GetElementalDamageMultiplier(ElementType attackElement, ElementalProperties defenderProps)
        {
            // Start with base multiplier from standard element matchup
            float baseMultiplier = ElementalProperties.GetElementalMultiplier(attackElement, defenderProps.Element);

            // Check for custom resistance/weakness modifiers
            if (defenderProps.Resistances.TryGetValue(attackElement, out float resistanceModifier))
            {
                // Positive resistance = damage reduction
                // Negative resistance = damage increase (weakness)
                // Apply as multiplier: resistance of 0.5 means 50% damage reduction (multiply by 0.5)
                if (resistanceModifier >= 0)
                {
                    // Resistance: reduce damage
                    baseMultiplier *= (1f - resistanceModifier);
                }
                else
                {
                    // Weakness: increase damage
                    baseMultiplier *= (1f + System.MathF.Abs(resistanceModifier));
                }
            }

            // Ensure non-negative multiplier (absorption would be 0.0)
            return System.Math.Max(0f, baseMultiplier);
        }

        #endregion

        #region Battle Stat Formulas

        /// <summary>
        /// Calculates damage dealt in an attack based on attack and defense values.
        /// </summary>
        /// <param name="attack">Attacker's attack value.</param>
        /// <param name="defense">Defender's defense value.</param>
        /// <returns>Damage dealt (minimum 1).</returns>
        /// <remarks>
        /// Formula:
        /// - If Attack >= Defense: Damage = (Attack * 2) - Defense
        /// - If Attack &lt; Defense: Damage = (Attack * Attack) / Defense
        /// 
        /// This creates a system where:
        /// - High attack vs low defense = large damage (linear scaling)
        /// - Low attack vs high defense = minimal damage (quadratic penalty)
        /// - Equal attack/defense = moderate damage
        /// 
        /// Example calculations:
        /// - Attack 50, Defense 30: 70 damage (50*2 - 30)
        /// - Attack 30, Defense 30: 30 damage (30*2 - 30)
        /// - Attack 20, Defense 40: 10 damage (20*20 / 40)
        /// - Attack 10, Defense 50: 2 damage (10*10 / 50)
        /// 
        /// Minimum damage is always 1 to prevent zero damage.
        /// 
        /// Tuning: This formula is already implemented in EnhancedAttackResolver.
        /// Adjust multipliers if damage scaling feels off.
        /// </remarks>
        public static int CalculateAttackDamage(int attack, int defense)
        {
            int damage;
            if (attack >= defense)
            {
                damage = attack * 2 - defense;
            }
            else
            {
                damage = attack * attack / defense;
            }

            return System.Math.Max(1, damage);
        }

        /// <summary>
        /// Calculates evasion value for hit chance calculations.
        /// </summary>
        /// <param name="agility">Character's agility stat.</param>
        /// <param name="level">Character's level.</param>
        /// <returns>Evasion value (0-255 range).</returns>
        /// <remarks>
        /// Formula: min(255, Agility * 2 + Level)
        /// 
        /// Hit determination: random(0-255) &lt; Evasion = Hit Evaded
        /// 
        /// Example evasion values:
        /// - Agility 10, Level 5: 25 evasion (~10% evade chance)
        /// - Agility 25, Level 10: 60 evasion (~23% evade chance)
        /// - Agility 50, Level 30: 130 evasion (~51% evade chance)
        /// - Agility 75, Level 50: 200 evasion (~78% evade chance)
        /// - Agility 99, Level 99: 255 evasion (capped, ~100% evade chance)
        /// 
        /// This formula is already implemented in BattleStats.
        /// 
        /// Tuning: Adjust multipliers if evasion feels too strong/weak.
        /// High agility characters should have meaningful but not overwhelming evasion.
        /// </remarks>
        public static int CalculateEvasion(int agility, int level)
        {
            return System.Math.Min(255, agility * 2 + level);
        }

        #endregion
    }
}

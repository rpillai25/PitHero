using PitHero.Combat;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>Result of a battle AI decision.</summary>
    public struct BattleAction
    {
        /// <summary>Types of battle actions the AI can recommend.</summary>
        public enum ActionKind
        {
            PhysicalAttack,
            UseAttackSkill,
            UseHealingSkill,
            UseConsumable
        }

        /// <summary>What kind of action to take.</summary>
        public ActionKind Kind;

        /// <summary>Skill to use (for UseAttackSkill or UseHealingSkill).</summary>
        public ISkill Skill;

        /// <summary>Consumable to use (for UseConsumable).</summary>
        public Consumable Consumable;

        /// <summary>Bag slot index of the consumable (for UseConsumable).</summary>
        public int BagIndex;

        /// <summary>Target character (Hero or Mercenary object) for healing/buff actions.</summary>
        public object Target;

        /// <summary>True if the target is the hero.</summary>
        public bool TargetsHero;

        /// <summary>Preferred enemy target for attack actions (null = no preference).</summary>
        public IEnemy TargetEnemy;
    }

    /// <summary>
    /// Provides static methods for AI battle decisions based on the selected battle tactic.
    /// Does not execute actions — only returns what action the AI recommends.
    /// Operates on <see cref="IBattlePartyView"/> and <see cref="IEnemy"/> interfaces rather
    /// than Nez entities, so it can be used in both live and headless execution contexts.
    /// </summary>
    public static class BattleTacticDecisionEngine
    {
        private const float DefensiveHealThreshold = 0.6f;
        private const float StrategicMPThreshold = 0.2f;
        private const float DefensiveMPThreshold = 0.3f;

        // Pre-allocated buffers reused each call (single-threaded game)
        private static readonly List<ISkill> _attackSkillBuffer = new List<ISkill>(32);
        private static readonly List<ISkill> _healSkillBuffer = new List<ISkill>(16);
        private static readonly List<ISkill> _buffSkillBuffer = new List<ISkill>(16);

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Decides what the hero should do this turn based on the current battle tactic.
        /// <paramref name="roundNumber"/> is the 1-based battle round (Blitz casts a buff opener in round 1 only).
        /// <paramref name="battleCriticalReached"/> latches once any ally hits critical HP (or dies) this
        /// battle; Strategic uses it to buff reactively in fights that have proven dangerous.
        /// </summary>
        public static BattleAction DecideHeroAction(
            IBattlePartyView party,
            List<IEnemy> livingMonsters,
            List<Mercenary> livingMercenaries,
            int roundNumber,
            bool battleCriticalReached)
        {
            var hero = party.Hero;
            if (hero == null || livingMonsters == null || livingMonsters.Count == 0)
                return CreatePhysicalAttack(null);

            switch (party.CurrentBattleTactic)
            {
                case BattleTactic.Blitz:
                    return DecideBlitz(party, hero, livingMonsters, livingMercenaries, roundNumber);

                case BattleTactic.Defensive:
                    return DecideDefensive(party, hero, livingMonsters, livingMercenaries);

                case BattleTactic.Strategic:
                default:
                    return DecideStrategic(party, hero, livingMonsters, livingMercenaries, battleCriticalReached);
            }
        }

        /// <summary>
        /// Decides what a mercenary should do during their turn.
        /// Follows the hero's current battle tactic with mercenary-specific targeting restrictions.
        /// <paramref name="roundNumber"/> is the 1-based battle round (Blitz casts a buff opener in round 1 only).
        /// <paramref name="battleCriticalReached"/> latches once any ally hits critical HP (or dies) this
        /// battle; Strategic uses it to buff reactively in fights that have proven dangerous.
        /// </summary>
        public static BattleAction DecideMercenaryAction(
            Mercenary merc,
            IBattlePartyView party,
            List<IEnemy> livingMonsters,
            List<Mercenary> livingMercenaries,
            int roundNumber,
            bool battleCriticalReached)
        {
            if (merc == null || livingMonsters == null || livingMonsters.Count == 0)
                return CreatePhysicalAttack(null);

            switch (party.CurrentBattleTactic)
            {
                case BattleTactic.Blitz:
                    return DecideMercBlitz(merc, party, livingMonsters, livingMercenaries, roundNumber);

                case BattleTactic.Defensive:
                    return DecideMercDefensive(merc, party, livingMonsters, livingMercenaries);

                case BattleTactic.Strategic:
                default:
                    return DecideMercStrategic(merc, party, livingMonsters, livingMercenaries, battleCriticalReached);
            }
        }

        // ====================================================================
        // HERO TACTIC IMPLEMENTATIONS
        // ====================================================================

        /// <summary>Blitz: aggressive attacks but perform heal/restore like Strategic; prefer high-MP attacks.
        /// Round 1 only: one self-buff opener cast, then pure aggression (issue #294).</summary>
        private static BattleAction DecideBlitz(
            IBattlePartyView party, Hero hero, List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries,
            int roundNumber)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (use same thresholds as Strategic)
            BattleAction healAction;
            if (TryHeroHealAction(party, hero, livingMercenaries,
                    GameConfig.HeroCriticalHPPercent, StrategicMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Round-1 opener: one buff cast, then pure aggression
            if (roundNumber <= 1)
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, hero, party, livingMercenaries,
                    allowSelfBuffs: true, allowAllyBuffs: true, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            // AoE check first
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEnemy);

            // Best single-target attack skill (prefer elemental advantage, then highest MP cost)
            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters, true);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEnemy);

            // Fall back to physical attack
            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Strategic: heal at 40%, restore MP at 20%, reactive buffs once the battle has
        /// proven dangerous (an ally hit critical HP), efficient attacks.</summary>
        private static BattleAction DecideStrategic(
            IBattlePartyView party, Hero hero,
            List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries,
            bool battleCriticalReached)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (HP < 40% OR MP < 20%) — healing always wins over buffing
            BattleAction healAction;
            if (TryHeroHealAction(party, hero, livingMercenaries,
                    GameConfig.HeroCriticalHPPercent, StrategicMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Reactive buffs (issue #294): once any ally has hit critical HP this battle the
            //    fight has proven dangerous — spend free turns spreading ally-targetable buffs.
            //    Strictly-self buffs still require the caster themself to be critical.
            bool heroCritical = party.IsHeroHPCritical();
            if (battleCriticalReached || heroCritical)
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, hero, party, livingMercenaries,
                    allowSelfBuffs: heroCritical, allowAllyBuffs: battleCriticalReached, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            // 3. Attack (prefer elemental advantage, then lowest MP cost)
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEnemy);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEnemy);

            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Defensive: heal at 60%, restore MP at 30%, buff, then attack only when safe.</summary>
        private static BattleAction DecideDefensive(
            IBattlePartyView party, Hero hero,
            List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (HP < 60% OR MP < 30%)
            BattleAction healAction;
            if (TryHeroHealAction(party, hero, livingMercenaries,
                    DefensiveHealThreshold, DefensiveMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Apply buff if available
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, hero, party, livingMercenaries,
                    allowSelfBuffs: true, allowAllyBuffs: true, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            // 3. Attack (still attack even if not all allies are at 60% — nothing else to do)
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEnemy);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, hero.MPCostReduction, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEnemy);

            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        // ====================================================================
        // MERCENARY TACTIC IMPLEMENTATIONS
        // ====================================================================

        /// <summary>Blitz tactic for mercenary: aggressive attacks but perform heal/restore like Strategic.
        /// Round 1 only: one self-buff opener cast, then pure aggression (issue #294).</summary>
        private static BattleAction DecideMercBlitz(
            Mercenary merc, IBattlePartyView party, List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries,
            int roundNumber)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (use same thresholds as Strategic)
            BattleAction healAction;
            if (TryMercHealAction(merc, party, livingMercenaries,
                    GameConfig.HeroCriticalHPPercent, StrategicMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Round-1 opener: one buff cast, then pure aggression
            if (roundNumber <= 1)
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, merc, party, livingMercenaries,
                    allowSelfBuffs: true, allowAllyBuffs: true, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            var aoeResult = TryAoESkill(_attackSkillBuffer, merc.CurrentMP, merc.MPCostReduction, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEnemy);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, merc.CurrentMP, merc.MPCostReduction, livingMonsters, true);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEnemy);

            var weaponElement = merc.WeaponShield1?.ElementalProps?.Element ?? ElementType.Neutral;
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Strategic tactic for mercenary: heal, reactive buffs once the battle has
        /// proven dangerous (an ally hit critical HP), then efficient attack.</summary>
        private static BattleAction DecideMercStrategic(
            Mercenary merc, IBattlePartyView party,
            List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries,
            bool battleCriticalReached)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (HP < 40% OR MP < 20%) — healing always wins over buffing
            BattleAction healAction;
            if (TryMercHealAction(merc, party, livingMercenaries,
                    GameConfig.HeroCriticalHPPercent, StrategicMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Reactive buffs (issue #294): ally-targetable buffs once the battle-critical flag
            //    is latched; strictly-self buffs only when this merc themself is critical.
            bool mercCritical = party.IsMercenaryHPCritical(merc);
            if (battleCriticalReached || mercCritical)
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, merc, party, livingMercenaries,
                    allowSelfBuffs: mercCritical, allowAllyBuffs: battleCriticalReached, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            // 3. Attack
            return MercAttack(merc, livingMonsters);
        }

        /// <summary>Defensive tactic for mercenary: heal at 60%, buff, then attack.</summary>
        private static BattleAction DecideMercDefensive(
            Mercenary merc, IBattlePartyView party,
            List<IEnemy> livingMonsters, List<Mercenary> livingMercenaries)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal/restore check (HP < 60% OR MP < 30%)
            BattleAction healAction;
            if (TryMercHealAction(merc, party, livingMercenaries,
                    DefensiveHealThreshold, DefensiveMPThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. Buff
            {
                var buffSkill = FindBestBuffSkill(_buffSkillBuffer, merc, party, livingMercenaries,
                    allowSelfBuffs: true, allowAllyBuffs: true, out var buffTarget);
                if (buffSkill != null)
                    return CreateBuffAction(buffSkill, buffTarget, party);
            }

            // 3. Attack
            return MercAttack(merc, livingMonsters);
        }

        /// <summary>Mercenary attack fallback: AoE check then best single-target or physical.</summary>
        private static BattleAction MercAttack(Mercenary merc, List<IEnemy> livingMonsters)
        {
            var aoeResult = TryAoESkill(_attackSkillBuffer, merc.CurrentMP, merc.MPCostReduction, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEnemy);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, merc.CurrentMP, merc.MPCostReduction, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEnemy);

            var weaponElement = merc.WeaponShield1?.ElementalProps?.Element ?? ElementType.Neutral;
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        // ====================================================================
        // HEALING DECISION HELPERS
        // ====================================================================

        /// <summary>
        /// Attempts to produce a heal action for the hero using the heal priority order.
        /// Uses PotionSelectionEngine battle rules for consumable selection with unified HP/MP scoring.
        /// Returns true and sets healAction if healing was decided.
        /// </summary>
        private static bool TryHeroHealAction(
            IBattlePartyView party, Hero hero, List<Mercenary> livingMercenaries,
            float hpThreshold, float mpThreshold, List<ISkill> healSkills, out BattleAction healAction)
        {
            healAction = default;

            object healTarget;
            bool targetIsHero;
            int targetCurrentHP;
            int targetMaxHP;
            int targetCurrentMP;
            int targetMaxMP;
            if (!GetHealTarget(hero, party, livingMercenaries, hpThreshold, mpThreshold,
                    out healTarget, out targetIsHero, out targetCurrentHP, out targetMaxHP,
                    out targetCurrentMP, out targetMaxMP))
                return false;

            var priorities = party.GetHealPrioritiesInOrder();
            for (int p = 0; p < priorities.Length; p++)
            {
                switch (priorities[p])
                {
                    case HeroHealPriority.Inn:
                        // Cannot use Inn during battle
                        continue;

                    case HeroHealPriority.HealingItem:
                        if (party.HealingItemExhausted) continue;
                        // Check consumable targeting permission
                        if (!targetIsHero && !party.UseConsumablesOnMercenaries) continue;
                        Consumable bestItem;
                        int bestIdx;
                        if (PotionSelectionEngine.SelectBattlePotion(party.Bag,
                            targetCurrentHP, targetMaxHP, targetCurrentMP, targetMaxMP,
                            out bestItem, out bestIdx))
                        {
                            healAction = CreateConsumableAction(bestItem, bestIdx, healTarget, targetIsHero);
                            return true;
                        }
                        continue;

                    case HeroHealPriority.HealingSkill:
                        if (party.HealingSkillExhausted) continue;
                        var skill = FindBestHealingSkill(healSkills, hero, hero.CurrentMP,
                            targetCurrentHP, targetMaxHP, targetIsHero);
                        if (skill != null)
                        {
                            healAction = CreateHealingSkillAction(skill, healTarget, targetIsHero);
                            return true;
                        }
                        continue;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to produce a heal action for a mercenary.
        /// Healing skills are unrestricted; consumable use respects settings.
        /// Uses PotionSelectionEngine battle rules for unified HP/MP potion scoring.
        /// </summary>
        private static bool TryMercHealAction(
            Mercenary merc, IBattlePartyView party, List<Mercenary> livingMercenaries,
            float hpThreshold, float mpThreshold, List<ISkill> healSkills,
            out BattleAction healAction)
        {
            healAction = default;
            var hero = party.Hero;

            object healTarget;
            bool targetIsHero;
            int targetCurrentHP;
            int targetMaxHP;
            int targetCurrentMP;
            int targetMaxMP;
            if (!GetHealTarget(hero, party, livingMercenaries, hpThreshold, mpThreshold,
                    out healTarget, out targetIsHero, out targetCurrentHP, out targetMaxHP,
                    out targetCurrentMP, out targetMaxMP))
                return false;

            // 1. Try healing skill (no restrictions for mercenaries)
            var skill = FindBestHealingSkill(healSkills, merc, merc.CurrentMP,
                targetCurrentHP, targetMaxHP, object.ReferenceEquals(healTarget, merc));
            if (skill != null)
            {
                healAction = CreateHealingSkillAction(skill, healTarget, targetIsHero);
                return true;
            }

            // 2. Try consumable (with restrictions)
            bool canUseConsumable;
            if (targetIsHero)
                canUseConsumable = party.MercenariesCanUseConsumables;
            else
                canUseConsumable = party.MercenariesCanUseConsumables &&
                                   party.UseConsumablesOnMercenaries;

            if (canUseConsumable)
            {
                Consumable bestItem;
                int bestIdx;
                if (PotionSelectionEngine.SelectBattlePotion(party.Bag,
                    targetCurrentHP, targetMaxHP, targetCurrentMP, targetMaxMP,
                    out bestItem, out bestIdx))
                {
                    healAction = CreateConsumableAction(bestItem, bestIdx, healTarget, targetIsHero);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the ally (hero or mercenary) most in need of healing or MP restoration
        /// below the given thresholds. Also considers active burst damage flags via the party view
        /// so that a burst-damaged character triggers healing even if HP is above the raw threshold.
        /// Returns true if a target was found.
        /// </summary>
        private static bool GetHealTarget(
            Hero hero, IBattlePartyView party, List<Mercenary> livingMercenaries,
            float hpThreshold, float mpThreshold,
            out object target, out bool isHero, out int currentHP, out int maxHP,
            out int currentMP, out int maxMP)
        {
            target = null;
            isHero = false;
            currentHP = 0;
            maxHP = 0;
            currentMP = 0;
            maxMP = 0;
            float lowestPercent = 1f;

            // Check hero — burst damage flag is included via IsHeroHPCritical()
            if (hero != null && hero.MaxHP > 0)
            {
                float heroHPPercent = (float)hero.CurrentHP / hero.MaxHP;
                float heroMPPercent = hero.MaxMP > 0 ? (float)hero.CurrentMP / hero.MaxMP : 1f;
                bool hpCritical = heroHPPercent < hpThreshold || party.IsHeroHPCritical();
                bool mpCritical = heroMPPercent < mpThreshold;
                float minPercent = System.Math.Min(heroHPPercent, heroMPPercent);

                if ((hpCritical || mpCritical) && minPercent < lowestPercent)
                {
                    target = hero;
                    isHero = true;
                    currentHP = hero.CurrentHP;
                    maxHP = hero.MaxHP;
                    currentMP = hero.CurrentMP;
                    maxMP = hero.MaxMP;
                    lowestPercent = minPercent;
                }
            }

            // Check mercenaries — burst damage flag is included via IsMercenaryHPCritical()
            if (livingMercenaries != null)
            {
                for (int i = 0; i < livingMercenaries.Count; i++)
                {
                    var merc = livingMercenaries[i];
                    if (merc == null || merc.MaxHP <= 0) continue;

                    float mercHPPercent = (float)merc.CurrentHP / merc.MaxHP;
                    float mercMPPercent = merc.MaxMP > 0 ? (float)merc.CurrentMP / merc.MaxMP : 1f;
                    bool hpCritical = mercHPPercent < hpThreshold || party.IsMercenaryHPCritical(merc);
                    bool mpCritical = mercMPPercent < mpThreshold;
                    float minPercent = System.Math.Min(mercHPPercent, mercMPPercent);

                    if ((hpCritical || mpCritical) && minPercent < lowestPercent)
                    {
                        target = merc;
                        isHero = false;
                        currentHP = merc.CurrentHP;
                        maxHP = merc.MaxHP;
                        currentMP = merc.CurrentMP;
                        maxMP = merc.MaxMP;
                        lowestPercent = minPercent;
                    }
                }
            }

            return target != null;
        }

        // ====================================================================
        // ATTACK SKILL SELECTION
        // ====================================================================

        /// <summary>
        /// Result of finding an attack skill: the skill and the best monster to target.
        /// </summary>
        private struct SkillResult
        {
            public ISkill Skill;
            public IEnemy TargetEnemy;
        }

        /// <summary>
        /// Returns the effective MP cost of a skill after applying the combatant's MPCostReduction.
        /// Mirrors the formula in ICombatant.GetEffectiveMPCost (floor of 1 when rawCost &gt; 0).
        /// </summary>
        private static int EffectiveMPCost(int rawCost, float mpCostReduction)
        {
            if (rawCost <= 0) return 0;
            int reduced = (int)(rawCost * (1f - mpCostReduction));
            return reduced < 1 ? 1 : reduced;
        }

        /// <summary>
        /// Finds the best single-target attack skill from the buffer.
        /// When preferHighMP is true (Blitz), prefers higher MP cost (stronger).
        /// When false (Strategic/Defensive), prefers lower MP cost (efficient).
        /// Always prioritizes elemental advantage.
        /// </summary>
        private static SkillResult FindBestAttackSkill(
            List<ISkill> attackSkills, int currentMP, float mpCostReduction,
            List<IEnemy> livingMonsters, bool preferHighMP)
        {
            var result = new SkillResult();
            float bestScore = -1f;

            for (int i = 0; i < attackSkills.Count; i++)
            {
                var skill = attackSkills[i];
                if (skill.Kind != SkillKind.Active) continue;
                if (skill.TargetType != SkillTargetType.SingleEnemy) continue;
                if (EffectiveMPCost(skill.MPCost, mpCostReduction) > currentMP) continue;

                // Find best monster target for this skill's element
                IEnemy targetEnemy = FindBestMonsterTarget(livingMonsters, skill.Element);
                if (targetEnemy == null) continue;

                float multiplier = ElementalProperties.GetElementalMultiplier(
                    skill.Element, targetEnemy.Element);

                // Score: elemental advantage weighted heavily, then MP cost
                float mpFactor = preferHighMP
                    ? skill.MPCost / 100f
                    : (100f - skill.MPCost) / 100f;
                float score = multiplier * 10f + mpFactor;

                if (score > bestScore)
                {
                    bestScore = score;
                    result.Skill = skill;
                    result.TargetEnemy = targetEnemy;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if an AoE skill should be used. Returns the skill and a representative target
        /// if AoE is recommended, or null skill if not.
        /// AoE is only used when multiple enemies exist and the majority are not resistant.
        /// </summary>
        private static SkillResult TryAoESkill(
            List<ISkill> attackSkills, int currentMP, float mpCostReduction, List<IEnemy> livingMonsters)
        {
            var result = new SkillResult();
            if (livingMonsters.Count < 2) return result;

            ISkill bestAoE = null;
            int bestAoECost = -1;

            for (int i = 0; i < attackSkills.Count; i++)
            {
                var skill = attackSkills[i];
                if (skill.Kind != SkillKind.Active) continue;
                if (skill.TargetType != SkillTargetType.SurroundingEnemies) continue;
                if (EffectiveMPCost(skill.MPCost, mpCostReduction) > currentMP) continue;

                // Count how many enemies resist this element
                int resistCount = 0;
                for (int m = 0; m < livingMonsters.Count; m++)
                {
                    float mult = ElementalProperties.GetElementalMultiplier(
                        skill.Element, livingMonsters[m].Element);
                    if (mult < 1f) resistCount++;
                }

                // Skip if majority resist (> 50%)
                if (resistCount * 2 > livingMonsters.Count) continue;

                // Prefer AoE with highest MP cost (strongest)
                if (bestAoE == null || skill.MPCost > bestAoECost)
                {
                    bestAoE = skill;
                    bestAoECost = skill.MPCost;
                }
            }

            if (bestAoE != null)
            {
                result.Skill = bestAoE;
                // Target the first monster as a representative
                result.TargetEnemy = livingMonsters[0];
            }
            return result;
        }

        // ====================================================================
        // HEALING / BUFF SKILL SELECTION
        // ====================================================================

        /// <summary>
        /// Finds the best healing skill for the given target deficit.
        /// Prefers skills that match the target context (Self for self, SingleAlly for ally, AllAllies).
        /// Picks the skill whose caster-scaled heal amount best matches the deficit (least waste).
        /// </summary>
        private static ISkill FindBestHealingSkill(
            List<ISkill> healSkills, object caster, int currentMP,
            int targetCurrentHP, int targetMaxHP, bool targetIsSelf)
        {
            int hpNeeded = targetMaxHP - targetCurrentHP;
            if (hpNeeded <= 0) return null;

            ISkill bestSkill = null;
            int bestWaste = int.MaxValue;

            float healerMPCostReduction = (caster as ICombatant)?.MPCostReduction ?? 0f;
            for (int i = 0; i < healSkills.Count; i++)
            {
                var skill = healSkills[i];
                if (EffectiveMPCost(skill.MPCost, healerMPCostReduction) > currentMP) continue;
                if (skill.HPRestoreAmount <= 0) continue;

                // Only ally-targetable heals may be redirected to the chosen target;
                // a Self-declared heal is genuinely self-only
                bool compatible = skill.TargetType == SkillTargetType.SingleAlly ||
                                  skill.TargetType == SkillTargetType.AllAllies;

                if (!compatible) continue;

                int waste = System.Math.Abs(SkillHealCalculator.GetAmount(skill, caster) - hpNeeded);

                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestSkill = skill;
                }
            }

            return bestSkill;
        }

        /// <summary>
        /// Finds a buff skill (Self or SingleAlly target, no heal/MP restore) that can be cast,
        /// and selects its target. Self skills target the caster; SingleAlly skills target the
        /// neediest (lowest HP%) living party member not already at the skill's stack cap, so a
        /// buffer spreads multi-turn buffs across the party instead of re-casting on one member.
        /// <paramref name="allowSelfBuffs"/>/<paramref name="allowAllyBuffs"/> filter by target
        /// kind (Strategic gates them differently; Defensive/Blitz pass both true).
        /// Returns the first castable buff skill (with <paramref name="buffTarget"/> set), or null.
        /// </summary>
        private static ISkill FindBestBuffSkill(List<ISkill> buffSkills, ICombatant caster,
            IBattlePartyView party, List<Mercenary> livingMercenaries,
            bool allowSelfBuffs, bool allowAllyBuffs, out ICombatant buffTarget)
        {
            buffTarget = null;
            for (int i = 0; i < buffSkills.Count; i++)
            {
                var skill = buffSkills[i];
                bool isAllyTargetable = skill.TargetType == SkillTargetType.SingleAlly;
                if (isAllyTargetable ? !allowAllyBuffs : !allowSelfBuffs)
                    continue;

                if (EffectiveMPCost(skill.MPCost, caster.MPCostReduction) > caster.CurrentMP)
                    continue;

                var target = isAllyTargetable
                    ? FindNeediestUncappedAlly(skill, party, livingMercenaries)
                    : (HasUncappedBuff(skill, caster) ? caster : null);
                if (target == null) continue;

                buffTarget = target;
                return skill;
            }
            return null;
        }

        /// <summary>
        /// True if the target still has stack headroom for at least one buff this skill grants
        /// (checked per (skill id, buff type) pair — fixes multi-buff skills like FadeSkill).
        /// </summary>
        private static bool HasUncappedBuff(ISkill skill, ICombatant target)
        {
            if (skill.GrantedBuffs.Count == 0) return true;
            for (int b = 0; b < skill.GrantedBuffs.Count; b++)
            {
                var grantedBuff = skill.GrantedBuffs[b];
                if (target.GetBuffStacks(skill.Id, grantedBuff.Type) < grantedBuff.MaxStacks)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Picks the living party member (hero or mercenary) with the lowest HP% among those
        /// not at the skill's stack cap. Hero wins ties so the run-critical character is buffed first.
        /// </summary>
        private static ICombatant FindNeediestUncappedAlly(ISkill skill, IBattlePartyView party,
            List<Mercenary> livingMercenaries)
        {
            ICombatant best = null;
            float bestHpPct = float.MaxValue;

            var hero = party.Hero;
            if (hero != null && hero.CurrentHP > 0 && HasUncappedBuff(skill, hero))
            {
                best = hero;
                bestHpPct = hero.CurrentHP / (float)hero.MaxHP;
            }

            if (livingMercenaries != null)
            {
                for (int i = 0; i < livingMercenaries.Count; i++)
                {
                    var merc = livingMercenaries[i];
                    if (merc == null || merc.CurrentHP <= 0 || !HasUncappedBuff(skill, merc))
                        continue;
                    float hpPct = merc.CurrentHP / (float)merc.MaxHP;
                    if (hpPct < bestHpPct)
                    {
                        best = merc;
                        bestHpPct = hpPct;
                    }
                }
            }

            return best;
        }

        /// <summary>Wraps a chosen buff (skill, target) pair into a UseHealingSkill action.</summary>
        private static BattleAction CreateBuffAction(ISkill skill, ICombatant target, IBattlePartyView party)
        {
            bool targetsHero = ReferenceEquals(target, party.Hero);
            return CreateHealingSkillAction(skill, target, targetsHero);
        }

        // ====================================================================
        // ELEMENTAL & TARGET HELPERS
        // ====================================================================

        /// <summary>Checks if the attack element has an advantage over the target element.</summary>
        private static bool HasElementalAdvantage(ElementType attackElement, ElementType targetElement)
        {
            return ElementalProperties.GetElementalMultiplier(attackElement, targetElement) > 1f;
        }

        /// <summary>
        /// Finds the best monster to target with the given attack element.
        /// Prefers elementally weak targets, then lowest HP as tiebreaker.
        /// </summary>
        private static IEnemy FindBestMonsterTarget(List<IEnemy> livingMonsters, ElementType attackElement)
        {
            if (livingMonsters == null || livingMonsters.Count == 0) return null;

            IEnemy bestTarget = null;
            float bestMultiplier = -1f;
            int lowestHP = int.MaxValue;

            for (int i = 0; i < livingMonsters.Count; i++)
            {
                var enemy = livingMonsters[i];
                float multiplier = ElementalProperties.GetElementalMultiplier(attackElement, enemy.Element);

                if (multiplier > bestMultiplier ||
                    (multiplier == bestMultiplier && enemy.CurrentHP < lowestHP))
                {
                    bestTarget = enemy;
                    bestMultiplier = multiplier;
                    lowestHP = enemy.CurrentHP;
                }
            }

            return bestTarget;
        }

        /// <summary>Extracts the weapon element from the hero's equipped weapon.</summary>
        private static ElementType GetWeaponElementFromHero(Hero hero)
        {
            var gear = hero.WeaponShield1 as IGear;
            if (gear?.ElementalProps != null)
                return gear.ElementalProps.Element;
            return ElementType.Neutral;
        }

        // ====================================================================
        // SKILL COLLECTION
        // ====================================================================

        /// <summary>
        /// Collects all usable skills from the hero into categorised buffers.
        /// Attack skills target enemies; healing skills restore HP; buff skills target self/ally without healing.
        /// </summary>
        private static void CollectHeroSkills(
            Hero hero,
            List<ISkill> attackSkills,
            List<ISkill> healSkills,
            List<ISkill> buffSkills)
        {
            attackSkills.Clear();
            healSkills.Clear();
            buffSkills.Clear();

            // From learned skills
            var enumerator = hero.LearnedSkills.GetEnumerator();
            while (enumerator.MoveNext())
            {
                CategorizeSkill(enumerator.Current.Value, attackSkills, healSkills, buffSkills);
            }

            // From unlocked synergy skills
            var synergies = hero.ActiveSynergies;
            for (int i = 0; i < synergies.Count; i++)
            {
                var synergy = synergies[i];
                if (synergy.IsSkillUnlocked && synergy.Pattern.UnlockedSkill != null)
                    CategorizeSkill(synergy.Pattern.UnlockedSkill, attackSkills, healSkills, buffSkills);
            }
        }

        /// <summary>
        /// Collects all usable skills from a mercenary into categorised buffers.
        /// </summary>
        private static void CollectMercenarySkills(
            Mercenary merc,
            List<ISkill> attackSkills,
            List<ISkill> healSkills,
            List<ISkill> buffSkills)
        {
            attackSkills.Clear();
            healSkills.Clear();
            buffSkills.Clear();

            var enumerator = merc.LearnedSkills.GetEnumerator();
            while (enumerator.MoveNext())
            {
                CategorizeSkill(enumerator.Current.Value, attackSkills, healSkills, buffSkills);
            }
        }

        /// <summary>Categorizes a single active skill into the appropriate buffer.</summary>
        private static void CategorizeSkill(
            ISkill skill,
            List<ISkill> attackSkills,
            List<ISkill> healSkills,
            List<ISkill> buffSkills)
        {
            if (skill.Kind != SkillKind.Active) return;

            // Healing skill: restores HP (may also grant buffs — those apply in BattleEngine healing path)
            if (skill.HPRestoreAmount > 0)
            {
                healSkills.Add(skill);
                return;
            }

            // Attack skill: targets enemies
            if (skill.TargetType == SkillTargetType.SingleEnemy ||
                skill.TargetType == SkillTargetType.SurroundingEnemies)
            {
                attackSkills.Add(skill);
                return;
            }

            // Phase 3: data-driven buff skill — has GrantedBuffs and no HP restore
            if (skill.GrantedBuffs.Count > 0 && skill.HPRestoreAmount == 0)
            {
                buffSkills.Add(skill);
                return;
            }

            // Fallback: non-healing self/ally skill (CleansesDebuffs-only or future kinds)
            if (skill.TargetType == SkillTargetType.Self ||
                skill.TargetType == SkillTargetType.SingleAlly)
            {
                buffSkills.Add(skill);
            }
        }

        // ====================================================================
        // ACTION FACTORY HELPERS
        // ====================================================================

        /// <summary>Creates a physical attack action targeting the given monster (or null for any).</summary>
        private static BattleAction CreatePhysicalAttack(IEnemy targetEnemy)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.PhysicalAttack,
                TargetEnemy = targetEnemy
            };
        }

        /// <summary>Creates an attack skill action targeting the given monster.</summary>
        private static BattleAction CreateAttackSkillAction(ISkill skill, IEnemy targetEnemy)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.UseAttackSkill,
                Skill = skill,
                TargetEnemy = targetEnemy
            };
        }

        /// <summary>Creates a healing or buff skill action targeting the given ally.</summary>
        private static BattleAction CreateHealingSkillAction(ISkill skill, object target, bool targetsHero)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.UseHealingSkill,
                Skill = skill,
                Target = target,
                TargetsHero = targetsHero
            };
        }

        /// <summary>Creates a consumable use action targeting the given ally.</summary>
        private static BattleAction CreateConsumableAction(
            Consumable consumable, int bagIndex, object target, bool targetsHero)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.UseConsumable,
                Consumable = consumable,
                BagIndex = bagIndex,
                Target = target,
                TargetsHero = targetsHero
            };
        }
    }
}

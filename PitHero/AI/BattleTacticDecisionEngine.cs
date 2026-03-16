using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
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

        /// <summary>Entity of the target monster (for attack actions) or null.</summary>
        public Entity TargetEntity;
    }

    /// <summary>
    /// Provides static methods for AI battle decisions based on the selected battle tactic.
    /// Does not execute actions — only returns what action the AI recommends.
    /// </summary>
    public static class BattleTacticDecisionEngine
    {
        private const float DefensiveHealThreshold = 0.6f;
        private const float StrategicMPThreshold = 0.2f;
        private const float DefensiveMPThreshold = 0.3f;
        private const int FullRestoreWastePenalty = 999999;

        // Pre-allocated buffers reused each call (single-threaded game)
        private static readonly List<ISkill> _attackSkillBuffer = new List<ISkill>(32);
        private static readonly List<ISkill> _healSkillBuffer = new List<ISkill>(16);
        private static readonly List<ISkill> _buffSkillBuffer = new List<ISkill>(16);

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Decides what the hero should do this turn based on the current battle tactic.
        /// </summary>
        public static BattleAction DecideHeroAction(
            HeroComponent heroComponent,
            List<Entity> livingMonsters,
            List<Entity> livingMercenaries)
        {
            var hero = heroComponent.LinkedHero;
            if (hero == null || livingMonsters == null || livingMonsters.Count == 0)
                return CreatePhysicalAttack(null);

            switch (heroComponent.CurrentBattleTactic)
            {
                case BattleTactic.Blitz:
                    return DecideBlitz(heroComponent, hero, livingMonsters);

                case BattleTactic.Defensive:
                    return DecideDefensive(heroComponent, hero, livingMonsters, livingMercenaries);

                case BattleTactic.Strategic:
                default:
                    return DecideStrategic(heroComponent, hero, livingMonsters, livingMercenaries);
            }
        }

        /// <summary>
        /// Decides what a mercenary should do during their turn.
        /// Follows the hero's current battle tactic with mercenary-specific targeting restrictions.
        /// </summary>
        public static BattleAction DecideMercenaryAction(
            MercenaryComponent mercComponent,
            HeroComponent heroComponent,
            List<Entity> livingMonsters,
            List<Entity> livingMercenaries)
        {
            var merc = mercComponent.LinkedMercenary;
            if (merc == null || livingMonsters == null || livingMonsters.Count == 0)
                return CreatePhysicalAttack(null);

            switch (heroComponent.CurrentBattleTactic)
            {
                case BattleTactic.Blitz:
                    return DecideMercBlitz(merc, heroComponent, livingMonsters);

                case BattleTactic.Defensive:
                    return DecideMercDefensive(merc, mercComponent, heroComponent, livingMonsters, livingMercenaries);

                case BattleTactic.Strategic:
                default:
                    return DecideMercStrategic(merc, mercComponent, heroComponent, livingMonsters, livingMercenaries);
            }
        }

        // ====================================================================
        // HERO TACTIC IMPLEMENTATIONS
        // ====================================================================

        /// <summary>Blitz: all-out attack, never heal, prioritize elemental weaknesses.</summary>
        private static BattleAction DecideBlitz(
            HeroComponent heroComponent, Hero hero, List<Entity> livingMonsters)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // AoE check first
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEntity);

            // Best single-target attack skill (prefer elemental advantage, then highest MP cost)
            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters, true);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEntity);

            // Fall back to physical attack
            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Strategic: heal at 40%, restore MP at 20%, efficient attacks.</summary>
        private static BattleAction DecideStrategic(
            HeroComponent heroComponent, Hero hero,
            List<Entity> livingMonsters, List<Entity> livingMercenaries)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal check (40% threshold)
            BattleAction healAction;
            if (TryHeroHealAction(heroComponent, hero, livingMercenaries,
                    GameConfig.HeroCriticalHPPercent, _healSkillBuffer, out healAction))
                return healAction;

            // 2. MP restore check (20%)
            if (ShouldRestoreMP(hero.CurrentMP, hero.MaxMP, StrategicMPThreshold))
            {
                int mpNeeded = hero.MaxMP - hero.CurrentMP;
                Consumable mpItem;
                int mpIdx;
                if (FindBestMPConsumable(heroComponent.Bag, mpNeeded, out mpItem, out mpIdx))
                    return CreateConsumableAction(mpItem, mpIdx, hero, true);
            }

            // 3. Attack (prefer elemental advantage, then lowest MP cost)
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEntity);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEntity);

            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Defensive: heal at 60%, restore MP at 30%, buff, then attack only when safe.</summary>
        private static BattleAction DecideDefensive(
            HeroComponent heroComponent, Hero hero,
            List<Entity> livingMonsters, List<Entity> livingMercenaries)
        {
            CollectHeroSkills(hero, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal check (60% threshold)
            BattleAction healAction;
            if (TryHeroHealAction(heroComponent, hero, livingMercenaries,
                    DefensiveHealThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. MP restore check (30%)
            if (ShouldRestoreMP(hero.CurrentMP, hero.MaxMP, DefensiveMPThreshold))
            {
                int mpNeeded = hero.MaxMP - hero.CurrentMP;
                Consumable mpItem;
                int mpIdx;
                if (FindBestMPConsumable(heroComponent.Bag, mpNeeded, out mpItem, out mpIdx))
                    return CreateConsumableAction(mpItem, mpIdx, hero, true);
            }

            // 3. Apply buff if available
            var buffSkill = FindBestBuffSkill(_buffSkillBuffer, hero.CurrentMP);
            if (buffSkill != null)
                return CreateHealingSkillAction(buffSkill, hero, true);

            // 4. Attack (still attack even if not all allies are at 60% — nothing else to do)
            var aoeResult = TryAoESkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEntity);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, hero.CurrentMP, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEntity);

            var weaponElement = GetWeaponElementFromHero(hero);
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        // ====================================================================
        // MERCENARY TACTIC IMPLEMENTATIONS
        // ====================================================================

        /// <summary>Blitz tactic for mercenary: all-out attack.</summary>
        private static BattleAction DecideMercBlitz(
            Mercenary merc, HeroComponent heroComponent, List<Entity> livingMonsters)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            var aoeResult = TryAoESkill(_attackSkillBuffer, merc.CurrentMP, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEntity);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, merc.CurrentMP, livingMonsters, true);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEntity);

            var weaponElement = merc.WeaponShield1?.ElementalProps?.Element ?? ElementType.Neutral;
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        /// <summary>Strategic tactic for mercenary: heal, then efficient attack.</summary>
        private static BattleAction DecideMercStrategic(
            Mercenary merc, MercenaryComponent mercComponent, HeroComponent heroComponent,
            List<Entity> livingMonsters, List<Entity> livingMercenaries)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal check (40% threshold) — skills are unrestricted
            BattleAction healAction;
            if (TryMercHealAction(merc, heroComponent, livingMercenaries, mercComponent,
                    GameConfig.HeroCriticalHPPercent, _healSkillBuffer, out healAction))
                return healAction;

            // 2. MP restore (20%) — consumable restrictions apply
            if (ShouldRestoreMP(merc.CurrentMP, merc.MaxMP, StrategicMPThreshold) &&
                heroComponent.MercenariesCanUseConsumables)
            {
                int mpNeeded = merc.MaxMP - merc.CurrentMP;
                Consumable mpItem;
                int mpIdx;
                if (FindBestMPConsumable(heroComponent.Bag, mpNeeded, out mpItem, out mpIdx))
                    return CreateConsumableAction(mpItem, mpIdx, merc, false);
            }

            // 3. Attack
            return MercAttack(merc, livingMonsters);
        }

        /// <summary>Defensive tactic for mercenary: heal at 60%, buff, then attack.</summary>
        private static BattleAction DecideMercDefensive(
            Mercenary merc, MercenaryComponent mercComponent, HeroComponent heroComponent,
            List<Entity> livingMonsters, List<Entity> livingMercenaries)
        {
            CollectMercenarySkills(merc, _attackSkillBuffer, _healSkillBuffer, _buffSkillBuffer);

            // 1. Heal check (60%)
            BattleAction healAction;
            if (TryMercHealAction(merc, heroComponent, livingMercenaries, mercComponent,
                    DefensiveHealThreshold, _healSkillBuffer, out healAction))
                return healAction;

            // 2. MP restore (30%)
            if (ShouldRestoreMP(merc.CurrentMP, merc.MaxMP, DefensiveMPThreshold) &&
                heroComponent.MercenariesCanUseConsumables)
            {
                int mpNeeded = merc.MaxMP - merc.CurrentMP;
                Consumable mpItem;
                int mpIdx;
                if (FindBestMPConsumable(heroComponent.Bag, mpNeeded, out mpItem, out mpIdx))
                    return CreateConsumableAction(mpItem, mpIdx, merc, false);
            }

            // 3. Buff
            var buffSkill = FindBestBuffSkill(_buffSkillBuffer, merc.CurrentMP);
            if (buffSkill != null)
                return CreateHealingSkillAction(buffSkill, merc, false);

            // 4. Attack
            return MercAttack(merc, livingMonsters);
        }

        /// <summary>Mercenary attack fallback: AoE check then best single-target or physical.</summary>
        private static BattleAction MercAttack(Mercenary merc, List<Entity> livingMonsters)
        {
            var aoeResult = TryAoESkill(_attackSkillBuffer, merc.CurrentMP, livingMonsters);
            if (aoeResult.Skill != null)
                return CreateAttackSkillAction(aoeResult.Skill, aoeResult.TargetEntity);

            var bestSkill = FindBestAttackSkill(_attackSkillBuffer, merc.CurrentMP, livingMonsters, false);
            if (bestSkill.Skill != null)
                return CreateAttackSkillAction(bestSkill.Skill, bestSkill.TargetEntity);

            var weaponElement = merc.WeaponShield1?.ElementalProps?.Element ?? ElementType.Neutral;
            return CreatePhysicalAttack(FindBestMonsterTarget(livingMonsters, weaponElement));
        }

        // ====================================================================
        // HEALING DECISION HELPERS
        // ====================================================================

        /// <summary>
        /// Attempts to produce a heal action for the hero using the heal priority order.
        /// Returns true and sets healAction if healing was decided.
        /// </summary>
        private static bool TryHeroHealAction(
            HeroComponent heroComponent, Hero hero, List<Entity> livingMercenaries,
            float threshold, List<ISkill> healSkills, out BattleAction healAction)
        {
            healAction = default;

            object healTarget;
            bool targetIsHero;
            int targetCurrentHP;
            int targetMaxHP;
            if (!GetHealTarget(hero, livingMercenaries, threshold,
                    out healTarget, out targetIsHero, out targetCurrentHP, out targetMaxHP))
                return false;

            var priorities = heroComponent.GetHealPrioritiesInOrder();
            for (int p = 0; p < priorities.Length; p++)
            {
                switch (priorities[p])
                {
                    case HeroHealPriority.Inn:
                        // Cannot use Inn during battle
                        continue;

                    case HeroHealPriority.HealingItem:
                        if (heroComponent.HealingItemExhausted) continue;
                        // Check consumable targeting permission
                        if (!targetIsHero && !heroComponent.UseConsumablesOnMercenaries) continue;
                        int hpNeeded = targetMaxHP - targetCurrentHP;
                        Consumable hpItem;
                        int hpIdx;
                        if (FindBestHPConsumable(heroComponent.Bag, hpNeeded, out hpItem, out hpIdx))
                        {
                            healAction = CreateConsumableAction(hpItem, hpIdx, healTarget, targetIsHero);
                            return true;
                        }
                        continue;

                    case HeroHealPriority.HealingSkill:
                        if (heroComponent.HealingSkillExhausted) continue;
                        var skill = FindBestHealingSkill(healSkills, hero.CurrentMP,
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
        /// </summary>
        private static bool TryMercHealAction(
            Mercenary merc, HeroComponent heroComponent, List<Entity> livingMercenaries,
            MercenaryComponent selfComp, float threshold, List<ISkill> healSkills,
            out BattleAction healAction)
        {
            healAction = default;
            var hero = heroComponent.LinkedHero;

            object healTarget;
            bool targetIsHero;
            int targetCurrentHP;
            int targetMaxHP;
            if (!GetHealTarget(hero, livingMercenaries, threshold,
                    out healTarget, out targetIsHero, out targetCurrentHP, out targetMaxHP))
                return false;

            // 1. Try healing skill (no restrictions for mercenaries)
            var skill = FindBestHealingSkill(healSkills, merc.CurrentMP,
                targetCurrentHP, targetMaxHP, object.ReferenceEquals(healTarget, merc));
            if (skill != null)
            {
                healAction = CreateHealingSkillAction(skill, healTarget, targetIsHero);
                return true;
            }

            // 2. Try consumable (with restrictions)
            bool canUseConsumable;
            if (targetIsHero)
                canUseConsumable = heroComponent.MercenariesCanUseConsumables;
            else
                canUseConsumable = heroComponent.MercenariesCanUseConsumables &&
                                   heroComponent.UseConsumablesOnMercenaries;

            if (canUseConsumable)
            {
                int hpNeeded = targetMaxHP - targetCurrentHP;
                Consumable hpItem;
                int hpIdx;
                if (FindBestHPConsumable(heroComponent.Bag, hpNeeded, out hpItem, out hpIdx))
                {
                    healAction = CreateConsumableAction(hpItem, hpIdx, healTarget, targetIsHero);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the ally (hero or mercenary) most in need of healing below the given threshold.
        /// Returns true if a target was found.
        /// </summary>
        private static bool GetHealTarget(
            Hero hero, List<Entity> livingMercenaries, float threshold,
            out object target, out bool isHero, out int currentHP, out int maxHP)
        {
            target = null;
            isHero = false;
            currentHP = 0;
            maxHP = 0;
            float lowestPercent = 1f;

            // Check hero
            if (hero != null && hero.MaxHP > 0)
            {
                float heroPercent = (float)hero.CurrentHP / hero.MaxHP;
                if (heroPercent < threshold && heroPercent < lowestPercent)
                {
                    target = hero;
                    isHero = true;
                    currentHP = hero.CurrentHP;
                    maxHP = hero.MaxHP;
                    lowestPercent = heroPercent;
                }
            }

            // Check mercenaries
            if (livingMercenaries != null)
            {
                for (int i = 0; i < livingMercenaries.Count; i++)
                {
                    var mercComp = livingMercenaries[i].GetComponent<MercenaryComponent>();
                    if (mercComp == null) continue;
                    var merc = mercComp.LinkedMercenary;
                    if (merc == null || merc.MaxHP <= 0) continue;

                    float mercPercent = (float)merc.CurrentHP / merc.MaxHP;
                    if (mercPercent < threshold && mercPercent < lowestPercent)
                    {
                        target = merc;
                        isHero = false;
                        currentHP = merc.CurrentHP;
                        maxHP = merc.MaxHP;
                        lowestPercent = mercPercent;
                    }
                }
            }

            return target != null;
        }

        /// <summary>Determines if a character needs healing based on HP percent and threshold.</summary>
        private static bool ShouldHeal(int currentHP, int maxHP, float threshold)
        {
            if (maxHP <= 0) return false;
            return (float)currentHP / maxHP < threshold;
        }

        /// <summary>Determines if MP restoration is needed based on current MP percent and threshold.</summary>
        private static bool ShouldRestoreMP(int currentMP, int maxMP, float threshold)
        {
            if (maxMP <= 0) return false;
            return (float)currentMP / maxMP < threshold;
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
            public Entity TargetEntity;
        }

        /// <summary>
        /// Finds the best single-target attack skill from the buffer.
        /// When preferHighMP is true (Blitz), prefers higher MP cost (stronger).
        /// When false (Strategic/Defensive), prefers lower MP cost (efficient).
        /// Always prioritizes elemental advantage.
        /// </summary>
        private static SkillResult FindBestAttackSkill(
            List<ISkill> attackSkills, int currentMP,
            List<Entity> livingMonsters, bool preferHighMP)
        {
            var result = new SkillResult();
            float bestScore = -1f;

            for (int i = 0; i < attackSkills.Count; i++)
            {
                var skill = attackSkills[i];
                if (skill.Kind != SkillKind.Active) continue;
                if (skill.TargetType != SkillTargetType.SingleEnemy) continue;
                if (skill.MPCost > currentMP) continue;

                // Find best monster target for this skill's element
                Entity targetEntity = FindBestMonsterTarget(livingMonsters, skill.Element);
                if (targetEntity == null) continue;

                var enemyComp = targetEntity.GetComponent<EnemyComponent>();
                if (enemyComp == null) continue;

                float multiplier = ElementalProperties.GetElementalMultiplier(
                    skill.Element, enemyComp.Enemy.Element);

                // Score: elemental advantage weighted heavily, then MP cost
                float mpFactor = preferHighMP
                    ? skill.MPCost / 100f
                    : (100f - skill.MPCost) / 100f;
                float score = multiplier * 10f + mpFactor;

                if (score > bestScore)
                {
                    bestScore = score;
                    result.Skill = skill;
                    result.TargetEntity = targetEntity;
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
            List<ISkill> attackSkills, int currentMP, List<Entity> livingMonsters)
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
                if (skill.MPCost > currentMP) continue;

                // Count how many enemies resist this element
                int resistCount = 0;
                for (int m = 0; m < livingMonsters.Count; m++)
                {
                    var ec = livingMonsters[m].GetComponent<EnemyComponent>();
                    if (ec == null) continue;
                    float mult = ElementalProperties.GetElementalMultiplier(
                        skill.Element, ec.Enemy.Element);
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
                result.TargetEntity = livingMonsters[0];
            }
            return result;
        }

        // ====================================================================
        // HEALING / BUFF SKILL SELECTION
        // ====================================================================

        /// <summary>
        /// Finds the best healing skill for the given target deficit.
        /// Prefers skills that match the target context (Self for self, SingleAlly for ally, AllAllies).
        /// Picks the skill whose HPRestoreAmount best matches the deficit (least waste).
        /// </summary>
        private static ISkill FindBestHealingSkill(
            List<ISkill> healSkills, int currentMP,
            int targetCurrentHP, int targetMaxHP, bool targetIsSelf)
        {
            int hpNeeded = targetMaxHP - targetCurrentHP;
            if (hpNeeded <= 0) return null;

            ISkill bestSkill = null;
            int bestWaste = int.MaxValue;

            for (int i = 0; i < healSkills.Count; i++)
            {
                var skill = healSkills[i];
                if (skill.MPCost > currentMP) continue;
                if (skill.HPRestoreAmount <= 0) continue;

                // Check target type compatibility
                // Self-targeted healing skills can also be used on allies during battle
                // (the AI redirects the heal to the target in battle execution)
                bool compatible = false;
                if (skill.TargetType == SkillTargetType.Self)
                    compatible = true;
                if (skill.TargetType == SkillTargetType.SingleAlly)
                    compatible = true;
                if (skill.TargetType == SkillTargetType.AllAllies)
                    compatible = true;

                if (!compatible) continue;

                int waste = System.Math.Abs(skill.HPRestoreAmount - hpNeeded);

                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestSkill = skill;
                }
            }

            return bestSkill;
        }

        /// <summary>
        /// Finds a buff skill (Self or SingleAlly target, no heal/MP restore) that can be cast.
        /// Returns the first available buff skill, or null if none.
        /// </summary>
        private static ISkill FindBestBuffSkill(List<ISkill> buffSkills, int currentMP)
        {
            for (int i = 0; i < buffSkills.Count; i++)
            {
                var skill = buffSkills[i];
                if (skill.MPCost <= currentMP)
                    return skill;
            }
            return null;
        }

        // ====================================================================
        // CONSUMABLE SELECTION
        // ====================================================================

        /// <summary>
        /// Finds the most efficient HP-restoring consumable from the bag.
        /// Minimizes waste (restore amount - HP needed). Full-heal potions (-1) are saved for emergencies.
        /// </summary>
        private static bool FindBestHPConsumable(
            ItemBag bag, int hpNeeded,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;
            int bestWaste = int.MaxValue;

            if (bag == null || hpNeeded <= 0) return false;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var item = bag.GetSlotItem(i);
                var consumable = item as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;
                if (consumable.HPRestoreAmount == 0) continue;

                int waste;
                if (consumable.HPRestoreAmount == -1)
                {
                    // Full heal: penalise to save for emergencies
                    waste = FullRestoreWastePenalty;
                }
                else
                {
                    waste = System.Math.Abs(consumable.HPRestoreAmount - hpNeeded);
                }

                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestItem = consumable;
                    bestIndex = i;
                }
            }

            return bestItem != null;
        }

        /// <summary>
        /// Finds the most efficient MP-restoring consumable from the bag.
        /// Same logic as HP consumable selection but applied to MP values.
        /// </summary>
        private static bool FindBestMPConsumable(
            ItemBag bag, int mpNeeded,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;
            int bestWaste = int.MaxValue;

            if (bag == null || mpNeeded <= 0) return false;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var item = bag.GetSlotItem(i);
                var consumable = item as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;
                if (consumable.MPRestoreAmount == 0) continue;

                int waste;
                if (consumable.MPRestoreAmount == -1)
                {
                    // Full restore: penalise to save for emergencies
                    waste = FullRestoreWastePenalty;
                }
                else
                {
                    waste = System.Math.Abs(consumable.MPRestoreAmount - mpNeeded);
                }

                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestItem = consumable;
                    bestIndex = i;
                }
            }

            return bestItem != null;
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
        private static Entity FindBestMonsterTarget(List<Entity> livingMonsters, ElementType attackElement)
        {
            if (livingMonsters == null || livingMonsters.Count == 0) return null;

            Entity bestTarget = null;
            float bestMultiplier = -1f;
            int lowestHP = int.MaxValue;

            for (int i = 0; i < livingMonsters.Count; i++)
            {
                var entity = livingMonsters[i];
                var enemyComp = entity.GetComponent<EnemyComponent>();
                if (enemyComp == null) continue;

                var enemy = enemyComp.Enemy;
                float multiplier = ElementalProperties.GetElementalMultiplier(attackElement, enemy.Element);

                if (multiplier > bestMultiplier ||
                    (multiplier == bestMultiplier && enemy.CurrentHP < lowestHP))
                {
                    bestTarget = entity;
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

            // Healing skill: restores HP
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

            // Buff skill: targets self or ally without healing
            if (skill.TargetType == SkillTargetType.Self ||
                skill.TargetType == SkillTargetType.SingleAlly)
            {
                buffSkills.Add(skill);
            }
        }

        // ====================================================================
        // ACTION FACTORY HELPERS
        // ====================================================================

        /// <summary>Creates a physical attack action targeting the given monster entity.</summary>
        private static BattleAction CreatePhysicalAttack(Entity targetEntity)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.PhysicalAttack,
                TargetEntity = targetEntity
            };
        }

        /// <summary>Creates an attack skill action targeting the given monster entity.</summary>
        private static BattleAction CreateAttackSkillAction(ISkill skill, Entity targetEntity)
        {
            return new BattleAction
            {
                Kind = BattleAction.ActionKind.UseAttackSkill,
                Skill = skill,
                TargetEntity = targetEntity
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

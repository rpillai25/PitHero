using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.AI
{
    /// <summary>
    /// Centralized potion selection logic for both battle and out-of-battle contexts.
    /// Battle rules prioritize turn economy with weighted scoring.
    /// Out-of-battle rules minimize total waste with no turn cost pressure.
    /// </summary>
    public static class PotionSelectionEngine
    {
        /// <summary>HP percent below which emergency survival override activates in battle.</summary>
        public const float EmergencyHPThreshold = 0.20f;

        /// <summary>HP percent below which HP is considered critical (weight doubles).</summary>
        public const float CriticalHPThreshold = 0.40f;

        /// <summary>MP percent below which MP is considered critical (weight doubles).</summary>
        public const float CriticalMPThreshold = 0.50f;

        /// <summary>Weight applied to resource restoration when that resource is not critical.</summary>
        private const float NormalWeight = 1.0f;

        /// <summary>Weight applied to resource restoration when that resource is critical.</summary>
        private const float CriticalWeight = 2.0f;

        /// <summary>Minimum deficit percent required to justify using a Full Potion outside emergency.</summary>
        private const float FullPotionMinDeficitPercent = 0.50f;

        // ====================================================================
        // BATTLE POTION SELECTION
        // ====================================================================

        /// <summary>
        /// Selects the best potion for battle context using weighted scoring.
        /// Emergency survival override: if HP is extremely low, picks the potion with maximum HP restoration.
        /// Otherwise uses weighted scoring: Score = EffectiveHP * HPWeight + EffectiveMP * MPWeight.
        /// </summary>
        public static bool SelectBattlePotion(
            ItemBag bag, int currentHP, int maxHP, int currentMP, int maxMP,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;

            if (bag == null || maxHP <= 0) return false;

            int missingHP = maxHP - currentHP;
            int missingMP = maxMP > 0 ? maxMP - currentMP : 0;

            // Nothing to restore
            if (missingHP <= 0 && missingMP <= 0) return false;

            float hpPercent = (float)currentHP / maxHP;
            float mpPercent = maxMP > 0 ? (float)currentMP / maxMP : 1f;

            // Emergency survival override: HP extremely low
            if (hpPercent < EmergencyHPThreshold)
            {
                return SelectEmergencyPotion(bag, maxHP, out bestItem, out bestIndex);
            }

            // Weighted scoring system
            float hpWeight = hpPercent < CriticalHPThreshold ? CriticalWeight : NormalWeight;
            float mpWeight = mpPercent < CriticalMPThreshold ? CriticalWeight : NormalWeight;

            float bestScore = -1f;
            int bestEffectiveHP = -1;
            int bestTotalWaste = int.MaxValue;
            int bestPrice = int.MaxValue;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var consumable = bag.GetSlotItem(i) as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;

                // Must restore at least one resource
                bool restoresHP = consumable.HPRestoreAmount != 0;
                bool restoresMP = consumable.MPRestoreAmount != 0;
                if (!restoresHP && !restoresMP) continue;

                // Full potions rule: skip full potions unless deficit is large enough
                if (!IsFullPotionJustified(consumable, currentHP, maxHP, currentMP, maxMP))
                    continue;

                // Calculate effective healing (capped at missing amount)
                int potionHP = GetEffectiveRestoreAmount(consumable.HPRestoreAmount, maxHP);
                int potionMP = GetEffectiveRestoreAmount(consumable.MPRestoreAmount, maxMP);
                int effectiveHP = System.Math.Min(potionHP, missingHP);
                int effectiveMP = System.Math.Min(potionMP, missingMP);

                // Must provide at least some benefit
                if (effectiveHP <= 0 && effectiveMP <= 0) continue;

                float score = (effectiveHP * hpWeight) + (effectiveMP * mpWeight);
                int totalWaste = (potionHP - effectiveHP) + (potionMP - effectiveMP);

                // Selection: highest score, then tie-breakers
                if (score > bestScore ||
                    (score == bestScore && effectiveHP > bestEffectiveHP) ||
                    (score == bestScore && effectiveHP == bestEffectiveHP && totalWaste < bestTotalWaste) ||
                    (score == bestScore && effectiveHP == bestEffectiveHP && totalWaste == bestTotalWaste && consumable.Price < bestPrice))
                {
                    bestScore = score;
                    bestEffectiveHP = effectiveHP;
                    bestTotalWaste = totalWaste;
                    bestPrice = consumable.Price;
                    bestItem = consumable;
                    bestIndex = i;
                }
            }

            return bestItem != null;
        }

        /// <summary>
        /// Emergency potion selection: picks the potion that restores the most HP immediately.
        /// Ignores MP, waste, and efficiency. Allows full potions regardless of deficit.
        /// </summary>
        private static bool SelectEmergencyPotion(
            ItemBag bag, int maxHP,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;
            int bestRawHP = -1;
            int bestPrice = int.MaxValue;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var consumable = bag.GetSlotItem(i) as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;

                int rawHP = GetEffectiveRestoreAmount(consumable.HPRestoreAmount, maxHP);
                if (rawHP <= 0) continue;

                // Prefer highest HP, then cheaper potion as tie-breaker
                if (rawHP > bestRawHP ||
                    (rawHP == bestRawHP && consumable.Price < bestPrice))
                {
                    bestRawHP = rawHP;
                    bestPrice = consumable.Price;
                    bestItem = consumable;
                    bestIndex = i;
                }
            }

            return bestItem != null;
        }

        // ====================================================================
        // OUT-OF-BATTLE POTION SELECTION
        // ====================================================================

        /// <summary>
        /// Selects the best potion for out-of-battle context. Minimizes total waste.
        /// When both HP and MP are needed, compares MixPotion waste vs separate potions waste.
        /// </summary>
        public static bool SelectOutOfBattlePotion(
            ItemBag bag, int currentHP, int maxHP, int currentMP, int maxMP,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;

            if (bag == null || maxHP <= 0) return false;

            int missingHP = maxHP - currentHP;
            int missingMP = maxMP > 0 ? maxMP - currentMP : 0;

            // Nothing to restore
            if (missingHP <= 0 && missingMP <= 0) return false;

            bool needsHP = missingHP > 0;
            bool needsMP = missingMP > 0;

            if (needsHP && needsMP)
            {
                return SelectDualResourcePotion(bag, missingHP, missingMP, maxHP, maxMP, currentHP, currentMP, out bestItem, out bestIndex);
            }
            else if (needsHP)
            {
                return SelectSingleResourcePotion(bag, missingHP, maxHP, currentHP, true, out bestItem, out bestIndex);
            }
            else
            {
                return SelectSingleResourcePotion(bag, missingMP, maxMP, currentMP, false, out bestItem, out bestIndex);
            }
        }

        /// <summary>
        /// Single resource case: use the smallest potion that sufficiently heals.
        /// Priority: exact/near-exact fit, avoid large overheal.
        /// Falls back to largest available if nothing can fully cover the deficit.
        /// </summary>
        private static bool SelectSingleResourcePotion(
            ItemBag bag, int missing, int maxStat, int currentStat, bool isHP,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;
            int bestWaste = int.MaxValue;
            int bestPrice = int.MaxValue;

            // Also track best partial (largest available) as fallback
            Consumable bestPartial = null;
            int bestPartialIndex = -1;
            int bestPartialAmount = -1;
            int bestPartialPrice = int.MaxValue;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var consumable = bag.GetSlotItem(i) as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;
                if (consumable.BattleOnly) continue;

                int restoreAmount = isHP ? consumable.HPRestoreAmount : consumable.MPRestoreAmount;
                if (restoreAmount == 0) continue;

                // Full potions rule: only use full potions when deficit is large enough
                if (!IsFullPotionJustifiedSingle(restoreAmount, currentStat, maxStat))
                    continue;

                int effectiveAmount = GetEffectiveRestoreAmount(restoreAmount, maxStat);
                if (effectiveAmount <= 0) continue;

                int capped = System.Math.Min(effectiveAmount, missing);
                int waste = effectiveAmount - capped;

                // Sufficient: covers the full deficit
                if (effectiveAmount >= missing)
                {
                    // Prefer lowest waste, then cheaper
                    if (waste < bestWaste ||
                        (waste == bestWaste && consumable.Price < bestPrice))
                    {
                        bestWaste = waste;
                        bestPrice = consumable.Price;
                        bestItem = consumable;
                        bestIndex = i;
                    }
                }
                else
                {
                    // Partial: track largest for fallback
                    if (effectiveAmount > bestPartialAmount ||
                        (effectiveAmount == bestPartialAmount && consumable.Price < bestPartialPrice))
                    {
                        bestPartialAmount = effectiveAmount;
                        bestPartialPrice = consumable.Price;
                        bestPartial = consumable;
                        bestPartialIndex = i;
                    }
                }
            }

            // If no sufficient potion found, use the best partial
            if (bestItem == null && bestPartial != null)
            {
                bestItem = bestPartial;
                bestIndex = bestPartialIndex;
            }

            return bestItem != null;
        }

        /// <summary>
        /// Dual resource case: compares MixPotion waste vs separate potions waste.
        /// Step 1: Find best MixPotion (restores both HP and MP) and compute TotalMixWaste.
        /// Step 2: Find best separate HP potion + MP potion and compute TotalSeparateWaste.
        /// Step 3: Choose option with lower total waste. Tie-break: prefer separate potions.
        /// Returns the single best potion to use this action (if separate, picks the more critical resource first).
        /// </summary>
        private static bool SelectDualResourcePotion(
            ItemBag bag, int missingHP, int missingMP, int maxHP, int maxMP, int currentHP, int currentMP,
            out Consumable bestItem, out int bestIndex)
        {
            bestItem = null;
            bestIndex = -1;

            // Find best MixPotion (restores both)
            Consumable bestMix = null;
            int bestMixIndex = -1;
            int bestMixWaste = int.MaxValue;
            int bestMixPrice = int.MaxValue;

            // Find best HP-only potion
            Consumable bestHP = null;
            int bestHPIndex = -1;
            int bestHPWaste = int.MaxValue;
            int bestHPPrice = int.MaxValue;
            int bestHPPartialAmount = -1;

            // Find best MP-only potion
            Consumable bestMP = null;
            int bestMPIndex = -1;
            int bestMPWaste = int.MaxValue;
            int bestMPPrice = int.MaxValue;
            int bestMPPartialAmount = -1;

            for (int i = 0; i < bag.Capacity; i++)
            {
                var consumable = bag.GetSlotItem(i) as Consumable;
                if (consumable == null) continue;
                if (consumable.StackCount <= 0) continue;
                if (consumable.BattleOnly) continue;

                // Full potions rule
                if (!IsFullPotionJustified(consumable, currentHP, maxHP, currentMP, maxMP))
                    continue;

                int hpAmount = GetEffectiveRestoreAmount(consumable.HPRestoreAmount, maxHP);
                int mpAmount = GetEffectiveRestoreAmount(consumable.MPRestoreAmount, maxMP);

                bool hasHP = hpAmount > 0;
                bool hasMP = mpAmount > 0;

                if (hasHP && hasMP)
                {
                    // MixPotion candidate
                    int hpWaste = System.Math.Max(0, hpAmount - missingHP);
                    int mpWaste = System.Math.Max(0, mpAmount - missingMP);
                    int totalWaste = hpWaste + mpWaste;

                    if (totalWaste < bestMixWaste ||
                        (totalWaste == bestMixWaste && consumable.Price < bestMixPrice))
                    {
                        bestMixWaste = totalWaste;
                        bestMixPrice = consumable.Price;
                        bestMix = consumable;
                        bestMixIndex = i;
                    }
                }

                if (hasHP && !hasMP)
                {
                    // HP-only candidate
                    int waste = System.Math.Max(0, hpAmount - missingHP);
                    if (hpAmount >= missingHP)
                    {
                        if (waste < bestHPWaste ||
                            (waste == bestHPWaste && consumable.Price < bestHPPrice))
                        {
                            bestHPWaste = waste;
                            bestHPPrice = consumable.Price;
                            bestHP = consumable;
                            bestHPIndex = i;
                        }
                    }
                    else
                    {
                        // Partial: track largest for fallback
                        if (hpAmount > bestHPPartialAmount ||
                            (hpAmount == bestHPPartialAmount && consumable.Price < bestHPPrice))
                        {
                            bestHPPartialAmount = hpAmount;
                            bestHPWaste = waste;
                            bestHPPrice = consumable.Price;
                            bestHP = consumable;
                            bestHPIndex = i;
                        }
                    }
                }

                if (hasMP && !hasHP)
                {
                    // MP-only candidate
                    int waste = System.Math.Max(0, mpAmount - missingMP);
                    if (mpAmount >= missingMP)
                    {
                        if (waste < bestMPWaste ||
                            (waste == bestMPWaste && consumable.Price < bestMPPrice))
                        {
                            bestMPWaste = waste;
                            bestMPPrice = consumable.Price;
                            bestMP = consumable;
                            bestMPIndex = i;
                        }
                    }
                    else
                    {
                        // Partial: track largest for fallback
                        if (mpAmount > bestMPPartialAmount ||
                            (mpAmount == bestMPPartialAmount && consumable.Price < bestMPPrice))
                        {
                            bestMPPartialAmount = mpAmount;
                            bestMPWaste = waste;
                            bestMPPrice = consumable.Price;
                            bestMP = consumable;
                            bestMPIndex = i;
                        }
                    }
                }
            }

            // Compare MixPotion vs separate potions
            bool hasMix = bestMix != null;
            bool hasSeparateHP = bestHP != null;
            bool hasSeparateMP = bestMP != null;
            bool hasBothSeparate = hasSeparateHP && hasSeparateMP;

            if (hasMix && hasBothSeparate)
            {
                int totalSeparateWaste = bestHPWaste + bestMPWaste;

                if (bestMixWaste < totalSeparateWaste)
                {
                    // MixPotion is more efficient
                    bestItem = bestMix;
                    bestIndex = bestMixIndex;
                }
                else if (totalSeparateWaste < bestMixWaste)
                {
                    // Separate is more efficient; pick the more critical resource first
                    SelectMoreCriticalResource(currentHP, maxHP, currentMP, maxMP,
                        bestHP, bestHPIndex, bestMP, bestMPIndex,
                        out bestItem, out bestIndex);
                }
                else
                {
                    // Tied: prefer separate potions (resource flexibility)
                    SelectMoreCriticalResource(currentHP, maxHP, currentMP, maxMP,
                        bestHP, bestHPIndex, bestMP, bestMPIndex,
                        out bestItem, out bestIndex);
                }
            }
            else if (hasMix)
            {
                bestItem = bestMix;
                bestIndex = bestMixIndex;
            }
            else if (hasSeparateHP && hasSeparateMP)
            {
                // No mix available; pick the more critical resource first
                SelectMoreCriticalResource(currentHP, maxHP, currentMP, maxMP,
                    bestHP, bestHPIndex, bestMP, bestMPIndex,
                    out bestItem, out bestIndex);
            }
            else if (hasSeparateHP)
            {
                bestItem = bestHP;
                bestIndex = bestHPIndex;
            }
            else if (hasSeparateMP)
            {
                bestItem = bestMP;
                bestIndex = bestMPIndex;
            }

            return bestItem != null;
        }

        /// <summary>
        /// Picks the potion for the more critical resource (lower percent = more critical).
        /// HP is preferred when percentages are equal (survival bias).
        /// </summary>
        private static void SelectMoreCriticalResource(
            int currentHP, int maxHP, int currentMP, int maxMP,
            Consumable hpItem, int hpIndex, Consumable mpItem, int mpIndex,
            out Consumable result, out int resultIndex)
        {
            float hpPercent = maxHP > 0 ? (float)currentHP / maxHP : 1f;
            float mpPercent = maxMP > 0 ? (float)currentMP / maxMP : 1f;

            // HP preferred when equal (survival bias)
            if (hpPercent <= mpPercent)
            {
                result = hpItem;
                resultIndex = hpIndex;
            }
            else
            {
                result = mpItem;
                resultIndex = mpIndex;
            }
        }

        // ====================================================================
        // SHARED HELPERS
        // ====================================================================

        /// <summary>
        /// Resolves a potion's restore amount to an effective integer value.
        /// -1 (full restore) maps to the max stat value.
        /// 0 means no restoration for that stat.
        /// </summary>
        public static int GetEffectiveRestoreAmount(int restoreAmount, int maxStat)
        {
            if (restoreAmount == -1) return maxStat;
            if (restoreAmount < 0) return 0;
            return restoreAmount;
        }

        /// <summary>
        /// Determines whether a full potion is justified given current HP/MP state.
        /// Full potions are only allowed when the deficit is at least FullPotionMinDeficitPercent of max.
        /// Non-full potions always pass this check.
        /// </summary>
        private static bool IsFullPotionJustified(
            Consumable consumable, int currentHP, int maxHP, int currentMP, int maxMP)
        {
            bool isFullHP = consumable.HPRestoreAmount == -1;
            bool isFullMP = consumable.MPRestoreAmount == -1;

            // Not a full potion at all
            if (!isFullHP && !isFullMP) return true;

            // Check HP full potion justification
            if (isFullHP && maxHP > 0)
            {
                float hpDeficitPercent = (float)(maxHP - currentHP) / maxHP;
                if (hpDeficitPercent >= FullPotionMinDeficitPercent) return true;
            }

            // Check MP full potion justification
            if (isFullMP && maxMP > 0)
            {
                float mpDeficitPercent = (float)(maxMP - currentMP) / maxMP;
                if (mpDeficitPercent >= FullPotionMinDeficitPercent) return true;
            }

            // Full potion with no justified resource
            // If it's a FullMixPotion (-1, -1), either resource meeting threshold justifies it
            // If only one side is full and that side doesn't meet threshold, reject
            return false;
        }

        /// <summary>
        /// Simplified full potion check for single-resource context.
        /// </summary>
        private static bool IsFullPotionJustifiedSingle(int restoreAmount, int currentStat, int maxStat)
        {
            if (restoreAmount != -1) return true;
            if (maxStat <= 0) return false;
            float deficitPercent = (float)(maxStat - currentStat) / maxStat;
            return deficitPercent >= FullPotionMinDeficitPercent;
        }
    }
}

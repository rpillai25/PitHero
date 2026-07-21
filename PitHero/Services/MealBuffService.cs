using System.Collections.Generic;
using PitHero.Dining;
using RolePlayingFramework.Combat;

namespace PitHero.Services
{
    /// <summary>
    /// Holds each party member's active food buffs for the day (issue #319). Battle buffs are
    /// cleared at every battle boundary, so this service re-injects meal buffs at each battle
    /// start as BattleBuffs with RemainingTurns = -1 (until battle end). Records clear at the
    /// 6 AM daily reset. Food grants buffs only — HP/MP recovery is the inn's job.
    /// </summary>
    public sealed class MealBuffService
    {
        /// <summary>SourceSkillId stamped on injected meal buffs.</summary>
        public const string MealBuffSourceId = "meal";

        private struct MealRecord
        {
            public ICombatant Combatant;
            public DishType Dish;
            public bool Deluxe;
        }

        private readonly List<MealRecord> _records = new List<MealRecord>(3);

        /// <summary>
        /// Applies a finished meal: records the dish's buffs for injection into every battle
        /// until the next 6 AM reset.
        /// </summary>
        public void ApplyMeal(ICombatant combatant, DishType dish, bool deluxe)
            => RestoreRecord(combatant, dish, deluxe);

        /// <summary>Records a meal's day-long buffs (also the save-load path).</summary>
        public void RestoreRecord(ICombatant combatant, DishType dish, bool deluxe)
        {
            if (combatant == null) return;

            // One meal per member per day — replace any existing record for this combatant
            for (int i = 0; i < _records.Count; i++)
            {
                if (ReferenceEquals(_records[i].Combatant, combatant))
                {
                    _records[i] = new MealRecord { Combatant = combatant, Dish = dish, Deluxe = deluxe };
                    return;
                }
            }
            _records.Add(new MealRecord { Combatant = combatant, Dish = dish, Deluxe = deluxe });
        }

        /// <summary>Returns true and the active meal for the combatant, if any.</summary>
        public bool TryGetMeal(ICombatant combatant, out DishType dish, out bool deluxe)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (ReferenceEquals(_records[i].Combatant, combatant))
                {
                    dish = _records[i].Dish;
                    deluxe = _records[i].Deluxe;
                    return true;
                }
            }
            dish = default;
            deluxe = false;
            return false;
        }

        /// <summary>
        /// Injects the combatant's active meal buffs as until-battle-end BattleBuffs. Called at
        /// battle start after ClearBattleState. Pure list writes — consumes no battle RNG.
        /// </summary>
        public void InjectBuffsAtBattleStart(ICombatant combatant)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (!ReferenceEquals(_records[i].Combatant, combatant))
                    continue;

                var def = DishConfig.GetDefinition(_records[i].Dish);
                bool deluxe = _records[i].Deluxe;
                for (int b = 0; b < def.Buffs.Length; b++)
                {
                    int magnitude = deluxe
                        ? DishConfig.GetDeluxeMagnitude(def.Buffs[b].Magnitude)
                        : def.Buffs[b].Magnitude;
                    combatant.AddBattleBuff(new BattleBuff(def.Buffs[b].Type, magnitude, -1, MealBuffSourceId));
                }
                return;
            }
        }

        /// <summary>Removes a single combatant's meal record (e.g. mercenary dismissed).</summary>
        public void ClearFor(ICombatant combatant)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_records[i].Combatant, combatant))
                    _records.RemoveAt(i);
            }
        }

        /// <summary>Clears all meal records — the 6 AM daily reset.</summary>
        public void ClearAll() => _records.Clear();
    }
}

using System.Collections.Generic;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Shared, allocation-free battle-buff container used by both <c>Hero</c> and <c>Mercenary</c>.
    /// Pre-allocates a fixed-capacity list and provides the full buff API so the two combatant
    /// classes can delegate instead of duplicating identical code.
    /// </summary>
    public sealed class BattleBuffSet
    {
        private readonly List<BattleBuff> _buffs = new List<BattleBuff>(8);

        /// <summary>Adds a battle buff. Each entry is tracked individually to support stacks.</summary>
        public void AddBattleBuff(in BattleBuff buff)
        {
            _buffs.Add(buff);
        }

        /// <summary>Returns the summed magnitude of all active buffs of the given type.</summary>
        public int GetBuffTotal(BuffType type)
        {
            int total = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Type == type)
                    total += _buffs[i].Magnitude;
            }
            return total;
        }

        /// <summary>
        /// Returns the number of active buff entries whose <see cref="BattleBuff.SourceSkillId"/>
        /// matches <paramref name="sourceSkillId"/> AND whose <see cref="BattleBuff.Type"/>
        /// matches <paramref name="type"/>.
        /// Counting by both skill id and type prevents a multi-buff skill (e.g. FadeSkill with
        /// EvasionUp + MPRegen) from blocking its second buff type when the first type is capped.
        /// </summary>
        public int GetBuffStacks(string sourceSkillId, BuffType type)
        {
            int count = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].SourceSkillId == sourceSkillId && _buffs[i].Type == type)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Decrements finite-duration buffs and removes expired ones.
        /// Buffs with <c>RemainingTurns == -1</c> are permanent until battle end and are never decremented.
        /// </summary>
        public void TickBuffDurations()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.RemainingTurns == -1) continue; // permanent until battle end
                buff.RemainingTurns--;
                if (buff.RemainingTurns <= 0)
                    _buffs.RemoveAt(i);
                else
                    _buffs[i] = buff;
            }
        }

        /// <summary>Removes all active battle buffs.</summary>
        public void Clear()
        {
            _buffs.Clear();
        }
    }
}

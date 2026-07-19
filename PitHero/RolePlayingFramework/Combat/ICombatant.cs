using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Shared interface for all participants in combat (Hero and Mercenary).
    /// Provides a unified API for skills, passive effects, and vital operations so that
    /// skill Execute/ApplyPassive implementations work identically for both entity types.
    /// </summary>
    public interface ICombatant
    {
        // ── Identity / stats ─────────────────────────────────────────────────────

        /// <summary>Display name.</summary>
        string Name { get; }

        /// <summary>Current character level.</summary>
        int Level { get; }

        /// <summary>Returns current total stats (base + job + equipment + bonuses).</summary>
        StatBlock GetTotalStats();

        /// <summary>
        /// Returns total stats with battle-buff-adjusted Magic (MagicUp) for caster-side
        /// skill damage/heal formulas. Never affects MaxMP or out-of-battle stat displays.
        /// </summary>
        StatBlock GetSkillStats();

        /// <summary>Maximum HP.</summary>
        int MaxHP { get; }

        /// <summary>Current HP.</summary>
        int CurrentHP { get; }

        /// <summary>Maximum MP.</summary>
        int MaxMP { get; }

        /// <summary>Current MP.</summary>
        int CurrentMP { get; }

        // ── Vital operations ─────────────────────────────────────────────────────

        /// <summary>Inflicts damage. Returns true if the combatant died.</summary>
        bool TakeDamage(int amount);

        /// <summary>Restores HP up to MaxHP. Returns true if HP was actually restored.</summary>
        bool RestoreHP(int amount);

        /// <summary>Restores MP up to MaxMP. Returns true if MP was actually restored.</summary>
        bool RestoreMP(int amount);

        /// <summary>
        /// Spends MP, applying MPCostReduction. Returns true on success, false if
        /// insufficient MP after reduction is applied.
        /// </summary>
        bool SpendMP(int amount);

        /// <summary>
        /// Returns the effective MP cost after applying <see cref="MPCostReduction"/>,
        /// using the same formula as <see cref="SpendMP"/> (floor of 1 when rawCost &gt; 0).
        /// Use this in affordability checks so they are consistent with what SpendMP will deduct.
        /// Returns 0 when <paramref name="rawCost"/> is 0 or negative.
        /// </summary>
        int GetEffectiveMPCost(int rawCost);

        // ── Passive fields written by skill ApplyPassive / synergy effects ───────

        /// <summary>Flat defense bonus from passives and buffs.</summary>
        int PassiveDefenseBonus { get; set; }

        /// <summary>Chance (0–1) to deflect an incoming attack entirely.</summary>
        float DeflectChance { get; set; }

        /// <summary>When true, the combatant retaliates after surviving a hit.</summary>
        bool EnableCounter { get; set; }

        /// <summary>MP restored per turn tick.</summary>
        int MPTickRegen { get; set; }

        /// <summary>Multiplicative bonus applied to all HP healed by skills.</summary>
        float HealPowerBonus { get; set; }

        /// <summary>Multiplicative bonus applied to Fire-element skill damage.</summary>
        float FireDamageBonus { get; set; }

        /// <summary>Fraction by which MP skill costs are reduced (0.15 = −15%).</summary>
        float MPCostReduction { get; set; }

        // ── Future-phase passive fields (plumbing only; default 0 in Phase 1) ────

        /// <summary>Flat evasion bonus from passives (Phase 3/4 plumbing).</summary>
        int EvasionBonus { get; set; }

        /// <summary>Tile-radius sight bonus from Eagle Eye (Phase 4 plumbing).</summary>
        int SightRangeBonus { get; set; }

        /// <summary>First-attack critical-hit chance bonus from Quickdraw (Phase 4 plumbing).</summary>
        float FirstAttackCritChance { get; set; }

        /// <summary>Defense bonus applied only when wearing heavy mail armor (Phase 5 plumbing).</summary>
        int HeavyArmorDefenseBonus { get; set; }

        /// <summary>When true, the combatant detects and auto-disarms traps revealed by fog clearing (Phase 6).</summary>
        bool TrapSense { get; set; }

        // ── Equipment ────────────────────────────────────────────────────────────

        /// <summary>Grants permission to equip items of the given kind outside normal job restrictions.</summary>
        void AddExtraEquipPermission(ItemKind kind);

        // ── Combat ───────────────────────────────────────────────────────────────

        /// <summary>Returns the derived battle stats (Attack, Defense, Evasion) used in combat.</summary>
        BattleStats GetBattleStats();

        /// <summary>Performs one MP/regen tick (called at the end of each battle round).</summary>
        void TickRegeneration();

        // ── Battle-scoped buff system ─────────────────────────────────────────────

        /// <summary>Adds or refreshes a battle-scoped buff on this combatant.</summary>
        void AddBattleBuff(in BattleBuff buff);

        /// <summary>
        /// Returns the summed magnitude of all active buffs of the given type,
        /// or 0 if no such buff is active.
        /// </summary>
        int GetBuffTotal(BuffType type);

        /// <summary>
        /// Returns the number of active buff stacks whose <see cref="BattleBuff.SourceSkillId"/>
        /// matches <paramref name="sourceSkillId"/> AND whose <see cref="BattleBuff.Type"/>
        /// matches <paramref name="type"/>.
        /// Counting by both id and type prevents a multi-buff skill (e.g. FadeSkill with
        /// EvasionUp + MPRegen) from blocking its second buff type when the first is at max stacks.
        /// </summary>
        int GetBuffStacks(string sourceSkillId, BuffType type);

        /// <summary>
        /// Decrements <see cref="BattleBuff.RemainingTurns"/> on all finite-duration buffs
        /// and removes those that have expired.
        /// Buffs with <c>RemainingTurns == -1</c> are permanent until battle end and are
        /// never decremented here.
        /// </summary>
        void TickBuffDurations();

        /// <summary>
        /// Removes all active battle buffs.
        /// Called at battle start to clear any leaked state and in the battle finally block
        /// so buffs never persist outside of a battle.
        /// </summary>
        void ClearBattleState();
    }
}

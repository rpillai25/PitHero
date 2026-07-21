using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Mercenaries
{
    /// <summary>Runtime mercenary instance with equipment, skills and derived stats (no crystal or synergies).</summary>
    public sealed class Mercenary : ICombatant
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; private set; }
        public int Experience { get; internal set; }
        public StatBlock BaseStats { get; private set; }

        public int MaxHP { get; private set; }
        public int MaxMP { get; private set; }
        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public int PassiveDefenseBonus { get; set; }
        public float DeflectChance { get; set; }
        public bool EnableCounter { get; set; }
        public int MPTickRegen { get; set; }
        public float HealPowerBonus { get; set; }
        public float FireDamageBonus { get; set; }
        public float MPCostReduction { get; set; }

        // Future-phase passive fields (Phase 1 plumbing; default 0 until wired in later phases)
        public int EvasionBonus { get; set; }
        public int SightRangeBonus { get; set; }
        public float FirstAttackCritChance { get; set; }
        public int HeavyArmorDefenseBonus { get; set; }
        public bool TrapSense { get; set; }

        // Extra equip permissions granted by learned passives (e.g. knight.light_armor allows robes)
        private readonly HashSet<ItemKind> _extraEquipPermissions = new HashSet<ItemKind>();

        // Battle-scoped buff state — delegated to shared BattleBuffSet
        private readonly BattleBuffSet _buffSet = new BattleBuffSet();

        public IGear? WeaponShield1 { get; private set; }
        public IGear? Armor { get; private set; }
        public IGear? Hat { get; private set; }
        public IGear? WeaponShield2 { get; private set; }
        public IGear? Accessory1 { get; private set; }
        public IGear? Accessory2 { get; private set; }

        private readonly Dictionary<string, ISkill> _learnedSkills = new Dictionary<string, ISkill>(16);

        /// <summary>Skills learned by this mercenary, keyed by skill Id.</summary>
        public IReadOnlyDictionary<string, ISkill> LearnedSkills => _learnedSkills;

        public Mercenary(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            
            RecalculateDerived();
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Adds experience and levels up, growing stats each level.</summary>
        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;
            Experience += amount;
            var leveled = false;
            while (Experience >= RequiredExpForNextLevel())
            {
                Experience -= RequiredExpForNextLevel();

                if (Level >= StatConstants.MaxLevel) break;

                Level++;
                leveled = true;

                BaseStats = StatConstants.ClampStatBlock(
                    new StatBlock(
                        BaseStats.Strength + 1,
                        BaseStats.Agility + 1,
                        BaseStats.Vitality + 1,
                        BaseStats.Magic + 1
                    )
                );

                RecalculateDerived();
            }
            return leveled;
        }

        /// <summary>Gets required XP for the next level.</summary>
        public int RequiredExpForNextLevel() => Level * 100;

        /// <summary>Grants permission to equip items of the given kind outside normal job restrictions.</summary>
        public void AddExtraEquipPermission(ItemKind kind)
        {
            _extraEquipPermissions.Add(kind);
        }

        /// <summary>Checks whether this mercenary can equip the given item based on job and extra permissions.</summary>
        public bool CanEquipItem(IItem item)
        {
            if (item == null) return false;
            if (item is IGear gear)
            {
                if ((gear.AllowedJobs & Job.JobFlag) != 0) return true;
                if (_extraEquipPermissions.Contains(gear.Kind)) return true;
                return false;
            }
            return false;
        }

        /// <summary>Equips an item in the appropriate slot.</summary>
        public bool Equip(IGear item)
        {
            if (item == null) return false;

            switch (item.Kind)
            {
                case ItemKind.WeaponSword:
                case ItemKind.WeaponKnife:
                case ItemKind.WeaponKnuckle:
                case ItemKind.WeaponStaff:
                case ItemKind.WeaponRod:
                case ItemKind.WeaponHammer:
                    if (WeaponShield1 != null) return false;
                    WeaponShield1 = item;
                    break;
                case ItemKind.Shield:
                    if (WeaponShield2 != null) return false;
                    WeaponShield2 = item;
                    break;
                case ItemKind.ArmorMail:
                case ItemKind.ArmorGi:
                case ItemKind.ArmorRobe:
                    if (Armor != null) return false;
                    Armor = item;
                    break;
                case ItemKind.HatHelm:
                case ItemKind.HatHeadband:
                case ItemKind.HatWizard:
                case ItemKind.HatPriest:
                    if (Hat != null) return false;
                    Hat = item;
                    break;
                case ItemKind.Accessory:
                    if (Accessory1 == null)
                        Accessory1 = item;
                    else if (Accessory2 == null)
                        Accessory2 = item;
                    else
                        return false;
                    break;
                default:
                    return false;
            }

            RecalculateDerived();
            return true;
        }

        /// <summary>Unequips an item from the specified slot.</summary>
        public IGear? Unequip(EquipmentSlot slot)
        {
            IGear? removed = null;
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    removed = WeaponShield1;
                    WeaponShield1 = null;
                    break;
                case EquipmentSlot.Armor:
                    removed = Armor;
                    Armor = null;
                    break;
                case EquipmentSlot.Hat:
                    removed = Hat;
                    Hat = null;
                    break;
                case EquipmentSlot.WeaponShield2:
                    removed = WeaponShield2;
                    WeaponShield2 = null;
                    break;
                case EquipmentSlot.Accessory1:
                    removed = Accessory1;
                    Accessory1 = null;
                    break;
                case EquipmentSlot.Accessory2:
                    removed = Accessory2;
                    Accessory2 = null;
                    break;
            }

            if (removed != null)
                RecalculateDerived();

            return removed;
        }

        /// <summary>Sets equipment in a specific slot with job restriction and slot-type validation.</summary>
        public bool SetEquipmentSlot(EquipmentSlot slot, IItem? item)
        {
            if (item == null)
            {
                switch (slot)
                {
                    case EquipmentSlot.WeaponShield1: WeaponShield1 = null; break;
                    case EquipmentSlot.Armor: Armor = null; break;
                    case EquipmentSlot.Hat: Hat = null; break;
                    case EquipmentSlot.WeaponShield2: WeaponShield2 = null; break;
                    case EquipmentSlot.Accessory1: Accessory1 = null; break;
                    case EquipmentSlot.Accessory2: Accessory2 = null; break;
                }
                RecalculateDerived();
                return true;
            }

            if (!CanEquipItem(item)) return false;
            if (item is not IGear gear) return false;

            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    if (gear.Kind == ItemKind.WeaponSword || gear.Kind == ItemKind.WeaponKnife
                        || gear.Kind == ItemKind.WeaponKnuckle || gear.Kind == ItemKind.WeaponStaff
                        || gear.Kind == ItemKind.WeaponRod || gear.Kind == ItemKind.WeaponHammer)
                    {
                        WeaponShield1 = gear; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Armor:
                    if (gear.Kind == ItemKind.ArmorMail || gear.Kind == ItemKind.ArmorGi || gear.Kind == ItemKind.ArmorRobe)
                    {
                        Armor = gear; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Hat:
                    if (gear.Kind == ItemKind.HatHelm || gear.Kind == ItemKind.HatHeadband
                        || gear.Kind == ItemKind.HatWizard || gear.Kind == ItemKind.HatPriest)
                    {
                        Hat = gear; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.WeaponShield2:
                    if (gear.Kind == ItemKind.Shield)
                    {
                        WeaponShield2 = gear; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Accessory1:
                    if (gear.Kind == ItemKind.Accessory) { Accessory1 = gear; RecalculateDerived(); return true; }
                    return false;
                case EquipmentSlot.Accessory2:
                    if (gear.Kind == ItemKind.Accessory) { Accessory2 = gear; RecalculateDerived(); return true; }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>Directly assigns equipment to two slots in a single recalculation (for slot-to-slot swaps).</summary>
        public void ApplyEquipmentSwap(EquipmentSlot slotA, IItem? itemForA, EquipmentSlot slotB, IItem? itemForB)
        {
            SetSlotDirect(slotA, itemForA as IGear);
            SetSlotDirect(slotB, itemForB as IGear);
            RecalculateDerived();
        }

        /// <summary>Directly sets a slot without validation or recalculation (used by ApplyEquipmentSwap).</summary>
        private void SetSlotDirect(EquipmentSlot slot, IGear? item)
        {
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1: WeaponShield1 = item; break;
                case EquipmentSlot.Armor: Armor = item; break;
                case EquipmentSlot.Hat: Hat = item; break;
                case EquipmentSlot.WeaponShield2: WeaponShield2 = item; break;
                case EquipmentSlot.Accessory1: Accessory1 = item; break;
                case EquipmentSlot.Accessory2: Accessory2 = item; break;
            }
        }

        /// <summary>Gets total stats (base + job + equipment) clamped to maximums.</summary>
        public StatBlock GetTotalStats()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            var total = BaseStats.Add(jobStats);

            if (WeaponShield1 != null) total = total.Add(WeaponShield1.StatBonus);
            if (Armor != null) total = total.Add(Armor.StatBonus);
            if (Hat != null) total = total.Add(Hat.StatBonus);
            if (WeaponShield2 != null) total = total.Add(WeaponShield2.StatBonus);
            if (Accessory1 != null) total = total.Add(Accessory1.StatBonus);
            if (Accessory2 != null) total = total.Add(Accessory2.StatBonus);

            return StatConstants.ClampStatBlock(total);
        }

        /// <summary>Total stats with battle-buff-adjusted Magic (MagicUp) for caster-side skill formulas.</summary>
        public StatBlock GetSkillStats()
        {
            var stats = GetTotalStats();
            int magicUp = GetBuffTotal(BuffType.MagicUp);
            if (magicUp == 0) return stats;
            return new StatBlock(stats.Strength, stats.Agility, stats.Vitality, stats.Magic + magicUp);
        }

        /// <summary>Recalculates derived stats from base + job + equipment.</summary>
        private void RecalculateDerived()
        {
            var total = GetTotalStats();

            int baseHP = 25 + (total.Vitality * 5);
            int baseMP = 10 + (total.Magic * 3);

            MaxHP = StatConstants.ClampHP(baseHP);
            MaxMP = StatConstants.ClampMP(baseMP);

            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
        }

        /// <summary>
        /// Computes battle stats for combat calculations, including any active battle buffs.
        /// </summary>
        public BattleStats GetBattleStats()
        {
            var effectiveStats = GetTotalStats();

            // AgilityUp cascades into evasion and accuracy (merc defense is Vitality-based)
            int agility = effectiveStats.Agility + GetBuffTotal(BuffType.AgilityUp);

            int atk = effectiveStats.Strength + GetBuffTotal(BuffType.AttackUp);
            if (WeaponShield1 != null) atk += WeaponShield1.AttackBonus;
            if (WeaponShield2 != null) atk += WeaponShield2.AttackBonus;

            int def = effectiveStats.Vitality + PassiveDefenseBonus;
            if (Armor != null)
            {
                def += Armor.DefenseBonus;
                if (Armor.Kind == ItemKind.ArmorMail)
                    def += HeavyArmorDefenseBonus;
            }
            if (Hat != null) def += Hat.DefenseBonus;
            if (WeaponShield1 != null) def += WeaponShield1.DefenseBonus;
            if (WeaponShield2 != null) def += WeaponShield2.DefenseBonus;
            def += GetBuffTotal(BuffType.DefenseUp);

            int baseEvasion = RolePlayingFramework.Balance.BalanceConfig.CalculateEvasion(agility, Level);
            int evasion = baseEvasion + EvasionBonus + GetBuffTotal(BuffType.EvasionUp);
            if (evasion > 255) evasion = 255;
            if (evasion < 0) evasion = 0;

            // Accuracy = base formula only — evasion gear/buffs don't help land hits
            return new BattleStats(atk, def, evasion, baseEvasion);
        }

        /// <summary>Takes damage and returns true if mercenary died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            CurrentHP -= amount;
            if (CurrentHP < 0) CurrentHP = 0;
            return CurrentHP == 0;
        }

        /// <summary>Restores HP up to max. Returns true if HP was actually restored.</summary>
        public bool RestoreHP(int amount)
        {
            if (amount <= 0) return false;
            if (CurrentHP >= MaxHP) return false; // Already at max HP
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            return true;
        }

        /// <summary>Restores MP up to MaxMP. Returns true if MP was actually restored.</summary>
        public bool RestoreMP(int amount)
        {
            if (amount <= 0) return false;
            if (CurrentMP >= MaxMP) return false;
            CurrentMP += amount;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
            return true;
        }

        /// <summary>
        /// Directly sets CurrentMP to a saved value, clamped to [0, MaxMP].
        /// Use this for state-restore paths (save/load) so MPCostReduction is NOT applied.
        /// </summary>
        public void SetCurrentMP(int value)
        {
            CurrentMP = value < 0 ? 0 : (value > MaxMP ? MaxMP : value);
        }

        /// <summary>
        /// Returns the effective MP cost after applying MPCostReduction (floor of 1 when rawCost &gt; 0).
        /// This is the exact amount SpendMP will deduct; use it in affordability checks.
        /// </summary>
        public int GetEffectiveMPCost(int rawCost)
        {
            if (rawCost <= 0) return 0;
            int reduced = (int)(rawCost * (1f - MPCostReduction));
            return reduced < 1 ? 1 : reduced;
        }

        /// <summary>Spends MP applying MPCostReduction. Returns true on success.</summary>
        public bool SpendMP(int amount)
        {
            if (amount <= 0) return true;
            int reduced = GetEffectiveMPCost(amount);
            if (CurrentMP < reduced) return false;
            CurrentMP -= reduced;
            return true;
        }

        /// <summary>Thin alias for SpendMP — preserves call sites that predate the ICombatant unification.</summary>
        public bool UseMP(int amount) => SpendMP(amount);

        /// <summary>Teaches the mercenary a skill and recomputes passive effects. Returns true if learned.</summary>
        public bool LearnSkill(ISkill skill)
        {
            if (skill == null || _learnedSkills.ContainsKey(skill.Id))
                return false;
            _learnedSkills[skill.Id] = skill;
            ApplyPassiveSkills();
            return true;
        }

        /// <summary>Removes a learned skill by Id and recomputes passive effects. Returns true if removed.</summary>
        public bool ForgetSkill(string skillId)
        {
            if (skillId == null) return false;
            if (_learnedSkills.Remove(skillId))
            {
                ApplyPassiveSkills();
                return true;
            }
            return false;
        }

        /// <summary>Learns all skills from the mercenary's job and applies passive effects.</summary>
        public void LearnAllJobSkills()
        {
            var skills = Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                LearnSkill(skills[i]);
            }
            ApplyPassiveSkills();
        }

        /// <summary>Per-turn passive HP/MP regen (called at the end of each battle round), including HPRegen/MPRegen buffs.</summary>
        public void TickRegeneration()
        {
            int totalRegen = MPTickRegen + GetBuffTotal(BuffType.MPRegen);
            if (totalRegen > 0)
            {
                CurrentMP += totalRegen;
                if (CurrentMP > MaxMP) CurrentMP = MaxMP;
            }
            int hpRegen = GetBuffTotal(BuffType.HPRegen);
            if (hpRegen > 0)
                RestoreHP(hpRegen);
        }

        // ── Battle-scoped buff system — delegates to shared BattleBuffSet ────────────────

        /// <summary>Adds a battle buff. Each buff is tracked individually to support stacks.</summary>
        public void AddBattleBuff(in BattleBuff buff) => _buffSet.AddBattleBuff(buff);

        /// <summary>Returns the summed magnitude of all active buffs of the given type.</summary>
        public int GetBuffTotal(BuffType type) => _buffSet.GetBuffTotal(type);

        /// <summary>
        /// Returns the number of active buff stacks from the given source skill AND of the given type.
        /// Filtering by both skill id and buff type prevents multi-buff skills from blocking their
        /// second buff type when the first type is already at max stacks.
        /// </summary>
        public int GetBuffStacks(string sourceSkillId, BuffType type)
            => _buffSet.GetBuffStacks(sourceSkillId, type);

        /// <summary>Decrements finite-duration buffs and removes expired ones.</summary>
        public void TickBuffDurations() => _buffSet.TickBuffDurations();

        /// <summary>Clears all battle buffs. Called at battle start and in the battle finally block.</summary>
        public void ClearBattleState() => _buffSet.Clear();

        /// <summary>
        /// Resets all passive fields and re-applies them from the current learned-skill set.
        /// Uses CombatantPassiveApplier so the reset list stays identical to Hero.
        /// </summary>
        private void ApplyPassiveSkills()
        {
            // Extra-equip permissions are rebuilt from scratch on every re-application
            _extraEquipPermissions.Clear();
            CombatantPassiveApplier.ResetAndApply(this, _learnedSkills);
        }
    }
}

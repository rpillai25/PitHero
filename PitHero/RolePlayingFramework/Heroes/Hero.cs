using System.Collections.Generic;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Combat;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Runtime hero instance with equipment, skills and derived stats.</summary>
    public sealed class Hero
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public StatBlock BaseStats { get; private set; }

        public int MaxHP { get; private set; }
        public int MaxMP { get; private set; }
        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        // Passive modifiers from learned skills
        public int PassiveDefenseBonus { get; set; }
        public float DeflectChance { get; set; }
        public bool EnableCounter { get; set; }
        public int MPTickRegen { get; set; }
        public float HealPowerBonus { get; set; }
        public float FireDamageBonus { get; set; }
        public float MPCostReduction { get; set; }

        // Equipment
        public IItem? WeaponShield1 { get; private set; }
        public IItem? Armor { get; private set; }
        public IItem? Hat { get; private set; }
        public IItem? WeaponShield2 { get; private set; }
        public IItem? Accessory1 { get; private set; }
        public IItem? Accessory2 { get; private set; }

        // Learned skills (by Id)
        private readonly Dictionary<string, ISkill> _learnedSkills;
        public IReadOnlyDictionary<string, ISkill> LearnedSkills => _learnedSkills;

        // Additional equip permissions from passives
        private readonly HashSet<Equipment.ItemKind> _extraEquipPermissions = new HashSet<Equipment.ItemKind>();

        private readonly HeroCrystal? _boundCrystal;
        
        /// <summary>Gets the crystal bound to this hero (if any).</summary>
        public HeroCrystal? BoundCrystal => _boundCrystal;

        public Hero(string name, IJob job, int level, in StatBlock baseStats, HeroCrystal? fromCrystal = null)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            _learnedSkills = new Dictionary<string, ISkill>(16);
            _boundCrystal = fromCrystal;
            if (fromCrystal != null)
            {
                // preload crystal skills that exist in this job (handles composite jobs)
                var skills = job.Skills;
                for (int i = 0; i < skills.Count; i++)
                {
                    var s = skills[i];
                    if (fromCrystal.HasSkill(s.Id))
                        _learnedSkills[s.Id] = s;
                }
            }
            ApplyPassiveSkills();
            RecalculateDerived();
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Adds extra equipment permission (from passives).</summary>
        public void AddExtraEquipPermission(Equipment.ItemKind kind)
        {
            _extraEquipPermissions.Add(kind);
        }

        /// <summary>Adds experience and levels up linearly.</summary>
        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;
            Experience += amount;
            var leveled = false;
            while (Experience >= RequiredExpForNextLevel())
            {
                Experience -= RequiredExpForNextLevel();
                
                // Clamp level to max before incrementing
                if (Level >= StatConstants.MaxLevel) break;
                
                Level++;
                leveled = true;
                
                // Increment base stats by 1 per level, ensuring they don't exceed caps
                BaseStats = StatConstants.ClampStatBlock(
                    new StatBlock(
                        BaseStats.Strength + 1, 
                        BaseStats.Agility + 1, 
                        BaseStats.Vitality + 1, 
                        BaseStats.Magic + 1
                    )
                );
                
                RecalculateDerived();
                ApplyPassiveSkills();
            }
            return leveled;
        }

        /// <summary>Gets required XP for the next level.</summary>
        public int RequiredExpForNextLevel() => Level * 100;

        /// <summary>Recomputes HP/MP and caps.</summary>
        public void RecalculateDerived()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            var total = BaseStats.Add(jobStats).Add(GetEquipmentStatBonus());
            // Clamp total stats before using them for HP/MP calculations
            total = StatConstants.ClampStatBlock(total);
            
            // Calculate HP and MP using the utility methods with capping
            MaxHP = GrowthCurveCalculator.CalculateHP(
                total.Vitality, 
                baseHP: 25, 
                vitalityMultiplier: 5
            ) + GetEquipmentHPBonus();
            
            MaxMP = GrowthCurveCalculator.CalculateMP(
                total.Magic, 
                baseMP: 10, 
                magicMultiplier: 3
            ) + GetEquipmentMPBonus();
            
            // Ensure HP/MP stay within caps after adding equipment bonuses
            MaxHP = StatConstants.ClampHP(MaxHP);
            MaxMP = StatConstants.ClampMP(MaxMP);
            
            // Clamp current values to new maximums
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
        }

        /// <summary>Returns current total stats (base + job + equipment).</summary>
        public StatBlock GetTotalStats()
        {
            // Use GrowthCurveCalculator to calculate base stats + job contribution at current level
            var statsWithJob = GrowthCurveCalculator.CalculateTotalStatsAtLevel(
                BaseStats,
                Job.BaseBonus,
                Job.GrowthPerLevel,
                Level
            );
            
            // Add equipment bonuses and clamp (equipment is already a StatBlock)
            var total = statsWithJob.Add(GetEquipmentStatBonus());
            return StatConstants.ClampStatBlock(total);
        }

        /// <summary>Inflicts damage, returns true if hero died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            CurrentHP -= amount;
            if (CurrentHP < 0) CurrentHP = 0;
            return CurrentHP == 0;
        }

        /// <summary>Spend MP if sufficient (includes passive cost reduction).</summary>
        public bool SpendMP(int amount)
        {
            if (amount <= 0) return true;
            var reduced = (int)(amount * (1f - MPCostReduction));
            if (reduced < 1) reduced = 1;
            if (CurrentMP < reduced) return false;
            CurrentMP -= reduced;
            return true;
        }

        /// <summary>Restores HP (clamps to MaxHP). Returns true if HP was actually restored.</summary>
        public bool RestoreHP(int amount)
        {
            if (amount <= 0) return false;
            if (CurrentHP >= MaxHP) return false; // Already at max HP
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            return true;
        }

        /// <summary>Per-turn passive MP regen.</summary>
        public void TickRegeneration()
        {
            if (MPTickRegen > 0)
            {
                CurrentMP += MPTickRegen;
                if (CurrentMP > MaxMP) CurrentMP = MaxMP;
            }
        }

        /// <summary>Equips an item into the appropriate slot if job allows.</summary>
        public bool TryEquip(IItem item)
        {
            if (item == null) return false;
            switch (item.Kind)
            {
                case ItemKind.WeaponSword:
                    if (Job is Jobs.Primary.Knight) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponKnuckle:
                    if (Job is Jobs.Primary.Monk) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponStaff:
                    if (Job is Jobs.Primary.Priest) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponRod:
                    if (Job is Jobs.Primary.Mage) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorMail:
                    if (Job is Jobs.Primary.Knight || _extraEquipPermissions.Contains(ItemKind.ArmorMail)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorGi:
                    if (Job is Jobs.Primary.Monk || _extraEquipPermissions.Contains(ItemKind.ArmorGi)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorRobe:
                    if (Job is Jobs.Primary.Mage || Job is Jobs.Primary.Priest || _extraEquipPermissions.Contains(ItemKind.ArmorRobe)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatHelm:
                    if (Job is Jobs.Primary.Knight) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatHeadband:
                    if (Job is Jobs.Primary.Monk) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatWizard:
                    if (Job is Jobs.Primary.Mage) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatPriest:
                    if (Job is Jobs.Primary.Priest) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.Shield:
                    // All classes can equip shields for now
                    WeaponShield2 = item; RecalculateDerived(); return true;
                case ItemKind.Accessory:
                    if (Accessory1 == null) { Accessory1 = item; RecalculateDerived(); return true; }
                    if (Accessory2 == null) { Accessory2 = item; RecalculateDerived(); return true; }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>Sets the specified equipment slot to the provided item (or null), enforcing job/type rules.</summary>
        public bool SetEquipmentSlot(EquipmentSlot slot, IItem? item)
        {
            // Clear case
            if (item == null)
            {
                switch (slot)
                {
                    case EquipmentSlot.WeaponShield1: if (WeaponShield1 != null) { WeaponShield1 = null; } break;
                    case EquipmentSlot.Armor: if (Armor != null) { Armor = null; } break;
                    case EquipmentSlot.Hat: if (Hat != null) { Hat = null; } break;
                    case EquipmentSlot.WeaponShield2: if (WeaponShield2 != null) { WeaponShield2 = null; } break;
                    case EquipmentSlot.Accessory1: if (Accessory1 != null) { Accessory1 = null; } break;
                    case EquipmentSlot.Accessory2: if (Accessory2 != null) { Accessory2 = null; } break;
                }
                RecalculateDerived();
                return true;
            }

            // Assign with validation
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    if (item.Kind == ItemKind.WeaponSword && Job is Jobs.Primary.Knight
                        || item.Kind == ItemKind.WeaponKnuckle && Job is Jobs.Primary.Monk
                        || item.Kind == ItemKind.WeaponStaff && Job is Jobs.Primary.Priest
                        || item.Kind == ItemKind.WeaponRod && Job is Jobs.Primary.Mage)
                    {
                        WeaponShield1 = item; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Armor:
                    if (item.Kind == ItemKind.ArmorMail && (Job is Jobs.Primary.Knight || _extraEquipPermissions.Contains(ItemKind.ArmorMail))
                        || item.Kind == ItemKind.ArmorGi && (Job is Jobs.Primary.Monk || _extraEquipPermissions.Contains(ItemKind.ArmorGi))
                        || item.Kind == ItemKind.ArmorRobe && (Job is Jobs.Primary.Mage || Job is Jobs.Primary.Priest || _extraEquipPermissions.Contains(ItemKind.ArmorRobe)))
                    {
                        Armor = item; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Hat:
                    if (item.Kind == ItemKind.HatHelm && Job is Jobs.Primary.Knight
                        || item.Kind == ItemKind.HatHeadband && Job is Jobs.Primary.Monk
                        || item.Kind == ItemKind.HatWizard && Job is Jobs.Primary.Mage
                        || item.Kind == ItemKind.HatPriest && Job is Jobs.Primary.Priest)
                    {
                        Hat = item; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.WeaponShield2:
                    if (item.Kind == ItemKind.Shield)
                    {
                        WeaponShield2 = item; RecalculateDerived(); return true;
                    }
                    return false;
                case EquipmentSlot.Accessory1:
                    if (item.Kind == ItemKind.Accessory) { Accessory1 = item; RecalculateDerived(); return true; }
                    return false;
                case EquipmentSlot.Accessory2:
                    if (item.Kind == ItemKind.Accessory) { Accessory2 = item; RecalculateDerived(); return true; }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>Applies a swap between two equipment slots and recalculates once to avoid transient HP/MP clamps.</summary>
        public void ApplyEquipmentSwap(EquipmentSlot slotA, IItem? itemForA, EquipmentSlot slotB, IItem? itemForB)
        {
            // Assign directly without recalculating per-slot. Validation assumed handled by caller.
            switch (slotA)
            {
                case EquipmentSlot.WeaponShield1: WeaponShield1 = itemForA; break;
                case EquipmentSlot.Armor: Armor = itemForA; break;
                case EquipmentSlot.Hat: Hat = itemForA; break;
                case EquipmentSlot.WeaponShield2: WeaponShield2 = itemForA; break;
                case EquipmentSlot.Accessory1: Accessory1 = itemForA; break;
                case EquipmentSlot.Accessory2: Accessory2 = itemForA; break;
            }
            switch (slotB)
            {
                case EquipmentSlot.WeaponShield1: WeaponShield1 = itemForB; break;
                case EquipmentSlot.Armor: Armor = itemForB; break;
                case EquipmentSlot.Hat: Hat = itemForB; break;
                case EquipmentSlot.WeaponShield2: WeaponShield2 = itemForB; break;
                case EquipmentSlot.Accessory1: Accessory1 = itemForB; break;
                case EquipmentSlot.Accessory2: Accessory2 = itemForB; break;
            }
            RecalculateDerived();
        }

        /// <summary>Unequips an item from its slot.</summary>
        public bool TryUnequip(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1: if (WeaponShield1 != null) { WeaponShield1 = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Armor: if (Armor != null) { Armor = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Hat: if (Hat != null) { Hat = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.WeaponShield2: if (WeaponShield2 != null) { WeaponShield2 = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Accessory1: if (Accessory1 != null) { Accessory1 = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Accessory2: if (Accessory2 != null) { Accessory2 = null; RecalculateDerived(); return true; } break;
            }
            return false;
        }

        /// <summary>Sum of all equipment stat bonuses.</summary>
        private StatBlock GetEquipmentStatBonus()
        {
            var sum = new StatBlock(0, 0, 0, 0);
            if (WeaponShield1 is IGear weaponGear) sum = sum.Add(weaponGear.StatBonus);
            if (Armor is IGear armorGear) sum = sum.Add(armorGear.StatBonus);
            if (Hat is IGear helmGear) sum = sum.Add(helmGear.StatBonus);
            if (WeaponShield2 is IGear shieldGear) sum = sum.Add(shieldGear.StatBonus);
            if (Accessory1 is IGear acc1Gear) sum = sum.Add(acc1Gear.StatBonus);
            if (Accessory2 is IGear acc2Gear) sum = sum.Add(acc2Gear.StatBonus);
            return sum;
        }

        /// <summary>Total flat attack bonus from gear.</summary>
        public int GetEquipmentAttackBonus()
        {
            int atk = 0;
            if (WeaponShield1 is IGear weaponGear) atk += weaponGear.AttackBonus;
            if (WeaponShield2 is IGear shieldGear) atk += shieldGear.AttackBonus;
            if (Accessory1 is IGear acc1Gear) atk += acc1Gear.AttackBonus;
            if (Accessory2 is IGear acc2Gear) atk += acc2Gear.AttackBonus;
            return atk;
        }

        /// <summary>Total flat defense bonus from gear plus passives.</summary>
        public int GetEquipmentDefenseBonus()
        {
            int def = PassiveDefenseBonus;
            if (Armor is IGear armorGear) def += armorGear.DefenseBonus;
            if (Hat is IGear helmGear) def += helmGear.DefenseBonus;
            if (WeaponShield2 is IGear shieldGear) def += shieldGear.DefenseBonus;
            if (Accessory1 is IGear acc1Gear) def += acc1Gear.DefenseBonus;
            if (Accessory2 is IGear acc2Gear) def += acc2Gear.DefenseBonus;
            return def;
        }

        /// <summary>Total flat HP bonus from gear.</summary>
        public int GetEquipmentHPBonus()
        {
            int hp = 0;
            if (WeaponShield1 is IGear weaponGear) hp += weaponGear.HPBonus;
            if (Armor is IGear armorGear) hp += armorGear.HPBonus;
            if (Hat is IGear helmGear) hp += helmGear.HPBonus;
            if (WeaponShield2 is IGear shieldGear) hp += shieldGear.HPBonus;
            if (Accessory1 is IGear acc1Gear) hp += acc1Gear.HPBonus;
            if (Accessory2 is IGear acc2Gear) hp += acc2Gear.HPBonus;
            return hp;
        }

        /// <summary>Total flat MP bonus from gear.</summary>
        public int GetEquipmentMPBonus()
        {
            int mp = 0;
            if (WeaponShield1 is IGear weaponGear) mp += weaponGear.MPBonus;
            if (Armor is IGear armorGear) mp += armorGear.MPBonus;
            if (Hat is IGear helmGear) mp += helmGear.MPBonus;
            if (WeaponShield2 is IGear shieldGear) mp += shieldGear.MPBonus;
            if (Accessory1 is IGear acc1Gear) mp += acc1Gear.MPBonus;
            if (Accessory2 is IGear acc2Gear) mp += acc2Gear.MPBonus;
            return mp;
        }

        private void ApplyPassiveSkills()
        {
            PassiveDefenseBonus = 0;
            DeflectChance = 0;
            EnableCounter = false;
            MPTickRegen = 0;
            HealPowerBonus = 0f;
            FireDamageBonus = 0f;
            MPCostReduction = 0f;
            foreach (var kv in _learnedSkills)
            {
                if (kv.Value.Kind == SkillKind.Passive)
                    kv.Value.ApplyPassive(this);
            }
        }

        public ISkill? ChooseActiveSkillForBattle()
        {
            ISkill? best = null;
            int bestCost = -1;
            foreach (var kv in _learnedSkills)
            {
                var s = kv.Value;
                if (s.Kind != SkillKind.Active) continue;
                if (s.MPCost > CurrentMP) continue;
                if (s.MPCost > bestCost) { best = s; bestCost = s.MPCost; }
            }
            return best;
        }

        public string? TryUseSkill(ISkill skill, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            if (skill.Kind != SkillKind.Active) return null;
            if (!SpendMP(skill.MPCost)) return null;
            return skill.Execute(this, primary, surrounding, resolver);
        }

        /// <summary>Earns JP for the bound crystal (if any).</summary>
        public void EarnJP(int amount)
        {
            _boundCrystal?.EarnJP(amount);
        }

        /// <summary>Attempts to purchase a skill using JP from the bound crystal. Returns true if successful.</summary>
        public bool TryPurchaseSkill(ISkill skill)
        {
            if (_boundCrystal == null) return false;
            if (_boundCrystal.TryPurchaseSkill(skill))
            {
                // Add the skill to the hero's learned skills
                if (!_learnedSkills.ContainsKey(skill.Id))
                {
                    _learnedSkills[skill.Id] = skill;
                    ApplyPassiveSkills();
                }
                return true;
            }
            return false;
        }

        /// <summary>Gets available JP from the bound crystal.</summary>
        public int GetCurrentJP() => _boundCrystal?.CurrentJP ?? 0;

        /// <summary>Gets total JP earned from the bound crystal.</summary>
        public int GetTotalJP() => _boundCrystal?.TotalJP ?? 0;

        /// <summary>Gets the job level from the bound crystal.</summary>
        public int GetJobLevel() => _boundCrystal?.JobLevel ?? 1;

        /// <summary>Checks if the job is mastered (all skills learned).</summary>
        public bool IsJobMastered() => _boundCrystal?.IsJobMastered() ?? false;

        /// <summary>Restores MP up to max (amount < 0 indicates full restore). Returns true if MP was actually restored.</summary>
        public bool RestoreMP(int amount)
        {
            if (amount < 0)
            {
                if (CurrentMP >= MaxMP) return false; // Already at max MP
                CurrentMP = MaxMP;
                return true;
            }
            if (amount <= 0) return false;
            if (CurrentMP >= MaxMP) return false; // Already at max MP
            CurrentMP += amount;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
            return true;
        }
    }
}

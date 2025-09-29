using System.Collections.Generic;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
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
        public int MaxAP { get; private set; }
        public int CurrentHP { get; private set; }
        public int CurrentAP { get; private set; }

        // Passive modifiers from learned skills
        public int PassiveDefenseBonus { get; set; }
        public float DeflectChance { get; set; }
        public bool EnableCounter { get; set; }
        public int APTickRegen { get; set; }
        public float HealPowerBonus { get; set; }
        public float FireDamageBonus { get; set; }
        public float APCostReduction { get; set; }

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
            LearnInitialSkills();
            ApplyPassiveSkills();
            RecalculateDerived();
            CurrentHP = MaxHP;
            CurrentAP = MaxAP;
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
                Level++;
                leveled = true;
                BaseStats = new StatBlock(BaseStats.Strength + 1, BaseStats.Agility + 1, BaseStats.Vitality + 1, BaseStats.Magic + 1);
                RecalculateDerived();
                AutoLearnNewSkills();
                ApplyPassiveSkills();
            }
            return leveled;
        }

        /// <summary>Gets required XP for the next level.</summary>
        public int RequiredExpForNextLevel() => Level * 100;

        /// <summary>Recomputes HP/AP and caps.</summary>
        public void RecalculateDerived()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            var total = BaseStats.Add(jobStats).Add(GetEquipmentStatBonus());
            MaxHP = 25 + total.Vitality * 5 + GetEquipmentHPBonus(); // Add equipment HP bonus
            MaxAP = 10 + total.Magic * 3 + GetEquipmentAPBonus();     // Add equipment AP bonus
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentAP > MaxAP) CurrentAP = MaxAP;
        }

        /// <summary>Returns current total stats (base + job + equipment).</summary>
        public StatBlock GetTotalStats()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            return BaseStats.Add(jobStats).Add(GetEquipmentStatBonus());
        }

        /// <summary>Inflicts damage, returns true if hero died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            CurrentHP -= amount;
            if (CurrentHP < 0) CurrentHP = 0;
            return CurrentHP == 0;
        }

        /// <summary>Spend AP if sufficient (includes passive cost reduction).</summary>
        public bool SpendAP(int amount)
        {
            if (amount <= 0) return true;
            var reduced = (int)(amount * (1f - APCostReduction));
            if (reduced < 1) reduced = 1;
            if (CurrentAP < reduced) return false;
            CurrentAP -= reduced;
            return true;
        }

        /// <summary>Heals HP up to max.</summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        }

        /// <summary>Per-turn passive AP regen.</summary>
        public void TickRegeneration()
        {
            if (APTickRegen > 0)
            {
                CurrentAP += APTickRegen;
                if (CurrentAP > MaxAP) CurrentAP = MaxAP;
            }
        }

        /// <summary>Equips an item into the appropriate slot if job allows.</summary>
        public bool TryEquip(IItem item)
        {
            if (item == null) return false;
            switch (item.Kind)
            {
                case ItemKind.WeaponSword:
                    if (Job is Jobs.Knight) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponKnuckle:
                    if (Job is Jobs.Monk) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponStaff:
                    if (Job is Jobs.Priest) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponRod:
                    if (Job is Jobs.Mage) { WeaponShield1 = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorMail:
                    if (Job is Jobs.Knight || _extraEquipPermissions.Contains(ItemKind.ArmorMail)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorGi:
                    if (Job is Jobs.Monk || _extraEquipPermissions.Contains(ItemKind.ArmorGi)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.ArmorRobe:
                    if (Job is Jobs.Mage || Job is Jobs.Priest || _extraEquipPermissions.Contains(ItemKind.ArmorRobe)) { Armor = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatHelm:
                    if (Job is Jobs.Knight) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatHeadband:
                    if (Job is Jobs.Monk) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatWizard:
                    if (Job is Jobs.Mage) { Hat = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatPriest:
                    if (Job is Jobs.Priest) { Hat = item; RecalculateDerived(); return true; }
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

        /// <summary>Total flat AP bonus from gear.</summary>
        public int GetEquipmentAPBonus()
        {
            int ap = 0;
            if (WeaponShield1 is IGear weaponGear) ap += weaponGear.APBonus;
            if (Armor is IGear armorGear) ap += armorGear.APBonus;
            if (Hat is IGear helmGear) ap += helmGear.APBonus;
            if (WeaponShield2 is IGear shieldGear) ap += shieldGear.APBonus;
            if (Accessory1 is IGear acc1Gear) ap += acc1Gear.APBonus;
            if (Accessory2 is IGear acc2Gear) ap += acc2Gear.APBonus;
            return ap;
        }

        private void LearnInitialSkills() => AutoLearnNewSkills();

        private void AutoLearnNewSkills()
        {
            var known = new HashSet<string>(_learnedSkills.Keys);
            var buffer = new List<ISkill>(4);
            Job.GetLearnableSkills(Level, known, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                var skill = buffer[i];
                _learnedSkills[skill.Id] = skill;
                _boundCrystal?.AddLearnedSkill(skill.Id);
            }
        }

        private void ApplyPassiveSkills()
        {
            PassiveDefenseBonus = 0;
            DeflectChance = 0;
            EnableCounter = false;
            APTickRegen = 0;
            HealPowerBonus = 0f;
            FireDamageBonus = 0f;
            APCostReduction = 0f;
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
                if (s.APCost > CurrentAP) continue;
                if (s.APCost > bestCost) { best = s; bestCost = s.APCost; }
            }
            return best;
        }

        public string? TryUseSkill(ISkill skill, IEnemy primary, List<IEnemy> surrounding, IAttackResolver resolver)
        {
            if (skill.Kind != SkillKind.Active) return null;
            if (!SpendAP(skill.APCost)) return null;
            return skill.Execute(this, primary, surrounding, resolver);
        }
    }
}

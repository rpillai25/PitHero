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
        public IItem? Weapon { get; private set; }
        public IItem? Armor { get; private set; }
        public IItem? Helm { get; private set; }
        public IItem? Shield { get; private set; }
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
                    if (Job is Jobs.Knight) { Weapon = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponKnuckle:
                    if (Job is Jobs.Monk) { Weapon = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponStaff:
                    if (Job is Jobs.Priest) { Weapon = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.WeaponRod:
                    if (Job is Jobs.Mage) { Weapon = item; RecalculateDerived(); return true; }
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
                    if (Job is Jobs.Knight) { Helm = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatHeadband:
                    if (Job is Jobs.Monk) { Helm = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatWizard:
                    if (Job is Jobs.Mage) { Helm = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.HatPriest:
                    if (Job is Jobs.Priest) { Helm = item; RecalculateDerived(); return true; }
                    return false;
                case ItemKind.Shield:
                    // All classes can equip shields for now
                    Shield = item; RecalculateDerived(); return true;
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
                case EquipmentSlot.Weapon: if (Weapon != null) { Weapon = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Armor: if (Armor != null) { Armor = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Hat: if (Helm != null) { Helm = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Shield: if (Shield != null) { Shield = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Accessory1: if (Accessory1 != null) { Accessory1 = null; RecalculateDerived(); return true; } break;
                case EquipmentSlot.Accessory2: if (Accessory2 != null) { Accessory2 = null; RecalculateDerived(); return true; } break;
            }
            return false;
        }

        /// <summary>Sum of all equipment stat bonuses.</summary>
        private StatBlock GetEquipmentStatBonus()
        {
            var sum = new StatBlock(0, 0, 0, 0);
            if (Weapon != null) sum = sum.Add(Weapon.StatBonus);
            if (Armor != null) sum = sum.Add(Armor.StatBonus);
            if (Helm != null) sum = sum.Add(Helm.StatBonus);
            if (Shield != null) sum = sum.Add(Shield.StatBonus);
            if (Accessory1 != null) sum = sum.Add(Accessory1.StatBonus);
            if (Accessory2 != null) sum = sum.Add(Accessory2.StatBonus);
            return sum;
        }

        /// <summary>Total flat attack bonus from gear.</summary>
        public int GetEquipmentAttackBonus()
        {
            int atk = 0;
            if (Weapon != null) atk += Weapon.AttackBonus;
            if (Shield != null) atk += Shield.AttackBonus;
            if (Accessory1 != null) atk += Accessory1.AttackBonus;
            if (Accessory2 != null) atk += Accessory2.AttackBonus;
            return atk;
        }

        /// <summary>Total flat defense bonus from gear plus passives.</summary>
        public int GetEquipmentDefenseBonus()
        {
            int def = PassiveDefenseBonus;
            if (Armor != null) def += Armor.DefenseBonus;
            if (Helm != null) def += Helm.DefenseBonus;
            if (Shield != null) def += Shield.DefenseBonus;
            if (Accessory1 != null) def += Accessory1.DefenseBonus;
            if (Accessory2 != null) def += Accessory2.DefenseBonus;
            return def;
        }

        /// <summary>Total flat HP bonus from gear.</summary>
        public int GetEquipmentHPBonus()
        {
            int hp = 0;
            if (Weapon != null) hp += Weapon.HPBonus;
            if (Armor != null) hp += Armor.HPBonus;
            if (Helm != null) hp += Helm.HPBonus;
            if (Shield != null) hp += Shield.HPBonus;
            if (Accessory1 != null) hp += Accessory1.HPBonus;
            if (Accessory2 != null) hp += Accessory2.HPBonus;
            return hp;
        }

        /// <summary>Total flat AP bonus from gear.</summary>
        public int GetEquipmentAPBonus()
        {
            int ap = 0;
            if (Weapon != null) ap += Weapon.APBonus;
            if (Armor != null) ap += Armor.APBonus;
            if (Helm != null) ap += Helm.APBonus;
            if (Shield != null) ap += Shield.APBonus;
            if (Accessory1 != null) ap += Accessory1.APBonus;
            if (Accessory2 != null) ap += Accessory2.APBonus;
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

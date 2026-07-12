using Nez;
using PitHero;
using PitHero.Services;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Simple gear item with stat and flat bonuses.
    /// Supports elemental types and resistances for integration with the elemental combat system.
    /// Supports pit-tier scaling: a tier-N copy has higher stats and a "+N" suffix on its display name.
    /// </summary>
    /// <remarks>
    /// Elemental System Integration:
    /// - All gear has ElementalProperties (Neutral, Fire, Water, Earth, Wind, Light, Dark)
    /// - Weapons typically deal elemental damage matching their element
    /// - Defensive gear (armor/shields/helms) can have custom resistances
    /// - Resistances are percentage-based (0.25 = 25% damage reduction, -0.15 = 15% extra damage)
    /// - Defensive gear typically resists its own element and is weak to opposing element
    ///
    /// Current Equipment Elemental Assignments:
    /// Weapons:
    ///   - ShortSword: Neutral
    ///   - LongSword: Fire
    ///
    /// Armor:
    ///   - LeatherArmor: Neutral
    ///   - IronArmor: Earth (25% Earth resist, -15% Wind weakness)
    ///
    /// Shields:
    ///   - WoodenShield: Neutral
    ///   - IronShield: Water (30% Water resist, -15% Fire weakness)
    ///
    /// Helms:
    ///   - SquireHelm: Neutral
    ///   - IronHelm: Earth (20% Earth resist, -10% Wind weakness)
    ///
    /// Accessories:
    ///   - RingOfPower: Neutral
    ///   - NecklaceOfHealth: Light
    ///   - ProtectRing: Neutral
    ///   - MagicChain: Dark
    /// </remarks>
    public sealed class Gear : IGear
    {
        private readonly string _nameKey;
        private readonly string _descKey;
        private readonly string _spriteName;
        private TextService _textService;

        // ── Tier-scaling state ────────────────────────────────────────────────────────
        private readonly int _tier;
        // Tier suffix cached at construction ("" for tier 1, "+2" for tier 2, etc.)
        private readonly string _tierSuffix;
        // Fully composed "BaseName+N" string cached once TextService resolves.
        // null = not yet resolved; assigned on first Name access when tier >= 2.
        private string _cachedTieredName;

        private TextService GetTextService()
        {
            // Core.Instance is null in headless hosts (unit tests, virtual balance runs);
            // Core.Services would throw there, so fall back to the raw text key instead.
            if (_textService == null && Core.Instance != null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService;
        }

        public string Name
        {
            get
            {
                if (_tier <= 1)
                    return GetTextService()?.DisplayText(TextType.Inventory, _nameKey) ?? _nameKey;

                // Tier >= 2: compose once and cache to avoid per-frame allocation.
                if (_cachedTieredName != null)
                    return _cachedTieredName;

                string baseName = GetTextService()?.DisplayText(TextType.Inventory, _nameKey) ?? _nameKey;
                _cachedTieredName = baseName + _tierSuffix;
                return _cachedTieredName;
            }
        }

        /// <summary>Sprite name used to look up the item's sprite in the Items atlas. Derived from the item class name.</summary>
        public string SpriteName => _spriteName;

        public ItemKind Kind { get; }
        public ItemRarity Rarity { get; }
        public string Description => GetTextService()?.DisplayText(TextType.Inventory, _descKey) ?? _descKey;
        public int Price { get; }
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HPBonus { get; }
        public int MPBonus { get; }
        public ElementalProperties ElementalProps { get; }
        public JobType AllowedJobs { get; }

        /// <summary>
        /// Pit tier this gear belongs to.  1 = base; 2+ = tier-scaled copy with bonus stats and a "+N" name suffix.
        /// </summary>
        public int Tier => _tier;

        /// <summary>
        /// Creates a new Gear item with specified properties.
        /// </summary>
        /// <param name="name">Display name of the gear item</param>
        /// <param name="kind">Type of equipment (weapon, armor, shield, etc.)</param>
        /// <param name="rarity">Item rarity level</param>
        /// <param name="description">Item description text</param>
        /// <param name="price">Gold cost to purchase</param>
        /// <param name="stats">Stat bonuses (Strength, Agility, Vitality, Magic)</param>
        /// <param name="atk">Flat attack bonus (for weapons primarily)</param>
        /// <param name="def">Flat defense bonus (for armor/shields/helms primarily)</param>
        /// <param name="hp">Flat HP bonus</param>
        /// <param name="mp">Flat MP bonus</param>
        /// <param name="elementalProps">Elemental properties with type and optional resistances. Defaults to Neutral if not specified.</param>
        /// <param name="allowedJobs">Bitflag of job classes that can equip this gear. Defaults based on ItemKind if not specified.</param>
        /// <param name="tier">Pit tier (1 = base, 2+ = scaled copy). Default 1 keeps all existing call sites unchanged.</param>
        public Gear(string name, ItemKind kind, ItemRarity rarity, string description, int price, in StatBlock stats, int atk = 0, int def = 0, int hp = 0, int mp = 0, ElementalProperties elementalProps = null, JobType? allowedJobs = null, int tier = 1)
        {
            _nameKey = name;
            _descKey = description;
            const string prefix = "Inv_";
            const string suffix = "_Name";
            _spriteName = (name.StartsWith(prefix) && name.EndsWith(suffix))
                ? name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length)
                : name;
            Kind = kind;
            Rarity = rarity;
            Price = price;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
            HPBonus = hp;
            MPBonus = mp;
            ElementalProps = elementalProps ?? new ElementalProperties(ElementType.Neutral);
            AllowedJobs = allowedJobs ?? GetDefaultAllowedJobs(kind);
            _tier = tier < 1 ? 1 : tier;
            _tierSuffix = _tier >= 2 ? ("+" + _tier) : string.Empty;
        }

        /// <summary>
        /// Returns a new Gear with stats scaled for the given pit tier and depth delta.
        /// For tier ≤ 1 returns source unchanged (no allocation).
        /// </summary>
        /// <param name="source">Base gear to scale from.</param>
        /// <param name="tier">Target pit tier (must be ≥ 2 to produce a new instance).</param>
        /// <param name="depthDelta">Additional effective depth beyond tier-1 baseline (e.g. (tier-1) * MaxBiomeLevel).</param>
        public static Gear CreateTierScaledCopy(Gear source, int tier, int depthDelta)
        {
            if (source == null) return null;
            if (tier <= 1) return source;

            int atkDelta  = BalanceConfig.CalculateEquipmentAttackBonusDelta(depthDelta, source.Rarity);
            int defDelta  = BalanceConfig.CalculateEquipmentDefenseBonusDelta(depthDelta, source.Rarity);
            int statDelta = BalanceConfig.CalculateEquipmentStatBonusDelta(depthDelta, source.Rarity);

            var scaledStats = new StatBlock(
                source.StatBonus.Strength  + statDelta,
                source.StatBonus.Agility   + statDelta,
                source.StatBonus.Vitality  + statDelta,
                source.StatBonus.Magic     + statDelta);

            // HP/MP: no dedicated delta formula — scale linearly with tier.
            return new Gear(
                source._nameKey,
                source.Kind,
                source.Rarity,
                source._descKey,
                source.Price * tier,
                in scaledStats,
                atk:          source.AttackBonus  + atkDelta,
                def:          source.DefenseBonus + defDelta,
                hp:           source.HPBonus  * tier,
                mp:           source.MPBonus  * tier,
                elementalProps: source.ElementalProps,
                allowedJobs:  source.AllowedJobs,
                tier:         tier);
        }

        /// <summary>Returns the default allowed jobs for a given ItemKind.</summary>
        public static JobType GetDefaultAllowedJobs(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.WeaponSword: return JobType.Knight;
                case ItemKind.WeaponKnife: return JobType.Thief | JobType.Mage;
                case ItemKind.WeaponKnuckle: return JobType.Monk;
                case ItemKind.WeaponStaff: return JobType.Priest;
                case ItemKind.WeaponRod: return JobType.Mage;
                case ItemKind.WeaponBow: return JobType.Archer;
                case ItemKind.WeaponHammer: return JobType.Knight | JobType.Priest;
                case ItemKind.ArmorMail: return JobType.Knight;
                case ItemKind.ArmorGi: return JobType.Monk;
                case ItemKind.ArmorRobe: return JobType.Mage | JobType.Priest;
                case ItemKind.HatHelm: return JobType.Knight;
                case ItemKind.HatHeadband: return JobType.Monk;
                case ItemKind.HatWizard: return JobType.Mage;
                case ItemKind.HatPriest: return JobType.Priest;
                case ItemKind.Shield: return JobType.All;
                case ItemKind.Accessory: return JobType.All;
                default: return JobType.All;
            }
        }
    }
}

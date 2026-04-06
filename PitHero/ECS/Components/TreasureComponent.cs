using Nez;
using Nez.Sprites;
using PitHero.Config;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Loot;

namespace PitHero.ECS.Components
{
    /// <summary>Captures the active party's job composition for biased loot drops.</summary>
    public struct LootJobContext
    {
        /// <summary>The hero's job flag (highest priority).</summary>
        public JobType HeroJob;

        /// <summary>Bitwise OR of all hired mercenary job flags.</summary>
        public JobType MercJobs;

        /// <summary>
        /// Per-job drop counts sourced from <c>LootDropTracker</c>.
        /// Index 0=Knight, 1=Monk, 2=Mage, 3=Priest, 4=Thief, 5=Archer.
        /// Null when deficit tracking is unavailable (flat static weights apply).
        /// </summary>
        public int[] JobDropCounts;

        /// <summary>
        /// Cached maximum drop count across all six job slots.
        /// 0 when <see cref="JobDropCounts"/> is null or all counts are zero.
        /// </summary>
        public int MaxDropCount;

        /// <summary>True when no party context is available (default behavior / flat random).</summary>
        public bool IsEmpty => HeroJob == JobType.None && MercJobs == JobType.None;

        public static LootJobContext Empty => default;
    }

    /// <summary>
    /// Component that manages treasure chest state and appearance
    /// </summary>
    public class TreasureComponent : Component
    {
        public enum TreasureState
        {
            CLOSED,
            OPEN
        }

        private TreasureState _state = TreasureState.CLOSED;
        private int _level = 1;
        private int _pitLevel = 1;
        private SpriteRenderer _baseRenderer;
        private SpriteRenderer _woodRenderer;
        private SpriteAtlas _actorsAtlas;
        private IItem _containedItem;

        /// <summary>
        /// Current state of the treasure chest
        /// </summary>
        public TreasureState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    UpdateSprites();
                }
            }
        }

        /// <summary>
        /// Treasure level (1-5) which determines the wood color
        /// </summary>
        public int Level
        {
            get => _level;
            set
            {
                if (_level != value && value >= 1 && value <= 5)
                {
                    _level = value;
                    UpdateWoodColor();
                }
            }
        }

        /// <summary>
        /// Item contained within this treasure chest
        /// </summary>
        public IItem ContainedItem
        {
            get => _containedItem;
            set => _containedItem = value;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            LoadAtlas();
            InitializeRenderers();
        }

        private void LoadAtlas()
        {
            try
            {
                _actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[TreasureComponent] Failed to load Actors.atlas: {ex.Message}");
            }
        }

        private void InitializeRenderers()
        {
            if (_actorsAtlas == null)
            {
                Debug.Warn("[TreasureComponent] Cannot initialize renderers without atlas");
                return;
            }

            // Create base renderer on the treasure base layer
            _baseRenderer = Entity.AddComponent(new SpriteRenderer());
            _baseRenderer.SetRenderLayer(GameConfig.RenderLayerTreasureBase);

            // Create wood renderer on the treasure wood layer
            _woodRenderer = Entity.AddComponent(new SpriteRenderer());
            _woodRenderer.SetRenderLayer(GameConfig.RenderLayerTreasureWood);

            UpdateSprites();
            UpdateWoodColor();
        }

        private void UpdateSprites()
        {
            if (_baseRenderer == null || _woodRenderer == null || _actorsAtlas == null)
                return;

            // Load appropriate sprites based on state
            if (_state == TreasureState.CLOSED)
            {
                _baseRenderer.Sprite = _actorsAtlas.GetSprite("treasure_base_closed");
                _woodRenderer.Sprite = _actorsAtlas.GetSprite("treasure_wood_closed");
            }
            else
            {
                _baseRenderer.Sprite = _actorsAtlas.GetSprite("treasure_base_open");
                _woodRenderer.Sprite = _actorsAtlas.GetSprite("treasure_wood_open");
            }
        }

        private void UpdateWoodColor()
        {
            if (_woodRenderer == null)
                return;

            // Set wood color based on treasure level
            _woodRenderer.Color = _level switch
            {
                1 => GameConfig.TREASURE_SHADE_1, // Brown - Common
                2 => GameConfig.TREASURE_SHADE_2, // Green - Special
                3 => GameConfig.TREASURE_SHADE_3, // Blue - Rare
                4 => GameConfig.TREASURE_SHADE_4, // Purple - Epic
                5 => GameConfig.TREASURE_SHADE_5, // Gold - Legendary
                _ => GameConfig.TREASURE_SHADE_1  // Default to brown
            };
        }

        /// <summary>
        /// Generate an appropriate item for the given treasure level
        /// </summary>
        public static IItem GenerateItemForTreasureLevel(int treasureLevel)
        {
            var rarity = RarityUtils.GetRarityForTreasureLevel(treasureLevel);

            // Generate potions based on rarity
            return rarity switch
            {
                ItemRarity.Normal => GenerateNormalPotion(),
                ItemRarity.Uncommon => GenerateNormalPotion(),
                ItemRarity.Rare => GenerateMidPotion(),
                ItemRarity.Epic => GenerateFullPotion(),
                ItemRarity.Legendary => GenerateFullPotion(),
                _ => PotionItems.HPPotion() // Default to basic HP potion
            };
        }

        /// <summary>
        /// Generate cave biome loot for the given treasure level.
        /// Rolls consumable vs equipment first using CaveConsumableDropRate (60/40 split).
        /// </summary>
        public static IItem GenerateCaveItemForTreasureLevel(int treasureLevel, LootJobContext ctx = default)
        {
            bool isConsumable = Nez.Random.NextFloat() < BalanceConfig.CaveConsumableDropRate;

            if (isConsumable)
            {
                return treasureLevel switch
                {
                    1 => GenerateNormalPotion(),
                    2 => GenerateCaveUncommonEquipment(ctx),
                    3 => GenerateMidPotion(),
                    _ => GenerateItemForTreasureLevel(treasureLevel)
                };
            }

            return treasureLevel switch
            {
                1 => GenerateCaveCommonEquipment(ctx),
                2 => GenerateCaveUncommonEquipment(ctx),
                3 => GearItems.NecklaceOfHealth(),
                _ => GenerateItemForTreasureLevel(treasureLevel)
            };
        }

        /// <summary>
        /// Generate a random Normal (Common) potion
        /// </summary>
        private static IItem GenerateNormalPotion()
        {
            var random = Nez.Random.NextInt(3);
            return random switch
            {
                0 => PotionItems.HPPotion(),
                1 => PotionItems.MPPotion(),
                2 => PotionItems.MixPotion(),
                _ => PotionItems.HPPotion()
            };
        }

        /// <summary>
        /// Generate a random Mid (Rare) potion
        /// </summary>
        private static IItem GenerateMidPotion()
        {
            var random = Nez.Random.NextInt(3);
            return random switch
            {
                0 => PotionItems.MidHPPotion(),
                1 => PotionItems.MidMPPotion(),
                2 => PotionItems.MidMixPotion(),
                _ => PotionItems.MidHPPotion()
            };
        }

        /// <summary>
        /// Generate a random Full (Epic) potion
        /// </summary>
        private static IItem GenerateFullPotion()
        {
            var random = Nez.Random.NextInt(3);
            return random switch
            {
                0 => PotionItems.FullHPPotion(),
                1 => PotionItems.FullMPPotion(),
                2 => PotionItems.FullMixPotion(),
                _ => PotionItems.FullHPPotion()
            };
        }

        /// <summary>
        /// Generate cave common equipment from all Normal-rarity cave gear items (56 items).
        /// </summary>
        private static readonly ItemKind[] _commonPoolKinds = new ItemKind[]
        {
            ItemKind.WeaponSword,    // 0:  RustyBlade
            ItemKind.WeaponSword,    // 1:  ShortSword
            ItemKind.WeaponSword,    // 2:  CavernCutter
            ItemKind.WeaponSword,    // 3:  CaveStalkersBlade
            ItemKind.WeaponSword,    // 4:  GraniteBlade
            ItemKind.WeaponSword,    // 5:  LongSword
            ItemKind.WeaponSword,    // 6:  MinersPickSword
            ItemKind.WeaponSword,    // 7:  ShadowFang
            ItemKind.WeaponSword,    // 8:  SpelunkersSaber
            ItemKind.WeaponSword,    // 9:  StoneSword
            ItemKind.WeaponSword,    // 10: TorchBlade
            ItemKind.WeaponSword,    // 11: FlameHatchet
            ItemKind.WeaponSword,    // 12: MinersAxe
            ItemKind.WeaponSword,    // 13: StoneHatchet
            ItemKind.WeaponSword,    // 14: WoodcuttersAxe
            ItemKind.WeaponKnife,    // 15: CaveShiv
            ItemKind.WeaponKnife,    // 16: RustyDagger
            ItemKind.WeaponKnife,    // 17: SilentFang
            ItemKind.WeaponSword,    // 18: StoneLance
            ItemKind.WeaponSword,    // 19: WoodenSpear
            ItemKind.WeaponHammer,   // 20: Mallet
            ItemKind.WeaponHammer,   // 21: StoneCrusher
            ItemKind.WeaponStaff,    // 22: TorchStaff
            ItemKind.WeaponRod,      // 23: WalkingStick
            ItemKind.ArmorRobe,      // 24: BurlapTunic
            ItemKind.ArmorGi,        // 25: CaveExplorersVest
            ItemKind.ArmorMail,      // 26: ChainShirt
            ItemKind.ArmorGi,        // 27: HardenedLeather
            ItemKind.ArmorGi,        // 28: HideVest
            ItemKind.ArmorMail,      // 29: IronArmor
            ItemKind.ArmorMail,      // 30: LeatherArmor
            ItemKind.ArmorGi,        // 31: PaddedArmor
            ItemKind.ArmorMail,      // 32: ScaleMail
            ItemKind.ArmorGi,        // 33: StuddedLeather
            ItemKind.ArmorRobe,      // 34: TatteredCloth
            ItemKind.Shield,         // 35: CaveGuard
            ItemKind.Shield,         // 36: HideShield
            ItemKind.Shield,         // 37: IronBuckler
            ItemKind.Shield,         // 38: IronShield
            ItemKind.Shield,         // 39: KiteShield
            ItemKind.Shield,         // 40: ReinforcedBuckler
            ItemKind.Shield,         // 41: RoundShield
            ItemKind.Shield,         // 42: StoneShield
            ItemKind.Shield,         // 43: WoodenPlank
            ItemKind.Shield,         // 44: WoodenShield
            ItemKind.HatHelm,        // 45: Bascinet
            ItemKind.HatHeadband,    // 46: CaveExplorersHood
            ItemKind.HatHelm,        // 47: ChainCoif
            ItemKind.HatHeadband,    // 48: ClothCap
            ItemKind.HatHeadband,    // 49: HideHood
            ItemKind.HatHelm,        // 50: IronHelm
            ItemKind.HatHeadband,    // 51: LeatherCap
            ItemKind.HatHeadband,    // 52: PaddedCoif
            ItemKind.HatHelm,        // 53: ReinforcedCap
            ItemKind.HatHelm,        // 54: SquireHelm
            ItemKind.Accessory,      // 55: ProtectRing
        };

        /// <summary>
        /// Generate cave uncommon equipment from all Uncommon-rarity cave gear items (77 items).
        /// </summary>
        private static readonly ItemKind[] _uncommonPoolKinds = new ItemKind[]
        {
            ItemKind.WeaponSword,    // 0:  AbyssFang
            ItemKind.WeaponSword,    // 1:  CrystalEdge
            ItemKind.WeaponSword,    // 2:  DepthsReaver
            ItemKind.WeaponSword,    // 3:  DiamondEdge
            ItemKind.WeaponSword,    // 4:  EmberSword
            ItemKind.WeaponSword,    // 5:  GloomBlade
            ItemKind.WeaponSword,    // 6:  InfernoEdge
            ItemKind.WeaponSword,    // 7:  LavaForgedSword
            ItemKind.WeaponSword,    // 8:  MagmaBlade
            ItemKind.WeaponSword,    // 9:  PitLordsSword
            ItemKind.WeaponSword,    // 10: QuartzSaber
            ItemKind.WeaponSword,    // 11: StalagmiteSword
            ItemKind.WeaponSword,    // 12: UndergroundRapier
            ItemKind.WeaponSword,    // 13: VoidCutter
            ItemKind.WeaponSword,    // 14: CrystalCleaver
            ItemKind.WeaponSword,    // 15: ObsidianCleaver
            ItemKind.WeaponSword,    // 16: ShadowSplitter
            ItemKind.WeaponSword,    // 17: VolcanicAxe
            ItemKind.WeaponKnife,    // 18: AssassinsEdge
            ItemKind.WeaponKnife,    // 19: SerpentsTooth
            ItemKind.WeaponKnife,    // 20: ShadowStiletto
            ItemKind.WeaponSword,    // 21: CavePike
            ItemKind.WeaponSword,    // 22: FlameLance
            ItemKind.WeaponSword,    // 23: InfernalPike
            ItemKind.WeaponSword,    // 24: StalactiteSpear
            ItemKind.WeaponHammer,   // 25: GeologistsHammer
            ItemKind.WeaponHammer,   // 26: MagmaMaul
            ItemKind.WeaponHammer,   // 27: QuakeHammer
            ItemKind.WeaponStaff,    // 28: EarthenStaff
            ItemKind.WeaponRod,      // 29: EmberRod
            ItemKind.WeaponStaff,    // 30: ShadowwoodStaff
            ItemKind.ArmorMail,      // 31: AbyssPlate
            ItemKind.ArmorMail,      // 32: CrystalGuard
            ItemKind.ArmorMail,      // 33: DiamondMail
            ItemKind.ArmorMail,      // 34: EmberguardMail
            ItemKind.ArmorMail,      // 35: GranitePlate
            ItemKind.ArmorMail,      // 36: LavaplateArmor
            ItemKind.ArmorMail,      // 37: MagmaBlastPlate
            ItemKind.ArmorMail,      // 38: PitLordsArmor
            ItemKind.ArmorMail,      // 39: ReinforcedPlate
            ItemKind.ArmorGi,        // 40: ShadowVest
            ItemKind.ArmorMail,      // 41: SteelCuirass
            ItemKind.ArmorMail,      // 42: StonePlate
            ItemKind.ArmorMail,      // 43: Voidmail
            ItemKind.ArmorMail,      // 44: VolcanicArmor
            ItemKind.Shield,         // 45: AbyssWall
            ItemKind.Shield,         // 46: CrystalBarrier
            ItemKind.Shield,         // 47: DiamondBarrier
            ItemKind.Shield,         // 48: EmberShield
            ItemKind.Shield,         // 49: GraniteGuard
            ItemKind.Shield,         // 50: HeaterShield
            ItemKind.Shield,         // 51: InfernoGuard
            ItemKind.Shield,         // 52: LavaShield
            ItemKind.Shield,         // 53: MagmaWall
            ItemKind.Shield,         // 54: PitLordsAegis
            ItemKind.Shield,         // 55: QuartzWall
            ItemKind.Shield,         // 56: ShadowGuard
            ItemKind.Shield,         // 57: SteelShield
            ItemKind.Shield,         // 58: TowerShield
            ItemKind.Shield,         // 59: VoidBarrier
            ItemKind.HatHelm,        // 60: AbyssHelm
            ItemKind.HatHeadband,    // 61: CrystalCirclet
            ItemKind.HatHeadband,    // 62: DiamondCirclet
            ItemKind.HatHelm,        // 63: EmberHelm
            ItemKind.HatHelm,        // 64: GreatHelm
            ItemKind.HatHelm,        // 65: InfernoCrown
            ItemKind.HatHelm,        // 66: LavaCrown
            ItemKind.HatHelm,        // 67: MagmaHelm
            ItemKind.HatHelm,        // 68: PitLordsCrown
            ItemKind.HatHelm,        // 69: QuartzHelm
            ItemKind.HatHeadband,    // 70: ShadowCowl
            ItemKind.HatHelm,        // 71: SteelHelm
            ItemKind.HatHelm,        // 72: StoneCrown
            ItemKind.HatHelm,        // 73: VoidMask
            ItemKind.HatHelm,        // 74: WingedHelm
            ItemKind.Accessory,      // 75: MagicChain
            ItemKind.Accessory,      // 76: RingOfPower
        };

        /// <summary>
        /// Pre-allocated weight buffers for AOT compliance — no heap allocation during pool selection.
        /// Sizes match the corresponding pool arrays: common=56, uncommon=77.
        /// </summary>
        private static readonly int[] _commonWeightBuffer = new int[56];
        private static readonly int[] _uncommonWeightBuffer = new int[77];

        /// <summary>
        /// Selects a weighted pool index biased toward gear usable by the active party.
        /// Falls back to flat random when no party context is available.
        /// Applies a deficit bonus so party members who are behind on drops get higher weights.
        /// </summary>
        private static int SelectWeightedPoolIndex(ItemKind[] poolKinds, LootJobContext ctx)
        {
            if (ctx.IsEmpty)
                return Nez.Random.NextInt(poolKinds.Length);

            int[] weights = poolKinds.Length <= 56 ? _commonWeightBuffer : _uncommonWeightBuffer;
            int totalWeight = 0;

            for (int i = 0; i < poolKinds.Length; i++)
            {
                JobType allowed = Gear.GetDefaultAllowedJobs(poolKinds[i]);
                int weight;

                if (allowed == JobType.All)
                {
                    weight = BalanceConfig.LootWeightAllJobs;
                }
                else if ((allowed & ctx.HeroJob) != 0)
                {
                    weight = BalanceConfig.LootWeightHeroJob;
                    weight += ComputeDeficitBonus(allowed, ctx);
                }
                else if ((allowed & ctx.MercJobs) != 0)
                {
                    weight = BalanceConfig.LootWeightMercJob;
                    weight += ComputeDeficitBonus(allowed, ctx);
                }
                else
                {
                    weight = BalanceConfig.LootWeightNoPartyJob;
                }

                weights[i] = weight;
                totalWeight += weight;
            }

            int roll = Nez.Random.NextInt(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < poolKinds.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return i;
            }

            return poolKinds.Length - 1;
        }

        /// <summary>
        /// Computes an additional weight bonus based on how far behind the most-deficit
        /// active-party job (that can equip this item) is relative to the leader.
        /// Returns 0 when no deficit data is available or all counts are zero.
        /// The deficit is capped at <see cref="GameConfig.MaxLootDeficit"/> to prevent
        /// overflow and to keep weights sensible for extremely long runs.
        /// </summary>
        private static int ComputeDeficitBonus(JobType allowed, LootJobContext ctx)
        {
            if (ctx.JobDropCounts == null || ctx.MaxDropCount == 0)
                return 0;

            JobType partyJobs = ctx.HeroJob | ctx.MercJobs;
            int maxDeficit = 0;

            for (int i = 0; i < LootDropTracker.JobFlagCount; i++)
            {
                JobType flag = LootDropTracker.GetJobFlag(i);
                if ((allowed & flag) == 0)
                    continue; // item can't be used by this job
                if ((partyJobs & flag) == 0)
                    continue; // job not in active party

                int deficit = ctx.MaxDropCount - ctx.JobDropCounts[i];
                if (deficit > maxDeficit)
                    maxDeficit = deficit;
            }

            if (maxDeficit > GameConfig.MaxLootDeficit)
                maxDeficit = GameConfig.MaxLootDeficit;

            return maxDeficit * BalanceConfig.LootDeficitBonusPerDrop;
        }

        /// <summary>
        /// Generate cave common equipment from all Normal-rarity cave gear items (56 items).
        /// </summary>
        private static IItem GenerateCaveCommonEquipment(LootJobContext ctx = default)
        {
            int index = SelectWeightedPoolIndex(_commonPoolKinds, ctx);
            switch (index)
            {
                // Swords (Normal) — 0..10
                case 0:  return GearItems.RustyBlade();
                case 1:  return GearItems.ShortSword();
                case 2:  return GearItems.CavernCutter();
                case 3:  return GearItems.CaveStalkersBlade();
                case 4:  return GearItems.GraniteBlade();
                case 5:  return GearItems.LongSword();
                case 6:  return GearItems.MinersPickSword();
                case 7:  return GearItems.ShadowFang();
                case 8:  return GearItems.SpelunkersSaber();
                case 9:  return GearItems.StoneSword();
                case 10: return GearItems.TorchBlade();
                // Axes (Normal) — 11..14
                case 11: return GearItems.FlameHatchet();
                case 12: return GearItems.MinersAxe();
                case 13: return GearItems.StoneHatchet();
                case 14: return GearItems.WoodcuttersAxe();
                // Daggers (Normal) — 15..17
                case 15: return GearItems.CaveShiv();
                case 16: return GearItems.RustyDagger();
                case 17: return GearItems.SilentFang();
                // Spears (Normal) — 18..19
                case 18: return GearItems.StoneLance();
                case 19: return GearItems.WoodenSpear();
                // Hammers (Normal) — 20..21
                case 20: return GearItems.Mallet();
                case 21: return GearItems.StoneCrusher();
                // Staves (Normal) — 22..23
                case 22: return GearItems.TorchStaff();
                case 23: return GearItems.WalkingStick();
                // Armor (Normal) — 24..34
                case 24: return GearItems.BurlapTunic();
                case 25: return GearItems.CaveExplorersVest();
                case 26: return GearItems.ChainShirt();
                case 27: return GearItems.HardenedLeather();
                case 28: return GearItems.HideVest();
                case 29: return GearItems.IronArmor();
                case 30: return GearItems.LeatherArmor();
                case 31: return GearItems.PaddedArmor();
                case 32: return GearItems.ScaleMail();
                case 33: return GearItems.StuddedLeather();
                case 34: return GearItems.TatteredCloth();
                // Shields (Normal) — 35..44
                case 35: return GearItems.CaveGuard();
                case 36: return GearItems.HideShield();
                case 37: return GearItems.IronBuckler();
                case 38: return GearItems.IronShield();
                case 39: return GearItems.KiteShield();
                case 40: return GearItems.ReinforcedBuckler();
                case 41: return GearItems.RoundShield();
                case 42: return GearItems.StoneShield();
                case 43: return GearItems.WoodenPlank();
                case 44: return GearItems.WoodenShield();
                // Helms (Normal) — 45..54
                case 45: return GearItems.Bascinet();
                case 46: return GearItems.CaveExplorersHood();
                case 47: return GearItems.ChainCoif();
                case 48: return GearItems.ClothCap();
                case 49: return GearItems.HideHood();
                case 50: return GearItems.IronHelm();
                case 51: return GearItems.LeatherCap();
                case 52: return GearItems.PaddedCoif();
                case 53: return GearItems.ReinforcedCap();
                case 54: return GearItems.SquireHelm();
                // Accessories (Normal) — 55
                default: return GearItems.ProtectRing();
            }
        }

        /// <summary>
        /// Generate cave uncommon equipment from all Uncommon-rarity cave gear items (77 items).
        /// </summary>
        private static IItem GenerateCaveUncommonEquipment(LootJobContext ctx = default)
        {
            int index = SelectWeightedPoolIndex(_uncommonPoolKinds, ctx);
            switch (index)
            {
                // Swords (Uncommon) — 0..13
                case 0:  return GearItems.AbyssFang();
                case 1:  return GearItems.CrystalEdge();
                case 2:  return GearItems.DepthsReaver();
                case 3:  return GearItems.DiamondEdge();
                case 4:  return GearItems.EmberSword();
                case 5:  return GearItems.GloomBlade();
                case 6:  return GearItems.InfernoEdge();
                case 7:  return GearItems.LavaForgedSword();
                case 8:  return GearItems.MagmaBlade();
                case 9:  return GearItems.PitLordsSword();
                case 10: return GearItems.QuartzSaber();
                case 11: return GearItems.StalagmiteSword();
                case 12: return GearItems.UndergroundRapier();
                case 13: return GearItems.VoidCutter();
                // Axes (Uncommon) — 14..17
                case 14: return GearItems.CrystalCleaver();
                case 15: return GearItems.ObsidianCleaver();
                case 16: return GearItems.ShadowSplitter();
                case 17: return GearItems.VolcanicAxe();
                // Daggers (Uncommon) — 18..20
                case 18: return GearItems.AssassinsEdge();
                case 19: return GearItems.SerpentsTooth();
                case 20: return GearItems.ShadowStiletto();
                // Spears (Uncommon) — 21..24
                case 21: return GearItems.CavePike();
                case 22: return GearItems.FlameLance();
                case 23: return GearItems.InfernalPike();
                case 24: return GearItems.StalactiteSpear();
                // Hammers (Uncommon) — 25..27
                case 25: return GearItems.GeologistsHammer();
                case 26: return GearItems.MagmaMaul();
                case 27: return GearItems.QuakeHammer();
                // Staves (Uncommon) — 28..30
                case 28: return GearItems.EarthenStaff();
                case 29: return GearItems.EmberRod();
                case 30: return GearItems.ShadowwoodStaff();
                // Armor (Uncommon) — 31..44
                case 31: return GearItems.AbyssPlate();
                case 32: return GearItems.CrystalGuard();
                case 33: return GearItems.DiamondMail();
                case 34: return GearItems.EmberguardMail();
                case 35: return GearItems.GranitePlate();
                case 36: return GearItems.LavaplateArmor();
                case 37: return GearItems.MagmaBlastPlate();
                case 38: return GearItems.PitLordsArmor();
                case 39: return GearItems.ReinforcedPlate();
                case 40: return GearItems.ShadowVest();
                case 41: return GearItems.SteelCuirass();
                case 42: return GearItems.StonePlate();
                case 43: return GearItems.Voidmail();
                case 44: return GearItems.VolcanicArmor();
                // Shields (Uncommon) — 45..59
                case 45: return GearItems.AbyssWall();
                case 46: return GearItems.CrystalBarrier();
                case 47: return GearItems.DiamondBarrier();
                case 48: return GearItems.EmberShield();
                case 49: return GearItems.GraniteGuard();
                case 50: return GearItems.HeaterShield();
                case 51: return GearItems.InfernoGuard();
                case 52: return GearItems.LavaShield();
                case 53: return GearItems.MagmaWall();
                case 54: return GearItems.PitLordsAegis();
                case 55: return GearItems.QuartzWall();
                case 56: return GearItems.ShadowGuard();
                case 57: return GearItems.SteelShield();
                case 58: return GearItems.TowerShield();
                case 59: return GearItems.VoidBarrier();
                // Helms (Uncommon) — 60..74
                case 60: return GearItems.AbyssHelm();
                case 61: return GearItems.CrystalCirclet();
                case 62: return GearItems.DiamondCirclet();
                case 63: return GearItems.EmberHelm();
                case 64: return GearItems.GreatHelm();
                case 65: return GearItems.InfernoCrown();
                case 66: return GearItems.LavaCrown();
                case 67: return GearItems.MagmaHelm();
                case 68: return GearItems.PitLordsCrown();
                case 69: return GearItems.QuartzHelm();
                case 70: return GearItems.ShadowCowl();
                case 71: return GearItems.SteelHelm();
                case 72: return GearItems.StoneCrown();
                case 73: return GearItems.VoidMask();
                case 74: return GearItems.WingedHelm();
                // Accessories (Uncommon) — 75..76
                case 75: return GearItems.MagicChain();
                default: return GearItems.RingOfPower();
            }
        }

        /// <summary>
        /// Determine treasure level based on pit level using probability distribution
        /// </summary>
        public static int DetermineTreasureLevel(int pitLevel)
        {
            var random = Nez.Random.NextFloat();

            if (CaveBiomeConfig.IsCaveLevel(pitLevel))
            {
                return CaveBiomeConfig.DetermineCaveTreasureLevel(pitLevel, random);
            }

            return pitLevel switch
            {
                // Pit Levels 1-10: Only Level 1 (100%)
                <= 10 => 1,

                // Pit Levels 10-30: Level 1 (80%), Level 2 (20%)
                <= 30 => random < 0.8f ? 1 : 2,

                // Pit Levels 30-60: Level 1 (70%), Level 2 (20%), Level 3 (10%)
                <= 60 => random switch
                {
                    < 0.7f => 1,
                    < 0.9f => 2,
                    _ => 3
                },

                // Pit Levels 60-90: Level 1 (55%), Level 2 (25%), Level 3 (15%), Level 4 (5%)
                <= 90 => random switch
                {
                    < 0.55f => 1,
                    < 0.8f => 2,
                    < 0.95f => 3,
                    _ => 4
                },

                // Pit Levels 90+: Level 1 (40%), Level 2 (30%), Level 3 (20%), Level 4 (9%), Level 5 (1%)
                _ => random switch
                {
                    < 0.4f => 1,
                    < 0.7f => 2,
                    < 0.9f => 3,
                    < 0.99f => 4,
                    _ => 5
                }
            };
        }

        /// <summary>
        /// Initialize this treasure chest for a specific pit level.
        /// Only sets the visual level; item generation is deferred to
        /// <see cref="GenerateContainedItem"/> so deficit-tracking weights
        /// reflect the party state at chest-open time rather than spawn time.
        /// </summary>
        public void InitializeForPitLevel(int pitLevel, LootJobContext jobContext = default)
        {
            _pitLevel = pitLevel;
            Level = DetermineTreasureLevel(pitLevel);
            ContainedItem = null; // generated at open time via GenerateContainedItem
        }

        /// <summary>
        /// Generates and assigns the contained item using the supplied party context.
        /// Call this when the chest is opened so that deficit-tracking weights reflect
        /// the current party composition and drop history.
        /// </summary>
        public void GenerateContainedItem(LootJobContext ctx)
        {
            if (CaveBiomeConfig.IsCaveLevel(_pitLevel))
            {
                ContainedItem = GenerateCaveItemForTreasureLevel(Level, ctx);
                return;
            }

            ContainedItem = GenerateItemForTreasureLevel(Level);
        }
    }
}
using Nez;
using Nez.Sprites;
using PitHero.Config;
using PitHero.Farming;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;

namespace PitHero.ECS.Components
{
    /// <summary>Captures the active party's job composition for biased loot drops.</summary>
    public struct LootJobContext
    {
        /// <summary>The hero's job flag (highest priority).</summary>
        public JobType HeroJob;

        /// <summary>Bitwise OR of all hired mercenary job flags.</summary>
        public JobType MercJobs;

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

        /// <summary>
        /// When non-null, this chest yields seeds of the specified crop type instead of a normal item.
        /// Transient — set during <see cref="InitializeForPitLevel"/>; cleared after pickup.
        /// </summary>
        public CropType? ContainedSeedType;

        /// <summary>Number of seeds to award when <see cref="ContainedSeedType"/> is set.</summary>
        public int ContainedSeedCount;

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

            // Create base renderer (drawn first = behind wood)
            _baseRenderer = Entity.AddComponent(new SpriteRenderer());

            // Create wood renderer (drawn second = in front of base)
            _woodRenderer = Entity.AddComponent(new SpriteRenderer());

            UpdateSprites();
            UpdateWoodColor();

            // Composite both layers into a single 32×32 render target so the chest
            // always occupies exactly one render layer with no z-order inconsistency.
            // Treasure sprites are 32×32 with center origin — pivot maps entity pos to RT center.
            var compositor = Entity.AddComponent(new StaticSpriteCompositor(
                new Nez.Sprites.SpriteRenderer[] { _baseRenderer, _woodRenderer },
                GameConfig.TileSize,
                GameConfig.TileSize,
                new Microsoft.Xna.Framework.Vector2(GameConfig.TileSize / 2f, GameConfig.TileSize / 2f)));
            compositor.SetRenderLayer(GameConfig.RenderLayerSingleTileObject);
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
        /// Normal (level 1) chests can drop consumables or gear. All other chest colors drop gear only.
        /// </summary>
        public static IItem GenerateCaveItemForTreasureLevel(int treasureLevel, LootJobContext ctx = default)
        {
            if (treasureLevel == 1)
            {
                bool isConsumable = Nez.Random.NextFloat() < BalanceConfig.CaveConsumableDropRate;
                return isConsumable ? GenerateNormalPotion() : GenerateCaveCommonEquipment(ctx);
            }

            return treasureLevel switch
            {
                2 => GenerateCaveUncommonEquipment(ctx),
                3 => GenerateCaveRareEquipment(ctx),
                4 => GenerateCaveEpicEquipment(ctx),
                _ => GenerateCaveEpicEquipment(ctx),
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
        /// Generate cave common equipment from all Normal-rarity cave gear items (78 items).
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
            // Swords promoted to Normal (56-60)
            ItemKind.WeaponSword,    // 56: CrystalEdge
            ItemKind.WeaponSword,    // 57: UndergroundRapier
            ItemKind.WeaponSword,    // 58: EmberSword
            ItemKind.WeaponSword,    // 59: VoidCutter
            ItemKind.WeaponSword,    // 60: StalagmiteSword
            // Armor promoted to Normal (61-65)
            ItemKind.ArmorMail,      // 61: StonePlate
            ItemKind.ArmorMail,      // 62: EmberguardMail
            ItemKind.ArmorGi,        // 63: ShadowVest
            ItemKind.ArmorMail,      // 64: ReinforcedPlate
            ItemKind.ArmorMail,      // 65: CrystalGuard
            // Shields promoted to Normal (66-71)
            ItemKind.Shield,         // 66: SteelShield
            ItemKind.Shield,         // 67: GraniteGuard
            ItemKind.Shield,         // 68: EmberShield
            ItemKind.Shield,         // 69: ShadowGuard
            ItemKind.Shield,         // 70: TowerShield
            ItemKind.Shield,         // 71: CrystalBarrier
            // Helms promoted to Normal (72-77)
            ItemKind.HatHelm,        // 72: SteelHelm
            ItemKind.HatHelm,        // 73: StoneCrown
            ItemKind.HatHelm,        // 74: EmberHelm
            ItemKind.HatHeadband,    // 75: ShadowCowl
            ItemKind.HatHelm,        // 76: GreatHelm
            ItemKind.HatHeadband,    // 77: CrystalCirclet
        };

        /// <summary>
        /// Generate cave uncommon equipment from all Uncommon-rarity cave gear items (39 items).
        /// </summary>
        private static readonly ItemKind[] _uncommonPoolKinds = new ItemKind[]
        {
            // Swords (Uncommon) — 0..4
            ItemKind.WeaponSword,    // 0:  GloomBlade
            ItemKind.WeaponSword,    // 1:  LavaForgedSword
            ItemKind.WeaponSword,    // 2:  DepthsReaver
            ItemKind.WeaponSword,    // 3:  QuartzSaber
            ItemKind.WeaponSword,    // 4:  InfernoEdge
            // Axes (Uncommon) — 5..8
            ItemKind.WeaponSword,    // 5:  CrystalCleaver
            ItemKind.WeaponSword,    // 6:  ObsidianCleaver
            ItemKind.WeaponSword,    // 7:  ShadowSplitter
            ItemKind.WeaponSword,    // 8:  VolcanicAxe
            // Daggers (Uncommon) — 9..11
            ItemKind.WeaponKnife,    // 9:  AssassinsEdge
            ItemKind.WeaponKnife,    // 10: SerpentsTooth
            ItemKind.WeaponKnife,    // 11: ShadowStiletto
            // Spears (Uncommon) — 12..15
            ItemKind.WeaponSword,    // 12: CavePike
            ItemKind.WeaponSword,    // 13: FlameLance
            ItemKind.WeaponSword,    // 14: InfernalPike
            ItemKind.WeaponSword,    // 15: StalactiteSpear
            // Hammers (Uncommon) — 16..18
            ItemKind.WeaponHammer,   // 16: GeologistsHammer
            ItemKind.WeaponHammer,   // 17: MagmaMaul
            ItemKind.WeaponHammer,   // 18: QuakeHammer
            // Staves/Rods (Uncommon) — 19..21
            ItemKind.WeaponStaff,    // 19: EarthenStaff
            ItemKind.WeaponRod,      // 20: EmberRod
            ItemKind.WeaponStaff,    // 21: ShadowwoodStaff
            // Armor (Uncommon) — 22..26
            ItemKind.ArmorMail,      // 22: LavaplateArmor
            ItemKind.ArmorMail,      // 23: Voidmail
            ItemKind.ArmorMail,      // 24: SteelCuirass
            ItemKind.ArmorMail,      // 25: GranitePlate
            ItemKind.ArmorMail,      // 26: VolcanicArmor
            // Shields (Uncommon) — 27..31
            ItemKind.Shield,         // 27: LavaShield
            ItemKind.Shield,         // 28: VoidBarrier
            ItemKind.Shield,         // 29: HeaterShield
            ItemKind.Shield,         // 30: QuartzWall
            ItemKind.Shield,         // 31: InfernoGuard
            // Helms (Uncommon) — 32..36
            ItemKind.HatHelm,        // 32: LavaCrown
            ItemKind.HatHelm,        // 33: VoidMask
            ItemKind.HatHelm,        // 34: WingedHelm
            ItemKind.HatHelm,        // 35: QuartzHelm
            ItemKind.HatHelm,        // 36: InfernoCrown
            // Accessories (Uncommon) — 37..38
            ItemKind.Accessory,      // 37: MagicChain
            ItemKind.Accessory,      // 38: RingOfPower
        };

        /// <summary>Generate cave rare equipment from all Rare-rarity cave gear items (12 items).</summary>
        private static readonly ItemKind[] _rarePoolKinds = new ItemKind[]
        {
            ItemKind.WeaponSword,    // 0:  AbyssFang
            ItemKind.WeaponSword,    // 1:  DiamondEdge
            ItemKind.WeaponSword,    // 2:  MagmaBlade
            ItemKind.ArmorMail,      // 3:  AbyssPlate
            ItemKind.ArmorMail,      // 4:  DiamondMail
            ItemKind.ArmorMail,      // 5:  MagmaBlastPlate
            ItemKind.Shield,         // 6:  AbyssWall
            ItemKind.Shield,         // 7:  DiamondBarrier
            ItemKind.Shield,         // 8:  MagmaWall
            ItemKind.HatHelm,        // 9:  AbyssHelm
            ItemKind.HatHeadband,    // 10: DiamondCirclet
            ItemKind.HatHelm,        // 11: MagmaHelm
        };

        /// <summary>Generate cave epic equipment from all Epic-rarity cave gear items (4 items).</summary>
        private static readonly ItemKind[] _epicPoolKinds = new ItemKind[]
        {
            ItemKind.WeaponSword,    // 0: PitLordsSword
            ItemKind.ArmorMail,      // 1: PitLordsArmor
            ItemKind.Shield,         // 2: PitLordsAegis
            ItemKind.HatHelm,        // 3: PitLordsCrown
        };

        /// <summary>
        /// Selects a weighted pool index biased toward gear usable by the active party.
        /// Falls back to flat random when no party context is available.
        /// </summary>
        private static int SelectWeightedPoolIndex(ItemKind[] poolKinds, LootJobContext ctx)
        {
            if (ctx.IsEmpty)
                return Nez.Random.NextInt(poolKinds.Length);

            int totalWeight = 0;
            var weights = new int[poolKinds.Length];

            for (int i = 0; i < poolKinds.Length; i++)
            {
                JobType allowed = Gear.GetDefaultAllowedJobs(poolKinds[i]);
                int weight;

                if (allowed == JobType.All)
                    weight = BalanceConfig.LootWeightAllJobs;
                else if ((allowed & ctx.HeroJob) != 0)
                    weight = BalanceConfig.LootWeightHeroJob;
                else if ((allowed & ctx.MercJobs) != 0)
                    weight = BalanceConfig.LootWeightMercJob;
                else
                    weight = BalanceConfig.LootWeightNoPartyJob;

                weights[i] = weight;
                totalWeight += weight;
            }

            int roll = Nez.Random.NextInt(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return i;
            }

            return poolKinds.Length - 1;
        }

        /// <summary>
        /// Generate cave common equipment from all Normal-rarity cave gear items (78 items).
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
                case 55: return GearItems.ProtectRing();
                // Swords promoted to Normal — 56..60
                case 56: return GearItems.CrystalEdge();
                case 57: return GearItems.UndergroundRapier();
                case 58: return GearItems.EmberSword();
                case 59: return GearItems.VoidCutter();
                case 60: return GearItems.StalagmiteSword();
                // Armor promoted to Normal — 61..65
                case 61: return GearItems.StonePlate();
                case 62: return GearItems.EmberguardMail();
                case 63: return GearItems.ShadowVest();
                case 64: return GearItems.ReinforcedPlate();
                case 65: return GearItems.CrystalGuard();
                // Shields promoted to Normal — 66..71
                case 66: return GearItems.SteelShield();
                case 67: return GearItems.GraniteGuard();
                case 68: return GearItems.EmberShield();
                case 69: return GearItems.ShadowGuard();
                case 70: return GearItems.TowerShield();
                case 71: return GearItems.CrystalBarrier();
                // Helms promoted to Normal — 72..77
                case 72: return GearItems.SteelHelm();
                case 73: return GearItems.StoneCrown();
                case 74: return GearItems.EmberHelm();
                case 75: return GearItems.ShadowCowl();
                case 76: return GearItems.GreatHelm();
                default: return GearItems.CrystalCirclet();
            }
        }

        /// <summary>
        /// Generate cave uncommon equipment from all Uncommon-rarity cave gear items (39 items).
        /// </summary>
        private static IItem GenerateCaveUncommonEquipment(LootJobContext ctx = default)
        {
            int index = SelectWeightedPoolIndex(_uncommonPoolKinds, ctx);
            switch (index)
            {
                // Swords (Uncommon) — 0..4
                case 0:  return GearItems.GloomBlade();
                case 1:  return GearItems.LavaForgedSword();
                case 2:  return GearItems.DepthsReaver();
                case 3:  return GearItems.QuartzSaber();
                case 4:  return GearItems.InfernoEdge();
                // Axes (Uncommon) — 5..8
                case 5:  return GearItems.CrystalCleaver();
                case 6:  return GearItems.ObsidianCleaver();
                case 7:  return GearItems.ShadowSplitter();
                case 8:  return GearItems.VolcanicAxe();
                // Daggers (Uncommon) — 9..11
                case 9:  return GearItems.AssassinsEdge();
                case 10: return GearItems.SerpentsTooth();
                case 11: return GearItems.ShadowStiletto();
                // Spears (Uncommon) — 12..15
                case 12: return GearItems.CavePike();
                case 13: return GearItems.FlameLance();
                case 14: return GearItems.InfernalPike();
                case 15: return GearItems.StalactiteSpear();
                // Hammers (Uncommon) — 16..18
                case 16: return GearItems.GeologistsHammer();
                case 17: return GearItems.MagmaMaul();
                case 18: return GearItems.QuakeHammer();
                // Staves/Rods (Uncommon) — 19..21
                case 19: return GearItems.EarthenStaff();
                case 20: return GearItems.EmberRod();
                case 21: return GearItems.ShadowwoodStaff();
                // Armor (Uncommon) — 22..26
                case 22: return GearItems.LavaplateArmor();
                case 23: return GearItems.Voidmail();
                case 24: return GearItems.SteelCuirass();
                case 25: return GearItems.GranitePlate();
                case 26: return GearItems.VolcanicArmor();
                // Shields (Uncommon) — 27..31
                case 27: return GearItems.LavaShield();
                case 28: return GearItems.VoidBarrier();
                case 29: return GearItems.HeaterShield();
                case 30: return GearItems.QuartzWall();
                case 31: return GearItems.InfernoGuard();
                // Helms (Uncommon) — 32..36
                case 32: return GearItems.LavaCrown();
                case 33: return GearItems.VoidMask();
                case 34: return GearItems.WingedHelm();
                case 35: return GearItems.QuartzHelm();
                case 36: return GearItems.InfernoCrown();
                // Accessories (Uncommon) — 37..38
                case 37: return GearItems.MagicChain();
                default: return GearItems.RingOfPower();
            }
        }

        /// <summary>Generate cave rare equipment from all Rare-rarity cave gear items (12 items).</summary>
        private static IItem GenerateCaveRareEquipment(LootJobContext ctx = default)
        {
            int index = SelectWeightedPoolIndex(_rarePoolKinds, ctx);
            return index switch
            {
                0  => GearItems.AbyssFang(),
                1  => GearItems.DiamondEdge(),
                2  => GearItems.MagmaBlade(),
                3  => GearItems.AbyssPlate(),
                4  => GearItems.DiamondMail(),
                5  => GearItems.MagmaBlastPlate(),
                6  => GearItems.AbyssWall(),
                7  => GearItems.DiamondBarrier(),
                8  => GearItems.MagmaWall(),
                9  => GearItems.AbyssHelm(),
                10 => GearItems.DiamondCirclet(),
                _  => GearItems.MagmaHelm(),
            };
        }

        /// <summary>Generate cave epic equipment from all Epic-rarity cave gear items (4 items).</summary>
        private static IItem GenerateCaveEpicEquipment(LootJobContext ctx = default)
        {
            int index = SelectWeightedPoolIndex(_epicPoolKinds, ctx);
            return index switch
            {
                0 => GearItems.PitLordsSword(),
                1 => GearItems.PitLordsArmor(),
                2 => GearItems.PitLordsAegis(),
                _ => GearItems.PitLordsCrown(),
            };
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
        /// Initialize this treasure chest for a specific pit level
        /// </summary>
        public void InitializeForPitLevel(int pitLevel, LootJobContext jobContext = default)
        {
            Level = DetermineTreasureLevel(pitLevel);

            // Seed drop: any uncommon (level-2) chest has a chance to yield seeds instead of normal loot.
            if (Level == 2 && Nez.Random.NextFloat() < BalanceConfig.SeedChestDropRate)
            {
                ContainedSeedType  = (CropType)Nez.Random.NextInt(CropTypeInfo.Count);
                ContainedSeedCount = Nez.Random.NextInt(3) + 1; // 1..3
                ContainedItem = null;
                return;
            }

            if (CaveBiomeConfig.IsCaveLevel(pitLevel))
            {
                ContainedItem = GenerateCaveItemForTreasureLevel(Level, jobContext);
                return;
            }

            ContainedItem = GenerateItemForTreasureLevel(Level);
        }
    }
}
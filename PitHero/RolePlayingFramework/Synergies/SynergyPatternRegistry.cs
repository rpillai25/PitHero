using System.Collections.Generic;

namespace RolePlayingFramework.Synergies
{
    /// <summary>
    /// Single shared registry of all synergy patterns in the game.
    /// Initialized once via static constructor (AOT-safe, no reflection).
    /// The canonical order matches the stencil library panel slot order.
    /// </summary>
    public static class SynergyPatternRegistry
    {
        private static readonly List<SynergyPattern> _all;
        private static readonly Dictionary<string, SynergyPattern> _byId;

        /// <summary>All synergy patterns in canonical display order (stencil library panel slot order).</summary>
        public static IReadOnlyList<SynergyPattern> All => _all;

        static SynergyPatternRegistry()
        {
            // 63 patterns total (HeroUI order is authoritative for slot display)
            _all = new List<SynergyPattern>(64);
            _byId = new Dictionary<string, SynergyPattern>(64);

            // Knight patterns
            Add(KnightSynergyPatterns.CreateHolyStrike());
            Add(KnightSynergyPatterns.CreateIaidoSlash());
            Add(KnightSynergyPatterns.CreateShadowSlash());
            Add(KnightSynergyPatterns.CreateSpellblade());
            Add(KnightSynergyPatterns.CreateArmorMastery());
            Add(KnightSynergyPatterns.CreateSwordProficiency());
            Add(KnightSynergyPatterns.CreateGuardiansResolve());
            Add(KnightSynergyPatterns.CreateBerserkerRage());
            Add(KnightSynergyPatterns.CreateShieldMastery());
            Add(KnightSynergyPatterns.CreateHeavyFortification());

            // Mage patterns
            Add(MageSynergyPatterns.CreateMeteor());
            Add(MageSynergyPatterns.CreateShadowBolt());
            Add(MageSynergyPatterns.CreateElementalVolley());
            Add(MageSynergyPatterns.CreateBlitz());
            Add(MageSynergyPatterns.CreateArcaneFocus());
            Add(MageSynergyPatterns.CreateElementalMastery());
            Add(MageSynergyPatterns.CreateSpellWeaving());
            Add(MageSynergyPatterns.CreateManaConvergence());
            Add(MageSynergyPatterns.CreateRodFocus());

            // Priest patterns
            Add(PriestSynergyPatterns.CreateAuraHeal());
            Add(PriestSynergyPatterns.CreatePurify());
            Add(PriestSynergyPatterns.CreateSacredStrike());
            Add(PriestSynergyPatterns.CreateLifeLeech());
            Add(PriestSynergyPatterns.CreateDivineProtection());
            Add(PriestSynergyPatterns.CreateHealingAmplification());
            Add(PriestSynergyPatterns.CreateHolyAura());
            Add(PriestSynergyPatterns.CreateSanctifiedMind());
            Add(PriestSynergyPatterns.CreateDivineVestments());

            // Monk patterns
            Add(MonkSynergyPatterns.CreateDragonClaw());
            Add(MonkSynergyPatterns.CreateEnergyBurst());
            Add(MonkSynergyPatterns.CreateDragonKick());
            Add(MonkSynergyPatterns.CreateSneakPunch());
            Add(MonkSynergyPatterns.CreateIronFist());
            Add(MonkSynergyPatterns.CreateMartialFocus());
            Add(MonkSynergyPatterns.CreateKiMastery());
            Add(MonkSynergyPatterns.CreateEvasionTraining());
            Add(MonkSynergyPatterns.CreateBalanceTraining());

            // Thief patterns
            Add(ThiefSynergyPatterns.CreateSmokeBomb());
            Add(ThiefSynergyPatterns.CreatePoisonArrow());
            Add(ThiefSynergyPatterns.CreateFade());
            Add(ThiefSynergyPatterns.CreateKiCloak());
            Add(ThiefSynergyPatterns.CreateShadowStep());
            Add(ThiefSynergyPatterns.CreateLockpicking());
            Add(ThiefSynergyPatterns.CreateTrapMastery());
            Add(ThiefSynergyPatterns.CreateAssassinsEdge());

            // Archer patterns
            // NOTE: SharpAim was present in HeroUI but missing from RegisterAllArcherPatterns
            // (where it appeared as a commented-out CreateEagleEye() — the old method name).
            // The registry is the union of both lists; SharpAim is included here.
            Add(ArcherSynergyPatterns.CreatePiercingArrow());
            Add(ArcherSynergyPatterns.CreateLightshot());
            Add(ArcherSynergyPatterns.CreateKiArrow());
            Add(ArcherSynergyPatterns.CreateArrowFlurry());
            Add(ArcherSynergyPatterns.CreateMarksman());
            Add(ArcherSynergyPatterns.CreateSharpAim());
            Add(ArcherSynergyPatterns.CreateRangersPath());
            Add(ArcherSynergyPatterns.CreateWindArcher());

            // Cross-class patterns
            Add(CrossClassSynergyPatterns.CreateSacredBlade());
            Add(CrossClassSynergyPatterns.CreateFlashStrike());
            Add(CrossClassSynergyPatterns.CreateSoulWard());
            Add(CrossClassSynergyPatterns.CreateDragonBolt());
            Add(CrossClassSynergyPatterns.CreateElementalStorm());
            Add(CrossClassSynergyPatterns.CreateBattleMage());
            Add(CrossClassSynergyPatterns.CreateHolyWarrior());
            Add(CrossClassSynergyPatterns.CreateShadowMaster());
            Add(CrossClassSynergyPatterns.CreateArcaneProtector());
            Add(CrossClassSynergyPatterns.CreateElementalChampion());
        }

        /// <summary>Adds a pattern to the list and lookup dictionary; silently skips duplicate IDs.</summary>
        private static void Add(SynergyPattern pattern)
        {
            if (pattern == null) return;
            if (_byId.ContainsKey(pattern.Id)) return;
            _all.Add(pattern);
            _byId[pattern.Id] = pattern;
        }

        /// <summary>Returns the pattern with the given ID, or null if not found.</summary>
        public static SynergyPattern GetById(string id)
        {
            if (id == null) return null;
            _byId.TryGetValue(id, out var pattern);
            return pattern;
        }
    }
}

using System;
using System.Diagnostics;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;
#if DEBUG
using System.IO;
using System.Text;
using Nez;
using PitHero.ECS.Components;
#endif

namespace PitHero.Services.Analytics
{
    /// <summary>
    /// Static facade for game-balance analytics logging (issue #289). Every log method is
    /// [Conditional("DEBUG")] so call sites (including argument evaluation) are removed entirely
    /// from Release builds. Events are buffered as JSONL and flushed periodically by AnalyticsManager.
    /// </summary>
    public static class AnalyticsService
    {
#if DEBUG
        private static bool _enabled;
        private static bool _sessionStarted;
        private static string _directory;
        private static StreamWriter _writer;
        private static readonly StringBuilder _buffer = new StringBuilder(GameConfig.AnalyticsFlushThresholdChars);
        private static readonly JsonLineBuilder _json = new JsonLineBuilder(_buffer);
        private static float _flushTimer;
        private static long _sessionGoldGained;
        private static int _monstersDefeated;
        private static DateTime _sessionStartUtc;
#endif

        /// <summary>Runtime toggle for analytics logging. Always false (and set is ignored) in Release builds.</summary>
        public static bool Enabled
        {
#if DEBUG
            get => _enabled;
            set => _enabled = value;
#else
            get => false;
            set { }
#endif
        }

        /// <summary>Initializes analytics using the default output directory (%LOCALAPPDATA%\PitHero\analytics).</summary>
        [Conditional("DEBUG")]
        public static void Initialize()
        {
#if DEBUG
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitHero");
            Initialize(Path.Combine(baseDir, GameConfig.AnalyticsDirectoryName));
#endif
        }

        /// <summary>Initializes analytics with an explicit output directory. Logging is enabled only if GameConfig.AnalyticsEnabled is true.</summary>
        [Conditional("DEBUG")]
        public static void Initialize(string directory)
        {
#if DEBUG
            _directory = directory;
            _enabled = GameConfig.AnalyticsEnabled;
            _sessionStartUtc = DateTime.UtcNow;
#endif
        }

        /// <summary>Flushes remaining events and closes the output file.</summary>
        [Conditional("DEBUG")]
        public static void Shutdown()
        {
#if DEBUG
            Flush();
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
            _enabled = false;
            _sessionStarted = false;
#endif
        }

        /// <summary>Accumulates elapsed time and flushes the buffer once the configured interval passes. Called every frame by AnalyticsManager.</summary>
        [Conditional("DEBUG")]
        public static void TickFlush(float deltaSeconds)
        {
#if DEBUG
            if (!_enabled)
                return;
            _flushTimer += deltaSeconds;
            if (_flushTimer >= GameConfig.AnalyticsFlushIntervalSeconds)
            {
                _flushTimer = 0f;
                Flush();
            }
#endif
        }

        /// <summary>Writes all buffered events to disk immediately.</summary>
        [Conditional("DEBUG")]
        public static void Flush()
        {
#if DEBUG
            if (_writer == null || _buffer.Length == 0)
                return;
            try
            {
                _writer.Write(_buffer);
                _writer.Flush();
            }
            catch (IOException e)
            {
                Nez.Debug.Warn($"[Analytics] Flush failed, disabling analytics: {e.Message}");
                _enabled = false;
            }
            _buffer.Clear();
#endif
        }

        /// <summary>Logs the start of a play session (new game or save load). Rotates to a new output file if a session was already active.</summary>
        [Conditional("DEBUG")]
        public static void LogSessionStart(string mode, int gold)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (_sessionStarted && _writer != null)
            {
                Flush();
                _writer.Dispose();
                _writer = null;
            }
            _sessionStarted = true;
            _sessionStartUtc = DateTime.UtcNow;
            _sessionGoldGained = 0;
            _monstersDefeated = 0;
            if (!BeginEvent("session_start"))
                return;
            _json.Field("mode", mode);
            _json.Field("gold", gold);
            EndEvent();
#endif
        }

        /// <summary>Logs end-of-session totals.</summary>
        [Conditional("DEBUG")]
        public static void LogSessionEnd()
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("session_end"))
                return;
            _json.Field("goldGainedTotal", _sessionGoldGained);
            _json.Field("monstersDefeated", _monstersDefeated);
            _json.Field("durationSec", (int)(DateTime.UtcNow - _sessionStartUtc).TotalSeconds);
            EndEvent();
#endif
        }

        /// <summary>Logs generation of a new pit level.</summary>
        [Conditional("DEBUG")]
        public static void LogPitGenerated(int pitLevel, bool isBossFloor, int monsterCount, int chestCount)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("pit_generated"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("pitTier", GetCurrentPitTier());
            _json.Field("isBossFloor", isBossFloor);
            _json.Field("monsterCount", monsterCount);
            _json.Field("chestCount", chestCount);
            EndEvent();
#endif
        }

        /// <summary>Logs a spawned treasure chest and its contents.</summary>
        [Conditional("DEBUG")]
        public static void LogChestSpawned(int pitLevel, int x, int y, int chestLevel, IItem item, string seedType, int seedCount)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("chest_spawned"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("pitTier", GetCurrentPitTier());
            _json.Field("x", x);
            _json.Field("y", y);
            _json.Field("chestLevel", chestLevel);
            _json.Field("item", item?.Name);
            _json.Field("kind", item != null ? item.Kind.ToString() : null);
            _json.Field("rarity", item != null ? item.Rarity.ToString() : null);
            if (seedType != null)
            {
                _json.Field("seedType", seedType);
                _json.Field("seedCount", seedCount);
            }
            EndEvent();
#endif
        }

        /// <summary>Logs a spawned monster.</summary>
        [Conditional("DEBUG")]
        public static void LogMonsterSpawned(int pitLevel, int x, int y, IEnemy enemy)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("monster_spawned"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("pitTier", GetCurrentPitTier());
            _json.Field("x", x);
            _json.Field("y", y);
            _json.Field("name", enemy.Name);
            _json.Field("enemyId", enemy.EnemyId.ToString());
            _json.Field("level", enemy.Level);
            _json.Field("maxHP", enemy.MaxHP);
            _json.Field("isBoss", enemy.IsBoss);
            EndEvent();
#endif
        }

        /// <summary>Logs the hero activating the wizard orb to advance to the next pit level.</summary>
        [Conditional("DEBUG")]
        public static void LogOrbActivated(int fromPitLevel, int toPitLevel, int heroLevel)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("orb_activated"))
                return;
            _json.Field("fromPitLevel", fromPitLevel);
            _json.Field("toPitLevel", toPitLevel);
            _json.Field("pitTier", GetCurrentPitTier());
            _json.Field("heroLevel", heroLevel);
            EndEvent();
#endif
        }

        /// <summary>Logs the hero jumping into the pit.</summary>
        [Conditional("DEBUG")]
        public static void LogPitJump(int pitLevel, Hero hero)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("pit_jump"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("heroLevel", hero.Level);
            _json.Field("heroHP", hero.CurrentHP);
            _json.Field("heroMaxHP", hero.MaxHP);
            EndEvent();
#endif
        }

        /// <summary>Logs a full snapshot of the party (hero plus hired mercenaries) with stats, skills and gear.</summary>
        [Conditional("DEBUG")]
        public static void LogPartySnapshot(string reason, Hero hero)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("party_snapshot"))
                return;
            _json.Field("reason", reason);
            _json.BeginArray("members");
            if (hero != null)
            {
                _json.BeginArrayObject();
                WriteHeroFields(hero);
                _json.EndObject();
            }
            if (Core.Instance != null)
            {
                var mercManager = Core.Services.GetService<MercenaryManager>();
                if (mercManager != null)
                {
                    var hired = mercManager.GetHiredMercenaries();
                    for (int i = 0; i < hired.Count; i++)
                    {
                        var merc = hired[i].GetComponent<MercenaryComponent>()?.LinkedMercenary;
                        if (merc == null)
                            continue;
                        _json.BeginArrayObject();
                        WriteMercenaryFields(merc);
                        _json.EndObject();
                    }
                }
            }
            _json.EndArray();
            EndEvent();
#endif
        }

        /// <summary>Logs the party sleeping at the inn.</summary>
        [Conditional("DEBUG")]
        public static void LogInnSleep(int cost, int goldAfter)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("inn_sleep"))
                return;
            _json.Field("cost", cost);
            _json.Field("goldAfter", goldAfter);
            EndEvent();
#endif
        }

        /// <summary>Logs a new mercenary arriving at the tavern, with stats and hire cost.</summary>
        [Conditional("DEBUG")]
        public static void LogMercArrived(Mercenary merc, int hireCost)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("merc_arrived"))
                return;
            var stats = merc.GetTotalStats();
            _json.Field("name", merc.Name);
            _json.Field("job", merc.Job.NameKey);
            _json.Field("level", merc.Level);
            _json.Field("str", stats.Strength);
            _json.Field("agi", stats.Agility);
            _json.Field("vit", stats.Vitality);
            _json.Field("mag", stats.Magic);
            _json.Field("maxHP", merc.MaxHP);
            _json.Field("maxMP", merc.MaxMP);
            _json.Field("hireCost", hireCost);
            EndEvent();
#endif
        }

        /// <summary>Logs a mercenary being removed (left tavern rotation, or dismissed from the party).</summary>
        [Conditional("DEBUG")]
        public static void LogMercLeft(string name, string reason)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("merc_left"))
                return;
            _json.Field("name", name);
            _json.Field("reason", reason);
            EndEvent();
#endif
        }

        /// <summary>Logs an item actually collected from a chest.</summary>
        [Conditional("DEBUG")]
        public static void LogItemAcquired(IItem item, int pitLevel, int chestLevel)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("item_acquired"))
                return;
            _json.Field("item", item.Name);
            _json.Field("kind", item.Kind.ToString());
            _json.Field("rarity", item.Rarity.ToString());
            _json.Field("pitLevel", pitLevel);
            _json.Field("chestLevel", chestLevel);
            EndEvent();
#endif
        }

        /// <summary>Logs gear equipped on the hero along with the hero's resulting stats.</summary>
        [Conditional("DEBUG")]
        public static void LogGearEquipped(Hero hero, EquipmentSlot slot, IItem item)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("gear_equipped"))
                return;
            _json.Field("character", hero.Name);
            _json.Field("charType", "hero");
            _json.Field("slot", slot.ToString());
            _json.Field("item", item.Name);
            _json.Field("rarity", item.Rarity.ToString());
            WriteResultingStats(hero.GetTotalStats(), hero.MaxHP, hero.MaxMP);
            EndEvent();
#endif
        }

        /// <summary>Logs gear equipped on a mercenary along with the mercenary's resulting stats.</summary>
        [Conditional("DEBUG")]
        public static void LogGearEquipped(Mercenary merc, EquipmentSlot slot, IItem item)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("gear_equipped"))
                return;
            _json.Field("character", merc.Name);
            _json.Field("charType", "merc");
            _json.Field("slot", slot.ToString());
            _json.Field("item", item.Name);
            _json.Field("rarity", item.Rarity.ToString());
            WriteResultingStats(merc.GetTotalStats(), merc.MaxHP, merc.MaxMP);
            EndEvent();
#endif
        }

        /// <summary>Logs a gold gain, its source, the session running total and the player's current gold.</summary>
        [Conditional("DEBUG")]
        public static void LogGoldGained(int amount, string source, int currentGold)
        {
#if DEBUG
            if (!_enabled)
                return;
            _sessionGoldGained += amount;
            if (!BeginEvent("gold_gained"))
                return;
            _json.Field("amount", amount);
            _json.Field("source", source);
            _json.Field("sessionTotal", _sessionGoldGained);
            _json.Field("currentGold", currentGold);
            EndEvent();
#endif
        }

        /// <summary>Logs a trap triggered by the hero (out-of-battle chip damage).</summary>
        [Conditional("DEBUG")]
        public static void LogTrapTriggered(int pitLevel, int x, int y, int damage)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("trap_triggered"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("x", x);
            _json.Field("y", y);
            _json.Field("damage", damage);
            EndEvent();
#endif
        }

        /// <summary>Logs a trap auto-disarmed by the TrapSense passive before the hero stepped on it.</summary>
        [Conditional("DEBUG")]
        public static void LogTrapDisarmed(int pitLevel, int x, int y)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("trap_disarmed"))
                return;
            _json.Field("pitLevel", pitLevel);
            _json.Field("x", x);
            _json.Field("y", y);
            EndEvent();
#endif
        }

        /// <summary>Logs a monster defeated by the party.</summary>
        [Conditional("DEBUG")]
        public static void LogMonsterDefeated(IEnemy enemy)
        {
#if DEBUG
            if (!_enabled)
                return;
            _monstersDefeated++;
            if (!BeginEvent("monster_defeated"))
                return;
            _json.Field("name", enemy.Name);
            _json.Field("enemyId", enemy.EnemyId.ToString());
            _json.Field("level", enemy.Level);
            _json.Field("isBoss", enemy.IsBoss);
            _json.Field("pitLevel", GetCurrentPitLevel());
            _json.Field("pitTier", GetCurrentPitTier());
            _json.Field("xp", enemy.ExperienceYield);
            _json.Field("jp", enemy.JPYield);
            _json.Field("gold", enemy.GoldYield);
            EndEvent();
#endif
        }

        /// <summary>Logs a single attack or offensive skill use with target and damage.</summary>
        [Conditional("DEBUG")]
        public static void LogAttack(string actor, string actorType, string action, string target, string targetType, int damage, int hpBefore, int hpAfter, bool killed)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("attack"))
                return;
            _json.Field("actor", actor);
            _json.Field("actorType", actorType);
            _json.Field("action", action);
            _json.Field("target", target);
            _json.Field("targetType", targetType);
            _json.Field("dmg", damage);
            _json.Field("hpBefore", hpBefore);
            _json.Field("hpAfter", hpAfter);
            _json.Field("killed", killed);
            EndEvent();
#endif
        }

        /// <summary>Logs HP restored by a skill or item.</summary>
        [Conditional("DEBUG")]
        public static void LogHeal(string actor, string source, string target, int amount, int hpAfter)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("heal"))
                return;
            _json.Field("actor", actor);
            _json.Field("source", source);
            _json.Field("target", target);
            _json.Field("amount", amount);
            _json.Field("hpAfter", hpAfter);
            EndEvent();
#endif
        }

        /// <summary>Logs the hero being killed by a monster, with full hero details and killer stats.</summary>
        [Conditional("DEBUG")]
        public static void LogCharacterKilled(Hero victim, IEnemy killer)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("char_killed"))
                return;
            WriteHeroFields(victim);
            WriteKillerObject(killer);
            EndEvent();
#endif
        }

        /// <summary>Logs a mercenary being killed by a monster, with full mercenary details and killer stats.</summary>
        [Conditional("DEBUG")]
        public static void LogCharacterKilled(Mercenary victim, IEnemy killer)
        {
#if DEBUG
            if (!_enabled)
                return;
            if (!BeginEvent("char_killed"))
                return;
            WriteMercenaryFields(victim);
            WriteKillerObject(killer);
            EndEvent();
#endif
        }

#if DEBUG
        /// <summary>Opens a new event object with timestamp, in-game time and event type. Returns false if output is unavailable.</summary>
        private static bool BeginEvent(string eventType)
        {
            if (!EnsureWriter())
                return false;
            _json.BeginEvent();
            var now = DateTime.Now;
            _json.TimestampField("t", now, TimeZoneInfo.Local.GetUtcOffset(now));
            if (Core.Instance != null)
            {
                var timeService = Core.Services.GetService<InGameTimeService>();
                if (timeService != null)
                    _json.TimeField("gt", timeService.Hour, timeService.Minute);
            }
            _json.Field("e", eventType);
            return true;
        }

        /// <summary>Closes the current event line and flushes early if the buffer is large.</summary>
        private static void EndEvent()
        {
            _json.EndEvent();
            if (_buffer.Length >= GameConfig.AnalyticsFlushThresholdChars)
                Flush();
        }

        /// <summary>Opens the session output file if not already open. Returns false when the file cannot be opened.</summary>
        private static bool EnsureWriter()
        {
            if (_writer != null)
                return true;
            if (_directory == null)
                return false;
            try
            {
                Directory.CreateDirectory(_directory);
                var fileName = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl";
                _writer = new StreamWriter(Path.Combine(_directory, fileName), append: true);
                return true;
            }
            catch (IOException e)
            {
                Nez.Debug.Warn($"[Analytics] Could not open output file, disabling analytics: {e.Message}");
                _enabled = false;
                return false;
            }
        }

        /// <summary>Writes the current pit level fetched from the PitWidthManager service (0 when unavailable).</summary>
        private static int GetCurrentPitLevel()
        {
            if (Core.Instance == null)
                return 0;
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            return pitWidthManager?.CurrentPitLevel ?? 0;
        }

        /// <summary>Writes the current pit tier fetched from the PitWidthManager service (1 when unavailable).</summary>
        private static int GetCurrentPitTier()
        {
            if (Core.Instance == null)
                return 1;
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            return pitWidthManager?.CurrentPitTier ?? 1;
        }

        /// <summary>Writes name/job/level/stats/skills/gear fields for a hero into the current object.</summary>
        private static void WriteHeroFields(Hero hero)
        {
            var stats = hero.GetTotalStats();
            _json.Field("name", hero.Name);
            _json.Field("type", "hero");
            _json.Field("job", hero.Job.NameKey);
            _json.Field("level", hero.Level);
            _json.Field("str", stats.Strength);
            _json.Field("agi", stats.Agility);
            _json.Field("vit", stats.Vitality);
            _json.Field("mag", stats.Magic);
            _json.Field("maxHP", hero.MaxHP);
            _json.Field("curHP", hero.CurrentHP);
            _json.Field("maxMP", hero.MaxMP);
            _json.BeginArray("skills");
            foreach (var kv in hero.LearnedSkills)
                _json.ArrayStringValue(kv.Key);
            _json.EndArray();
            _json.BeginObject("gear");
            WriteGearSlot("weapon", hero.WeaponShield1);
            WriteGearSlot("armor", hero.Armor);
            WriteGearSlot("hat", hero.Hat);
            WriteGearSlot("shield", hero.WeaponShield2);
            WriteGearSlot("acc1", hero.Accessory1);
            WriteGearSlot("acc2", hero.Accessory2);
            _json.EndObject();
        }

        /// <summary>Writes name/job/level/stats/skills/gear fields for a mercenary into the current object.</summary>
        private static void WriteMercenaryFields(Mercenary merc)
        {
            var stats = merc.GetTotalStats();
            _json.Field("name", merc.Name);
            _json.Field("type", "merc");
            _json.Field("job", merc.Job.NameKey);
            _json.Field("level", merc.Level);
            _json.Field("str", stats.Strength);
            _json.Field("agi", stats.Agility);
            _json.Field("vit", stats.Vitality);
            _json.Field("mag", stats.Magic);
            _json.Field("maxHP", merc.MaxHP);
            _json.Field("curHP", merc.CurrentHP);
            _json.Field("maxMP", merc.MaxMP);
            _json.BeginArray("skills");
            foreach (var kv in merc.LearnedSkills)
                _json.ArrayStringValue(kv.Key);
            _json.EndArray();
            _json.BeginObject("gear");
            WriteGearSlot("weapon", merc.WeaponShield1);
            WriteGearSlot("armor", merc.Armor);
            WriteGearSlot("hat", merc.Hat);
            WriteGearSlot("shield", merc.WeaponShield2);
            WriteGearSlot("acc1", merc.Accessory1);
            WriteGearSlot("acc2", merc.Accessory2);
            _json.EndObject();
        }

        /// <summary>Writes a killer sub-object for char_killed events.</summary>
        private static void WriteKillerObject(IEnemy killer)
        {
            _json.BeginObject("killer");
            _json.Field("name", killer.Name);
            _json.Field("enemyId", killer.EnemyId.ToString());
            _json.Field("level", killer.Level);
            _json.Field("str", killer.Stats.Strength);
            _json.Field("agi", killer.Stats.Agility);
            _json.Field("vit", killer.Stats.Vitality);
            _json.Field("mag", killer.Stats.Magic);
            _json.Field("maxHP", killer.MaxHP);
            _json.EndObject();
        }

        /// <summary>Writes a gear slot field when the slot is occupied.</summary>
        private static void WriteGearSlot(string slotName, IItem item)
        {
            if (item != null)
                _json.Field(slotName, item.Name);
        }

        /// <summary>Writes the post-equip resulting stat fields.</summary>
        private static void WriteResultingStats(RolePlayingFramework.Stats.StatBlock stats, int maxHP, int maxMP)
        {
            _json.Field("str", stats.Strength);
            _json.Field("agi", stats.Agility);
            _json.Field("vit", stats.Vitality);
            _json.Field("mag", stats.Magic);
            _json.Field("maxHP", maxHP);
            _json.Field("maxMP", maxMP);
        }
#endif
    }
}

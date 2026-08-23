using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace PitHero.Services
{
    /// <summary>
    /// Service responsible for loading and providing localized text strings.
    /// Loads localization files from Content/Localization/{language}/ on startup.
    /// </summary>
    public class TextService
    {
        private const string DefaultLanguage = "en-us";
        private readonly string _language;
        private readonly Dictionary<TextType, Dictionary<string, string>> _dictionaries;

        /// <summary>
        /// Initializes the TextService with the default language (en-us).
        /// </summary>
        public TextService() : this(DefaultLanguage) { }

        /// <summary>
        /// Initializes the TextService with the specified language code.
        /// </summary>
        /// <param name="language">The language code (e.g. "en-us").</param>
        public TextService(string language)
        {
            _language = language;
            _dictionaries = new Dictionary<TextType, Dictionary<string, string>>();
            LoadAll();
        }

        /// <summary>
        /// Loads all localization files for the current language.
        /// </summary>
        private void LoadAll()
        {
            LoadFile(TextType.UI, "UI.txt");
            LoadFile(TextType.Inventory, "Inventory.txt");
            LoadFile(TextType.Skill, "Skill.txt");
            LoadFile(TextType.Job, "Job.txt");
            LoadFile(TextType.Monster, "Monster.txt");
            LoadFile(TextType.Dialogue, "Dialogue.txt");
            // Names.txt holds list-valued pools, so a key repeats across lines and appends.
            LoadFile(TextType.Name, "Names.txt", appendDuplicateKeys: true);
        }

        /// <summary>
        /// Loads a single localization file into the specified text type dictionary.
        /// </summary>
        /// <param name="textType">The text type to load the file into.</param>
        /// <param name="fileName">The name of the localization file.</param>
        /// <param name="appendDuplicateKeys">When true, a key that appears on more than one line has
        /// its values joined with commas instead of overwritten. Used by list-valued files (Names.txt)
        /// so a long pool can be wrapped over several readable lines.</param>
        private void LoadFile(TextType textType, string fileName, bool appendDuplicateKeys = false)
        {
            string path = $"Content/Localization/{_language}/{fileName}";
            var dict = new Dictionary<string, string>();
            _dictionaries[textType] = dict;

            try
            {
                using (Stream stream = OpenContentStream(path))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        int separatorIndex = line.IndexOf(',');
                        if (separatorIndex < 1)
                        {
                            Nez.Debug.Log($"[TextService] Invalid line {lineNumber} in {fileName}: '{line}'");
                            continue;
                        }

                        string keyStr = line.Substring(0, separatorIndex).Trim();
                        string value = line.Substring(separatorIndex + 1);

                        if (appendDuplicateKeys && dict.TryGetValue(keyStr, out var existing) && existing.Length > 0)
                            dict[keyStr] = existing + "," + value;
                        else
                            dict[keyStr] = value;
                    }
                }
                Nez.Debug.Log($"[TextService] Loaded {dict.Count} entries from {path}");
            }
            catch (Exception ex)
            {
                Nez.Debug.Log($"[TextService] Failed to load {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens a content file for reading. TitleContainer is the normal path, but touching it
        /// initializes FNAPlatform, which needs the native libraries -- those are absent in headless
        /// hosts (unit tests, virtual balance runs), where it throws before reading a byte. Fall back
        /// to a plain file read relative to the base directory so localization still loads there.
        /// </summary>
        private static Stream OpenContentStream(string path)
        {
            try
            {
                return TitleContainer.OpenStream(path);
            }
            catch (Exception)
            {
                var fullPath = Path.Combine(AppContext.BaseDirectory, path);
                return File.OpenRead(fullPath);
            }
        }

        /// <summary>
        /// Returns the localized text for the given text type and key.
        /// Falls back to the key name if no entry is found.
        /// </summary>
        /// <param name="textType">The text type (e.g. UI, Inventory).</param>
        /// <param name="key">The text key to look up.</param>
        /// <returns>The localized string, or the key name as fallback.</returns>
        public string DisplayText(TextType textType, string key)
        {
            if (_dictionaries.TryGetValue(textType, out var dict) &&
                dict.TryGetValue(key, out string value))
            {
                return value;
            }
            Nez.Debug.Log($"[TextService] Missing key {key} for {textType}");
            return key;
        }

        /// <summary>
        /// Returns a comma-separated localized entry split into its parts, for list-valued keys such
        /// as the Names.txt character pools. Blank entries are dropped. Returns an empty array when
        /// the key is missing, so callers can detect an unloaded pool instead of getting the key back.
        /// </summary>
        /// <param name="textType">The text type (e.g. Name).</param>
        /// <param name="key">The list-valued text key to look up.</param>
        /// <returns>The entries, or an empty array if the key is missing.</returns>
        public string[] DisplayTextList(TextType textType, string key)
        {
            if (_dictionaries.TryGetValue(textType, out var dict) &&
                dict.TryGetValue(key, out string value))
            {
                var parts = value.Split(',');
                var results = new List<string>(parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    var trimmed = parts[i].Trim();
                    if (trimmed.Length > 0)
                        results.Add(trimmed);
                }
                return results.ToArray();
            }
            Nez.Debug.Log($"[TextService] Missing list key {key} for {textType}");
            return Array.Empty<string>();
        }
    }
}

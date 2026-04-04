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
        }

        /// <summary>
        /// Loads a single localization file into the specified text type dictionary.
        /// </summary>
        /// <param name="textType">The text type to load the file into.</param>
        /// <param name="fileName">The name of the localization file.</param>
        private void LoadFile(TextType textType, string fileName)
        {
            string path = $"Content/Localization/{_language}/{fileName}";
            var dict = new Dictionary<string, string>();
            _dictionaries[textType] = dict;

            try
            {
                using (Stream stream = TitleContainer.OpenStream(path))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        int separatorIndex = line.IndexOf('=');
                        if (separatorIndex < 1)
                        {
                            Nez.Debug.Log($"[TextService] Invalid line {lineNumber} in {fileName}: '{line}'");
                            continue;
                        }

                        string keyStr = line.Substring(0, separatorIndex).Trim();
                        string value = line.Substring(separatorIndex + 1);

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
    }
}

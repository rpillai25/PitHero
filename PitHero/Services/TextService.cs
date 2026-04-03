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
        private readonly Dictionary<DialogueType, Dictionary<TextKey, string>> _dictionaries;

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
            _dictionaries = new Dictionary<DialogueType, Dictionary<TextKey, string>>();
            LoadAll();
        }

        /// <summary>
        /// Loads all localization files for the current language.
        /// </summary>
        private void LoadAll()
        {
            LoadFile(DialogueType.UI, "UI.txt");
        }

        /// <summary>
        /// Loads a single localization file into the specified dialogue type dictionary.
        /// </summary>
        /// <param name="dialogueType">The dialogue type to load the file into.</param>
        /// <param name="fileName">The name of the localization file.</param>
        private void LoadFile(DialogueType dialogueType, string fileName)
        {
            string path = $"Content/Localization/{_language}/{fileName}";
            var dict = new Dictionary<TextKey, string>();
            _dictionaries[dialogueType] = dict;

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

                        if (Enum.TryParse<TextKey>(keyStr, out TextKey key))
                        {
                            dict[key] = value;
                        }
                        else
                        {
                            Nez.Debug.Log($"[TextService] Unknown key '{keyStr}' in {fileName} line {lineNumber}");
                        }
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
        /// Returns the localized text for the given dialogue type and key.
        /// Falls back to the key name if no entry is found.
        /// </summary>
        /// <param name="dialogueType">The dialogue type (e.g. UI).</param>
        /// <param name="key">The text key to look up.</param>
        /// <returns>The localized string, or the key name as fallback.</returns>
        public string DisplayText(DialogueType dialogueType, TextKey key)
        {
            if (_dictionaries.TryGetValue(dialogueType, out var dict) &&
                dict.TryGetValue(key, out string value))
            {
                return value;
            }
            Nez.Debug.Log($"[TextService] Missing key {key} for {dialogueType}");
            return key.ToString();
        }
    }
}

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PitHero.Tests
{
    /// <summary>
    /// Guards against non-ASCII characters in localization files. Nez's BitmapFont indexer
    /// (BitmapFont.cs: Characters[character]) throws KeyNotFoundException for any glyph
    /// missing from the font atlas, crashing the game the moment such text is rendered
    /// (e.g. an em-dash in a skill description shown by the Hero Crystal tab).
    /// </summary>
    [TestClass]
    public class LocalizationCharacterSetTests
    {
        [TestMethod]
        public void AllLocalizationFiles_ContainOnlyAsciiCharacters()
        {
            var localizationDir = FindLocalizationDirectory();
            Assert.IsNotNull(localizationDir, "Could not locate PitHero/Content/Localization/en-us from test base directory");

            var files = Directory.GetFiles(localizationDir, "*.txt");
            Assert.IsTrue(files.Length > 0, "No localization files found in " + localizationDir);

            for (int f = 0; f < files.Length; f++)
            {
                var lines = File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    for (int c = 0; c < lines[i].Length; c++)
                    {
                        var ch = lines[i][c];
                        Assert.IsTrue(ch <= 127,
                            $"Non-ASCII character '{ch}' (U+{(int)ch:X4}) in {Path.GetFileName(files[f])} line {i + 1}: \"{lines[i]}\" — BitmapFont will throw KeyNotFoundException rendering it");
                    }
                }
            }
        }

        private static string FindLocalizationDirectory()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "PitHero", "Content", "Localization", "en-us");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}

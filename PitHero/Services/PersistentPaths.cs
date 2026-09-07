using System;
using System.IO;

namespace PitHero.Services
{
    /// <summary>Where the game keeps files that outlive a session: the folder Nez's FileDataStore defaults to.</summary>
    public static class PersistentPaths
    {
        /// <summary>%LOCALAPPDATA%\&lt;exe name&gt; (the save slots live here; replays in a sub-folder).</summary>
        public static string BaseDirectory()
        {
            var exeName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), exeName);
        }
    }
}

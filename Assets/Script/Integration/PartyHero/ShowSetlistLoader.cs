using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Loads a <see cref="ShowSetlist"/> from a JSON file and resolves song hashes
    /// against the loaded song library.
    /// </summary>
    public static class ShowSetlistLoader
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting = Formatting.Indented,
        };

        /// <summary>
        /// Loads and returns a ShowSetlist from the given JSON file path.
        /// Song entries whose hashes cannot be resolved in the library are kept
        /// with <c>Song = null</c> and logged as warnings.
        /// Returns <c>null</c> on file-not-found or parse error.
        /// </summary>
        public static ShowSetlist Load(string path)
        {
            if (!File.Exists(path))
            {
                YargLogger.LogFormatWarning<string>("[PartyHero Setlist] Setlist file not found: {0}", path);
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var setlist = JsonConvert.DeserializeObject<ShowSetlist>(json, _jsonSettings);
                if (setlist == null)
                {
                    YargLogger.LogWarning("[PartyHero Setlist] Deserialized setlist was null.");
                    return null;
                }

                ResolveSongs(setlist);
                return setlist;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero Setlist] Failed to load setlist.");
                return null;
            }
        }

        private static void ResolveSongs(ShowSetlist setlist)
        {
            foreach (var entry in setlist.Entries)
            {
                if (entry.Type != ShowEntryType.Song) continue;

                if (string.IsNullOrEmpty(entry.Hash))
                {
                    YargLogger.LogWarning("[PartyHero Setlist] Song entry is missing a hash — will be skipped during show.");
                    continue;
                }

                var hash = HashWrapper.FromString(entry.Hash.AsSpan());
                if (SongContainer.SongsByHash.TryGetValue(hash, out var songs) && songs.Count > 0)
                {
                    entry.Song = songs[0];
                }
                else
                {
                    YargLogger.LogFormatWarning<string>(
                        "[PartyHero Setlist] Hash {0} not found in library — song entry will be skipped.",
                        entry.Hash);
                }
            }
        }
    }
}

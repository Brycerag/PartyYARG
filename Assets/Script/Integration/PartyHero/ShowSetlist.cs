using System.Collections.Generic;
using Newtonsoft.Json;
using YARG.Core.Song;

namespace YARG.Integration.PartyHero
{
    public enum ShowEntryType
    {
        Song,
        Swap,
        Break,
        ShowEnd,
    }

    /// <summary>
    /// A single item in a <see cref="ShowSetlist"/> sequence.
    /// Song entries carry a hash string that is resolved to a <see cref="SongEntry"/>
    /// at load time. Non-song entries carry optional label / duration metadata.
    /// </summary>
    public class ShowEntry
    {
        [JsonProperty("type")]
        public ShowEntryType Type;

        /// <summary>Hash string identifying the song — only for Song entries.</summary>
        [JsonProperty("hash")]
        public string Hash;

        /// <summary>Optional display label shown in the interstitial UI.</summary>
        [JsonProperty("label")]
        public string Label;

        /// <summary>Duration in seconds for Break entries. 0 = manual skip only.</summary>
        [JsonProperty("durationSeconds")]
        public float DurationSeconds;

        /// <summary>Resolved at load time by <see cref="ShowSetlistLoader"/>. Not serialized.</summary>
        [JsonIgnore]
        public SongEntry Song;
    }

    /// <summary>
    /// An ordered list of show entries loaded from a PartyYARG setlist JSON file.
    /// </summary>
    public class ShowSetlist
    {
        [JsonProperty("name")]
        public string Name = "Untitled Show";

        [JsonProperty("venue")]
        public string Venue = "";

        [JsonProperty("entries")]
        public List<ShowEntry> Entries = new();
    }
}

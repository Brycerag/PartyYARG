using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Serializable settings for the PartyHero live-show integration system.
    /// Stored separately from YARG's main settings.json at:
    ///   {PersistentDataPath}/partyhero_settings.json
    /// </summary>
    [Serializable]
    public class PartyHeroSettings
    {
        // ── MIDI Input ────────────────────────────────────────────────────
        /// <summary>Whether to open a MIDI input device at startup.</summary>
        public bool   MidiEnabled    = true;

        /// <summary>
        /// Partial name match for the preferred MIDI device (case-insensitive).
        /// Leave empty to use the first available device.
        /// </summary>
        public string MidiDeviceName = "";

        /// <summary>MIDI note number that signals the band is ready. Default: 60 (C4).</summary>
        public int MidiBandReadyNote   = 60;

        /// <summary>MIDI note number that forces advancing to the next show state. Default: 61 (C#4).</summary>
        public int MidiForceNextNote   = 61;

        /// <summary>MIDI note number that toggles the player ready state. Default: 62 (D4).</summary>
        public int MidiPlayerReadyNote = 62;

        // ── TCP Command Server ────────────────────────────────────────────
        /// <summary>Whether to open a TCP command listener at startup.</summary>
        public bool TcpEnabled = true;

        /// <summary>
        /// Port the TCP server listens on.
        /// DAW/stage system sends newline-delimited commands here.
        /// Supported commands: BAND_READY, PLAYER_READY, FORCE_STATE {name}
        /// </summary>
        public int TcpPort = 9000;

        // ── OSC / UDP Receiver ────────────────────────────────────────────
        /// <summary>Whether to listen for OSC (Open Sound Control) UDP packets.</summary>
        public bool OscEnabled = true;

        /// <summary>UDP port to listen on for OSC messages.</summary>
        public int OscPort = 8000;

        /// <summary>OSC address for the "band ready" message.</summary>
        public string OscBandReadyAddress  = "/partyhero/band_ready";

        /// <summary>OSC address for the "force state" message. First argument is the state name.</summary>
        public string OscForceStateAddress = "/partyhero/force_state";

        /// <summary>OSC address for the "player ready toggle" message.</summary>
        public string OscPlayerReadyAddress = "/partyhero/player_ready";

        // ── Development / Testing ─────────────────────────────────────────
        /// <summary>
        /// When true, keyboard shortcuts are active for simulating triggers without hardware.
        /// Only has effect in editor, nightly, or test builds.
        ///   F9  = Band Ready
        ///   F10 = Player Ready Toggle
        ///   F11 = Force Next State
        /// </summary>
        public bool EnableDevKeys = true;

        // ── Persistence ───────────────────────────────────────────────────

        private static string SettingsPath =>
            Path.Combine(Application.persistentDataPath, "partyhero_settings.json");

        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting        = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        };

        /// <summary>
        /// Loads settings from disk. Writes a default file if none exists.
        /// Falls back to defaults on any parse error.
        /// </summary>
        public static PartyHeroSettings Load()
        {
            try
            {
                string path = SettingsPath;
                if (File.Exists(path))
                {
                    string json     = File.ReadAllText(path);
                    var    settings = JsonConvert.DeserializeObject<PartyHeroSettings>(json, _jsonSettings);
                    if (settings != null)
                    {
                        YargLogger.LogInfo("[PartyHero] Settings loaded from disk.");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero] Failed to load settings, using defaults.");
            }

            var defaults = new PartyHeroSettings();
            defaults.Save(); // write defaults so user can easily edit
            return defaults;
        }

        /// <summary>Saves current settings to disk.</summary>
        public void Save()
        {
            try
            {
                string path = SettingsPath;
                File.WriteAllText(path, JsonConvert.SerializeObject(this, _jsonSettings));
                YargLogger.LogFormatInfo("[PartyHero] Settings saved to: {0}", path);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero] Failed to save settings.");
            }
        }
    }
}

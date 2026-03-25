using System;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Central MonoSingleton that owns the MIDI, OSC, and TCP integration components for
    /// the PartyHero live show system.  Translates incoming signals from all three transports
    /// into a small set of game-level static events that the rest of YARG can subscribe to
    /// without depending on any specific transport.
    ///
    /// Scene setup: attach this to a persistent GameObject in PersistentScene.unity.
    ///
    /// Static events (subscribe from anywhere):
    ///   <see cref="OnBandReady"/>          — the live band has signalled they are ready
    ///   <see cref="OnPlayerReadyToggle"/>  — a player's ready state should be toggled
    ///   <see cref="OnForceState"/>         — force the show into the named state
    /// </summary>
    public class PartyHeroController : MonoSingleton<PartyHeroController>
    {
        // ── Static game-level events ──────────────────────────────────────

        /// <summary>The live band has signalled they are ready to start.</summary>
        public static event Action OnBandReady;

        /// <summary>A player ready state toggle was received.</summary>
        public static event Action OnPlayerReadyToggle;

        /// <summary>A force-state command was received; argument is the state name.</summary>
        public static event Action<string> OnForceState;

        // ── Instance state ────────────────────────────────────────────────

        public static PartyHeroSettings CurrentSettings { get; private set; }

        private MidiInputController _midi;
        private OscReceiver         _osc;
        private TcpCommandServer    _tcp;

        // ── MonoSingleton lifecycle ───────────────────────────────────────

        protected override void SingletonAwake()
        {
            // intentionally empty — real init happens in Start() so that
            // Application.persistentDataPath and other Unity APIs are fully ready
        }

        protected override void SingletonDestroy()
        {
            StopListeners();
        }

        private void Start()
        {
            CurrentSettings = PartyHeroSettings.Load();
            StartListeners();
        }

        private void Update()
        {
            _midi?.Tick();
            _osc?.Tick();
            _tcp?.Tick();

#if UNITY_EDITOR || YARG_NIGHTLY_BUILD || YARG_TEST_BUILD
            HandleDevKeys();
#endif
        }

        // ── Settings reload ───────────────────────────────────────────────

        /// <summary>
        /// Reload settings from disk and restart all listeners with the new configuration.
        /// Call this after the user saves settings in the UI.
        /// </summary>
        public void ReloadSettings()
        {
            StopListeners();
            CurrentSettings = PartyHeroSettings.Load();
            StartListeners();
            YargLogger.LogInfo("[PartyHero] Settings reloaded.");
        }

        // ── Listener management ───────────────────────────────────────────

        private void StartListeners()
        {
            var cfg = CurrentSettings;

            // ── MIDI ──────────────────────────────────────────────────────
            if (cfg.MidiEnabled)
            {
                _midi = new MidiInputController();
                _midi.NoteOn += OnMidiNoteOn;
                _midi.Open(cfg.MidiDeviceName);
            }

            // ── OSC / UDP ─────────────────────────────────────────────────
            if (cfg.OscEnabled)
            {
                _osc = new OscReceiver();
                _osc.MessageReceived += OnOscMessage;
                _osc.Start(cfg.OscPort);
            }

            // ── TCP ───────────────────────────────────────────────────────
            if (cfg.TcpEnabled)
            {
                _tcp = new TcpCommandServer();
                _tcp.CommandReceived += OnTcpCommand;
                _tcp.Start(cfg.TcpPort);
            }
        }

        private void StopListeners()
        {
            if (_midi != null)
            {
                _midi.NoteOn -= OnMidiNoteOn;
                _midi.Dispose();
                _midi = null;
            }

            if (_osc != null)
            {
                _osc.MessageReceived -= OnOscMessage;
                _osc.Dispose();
                _osc = null;
            }

            if (_tcp != null)
            {
                _tcp.CommandReceived -= OnTcpCommand;
                _tcp.Dispose();
                _tcp = null;
            }
        }

        // ── MIDI handlers ─────────────────────────────────────────────────

        private void OnMidiNoteOn(int note, int velocity)
        {
            var cfg = CurrentSettings;
            if (note == cfg.MidiBandReadyNote)
            {
                YargLogger.LogInfo("[PartyHero MIDI] Band ready note received.");
                OnBandReady?.Invoke();
            }
            else if (note == cfg.MidiPlayerReadyNote)
            {
                YargLogger.LogInfo("[PartyHero MIDI] Player ready note received.");
                OnPlayerReadyToggle?.Invoke();
            }
            else if (note == cfg.MidiForceNextNote)
            {
                YargLogger.LogInfo("[PartyHero MIDI] Force-next note received.");
                OnForceState?.Invoke("Next");
            }
        }

        // ── OSC handlers ──────────────────────────────────────────────────

        private void OnOscMessage(string address, object[] args)
        {
            var cfg = CurrentSettings;

            if (string.Equals(address, cfg.OscBandReadyAddress, StringComparison.OrdinalIgnoreCase))
            {
                YargLogger.LogFormatInfo("[PartyHero OSC] Band ready: {0}", address);
                OnBandReady?.Invoke();
            }
            else if (string.Equals(address, cfg.OscPlayerReadyAddress, StringComparison.OrdinalIgnoreCase))
            {
                YargLogger.LogFormatInfo("[PartyHero OSC] Player ready: {0}", address);
                OnPlayerReadyToggle?.Invoke();
            }
            else if (string.Equals(address, cfg.OscForceStateAddress, StringComparison.OrdinalIgnoreCase))
            {
                string stateName = args.Length > 0 ? args[0]?.ToString() : "Next";
                YargLogger.LogFormatInfo("[PartyHero OSC] Force state: {0}", stateName);
                OnForceState?.Invoke(stateName ?? "Next");
            }
        }

        // ── TCP handlers ──────────────────────────────────────────────────

        private void OnTcpCommand(string line)
        {
            if (!TcpCommandServer.TryParseCommand(line, out string keyword, out string argument))
                return;

            switch (keyword)
            {
                case "BAND_READY":
                    YargLogger.LogInfo("[PartyHero TCP] BAND_READY received.");
                    OnBandReady?.Invoke();
                    break;

                case "PLAYER_READY":
                    YargLogger.LogInfo("[PartyHero TCP] PLAYER_READY received.");
                    OnPlayerReadyToggle?.Invoke();
                    break;

                case "FORCE_STATE":
                    string stateName = string.IsNullOrEmpty(argument) ? "Next" : argument;
                    YargLogger.LogFormatInfo("[PartyHero TCP] FORCE_STATE {0}", stateName);
                    OnForceState?.Invoke(stateName);
                    break;

                default:
                    YargLogger.LogFormatWarning("[PartyHero TCP] Unknown command: {0}", keyword);
                    break;
            }
        }

        // ── Dev keys (editor / nightly builds only) ───────────────────────

#if UNITY_EDITOR || YARG_NIGHTLY_BUILD || YARG_TEST_BUILD
        private void HandleDevKeys()
        {
            if (!CurrentSettings.EnableDevKeys) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                YargLogger.LogInfo("[PartyHero DEV] F9 → Band Ready");
                OnBandReady?.Invoke();
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                YargLogger.LogInfo("[PartyHero DEV] F10 → Player Ready Toggle");
                OnPlayerReadyToggle?.Invoke();
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                YargLogger.LogInfo("[PartyHero DEV] F11 → Force State: Next");
                OnForceState?.Invoke("Next");
            }
        }
#endif
    }
}

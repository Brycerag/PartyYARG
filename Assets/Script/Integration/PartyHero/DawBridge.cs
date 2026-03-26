using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Bridges game state changes (scene transitions, pause/resume, show flow) to
    /// outgoing OSC UDP packets and/or MIDI note triggers, allowing a DAW or stage
    /// controller to stay in sync with the live show without polling the game.
    ///
    /// Subscribes to:
    ///   • <see cref="YARG.Integration.GameStateFetcher.GameStateChange"/>
    ///     — fires on every scene change and every pause/resume
    ///   • <see cref="SetlistManager.OnShowActivated"/> / <see cref="SetlistManager.OnShowEnded"/>
    ///     — fires when a full setlist show starts or ends via SetlistManager
    ///
    /// OSC message reference (addresses are configurable in PartyHeroSettings):
    ///   /partyyarg/show/start    s{showName}
    ///   /partyyarg/show/end
    ///   /partyyarg/song/readyup  s{name}  s{artist}  i{showIndex}
    ///   /partyyarg/song/start    s{name}  s{artist}  i{showIndex}
    ///   /partyyarg/song/end      s{name}  s{artist}  i{showIndex}
    ///   /partyyarg/break/start   s{label} f{durationSeconds}
    ///   /partyyarg/swap/start    s{label}
    ///   /partyyarg/pause
    ///   /partyyarg/resume
    ///
    /// MIDI: each event fires a configured Note On (vel 100) + Note Off (vel 0) on
    ///       the configured channel. Note -1 = disabled for that event.
    ///
    /// This MonoSingleton should be placed on a persistent GameObject alongside
    /// PartyHeroController and SetlistManager.
    /// </summary>
    public class DawBridge : MonoSingleton<DawBridge>
    {
        private OscSender            _osc;
        private MidiOutputController _midiOut;
        private PartyHeroSettings    _settings;

        private SceneIndex _lastScene      = SceneIndex.Persistent;
        private bool       _lastPaused;

        // ── MonoSingleton lifecycle ───────────────────────────────────────

        protected override void SingletonAwake()
        {
            // Subscribe immediately so we never miss events.
            GameStateFetcher.GameStateChange += OnGameStateChange;
            GameStateFetcher.SongStarted     += OnSongStarted;
            SetlistManager.OnShowActivated   += OnShowActivated;
            SetlistManager.OnShowEnded       += OnShowEnded;
        }

        protected override void SingletonDestroy()
        {
            GameStateFetcher.GameStateChange -= OnGameStateChange;
            GameStateFetcher.SongStarted     -= OnSongStarted;
            SetlistManager.OnShowActivated   -= OnShowActivated;
            SetlistManager.OnShowEnded       -= OnShowEnded;
            StopOutputs();
        }

        private void Start()
        {
            // PartyHeroController.Start() may or may not have run yet — read settings directly.
            _settings = PartyHeroController.CurrentSettings ?? PartyHeroSettings.Load();
            StartOutputs();
        }

        // ── Settings reload ───────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="PartyHeroController.ReloadSettings"/> after new settings
        /// are loaded from disk.
        /// </summary>
        public void ReloadSettings()
        {
            _settings = PartyHeroController.CurrentSettings ?? PartyHeroSettings.Load();
            StopOutputs();
            StartOutputs();
            YargLogger.LogInfo("[PartyHero DAW] Outputs reloaded.");
        }

        // ── Output management ─────────────────────────────────────────────

        private void StartOutputs()
        {
            if (_settings == null) return;

            if (_settings.OscOutputEnabled)
            {
                _osc = new OscSender();
                _osc.Start(_settings.OscOutputHost, _settings.OscOutputPort);
            }

            if (_settings.MidiOutputEnabled)
            {
                _midiOut = new MidiOutputController();
                _midiOut.Open(_settings.MidiOutputDeviceName);
            }
        }

        private void StopOutputs()
        {
            _osc?.Dispose();     _osc     = null;
            _midiOut?.Dispose(); _midiOut = null;
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnGameStateChange(GameStateFetcher.State state)
        {
            if (_settings == null) return;

            bool sceneChanged = state.CurrentScene != _lastScene;
            bool pauseChanged = state.Paused        != _lastPaused;

            if (sceneChanged)
            {
                _lastPaused = false;  // reset pause tracking on any scene change
                HandleSceneChange(state.CurrentScene);
            }
            else if (pauseChanged)
            {
                // Only a pause/resume within the same scene — no duplicate firing.
                if (state.Paused)
                {
                    SendOsc(_settings.OscPauseAddress);
                    SendMidi(_settings.MidiNotePause);
                }
                else
                {
                    SendOsc(_settings.OscResumeAddress);
                    SendMidi(_settings.MidiNoteResume);
                }
            }

            _lastScene  = state.CurrentScene;
            _lastPaused = state.Paused;
        }

        private void HandleSceneChange(SceneIndex scene)
        {
            // Read song/show data from GlobalVariables — always set before LoadScene is called.
            var    song     = GlobalVariables.State.CurrentSong;
            string name     = song?.Name.ToString()   ?? "";
            string artist   = song?.Artist.ToString() ?? "";
            int    showIdx  = GlobalVariables.State.ShowIndex;

            switch (scene)
            {
                case SceneIndex.ReadyUp:
                    SendOsc(_settings.OscSongReadyUpAddress, name, artist, showIdx);
                    SendMidi(_settings.MidiNoteSongReadyUp);
                    break;

                case SceneIndex.Gameplay:
                    // song/start is deferred to OnSongStarted (GameManager.SongStarted),
                    // which fires at t=0 of the highway — not here at scene-load time.
                    break;

                case SceneIndex.Score:
                    SendOsc(_settings.OscSongEndAddress, name, artist, showIdx);
                    SendMidi(_settings.MidiNoteSongEnd);
                    break;

                case SceneIndex.SetBreak:
                {
                    var entry = SetlistManager.Instance?.CurrentEntry;
                    SendOsc(_settings.OscBreakStartAddress,
                        entry?.Label ?? "",
                        entry?.DurationSeconds ?? 0f);
                    SendMidi(_settings.MidiNoteBreakStart);
                    break;
                }

                case SceneIndex.WaitingForSwap:
                {
                    var entry = SetlistManager.Instance?.CurrentEntry;
                    SendOsc(_settings.OscSwapStartAddress, entry?.Label ?? "");
                    SendMidi(_settings.MidiNoteSwapStart);
                    break;
                }

                case SceneIndex.ShowEnd:
                    SendOsc(_settings.OscShowEndAddress);
                    SendMidi(_settings.MidiNoteShowEnd);
                    break;
            }
        }

        private void OnShowActivated(ShowSetlist setlist)
        {
            if (_settings == null) return;
            SendOsc(_settings.OscShowStartAddress, setlist.Name);
            SendMidi(_settings.MidiNoteShowStart);
        }

        /// <summary>
        /// Fires when <see cref="GameStateFetcher.SetSongStarted"/> is called — i.e. at the
        /// exact instant the highway starts scrolling and the SongRunner clock reads t=0.
        /// This is the correct moment to trigger Ableton (or any DAW) transport:
        ///   • Ableton transport starts from its arrangement t=0 (bar 1).
        ///   • The chart should have an empty pre-roll matching the DAW count-in duration
        ///     so that bar 3 (band entry) aligns with the first notes in the highway.
        /// </summary>
        private void OnSongStarted()
        {
            if (_settings == null) return;

            var    song    = GlobalVariables.State.CurrentSong;
            string name    = song?.Name.ToString()   ?? "";
            string artist  = song?.Artist.ToString() ?? "";
            int    showIdx = GlobalVariables.State.ShowIndex;

            SendOsc(_settings.OscSongStartAddress, name, artist, showIdx);
            SendMidi(_settings.MidiNoteSongStart);
        }

        private void OnShowEnded()
        {
            // The ShowEnd scene transition already fires the OSC/MIDI — no duplicate needed here.
            // This hook exists for future use (e.g. early show abort before ShowEnd scene loads).
        }

        // ── Send helpers ──────────────────────────────────────────────────

        private void SendOsc(string address, params object[] args)
        {
            if (_osc == null) return;
            YargLogger.LogFormatDebug<string>("[PartyHero DAW] OSC → {0}", address);
            _osc.Send(address, args);
        }

        private void SendMidi(int note)
        {
            if (_midiOut == null || note < 0 || _settings == null) return;
            YargLogger.LogFormatDebug<int>("[PartyHero DAW] MIDI → note {0}", note);
            _midiOut.SendNote(_settings.MidiOutputChannel, note);
        }
    }
}

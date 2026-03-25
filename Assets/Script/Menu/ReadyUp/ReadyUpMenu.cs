using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Integration.PartyHero;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.ReadyUp
{
    /// <summary>
    /// Manages the ready-up interstitial screen that sits between the score screen
    /// and the next song in a live show.
    ///
    /// The scene waits for every human player to press Green and for the live band
    /// to send a "band ready" signal via MIDI, OSC, or TCP before launching gameplay.
    /// An Orange "Force Start" override is always available for the operator.
    ///
    /// Scene setup (do in Unity Editor after creating the scene):
    ///   1. Add an empty GameObject named "ReadyUp Manager"; attach this script.
    ///   2. Add a Canvas + EventSystem.
    ///   3. Wire the serialized fields below to your UI TextMeshPro elements.
    ///   4. Optionally create a <see cref="_playerCardContainer"/> with a prefab per
    ///      player showing their name and ready state.
    /// </summary>
    public class ReadyUpMenu : MonoBehaviour
    {
        // ── Inspector-wired UI (all optional — the scene functions without them) ──

        [Header("Song Info")]
        [SerializeField] private TextMeshProUGUI _songTitleText;
        [SerializeField] private TextMeshProUGUI _artistNameText;

        [Header("Status Text")]
        [SerializeField] private TextMeshProUGUI _bandStatusText;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Player Cards")]
        [SerializeField] private Transform       _playerCardContainer;
        [SerializeField] private ReadyUpPlayerCard _playerCardPrefab;

        [Header("Timing")]
        [SerializeField] private float _countdownSeconds = 3f;

        // ── Private state ─────────────────────────────────────────────────

        private readonly HashSet<YargPlayer>             _readyPlayers = new();
        private readonly List<ReadyUpPlayerCard>         _playerCards  = new();

        private bool _bandReady;
        private bool _launching;
        private bool _countdownRunning;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            _readyPlayers.Clear();
            _launching       = false;
            _countdownRunning = false;
            _bandReady = false;

            // If no external transports are configured, auto-satisfy band ready so
            // the screen doesn't wait forever for a signal that can never arrive.
            var settings = PartyHeroController.CurrentSettings;
            if (settings is { MidiEnabled: false, OscEnabled: false, TcpEnabled: false })
                _bandReady = true;

            RefreshSongInfo();
            BuildPlayerCards();

            PartyHeroController.OnBandReady          += HandleBandReady;
            PartyHeroController.OnPlayerReadyToggle  += HandlePlayerReadyToggle;
            PartyHeroController.OnForceState         += HandleForceState;

            SetNavigationScheme();
            RefreshUI();
        }

        private void OnDisable()
        {
            PartyHeroController.OnBandReady          -= HandleBandReady;
            PartyHeroController.OnPlayerReadyToggle  -= HandlePlayerReadyToggle;
            PartyHeroController.OnForceState         -= HandleForceState;

            foreach (var card in _playerCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _playerCards.Clear();

            Navigator.Instance.PopScheme();
        }

        // ── Navigation scheme ─────────────────────────────────────────────

        private void SetNavigationScheme()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new List<NavigationScheme.Entry>
            {
                // Each player presses Green to toggle their own ready state
                new(MenuAction.Green, "Menu.ReadyUp.ToggleReady", ctx =>
                {
                    TogglePlayerReady(ctx.Player);
                }),

                // Orange = operator force-start (bypasses all ready checks)
                new(MenuAction.Orange, "Menu.ReadyUp.ForceStart", () =>
                {
                    YargLogger.LogInfo("[ReadyUp] Force-start by operator.");
                    LaunchGameplay();
                }),

                // Red = end show early
                new(MenuAction.Red, "Menu.ScoreScreen.EndSetlistEarly", () =>
                {
                    GlobalVariables.State.PlayingAShow = false;
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                }),
            }, false));
        }

        // ── Player ready toggling ─────────────────────────────────────────

        private void TogglePlayerReady(YargPlayer player)
        {
            if (_readyPlayers.Contains(player))
                _readyPlayers.Remove(player);
            else
                _readyPlayers.Add(player);

            UpdatePlayerCard(player);
            RefreshUI();
            TryAutoLaunch();
        }

        // ── External signal handlers ──────────────────────────────────────

        private void HandleBandReady()
        {
            if (_bandReady) return;
            _bandReady = true;
            YargLogger.LogInfo("[ReadyUp] Band ready signal received.");
            RefreshUI();
            TryAutoLaunch();
        }

        private void HandlePlayerReadyToggle()
        {
            // An external MIDI/OSC signal marks ALL human players ready at once.
            foreach (var player in GlobalVariables.Instance.Players)
            {
                if (!player.Profile.IsBot)
                    _readyPlayers.Add(player);
            }

            foreach (var card in _playerCards)
                card?.SetReady(true);

            RefreshUI();
            TryAutoLaunch();
        }

        private void HandleForceState(string state)
        {
            if (state == "Next" || state == "ForceStart")
            {
                YargLogger.LogFormatInfo("[ReadyUp] Force state '{0}' received.", state);
                LaunchGameplay();
            }
        }

        // ── Auto-launch logic ─────────────────────────────────────────────

        private void TryAutoLaunch()
        {
            if (_launching || _countdownRunning) return;
            if (!_bandReady) return;
            if (!AllHumanPlayersReady()) return;

            StartCoroutine(ReadyCountdown());
        }

        private IEnumerator ReadyCountdown()
        {
            _countdownRunning = true;
            YargLogger.LogInfo("[ReadyUp] All ready — starting countdown.");

            float remaining = _countdownSeconds;
            while (remaining > 0f)
            {
                if (_statusText != null)
                    _statusText.text = Mathf.CeilToInt(remaining).ToString();

                remaining -= Time.deltaTime;
                yield return null;
            }

            _countdownRunning = false;
            LaunchGameplay();
        }

        private void LaunchGameplay()
        {
            if (_launching) return;
            _launching = true;

            StopAllCoroutines(); // in case countdown was running

            YargLogger.LogInfo("[ReadyUp] Launching gameplay.");
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        // ── UI helpers ────────────────────────────────────────────────────

        private void RefreshSongInfo()
        {
            var song = GlobalVariables.State.CurrentSong;
            if (song == null) return;

            if (_songTitleText  != null) _songTitleText.text  = song.Name;
            if (_artistNameText != null) _artistNameText.text = song.Artist;
        }

        private void BuildPlayerCards()
        {
            if (_playerCardPrefab == null || _playerCardContainer == null) return;

            foreach (var player in GlobalVariables.Instance.Players)
            {
                if (player.Profile.IsBot) continue;

                var card = Instantiate(_playerCardPrefab, _playerCardContainer);
                card.Initialize(player);
                _playerCards.Add(card);
            }
        }

        private void UpdatePlayerCard(YargPlayer player)
        {
            foreach (var card in _playerCards)
            {
                if (card.Player == player)
                {
                    card.SetReady(_readyPlayers.Contains(player));
                    return;
                }
            }
        }

        private void RefreshUI()
        {
            // Band status line
            if (_bandStatusText != null)
            {
                _bandStatusText.text = _bandReady
                    ? Localize.Key("Menu.ReadyUp.BandReady")
                    : Localize.Key("Menu.ReadyUp.BandWaiting");
            }

            // General status / countdown
            if (_statusText != null && !_countdownRunning)
            {
                _statusText.text = (AllHumanPlayersReady() && _bandReady)
                    ? Localize.Key("Menu.ReadyUp.AllReady")
                    : Localize.Key("Menu.ReadyUp.Waiting");
            }
        }

        private bool AllHumanPlayersReady()
        {
            bool hasHumans = false;
            foreach (var p in GlobalVariables.Instance.Players)
            {
                if (p.Profile.IsBot) continue;
                hasHumans = true;
                if (!_readyPlayers.Contains(p)) return false;
            }
            return hasHumans;
        }
    }
}

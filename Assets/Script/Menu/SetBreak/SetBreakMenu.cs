using System;
using UnityEngine;
using YARG;
using YARG.Core.Input;
using YARG.Integration.PartyHero;
using YARG.Menu.Navigation;

namespace YARG.Menu.SetBreak
{
    /// <summary>
    /// Shown during a set intermission / break.
    /// If the current <see cref="ShowEntry"/> has a DurationSeconds > 0, a countdown
    /// timer auto-advances when it expires. Operators can also skip the break
    /// immediately via the Green button, a MIDI/TCP band-ready event, or
    /// a FORCE_STATE END_BREAK command.
    /// </summary>
    public class SetBreakMenu : MonoBehaviour
    {
        private bool _advancing;
        private bool _hasTimer;
        private float _remainingSeconds;

        private void OnEnable()
        {
            _advancing = false;

            var entry = SetlistManager.Instance?.CurrentEntry;
            if (entry != null && entry.DurationSeconds > 0f)
            {
                _hasTimer = true;
                _remainingSeconds = entry.DurationSeconds;
            }
            else
            {
                _hasTimer = false;
            }

            PartyHeroController.OnBandReady += HandleBandReady;
            PartyHeroController.OnForceState += HandleForceState;

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.SetBreak.SkipBreak", EndBreak),
            }, false));
        }

        private void OnDisable()
        {
            PartyHeroController.OnBandReady -= HandleBandReady;
            PartyHeroController.OnForceState -= HandleForceState;
            Navigator.Instance?.PopScheme();
        }

        private void Update()
        {
            if (!_hasTimer || _advancing) return;

            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0f)
                EndBreak();
        }

        private void HandleBandReady() => EndBreak();

        private void HandleForceState(string state)
        {
            if (state.Equals("END_BREAK", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Next",      StringComparison.OrdinalIgnoreCase))
                EndBreak();
        }

        public void EndBreak()
        {
            if (_advancing) return;
            _advancing = true;

            var nextScene = SetlistManager.Instance != null
                ? SetlistManager.Instance.Advance()
                : SceneIndex.ReadyUp;

            GlobalVariables.Instance.LoadScene(nextScene);
        }

        /// <summary>Remaining seconds on the break timer. Returns -1 if there is no timer.</summary>
        public float RemainingSeconds => _hasTimer ? _remainingSeconds : -1f;
    }
}

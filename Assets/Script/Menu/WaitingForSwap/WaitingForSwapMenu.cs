using System;
using UnityEngine;
using YARG;
using YARG.Core.Input;
using YARG.Integration.PartyHero;
using YARG.Menu.Navigation;

namespace YARG.Menu.WaitingForSwap
{
    /// <summary>
    /// Shown between songs when a player instrument swap is required.
    /// Waits for MIDI/TCP band-ready or force-state "SWAP_DONE" signal,
    /// or manual Green button confirmation, then advances the setlist.
    /// </summary>
    public class WaitingForSwapMenu : MonoBehaviour
    {
        private bool _advancing;

        private void OnEnable()
        {
            _advancing = false;

            PartyHeroController.OnBandReady += HandleBandReady;
            PartyHeroController.OnForceState += HandleForceState;

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.WaitingForSwap.ConfirmSwap", ConfirmSwap),
            }, false));
        }

        private void OnDisable()
        {
            PartyHeroController.OnBandReady -= HandleBandReady;
            PartyHeroController.OnForceState -= HandleForceState;
            Navigator.Instance?.PopScheme();
        }

        private void HandleBandReady() => ConfirmSwap();

        private void HandleForceState(string state)
        {
            if (state.Equals("SWAP_DONE", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Next",      StringComparison.OrdinalIgnoreCase))
                ConfirmSwap();
        }

        public void ConfirmSwap()
        {
            if (_advancing) return;
            _advancing = true;

            var nextScene = SetlistManager.Instance != null
                ? SetlistManager.Instance.Advance()
                : SceneIndex.ReadyUp;

            GlobalVariables.Instance.LoadScene(nextScene);
        }
    }
}

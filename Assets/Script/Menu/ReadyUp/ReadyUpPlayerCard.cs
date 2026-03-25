using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Player;

namespace YARG.Menu.ReadyUp
{
    /// <summary>
    /// A single per-player card displayed on the ready-up screen.
    /// Wire <see cref="_nameText"/>, <see cref="_statusText"/>, and
    /// <see cref="_background"/> in the Inspector.
    /// </summary>
    public class ReadyUpPlayerCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Image           _background;

        [Header("Colors")]
        [SerializeField] private Color _readyColor   = new(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _waitingColor = new(0.3f, 0.3f, 0.3f, 1f);

        public YargPlayer Player { get; private set; }

        public void Initialize(YargPlayer player)
        {
            Player = player;

            if (_nameText != null)
                _nameText.text = player.Profile.Name;

            SetReady(false);
        }

        public void SetReady(bool ready)
        {
            if (_statusText  != null) _statusText.text = ready ? "READY" : "Not Ready";
            if (_background  != null) _background.color = ready ? _readyColor : _waitingColor;
        }
    }
}

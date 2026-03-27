using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    /// <summary>
    /// A non-localizable string dropdown that lists all video capture devices
    /// available at the time the setting is first constructed (webcams, USB capture
    /// cards, OBS Virtual Camera, etc.).
    ///
    /// The first entry is always <c>""</c> (meaning "first available device"),
    /// following the same convention used by <see cref="YARG.Integration.PartyHero"/>'s
    /// MIDI device dropdowns.
    /// </summary>
    public sealed class WebCamDeviceDropdownSetting : DropdownSetting<string>
    {
        public WebCamDeviceDropdownSetting(string value = "", Action<string> onChange = null)
            : base(value, onChange, localizable: false)
        {
            // Base ctor calls UpdateValues() before we can populate here,
            // so populate now that base init is complete.
            _possibleValues.Clear();
            _possibleValues.Add(""); // "" = first available

            foreach (var device in WebCamTexture.devices)
            {
                _possibleValues.Add(device.name);
            }

            // If the saved device name is no longer present, fall back silently.
            if (!_possibleValues.Contains(_value))
            {
                _value = "";
            }
        }

        // Called by the base ctor before WebCamTexture is accessible in this
        // constructor body — intentionally left empty; population happens above.
        public override void UpdateValues() { }
        /// <summary>
        /// Re-enumerates capture devices. Call this when the user plugs in a new
        /// device after the game has booted. The current selection is preserved if
        /// the device is still present; otherwise it falls back to "".
        /// </summary>
        public void Refresh()
        {
            var previous = _value;
            _possibleValues.Clear();
            _possibleValues.Add("");

            foreach (var device in WebCamTexture.devices)
            {
                _possibleValues.Add(device.name);
            }

            _value = _possibleValues.Contains(previous) ? previous : "";
        }    }
}

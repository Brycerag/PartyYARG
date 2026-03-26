using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Logging;
using YARG.Integration.PartyHero;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    /// <summary>
    /// Settings tab for PartyYARG live-show integration.
    /// Backed by <see cref="PartyHeroSettings"/> (a separate JSON file from YARG's
    /// main settings).  Settings are loaded on tab enter and saved + reloaded into
    /// <see cref="PartyHeroController"/> on tab exit.
    ///
    /// Covers:
    ///   • MIDI Input — enabled, device, trigger notes
    ///   • OSC / TCP Input — enabled, ports
    ///   • OSC Output — enabled, host, port
    ///   • MIDI Output — enabled, device, channel, all 9 event notes
    ///   • Dev Keys — editor/nightly shortcut toggle
    /// </summary>
    public class PartyHeroTab : Tab
    {
        // ── Cached Addressable prefabs ────────────────────────────────────
        private static GameObject _headerPrefab;

        // ── Live settings reference ───────────────────────────────────────
        private PartyHeroSettings _cfg;

        public PartyHeroTab(string name, string icon = "Generic") : base(name, icon)
        {
        }

        // ── Tab lifecycle ─────────────────────────────────────────────────

        public override void OnTabEnter()
        {
            // Always load fresh from disk so we reflect any external edits.
            _cfg = PartyHeroController.CurrentSettings ?? PartyHeroSettings.Load();
        }

        public override void OnTabExit()
        {
            // Persist any changes made in the UI, then hot-reload the integration layer.
            _cfg?.Save();
            PartyHeroController.Instance?.ReloadSettings();
            YargLogger.LogInfo("[PartyHero] Settings saved from UI.");
        }

        // ── Tab build ─────────────────────────────────────────────────────

        public override void BuildSettingTab(Transform container, NavigationGroup navGroup)
        {
            _cfg ??= PartyHeroController.CurrentSettings ?? PartyHeroSettings.Load();

            int idx = 0;

            // ─── MIDI Input ───────────────────────────────────────────────
            SpawnHeader("MidiInput", container);

            AddToggle("PartyYARG.MidiEnabled",
                _cfg.MidiEnabled, v => _cfg.MidiEnabled = v,
                container, navGroup, ref idx);

            AddMidiInDeviceDropdown(container, navGroup, ref idx);

            AddInt("PartyYARG.MidiBandReadyNote",
                _cfg.MidiBandReadyNote, v => _cfg.MidiBandReadyNote = v,
                0, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiForceNextNote",
                _cfg.MidiForceNextNote, v => _cfg.MidiForceNextNote = v,
                0, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiPlayerReadyNote",
                _cfg.MidiPlayerReadyNote, v => _cfg.MidiPlayerReadyNote = v,
                0, 127, container, navGroup, ref idx);

            // ─── OSC / TCP Input ──────────────────────────────────────────
            SpawnHeader("OscTcpInput", container);

            AddToggle("PartyYARG.OscEnabled",
                _cfg.OscEnabled, v => _cfg.OscEnabled = v,
                container, navGroup, ref idx);

            AddInt("PartyYARG.OscPort",
                _cfg.OscPort, v => _cfg.OscPort = v,
                1024, 65535, container, navGroup, ref idx);

            AddToggle("PartyYARG.TcpEnabled",
                _cfg.TcpEnabled, v => _cfg.TcpEnabled = v,
                container, navGroup, ref idx);

            AddInt("PartyYARG.TcpPort",
                _cfg.TcpPort, v => _cfg.TcpPort = v,
                1024, 65535, container, navGroup, ref idx);

            // ─── OSC Output ───────────────────────────────────────────────
            SpawnHeader("OscOutput", container);

            AddToggle("PartyYARG.OscOutputEnabled",
                _cfg.OscOutputEnabled, v => _cfg.OscOutputEnabled = v,
                container, navGroup, ref idx);

            AddIP("PartyYARG.OscOutputHost",
                _cfg.OscOutputHost, v => _cfg.OscOutputHost = v,
                container, navGroup, ref idx);

            AddInt("PartyYARG.OscOutputPort",
                _cfg.OscOutputPort, v => _cfg.OscOutputPort = v,
                1024, 65535, container, navGroup, ref idx);

            // ─── MIDI Output ──────────────────────────────────────────────
            SpawnHeader("MidiOutput", container);

            AddToggle("PartyYARG.MidiOutputEnabled",
                _cfg.MidiOutputEnabled, v => _cfg.MidiOutputEnabled = v,
                container, navGroup, ref idx);

            AddMidiOutDeviceDropdown(container, navGroup, ref idx);

            AddInt("PartyYARG.MidiOutputChannel",
                _cfg.MidiOutputChannel, v => _cfg.MidiOutputChannel = v,
                1, 16, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteSongStart",
                _cfg.MidiNoteSongStart, v => _cfg.MidiNoteSongStart = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteSongReadyUp",
                _cfg.MidiNoteSongReadyUp, v => _cfg.MidiNoteSongReadyUp = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteSongEnd",
                _cfg.MidiNoteSongEnd, v => _cfg.MidiNoteSongEnd = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteShowStart",
                _cfg.MidiNoteShowStart, v => _cfg.MidiNoteShowStart = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteShowEnd",
                _cfg.MidiNoteShowEnd, v => _cfg.MidiNoteShowEnd = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNotePause",
                _cfg.MidiNotePause, v => _cfg.MidiNotePause = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteResume",
                _cfg.MidiNoteResume, v => _cfg.MidiNoteResume = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteBreakStart",
                _cfg.MidiNoteBreakStart, v => _cfg.MidiNoteBreakStart = v,
                -1, 127, container, navGroup, ref idx);

            AddInt("PartyYARG.MidiNoteSwapStart",
                _cfg.MidiNoteSwapStart, v => _cfg.MidiNoteSwapStart = v,
                -1, 127, container, navGroup, ref idx);

            // ─── Dev Keys ─────────────────────────────────────────────────
            SpawnHeader("PartyDevKeys", container);

            AddToggle("PartyYARG.EnableDevKeys",
                _cfg.EnableDevKeys, v => _cfg.EnableDevKeys = v,
                container, navGroup, ref idx);
        }

        // ── Visual helpers ────────────────────────────────────────────────

        private static void SpawnHeader(string headerName, Transform container)
        {
            if (_headerPrefab == null)
            {
                _headerPrefab = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/Header")
                    .WaitForCompletion();
            }

            var go = UnityEngine.Object.Instantiate(_headerPrefab, container);
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                Localize.Key("Settings.Header", headerName);
        }

        private void AddToggle(string name, bool value, Action<bool> setter,
            Transform container, NavigationGroup navGroup, ref int idx)
        {
            var setting = new ToggleSetting(value, v => { setter(v); _cfg?.Save(); });
            SpawnField(name, setting, container, navGroup, ref idx);
        }

        private void AddInt(string name, int value, Action<int> setter,
            int min, int max, Transform container, NavigationGroup navGroup, ref int idx)
        {
            var setting = new IntSetting(value, min, max, v => { setter(v); _cfg?.Save(); });
            SpawnField(name, setting, container, navGroup, ref idx);
        }

        private void AddIP(string name, string value, Action<string> setter,
            Transform container, NavigationGroup navGroup, ref int idx)
        {
            var setting = new IPv4Setting(value, v => { setter(v); _cfg?.Save(); });
            SpawnField(name, setting, container, navGroup, ref idx);
        }

        private void AddMidiInDeviceDropdown(Transform container, NavigationGroup navGroup, ref int idx)
        {
            var names = new List<string> { "" };
            names.AddRange(MidiInputController.GetDeviceNames());
            var setting = new DeviceDropdownSetting(_cfg.MidiDeviceName, names,
                v => { _cfg.MidiDeviceName = v; _cfg?.Save(); });
            SpawnField("PartyYARG.MidiDeviceName", setting, container, navGroup, ref idx);
        }

        private void AddMidiOutDeviceDropdown(Transform container, NavigationGroup navGroup, ref int idx)
        {
            var names = new List<string> { "" };
            names.AddRange(MidiOutputController.GetDeviceNames());
            var setting = new DeviceDropdownSetting(_cfg.MidiOutputDeviceName, names,
                v => { _cfg.MidiOutputDeviceName = v; _cfg?.Save(); });
            SpawnField("PartyYARG.MidiOutputDeviceName", setting, container, navGroup, ref idx);
        }

        private static void SpawnField(string unlocalizedName, ISettingType setting,
            Transform container, NavigationGroup navGroup, ref int idx)
        {
            var visual = SpawnSettingVisual(setting, container);
            visual.AssignPresetSetting(unlocalizedName, false, setting);
            visual.AssignIndex(idx++);
            navGroup.AddNavigatable(visual.gameObject);
        }

        // ── Nested: MIDI device dropdown ──────────────────────────────────

        /// <summary>
        /// A non-localizable string dropdown populated from WinMM device enumeration.
        /// The first entry is always "" (meaning "first available device").
        /// </summary>
        private sealed class DeviceDropdownSetting : DropdownSetting<string>
        {
            private readonly List<string> _deviceNames;

            public DeviceDropdownSetting(string currentValue, List<string> deviceNames,
                Action<string> onChange)
                : base(currentValue, onChange, localizable: false)
            {
                _deviceNames = deviceNames;

                // Base ctor calls UpdateValues() before _deviceNames is set — populate now.
                _possibleValues.Clear();
                foreach (var n in _deviceNames)
                    _possibleValues.Add(n);

                // If the current value isn't in the list, fall back to first-available.
                if (!_possibleValues.Contains(_value))
                    _value = "";
            }

            // Called by base ctor before _deviceNames is initialized — intentionally empty.
            public override void UpdateValues() { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Sends MIDI messages to a hardware or virtual MIDI output device.
    ///
    /// Windows: uses WinMM (winmm.dll) via P/Invoke (same approach as MidiInputController).
    /// Other platforms: no-op stub (logs a warning on Open).
    ///
    /// <see cref="SendNote"/> emits a Note On followed immediately by a Note Off (velocity 0),
    /// which is the standard one-shot trigger pattern used by DAWs and lighting consoles.
    /// </summary>
    public class MidiOutputController : IDisposable
    {
        private bool _disposed;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

        // ── WinMM P/Invoke declarations ───────────────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MIDIOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint   vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public ushort wTechnology;
            public ushort wVoices;
            public ushort wNotes;
            public ushort wChannelMask;
            public uint   dwSupport;
        }

        [DllImport("winmm.dll")] private static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint midiOutGetDevCapsW(uint deviceId, ref MIDIOUTCAPS caps, uint cbSize);
        [DllImport("winmm.dll")] private static extern uint midiOutOpen(out IntPtr handle, uint deviceId, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint midiOutShortMsg(IntPtr handle, uint msg);
        [DllImport("winmm.dll")] private static extern uint midiOutClose(IntPtr handle);

        // ── State ─────────────────────────────────────────────────────────

        private IntPtr _handle;

        public bool IsOpen => _handle != IntPtr.Zero;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Opens a MIDI output device. If <paramref name="preferredDeviceName"/> is non-empty,
        /// <summary>
        /// Returns the names of all currently connected MIDI output devices.
        /// Returns an empty list on non-Windows or if none are found.
        /// </summary>
        public static List<string> GetDeviceNames()
        {
            var names = new List<string>();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            uint count = midiOutGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                var caps = new MIDIOUTCAPS();
                if (midiOutGetDevCapsW(i, ref caps, (uint)Marshal.SizeOf<MIDIOUTCAPS>()) == 0)
                    names.Add(caps.szPname);
            }
#endif
            return names;
        }

        /// <summary>
        /// Opens a MIDI output device. If <paramref name="preferredDeviceName"/> is a
        /// partial name, the first device whose name contains it (case-insensitive) is used.
        /// Falls back to device 0 if no match is found.
        /// </summary>
        public void Open(string preferredDeviceName = "")
        {
            uint count = midiOutGetNumDevs();
            if (count == 0)
            {
                YargLogger.LogWarning("[PartyHero MIDI Out] No MIDI output devices found.");
                return;
            }

            uint targetId = 0;
            bool found    = false;

            if (!string.IsNullOrEmpty(preferredDeviceName))
            {
                for (uint i = 0; i < count; i++)
                {
                    var caps = new MIDIOUTCAPS();
                    if (midiOutGetDevCapsW(i, ref caps, (uint)Marshal.SizeOf<MIDIOUTCAPS>()) == 0)
                    {
                        if (caps.szPname.IndexOf(preferredDeviceName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetId = i;
                            found    = true;
                            break;
                        }
                    }
                }

                if (!found)
                    YargLogger.LogFormatWarning<string>("[PartyHero MIDI Out] Device \"{0}\" not found; using device 0.", preferredDeviceName);
            }

            uint result = midiOutOpen(out _handle, targetId, IntPtr.Zero, IntPtr.Zero, 0);
            if (result != 0)
            {
                YargLogger.LogFormatWarning<uint>("[PartyHero MIDI Out] midiOutOpen failed (error {0}).", result);
                _handle = IntPtr.Zero;
                return;
            }

            var openCaps = new MIDIOUTCAPS();
            midiOutGetDevCapsW(targetId, ref openCaps, (uint)Marshal.SizeOf<MIDIOUTCAPS>());
            YargLogger.LogFormatInfo<string>("[PartyHero MIDI Out] Opened: {0}", openCaps.szPname);
        }

        /// <summary>
        /// Sends a Note On (velocity <paramref name="velocity"/>) immediately followed by
        /// a Note Off (velocity 0) on the given 1-based MIDI channel.
        /// This is the standard one-shot trigger used by DAWs and lighting consoles.
        /// </summary>
        public void SendNote(int channel, int note, int velocity = 100)
        {
            if (_handle == IntPtr.Zero) return;

            int ch = Math.Clamp(channel - 1, 0, 15);  // to 0-based channel index
            int n  = Math.Clamp(note,     0, 127);
            int v  = Math.Clamp(velocity, 1, 127);     // Note On must have velocity >= 1

            // Pack as little-endian uint: byte0=status, byte1=note, byte2=velocity, byte3=unused
            uint noteOn  = (uint)((0x90 | ch) | (n << 8) | (v << 16));
            uint noteOff = (uint)((0x90 | ch) | (n << 8) | (0 << 16)); // vel 0 = Note Off

            midiOutShortMsg(_handle, noteOn);
            midiOutShortMsg(_handle, noteOff);
        }

        public void Close()
        {
            if (_handle == IntPtr.Zero) return;
            midiOutClose(_handle);
            _handle = IntPtr.Zero;
            YargLogger.LogInfo("[PartyHero MIDI Out] Device closed.");
        }

#else
        // ── Stub for non-Windows platforms ────────────────────────────────

        public bool IsOpen => false;

        public void Open(string preferredDeviceName = "") =>
            YargLogger.LogWarning("[PartyHero MIDI Out] MIDI output is only supported on Windows.");

        public void SendNote(int channel, int note, int velocity = 100) { }

        public void Close() { }
#endif

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Close();
        }
    }
}

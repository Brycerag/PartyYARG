using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Receives live MIDI input from a connected hardware MIDI device.
    ///
    /// Windows: uses WinMM (winmm.dll) via P/Invoke for zero-dependency live MIDI input.
    /// Other platforms: no-op stub (logs a warning on Open).
    ///
    /// WinMM callbacks run on an internal WinMM thread. Events are queued and must be
    /// dispatched on the main thread by calling <see cref="Tick"/> each frame.
    /// </summary>
    public class MidiInputController : IDisposable
    {
        /// <summary>Fired on the main thread when a MIDI Note On is received (note 0-127, velocity 1-127).</summary>
        public event Action<int, int> NoteOn;

        /// <summary>Fired on the main thread when a MIDI Note Off is received (note 0-127, velocity 0).</summary>
        public event Action<int, int> NoteOff;

        /// <summary>Fired on the main thread when a MIDI Control Change is received (CC number 0-127, value 0-127).</summary>
        public event Action<int, int> ControlChange;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

        // ── WinMM P/Invoke declarations ───────────────────────────────────

        private const uint CALLBACK_FUNCTION = 0x30000;
        private const uint MIM_DATA          = 0x3C3;  // Short MIDI message received

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance,
                                         IntPtr dwParam1, IntPtr dwParam2);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MIDIINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint   vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint   dwSupport;
        }

        [DllImport("winmm.dll")] private static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint midiInGetDevCapsW(uint deviceId, ref MIDIINCAPS caps, uint cbSize);
        [DllImport("winmm.dll")] private static extern uint midiInOpen(out IntPtr handle, uint deviceId, MidiInProc callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint midiInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInStop(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInClose(IntPtr handle);

        // ── State ─────────────────────────────────────────────────────────

        // Queued MIDI messages from the WinMM callback thread, processed on the main thread in Tick().
        private readonly ConcurrentQueue<(byte status, byte data1, byte data2)> _queue = new();

        private IntPtr     _deviceHandle   = IntPtr.Zero;
        private MidiInProc _callbackDelegate; // field keeps delegate alive from GC

        private bool _disposed;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Opens a MIDI input device and begins receiving messages.
        /// If <paramref name="preferredDeviceName"/> is empty, the first available device is used.
        /// </summary>
        public void Open(string preferredDeviceName = "")
        {
            uint count = midiInGetNumDevs();
            if (count == 0)
            {
                YargLogger.LogWarning("[PartyHero MIDI] No MIDI input devices found.");
                return;
            }

            int chosenId = 0; // default to first device
            for (uint i = 0; i < count; i++)
            {
                var caps = new MIDIINCAPS();
                midiInGetDevCapsW(i, ref caps, (uint)Marshal.SizeOf<MIDIINCAPS>());
                YargLogger.LogFormatInfo<uint, string>("[PartyHero MIDI] Device {0}: {1}", i, caps.szPname);

                if (!string.IsNullOrEmpty(preferredDeviceName) &&
                    caps.szPname.IndexOf(preferredDeviceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chosenId = (int)i;
                }
            }

            _callbackDelegate = WinMmCallback; // store to prevent GC collection
            uint result = midiInOpen(out _deviceHandle, (uint)chosenId, _callbackDelegate,
                                     IntPtr.Zero, CALLBACK_FUNCTION);
            if (result != 0)
            {
                YargLogger.LogFormatError("[PartyHero MIDI] midiInOpen failed with code 0x{0:X}.", result);
                _deviceHandle = IntPtr.Zero;
                return;
            }

            midiInStart(_deviceHandle);
            YargLogger.LogFormatInfo("[PartyHero MIDI] Opened device {0} successfully.", chosenId);
        }

        /// <summary>
        /// Call once per frame from the main thread to dispatch queued MIDI events.
        /// </summary>
        public void Tick()
        {
            while (_queue.TryDequeue(out var msg))
            {
                byte msgType = (byte)(msg.status & 0xF0);
                switch (msgType)
                {
                    case 0x90 when msg.data2 > 0: // Note On (velocity > 0)
                        NoteOn?.Invoke(msg.data1, msg.data2);
                        break;
                    case 0x80:                    // Note Off explicit
                    case 0x90 when msg.data2 == 0: // Note On with velocity 0 = Note Off
                        NoteOff?.Invoke(msg.data1, msg.data2);
                        break;
                    case 0xB0:                    // Control Change
                        ControlChange?.Invoke(msg.data1, msg.data2);
                        break;
                }
            }
        }

        public void Close()
        {
            if (_deviceHandle == IntPtr.Zero) return;
            midiInStop(_deviceHandle);
            midiInClose(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
            YargLogger.LogInfo("[PartyHero MIDI] Device closed.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Close();
        }

        // ── WinMM callback (runs on WinMM internal thread — NEVER call Unity APIs here) ──

        private void WinMmCallback(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance,
                                   IntPtr dwParam1, IntPtr dwParam2)
        {
            if (wMsg != MIM_DATA) return;

            // dwParam1 low-bytes: status | data1 | data2
            long raw    = dwParam1.ToInt64();
            byte status = (byte)(raw & 0xFF);
            byte data1  = (byte)((raw >> 8) & 0xFF);
            byte data2  = (byte)((raw >> 16) & 0xFF);

            _queue.Enqueue((status, data1, data2));
        }

#else
        // ── Non-Windows stub ──────────────────────────────────────────────

        public void Open(string preferredDeviceName = "") =>
            YargLogger.LogWarning("[PartyHero MIDI] Live MIDI input is only supported on Windows.");

        public void Tick() { }
        public void Close() { }
        public void Dispose() { }

#endif
    }
}

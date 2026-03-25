using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Listens for OSC (Open Sound Control) messages arriving as UDP packets on a configurable port.
    ///
    /// OSC is the protocol used by DAWs (Ableton, QLab, etc.) for network-based control messages.
    /// Implements a minimal subset of the OSC 1.0 spec sufficient for trigger messages:
    ///   - String address (e.g. "/partyhero/band_ready")
    ///   - Type tags: i (int32), f (float32), s (string), T (true), F (false)
    ///
    /// The UDP listener runs on a background thread. Events are queued and dispatched
    /// on the main thread when <see cref="Tick"/> is called each frame.
    /// </summary>
    public class OscReceiver : IDisposable
    {
        /// <summary>
        /// Fired on the main thread when a valid OSC packet is received.
        /// Parameters: address string, argument array (int/float/string/bool values).
        /// </summary>
        public event Action<string, object[]> MessageReceived;

        private readonly ConcurrentQueue<(string address, object[] args)> _queue = new();

        private UdpClient  _udpClient;
        private Thread     _receiveThread;
        private CancellationTokenSource _cts;

        private bool _disposed;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Starts the UDP listener on the given port.</summary>
        public void Start(int port)
        {
            try
            {
                _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                _cts       = new CancellationTokenSource();

                _receiveThread = new Thread(() => ReceiveLoop(_cts.Token))
                {
                    IsBackground = true,
                    Name         = "PartyHero OSC Receiver",
                };
                _receiveThread.Start();

                YargLogger.LogFormatInfo("[PartyHero OSC] Listening on UDP port {0}.", port);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero OSC] Failed to start receiver.");
            }
        }

        /// <summary>Call once per frame from the main thread to dispatch queued OSC messages.</summary>
        public void Tick()
        {
            while (_queue.TryDequeue(out var msg))
                MessageReceived?.Invoke(msg.address, msg.args);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _udpClient?.Close(); // unblocks Receive() on the background thread
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _udpClient?.Dispose();
        }

        // ── Background receive loop ───────────────────────────────────────

        private void ReceiveLoop(CancellationToken ct)
        {
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remoteEndpoint);
                    if (TryParseOsc(data, out string address, out object[] args))
                    {
                        _queue.Enqueue((address, args));
                    }
                }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "[PartyHero OSC] Error receiving packet.");
                }
            }
        }

        // ── OSC packet parser ─────────────────────────────────────────────
        // OSC 1.0 wire format:
        //   [address string, null-padded to 4-byte boundary]
        //   [type tag string starting with ',', null-padded to 4-byte boundary]
        //   [arguments, each 4 or 8 bytes, big-endian]

        private static bool TryParseOsc(byte[] data, out string address, out object[] args)
        {
            address = null;
            args    = Array.Empty<object>();

            if (data == null || data.Length < 8) return false;

            try
            {
                int pos = 0;
                address = ReadOscString(data, ref pos);
                if (pos >= data.Length) return true; // address only, no args

                string typeTag = ReadOscString(data, ref pos);
                if (typeTag.Length < 1 || typeTag[0] != ',') return true; // no type tag

                var argList = new System.Collections.Generic.List<object>(typeTag.Length - 1);
                for (int i = 1; i < typeTag.Length; i++)
                {
                    switch (typeTag[i])
                    {
                        case 'i':
                            argList.Add(ReadBigEndianInt32(data, ref pos));
                            break;
                        case 'f':
                            argList.Add(ReadBigEndianFloat32(data, ref pos));
                            break;
                        case 's':
                            argList.Add(ReadOscString(data, ref pos));
                            break;
                        case 'T':
                            argList.Add(true);
                            break;
                        case 'F':
                            argList.Add(false);
                            break;
                        // Skip unknown/unsupported types silently
                    }
                }

                args = argList.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a null-terminated ASCII string from <paramref name="data"/> starting at
        /// <paramref name="pos"/>, then advances pos to the next 4-byte boundary.
        /// </summary>
        private static string ReadOscString(byte[] data, ref int pos)
        {
            int start = pos;
            while (pos < data.Length && data[pos] != 0) pos++;
            string result = Encoding.ASCII.GetString(data, start, pos - start);
            // Advance past null(s) to the next 4-byte boundary:
            //   pos is now at the first null; (pos/4 + 1)*4 gives next boundary
            pos = (pos / 4 + 1) * 4;
            return result;
        }

        /// <summary>Reads a big-endian 32-bit integer and advances pos by 4.</summary>
        private static int ReadBigEndianInt32(byte[] data, ref int pos)
        {
            int value = (data[pos] << 24) | (data[pos + 1] << 16)
                      | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            return value;
        }

        /// <summary>Reads a big-endian IEEE-754 float and advances pos by 4.</summary>
        private static float ReadBigEndianFloat32(byte[] data, ref int pos)
        {
            // Reverse bytes from network (big-endian) order to host (little-endian) order
            byte[] le = { data[pos + 3], data[pos + 2], data[pos + 1], data[pos] };
            pos += 4;
            return BitConverter.ToSingle(le, 0);
        }
    }
}

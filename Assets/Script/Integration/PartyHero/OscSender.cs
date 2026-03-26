using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Sends OSC (Open Sound Control) messages as UDP packets to a remote host.
    ///
    /// Fire-and-forget — no threading or queuing. Safe to call from the Unity main thread.
    ///
    /// Supports int, float, string, and bool (T/F) argument types.
    /// Automatically encodes packets per the OSC 1.0 spec:
    ///   address string (null-padded to 4-byte boundary)
    ///   type tag string starting with ',' (null-padded to 4-byte boundary)
    ///   arguments (int32/float32 big-endian; strings null-padded to 4-byte boundary)
    /// </summary>
    public class OscSender : IDisposable
    {
        private UdpClient  _udpClient;
        private IPEndPoint _endpoint;
        private bool       _ready;
        private bool       _disposed;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Opens the UDP socket and resolves the destination.
        /// Call once before sending.
        /// </summary>
        public void Start(string host, int port)
        {
            try
            {
                _udpClient = new UdpClient();
                _endpoint  = new IPEndPoint(ResolveHost(host), port);
                _ready     = true;
                YargLogger.LogFormatInfo<string, int>("[PartyHero OSC Out] Sending to {0}:{1}.", host, port);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero OSC Out] Failed to open sender.");
            }
        }

        /// <summary>
        /// Sends an OSC message with zero or more arguments.
        /// Supported argument types: <see cref="int"/>, <see cref="float"/>, <see cref="string"/>, <see cref="bool"/>.
        /// </summary>
        public void Send(string address, params object[] args)
        {
            if (!_ready) return;
            try
            {
                byte[] packet = BuildPacket(address, args);
                _udpClient.Send(packet, packet.Length, _endpoint);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero OSC Out] Send failed.");
            }
        }

        public void Stop()
        {
            _ready = false;
            _udpClient?.Close();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _udpClient?.Dispose();
        }

        // ── OSC packet builder ────────────────────────────────────────────

        private static byte[] BuildPacket(string address, object[] args)
        {
            // Build type tag string
            var tag = new StringBuilder(",");
            foreach (var arg in args)
            {
                tag.Append(arg switch
                {
                    int    => 'i',
                    float  => 'f',
                    double => 'f',
                    bool b => b ? 'T' : 'F',
                    _      => 's',   // string and everything else
                });
            }

            byte[] addrBytes = Pad(address);
            byte[] tagBytes  = Pad(tag.ToString());

            // Pre-calculate argument bytes
            int argLen = 0;
            foreach (var arg in args)
            {
                argLen += arg switch
                {
                    int    => 4,
                    float  => 4,
                    double => 4,
                    bool   => 0,  // T/F carry no data bytes in the packet
                    string s => PaddedLen(s.Length),
                    _        => PaddedLen(arg?.ToString()?.Length ?? 0),
                };
            }

            var packet = new byte[addrBytes.Length + tagBytes.Length + argLen];
            int pos    = 0;

            Buffer.BlockCopy(addrBytes, 0, packet, pos, addrBytes.Length);
            pos += addrBytes.Length;

            Buffer.BlockCopy(tagBytes, 0, packet, pos, tagBytes.Length);
            pos += tagBytes.Length;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case int i:
                        WriteInt32BE(packet, pos, i);
                        pos += 4;
                        break;

                    case float f:
                        WriteFloat32BE(packet, pos, f);
                        pos += 4;
                        break;

                    case double d:
                        WriteFloat32BE(packet, pos, (float)d);
                        pos += 4;
                        break;

                    case bool:
                        // T/F carry no data bytes
                        break;

                    default:
                        string s    = arg?.ToString() ?? "";
                        byte[] sBytes = Pad(s);
                        Buffer.BlockCopy(sBytes, 0, packet, pos, sBytes.Length);
                        pos += sBytes.Length;
                        break;
                }
            }

            return packet;
        }

        // Returns a null-terminated string padded to the next 4-byte boundary.
        private static byte[] Pad(string s)
        {
            byte[] raw = Encoding.UTF8.GetBytes(s);
            var result = new byte[PaddedLen(raw.Length)];
            Buffer.BlockCopy(raw, 0, result, 0, raw.Length);
            // remaining bytes are zero (null terminator + padding)
            return result;
        }

        // Size of a null-terminated string rounded up to the next 4-byte boundary.
        // Formula: ((len + 1) + 3) & ~3  =  (len + 4) & ~3
        private static int PaddedLen(int strLen) => (strLen + 4) & ~3;

        private static void WriteInt32BE(byte[] buf, int offset, int value)
        {
            buf[offset + 0] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >>  8);
            buf[offset + 3] = (byte)(value);
        }

        private static void WriteFloat32BE(byte[] buf, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, buf, offset, 4);
        }

        private static IPAddress ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out var ip))
                return ip;

            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length > 0)
                return addresses[0];

            throw new Exception($"Cannot resolve host: {host}");
        }
    }
}

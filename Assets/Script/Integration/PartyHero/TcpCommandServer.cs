using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// TCP server that accepts newline-delimited text commands from a DAW or show controller.
    ///
    /// Supported commands (case-insensitive keyword, original case preserved for arguments):
    ///   BAND_READY          — the live band is ready to start
    ///   PLAYER_READY        — toggle a player's ready state
    ///   FORCE_STATE {name}  — force the show into a named state
    ///
    /// One server thread accepts incoming connections; a separate thread is spawned per client.
    /// All commands are queued and dispatched on the main thread via <see cref="Tick"/>.
    /// </summary>
    public class TcpCommandServer : IDisposable
    {
        /// <summary>
        /// Fired on the main thread when a complete command line is received from any client.
        /// The full trimmed line is passed; use <see cref="TryParseCommand"/> to decode it.
        /// </summary>
        public event Action<string> CommandReceived;

        private readonly ConcurrentQueue<string> _queue = new();

        private TcpListener            _listener;
        private Thread                 _acceptThread;
        private CancellationTokenSource _cts;

        private bool _disposed;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Starts the TCP listener on the given port.</summary>
        public void Start(int port)
        {
            try
            {
                _cts      = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();

                _acceptThread = new Thread(() => AcceptLoop(_cts.Token))
                {
                    IsBackground = true,
                    Name         = "PartyHero TCP Accept",
                };
                _acceptThread.Start();

                YargLogger.LogFormatInfo("[PartyHero TCP] Listening on port {0}.", port);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero TCP] Failed to start server.");
            }
        }

        /// <summary>Call once per frame from the main thread to dispatch queued commands.</summary>
        public void Tick()
        {
            while (_queue.TryDequeue(out string line))
                CommandReceived?.Invoke(line);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop(); // unblocks AcceptTcpClient() on the accept thread
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        // ── Parsing helper ────────────────────────────────────────────────

        public static bool TryParseCommand(string line, out string keyword, out string argument)
        {
            keyword  = null;
            argument = null;

            if (string.IsNullOrWhiteSpace(line)) return false;

            int spaceIndex = line.IndexOf(' ');
            if (spaceIndex < 0)
            {
                keyword = line.Trim().ToUpperInvariant();
                return true;
            }

            keyword  = line.Substring(0, spaceIndex).Trim().ToUpperInvariant();
            argument = line.Substring(spaceIndex + 1).Trim(); // preserve original case
            return true;
        }

        // ── Background accept loop ────────────────────────────────────────

        private void AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    // Spawn a reader thread per client (expected to be 1–2 total)
                    var t = new Thread(() => ReadClient(client, ct))
                    {
                        IsBackground = true,
                        Name         = "PartyHero TCP Client",
                    };
                    t.Start();
                }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        YargLogger.LogException(ex, "[PartyHero TCP] Accept error.");
                }
            }
        }

        private void ReadClient(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var reader = new StreamReader(client.GetStream()))
                {
                    string line;
                    while (!ct.IsCancellationRequested && (line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrEmpty(line))
                            _queue.Enqueue(line);
                    }
                }
            }
            catch (IOException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[PartyHero TCP] Client read error.");
            }
        }
    }
}

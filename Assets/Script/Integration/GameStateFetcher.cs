using System;
using YARG.Core.Song;

namespace YARG.Integration
{
    public static class GameStateFetcher
    {
        public struct State
        {
            public SceneIndex CurrentScene;
            public SongEntry SongEntry;
            public bool Paused;
        }

        public static event Action<State> GameStateChange;

        private static State _current;

        public static void SetSceneIndex(SceneIndex scene)
        {
            _current = new State
            {
                CurrentScene = scene,
                SongEntry = null,
                Paused = false
            };

            GameStateChange?.Invoke(_current);
        }

        public static void SetSongEntry(SongEntry metadata)
        {
            _current.SongEntry = metadata;

            GameStateChange?.Invoke(_current);
        }

        public static void SetPaused(bool paused)
        {
            _current.Paused = paused;

            GameStateChange?.Invoke(_current);
        }

        /// <summary>
        /// Called by <see cref="YARG.Gameplay.GameManager"/> the instant the highway
        /// starts scrolling and the SongRunner's clock begins (t = 0).  This is the
        /// correct moment to trigger DAW transport sync — it fires well after the scene
        /// has loaded, after all audio has been buffered, and calibration has been applied.
        /// </summary>
        public static event Action SongStarted;

        public static void SetSongStarted()
        {
            SongStarted?.Invoke();
        }

        /// <summary>
        /// Called when the DAW (Ableton) responds to a <see cref="SetSongStarted"/> trigger
        /// with its current transport position in seconds.  <see cref="YARG.Gameplay.GameManager"/>
        /// subscribes here and seeks itself to the received position via
        /// <c>GameManager.SetSongTime(time, 0.0)</c>, aligning the highway to the DAW
        /// immediately after the DAW acknowledges start.
        ///
        /// OSC contract (inbound from DAW):
        ///   address: configurable (default "/partyyarg/sync/time")
        ///   args[0]: float — DAW transport position in seconds
        /// </summary>
        public static event Action<double> SyncTimeReceived;

        /// <summary>
        /// Fire from the main thread only (called by <see cref="YARG.Integration.PartyHero.PartyHeroController"/>
        /// after the OSC tick dequeues the DAW's sync reply).
        /// </summary>
        public static void SetSyncTime(double seconds)
        {
            SyncTimeReceived?.Invoke(seconds);
        }
    }
}
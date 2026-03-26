using System;
using System.Linq;
using UnityEngine;
using YARG;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Manages the runtime state of a live PartyYARG show, driving scene transitions
    /// through the ordered sequence of songs, swaps, breaks, and show-end markers.
    /// Lives on the PersistentScene alongside <see cref="PartyHeroController"/>.
    /// </summary>
    public class SetlistManager : MonoSingleton<SetlistManager>
    {
        public ShowSetlist ActiveSetlist { get; private set; }
        private int _entryIndex = -1;

        /// <summary>True when a setlist has been activated and the show is running.</summary>
        public bool IsActive => ActiveSetlist != null;

        /// <summary>The entry currently being shown (ready-up, swap screen, break, etc.).</summary>
        public ShowEntry CurrentEntry =>
            IsActive && _entryIndex >= 0 && _entryIndex < ActiveSetlist.Entries.Count
                ? ActiveSetlist.Entries[_entryIndex]
                : null;

        /// <summary>The entry that follows the current one, or null if at the end of the show.</summary>
        public ShowEntry PeekNextEntry
        {
            get
            {
                int next = _entryIndex + 1;
                return IsActive && next < ActiveSetlist.Entries.Count
                    ? ActiveSetlist.Entries[next]
                    : null;
            }
        }

        // ── Static show-lifecycle events ──────────────────────────────────────

        /// <summary>Fired when a setlist is activated via <see cref="ActivateSetlist"/>.</summary>
        public static event Action<ShowSetlist> OnShowActivated;

        /// <summary>Fired when the show reaches its end (last entry played or ShowEnd entry reached).</summary>
        public static event Action OnShowEnded;

        protected override void SingletonAwake() { }
        protected override void SingletonDestroy() { }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the active setlist and prepares <see cref="GlobalVariables.State"/> so that
        /// legacy show-flow code (DifficultySelect, ScoreScreen) continues to work.
        /// Call this before loading the first song's DifficultySelect screen.
        /// </summary>
        public void ActivateSetlist(ShowSetlist setlist)
        {
            ActiveSetlist = setlist;
            _entryIndex = -1;

            // Keep ShowSongs in sync for any legacy code that still reads it directly.
            GlobalVariables.State.ShowSongs = setlist.Entries
                .Where(e => e.Type == ShowEntryType.Song && e.Song != null)
                .Select(e => e.Song)
                .ToList();

            GlobalVariables.State.PlayingAShow = true;
            GlobalVariables.State.ShowIndex = -1;

            YargLogger.LogFormatInfo<string, int>(
                "[PartyHero Setlist] Show activated: \"{0}\" — {1} total entries.",
                setlist.Name, setlist.Entries.Count);

            OnShowActivated?.Invoke(setlist);
        }

        /// <summary>
        /// Advances to the next entry in the setlist, updates <see cref="GlobalVariables.State"/>,
        /// and returns the <see cref="SceneIndex"/> that should be loaded next.
        /// </summary>
        public SceneIndex Advance()
        {
            if (!IsActive)
            {
                YargLogger.LogWarning("[PartyHero Setlist] Advance() called but no setlist is active — returning Menu.");
                return SceneIndex.Menu;
            }

            _entryIndex++;

            if (_entryIndex >= ActiveSetlist.Entries.Count)
                return EndShow();

            var entry = ActiveSetlist.Entries[_entryIndex];
            switch (entry.Type)
            {
                case ShowEntryType.Song:
                    GlobalVariables.State.CurrentSong = entry.Song;
                    // Keep ShowIndex synced to the song-only position within ShowSongs.
                    GlobalVariables.State.ShowIndex = ActiveSetlist.Entries
                        .Take(_entryIndex + 1)
                        .Count(e => e.Type == ShowEntryType.Song) - 1;
                    YargLogger.LogFormatInfo<int>(
                        "[PartyHero Setlist] Advancing to song at entry index {0}.", _entryIndex);
                    return SceneIndex.ReadyUp;

                case ShowEntryType.Swap:
                    YargLogger.LogInfo("[PartyHero Setlist] Advancing to player swap interstitial.");
                    return SceneIndex.WaitingForSwap;

                case ShowEntryType.Break:
                    YargLogger.LogInfo("[PartyHero Setlist] Advancing to set break.");
                    return SceneIndex.SetBreak;

                case ShowEntryType.ShowEnd:
                    return EndShow();

                default:
                    return SceneIndex.Menu;
            }
        }

        /// <summary>
        /// Clears the active setlist without triggering show-end logic.
        /// Use when the operator ends the show early.
        /// </summary>
        public void Reset()
        {
            ActiveSetlist = null;
            _entryIndex = -1;
        }

        // ─────────────────────────────────────────────────────────────────────

        private SceneIndex EndShow()
        {
            YargLogger.LogInfo("[PartyHero Setlist] Show complete — returning to ShowEnd screen.");
            GlobalVariables.State.PlayingAShow = false;
            ActiveSetlist = null;
            OnShowEnded?.Invoke();
            return SceneIndex.ShowEnd;
        }
    }
}

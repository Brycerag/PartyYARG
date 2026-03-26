using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Pre-show sanity check that validates a <see cref="ShowSetlist"/> against the
    /// currently loaded song library before the operator activates the show.
    ///
    /// Call <see cref="Verify"/> before <see cref="SetlistManager.ActivateSetlist"/>.
    /// If the result is not <see cref="VerifyResult.Ok"/> the operator should be warned
    /// (or the activate should be blocked depending on severity).
    ///
    /// Example usage:
    /// <code>
    ///   var result = SetlistVerifier.Verify(setlist);
    ///   if (!result.IsOk)
    ///       Debug.LogWarning(result.FormatReport());
    /// </code>
    /// </summary>
    public static class SetlistVerifier
    {
        /// <summary>Severity of a single verification issue.</summary>
        public enum IssueSeverity
        {
            /// <summary>Show cannot run without this being resolved.</summary>
            Error,

            /// <summary>Show can run but something may behave unexpectedly.</summary>
            Warning,
        }

        /// <summary>A single issue found during verification.</summary>
        public readonly struct Issue
        {
            public readonly IssueSeverity Severity;

            /// <summary>
            /// Zero-based index of the setlist entry that has the issue,
            /// or -1 if the issue applies to the setlist as a whole.
            /// </summary>
            public readonly int EntryIndex;
            public readonly string Message;

            public Issue(IssueSeverity severity, int entryIndex, string message)
            {
                Severity   = severity;
                EntryIndex = entryIndex;
                Message    = message;
            }
        }

        /// <summary>Result of a <see cref="Verify"/> call.</summary>
        public readonly struct VerifyResult
        {
            /// <summary>All issues found. Empty means the setlist is clean.</summary>
            public readonly IReadOnlyList<Issue> Issues;

            /// <summary>True when there are no Error-severity issues (warnings are allowed).</summary>
            public bool IsOk
            {
                get
                {
                    foreach (var issue in Issues)
                        if (issue.Severity == IssueSeverity.Error) return false;
                    return true;
                }
            }

            public bool HasWarnings
            {
                get
                {
                    foreach (var issue in Issues)
                        if (issue.Severity == IssueSeverity.Warning) return true;
                    return false;
                }
            }

            public VerifyResult(IReadOnlyList<Issue> issues)
            {
                Issues = issues;
            }

            /// <summary>
            /// Produces a human-readable summary of all issues suitable for a dialog
            /// or log output.
            /// </summary>
            public string FormatReport()
            {
                if (Issues.Count == 0)
                    return "Setlist verification passed with no issues.";

                var sb = new StringBuilder();
                sb.AppendLine($"Setlist verification found {Issues.Count} issue(s):");

                foreach (var issue in Issues)
                {
                    string location = issue.EntryIndex >= 0
                        ? $"Entry {issue.EntryIndex + 1}"
                        : "Setlist";
                    sb.AppendLine($"  [{issue.Severity.ToString().ToUpperInvariant()}] {location}: {issue.Message}");
                }

                return sb.ToString().TrimEnd();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Validates the setlist and returns a <see cref="VerifyResult"/> describing any
        /// issues found.  Must be called on the main thread (reads <see cref="SongContainer"/>).
        /// </summary>
        public static VerifyResult Verify(ShowSetlist setlist)
        {
            var issues = new List<Issue>();

            if (setlist == null)
            {
                issues.Add(new Issue(IssueSeverity.Error, -1, "Setlist is null."));
                return new VerifyResult(issues);
            }

            if (string.IsNullOrWhiteSpace(setlist.Name))
                issues.Add(new Issue(IssueSeverity.Warning, -1, "Setlist has no name."));

            if (setlist.Entries == null || setlist.Entries.Count == 0)
            {
                issues.Add(new Issue(IssueSeverity.Error, -1, "Setlist has no entries."));
                return new VerifyResult(issues);
            }

            bool hasSong = false;

            for (int i = 0; i < setlist.Entries.Count; i++)
            {
                var entry = setlist.Entries[i];

                switch (entry.Type)
                {
                    case ShowEntryType.Song:
                        VerifySongEntry(entry, i, issues);
                        if (entry.Song != null) hasSong = true;
                        break;

                    case ShowEntryType.Break:
                        if (string.IsNullOrWhiteSpace(entry.Label))
                            issues.Add(new Issue(IssueSeverity.Warning, i,
                                "Break entry has no label — break screen will show empty title."));
                        break;

                    case ShowEntryType.Swap:
                        if (string.IsNullOrWhiteSpace(entry.Label))
                            issues.Add(new Issue(IssueSeverity.Warning, i,
                                "Swap entry has no label — swap screen will show empty title."));
                        break;

                    case ShowEntryType.ShowEnd:
                        if (i < setlist.Entries.Count - 1)
                            issues.Add(new Issue(IssueSeverity.Warning, i,
                                "ShowEnd entry is not the last entry — entries after it will never be reached."));
                        break;
                }
            }

            if (!hasSong)
                issues.Add(new Issue(IssueSeverity.Error, -1,
                    "Setlist contains no resolvable song entries — nothing will play."));

            // Log summary to the Unity console for easy operator visibility.
            var result = new VerifyResult(issues);
            if (result.IsOk && !result.HasWarnings)
                YargLogger.LogFormatInfo("[PartyHero Verifier] Setlist \"{0}\" — OK ({1} entries).",
                    setlist.Name, setlist.Entries.Count);
            else
                YargLogger.LogFormatWarning("[PartyHero Verifier] {0}", result.FormatReport());

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static void VerifySongEntry(ShowEntry entry, int index, List<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(entry.Hash))
            {
                issues.Add(new Issue(IssueSeverity.Error, index,
                    "Song entry has no hash — cannot resolve to a song."));
                return;
            }

            var hash = HashWrapper.FromString(entry.Hash.AsSpan());

            if (!SongContainer.SongsByHash.TryGetValue(hash, out var songs) || songs.Count == 0)
            {
                issues.Add(new Issue(IssueSeverity.Error, index,
                    $"Song hash \"{entry.Hash}\" not found in the loaded library. " +
                    $"(Label: \"{entry.Label ?? "<none>"}\") " +
                    "Scan your song folders and restart, or remove this entry."));
                return;
            }

            // Song resolved — attach it to the entry for use by SetlistManager.
            entry.Song = songs[0];
        }
    }
}

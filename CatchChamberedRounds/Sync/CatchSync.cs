using System;
using System.Collections.Generic;

namespace Tosox.CatchChamberedRounds.Sync
{
    internal static class CatchSync
    {
        // The round each player announced a catch for, keyed by profile id
        private static readonly Dictionary<string, string> Announcements = new Dictionary<string, string>();

        internal static Action<string, string> CatchAnnouncer { get; set; }

        internal static Action<string, string> AnimationCue { get; set; }

        /// <summary>
        /// Sends word that a reload was cut short by putting the weapon away, which Fika itself only
        /// reports when the trigger did it. Assigned by the Fika sync, and left null without Fika.
        /// </summary>
        internal static Action<string, bool> ReloadAbortAnnouncer { get; set; }

        internal static bool Incompatible { get; set; }

        internal static void SetAnnouncement(string profileId, string roundId)
        {
            if (!string.IsNullOrEmpty(profileId))
                Announcements[profileId] = roundId;
        }

        internal static bool TakeAnnouncement(string profileId, string roundId)
        {
            if (string.IsNullOrEmpty(profileId) || !Announcements.TryGetValue(profileId, out var announced))
                return false;

            Announcements.Remove(profileId);
            return !string.IsNullOrEmpty(roundId) && announced == roundId;
        }

        internal static void Clear()
        {
            Announcements.Clear();
            Incompatible = false;
        }
    }
}

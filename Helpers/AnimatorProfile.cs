using AnimationEventSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// What a weapon's animator says about itself, read out of the baked animation event table it
    /// carries rather than assumed from state names.
    ///
    /// Every state's events, and the normalized time each one fires at, are exactly the numbers
    /// needed to decide where one animation can be cut and joined to another. Reading them means
    /// weapons that call the same states different things all work, and so do modded weapons whose
    /// state names appear in no table anywhere.
    /// </summary>
    internal class AnimatorProfile
    {
        private const string BoltCloseEvent = "OffBoltCatch";
        private const string FeedRoundEvent = "AddAmmoInMag";

        /// <summary>
        /// Events and their normalized times, keyed by both the full path hash and the short name
        /// hash so a state can be found from an <see cref="AnimatorStateInfo"/> either way.
        /// </summary>
        private readonly Dictionary<int, Dictionary<string, float>> _states =
            new Dictionary<int, Dictionary<string, float>>();

        /// <summary>The state that feeds rounds into the magazine, or zero when there is no single one.</summary>
        internal int FeedState { get; private set; }

        /// <summary>Why there is no feed state, for the log. Null when there is one.</summary>
        internal string FeedStateProblem { get; private set; }

        /// <summary>
        /// Reads the table off an animator. Returns null when there is nothing to read, which is
        /// every caller's cue to leave the weapon alone rather than guess at it.
        /// </summary>
        internal static AnimatorProfile Read(Animator animator)
        {
            if (animator == null)
                return null;

            var behaviours = animator.GetBehaviours<AnimationEventsStateBehaviour>();
            if (behaviours == null || behaviours.Length == 0)
                return null;

            var profile = new AnimatorProfile();
            var feedCandidates = 0;

            foreach (var behaviour in behaviours)
            {
                // A state with no events carries EventsListId -1, and asking for its list makes EFT
                // log an error of its own, so those are skipped rather than queried
                if (behaviour == null || behaviour.EventsData == null || behaviour.EventsListId < 0)
                    continue;

                var events = behaviour.AnimationEvents;
                if (events == null || events.Count == 0)
                    continue;

                var times = new Dictionary<string, float>();
                foreach (var animationEvent in events)
                {
                    if (animationEvent == null || !animationEvent.Enabled)
                        continue;

                    // Keep the earliest, since a state can raise the same event more than once
                    if (!times.TryGetValue(animationEvent.FunctionName, out var seen) || animationEvent.Time < seen)
                        times[animationEvent.FunctionName] = animationEvent.Time;
                }

                var shortHash = ShortNameHashOf(behaviour.FullName);

                profile._states[behaviour.FullNameHash] = times;
                if (shortHash != 0)
                    profile._states[shortHash] = times;

                if (!times.ContainsKey(FeedRoundEvent))
                    continue;

                feedCandidates++;
                profile.FeedState = shortHash != 0 ? shortHash : behaviour.FullNameHash;
            }

            if (feedCandidates != 1)
            {
                // More than one feeding state means the rule no longer identifies a single answer,
                // and guessing between them is worse than playing both animations
                profile.FeedStateProblem = feedCandidates == 0
                    ? "no state feeds rounds into the magazine"
                    : $"{feedCandidates} states feed rounds into the magazine";

                profile.FeedState = 0;
            }

            return profile;
        }

        /// <summary>
        /// The moment the state currently playing would close the bolt. A state with no such event
        /// is not one to cut away from, which is how the semi-automatics opt themselves out: their
        /// catch never works a bolt, so there is no close to skip.
        /// </summary>
        internal bool TryGetBoltCloseTime(AnimatorStateInfo state, out float time)
        {
            time = 0f;

            if (!_states.TryGetValue(state.fullPathHash, out var events) &&
                !_states.TryGetValue(state.shortNameHash, out events))
                return false;

            return events.TryGetValue(BoltCloseEvent, out time);
        }

        /// <summary>
        /// A behaviour names its state by full path, "Hands.RELOAD IN". The animator matches a
        /// crossfade against the short name, so the layer prefix is dropped before hashing.
        /// </summary>
        private static int ShortNameHashOf(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return 0;

            var dot = fullName.LastIndexOf('.');
            var shortName = dot >= 0 && dot < fullName.Length - 1 ? fullName.Substring(dot + 1) : fullName;

            return Animator.StringToHash(shortName);
        }
    }
}

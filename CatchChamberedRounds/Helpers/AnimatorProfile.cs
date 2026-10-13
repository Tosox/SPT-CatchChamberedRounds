using AnimationEventSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal class AnimatorProfile
    {
        private const string BoltCloseEvent = "OffBoltCatch";
        private const string FeedRoundEvent = "AddAmmoInMag";

        // Events and their normalized times, keyed by both the full path hash and the short name hash
        private readonly Dictionary<int, Dictionary<string, float>> _states =
            new Dictionary<int, Dictionary<string, float>>();

        // The state that feeds rounds into the magazine or zero when there is no single one
        internal int FeedState { get; private set; }

        internal string FeedStateProblem { get; private set; }

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
                // A state with no events carries EventsListId -1 and asking for its list makes EFT
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
                // More than one feeding state means the rule no longer identifies a single answer
                // and guessing between them is worse than playing both animations
                profile.FeedStateProblem = feedCandidates == 0
                    ? "no state feeds rounds into the magazine"
                    : $"{feedCandidates} states feed rounds into the magazine";

                profile.FeedState = 0;
            }

            return profile;
        }

        internal bool TryGetBoltCloseTime(AnimatorStateInfo state, out float time)
        {
            time = 0f;

            if (!_states.TryGetValue(state.fullPathHash, out var events) &&
                !_states.TryGetValue(state.shortNameHash, out events))
                return false;

            return events.TryGetValue(BoltCloseEvent, out time);
        }

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

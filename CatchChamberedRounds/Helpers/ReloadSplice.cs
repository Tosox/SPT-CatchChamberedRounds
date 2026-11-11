using AnimationEventSystem;
using System.Collections;
using Tosox.CatchChamberedRounds.Patches;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    // Skips the reload's opening and blends straight into feeding rounds, so the catch flows into the reload
    internal static class ReloadSplice
    {
        private const float BlendDuration = 0.10f;
        private const int MaxFlushFrames = 12;

        private static FirearmsAnimator _animator;
        private static AnimatorProfile _profile;
        private static int _leavingState;

        // The splice is what makes the leaving state's events stale, so it mutes them too
        internal static void Arm(FirearmsAnimator animator, AnimatorProfile profile,
            AnimationEventsEmitter emitter, int leavingState)
        {
            _animator = animator;
            _profile = profile;
            _leavingState = leavingState;

            SuppressStaleEventsPatch.SuppressFrom(emitter, leavingState);
        }

        internal static void Clear()
        {
            Disarm();
            SuppressStaleEventsPatch.Clear();
        }

        internal static bool Consume(FirearmsAnimator animator)
        {
            if (_animator == null || !ReferenceEquals(animator, _animator))
                return false;

            var profile = _profile;
            var leavingState = _leavingState;

            // Disarm, not Clear, the event suppression has to outlive this call
            Disarm();

            var unity = animator.UnityAnimator();
            if (unity == null || profile == null || profile.FeedState == 0)
            {
                SuppressStaleEventsPatch.Clear();
                return false;
            }

            unity.CrossFade(profile.FeedState, BlendDuration, FirearmsAnimator.HANDS_LAYER_INDEX, 0f);
            Plugin.Instance.StartCoroutine(HoldBoltOpen(animator, unity, leavingState));
            return true;
        }

        private static void Disarm()
        {
            _animator = null;
            _profile = null;
            _leavingState = 0;
        }

        // The leaving state fires its remaining events in a burst when the transition starts, queued
        // until late update, so this waits on the state rather than a frame count
        private static IEnumerator HoldBoltOpen(FirearmsAnimator animator, Animator unity, int leavingState)
        {
            for (var frame = 0; frame < MaxFlushFrames; frame++)
            {
                animator.SetBoltCatch(true);

                yield return null;

                if (unity.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX).shortNameHash != leavingState)
                    break;
            }

            SuppressStaleEventsPatch.Clear();
        }
    }
}

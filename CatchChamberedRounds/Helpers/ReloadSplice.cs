using AnimationEventSystem;
using System.Collections;
using Tosox.CatchChamberedRounds.Patches;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    // Swallows the reload's own opening and blends into the state that feeds rounds instead, so the
    // catch runs straight into the reload
    internal static class ReloadSplice
    {
        private const float BlendDuration = 0.10f;
        private const int MaxFlushFrames = 12;

        private static FirearmsAnimator _animator;
        private static AnimatorProfile _profile;
        private static int _leavingState;

        // The splice is the only reason the leaving state's events are stale, so it silences them too
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

            // Taken rather than cleared, because the suppression has to outlive this call
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

        // The leaving state dumps its remaining events in one burst as the transition starts, and the
        // emitter queues them until the next late update, so this waits on the state, not on frames
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

using AnimationEventSystem;
using System.Collections;
using Tosox.CatchChamberedRounds.Patches;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// Takes the reload animation over from the catch, by swallowing the reload's own opening and
    /// blending into the state that feeds rounds instead. Armed before the reload starts and consumed
    /// by the animation it replaces, so it works just as well for a reload this client did not start.
    /// </summary>
    internal static class ReloadSplice
    {
        private const float BlendDuration = 0.10f;
        private const int MaxFlushFrames = 12;

        private static FirearmsAnimator _animator;
        private static AnimatorProfile _profile;
        private static int _leavingState;

        /// <summary>
        /// Silencing the state being left belongs to the splice, because the splice is the only
        /// reason its events are stale and the hold is the only thing that knows when they stop.
        /// </summary>
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

        /// <summary>
        /// Whether this animator's reload animation belongs to a splice and has been replaced by one.
        /// </summary>
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

        // The state being left fires every event it has left in one burst as soon as the animator
        // enters the transition, and the weapon emitter queues those until the next late update, so
        // the hold outlives the transition rather than a frame count that assumes a frame rate
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

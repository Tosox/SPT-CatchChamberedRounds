using AnimationSystem;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class AnimatorExtensions
    {
        internal static Animator UnityAnimator(this FirearmsAnimator animator)
        {
            return (animator?.Animator as UnityAnimatorWrapper)?.Animator;
        }
    }
}

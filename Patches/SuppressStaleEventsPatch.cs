using AnimationEventSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Drops the animation events of a state the splice has just cut away from.
    ///
    /// Leaving a state by crossfade makes EFT fire every event it had not reached yet, all at once:
    /// <c>AnimationEventsContainer.CheckEvents</c> sees the transition and runs the remainder of the
    /// clip's timeline up to 0.99999. That is deliberate on BSG's part, since a state normally has
    /// bookkeeping it must not be robbed of, but it is wrong for a section of animation that is
    /// being skipped on purpose. The bolt never closes on screen and you hear it close anyway.
    ///
    /// So the events of the abandoned state are dropped for the moment it takes the flush to arrive.
    /// Nothing is lost by it: the discharge's own tail is exactly the part being skipped, and what
    /// the operation needed from it has already been done by hand.
    /// </summary>
    public class SuppressStaleEventsPatch : ModulePatch
    {
        private static AnimationEventsEmitter _emitter;
        private static int _stateShortNameHash;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(AnimationEventsEmitter),
                nameof(AnimationEventsEmitter.SendEvent)
            );
        }

        /// <summary>
        /// Starts dropping events that come from <paramref name="stateShortNameHash"/> on the given
        /// emitter. Scoped to one state on one weapon so nothing else in the raid is affected.
        /// </summary>
        internal static void SuppressFrom(AnimationEventsEmitter emitter, int stateShortNameHash)
        {
            _emitter = emitter;
            _stateShortNameHash = stateShortNameHash;
        }

        internal static void Clear()
        {
            _emitter = null;
            _stateShortNameHash = 0;
        }

        [PatchPrefix]
        public static bool Prefix(AnimationEventsEmitter __instance, int stateShortNameHash)
        {
            if (_emitter == null || stateShortNameHash != _stateShortNameHash)
                return true;

            if (!ReferenceEquals(__instance, _emitter))
                return true;

            return false;
        }
    }
}

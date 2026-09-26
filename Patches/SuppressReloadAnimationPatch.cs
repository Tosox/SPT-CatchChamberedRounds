using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Swallows exactly one call to <c>FirearmsAnimator.Reload(bool)</c>.
    ///
    /// <c>ReloadInternalMagBase.Start</c> fires the Reload trigger whether or not the animator is
    /// already where it needs to be. When the catch is spliced into the reload the animator has
    /// just been crossfaded into RELOAD IN, and letting that trigger through would send it back to
    /// RELOAD START and replay the bolt opening the splice exists to skip.
    ///
    /// The flag is armed immediately before the reload operation is started and consumed by the
    /// very next call, so a reload from anywhere else is never affected.
    /// </summary>
    public class SuppressReloadAnimationPatch : ModulePatch
    {
        private static bool _skipNext;

        protected override MethodBase GetTargetMethod()
        {
            // Reload(bool), not the Reload(int, int, bool) overload that routes through it
            return AccessTools.Method(
                typeof(FirearmsAnimator),
                nameof(FirearmsAnimator.Reload),
                new[] { typeof(bool) }
            );
        }

        internal static void SkipNext()
        {
            _skipNext = true;
        }

        /// <summary>
        /// Drops a pending skip that was never used, so it cannot swallow an unrelated reload later.
        /// </summary>
        internal static void Clear()
        {
            _skipNext = false;
        }

        [PatchPrefix]
        public static bool Prefix()
        {
            if (!_skipNext)
                return true;

            _skipNext = false;
            return false;
        }
    }
}

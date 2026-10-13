using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class SuppressReloadAnimationPatch : ModulePatch
    {
        private static bool _skipNext;

        protected override MethodBase GetTargetMethod()
        {
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

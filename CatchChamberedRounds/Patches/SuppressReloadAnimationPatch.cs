using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class SuppressReloadAnimationPatch : ModulePatch
    {
        private static FirearmsAnimator _animator;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(FirearmsAnimator),
                nameof(FirearmsAnimator.Reload),
                new[] { typeof(bool) }
            );
        }

        internal static void SkipNext(FirearmsAnimator animator)
        {
            _animator = animator;
        }

        internal static void Clear()
        {
            _animator = null;
        }

        [PatchPrefix]
        public static bool Prefix(FirearmsAnimator __instance)
        {
            if (_animator == null || !ReferenceEquals(__instance, _animator))
                return true;

            _animator = null;
            return false;
        }
    }
}

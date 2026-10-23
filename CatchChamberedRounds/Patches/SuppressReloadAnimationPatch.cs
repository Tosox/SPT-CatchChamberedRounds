using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class SuppressReloadAnimationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(FirearmsAnimator),
                nameof(FirearmsAnimator.Reload),
                new[] { typeof(bool) }
            );
        }

        [PatchPrefix]
        public static bool Prefix(FirearmsAnimator __instance)
        {
            return !ReloadSplice.Consume(__instance);
        }
    }
}

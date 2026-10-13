using AnimationEventSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Tosox.CatchChamberedRounds.Patches
{
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

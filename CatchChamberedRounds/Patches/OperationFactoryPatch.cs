using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class OperationFactoryPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController),
                nameof(Player.FirearmController.GetOperationFactoryDelegates)
            );
        }

        [PatchPostfix]
        public static void Postfix(Player.FirearmController __instance,
            Dictionary<Type, Player.ItemHandsController.OperationFactoryDelegate> __result)
        {
            if (__result.ContainsKey(typeof(CatchAndReloadOperation)))
                return;

            __result[typeof(CatchAndReloadOperation)] =
                () => new CatchAndReloadOperation(__instance);

            __result[typeof(ObservedCatchOperation)] =
                () => new ObservedCatchOperation(__instance);
        }
    }
}

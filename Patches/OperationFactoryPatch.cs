using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Teaches the firearm controller how to build <see cref="CatchAndReloadOperation"/>.
    ///
    /// <c>InitiateOperation</c> looks the operation type up in this dictionary and would throw on a
    /// type it has never heard of. Patching the base method covers the networked controller too,
    /// because its own override calls back into this one.
    /// </summary>
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
        }
    }
}

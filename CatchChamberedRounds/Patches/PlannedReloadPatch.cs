using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class PlannedReloadPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController.FireOperationBase),
                nameof(Player.FirearmController.FireOperationBase.ReloadWithAmmo),
                new[] { typeof(AmmoPack), typeof(Callback), typeof(Callback) }
            );
        }

        [PatchPrefix]
        public static void Prefix(Player.FirearmController.FireOperationBase __instance, out bool __state)
        {
            __state = __instance._plannedAction == null;
        }

        [PatchPostfix]
        public static void Postfix(Player.FirearmController.FireOperationBase __instance, AmmoPack ammoPack,
                                   Callback finishCallback, Callback startCallback, bool __state)
        {
            var planned = __instance._plannedAction;

            // Only wrap an action this call created
            if (!__state || planned == null)
                return;

            // A reload pressed mid-shot is deferred, and the deferred call skips the patched idle operation
            __instance._plannedAction = () =>
            {
                // CanStartReload refuses while a planned action is set, so clear it first
                __instance._plannedAction = null;

                try
                {
                    if (ChamberDischarge.TryStart(__instance.Controller, ammoPack, finishCallback, startCallback))
                        return;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to start chamber discharge: {ex}");
                }

                planned();
            };
        }
    }
}

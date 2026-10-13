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

            // Only the action this call put there, never one that was already waiting
            if (!__state || planned == null)
                return;

            // A reload pressed while the weapon is still firing is deferred until the shot is over
            // and the deferred call starts the reload itself instead of passing the idle operation
            __instance._plannedAction = () =>
            {
                // CanStartReload still asks the fire operation, which refuses for as long as it holds
                // a planned action, so the action takes itself down before asking for a reload
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

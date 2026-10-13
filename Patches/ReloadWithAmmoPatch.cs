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
    public class ReloadWithAmmoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController),
                nameof(Player.FirearmController.ReloadWithAmmo)
            );
        }

        [PatchPrefix]
        public static bool Prefix(Player.FirearmController __instance, AmmoPack ammoPack, Callback callback)
        {
            try
            {
                // Skip the vanilla reload only while the discharge animation is taking over
                return !ChamberDischarge.TryStart(__instance, ammoPack, callback);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start chamber discharge: {ex}");
                return true;
            }
        }
    }
}

using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Intercepts the reload of an internal magazine weapon so the chambered round can be
    /// pulled out with the vanilla catch animation first. The reload is re-issued once the
    /// round is stowed, at which point it behaves like a reload of an already empty chamber.
    /// </summary>
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
        public static bool Prefix(Player.FirearmController __instance, AmmoPackReloadingClass ammoPack, Callback callback)
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

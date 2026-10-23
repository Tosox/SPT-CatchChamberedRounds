using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class ReloadAbortPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController.ReloadInternalMagWithOpenBoltOperation),
                nameof(Player.FirearmController.ReloadInternalMagWithOpenBoltOperation.HideWeapon),
                new[] { typeof(Action), typeof(bool), typeof(Item) }
            );
        }

        [PatchPostfix]
        public static void Postfix(Player.FirearmController.ReloadInternalMagWithOpenBoltOperation __instance, bool fastDrop)
        {
            // Putting the weapon away aborts the reload after the round in flight, which Fika only
            // tells the other clients about when the trigger caused it. Left unsaid they load the
            // whole magazine while this client loads one
            if (__instance.Player != null && __instance.Player.IsYourPlayer)
                CatchSync.ReloadAbortAnnouncer?.Invoke(__instance.Player.ProfileId, fastDrop);
        }
    }
}

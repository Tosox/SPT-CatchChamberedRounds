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
            // Fika only syncs trigger aborts. Without this, other clients would load the full magazine
            if (__instance.Player != null && __instance.Player.IsYourPlayer)
                CatchSync.ReloadAbortAnnouncer?.Invoke(__instance.Player.ProfileId, fastDrop);
        }
    }
}

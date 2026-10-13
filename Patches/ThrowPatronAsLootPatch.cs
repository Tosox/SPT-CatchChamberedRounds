using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class ThrowPatronAsLootPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Firearms),
                nameof(Firearms.ThrowPatronAsLoot)
            );
        }

        [PatchPrefix]
        public static bool Prefix(Item patron, IPlayer player, string source)
        {
            try
            {
                // Skip the vanilla throw only when the round is safely in the player's gear
                return !CartridgeCatcher.TryCatch(patron, player, source);
            }
            catch (Exception ex)
            {
                // Never let a mistake here eat a round or wedge a reload
                Logger.LogError($"Failed to catch cartridge from '{source}': {ex}");
                return true;
            }
        }
    }
}

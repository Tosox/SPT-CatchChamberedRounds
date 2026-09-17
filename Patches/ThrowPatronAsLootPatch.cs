using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Every place EFT spits a cartridge onto the ground funnels through
    /// <c>WeaponManagerClass.ThrowPatronAsLoot</c>, and each call site tags itself with a
    /// distinct <c>source</c> string. Matching on that tag catches the internal magazine
    /// reload without disturbing any of the other throws, and without reimplementing a
    /// single one of the reload state machines.
    ///
    /// This is the fallback. The reload is normally intercepted before it gets this far, so
    /// in practice only a round the discharge could not place reaches here.
    /// </summary>
    public class ThrowPatronAsLootPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(WeaponManagerClass),
                nameof(WeaponManagerClass.ThrowPatronAsLoot)
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

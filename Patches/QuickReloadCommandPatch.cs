using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    /// <summary>
    /// Flags EFT's own quick reload command while it runs.
    ///
    /// For an internal magazine weapon the quick reload path ends up calling the very same
    /// reload method as the normal one, synchronously and with nothing to distinguish it, so
    /// the only way to tell the two apart downstream is to notice the command going past here.
    /// </summary>
    public class QuickReloadCommandPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Class1730),
                nameof(Class1730.TranslateCommand)
            );
        }

        [PatchPrefix]
        public static void Prefix(ECommand command)
        {
            if (command == ECommand.QuickReloadWeapon)
                ChamberDischarge.QuickReloadRequested = true;
        }

        [PatchPostfix]
        public static void Postfix(ECommand command)
        {
            if (command == ECommand.QuickReloadWeapon)
                ChamberDischarge.QuickReloadRequested = false;
        }
    }
}

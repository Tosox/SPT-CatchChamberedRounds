using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Tosox.CatchChamberedRounds.Helpers;

namespace Tosox.CatchChamberedRounds.Patches
{
    public class QuickReloadCommandPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(FirearmHandsInputTranslator),
                nameof(FirearmHandsInputTranslator.TranslateCommand)
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

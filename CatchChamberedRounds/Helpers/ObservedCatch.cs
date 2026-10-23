using EFT;
using EFT.InventoryLogic;
using System;
using Tosox.CatchChamberedRounds.Configuration;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class ObservedCatch
    {
        internal static void Play(Player player, string roundId)
        {
            try
            {
                if (player == null || string.IsNullOrEmpty(roundId))
                    return;

                if (!(player.HandsController is Player.FirearmController controller))
                    return;

                var weapon = controller.Item;
                if (weapon == null || !weapon.HasChambers)
                    return;

                // Nothing to play if this client's copy of the weapon holds something else by now
                if (!(weapon.Chambers[0].ContainedItem is Ammo chambered) || chambered.Id != roundId)
                    return;

                // A non-null address is all the discharge reads to pick the catch animation over the
                // eject and it never moves the round itself
                var destination = player.InventoryController?.CreateItemAddress();
                if (destination == null)
                    return;

                // Read the same way the owner read it, so this client splices exactly where they do
                var profile = AnimatorProfile.Read(controller.FirearmsAnimator.UnityAnimator());

                controller.InitiateOperation<ObservedCatchOperation>()
                    .StartCatch(chambered, destination, profile);

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Playing the catch of {player.Profile?.Nickname} on this client");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to play an observed catch: {ex}");
            }
        }

        // Fika's own abort goes through SetTriggerPressed, which refuses until the first round is
        // in, so a swap before that one never reaches the other clients
        internal static void AbortReload(Player player, bool fastHide)
        {
            if (!(player?.HandsController is Player.FirearmController controller))
                return;

            if (!(controller.CurrentOperation is Player.FirearmController.ReloadInternalMagBase reload))
                return;

            // The override sets the abort flag and the base stashes the hide, so the reload also puts
            // the weapon away on its way out instead of finishing into an idle that waits for Fika
            reload.ReloadAborted = true;
            reload.HideWeapon(() => { }, fastHide);

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Cut the reload of {player.Profile?.Nickname} short on this client");
        }
    }
}

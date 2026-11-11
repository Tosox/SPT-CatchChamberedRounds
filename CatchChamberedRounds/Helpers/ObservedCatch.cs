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

                // This client's chamber may already hold a different round
                if (!(weapon.Chambers[0].ContainedItem is Ammo chambered) || chambered.Id != roundId)
                    return;

                // Read like the owner does, so both splice at the same point
                var profile = AnimatorProfile.Read(controller.FirearmsAnimator.UnityAnimator());

                controller.InitiateOperation<ObservedCatchOperation>()
                    .StartCatch(chambered, profile);

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Playing the catch of {player.Profile?.Nickname} on this client");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to play an observed catch: {ex}");
            }
        }

        // Fika's abort goes through SetTriggerPressed, which is ignored until the first round is in
        internal static void AbortReload(Player player, bool fastHide)
        {
            if (!(player?.HandsController is Player.FirearmController controller))
                return;

            if (!(controller.CurrentOperation is Player.FirearmController.ReloadInternalMagBase reload))
                return;

            // Also hide the weapon as the reload ends, instead of idling until Fika catches up
            reload.ReloadAborted = true;
            reload.HideWeapon(() => { }, fastHide);

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Cut the reload of {player.Profile?.Nickname} short on this client");
        }
    }
}

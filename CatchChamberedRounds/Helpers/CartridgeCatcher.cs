using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class CartridgeCatcher
    {
        private const string InternalMagBoltOpenSource = "ReloadInternalMagWithOpenBoltOperation.RemoveAmmoFromChamber";

        internal static bool TryCatch(Item patron, IPlayer player, string source)
        {
            if (patron == null || player == null)
                return false;

            if (player.IsAi())
                return false;

            if (source != InternalMagBoltOpenSource)
                return false;

            // Spent casings are litter, not ammo
            if (patron is Ammo ammo && ammo.IsUsed)
                return false;

            var controller = player.InventoryController;
            if (controller?.Inventory?.Equipment == null)
                return false;

            // Mirrored no matter what this client has configured, otherwise it would drop a round that
            // the player it belongs to kept
            if (!player.IsYourPlayer)
            {
                if (!CatchSync.TakeAnnouncement(player.ProfileId, patron.Id))
                {
                    if (Settings.VerboseLogging.Value)
                        Plugin.Log.LogInfo($"No catch announced for {player.Profile?.Nickname}, dropping the round");

                    return false;
                }

                var mirrored = InventoryPlacement.Commit(patron, controller);

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Mirrored the catch of {player.Profile?.Nickname}, stowed={mirrored}");

                return mirrored;
            }

            if (!Settings.Enabled.Value || CatchSync.Incompatible)
                return false;

            var name = patron.LocalizedName();

            // The player traded this round away for a faster reload
            if (ChamberDischarge.ConsumeBypass())
            {
                Notify($"Dropped {name}");
                return false;
            }

            if (!InventoryPlacement.Commit(patron, controller))
            {
                Notify($"Dropped {name}, nowhere to stow it");
                return false;
            }

            return true;
        }

        private static void Notify(string message)
        {
            if (!Settings.ShowNotification.Value)
                return;

            NotificationManager.DisplayMessageNotification(
                message,
                ENotificationDurationType.Default,
                ENotificationIconType.Default);
        }
    }
}

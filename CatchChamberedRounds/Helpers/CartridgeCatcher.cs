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
            if (!Settings.Enabled.Value || CatchSync.Incompatible || patron == null || player == null)
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
                return CatchSync.TakeAnnouncement(player.ProfileId, patron.Id)
                    && InventoryPlacement.Commit(patron, controller);

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

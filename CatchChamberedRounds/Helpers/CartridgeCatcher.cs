using EFT;
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

            if (patron is Ammo ammo && ammo.IsUsed)
                return false;

            var controller = player.InventoryController;
            if (controller?.Inventory?.Equipment == null)
                return false;

            // Mirror the owner regardless of local settings or this client drops a round they kept
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

            // A quick reload drops the round
            if (ChamberDischarge.ConsumeBypass())
                return false;

            return InventoryPlacement.Commit(patron, controller);
        }
    }
}

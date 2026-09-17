using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using Tosox.CatchChamberedRounds.Configuration;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class CartridgeCatcher
    {
        /// <summary>
        /// The tag EFT passes to <c>ThrowPatronAsLoot</c> from the internal magazine reload that
        /// has to open the bolt, which is the Mosin case this mod exists for. Every other throw
        /// the game makes, spent casings included, is deliberately left alone.
        /// </summary>
        private const string InternalMagBoltOpenSource = "ReloadInternalMagWithOpenBoltOperation.RemoveAmmoFromChamber";

        /// <summary>
        /// Tries to stow a cartridge EFT is about to throw on the ground.
        /// Returns true when the round was placed in the player's gear, in which case
        /// the caller must suppress the vanilla throw so the round is not duplicated.
        /// </summary>
        internal static bool TryCatch(Item patron, IPlayer player, string source)
        {
            if (!Settings.Enabled.Value || patron == null || player == null)
                return false;

            // Bots and remote players keep vanilla behaviour
            if (player.IsAI || !player.IsYourPlayer)
                return false;

            if (source != InternalMagBoltOpenSource)
                return false;

            // Spent casings are litter, not ammo
            if (patron is AmmoItemClass ammo && ammo.IsUsed)
                return false;

            var controller = player.InventoryController;
            if (controller?.Inventory?.Equipment == null)
                return false;

            // Read the name up front, the item may be merged away into an existing stack below
            var name = patron.LocalizedName();

            // The player traded this round away for a faster reload
            if (ChamberDischarge.ConsumeBypass())
            {
                Notify($"Dropped {name}");
                return false;
            }

            // No room anywhere: let it hit the floor rather than vanish
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

            NotificationManagerClass.DisplayMessageNotification(
                message,
                ENotificationDurationType.Default,
                ENotificationIconType.Default);
        }
    }
}

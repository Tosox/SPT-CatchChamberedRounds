using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using Tosox.CatchChamberedRounds.Configuration;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// Plays the vanilla "pull the round out of the chamber" animation before an internal
    /// magazine reload, then starts the reload once the round is safely stowed.
    ///
    /// The two animations cannot be blended: the reload clip drives its own operation through
    /// animation events, so hijacking the animator mid reload leaves the reload unable to
    /// finish. Running them back to back is the workable version, and it has the pleasant
    /// side effect that the reload then finds an empty chamber and never throws anything.
    /// </summary>
    internal static class ChamberDischarge
    {
        private static bool _chaining;
        private static bool _bypassArmed;

        /// <summary>
        /// Set for the duration of EFT's quick reload command, which reaches the reload call
        /// synchronously, so a reload arriving while this is true was asked for in a hurry.
        /// </summary>
        internal static bool QuickReloadRequested { get; set; }

        /// <summary>
        /// True when the player asked to skip the catch, so the round the reload is about to
        /// eject should be left to drop. Consumed once, by whichever throw it applies to.
        /// </summary>
        internal static bool ConsumeBypass()
        {
            if (!_bypassArmed)
                return false;

            _bypassArmed = false;
            return true;
        }

        internal static bool TryStart(Player.FirearmController controller, AmmoPackReloadingClass ammoPack, Callback callback)
        {
            // The chained reload must run as vanilla
            if (_chaining || controller == null || ammoPack == null)
                return false;

            if (!Settings.Enabled.Value)
                return false;

            // Only the weapons whose reload opens the bolt and spits the chambered round out
            var weapon = controller.Item;
            if (weapon == null || !weapon.MustBoltBeOpennedForInternalReload)
                return false;

            if (!weapon.HasChambers || weapon.ChamberAmmoCount <= 0)
                return false;

            // Leave vanilla to reject reloads it would not have started anyway
            var magazine = weapon.GetCurrentMagazine();
            if (magazine == null || controller.Blindfire || !controller.CanStartReload())
                return false;

            // A full magazine underneath a chambered round is the weapon at its true capacity,
            // and there is nothing left to load. Reloading regardless pulls the chambered round
            // out and then loads nothing, and EFT only chambers a fresh round when it actually
            // loaded one, so the gun ends up empty on a full magazine and cannot be fired.
            // Refuse the reload instead and leave the weapon exactly as it is.
            if (magazine.Count >= magazine.MaxCount || ammoPack.AmmoCount <= 0)
            {
                callback?.Fail("Weapon is already fully loaded");
                return true;
            }

            // Four in the magazine and one chambered is already the five rounds a reload can
            // leave you holding. Going through with it takes the chambered round out, feeds a
            // replacement in from your gear, and chambers that instead, so the count never
            // moves and the animation is spent for nothing. Refuse it unless asked not to.
            if (Settings.BlockPointlessReloads.Value &&
                magazine.Count + weapon.ChamberAmmoCount >= magazine.MaxCount)
            {
                callback?.Fail("Reloading would not add any rounds");
                return true;
            }

            // One key catches the round, the other reloads at vanilla speed and lets it drop.
            // Quick reload is the fast one by default, and inverting swaps which key is which.
            if (Settings.QuickReloadBypass.Value && QuickReloadRequested != Settings.InvertReloadKeys.Value)
            {
                _bypassArmed = true;
                return false;
            }

            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || !ReferenceEquals(player.HandsController, controller))
                return false;

            if (!(weapon.Chambers[0].ContainedItem is AmmoItemClass chambered) || chambered.IsUsed)
                return false;

            var inventory = player.InventoryController;
            if (inventory?.Inventory?.Equipment == null)
                return false;

            // With the animation off, stow the round right here, while it is still sitting in
            // the chamber slot and therefore still has a parent to be moved out of. Waiting for
            // the reload to eject it would hand us an item with no parent, which is a far more
            // awkward thing to place. The reload then finds an empty chamber and throws nothing.
            if (!Settings.PlayCatchAnimation.Value)
            {
                _bypassArmed = false;
                InventoryPlacement.Commit(chambered, inventory);
                return false;
            }

            // Check there is somewhere to put the round before committing to the animation,
            // otherwise the player would watch a catch animation and still lose it
            var preview = InventoryPlacement.Find(chambered, inventory, true);
            if (preview.Failed || preview.Value == null)
                return false;

            var destination = InventoryPlacement.AddressOf(preview.Value);
            if (destination == null)
                return false;

            // A non-null destination address is what makes the operation raise LoadOne, which
            // is the animator's catch-and-stow variant rather than a plain eject
            var discharge = controller.InitiateOperation<Player.FirearmController.GClass2024>();

            // Never let a stale bypass from an earlier reload leak into this one
            _bypassArmed = false;

            discharge.Start(chambered, 0, destination, new Callback(result =>
                OnDischarged(result, controller, inventory, chambered, ammoPack, callback)));

            return true;
        }

        private static void OnDischarged(IResult result, Player.FirearmController controller,
            InventoryController inventory, Item chambered, AmmoPackReloadingClass ammoPack, Callback callback)
        {
            if (result == null || result.Failed)
            {
                _bypassArmed = false;
                callback?.Fail("Chamber discharge did not complete");
                return;
            }

            // This runs from inside the operation, before it re-reads ChamberAmmoCount to
            // update the animator, which is exactly where vanilla completes the inventory move
            if (!InventoryPlacement.Commit(chambered, inventory))
            {
                callback?.Fail("No room left for the chambered round");
                return;
            }

            Plugin.Instance.StartCoroutine(ReloadNextFrame(controller, ammoPack, callback));
        }

        private static IEnumerator ReloadNextFrame(Player.FirearmController controller, AmmoPackReloadingClass ammoPack, Callback callback)
        {
            // Let the discharge operation finish unwinding into idle before replacing it
            yield return null;

            _chaining = true;
            try
            {
                controller.ReloadWithAmmo(ammoPack, callback);
            }
            finally
            {
                _chaining = false;
            }
        }
    }
}

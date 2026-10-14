using AnimationSystem;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Patches;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class ChamberDischarge
    {
        private static bool _chaining;
        private static bool _bypassArmed;

        internal static bool QuickReloadRequested { get; set; }

        internal static bool ConsumeBypass()
        {
            if (!_bypassArmed)
                return false;

            _bypassArmed = false;
            return true;
        }

        private static bool Decline(string reason)
        {
            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Catch declined: {reason}");

            return false;
        }

        internal static bool TryStart(Player.FirearmController controller, AmmoPack ammoPack, Callback callback,
                                      Callback startCallback)
        {
            if (!Settings.Enabled.Value)
                return false;

            if (_chaining)
                return false;

            if (controller == null || ammoPack == null)
                return Decline("no controller or no ammo pack");

            // Only the weapons whose reload opens the bolt and spits the chambered round out
            var weapon = controller.Item;
            if (weapon == null || !weapon.MustBoltBeOpennedForInternalReload)
                return Decline("weapon does not open its bolt for an internal reload");

            var player = controller._player;
            if (player == null || !player.IsYourPlayer)
                return Decline($"reload is not the local player's (player={player != null})");

            // Puts the animator's LoadOne flag back down, which otherwise stays up for as long as
            // the weapon is in hand and wedges an empty one into an uninterruptible load-a-round loop
            controller.FirearmsAnimator?.LoadOneTrigger(false);

            if (!weapon.HasChambers || weapon.ChamberAmmoCount <= 0)
                return Decline($"nothing chambered (HasChambers={weapon.HasChambers}, ChamberAmmoCount={weapon.ChamberAmmoCount})");

            if (CatchSync.Incompatible)
                return Decline("another client in the raid handles the chambered round differently");

            // Leave vanilla to reject reloads it would not have started anyway
            var magazine = weapon.GetCurrentMagazine();
            if (magazine == null || controller.Blindfire || !controller.CanStartReload())
                return Decline($"vanilla would not start this reload (magazine={magazine != null}, blindfire={controller.Blindfire})");

            if (magazine.Count >= magazine.MaxCount || ammoPack.AmmoCount <= 0)
            {
                callback?.Fail("Weapon is already fully loaded");
                return true;
            }

            if (Settings.BlockPointlessReloads.Value && magazine.Count + weapon.ChamberAmmoCount >= magazine.MaxCount)
            {
                callback?.Fail("Reloading would not add any rounds");
                return true;
            }

            if (Settings.QuickReloadBypass.Value && QuickReloadRequested != Settings.InvertReloadKeys.Value)
            {
                _bypassArmed = true;
                return Decline($"reload key asked to skip the catch (quickReload={QuickReloadRequested}, invert={Settings.InvertReloadKeys.Value})");
            }

            if (!(weapon.Chambers[0].ContainedItem is Ammo chambered) || chambered.IsUsed)
                return Decline($"chamber holds no live round (contained={weapon.Chambers[0].ContainedItem?.GetType().Name ?? "null"})");

            var inventory = player.InventoryController;
            if (inventory?.Inventory?.Equipment == null)
                return Decline("no inventory to place the round into");

            // Read the weapon's animator profile to prepare for the splice
            var profile = AnimatorProfile.Read(
                (controller.FirearmsAnimator?.Animator as UnityAnimatorWrapper)?.Animator);

            // A weapon the splice cannot read is not an error, it just plays both animations in sequence
            if (Settings.VerboseLogging.Value && profile?.FeedStateProblem != null)
                Plugin.Log.LogInfo($"Will not splice {weapon.LocalizedName()}: {profile.FeedStateProblem}");

            // With the animation off, stow the round right here
            if (!Settings.PlayCatchAnimation.Value)
            {
                _bypassArmed = false;

                if (InventoryPlacement.Commit(chambered, inventory))
                    CatchSync.CatchAnnouncer?.Invoke(player.ProfileId, chambered.Id);

                return Decline("catch animation turned off, round stowed silently");
            }

            // Check there is somewhere to put the round before committing to the animation
            var preview = InventoryPlacement.Find(chambered, inventory, true);
            if (preview.Failed || preview.Value == null)
                return Decline($"nowhere to put the round ({preview.Error?.ToString() ?? "no result"})");

            // Without an address the discharge plays its eject variant and throws the round
            var destination = InventoryPlacement.AddressOf(preview.Value);
            if (destination == null)
                return Decline($"could not read an address out of a {preview.Value.GetType().FullName}");

            // Never let a stale bypass from an earlier reload leak into this one
            _bypassArmed = false;

            // Never let a skip armed by an abandoned splice swallow this reload's animation
            SuppressReloadAnimationPatch.Clear();

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Catch starting: discharging {chambered.LocalizedName()} to {destination}");

            var discharge = controller.InitiateOperation<CatchAndReloadOperation>();
            discharge.StartCatch(chambered, destination, inventory, ammoPack, profile, callback, startCallback);

            return true;
        }

        internal static void ChainReload(IResult result, Player.FirearmController controller, InventoryController inventory,
                                         Item chambered, AmmoPack ammoPack, Callback callback)
        {
            // The catch is done and a reload follows instead of the chambering that would normally
            // lower the flag and the reload drives its own trigger without ever touching this one
            controller?.FirearmsAnimator?.LoadOneTrigger(false);

            if (result == null || result.Failed)
            {
                _bypassArmed = false;
                Plugin.Log.LogWarning($"Chamber discharge did not complete: {result?.Error?.ToString() ?? "no result"}");
                callback?.Fail("Chamber discharge did not complete");
                return;
            }

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo("Catch animation finished, chaining the reload");

            // This runs from inside the operation, before it re-reads ChamberAmmoCount to
            // update the animator, which is exactly where vanilla completes the inventory move
            if (!InventoryPlacement.Commit(chambered, inventory))
            {
                callback?.Fail("No room left for the chambered round");
                return;
            }

            // Tell the other clients to catch this round as well
            CatchSync.CatchAnnouncer?.Invoke(controller._player.ProfileId, chambered.Id);

            Plugin.Instance.StartCoroutine(ReloadNextFrame(controller, ammoPack, callback));
        }

        private static IEnumerator ReloadNextFrame(Player.FirearmController controller, AmmoPack ammoPack, Callback callback)
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

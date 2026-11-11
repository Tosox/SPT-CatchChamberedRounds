using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class ChamberDischarge
    {
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

            if (controller == null || ammoPack == null)
                return Decline("no controller or no ammo pack");

            var player = controller._player;
            if (player == null || !player.IsYourPlayer)
                return Decline($"reload is not the local player's (player={player != null})");

            var weapon = controller.Item;
            if (weapon == null || !weapon.MustBoltBeOpennedForInternalReload)
                return Decline("weapon does not open its bolt for an internal reload");

            if (!weapon.HasChambers || weapon.ChamberAmmoCount <= 0)
                return Decline($"nothing chambered (HasChambers={weapon.HasChambers}, ChamberAmmoCount={weapon.ChamberAmmoCount})");

            if (CatchSync.Incompatible)
                return Decline("another client in the raid handles the chambered round differently");

            // Let vanilla handle reloads it would refuse anyway
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

            if (QuickReloadRequested != Settings.CatchOnQuickReload.Value)
            {
                _bypassArmed = true;
                return Decline($"reload key asked to skip the catch (quickReload={QuickReloadRequested}, catchOnQuickReload={Settings.CatchOnQuickReload.Value})");
            }

            if (!(weapon.Chambers[0].ContainedItem is Ammo chambered) || chambered.IsUsed)
                return Decline($"chamber holds no live round (contained={weapon.Chambers[0].ContainedItem?.GetType().Name ?? "null"})");

            var inventory = player.InventoryController;
            if (inventory?.Inventory?.Equipment == null)
                return Decline("no inventory to place the round into");

            var profile = AnimatorProfile.Read(controller.FirearmsAnimator.UnityAnimator());

            // Not an error, that weapon just plays both animations in sequence
            if (Settings.VerboseLogging.Value && profile?.FeedStateProblem != null)
                Plugin.Log.LogInfo($"Will not splice {weapon.LocalizedName()}: {profile.FeedStateProblem}");

            if (!Settings.PlayCatchAnimation.Value)
            {
                _bypassArmed = false;

                if (InventoryPlacement.Commit(chambered, inventory))
                    CatchSync.CatchAnnouncer?.Invoke(player.ProfileId, chambered.Id);

                return Decline("catch animation turned off, round stowed silently");
            }

            // Make sure the round fits before starting the animation
            var preview = InventoryPlacement.Find(chambered, inventory, true);
            if (preview.Failed || preview.Value == null)
                return Decline($"nowhere to put the round ({preview.Error?.ToString() ?? "no result"})");

            // Clear a bypass left over from an earlier reload
            _bypassArmed = false;

            // Clear a splice left armed by an abandoned catch
            ReloadSplice.Clear();

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Catch starting: discharging {chambered.LocalizedName()}");

            var discharge = controller.InitiateOperation<CatchAndReloadOperation>();
            discharge.StartCatch(chambered, inventory, ammoPack, profile, callback, startCallback);

            CatchSync.AnimationCue?.Invoke(player.ProfileId, chambered.Id);

            return true;
        }

        internal static void ChainReload(IResult result, Player.FirearmController controller, InventoryController inventory,
                                         Item chambered, AmmoPack ammoPack, Callback callback,
                                         Callback startCallback)
        {
            if (result == null || result.Failed)
            {
                _bypassArmed = false;
                Plugin.Log.LogWarning($"Chamber discharge did not complete: {result?.Error?.ToString() ?? "no result"}");
                callback?.Fail("Chamber discharge did not complete");
                return;
            }

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo("Catch animation finished, chaining the reload");

            // Runs inside the operation before it re-reads ChamberAmmoCount, where vanilla moves it too
            if (!InventoryPlacement.Commit(chambered, inventory))
            {
                callback?.Fail("No room left for the chambered round");
                return;
            }

            CatchSync.CatchAnnouncer?.Invoke(controller._player.ProfileId, chambered.Id);

            // Covers the frame between the catch dropping its mark and the reload taking one
            WeaponMark.Raise(controller);

            Plugin.Instance.StartCoroutine(ReloadNextFrame(controller, ammoPack, callback, startCallback));
        }

        private static IEnumerator ReloadNextFrame(Player.FirearmController controller, AmmoPack ammoPack, Callback callback,
                                                   Callback startCallback)
        {
            // Let the discharge finish into idle first
            yield return null;

            try
            {
                // Not controller.ReloadWithAmmo, Fika refuses that while its own weapon swap is pending
                var reload = controller.InitiateOperation<Player.FirearmController.ReloadInternalMagBase>();
                reload.Start(ammoPack, callback);

                startCallback?.Succeed();
            }
            finally
            {
                // The reload holds its own mark from here
                WeaponMark.Lower(controller);
            }
        }
    }
}
